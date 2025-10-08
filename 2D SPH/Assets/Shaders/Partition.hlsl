static const uint primeX = 73856093;
static const uint primeY = 19349663;
static const uint primeZ = 83492791;

uint gridX;
uint gridY;

uint CalculateHash(int2 gridPos) {
    uint total = gridPos.x * primeX + gridPos.y * primeY;
    
    return total % tableSize;
}

void IndexAndCount(uint i) {
    float2 pos = Positions[i];
    pos += containerSize / 2;

    int2 gridPos = int2(floor(pos.x / smoothingRadius), floor(pos.y / smoothingRadius));
    uint hash = CalculateHash(gridPos);

    GridIndices[i] = hash;
    
    InterlockedAdd(CellCounts[hash], 1);
}