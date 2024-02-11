

using ComputeSharp;
using ParticlePhysics.Internal;
using ProtoEngine;

public class Grid
{
    // grid buffers
    public ReadWriteBuffer<int2> gridKeysValues; // x = grid cell index, y = particle index
    public ReadWriteBuffer<int> gridCounts; // number of particles in each grid cell
    public ReadWriteBuffer<int> gridStarts; // starting index of each grid cell in the gridKeysValues buffer

    public int2 extents;
    public int2 cellCount;
    public int cellCountLinear;
    public float2 cellSize;

    public Grid(int2 extents, float cellSize, int maxItems)
    {
        Initialize(extents, cellSize, maxItems);
    }

    public Grid()
    {
    }

    public void Initialize(Vector2 extents, float cellSize, int maxItems)
    {
        this.extents = extents;
        this.cellSize = cellSize;
        cellCount = new int2((int)(extents.X / cellSize), (int)(extents.Y / cellSize));
        cellCountLinear = cellCount.X * cellCount.Y;

        gridKeysValues = Application.GPU.AllocateReadWriteBuffer<int2>(maxItems);
        gridCounts = Application.GPU.AllocateReadWriteBuffer<int>(cellCountLinear);
        gridStarts = Application.GPU.AllocateReadWriteBuffer<int>(cellCountLinear);
    }

    public void BuildGrid(ReadWriteBuffer<float2> positions, ReadWriteBuffer<int> active, in ComputeContext context)
    {
        context.Clear(gridCounts);
        context.For(positions.Length, 1, 1, 1024, 1, 1, new BuildGridKernel(positions, active, gridCounts, gridKeysValues, extents, cellCount, cellCountLinear, cellSize));
        BitonicSort.Sort(gridKeysValues, in context);
        context.For(positions.Length, 1, 1, 1024, 1, 1, new BitonicOffsetKernel(gridKeysValues, gridStarts));
    }
}