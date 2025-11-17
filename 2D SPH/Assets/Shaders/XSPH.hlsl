float velocitySmoothing;

float3 CalculateXSPHContribution(float densityJ, float3 posOffset, float3 velOffset, float kernel) {
    float massOverDensity = particleMass / densityJ;
    float kernel = kernel;

    return velocitySmoothing * massOverDensity * kernel * -velOffset;
}
