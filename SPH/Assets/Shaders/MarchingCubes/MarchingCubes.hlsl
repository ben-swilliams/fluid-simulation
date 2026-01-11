int FlattenedIndex(int3 idx) {
    return idx.x + idx.y * densityTexDims.x + idx.z * densityTexDims.x * densityTexDims.y;
}

float3 VoxelToWorld(int3 voxelCoord) {
    float3 normCoord = (voxelCoord / (densityTexDims - 1.0)) - 0.5;
    return normCoord * containerSize;
}

float SampleDensityTexture(uint3 voxelCoord)
{
	bool isEdge = any(voxelCoord <= 0 || voxelCoord >= densityTexDims - 1);
	if (isEdge) return isoLevel;

	float3 uvw = voxelCoord / (float3) (densityTexDims - 1);
	return DensityTex.SampleLevel(linearClampSampler, uvw, 0);
}

float3 CalculateNormal(int3 corner) {
    int3 offsetX = int3(1, 0, 0);
    int3 offsetY = int3(0, 1, 0);
    int3 offsetZ = int3(0, 0, 1);

    float dx = SampleDensityTexture(corner + offsetX) - SampleDensityTexture(corner - offsetX);
    float dy = SampleDensityTexture(corner + offsetY) - SampleDensityTexture(corner - offsetY);
    float dz = SampleDensityTexture(corner + offsetZ) - SampleDensityTexture(corner - offsetZ);

    return -normalize(float3(dx, dy, dz));
}

Vertex CreateVertex(int3 cornerA, int3 cornerB) {
    // We just need to find where along this edge the density would equal isoLevel

    float3 posA = VoxelToWorld(cornerA);
    float3 posB = VoxelToWorld(cornerB);

    float densityA = SampleDensityTexture(cornerA);
    float densityB = SampleDensityTexture(cornerB);

    // Isolevel is t along the edge (0-1)
    float t = (isoLevel - densityA) / (densityB - densityA);
    float3 pos = posA + t * (posB - posA);

    float3 normalA = CalculateNormal(cornerA);
    float3 normalB = CalculateNormal(cornerB);
    float3 normal = normalize(normalA + t * (normalB - normalA));

    Vertex v;
    v.position = pos;
    v.normal = normal;

    return v;
}