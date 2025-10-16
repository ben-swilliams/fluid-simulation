RWStructuredBuffer<float2> OldPositions;
RWStructuredBuffer<float2> NewPositions;

RWStructuredBuffer<float2> OldVelocities;
RWStructuredBuffer<float2> NewVelocities;

void SortParticle(uint i)
{
    
    uint hash = CalculateHash(OldPositions[i]);

    uint oldOffset;
    InterlockedAdd(LocalOffsets[hash], 1, oldOffset);

    uint offset = oldOffset;
    uint destIndex = Offsets[hash] + offset;

    if (destIndex >= instanceCount) return;

    NewPositions[destIndex]  = OldPositions[i];
    NewVelocities[destIndex] = OldVelocities[i];
}