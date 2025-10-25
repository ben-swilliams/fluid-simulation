void Collisions(uint idx)
{
    if (Positions[idx].y < -containerSize.y / 2 + size / 2) { 
        Velocities[idx].y *= -dampingFactor;
        Positions[idx].y = -containerSize.y / 2 + size / 2;
    }

    if (Positions[idx].y > containerSize.y / 2 - size / 2) {
        Velocities[idx].y *= -dampingFactor;
        Positions[idx].y = containerSize.y / 2 - size / 2;
    }

    if (Positions[idx].x < -containerSize.x / 2 + size / 2) {
        Velocities[idx].x *= -dampingFactor;
        Positions[idx].x = -containerSize.x / 2 + size / 2;
    }

    if (Positions[idx].x > containerSize.x / 2 - size / 2) {
        Velocities[idx].x *= -dampingFactor;
        Positions[idx].x = containerSize.x / 2 - size / 2;
    }

    if (Positions[idx].z < -containerSize.z / 2 + size / 2) {
        Velocities[idx].z *= -dampingFactor;
        Positions[idx].z = -containerSize.z / 2 + size / 2;
    }

    if (Positions[idx].z > containerSize.z / 2 - size / 2) {
        Velocities[idx].z *= -dampingFactor;
        Positions[idx].z = containerSize.z / 2 - size / 2;
    }
}

float3 MouseForce(uint idx) {
    float3 diff = mousePos - Positions[idx];
    if (dot(diff, diff) > mouseRadius * mouseRadius) return float3(0, 0, 0);

    float3 forceVector = normalize(diff) * power;

    return forceVector / particleMass;
}
