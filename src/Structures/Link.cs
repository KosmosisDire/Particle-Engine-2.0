public struct Link
{
    public int particle1;
    public int particle2;
    public float length;
    public int ID;

    public Link(int particle1, int particle2, float length, int ID)
    {
        this.particle1 = particle1;
        this.particle2 = particle2;
        this.length = length;
        this.ID = ID;
    }
}
