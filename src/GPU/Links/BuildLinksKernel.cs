using ComputeSharp;

[AutoConstructor]
internal readonly partial struct BuildLinksKernel : IComputeShader
{
    public readonly ReadWriteBuffer<int2> pairs; // these two have length maxLinks
    public readonly ReadWriteBuffer<int> active;

    public readonly ReadWriteBuffer<int2> linkKeysValues; // these three have length maxParticles
    public readonly ReadWriteBuffer<int> linkCounts;

    private bool HasBit(int packed, int bit)
    {
        return (packed & (1u << bit)) != 0;
    }

    public void Execute() // for each link
    {
        int id = ThreadIds.X;

        bool isActive = HasBit(active[id / 32], id % 32);

        if (!isActive) return;

        int p1 = pairs[id].X;
        int p2 = pairs[id].Y;

        linkKeysValues[id * 2] = new int2(p1, id);
        linkKeysValues[id * 2 + 1] = new int2(p2, id);
        Hlsl.InterlockedAdd(ref linkCounts[p1], 1);
        Hlsl.InterlockedAdd(ref linkCounts[p2], 1);
    }
        
}


