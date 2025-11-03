float kernelConstant;
float gradConstant;

float CubicSplineKernel(float3 offset) {
    float r = length(offset);
    float q = r / smoothingRadius;

    if (q >= 2) return 0;

    float result;
    if (q >= 1) {
        result = 0.25 * pow(2 - q, 3);
    } else {
        result = (1 - 1.5 * q * q + 0.75 * q * q * q);
    }

    return kernelConstant * result;
}

float3 CubicSplineGrad(float3 offset) {
    float r = length(offset);
    float q = r / smoothingRadius;
    if (q < 1e-7 || q >= 2) return float3(0, 0, 0);
    
    float3 dir = offset / r;
    float coeff;

    if (q >= 1) {
        coeff = -0.75 * (2 - q) * (2 - q);
    } else {
        coeff = -3 * q + 2.25 * q * q;
    }

    return gradConstant * coeff * dir;
}

float SpikyKernel(float3 offset) {
    float r = length(offset);
    float q = r / smoothingRadius;

    return pow(1 - q, 3);
}

float3 SpikyKernelGrad(float3 offset) {
    float r = length(offset);
    float q = r / smoothingRadius;

    if (q < 1e-7 || r > smoothingRadius) return float3(0, 0, 0);

    float3 dir = offset / r;

    float coeff = (1 - q) * (1 - q);

    return coeff * dir;
}