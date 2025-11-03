float3 CalculatePressureContribution(uint i, uint j) {
    float3 offset = Positions[i] - Positions[j];

    float pressureI = Pressures[i] / (Densities[i] * Densities[i]);
    float pressureJ = Pressures[j] / (Densities[j] * Densities[j]);

    return (pressureI + pressureJ) * CubicSplineGrad(offset);
}