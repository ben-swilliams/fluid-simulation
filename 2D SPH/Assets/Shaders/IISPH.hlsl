float relaxationFactor;

float3 CalculateDContribution(float3 grad, float densitySq) {
    float3 d = (particleMass / densitySq) * grad;
    return -deltaTime * deltaTime * d;
}

float2 CalculateDeltaDensityAndA(uint i) {
    float deltaDensity = 0;
    float a = 0;

    float densitySq = max(Densities[i] * Densities[i], Epsilon);

    float3 posI = Positions[i];
    int3 gridPosI = GetGridPos(posI);

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

    float3 posI = Positions[i];

    int3 gridPosI = GetGridPos(posI);

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
                    float3 offset = posI - Positions[j];

                    float densitySq = max(Densities[j] * Densities[j], Epsilon);

                    pressureSum += -particleMass * IterPressures[j] * CubicSplineGrad(offset) / densitySq;
                }
            }
        }
    }

    return deltaTime * deltaTime * pressureSum;
}

float CalculateNextIISPHPressureValue(uint i) {
    float aii = Aii[i];
    if (abs(aii) < Epsilon) return IterPressures[i];
    float pressureSum = 0;

    float densitySq = max(Densities[i] * Densities[i], Epsilon);
    
    float3 posI = Positions[i];
    float3 dpSumI = DPSum[i];
    float pressureI = IterPressures[i];
    int3 gridPosI = GetGridPos(posI);

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
                    float3 offset = posI - Positions[j];
                    float3 grad = CubicSplineGrad(offset);

                    float3 d_ji = deltaTime * deltaTime * particleMass * grad / densitySq;

                    float3 inner = dpSumI - Dii[j] * IterPressures[j] - (DPSum[j] - d_ji * pressureI);

                    pressureSum += dot(inner, grad);
                }
            }
        }
    }

    float nextPressure = (1 - relaxationFactor) * pressureI + 
                        (relaxationFactor / aii) * (restDensity - Densities[2 * instanceCount + i] - particleMass * pressureSum);

    // return nextPressure;
    return max(0, nextPressure);
}

void CalculateIISPHComponents(uint i, out float3 viscosity, out float3 surfaceTension, out float3 D) {
    viscosity = float3(0, 0, 0);
    surfaceTension = float3(0, 0, 0);
    D = float3(0, 0, 0);

    float3 posI = Positions[i];
    float3 velI = Velocities[i];
    int3 gridPosI = GetGridPos(posI);

    float densitySq = max(Densities[i] * Densities[i], Epsilon);

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
                    float rSq = dot(posOffset, posOffset);

                    if (rSq < Epsilon * Epsilon || rSq > 4 * smoothingRadius * smoothingRadius) continue;

                    float r = length(posOffset);

                    float3 velOffset = velI - Velocities[j];

                    float kernel = CubicSplineKernel(posOffset);
                    float3 grad = CubicSplineGrad(posOffset);

                    viscosity += CalculateViscosityContribution(posOffset, velOffset, grad, i, j);
                    surfaceTension += CalculateSurfaceTensionContribution(posOffset, kernel, r);
                    D += CalculateDContribution(grad, densitySq);
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
                    float3 velOffset = velI - Velocities[j];

                    float kernel = CubicSplineKernel(posOffset);
                    float3 grad = CubicSplineGrad(posOffset);

                    pressureForce += CalculatePressureContribution(posOffset, grad, i, j);
                    xsphCorrection += CalculateXSPHContribution(Densities[j], posOffset, velOffset, kernel);
                }
            }
        }
    }

    return particleMass * pressureForce + xsphCorrection;
}