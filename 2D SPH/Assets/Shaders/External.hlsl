float size;
float dampingFactor;

float2 gravity;
float2 containerSize;

void Collisions(uint idx) {
    // Bottom
    if (Positions[idx].y < -containerSize.y/2 + size/2){ 
        Velocities[idx].y = Velocities[idx].y * -dampingFactor;
        Positions[idx].y = -containerSize.y/2 + size/2;
    }

    // Top
    if (Positions[idx].y > containerSize.y/2 - size/2) {
        Velocities[idx].y = Velocities[idx].y * -dampingFactor;
        Positions[idx].y = containerSize.y/2 - size/2;
    }

    // Left
    if (Positions[idx].x < -containerSize.x/2 + size / 2) {
        Velocities[idx].x = Velocities[idx].x * -dampingFactor;
        Positions[idx].x = -containerSize.x/2 + size/2;
    }

    // Right
    if (Positions[idx].x > containerSize.x/2 - size / 2) {
        Velocities[idx].x = Velocities[idx].x * -dampingFactor;
        Positions[idx].x = containerSize.x/2 - size/2;
    }
}
