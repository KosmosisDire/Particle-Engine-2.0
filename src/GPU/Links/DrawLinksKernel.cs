using ComputeSharp;
using ParticlePhysics.Internal;

[AutoConstructor]
internal readonly partial struct DrawLinksKernel : IComputeShader
{
    public readonly ReadWriteBuffer<int2> pairs;
    public readonly ReadWriteBuffer<int> active;
    public readonly ReadWriteBuffer<float2> positions;
    public readonly ReadWriteTexture2D<uint> bitmapDataInt;

    public readonly int2 resolution;
    public readonly RectGPU cameraRect;
    public readonly float cameraScale;
    public readonly uint color;

    private bool HasBit(int packed, int bit)
    {
        return (packed & (1u << bit)) != 0;
    }

    private void DrawLineFromID(int id)
    { 
        int2 link = pairs[id];
        bool isActive = HasBit(active[id / 32], id % 32);

        if (!isActive) return;

        float2 a = (positions[link.X] - cameraRect.topLeft) / cameraScale;
        float2 b = (positions[link.Y] - cameraRect.topLeft) / cameraScale;

        // return if both points are outside the screen
        if (Hlsl.Any(Hlsl.Any(Hlsl.All(a < 0) || Hlsl.All(a > resolution)) && Hlsl.Any(Hlsl.All(b < 0) || Hlsl.All(b > resolution)))) return;
        
        RGBA_GPU rgba = RGBA_GPU.FromPackedRGBA(color);
        DrawLine(a, b, rgba);
    }

    public void Execute()
    {
        DrawLineFromID(ThreadIds.X);
    }

    private void SetColor(int2 coord, RGBA_GPU c)
    {
        if (Hlsl.All(coord >= 0) && Hlsl.All(coord < resolution))
        {
            RGBA_GPU background = RGBA_GPU.FromPackedRGBA(bitmapDataInt[coord]);
            background.A(255);
            bitmapDataInt[coord] = background.Blend(c).ToPackedRGBA();
        }
    }

    private void DrawLine(float2 start, float2 end, RGBA_GPU color)
    {
        float2 diff = end - start;
        float length = Hlsl.Length(diff);

        float2 delta = diff / length;

        for (int i = 0; i < length; i++)
        {
            SetColor((int2)(start + delta * i), color);
        }
    }

}