using ComputeSharp;

namespace ParticlePhysics.Internal;

[AutoConstructor]
internal readonly partial struct BuildGridKernel : IComputeShader
{
    #region Fields

    public readonly ReadWriteBuffer<float2> positions;
    public readonly ReadWriteBuffer<int> active; // each int represents 32 booleans

    public readonly ReadWriteBuffer<int> gridCounts; // holds the count of particles in each cell
    public readonly ReadWriteBuffer<int2> gridKeysValues; // X: cell index, Y: particle index

    public readonly int2 extents;
    public readonly int2 cellCount;
    public readonly int cellCountLinear;
    public readonly float2 cellSize;

    #endregion

    private bool HasBit(int packed, int bit)
    {
        return (packed & (1u << bit)) != 0;
    }

    public void Execute()
    {
        int id = ThreadIds.X;

        if(id >= positions.Length) return;

        bool isActive = HasBit(active[id / 32], id % 32);
        if (!isActive) 
        {
            gridKeysValues[id] = new int2(cellCountLinear + 1, 0);
            return;
        }

        int index = GetGridIndex(positions[id]);
        Hlsl.InterlockedAdd(ref gridCounts[index], 1);

        gridKeysValues[id] = new int2(index, id);
    }
    

    #region Helper Functions

    public int2 GetGridCoord(float2 position)
    {
        int x = (int)Hlsl.Floor(Hlsl.Clamp(position.X / cellSize.X, 0, cellCount.X - 1));
        int y = (int)Hlsl.Floor(Hlsl.Clamp(position.Y / cellSize.Y, 0, cellCount.Y - 1));
        return new int2(x, y);
    }

    public int IndexFromCoord(int2 coord)
    {
        int index = coord.X + coord.Y * cellCount.X;
        return index;
    }

    public int GetGridIndex(float2 position)
    {
        int2 coord = GetGridCoord(position);
        int index = IndexFromCoord(coord);
        return index;
    }

    #endregion
    
}


