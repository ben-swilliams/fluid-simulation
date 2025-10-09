static const uint primeX = 73856093;
static const uint primeY = 19349663;
static const uint primeZ = 83492791;

uint gridX;
uint gridY;

int2 GetGridPos(float2 pos) {
    float2 offsetPos = pos + containerSize / 2;
    int2 gridPos = int2(floor(offsetPos.x / smoothingRadius), floor(offsetPos.y / smoothingRadius));

    return gridPos;
}

uint CalculateHashFromGrid(int2 gridPos) {
    uint total = gridPos.x * primeX + gridPos.y * primeY;
    return total % tableSize;
}

uint CalculateHash(float2 pos) {
    int2 gridPos = GetGridPos(pos);
    return CalculateHashFromGrid(gridPos);
}

bool IsInBounds(int2 gridPos) {
    if (gridPos.x < 0 || gridPos.y < 0) return false;

    int2 maxCorner = GetGridPos(containerSize / 2);

    if (gridPos.x > maxCorner.x || gridPos.y > maxCorner.y) return false;

    return true;
}