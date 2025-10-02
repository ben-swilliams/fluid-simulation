float pressureKernelConstant;

float gasConstant;
float restDensity;

float PressureKernel(float2 offset) {
    if (dot(offset, offset) > smoothingRadius * smoothingRadius) return 0;

    float r = length(offset);
    float inner = smoothingRadius - r;

    return pressureKernelConstant * inner * inner * inner;
}

float2 PressureKernelGrad(float2 offset) {
    float r = length(offset);
    float diffSq = (r - smoothingRadius) * (r - smoothingRadius);

    return float2(3 * diffSq, -3 * diffSq);
}

float CalculatePressure(float2 pointWorld) {
    float density = CalculateDensity(pointWorld);

    return gasConstant * (density - restDensity);
}

float2 CalculatePressureForce(uint i) {
    float2 pForce = 0;

    float pressureI = CalculatePressure(Positions[i]);

    for (uint j = 0; j < instanceCount; j++) {
        float pressureJ = CalculatePressure(Positions[j]);
        float densityJ = CalculateDensity(Positions[j]);
        float2 offset = Positions[i] - Positions[j];

        pForce += PressureKernelGrad(offset);
    }

    return pForce;
}