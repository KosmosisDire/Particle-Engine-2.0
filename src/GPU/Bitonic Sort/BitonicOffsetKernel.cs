using ComputeSharp;

[AutoConstructor]
internal readonly partial struct BitonicOffsetKernel : IComputeShader
{
    public readonly ReadWriteBuffer<int2> keyValueBuffer;
    public readonly ReadWriteBuffer<int> offsets;

    public void Execute()
    {
        int id = ThreadIds.X;

        if (id >= keyValueBuffer.Length) return;

        int key = keyValueBuffer[id].X;
        int keyPrev = id == 0 ? 9999999 : keyValueBuffer[id-1].X;
        if (key != keyPrev)
        {
            offsets[key] = id;
        }
    }
}