static const uint primeX = 73856093;
static const uint primeY = 19349663;
static const uint primeZ = 83492791;

int maxCornerX;
int maxCornerY;
int maxCornerZ;

uint useIndex;

uint PrimeHash(int3 gridPos) {
    uint total = (uint)gridPos.x * primeX + (uint)gridPos.y * primeY + (uint)gridPos.z * primeZ;
    return total % tableSize;
}

uint IndexHash(int3 gridPos) {
    uint total = gridPos.x + gridPos.y * (maxCornerX + 1) + gridPos.z * (maxCornerX + 1) * (maxCornerY + 1);
    return total;
}

int3 GetGridPos(float3 pos) {
    float3 offsetPos = pos + containerSize / 2;
    int3 gridPos = int3(floor(offsetPos.x / (2 * smoothingRadius)),
                        floor(offsetPos.y / (2 * smoothingRadius)),
                        floor(offsetPos.z / (2 * smoothingRadius)));

    return gridPos;
}

uint CalculateHashFromGrid(int3 gridPos) {
    return useIndex == 1 ? IndexHash(gridPos) : PrimeHash(gridPos);
}

uint CalculateHash(float3 pos) {
    int3 gridPos = GetGridPos(pos);
    return CalculateHashFromGrid(gridPos);
}

bool IsInBounds(int3 gridPos) {
    if (gridPos.x < 0 || gridPos.y < 0 || gridPos.z < 0) return false;

    if (gridPos.x > maxCornerX || gridPos.y > maxCornerY || gridPos.z > maxCornerZ) return false;

    return true;
}

void IndexAndCount(uint i) {
    uint hash = CalculateHash(Positions[i]);
    
    InterlockedAdd(CellCounts[hash], 1);
}