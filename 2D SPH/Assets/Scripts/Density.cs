using UnityEngine;

class Density : MonoBehaviour
{
    [SerializeField] ComputeShader densityShader;
    [SerializeField] GameObject simObject;
    [SerializeField] float smoothingRadius = 0.1f;


    Simulate sim;
    Spawn spawn;

    private float smoothingRadiusSq;
    private float kernelConstant;
    private float kernelVolume;

    void Start()
    {
        spawn = simObject.GetComponent<Spawn>();
        sim = simObject.GetComponent<Simulate>();
    }

    void Update()
    {
        UpdateConstants();
        if (!sim.Started) return;
    }

    void UpdateConstants()
    {
        smoothingRadiusSq = smoothingRadius * smoothingRadius;
        kernelConstant = 315 / (64 * Mathf.PI * Mathf.Pow(smoothingRadiusSq, 4.5f));
        kernelVolume = kernelConstant * Mathf.PI * Mathf.Pow(smoothingRadiusSq, 4) * 0.25f;

        densityShader.SetFloat("smoothingRadiusSq", smoothingRadiusSq);
        densityShader.SetFloat("kernelConstant", kernelConstant);
        densityShader.SetFloat("kernelVolume", kernelVolume);
    }
}