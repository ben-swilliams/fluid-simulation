float viscosityKernelGradConstant;

float viscosityMultiplier;

// TODO: Verify this is right
float3 CalculateViscosityContribution(uint i, uint j) {
    float3 posOffset = Positions[i] - Positions[j];
    float3 velOffset = Velocities[i] - Velocities[j];
    float velPosDot = dot(velOffset, posOffset);

    float r = length(posOffset);

    if (r < Epsilon || velPosDot >= 0) return float3(0, 0, 0);

    float3 gradient = CubicSplineGrad(posOffset);

    float viscosityCoefficient = 2 * viscosityMultiplier * smoothingRadius / (Densities[i] + Densities[j]);
    float Pi = viscosityCoefficient * (velPosDot / max(dot(posOffset, posOffset), Epsilon));

    return particleMass * Pi * CubicSplineKernel(posOffset);
}