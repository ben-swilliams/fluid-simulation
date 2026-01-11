static const uint NumThreads = 256;
static const float Epsilon = 1e-12;
static const int3 offsets[27] =
{
	int3(-1, -1, -1),
	int3(0, -1, -1),
	int3(1, -1, -1),

	int3(-1, 0, -1),
	int3(0, 0, -1),
	int3(1, 0, -1),

	int3(-1, 1, -1),
	int3(0, 1, -1),
	int3(1, 1, -1),

	int3(-1, -1, 0),
	int3(0, -1, 0),
	int3(1, -1, 0),

	int3(-1, 0, 0),
	int3(0, 0, 0),
	int3(1, 0, 0),

	int3(-1, 1, 0),
	int3(0, 1, 0),
	int3(1, 1, 0),

	int3(-1, -1, 1),
	int3(0, -1, 1),
	int3(1, -1, 1),

	int3(-1, 0, 1),
	int3(0, 0, 1),
	int3(1, 0, 1),

	int3(-1, 1, 1),
	int3(0, 1, 1),
	int3(1, 1, 1)
};

uint instanceCount;
uint tableSize;

float smoothingRadius;
float deltaTime;
float particleMass;
float maxVelocity;
float restDensity;
float size;
float dampingFactor;
float power;
float mouseRadius;
float surfaceTensionMultiplier;
float velocitySmoothing;

float3 containerSize;
float3 mousePos;
float3 gravity;

// Integer buffers
RWStructuredBuffer<uint> Offsets;

// Float buffers
RWStructuredBuffer<float> Pressures;
RWStructuredBuffer<float> Densities;

// Float3 buffers
RWStructuredBuffer<float3> IntermediateAccelerations;
RWStructuredBuffer<float3> Velocities;
RWStructuredBuffer<float3> Positions;
