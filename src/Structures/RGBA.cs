
using ComputeSharp;

namespace ParticlePhysics.Internal;

public struct RGBA_GPU
{
    public uint4 value;
    
    public uint R(){return value.X;}
    public uint G(){return value.Y;}
    public uint B(){return value.Z;}
    public uint A(){return value.W;}

    public void R(uint h) {value.X = h;}
    public void G(uint s) {value.Y = s;}
    public void B(uint l) {value.Z = l;}
    public void A(uint a) {value.W = a;}

    public static RGBA_GPU New(uint r, uint g, uint b, uint a)
    {
        var rgba = new RGBA_GPU();
        rgba.value = new uint4(r, g, b, a);
        return rgba;
    }

    public static RGBA_GPU New(uint lightness, uint alpha)
    {
        var rgba = new RGBA_GPU();
        rgba.value = new uint4(lightness, lightness, lightness, alpha);
        return rgba;
    }

    public static RGBA_GPU FromPackedRGBA(uint packedRGBA)
    {
        uint r = (packedRGBA >> 0) & 0xFF;
        uint g = (packedRGBA >> 8) & 0xFF;
        uint b = (packedRGBA >> 16) & 0xFF;
        uint a = (packedRGBA >> 24) & 0xFF;
        RGBA_GPU rgba = New(r, g, b, a);
        return rgba;
    }

    public uint ToPackedRGBA()
    {
        uint r = R();
        uint g = G();
        uint b = B();
        uint a = A();
        uint packedRGBA = (r << 0) | (g << 8) | (b << 16) | (a << 24);
        return packedRGBA;
    }

    public RGBA_GPU Lerp(RGBA_GPU other, float t)
    {
        RGBA_GPU result = new RGBA_GPU();
        result.value = (uint4)Hlsl.Lerp(value, other.value, t);
        return result;
    }

    public RGBA_GPU Blend(RGBA_GPU other)
    {
        var result = Lerp(other, other.A() / 255f);
        result.A(255);
        return result;
    }

    public static RGBA_GPU operator *(RGBA_GPU a, float b)
    {
        RGBA_GPU result = new RGBA_GPU
        {
            value = (uint4)Hlsl.Mul(a.value, b)
        };
        return result;
    }
    


}
