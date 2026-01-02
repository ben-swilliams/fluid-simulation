// 0 = cubic, 1 = spiky, 2 = poly6
uint pressureKernel;
uint nearPressureKernel;
uint densityKernel;
uint nearDensityKernel;
uint surfaceTensionKernel;
uint viscosityKernel;
uint xsphKernel;

float cubicKernelConstant;
float spikyKernelConstant;
float poly6KernelConstant;

float CubicSplineKernel(float r) {
    float q = r / smoothingRadius;

    if (isnan(q) || q >= 2 || r < Epsilon) return 0;

    float result;
    if (q >= 1) {
        result = 0.25 * pow(2 - q, 3);
    } else {
        result = (1 - 1.5 * q * q + 0.75 * q * q * q);
    }

    return cubicKernelConstant * result;
}

float3 CubicSplineGrad(float3 offset, float r) {
    float q = r / smoothingRadius;
    if (q >= 2 || isnan(q) || r < Epsilon) return float3(0, 0, 0);
    
    float3 dir = offset / r;
    float coeff;

    if (q >= 1) {
        coeff = -0.75 * (2 - q) * (2 - q);
    } else {
        coeff = -3 * q + 2.25 * q * q;
    }

    return (6 / smoothingRadius) * cubicKernelConstant * coeff * dir;
}

float SpikyKernel(float r) {
    if (isnan(r) || r > smoothingRadius) return 0;

    return spikyKernelConstant * pow(smoothingRadius - r, 3);
}

float3 SpikyGrad(float3 offset, float r) {
    if (isnan(r) || r > smoothingRadius || r < Epsilon) return float3(0, 0, 0);

    float3 dir = offset / r;

    float coeff = (smoothingRadius - r) * (smoothingRadius - r);

    return 3 * spikyKernelConstant * coeff * dir;
}

float Poly6Kernel(float r) {
    if (isnan(r) || r > smoothingRadius) return 0;

    float diff = smoothingRadius * smoothingRadius - r * r;
    
    return poly6KernelConstant * diff * diff * diff;
}

float3 Poly6Grad(float3 offset, float r) {
    if (isnan(r) || r > smoothingRadius || r < Epsilon) return float3(0, 0, 0);

    float3 dir = offset / r;

    float diff = smoothingRadius * smoothingRadius - r * r;

    return -6 * poly6KernelConstant * diff * diff * dir;
}

float PressureKernel(float r) {
    if (pressureKernel == 0)
        return CubicSplineKernel(r);
    if (pressureKernel == 1)
        return SpikyKernel(r);
    if (pressureKernel == 2)
        return Poly6Kernel(r);

    return CubicSplineKernel(r);
}

float3 PressureGrad(float3 offset, float r) {
    if (pressureKernel == 0)
        return CubicSplineGrad(r);
    if (pressureKernel == 1)
        return SpikyGrad(r);
    if (pressureKernel == 2)
        return Poly6Grad(r);

    return CubicSplineGrad(offset, r);
}

float NearPressureKernel(float r) {
    if (nearPressureKernel == 0)
        return CubicSplineKernel(r);
    if (nearPressureKernel == 1)
        return SpikyKernel(r);
    if (nearPressureKernel == 2)
        return Poly6Kernel(r);

    return SpikyKernel(r);
}

float3 NearPressureGrad(float3 offset, float r) {
    if (nearPressureKernel == 0)
        return CubicSplineGrad(r);
    if (nearPressureKernel == 1)
        return SpikyGrad(r);
    if (nearPressureKernel == 2)
        return Poly6Grad(r);

    return SpikyKernelGrad(offset, r);
}

float DensityKernel(float r) {
    if (densityKernel == 0)
        return CubicSplineKernel(r);
    if (densityKernel == 1)
        return SpikyKernel(r);
    if (densityKernel == 2)
        return Poly6Kernel(r);

    return CubicSplineKernel(r);
}

float3 DensityGrad(float3 offset, float r) {
    if (densityKernel == 0)
        return CubicSplineGrad(r);
    if (densityKernel == 1)
        return SpikyGrad(r);
    if (densityKernel == 2)
        return Poly6Grad(r);

    return CubicSplineGrad(offset, r);
}

float NearDensityKernel(float r) {
    if (nearDensityKernel == 0)
        return CubicSplineKernel(r);
    if (nearDensityKernel == 1)
        return SpikyKernel(r);
    if (nearDensityKernel == 2)
        return Poly6Kernel(r);

    return SpikyKernel(r);
}

float3 NearDensityGrad(float3 offset, float r) {
    if (nearDensityKernel == 0)
        return CubicSplineGrad(r);
    if (nearDensityKernel == 1)
        return SpikyGrad(r);
    if (nearDensityKernel == 2)
        return Poly6Grad(r);
    return SpikyKernelGrad(offset, r);
}

float SurfaceTensionKernel(float r) {
    if (surfaceTensionKernel == 0)
        return CubicSplineKernel(r);
    if (surfaceTensionKernel == 1)
        return SpikyKernel(r);
    if (surfaceTensionKernel == 2)
        return Poly6Kernel(r);

    return CubicSplineKernel(r);
}

float3 ViscosityGrad(float3 offset, float r) {
    if (viscosityKernel == 0)
        return CubicSplineGrad(r);
    if (viscosityKernel == 1)
        return SpikyGrad(r);
    if (viscosityKernel == 2)
        return Poly6Grad(r);

    return CubicSplineGrad(offset, r);
}

float XSPHKernel(float r) {
    if (xsphKernel == 0)
        return CubicSplineKernel(r);
    if (xsphKernel == 1)
        return SpikyKernel(r);
    if (xsphKernel == 2)
        return Poly6Kernel(r);

    return CubicSplineKernel(r);
}