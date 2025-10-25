float viscosityKernelGradConstant;

float viscosityMultiplier;

float2 CalculateViscosityContribution(uint i, uint j) {
    float2 posOffset = Positions[i] - Positions[j];
    float2 velOffset = Velocities[i] - Velocities[j];
    float r = length(posOffset);

    if (r < 1e-6) return float2(0, 0);

    // Physical viscosity - always acts on velocity differences
    float2 gradient = CubicSplineGrad(posOffset);

    // Viscous force: ν * m_j * (v_i - v_j) / ρ_j * ∇W
    float viscosity = viscosityMultiplier; // Now directly the kinematic viscosity

    return 2.0 * viscosity * particleMass * velOffset * dot(posOffset, gradient) /
            (Densities[j] * (dot(posOffset, posOffset) + 0.01 * smoothingRadius * smoothingRadius));
}