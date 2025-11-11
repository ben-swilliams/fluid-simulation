float viscosityKernelGradConstant;

float viscosityMultiplier;

// TODO: Verify this is right
float3 CalculateViscosityContribution(float3 posOffset, float3 velOffset, float r, uint i, uint j) {
    float velPosDot = dot(velOffset, posOffset);

    if (r < Epsilon || velPosDot >= 0) return float3(0, 0, 0);

    float3 gradient = CubicSplineGrad(posOffset);

    float viscosityCoefficient = 2 * viscosityMultiplier * smoothingRadius / (Densities[i] + Densities[j]);
    float Pi = viscosityCoefficient * (velPosDot / max(dot(posOffset, posOffset), Epsilon));

    return particleMass * Pi * CubicSplineGrad(posOffset);
}