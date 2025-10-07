float viscosityKernelLapConstant;

float viscosityMultiplier;

float ViscosityKernelLap(float2 offset) {
    float r = length(offset);

    if (r < 0.0001 || r > smoothingRadius)
        return 0;

    return viscosityKernelLapConstant * (smoothingRadius - r);
}

float2 CalculateViscosityForce(uint i) {
    float2 vForce = float2(0, 0);

    return viscosityMultiplier * vForce;
}