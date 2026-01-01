float relaxationFactor;

float3 CalculateDContribution(float3 grad, float densitySq) {
    float3 d = (particleMass / densitySq) * grad;
    return -deltaTime * deltaTime * d;
}

float2 CalculateDeltaDensityAndA(uint i, float densityI) {
    float deltaDensity = 0;
    float a = 0;

    float densitySq = max(densityI * densityI, Epsilon);

    float3 posI = Positions[i];
    int3 gridPosI = GetGridPos(posI);

    float3 velI = Velocities[i];
    float3 d_ii = Dii[i];

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

            float3 velOffset = velI - Velocities[j];

            float3 grad = CubicSplineGrad(posOffset, length(posOffset));

            deltaDensity += particleMass * dot(velOffset, grad);

            float3 d_ji = deltaTime * deltaTime * particleMass * grad / densitySq;
    
            a += dot(d_ii - d_ji, grad);
        }
    }

    a *= particleMass;

    return float2(deltaDensity, a);
}

float3 CalculatePressureSum(uint i) {
    float3 pressureSum = float3(0, 0, 0);

    float3 posI = Positions[i];

    int3 gridPosI = GetGridPos(posI);

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

            float density = Densities[3 * j];
            float densitySq = max(density * density, Epsilon);

            pressureSum += -particleMass * IterPressures[j] * CubicSplineGrad(offset, r) / densitySq;
        }
    }

    return deltaTime * deltaTime * pressureSum;
}

float CalculateNextIISPHPressureValue(uint i, float densityI) {
    float aii = Aii[i];
    if (abs(aii) < Epsilon) return IterPressures[i];
    float pressureSum = 0;

    float densitySq = max(densityI * densityI, Epsilon);
    
    float3 posI = Positions[i];
    float3 dpSumI = DPSum[i];
    float pressureI = IterPressures[i];
    int3 gridPosI = GetGridPos(posI);

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

            float3 grad = CubicSplineGrad(offset, r);

            float3 d_ji = deltaTime * deltaTime * particleMass * grad / densitySq;

            float3 inner = dpSumI - Dii[j] * IterPressures[j] - (DPSum[j] - d_ji * pressureI);

            pressureSum += dot(inner, grad);
        }
    }

    float nextPressure = (1 - relaxationFactor) * pressureI + 
                        (relaxationFactor / aii) * (restDensity - Densities[3 * i + 2] - particleMass * pressureSum);

    // return nextPressure;
    return max(0, nextPressure);
}

void CalculateIISPHComponents(uint i, float densityI, out float3 viscosity, out float3 surfaceTension, out float3 D) {
    viscosity = float3(0, 0, 0);
    surfaceTension = float3(0, 0, 0);
    D = float3(0, 0, 0);

    float3 posI = Positions[i];
    float3 velI = Velocities[i];
    int3 gridPosI = GetGridPos(posI);

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

            float densityJ = Densities[3 * j];

            float3 velOffset = velI - Velocities[j];

            viscosity += CalculateViscosityContribution(posOffset, velOffset, ViscosityGrad(posOffset, r), densityI, densityJ);
            surfaceTension += -surfaceTensionMultiplier * SurfaceTensionKernel(r) * (posOffset / r);
            D += CalculateDContribution(PressureGrad(posOffset, r), max(densityI * densityI, Epsilon));
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

    CalculateIISPHComponents(i, Densities[3 * i], viscosity, surfaceTension, D);

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

            float3 velOffset = velI - Velocities[j];

            float r = length(posOffset);

            pressureForce += CalculatePressureContribution(posOffset, PressureGrad(posOffset, r), i, j, densityI, Densities[3 * j], nearDensityI, Densities[3 * j + 1]);

            float massOverDensity = particleMass / Densities[3 * j];
            xsphCorrection += velocitySmoothing * massOverDensity * XSPHKernel(r) * -velOffset;
        }
    }

    return particleMass * pressureForce + xsphCorrection;
}