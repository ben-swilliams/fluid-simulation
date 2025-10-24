float surfaceTensionMultiplier;

float2 CalculateSurfaceTensionContribution(uint i, uint j) {
    float2 offset = Positions[i] - Positions[j];
    float r = length(offset);

    if (r > smoothingRadius || r < 1e-7) return float2(0, 0);

    return -surfaceTensionMultiplier * CubicSplineKernel(offset) * (offset / r);
}