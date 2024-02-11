using ComputeSharp;

[AutoConstructor]
internal readonly partial struct UpdateLinksKernel : IComputeShader
{
    public readonly ReadOnlyBuffer<Link> linkData;

    public readonly ReadWriteBuffer<int2> linkPairs;
    public readonly ReadWriteBuffer<float> linkLengths;
    public readonly ReadWriteBuffer<int> strain;
    public readonly ReadWriteBuffer<int> active;

    public void Execute()
    {
        int newID = ThreadIds.X;

        var data = linkData[newID];

        int id = data.ID;

        linkPairs[id] = new int2(data.particle1, data.particle2);
        linkLengths[id] = data.length;
        strain[id] = 0;
        
        int packedIndex = (int)Hlsl.Floor(id / 32f);
        Hlsl.InterlockedOr(ref active[packedIndex], (int)(1u << (int)((float)id % 32)));
    }
}