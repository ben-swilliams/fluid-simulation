static const uint primeX = 73856093;
static const uint primeY = 19349663;
static const uint primeZ = 83492791;

int3 GetGridPos(float3 pos) {
    float3 offsetPos = pos + containerSize / 2;
    int3 gridPos = int3(floor(offsetPos.x / (2 * smoothingRadius)), floor(offsetPos.y / (2 * smoothingRadius)));

    return gridPos;
}

uint CalculateHashFromGrid(int3 gridPos) {
    uint total = (uint)gridPos.x * primeX + (uint)gridPos.y * primeY;
    return total % tableSize;
}

uint CalculateHash(float3 pos) {
    int3 gridPos = GetGridPos(pos);
    return CalculateHashFromGrid(gridPos);
}

bool IsInBounds(int3 gridPos) {
    if (gridPos.x < 0 || gridPos.y < 0) return false;

    int3 maxCorner = GetGridPos(containerSize / 2);

    if (gridPos.x > maxCorner.x || gridPos.y > maxCorner.y) return false;

    return true;
}

void IndexAndCount(uint i) {
    uint hash = CalculateHash(Positions[i]);
    
    InterlockedAdd(CellCounts[hash], 1);
}