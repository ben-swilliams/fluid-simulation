float velocitySmoothing;

float3 CalculateXSPHContribution(float densityJ, float3 posOffset, float3 velOffset, float kernel) {
    float massOverDensity = particleMass / densityJ;

    return velocitySmoothing * massOverDensity * kernel * -velOffset;
}
