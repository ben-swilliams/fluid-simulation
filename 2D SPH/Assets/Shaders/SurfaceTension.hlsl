float surfaceTensionConstant;

float2 GeneralKernelGrad(float2 offset) {
    float r = length(offset);

    if (r > smoothingRadius) return 0;

    float diffSq = smoothingRadius * smoothingRadius - r * r;

    return generalKernelConstant * -6 * diffSq * diffSq * offset;
}

float GeneralKernelLap(float2 offset) {
    float r = length(offset);

    if (r > smoothingRadius) return 0;

    float hSq = smoothingRadius * smoothingRadius;
    float rSq = r * r;

    return 12 * generalKernelConstant * (hSq - rSq) * (3 * rSq - hSq);
}