float velocitySmoothing;

float3 CalculateXSPHCorrection(uint i) {
    int3 gridPosI = GetGridPos(Positions[i]);

    float3 velAcc = float2(0, 0);

    for (int x = -1; x < 2; x++) {
        for (int y = -1; y < 2; y++) {
            int3 gridPosJ = gridPosI + int3(x, y);
            if (!IsInBounds(gridPosJ)) continue;

            uint hash = CalculateHashFromGrid(gridPosJ);

            uint startIndex = Offsets[hash];
            uint endIndex = Offsets[hash + 1];

            for (uint j = startIndex; j < endIndex; j++) {
                if (i == j) continue;

                float massOverDensity = particleMass / Densities[j];
                float3 velDiff = Velocities[j] - Velocities[i];

                float3 offset = Positions[i] - Positions[j];
                float weight = CubicSplineKernel(offset);

                velAcc += massOverDensity * weight * velDiff;
            }
        }
    }

    return velocitySmoothing * velAcc;
}
