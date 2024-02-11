public struct RectGPU
{
    public float2 position;
    public float2 size;
    public float2 center;
    public float2 topLeft;
    public float2 topRight;
    public float2 bottomLeft;
    public float2 bottomRight;

    public float left;
    public float right;
    public float top;
    public float bottom;

    public RectGPU(float2 position, float2 size)
    {
        this.position = position;
        this.size = size;
        Set(position, size);
    }

    public RectGPU(float x, float y, float width, float height)
    {
        position = new float2(x, y);
        size = new float2(width, height);
        Set(position, size);
    }

    public void Set(float2 position, float2 size)
    {
        this.position = position;
        this.size = size;

        this.size = size;
        center = position + this.size / 2;
        topLeft = position;
        topRight = position + new float2(this.size.X, 0);
        bottomLeft = position + new float2(0, this.size.Y);
        bottomRight = position + this.size;

        left = position.X;
        right = position.X + this.size.X;
        top = position.Y;
        bottom = position.Y + this.size.Y;
    }

    public static implicit operator RectGPU(ProtoEngine.Rect rect) => new(rect.position, rect.size);
}