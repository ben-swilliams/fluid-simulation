float viscosityKernelLapConstant;

float viscosityMultiplier;

float ViscosityKernelLap(float2 offset) {
    float r = length(offset);

    if (r > smoothingRadius) return 0;

    return viscosityKernelLapConstant * (smoothingRadius - r);
}

float2 CalculateViscosityContribution(uint i, uint j) {
    float2 posOffset = Positions[i] - Positions[j];
    float2 velOffset = Velocities[j] - Velocities[i];

    float laplacian = ViscosityKernelLap(posOffset);

    return particleMass * (velOffset / Densities[j]) * laplacian;
}