float3 CalculatePressureContribution(uint i, uint j) {
    float3 offset = Positions[i] - Positions[j];

    float pressureI = Pressures[i] / (Densities[i] * Densities[i]);
    float pressureJ = Pressures[j] / (Densities[j] * Densities[j]);

    float nearPressureI = Pressures[instanceCount + i] / (pow(Densities[instanceCount + i], 2));
    float nearPressureJ = Pressures[instanceCount + j] / (pow(Densities[instanceCount + j], 2));

    return (pressureI + pressureJ) * CubicSplineGrad(offset) + 
            (nearPressureI + nearPressureJ) * SpikyKernelGrad(offset);
}