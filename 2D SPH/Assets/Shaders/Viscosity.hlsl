float viscosityKernelGradConstant;

float viscosityMultiplier;

float3 CalculateViscosityContribution(uint i, uint j) {
    float3 posOffset = Positions[i] - Positions[j];
    float3 velOffset = Velocities[i] - Velocities[j];
    float r = length(posOffset);

    if (r < 1e-6) return float2(0, 0);

    float3 gradient = CubicSplineGrad(posOffset);

    float viscosity = viscosityMultiplier;

    return 2.0 * viscosity * particleMass * velOffset * dot(posOffset, gradient) /
            (Densities[j] * (dot(posOffset, posOffset) + 0.01 * smoothingRadius * smoothingRadius));
}