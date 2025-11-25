float viscosityKernelGradConstant;

float viscosityMultiplier;

// TODO: Verify this is right
float3 CalculateViscosityContribution(float3 posOffset, float3 velOffset, float3 grad, uint i, uint j) {
    float velPosDot = dot(velOffset, posOffset);

    if (velPosDot >= 0) return float3(0, 0, 0);

    float viscosityCoefficient = 2 * viscosityMultiplier * smoothingRadius / (Densities[3 * i] + Densities[3 * j]);
    float Pi = viscosityCoefficient * (velPosDot / max(dot(posOffset, posOffset), Epsilon));

    return particleMass * Pi * grad;
}