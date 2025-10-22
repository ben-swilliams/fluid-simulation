float viscosityKernelGradConstant;

float viscosityMultiplier;

float2 CalculateViscosityContribution(uint i, uint j) {
    float2 posOffset = Positions[i] - Positions[j];
    float2 velOffset = Velocities[i] - Velocities[j];

    float velPosDot = dot(velOffset, posOffset);

    if (velPosDot >= 0) return float2(0, 0);

    float2 gradient = SpikyKernelGrad(posOffset);

    float viscosityCoefficient = viscosityMultiplier * smoothingRadius / (Densities[i] + Densities[j]);
    
    float numerator = viscosityCoefficient * velPosDot;

    float posOffsetSq = dot(posOffset, posOffset);
    float epsilon = 0.01;
    float denominator = posOffsetSq + epsilon * smoothingRadius * smoothingRadius;

    return particleMass * (numerator / denominator) * gradient;
}