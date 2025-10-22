float viscosityKernelGradConstant;

float viscosityMultiplier;

float2 ViscosityKernelGrad(float2 offset) {
    float r = length(offset);

    if (r > smoothingRadius) return float2(0, 0);

    float a = 3 * r / (2 * smoothingRadius * smoothingRadius * smoothingRadius);
    float b = 2 / (smoothingRadius * smoothingRadius);
    float c = smoothingRadius / (2 * r * r * r);

    return viscosityKernelGradConstant * (-a + b - c) * offset;
}
float2 CalculateViscosityContribution(uint i, uint j) {
    float2 posOffset = Positions[i] - Positions[j];
    float2 velOffset = Velocities[i] - Velocities[j];

    float velPosDot = dot(velOffset, posOffset);

    if (velPosDot >= 0) return float2(0, 0);

    float2 gradient = PressureKernelGrad(posOffset);

    float viscosityCoefficient = viscosityMultiplier * smoothingRadius / (Densities[i] + Densities[j]);
    
    float numerator = viscosityCoefficient * velPosDot;

    float posOffsetSq = dot(posOffset, posOffset);
    float epsilon = 0.01;
    float denominator = posOffsetSq + epsilon * smoothingRadius * smoothingRadius;

    return particleMass * (numerator / denominator) * gradient;
}