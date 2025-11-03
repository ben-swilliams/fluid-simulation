float stiffness;

void CalculateTaitPressure(uint i) {
    float B = 1;

    return B * (pow(Densities[i] / restDensity, stiffness) - 1);
}

void CalculateWCSPHComponents(out float3 viscosity, out float3 surfaceTension, out float3 pressure) {
    viscosity = 0;
    surfaceTension = 0;
    pressure = 0;

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
                }
            }
        }
    }
}

float3 CalculateAcceleration(int i) {
    float3 viscosity;
    float3 surfaceTension;
    float3 pressure;

    CalculateWCSPHComponents(viscosity, surfaceTension, pressure);

    return viscosity + surfaceTension + pressure + gravity;
}