float lowHue;
float highHue;

float maxSpeed;
float maxDensityFluctuation;
float maxPressure;

float3 hsv2rgb(float3 c) {
    float4 K = float4(1.0, 2.0/3.0, 1.0/3.0, 3.0);
    float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
    return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
}

float3 CalculateColourFromProp(float value, float propMax) {
    float propNorm;
    propNorm = saturate(prop / propMax);

    float hue = lerp(lowHue, highHue, propNorm);
    float3 rgb = hsv2rgb(float3(hue, 1.0, 1.0));

    return rgb;
}

float3 ColourFromVelocity(uint i) {
    return CalculateColourFromProp(length(Velocities[i]), maxSpeed);
}

float3 ColourFromDensity(uint i) {
    // Remap density/maximum to re-use my property colour function
    float minDensity = (1 - maxDensityFluctuation) * restDensity;
    float maxDensity = (1 + maxDensityFluctuation) * restDensity;
    float normalisedDensity = saturate((Densities[i] - minDensity) / (maxDensity - minDensity));

    float remappedDensity = 
    return CalculateColourFromProp(normalisedDensity, 1);
}

float3 ColourFromPressure(uint i) {
    return CalculateColourFromProp(Pressures[i], maxPressure);
}