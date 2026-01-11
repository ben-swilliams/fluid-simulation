float3 GetCollisions(float3 pos)
{
    float3 collision = float3(0, 0, 0);

    if (pos.x < -containerSize.x / 2 + size / 2) {
        collision.x = -1;
    }

    if (pos.x > containerSize.x / 2 - size / 2) {
        collision.x = 1;
    }

    if (pos.y < -containerSize.y / 2 + size / 2) { 
        collision.y = -1;
    }

    if (pos.y > containerSize.y / 2 - size / 2) {
        collision.y = 1;
    }

    if (pos.z < -containerSize.z / 2 + size / 2) {
        collision.z = -1;
    }

    if (pos.z > containerSize.z / 2 - size / 2) {
        collision.z = 1;
    }

    return collision;
}

float3 CollideVelocity(float3 vel, float3 collision) {
    vel *= float3(
        collision.x == 0 ? 1 : -dampingFactor,
        collision.y == 0 ? 1 : -dampingFactor,
        collision.z == 0 ? 1 : -dampingFactor
    );

    return vel;
}

float3 CollideVelocity(uint i) {
    float3 collision = GetCollisions(Positions[i] + Velocities[i] * deltaTime);

    return CollideVelocity(Velocities[i], collision);
}

float3 GetCollisionOffset(float3 pos, float3 collision) {
    float3 container = containerSize / 2 - size / 2;
    float3 collisionPoint = collision * container;

    float3 reflected = 2 * collisionPoint - pos;

    float3 offset = reflected - pos;

    return offset * abs(collision);
}

void Collisions(uint i) {
    float3 collision = GetCollisions(Positions[i]);
    Velocities[i] = CollideVelocity(Velocities[i], collision);

    Positions[i] += GetCollisionOffset(Positions[i], collision);
}

float3 MouseForce(uint idx) {
    float3 diff = mousePos - Positions[idx];
    float r = length(diff);
    if (r > mouseRadius) return float3(0, 0, 0);

    float t = r / mouseRadius;
    float mag = lerp(power, 0, t);

    float3 forceVector = normalize(diff) * mag;

    return forceVector / particleMass;
}
