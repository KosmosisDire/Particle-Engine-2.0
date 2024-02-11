using ComputeSharp;

[AutoConstructor]
internal readonly partial struct ParticlePhysicsKernel : IComputeShader
{
    // particles
    public readonly ReadWriteBuffer<float2> positions;
    public readonly ReadWriteBuffer<int2> positionsInt; // no float atomics: calculations done w ints then copied to positions at the end
    public readonly ReadWriteBuffer<float2> lastPositions;
    public readonly ReadWriteBuffer<float> travelDistances;
    public readonly ReadWriteBuffer<int> active;
    public readonly ReadWriteBuffer<uint> colors;
    public readonly float radius;

    // grid
    public readonly ReadWriteBuffer<int2> gridKeysValues;
    public readonly ReadWriteBuffer<int> gridCounts;
    public readonly ReadWriteBuffer<int> gridStarts;
    public readonly int2 extents;
    public readonly int2 cellCount;
    public readonly int cellCountLinear;
    public readonly float2 cellSize;

    // links
    public readonly ReadWriteBuffer<int2> linkPairs;
    public readonly ReadWriteBuffer<float> linkLengths;
    public readonly ReadWriteBuffer<int> linkStrain;
    public readonly ReadWriteBuffer<int> linkActive;

    public readonly ReadWriteBuffer<int2> linkKeysValues;
    public readonly ReadWriteBuffer<int> linkCounts;
    public readonly ReadWriteBuffer<int> linkStarts;

    // physics properties
    public readonly float dt;
    public readonly float2 gravity;
    public readonly int iterations;

    private bool HasBit(int packed, int bit)
    {
        return (packed & (1u << bit)) != 0;
    }

    public static int2 PositionFloatToInt(float2 pos)
    {
        return (int2)(pos * 1000000f);
    }

    public static float2 PositionIntToFloat(int2 pos)
    {
        return (float2)pos / 1000000f;
    }

    public void Execute()
    {
        int id = ThreadIds.X;

        if(id >= positions.Length) return;

        bool isActive = HasBit(active[id / 32], id % 32);
        if (!isActive) return;

        for(int i = 0; i < iterations; i++)
        {
            Collisions(id);
            ApplyBoundary(id, false);
            Integrate(id, dt/iterations);
            SolveLinks(id);
        }

        positions[id] = PositionIntToFloat(positionsInt[id]);
    }

    public void Integrate(int id, float _dt)
    {
        float2 pos = PositionIntToFloat(positionsInt[id]);
        float2 lastPos = lastPositions[id];
        lastPositions[id] = PositionIntToFloat(positionsInt[id]);

        float2 velocity = pos - lastPos;
        float velocityMag = Hlsl.Length(velocity);

        float inertia = 1.0f + travelDistances[id] / (velocityMag + 1.0f);
        float antiPressure = (float)Hlsl.Pow(1.0f / inertia, 2);
        travelDistances[id] *= 0.4f;

        float2 acceleration = gravity - velocity * 2f;

        var offset = velocity + acceleration * antiPressure * (_dt * _dt);
        var offsetInt = PositionFloatToInt(offset);
        Hlsl.InterlockedAdd(ref positionsInt[id].X, offsetInt.X);
        Hlsl.InterlockedAdd(ref positionsInt[id].Y, offsetInt.Y);
    }

    void ParticleToCellCollisions(int id, int cellID)
    {
        int gridCellStart = gridStarts[cellID];
        int particleCount = gridCounts[cellID];
        int particleEnd = gridCellStart + particleCount;
        
        for(int j = gridCellStart; j < particleEnd; j++)
        {
            int other = gridKeysValues[j].Y;
            SolveCollision(id, other);
        }
    }

    void Collisions(int id)
    {
        float2 pos = PositionIntToFloat(positionsInt[id]);
        int2 cellCoord = GetGridCoord(pos);
        int cellID = IndexFromCoord(cellCoord);

        bool leftEdge = cellID % cellCount.X == 0;
        bool rightEdge = cellID % cellCount.X == cellCount.X - 1;
        bool topEdge = cellID < cellCount.X;
        bool bottomEdge = cellID >= cellCountLinear - cellCount.X;

        float2 edgeDist = new float2(pos.X % cellSize.X, pos.Y % cellSize.Y);

        ParticleToCellCollisions(id, cellID); // center

        bool particleEdgeOptimization = false; // should we only check collisions for particles that are near the edge of a cell?
        if(particleEdgeOptimization)
        {
            float edgeMargin = radius * 1.1f;
            bool leftEdgeParticle = edgeDist.X < edgeMargin;
            bool rightEdgeParticle = edgeDist.X > cellSize.X - edgeMargin;
            bool topEdgeParticle = edgeDist.Y < edgeMargin;
            bool bottomEdgeParticle = edgeDist.Y > cellSize.Y - edgeMargin;

            if(!rightEdge && rightEdgeParticle) ParticleToCellCollisions(id, cellID + 1); // right
            if(!leftEdge && leftEdgeParticle) ParticleToCellCollisions(id, cellID - 1); // left
            if(!bottomEdge && bottomEdgeParticle) ParticleToCellCollisions(id, cellID + cellCount.X); // bottom
            if(!topEdge && topEdgeParticle) ParticleToCellCollisions(id, cellID - cellCount.X); // top
            if(!bottomEdge && !rightEdge && (bottomEdgeParticle || rightEdgeParticle)) ParticleToCellCollisions(id, cellID + cellCount.X + 1); // bottom right
            if(!bottomEdge && !leftEdge && (bottomEdgeParticle || leftEdgeParticle)) ParticleToCellCollisions(id, cellID + cellCount.X - 1); // bottom left
            if(!topEdge && !rightEdge && (topEdgeParticle || rightEdgeParticle)) ParticleToCellCollisions(id, cellID - cellCount.X + 1); // top right
            if(!topEdge && !leftEdge && (topEdgeParticle || leftEdgeParticle)) ParticleToCellCollisions(id, cellID - cellCount.X - 1); // top left
        }
        else
        {
            if(!rightEdge) ParticleToCellCollisions(id, cellID + 1); 
            if(!leftEdge) ParticleToCellCollisions(id, cellID - 1);
            if(!bottomEdge) ParticleToCellCollisions(id, cellID + cellCount.X); 
            if(!topEdge) ParticleToCellCollisions(id, cellID - cellCount.X); 
            if(!bottomEdge && !rightEdge) ParticleToCellCollisions(id, cellID + cellCount.X + 1);
            if(!bottomEdge && !leftEdge) ParticleToCellCollisions(id, cellID + cellCount.X - 1);
            if(!topEdge && !rightEdge) ParticleToCellCollisions(id, cellID - cellCount.X + 1);
            if(!topEdge && !leftEdge) ParticleToCellCollisions(id, cellID - cellCount.X - 1);
        }
    }

    const float epsilon = 0.0000001f;
    // this function is used to make particles under pressure more resistant to being moved by other particles
    // instead of being a linear relationship between pressure and resistance, it scales exponentially
    float massScalingFunction(float input)
    {
        // to see this function in desmos: https://www.desmos.com/calculator/wfdd5redo7
        var func = ((-24 * Hlsl.Pow(input, 5) + 60 * Hlsl.Pow(input, 4) + -50 * Hlsl.Pow(input, 3) + 15 * Hlsl.Pow(input, 2)) - 0.5f) * 3f + 0.5f;
        return func;
    }
    void SolveCollision(int obj, int other)
    {
        float2 otherPos = PositionIntToFloat(positionsInt[other]);
        float2 objPos = PositionIntToFloat(positionsInt[obj]);

        float2 diff = objPos - otherPos;
        float sqrDist = Hlsl.Dot(diff, diff);
        float collisionDiameter = radius * 2f;

        if(sqrDist < collisionDiameter * collisionDiameter && sqrDist > epsilon) 
        {
            float2 velocity_obj = objPos - lastPositions[obj];
            float2 velocity_other = otherPos - lastPositions[other];

            float speed_obj = Hlsl.Length(velocity_obj);
            float speed_other = Hlsl.Length(velocity_other);
            float travelDistance_obj = travelDistances[obj];
            float travelDistance_other = travelDistances[other];

            float inertia_obj = 1.0f + travelDistance_obj / (speed_obj + 1.0f);
            float inertia_other = 1.0f + travelDistance_other / (speed_other + 1.0f);
            float totalMass = 1 / (inertia_obj + inertia_other);
            float massFactor_obj = massScalingFunction(inertia_obj * totalMass);
            float massFactor_other = massScalingFunction(inertia_other * totalMass);

            var dist = Hlsl.Sqrt(sqrDist);
            var normDir = diff / dist;
            var delta = normDir * (collisionDiameter - dist) * 0.25f / iterations;

            Move(obj, delta * massFactor_other);
            Move(other, -delta * massFactor_obj);
        }
    }

    public void SolveLink(int linkID)
    {
        bool isActive = HasBit(linkActive[linkID / 32], linkID % 32);
        if (!isActive) return;

        int2 link = linkPairs[linkID];
        float length = linkLengths[linkID];

        float2 a = PositionIntToFloat(positionsInt[link.X]);
        float2 b = PositionIntToFloat(positionsInt[link.Y]);

        float2 diff = b - a;
        float dist = Hlsl.Length(diff);

        if (dist < epsilon) return;

        float2 normDir = diff / dist;
        float2 delta = normDir * (dist - length) * 0.5f / iterations;

        Move(link.X, delta);
        Move(link.Y, -delta);

        int currentStrain = linkStrain[linkID];
        if (currentStrain > 1000000f)
        {
            int packedIndex = (int)Hlsl.Floor(linkID / 32f);
            Hlsl.InterlockedAnd(ref linkActive[packedIndex], ~(int)(1u << (int)((float)linkID % 32)));
            // colors[link.X] = RGBA_GPU.New(0, 111, 83, 255).ToPackedRGBA();
            // colors[link.Y] = RGBA_GPU.New(0, 111, 83, 255).ToPackedRGBA();
        }
        else
        {
            float strain = Hlsl.Clamp(Hlsl.Abs(dist / length - 1), 0, 1) * 100f;
            Hlsl.InterlockedAdd(ref linkStrain[linkID], (int)((strain * strain) - (100 - strain)));

            uint strainColor = (uint)(currentStrain / 1000000f * 255f);
            // colors[link.X] = RGBA_GPU.New(strainColor, 111, 83, 255).ToPackedRGBA();
            // colors[link.Y] = RGBA_GPU.New(strainColor, 111, 83, 255).ToPackedRGBA();
        }
    }

    public void SolveLinks(int particleID)
    {
        int start = linkStarts[particleID];
        int count = linkCounts[particleID];
        int end = start + count;

        for(int i = start-1; i < end+1; i++) // -1 and +1 seems to fix a bug where some links are not solved. Should investigate this further
        {
            int linkID = linkKeysValues[i].Y;
            SolveLink(linkID);
        }
    }
    
    void ApplyBoundary(int id, bool circular)
    {
        float2 pos = PositionIntToFloat(positionsInt[id]);

        if(circular)
        {
            float2 diff = pos - (float2)extents * 0.5f;
            float dist = Hlsl.Length(diff);
            if(dist > extents.X * 0.5f)
            {
                float2 normDir = diff / dist;
                float2 delta = normDir * (dist - extents.X * 0.5f);
                Move(id, -delta);
            }

            return;
        }

        float top = 0;
        float bottom = extents.Y;
        float left = 0;
        float right = extents.X;

        if(pos.X - radius < left)
        {
            MoveX(id, left - (pos.X - radius));
        }
        else if(pos.X + radius > right)
        {
            MoveX(id, right - (pos.X + radius));
        }

        if(pos.Y + radius > bottom)
        {
            MoveY(id, bottom - (pos.Y + radius));
        }
        else if(pos.Y - radius < top)
        {
            MoveY(id, top - (pos.Y - radius));
        }
    }

    #region Helper Functions

    void Move(int id, float2 offset)
    {
        // positions[id] += offset;
        var offsetInt = PositionFloatToInt(offset);
        Hlsl.InterlockedAdd(ref positionsInt[id].X, offsetInt.X);
        Hlsl.InterlockedAdd(ref positionsInt[id].Y, offsetInt.Y);
        travelDistances[id] += Hlsl.Abs(offset.X) + Hlsl.Abs(offset.Y);
    }
    void MoveX(int id, float offset)
    {
        // positions[id].X += offset;
        var offsetInt = PositionFloatToInt(new float2(offset, 0));
        Hlsl.InterlockedAdd(ref positionsInt[id].X, offsetInt.X);
        travelDistances[id] += Hlsl.Abs(offset);
    }
    void MoveY(int id, float offset)
    {
        // positions[id].Y += offset;
        var offsetInt = PositionFloatToInt(new float2(0, offset));
        Hlsl.InterlockedAdd(ref positionsInt[id].Y, offsetInt.Y);
        travelDistances[id] += Hlsl.Abs(offset);
    }
    void AddVelocity(int id, float2 velocity)
    {
        lastPositions[id] -= velocity;
    }
    void ClearLinks(int id)
    {
        int count = linkCounts[id];
        int start = linkStarts[id];

        for (int i = start; i < start + count; i++)
        {
            int2 pair = linkKeysValues[i];
            int linkID = pair.Y;
            int packedIndex = (int)Hlsl.Floor(linkID / 32f);
            Hlsl.InterlockedAnd(ref linkActive[packedIndex], ~(int)(1u << (int)((float)linkID % 32)));
        }

        linkCounts[id] = 0;
    }
    void DeleteParticle(int id)
    {
        int packedIndex = (int)Hlsl.Floor(id / 32f);
        Hlsl.InterlockedAnd(ref active[packedIndex], ~(int)(1u << (int)((float)id % 32)));
        ClearLinks(id);
    }
    
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

