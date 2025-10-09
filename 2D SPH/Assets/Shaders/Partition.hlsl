void IndexAndCount(uint i) {
    uint hash = CalculateHash(Positions[i]);
    
    InterlockedAdd(CellCounts[hash], 1);
}