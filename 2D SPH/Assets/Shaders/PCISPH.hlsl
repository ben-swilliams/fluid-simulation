float beta;
float delta;

void CalculatePCISPHComponents(uint i, out float3 viscosity, out float3 surfaceTension, out float3 pressure, out float3 xsph) {
    viscosity = 0;
    surfaceTension = 0;
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
                    float rSq = dot(posOffset, posOffset);

                    if (rSq < Epsilon * Epsilon || rSq > 4 * smoothingRadius * smoothingRadius) continue;

                    float r = length(posOffset);
                    float3 velOffset = velI - Velocities[j];
                    
                    float kernel = CubicSplineKernel(r);
                    float3 grad = CubicSplineGrad(posOffset, r);

                    viscosity += CalculateViscosityContribution(posOffset, velOffset, grad, i, j);
                    surfaceTension += -surfaceTensionMultiplier * kernel * (posOffset / r);
                    pressure += CalculatePressureContribution(posOffset, grad, i, j);

                    float massOverDensity = particleMass / Densities[j];
                    xsph += velocitySmoothing * massOverDensity * kernel * -velOffset;
                }
            }
        }
    }
}

float3 CalculatePCISPHAcceleration(int i) {
    if (deltaTime == 0) return 0;
    float3 viscosity;
    float3 surfaceTension;
    float3 pressure;
    float3 xsph;

    CalculatePCISPHComponents(i, viscosity, surfaceTension, pressure, xsph);

    // Divide XSPH by deltaTime to make it a direct velocity update
    return MouseForce(i) + viscosity + surfaceTension + gravity + pressure + xsph / deltaTime;
}

float CalculatePressureChange(int i) {
    float predictedDensity = 0;

    float3 posI = PredictedPositions[i];
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
                    float3 offset = posI - PredictedPositions[j];
                    float r = length(offset);

                    predictedDensity += particleMass * CubicSplineKernel(r);
                }
            }
        }
    } 

    float densityError = predictedDensity - restDensity;

    return delta * densityError;
}

float3 CalculatePCISPHPressureForce(int i) {
    float3 pressureForce = 0;

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
                    float rSq = dot(offset, offset);

                    if (rSq < Epsilon * Epsilon || rSq > 4 * smoothingRadius * smoothingRadius) continue;

                    float r = length(offset);

                    float3 grad = CubicSplineGrad(offset, r);
                    
                    pressureForce += CalculatePressureContribution(offset, grad, i, j);
                }
            }
        }
    } 

    return pressureForce;
}