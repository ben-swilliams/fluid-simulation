using UnityEngine;

class Density : MonoBehaviour
{
    [SerializeField] GameObject simObject;

    Simulate sim;
    Spawn spawn;

    private float smoothingRadiusSq;
    private float kernelConstant;

    void Start()
    {
        spawn = simObject.GetComponent<Spawn>();
        sim = simObject.GetComponent<Simulate>();
    }

    void Update()
    {
        CalculateConstants();
        if (!sim.Started) return;

        Debug.Log(CalculateDensity());
    }

    void CalculateConstants()
    {
        smoothingRadiusSq = Mathf.Pow(GetComponent<Transform>().localScale.x / 2, 2);
        kernelConstant = 315 / (64 * Mathf.PI * Mathf.Pow(smoothingRadiusSq, 4.5f));
    }

    float CalculateDensity()
    {
        Vector2 centre = GetComponent<Transform>().position;
        Vector2[] positions = sim.GetPositions();

        float density = 0;

        for (int p = 0; p < spawn.InstanceCount; p++)
        {
            Vector2 offset = positions[p] - centre;
            density += SmoothingKernel(offset);
        }

        return density;
    }

    float SmoothingKernel(Vector2 offset)
    {
        if (offset.sqrMagnitude > smoothingRadiusSq) return 0;

        return kernelConstant * Mathf.Pow(smoothingRadiusSq - offset.sqrMagnitude, 3);

    }
}