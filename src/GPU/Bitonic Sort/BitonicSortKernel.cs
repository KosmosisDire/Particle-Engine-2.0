using ComputeSharp;



[AutoConstructor]
internal readonly partial struct BitonicSortKernel : IComputeShader
{
    public readonly ReadWriteBuffer<int2> keyValueBuffer;

    public readonly int groupWidth;
    public readonly int groupHeight;
    public readonly int stepIndex;

    // Sort the given entries by their keys (smallest to largest)
    // This is done using bitonic merge sort, and takes multiple iterations
    public void Execute()
    {
        int i = ThreadIds.X;
        int hIndex = i & (groupWidth - 1);
        int indexLeft = hIndex + (groupHeight + 1) * (i / groupWidth);
        int rightStepSize = stepIndex == 0 ? groupHeight - 2 * hIndex : (groupHeight + 1) / 2;
        int indexRight = indexLeft + rightStepSize;

        // Exit if out of bounds (for non-power of 2 input sizes)
        if (indexRight >= keyValueBuffer.Length) return;

        int2 left = keyValueBuffer[indexLeft];
        int2 right = keyValueBuffer[indexRight];

        // Swap entries if value is descending
        if (left.X > right.X)
        {
            keyValueBuffer[indexLeft] = right;
            keyValueBuffer[indexRight] = left;
        }
    }
}