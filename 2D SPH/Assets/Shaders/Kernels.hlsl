float poly6KernelConstant;

float spikyKernelGradConstant;

float nearPressureKernelGradConstant;

/*
KERNELS
*/
float Poly6Kernel(float2 offset) {
    float oSquared = dot(offset, offset);
    if (oSquared > smoothingRadius * smoothingRadius) {
        return 0;
    } else {

        float diff = (smoothingRadius * smoothingRadius) - oSquared;

        return poly6KernelConstant * diff * diff * diff;
    }
}

/*
GRADIENTS
*/
float2 Poly6KernelGrad(float2 offset) {
    float r = length(offset);

    if (r > smoothingRadius || r < 1e-7) return 0;

    float diffSq = smoothingRadius * smoothingRadius - r * r;

    return poly6KernelConstant * -6 * diffSq * diffSq * offset;
}

float2 SpikyKernelGrad(float2 offset) {
    float r = length(offset);
    
    if (r < 1e-7 || r > smoothingRadius)
        return float2(0, 0);
    
    float inner = smoothingRadius - r;
    return spikyKernelGradConstant * inner * inner * offset / r;
}


/*
LAPLACIANS
*/
float Poly6KernelLap(float2 offset) {
    float r = length(offset);

    if (r > smoothingRadius) return 0;

    float hSq = smoothingRadius * smoothingRadius;
    float rSq = r * r;

    return 12 * poly6KernelConstant * (hSq - rSq) * (3 * rSq - hSq);
}

/*
OUT OF USE FOR NOW
*/

float2 NearPressureKernelGrad(float2 offset) {
    float r = length(offset);

    if (r < 1e-7 || r > smoothingRadius)
        return float2(0, 0);

    float inner = 1 - r/smoothingRadius;

    return nearPressureKernelGradConstant * inner * inner * offset / r;
}
