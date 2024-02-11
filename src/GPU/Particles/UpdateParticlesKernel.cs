using ComputeSharp;

[AutoConstructor]
internal readonly partial struct UpdateParticlesKernel : IComputeShader
{
    public readonly ReadOnlyBuffer<Particle> particles;

    public readonly ReadWriteBuffer<float2> positions;
    public readonly ReadWriteBuffer<int2> positionsInt;
    public readonly ReadWriteBuffer<float2> lastPositions;
    public readonly ReadWriteBuffer<int> active;
    public readonly ReadWriteBuffer<uint> colors;

    public readonly ReadWriteBuffer<int> linkActive;
    public readonly ReadWriteBuffer<int2> keyValuePairs;
    public readonly ReadWriteBuffer<int> counts;
    public readonly ReadWriteBuffer<int> starts;

    public void ClearLinks(int id)
    {
        int count = counts[id];
        int start = starts[id];

        for (int i = start; i < start + count; i++)
        {
            int2 pair = keyValuePairs[i];
            int linkID = pair.Y;
            int packedIndex = (int)Hlsl.Floor(linkID / 32f);
            Hlsl.InterlockedAnd(ref linkActive[packedIndex], ~(int)(1u << (int)((float)linkID % 32)));
        }

        counts[id] = 0;
    }

    public void Execute()
    {
        int newID = ThreadIds.X;

        Particle particle = particles[newID];
        int id = particle.ID;

        positions[id] = particle.position;
        positionsInt[id] = ParticlePhysicsKernel.PositionFloatToInt(particle.position);
        lastPositions[id] = particle.lastPosition;
        colors[id] = particle.color;

        int packedIndex = (int)Hlsl.Floor(id / 32f);
        if (particle.active == 1) Hlsl.InterlockedOr(ref active[packedIndex], (int)(1u << (int)((float)id % 32)));
        else 
        {
            ClearLinks(id);
            Hlsl.InterlockedAnd(ref active[packedIndex], ~(int)(1u << (int)((float)id % 32)));
        }
    }
}