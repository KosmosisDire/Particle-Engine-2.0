using ComputeSharp;
using ComputeSharp.Resources;
using ProtoEngine;

public static class GPUTools
{
    private static StructuredBuffer<T> Resize<T>(this StructuredBuffer<T> buffer, int newSize) where T : unmanaged
    {
        if (buffer.Length == newSize) return buffer;
        var array = new T[buffer.Length];
        buffer.CopyTo(array);
        Array.Resize(ref array, newSize);
        return Application.GPU.AllocateReadWriteBuffer(array);
    }

    public static ReadWriteBuffer<T> Resize<T>(this ReadWriteBuffer<T> buffer, int newSize) where T : unmanaged
    {
        return (ReadWriteBuffer<T>)Resize((StructuredBuffer<T>)buffer, newSize);
    }

    public static ReadOnlyBuffer<T> Resize<T>(this ReadOnlyBuffer<T> buffer, int newSize) where T : unmanaged
    {
        return (ReadOnlyBuffer<T>)Resize((StructuredBuffer<T>)buffer, newSize);
    }

    public static void DebugViewBuffer<T>(this StructuredBuffer<T> buffer) where T : unmanaged
    {
        var array = new T[buffer.Length];
        buffer.CopyTo(array);
        Debug.Break();
    }
}