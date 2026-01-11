float densityTexRadius;

float2 CalculateDensities(uint i) {
    float density = Epsilon;
    float nearDensity = Epsilon;

    float3 posI = Positions[i];
    int3 gridPosI = GetGridPos(posI);

    for (int o = 0; o < 27; o++) {
        int3 gridPosJ = gridPosI + offsets[o];
        if (!IsInBounds(gridPosJ)) continue;

        uint hash = CalculateHashFromGrid(gridPosJ);

        uint startIndex = Offsets[hash];
        uint endIndex = Offsets[hash + 1];

        for (uint j = startIndex; j < endIndex; j++) {
            float3 offset = posI - Positions[j];
            float r = length(offset);
            density += particleMass * DensityKernel(r);
            nearDensity += particleMass * NearDensityKernel(r);
        }
    }

    return float2(density, nearDensity);
}

float CalculateDensityAtWorld(float3 pos) {
    float density = Epsilon;

    int3 gridPosI = GetGridPos(pos);

    for (int o = 0; o < 27; o++) {
        int3 gridPosJ = gridPosI + offsets[o];
        if (!IsInBounds(gridPosJ)) continue;

        uint hash = CalculateHashFromGrid(gridPosJ);

        uint startIndex = Offsets[hash];
        uint endIndex = Offsets[hash + 1];

        for (uint j = startIndex; j < endIndex; j++) {
            float3 offset = pos - Positions[j];
            float r = length(offset);
            density += particleMass * DensityKernel(r / densityTexRadius);
        }
    }

    return density;
}