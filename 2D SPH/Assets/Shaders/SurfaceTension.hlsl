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

    return generalKernelConstant * 24 * (hSq * hSq - rSq * rSq);
}

float2 CalculateSurfaceTensionForce(uint i) {
    float2 n = float2(0, 0);
    float lap = 0;

    int2 gridPosI = GetGridPos(Positions[i]);

    for (int x = -1; x < 2; x++) {
        for (int y = -1; y < 2; y++) {
            int2 gridPosJ = gridPosI + int2(x, y);
            if (!IsInBounds(gridPosJ)) continue;

            uint hash = CalculateHashFromGrid(gridPosJ);

            uint startIndex = Offsets[hash];
            uint endIndex = Offsets[hash + 1];

            for (uint j = startIndex; j < endIndex; j++) {
                if (i == j) continue;

                float2 offset = Positions[i] - Positions[j];

                n += GeneralKernelGrad(offset);
                lap += GeneralKernelLap(offset);
            }
        }
    }

    return -surfaceTensionConstant * lap * n / length(n);
}