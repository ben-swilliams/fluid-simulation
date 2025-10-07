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

    for (uint j = 0; j < instanceCount; j++) {
        if (i == j) continue;

        float2 posOffset = PredictedPositions[i] - PredictedPositions[j];
        float2 velOffset = Velocities[j] - Velocities[i];

        float laplacian = ViscosityKernelLap(posOffset);
        
        vForce += particleMass * (velOffset / Densities[j]) * laplacian;
    }

    return viscosityMultiplier * vForce;
}