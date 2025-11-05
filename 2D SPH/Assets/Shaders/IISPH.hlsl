float relaxationFactor;

float3 CalculateDContribution(uint i, uint j) {
    float3 offset = Positions[i] - Positions[j];
    float3 d = (particleMass / (Densities[i] * Densities[i])) * CubicSplineGrad(offset);
    return -deltaTime * deltaTime * d;
}

float2 CalculateDeltaDensityAndA(uint i) {
    float deltaDensity = 0;
    float a = 0;

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
                    float3 posOffset = Positions[i] - Positions[j];
                    float r = length(posOffset);

                    float3 velOffset = Velocities[i] - Velocities[j];

                    float3 grad = CubicSplineGrad(posOffset);

                    deltaDensity += particleMass * dot(velOffset, grad);

                    float3 d_ji = deltaTime * deltaTime * particleMass * grad / (Densities[i] * Densities[i]);
            
                    a += dot(Dii[i] - d_ji, grad);
                }
            }
        }
    }

    a *= particleMass;

    return float2(deltaDensity, a);
}

float3 CalculatePressureSum(uint i) {
    float3 pressureSum = float3(0, 0, 0);

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
                    float3 offset = Positions[i] - Positions[j];

                    pressureSum += -particleMass * IterPressures[j] * CubicSplineGrad(offset) / (Densities[j] * Densities[j]);
                }
            }
        }
    }

    return deltaTime * deltaTime * pressureSum;
}

float CalculateNextPressureValue(uint i) {
    if (abs(Aii[i]) < Epsilon) return 0;
    float pressureSum = 0;
    
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
                    float3 offset = Positions[i] - Positions[j];
                    float3 grad = CubicSplineGrad(offset);

                    float3 d_ji = deltaTime * deltaTime * particleMass * grad / (Densities[i] * Densities[i]);

                    float3 inner = DPSum[i] - Dii[j] * IterPressures[j] - (DPSum[j] - d_ji * IterPressures[i]);

                    pressureSum += dot(inner, grad);
                }
            }
        }
    }

    float nextPressure = (1 - relaxationFactor) * IterPressures[i] + 
                        (relaxationFactor / Aii[i]) * (restDensity - Densities[2 * instanceCount + i] - particleMass * pressureSum);

    return max(0, nextPressure);
}

void CalculateIISPHComponents(uint i, out float3 viscosity, out float3 surfaceTension, out float3 D) {
    viscosity = 0;
    surfaceTension = 0;
    D = 0;

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
                    D += CalculateDContribution(i, j);
                }
            }
        }
    }
}

struct AccAndD {
    float3 acceleration;
    float3 D;
};

AccAndD CalculateAccelerationAndD(uint i) {
    AccAndD result;
    float3 viscosity;
    float3 surfaceTension;
    float3 D;

    CalculateIISPHComponents(i, viscosity, surfaceTension, D);

    result.acceleration = MouseForce(i) + gravity + viscosity + surfaceTension;
    result.D = D;

    return result;
}

float3 CalculateXSPHPressureForce(uint i) {
    float3 pressureForce = float3(0, 0, 0);
    float3 xsphCorrection = float3(0, 0, 0);

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
                    pressureForce += CalculatePressureContribution(i, j);
                    xsphCorrection += CalculateXSPHContribution(i, j);
                }
            }
        }
    }

    return particleMass * pressureForce + xsphCorrection;
}