uint gridX;
uint gridY;

int FlatIndex(int2 gridPos) {
    return gridPos.y * gridX + gridPos.x;
}

void IndexAndCount(uint i) {
    float2 pos = Positions[i];
    int2 gridPos = int2(floor(pos.x / smoothingRadius), floor(pos.y / smoothingRadius));
    int index = FlatIndex(gridPos);

    GridIndices[i] = index;
    InterlockedAdd(CellCounts[index], 1);
}