const float eps = 1e-4;

float kernelConstant;

float SmoothingKernel(float2 offset) {
    float oSquared = dot(offset, offset);
    if (oSquared > smoothingRadius * smoothingRadius) {
        return 0;
    } else {

        float diff = (smoothingRadius * smoothingRadius) - oSquared;

        return kernelConstant * diff * diff * diff;
    }
}

float CalculateDensity(uint i) {
    float density = 0;

    int2 gridPosI = GetGridPos(PredictedPositions[i]);

    for (int x = -1; x < 2; x++) {
        for (int y = -1; y < 2; y++) {
            int2 gridPosJ = gridPosI + int2(x, y);
            if (!IsInBounds(gridPosJ)) continue;

            uint hash = CalculateHashFromGrid(gridPosJ);

            uint startIndex = Offsets[hash];
            uint endIndex = Offsets[hash + 1];

            for (uint j = startIndex; j < endIndex; j++) {
                float2 offset = PredictedPositions[j] - PredictedPositions[i];
                density += particleMass * SmoothingKernel(offset);
            }
        }
    }
    return max(eps, density);
}