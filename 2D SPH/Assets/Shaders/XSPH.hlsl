float velocitySmoothing;

float3 CalculateXSPHContribution(float densityJ, float3 posOffset, float3 velOffset) {
    float massOverDensity = particleMass / densityJ;
    float kernel = CubicSplineKernel(posOffset);

    return velocitySmoothing * massOverDensity * kernel * -velOffset;
}
