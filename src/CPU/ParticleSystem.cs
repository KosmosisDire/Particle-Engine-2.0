
using ComputeSharp;
using ParticlePhysics.Internal;
using ProtoEngine;
using ProtoEngine.Rendering;
using SFML.Graphics;

namespace ParticleEngine;

public class ParticleSystem: IDisposable
{
    public int Count { get; private set; }

    private int _capacity;
    public int Capacity
    {
        get => _capacity;
        set
        {
            _capacity = value;
            IDPool.Resize(value);
            ReinitBuffers();
        }
    }
    
    private int _maxParticles;
    public int MaxParticles
    {
        get => _maxParticles;
        set
        {
            _maxParticles = value;
            if (Capacity > _maxParticles) Capacity = _maxParticles;
            else
            {
                ReinitLinkBuilder();
                ReinitGrid();
            }
        }
    }

    private float _radius;
    public float Radius
    {
        get => _radius;
        set
        {
            _radius = value;
            ReinitGrid();
        }
    }

    private Vector2 _boundsSize;
    public Vector2 BoundsSize
    {
        get => _boundsSize;
        set
        {
            _boundsSize = value;
            ReinitGrid();
        }
    }

    public Vector2 gravity = new(0, 0);
    public int iterations = 2;

    public Grid Grid { get; private set; }
    public IDPool IDPool { get; private set; }
    private LinkBuilder LinkBuilder { get; set; }

    // particle buffers
    public ReadWriteBuffer<float2> positions;
    private ReadWriteBuffer<int2> positionsInt;
    private ReadWriteBuffer<float2> lastPositions;
    private ReadWriteBuffer<float> travelDistances;
    private ReadWriteBuffer<int> active; // each bit of each uint represents active of inactive
    private ReadWriteBuffer<uint> colors; // 4 bytes ber uint (RGBA)

    // particle update buffers
    private ReadOnlyBuffer<Particle> particleUpdate;
    private UploadBuffer<Particle> particleUpload;
    private int uploadCount = 0;
    private const int maxUploadCount = 15000;

    public ParticleSystem(int maxParticles, float radius, Vector2 boundsSize, bool preallocate = false)
    {
        this._capacity = preallocate ? maxParticles : (int)MathF.Min(10, maxParticles);
        this._maxParticles = maxParticles;
        this._radius = radius;
        this._boundsSize = boundsSize;
        IDPool = new IDPool(this.Capacity, true);
        ReinitBuffers();
    }

    public void Update(float dt)
    {
        if (updatingBuffers) return;

        using var GPU = Application.GPU.CreateComputeContext();
        
        if (uploadCount > 0)
        {
            particleUpdate.CopyFrom(particleUpload);

            var updateKernel = new UpdateParticlesKernel
            (
                particleUpdate, positions, positionsInt, lastPositions, active, colors,
                LinkBuilder!.active, LinkBuilder.keyValuePairs, LinkBuilder.counts, LinkBuilder.starts
            );
            var threads = (int)MathF.Min(uploadCount, 1024);
            GPU.For(uploadCount, 1, 1, threads, 1, 1, updateKernel);
            uploadCount = 0;
        }

        if (Count > 0)
        {
            if (LinkBuilder.count != 0)
            {
                LinkBuilder.BuildLinks(in GPU);
            }

            Grid.BuildGrid(positions, active, in GPU);
            var kernel = new ParticlePhysicsKernel
            (
                positions, positionsInt, lastPositions, travelDistances, active, colors,
                Radius,
                Grid.gridKeysValues, Grid.gridCounts, Grid.gridStarts,
                Grid.extents, Grid.cellCount, Grid.cellCountLinear, Grid.cellSize,
                LinkBuilder.pairs, LinkBuilder.lengths, LinkBuilder.strain, LinkBuilder.active,
                LinkBuilder.keyValuePairs, LinkBuilder.counts, LinkBuilder.starts,
                dt, gravity, iterations
            );

            var threads = (int)MathF.Min(Count, 1024);
            GPU.For(Count, 1, 1, threads, 1, 1, kernel);
        }
    }

    public int? AddParticle(Vector2 position, Vector2 velocity, Color color)
    {
        if (uploadCount > particleUpload.Span.Length - 1)
        {
            return null;
        }

        if (Count >= Capacity - maxUploadCount && Capacity < MaxParticles)
        {
            Capacity = (int)MathF.Min(Count * 1.5f + maxUploadCount, MaxParticles);
        }

        var id = IDPool.NextID();
        var particle = new Particle(position, position - velocity, color.ToUInt32(), id);
        particleUpload.Span[uploadCount] = particle;
        uploadCount++;

        Count = (int)MathF.Min(Count + 1, MaxParticles);

        return id;
    }

