float viscosityKernelLapConstant;

float ViscosityKernelLap(float2 offset) {
    float r = length(offset);

    if (r < 0.0001 || r > smoothingRadius)
        return 0;

    return viscosityKernelLapConstant * (smoothingRadius - r);
}