static const uint primeX = 73856093;
static const uint primeY = 19349663;
static const uint primeZ = 83492791;

uint gridX;
uint gridY;

uint CalculateHash(float2 pos) {
    float2 offsetPos = pos + containerSize / 2;
    int2 gridPos = int2(floor(offsetPos.x / smoothingRadius), floor(offsetPos.y / smoothingRadius));
    uint total = gridPos.x * primeX + gridPos.y * primeY;
    
    return total % tableSize;
}

void IndexAndCount(uint i) {
    uint hash = CalculateHash(Positions[i]);
    
    InterlockedAdd(CellCounts[hash], 1);
}