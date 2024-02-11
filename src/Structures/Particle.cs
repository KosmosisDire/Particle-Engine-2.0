public struct Particle
{
    public float2 position;
    public float2 lastPosition;
    public uint color;
    public int active;
    public int ID;

    public Particle(float2 position, float2 lastPosition, uint color, int id, bool active = true)
    {
        this.position = position;
        this.lastPosition = lastPosition;
        this.color = color;
        this.active = active ? 1 : 0;
        ID = id;
    }

    public Particle(int id, bool active = false)
    {
        position = 0;
        lastPosition = 0;
        color = 0;
        this.active = active ? 1 : 0;
        ID = id;
    }
}