void SortParticle(uint i)
{
    uint hash = CalculateHash(OldPositions[i]);

    uint oldOffset;
    InterlockedAdd(LocalOffsets[hash], 1, oldOffset);

    uint offset = oldOffset;
    uint destIndex = Offsets[hash] + offset;

    if (destIndex >= instanceCount) return;

    SortedPositions[destIndex]  = Positions[i];
    SortedVelocities[destIndex] = Velocities[i];
    IterPressures[destIndex] = 0;
}