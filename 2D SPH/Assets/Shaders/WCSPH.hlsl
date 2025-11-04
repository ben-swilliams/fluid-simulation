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

    int3 gridPosI = GetGridPos(Positions[i]);

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

                    float massOverDensity = particleMass / Densities[j];
                    float3 offset = Positions[i] - Positions[j];

                    viscosity += CalculateViscosityContribution(i, j);
                    surfaceTension += CalculateSurfaceTensionContribution(i, j);
                    pressure += CalculatePressureContribution(i, j);
                    xsph += CalculateXSPHContribution(i, j);
                }
            }
        }
    }
}

float3 CalculateAcceleration(int i) {
    float3 viscosity;
    float3 surfaceTension;
    float3 pressure;
    float3 xsph;

    CalculateWCSPHComponents(i, viscosity, surfaceTension, pressure, xsph);

    // Divide XSPH by deltaTime to make it a direct velocity update
    return MouseForce(i) + viscosity + surfaceTension + pressure + gravity + xsph / deltaTime;
}