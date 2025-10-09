float viscosityKernelLapConstant;

float viscosityMultiplier;

float ViscosityKernelLap(float2 offset) {
    float r = length(offset);

    if (r < 0.0001 || r > smoothingRadius)
        return 0;

    return viscosityKernelLapConstant * (smoothingRadius - r);
}

float2 CalculateViscosityForce(uint i) {
    float2 vForce = float2(0, 0);

    int2 gridPosI = GetGridPos(PredictedPositions[i]);

    for (int x = -1; x < 2; x++) {
        for (int y = -1; y < 2; y++) {
            int2 gridPosJ = gridPosI + int2(x, y);
            if (!IsInBounds(gridPosJ)) continue;

            uint hash = CalculateHashFromGrid(gridPosJ);

            uint startIndex = Offsets[hash];
            uint endIndex = Offsets[hash + 1];

            for (uint j = startIndex; j < endIndex; j++) {
                float2 posOffset = PredictedPositions[i] - PredictedPositions[j];
                float2 velOffset = Velocities[j] - Velocities[i];

                float laplacian = ViscosityKernelLap(posOffset);
                
                vForce += particleMass * (velOffset / Densities[j]) * laplacian;
            }
        }
    }

    return viscosityMultiplier * vForce;
}