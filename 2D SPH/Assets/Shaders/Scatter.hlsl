RWStructuredBuffer<float3> OldPositions;
RWStructuredBuffer<float3> NewPositions;

RWStructuredBuffer<float3> OldVelocities;
RWStructuredBuffer<float3> NewVelocities;

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
    IterPressures[destIndex] = 0;
}