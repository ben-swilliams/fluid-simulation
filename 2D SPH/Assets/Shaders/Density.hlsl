const float eps = 1e-4;

float kernelConstant;

float SmoothingKernel(float2 offset) {
    float oSquared = dot(offset, offset);
    if (oSquared > smoothingRadius * smoothingRadius) {
        return 0;
    } else {

        float diff = (smoothingRadius * smoothingRadius) - oSquared;

        return kernelConstant * diff * diff * diff;
    }
}

float CalculateDensity(float2 pointWorld) {
    float density = 0;

    for (uint i = 0; i < instanceCount; i++) {
        float2 offset = PredictedPositions[i] - pointWorld;
        density += particleMass * SmoothingKernel(offset);
    }

    return max(eps, density);
}