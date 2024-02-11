using ComputeSharp;
using ProtoEngine;
using ProtoEngine.Rendering;
using SFML.Graphics;

public class LinkBuilder
{
    // link buffers
    public ReadWriteBuffer<int2> pairs;
    public ReadWriteBuffer<float> lengths;
    public ReadWriteBuffer<int> strain;
    public ReadWriteBuffer<int> active; // each bit represents a link, not each int

    public ReadWriteBuffer<int2> keyValuePairs;
    public ReadWriteBuffer<int> counts;
    public ReadWriteBuffer<int> starts;

    // link upload
    public ReadOnlyBuffer<Link> updateBuffer;
    public UploadBuffer<Link> linkUpload;
    public int uploadIndex = 0;

    public IDPool linkIDPool { get; private set; } = new(0, true);

    public int maxLinks;
    public int maxParticles;
    public int count;

    public LinkBuilder(int maxLinks, int maxParticles)
    {
        this.count = 0;
        Resize(maxLinks, maxParticles);
    }

    public LinkBuilder()
    {
        this.count = 0;
    }

    public void AllocateBuffers()
    {
        pairs = Application.GPU.AllocateReadWriteBuffer<int2>(maxLinks, AllocationMode.Clear);
        lengths = Application.GPU.AllocateReadWriteBuffer<float>(maxLinks, AllocationMode.Clear);
        strain = Application.GPU.AllocateReadWriteBuffer<int>(maxLinks, AllocationMode.Clear);
        active = Application.GPU.AllocateReadWriteBuffer<int>(maxLinks, AllocationMode.Clear);

        int2[] defaultKeysValues = Enumerable.Repeat(new int2(int.MaxValue, 0), maxParticles * 2).ToArray();
        keyValuePairs = Application.GPU.AllocateReadWriteBuffer<int2>(defaultKeysValues);
        counts = Application.GPU.AllocateReadWriteBuffer<int>(maxParticles, AllocationMode.Clear);
        starts = Application.GPU.AllocateReadWriteBuffer<int>(maxParticles, AllocationMode.Clear);

        updateBuffer = Application.GPU.AllocateReadOnlyBuffer<Link>(30000, AllocationMode.Clear);
        linkUpload = Application.GPU.AllocateUploadBuffer<Link>(30000, AllocationMode.Clear);
    }

    public void Resize(int maxLinks, int maxParticles)
    {
        this.maxLinks = maxLinks;
        this.maxParticles = maxParticles;
        linkIDPool.Resize(maxLinks);

        if (pairs == null)
        {
            AllocateBuffers();
        }
        else
        {
            pairs = pairs.Resize(maxLinks);
            lengths = lengths.Resize(maxLinks);
            strain = strain.Resize(maxLinks);
            active = active.Resize(maxLinks);

            keyValuePairs = keyValuePairs.Resize(maxParticles * 2);
            counts = counts.Resize(maxParticles);
            starts = starts.Resize(maxParticles);

            updateBuffer = Application.GPU.AllocateReadOnlyBuffer<Link>(15000, AllocationMode.Clear);
            linkUpload = Application.GPU.AllocateUploadBuffer<Link>(15000, AllocationMode.Clear);
        }
    }

    public int CreateLink(int particle1, int particle2, float length)
    {
        int id = linkIDPool.NextID();
        linkUpload.Span[uploadIndex++] = new Link(particle1, particle2, length, id);
        count++;
        return id;
    }

    public void RemoveLink(int id)
    {
        linkIDPool.FreeID(id);
        count--;
    }

    public void BuildLinks(in ComputeContext context)
    {
        if (uploadIndex > 0)
        {
            // upload new links
            updateBuffer.CopyFrom(linkUpload);
            context.For(uploadIndex, 1, 1, 1024, 1, 1, new UpdateLinksKernel(updateBuffer, pairs, lengths, strain, active));
            uploadIndex = 0;
        }

        context.Clear(counts);
        var threads = (int)MathF.Min(maxLinks, 1024);
        context.For(maxLinks, 1, 1, threads, 1, 1, new BuildLinksKernel(pairs, active, keyValuePairs, counts));
        BitonicSort.Sort(keyValuePairs, in context);
        BitonicSort.CalculateOffsets(keyValuePairs, starts, counts, in context);
    }

    public void Draw(ReadWriteBuffer<float2> particlePositions, Window window, Color color)
    {
        var kernel = new DrawLinksKernel
        (
            pairs, active, particlePositions, 
            window.ScreenBuffer, window.Size, 
            window.ActiveCamera!.RectBoundsWorld, (window.ActiveCamera?.scale * window.RenderSprite.Scale.X) ?? 1, 
            color.ToUInt32()
        );

        var threads = (int)MathF.Min(maxLinks, 1024);
        Application.GPU.For(maxLinks, 1, 1, threads, 1, 1, kernel);
    }
}