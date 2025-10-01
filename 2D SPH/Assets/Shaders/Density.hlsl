float smoothingRadiusSq;
float kernelConstant;
float kernelVolume;

float SmoothingKernel(float2 offset) {
    float oSquared = dot(offset, offset);
    if (oSquared > smoothingRadiusSq) {
        return 0;
    } else {

        float diff = smoothingRadiusSq - oSquared;

        return kernelConstant * diff * diff * diff;
    }
}

float CalculateDensity(float2 pointWorld) {
    float density = 0;

    for (uint i = 0; i < instanceCount; i++) {
        float2 offset = Positions[i] - pointWorld;
        density += SmoothingKernel(offset);
    }

    return density / kernelVolume;
}