    public void RemoveParticle(int id)
    {
        if (Count == 0) throw new Exception("Cannot remove particle from empty system");

        IDPool.FreeID(id);
        particleUpload.Span[uploadCount] = new Particle(id);
        Count = (int)MathF.Max(Count - 1, 0);
        uploadCount++;
    }

    public int AddLink(int id1, int id2, float length)
    {
        if (id1 < 0 || id1 >= Capacity || id2 < 0 || id2 >= Capacity) throw new Exception("Invalid particle id");
        return LinkBuilder.CreateLink(id1, id2, length + Radius * 2);
    }

    private void ReinitGrid()
    {
        Grid ??= new Grid();
        Grid?.Initialize(BoundsSize, Radius * 3, Capacity);
    }

    private void ReinitLinkBuilder()
    {
        LinkBuilder ??= new LinkBuilder();
        LinkBuilder?.Resize(MaxParticles * 3, MaxParticles);
    }

    private (float2[] positions, int2[] positionsInt, float2[] lastPositions, int[] active, uint[] colors) ReadBackParticles()
    {
        var positionsArr = new float2[positions.Length];
        var positionsIntArr = new int2[positions.Length];
        var lastPositionsArr = new float2[positions.Length];
        var activeArr = new int[positions.Length];
        var colorsArr = new uint[positions.Length];

        positions.CopyTo(positionsArr);
        positionsInt.CopyTo(positionsIntArr);
        lastPositions.CopyTo(lastPositionsArr);
        active.CopyTo(activeArr);
        colors.CopyTo(colorsArr);

        return (positionsArr, positionsIntArr, lastPositionsArr, activeArr, colorsArr);
    }

    private void AllocateBuffers()
    {
        positions = Application.GPU.AllocateReadWriteBuffer<float2>(Capacity, AllocationMode.Clear);
        positionsInt = Application.GPU.AllocateReadWriteBuffer<int2>(Capacity, AllocationMode.Clear);
        lastPositions = Application.GPU.AllocateReadWriteBuffer<float2>(Capacity, AllocationMode.Clear);
        travelDistances = Application.GPU.AllocateReadWriteBuffer<float>(Capacity, AllocationMode.Clear);
        active = Application.GPU.AllocateReadWriteBuffer<int>(Capacity, AllocationMode.Clear);
        colors = Application.GPU.AllocateReadWriteBuffer<uint>(Capacity, AllocationMode.Clear);
        
        particleUpdate = Application.GPU.AllocateReadOnlyBuffer<Particle>(maxUploadCount, AllocationMode.Clear);
        particleUpload = Application.GPU.AllocateUploadBuffer<Particle>(maxUploadCount, AllocationMode.Clear);
    }

    bool updatingBuffers = false;
    private void ReinitBuffers()
    {
        updatingBuffers = true;

        if (positions != null)
        {
            positions = positions!.Resize(Capacity);
            positionsInt = positionsInt.Resize(Capacity);
            lastPositions = lastPositions.Resize(Capacity);
            travelDistances = travelDistances.Resize(Capacity);
            active = active.Resize(Capacity);
            colors = colors.Resize(Capacity);
        }
        else
        {
            AllocateBuffers();
            ReinitLinkBuilder();
        }

        ReinitGrid();

        updatingBuffers = false;
    }

    public void Draw(Window window)
    {
        window.DrawCircles(positions, colors, active, Radius);
        // LinkBuilder.Draw(positions, window, new Color(35, 160, 117, 100));

        var starts = new Vector2[4]{new(0, 0), new(BoundsSize.X, 0), new(BoundsSize.X, BoundsSize.Y), new(0, BoundsSize.Y)};
        var ends = new Vector2[4]{new(BoundsSize.X, 0), new(BoundsSize.X, BoundsSize.Y), new(0, BoundsSize.Y), new(0, 0)};
        window.DrawLines(starts, ends, new Color(255, 255, 255, 100));
    }

    public void Dispose()
    {
        lock(this)
        {
            positions?.Dispose();
            lastPositions?.Dispose();
            active?.Dispose();
            colors?.Dispose();
            particleUpdate?.Dispose();
            particleUpload?.Dispose();
        }
    }

    ~ParticleSystem()
    {
        Dispose();
    }
}