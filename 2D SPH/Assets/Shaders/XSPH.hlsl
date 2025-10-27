float velocitySmoothing;

float3 CalculateXSPHContribution(uint i, uint j) {
    float massOverDensity = particleMass / Densities[j];
    float3 velDiff = Velocities[j] - Velocities[i];
    float3 offset = Positions[i] - Positions[j];
    float kernel = CubicSplineKernel(offset);

    return velocitySmoothing * massOverDensity * kernel * velDiff;
}
