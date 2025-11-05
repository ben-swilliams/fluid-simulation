float surfaceTensionMultiplier;

float3 CalculateSurfaceTensionContribution(uint i, uint j) {
    float3 offset = Positions[i] - Positions[j];
    float r = length(offset);

    if (r > smoothingRadius || r < 1e-7) return float3(0, 0, 0);

    return -surfaceTensionMultiplier * CubicSplineKernel(offset) * (offset / r);
}