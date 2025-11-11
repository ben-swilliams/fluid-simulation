float relaxationFactor;

float3 CalculateDContribution(uint i, uint j) {
    float3 offset = Positions[i] - Positions[j];
    float densitySq = max(Densities[i] * Densities[i], Epsilon);

    float3 d = (particleMass / densitySq) * CubicSplineGrad(offset);
    return -deltaTime * deltaTime * d;
}

float2 CalculateDeltaDensityAndA(uint i) {
    float deltaDensity = 0;
    float a = 0;

    float densitySq = max(Densities[i] * Densities[i], Epsilon);

    int3 gridPosI = GetGridPos(Positions[i]);

    float3 posI = Positions[i];
    float3 velI = Velocities[i];
    float3 d_ii = Dii[i];

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

                    float3 velOffset = velI - Velocities[j];

                    float3 grad = CubicSplineGrad(posOffset);

                    deltaDensity += particleMass * dot(velOffset, grad);

                    float3 d_ji = deltaTime * deltaTime * particleMass * grad / densitySq;
            
                    a += dot(d_ii - d_ji, grad);
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

                    float densitySq = max(Densities[j] * Densities[j], Epsilon);

                    pressureSum += -particleMass * IterPressures[j] * CubicSplineGrad(offset) / densitySq;
                }
            }
        }
    }

    return deltaTime * deltaTime * pressureSum;
}

float CalculateNextPressureValue(uint i) {
    if (abs(Aii[i]) < Epsilon) return 0;
    float pressureSum = 0;

    float densitySq = max(Densities[i] * Densities[i], Epsilon);
    
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

                    float3 d_ji = deltaTime * deltaTime * particleMass * grad / densitySq;

                    float3 inner = DPSum[i] - Dii[j] * IterPressures[j] - (DPSum[j] - d_ji * IterPressures[i]);

                    pressureSum += dot(inner, grad);
                }
            }
        }
    }

    float nextPressure = (1 - relaxationFactor) * IterPressures[i] + 
                        (relaxationFactor / Aii[i]) * (restDensity - Densities[2 * instanceCount + i] - particleMass * pressureSum);

    // return nextPressure;
    return max(0, nextPressure);
}

void CalculateIISPHComponents(uint i, out float3 viscosity, out float3 surfaceTension, out float3 D) {
    viscosity = float3(0, 0, 0);
    surfaceTension = float3(0, 0, 0);
    D = float3(0, 0, 0);

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