float surfaceTensionMultiplier;

float3 CalculateSurfaceTensionContribution(float3 offset, float r) {
    if (r > smoothingRadius || r < Epsilon) return float3(0, 0, 0);

    return -surfaceTensionMultiplier * CubicSplineKernel(offset) * (offset / r);
}