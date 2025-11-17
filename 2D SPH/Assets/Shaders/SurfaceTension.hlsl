float surfaceTensionMultiplier;

float3 CalculateSurfaceTensionContribution(float3 offset, float kernel, float r) {
    return -surfaceTensionMultiplier * kernel * (offset / r);
}