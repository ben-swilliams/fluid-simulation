void Collisions(uint idx) {
    if (Positions[idx].y < -containerSize.y/2 + size/2){ 
        Velocities[idx].y = Velocities[idx].y * -dampingFactor;
        Positions[idx].y = -containerSize.y/2 + size/2;
    }

    if (Positions[idx].y > containerSize.y/2 - size/2) {
        Velocities[idx].y = Velocities[idx].y * -dampingFactor;
        Positions[idx].y = containerSize.y/2 - size/2;
    }

    if (Positions[idx].x < -containerSize.x/2 + size / 2) {
        Velocities[idx].x = Velocities[idx].x * -dampingFactor;
        Positions[idx].x = -containerSize.x/2 + size/2;
    }

    if (Positions[idx].x > containerSize.x/2 - size / 2) {
        Velocities[idx].x = Velocities[idx].x * -dampingFactor;
        Positions[idx].x = containerSize.x/2 - size/2;
    }
}

float2 MouseForce(uint idx) {
    float2 diff = mousePos - Positions[idx];
    if (dot(diff, diff) > mouseRadius * mouseRadius) return float2(0, 0);

    float2 forceVector = normalize(diff) * power;

    return forceVector / particleMass;
}
