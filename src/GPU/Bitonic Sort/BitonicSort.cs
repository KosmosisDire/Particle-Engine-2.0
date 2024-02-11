using System.Numerics;
using ComputeSharp;

public static class BitonicSort
{
    public static void Sort(ReadWriteBuffer<int2> keyValueBuffer, in ComputeContext context)
    {
        // Launch each step of the sorting algorithm (once the previous step is complete)
        // Number of steps = [log2(n) * (log2(n) + 1)] / 2
        // where n = nearest power of 2 that is greater or equal to the number of inputs
        int numStages = (int)MathF.Log(BitOperations.RoundUpToPowerOf2((uint)keyValueBuffer.Length), 2);

        for (int stageIndex = 0; stageIndex < numStages; stageIndex++)
        {
            for (int stepIndex = 0; stepIndex < stageIndex + 1; stepIndex++)
            {
                // Calculate some pattern stuff
                int groupWidth = 1 << (stageIndex - stepIndex);
                int groupHeight = 2 * groupWidth - 1;
                int iterations = (int)(BitOperations.RoundUpToPowerOf2((uint)keyValueBuffer.Length) / 2);
                
                // Launch the kernel
                var threads = (int)MathF.Min(iterations, 1024);
                context.For(iterations, 1, 1, threads, 1, 1, new BitonicSortKernel(keyValueBuffer, groupWidth, groupHeight, stepIndex));
                context.Barrier(keyValueBuffer);
            }
        }
    }

    public static void CalculateOffsets(ReadWriteBuffer<int2> keyValueBuffer, ReadWriteBuffer<int> offsets, ReadWriteBuffer<int> counts, in ComputeContext context)
    {
        var threads = (int)MathF.Min(keyValueBuffer.Length, 1024);
        context.For(keyValueBuffer.Length, 1, 1, threads, 1, 1, new BitonicOffsetKernel(keyValueBuffer, offsets));
        context.Barrier(offsets);
    }

}