float velocitySmoothing;

float2 CalculateXSPHCorrection(uint i) {
    int2 gridPosI = GetGridPos(Positions[i]);

    float2 velAcc = float2(0, 0);

    for (int x = -1; x < 2; x++) {
        for (int y = -1; y < 2; y++) {
            int2 gridPosJ = gridPosI + int2(x, y);
            if (!IsInBounds(gridPosJ)) continue;

            uint hash = CalculateHashFromGrid(gridPosJ);

            uint startIndex = Offsets[hash];
            uint endIndex = Offsets[hash + 1];

            for (uint j = startIndex; j < endIndex; j++) {
                if (i == j) continue;

                float massOverDensity = particleMass / Densities[j];
                float2 velDiff = Velocities[j] - Velocities[i];

                float2 offset = Positions[i] - Positions[j];
                float weight = CubicSplineKernel(offset);

                velAcc += massOverDensity * weight * velDiff;
            }
        }
    }

    return velocitySmoothing * velAcc;
}
