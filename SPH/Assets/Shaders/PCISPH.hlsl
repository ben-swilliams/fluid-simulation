float beta;
float delta;

void CalculatePCISPHComponents(uint i, out float3 viscosity, out float3 surfaceTension, out float3 xsph) {
    viscosity = 0;
    surfaceTension = 0;
    xsph = 0;

    float3 posI = Positions[i];
    int3 gridPosI = GetGridPos(posI);

    float3 velI = Velocities[i];
    float densityI = Densities[3 * i];
    float nearDensityI = Densities[3 * i + 1];

    for (int o = 0; o < 27; o++) {
        int3 gridPosJ = gridPosI + offsets[o];
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

            float densityJ = Densities[3 * j];
            float nearDensityJ = Densities[3 * j + 1];
            
            viscosity += CalculateViscosityContribution(posOffset, velOffset, ViscosityGrad(posOffset, r), densityI, densityJ);
            surfaceTension += -surfaceTensionMultiplier * SurfaceTensionKernel(r) * (posOffset / r);

            float massOverDensity = particleMass / Densities[3 * j];
            xsph += velocitySmoothing * massOverDensity * XSPHKernel(r) * -velOffset;
        }
    }
}

float3 CalculatePCISPHAcceleration(int i) {
    if (deltaTime == 0) return 0;
    float3 viscosity;
    float3 surfaceTension;
    float3 xsph;

    CalculatePCISPHComponents(i, viscosity, surfaceTension, xsph);

    // Divide XSPH by deltaTime to make it a direct velocity update
    return MouseForce(i) + viscosity + surfaceTension + gravity + xsph / deltaTime;
}

float CalculatePressureChange(int i) {
    float predictedDensity = 0;

    float3 posI = PredictedPositions[i];
    int3 gridPosI = GetGridPos(Positions[i]);

    for (int o = 0; o < 27; o++) {
        int3 gridPosJ = gridPosI + offsets[o];
        if (!IsInBounds(gridPosJ)) continue;

        uint hash = CalculateHashFromGrid(gridPosJ);

        uint startIndex = Offsets[hash];
        uint endIndex = Offsets[hash + 1];

        for (uint j = startIndex; j < endIndex; j++) {
            float3 offset = posI - PredictedPositions[j];
            float r = length(offset);

            predictedDensity += particleMass * DensityKernel(r);
        }
    } 

    float densityError = predictedDensity - restDensity;

    return delta * densityError;
}

float3 CalculatePCISPHPressureForce(int i) {
    float3 pressureForce = 0;

    float3 posI = Positions[i];
    int3 gridPosI = GetGridPos(posI);
    float densityI = Densities[3 * i];
    float nearDensityI = Densities[3 * i + 1];

    for (int o = 0; o < 27; o++) {
        int3 gridPosJ = gridPosI + offsets[o];
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

            float3 grad = PressureGrad(offset, r);
            
            pressureForce += CalculatePressureContribution(offset, grad, i, j, densityI, Densities[3 * j], nearDensityI, Densities[3 * j + 1]);
        }
    } 

    return pressureForce;
}