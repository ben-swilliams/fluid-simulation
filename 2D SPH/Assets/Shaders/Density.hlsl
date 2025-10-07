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

float CalculateDensity(uint i) {
    float density = 0;

    for (uint j = 0; j < instanceCount; j++) {
        float2 offset = PredictedPositions[j] - PredictedPositions[i];
        density += particleMass * SmoothingKernel(offset);
    }

    return max(eps, density);
}