float B;
float stiffness;

float CalculateTaitPressure(uint i) {
    return max(0, B * (pow(Densities[i] / restDensity, stiffness) - 1));
}

void CalculateWCSPHComponents(uint i, out float3 viscosity, out float3 surfaceTension, out float3 pressure, out float3 xsph) {
    viscosity = 0;
    surfaceTension = 0;
    pressure = 0;
    xsph = 0;

    float3 posI = Positions[i];
    int3 gridPosI = GetGridPos(posI);

    float3 velI = Velocities[i];

    for (int x = -1; x < 2; x++) {
        for (int y = -1; y < 2; y++) {
            for (int z = -1; z < 2; z++) {
                int3 gridPosJ = gridPosI + int3(x, y, z);
                if (!IsInBounds(gridPosJ)) continue;

                uint hash = CalculateHashFromGrid(gridPosJ);

                uint startIndex = Offsets[hash];
                uint endIndex = Offsets[hash + 1];

                for (uint j = startIndex; j < endIndex; j++) {
                    if (i == j) continue;

                    float3 posOffset = posI - Positions[j];
                    float r = length(posOffset);
                    float3 velOffset = velI - Velocities[j];

                    viscosity += CalculateViscosityContribution(posOffset, velOffset, r, i, j);
                    surfaceTension += CalculateSurfaceTensionContribution(posOffset, r);
                    pressure += CalculatePressureContribution(posOffset, i, j);
                    xsph += CalculateXSPHContribution(Densities[j], posOffset, velOffset);
                }
            }
        }
    }
}

float3 CalculateWCSPHAcceleration(int i) {
    if (deltaTime == 0) return 0;
    float3 viscosity;
    float3 surfaceTension;
    float3 pressure;
    float3 xsph;

    CalculateWCSPHComponents(i, viscosity, surfaceTension, pressure, xsph);

    // Divide XSPH by deltaTime to make it a direct velocity update
    return MouseForce(i) + viscosity + surfaceTension + pressure + gravity + xsph / deltaTime;
}