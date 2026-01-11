void SortParticle(uint i)
{
    uint hash = CalculateHash(Positions[i]);

    uint oldOffset;
    InterlockedAdd(LocalOffsets[hash], 1, oldOffset);

    uint offset = oldOffset;
    uint destIndex = Offsets[hash] + offset;

    if (destIndex >= instanceCount) return;

    SortedVelocities[destIndex] = Velocities[i];
    SortedPositions[destIndex]  = Positions[i];
}