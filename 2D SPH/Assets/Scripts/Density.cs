using UnityEngine;

class Density : MonoBehaviour
{
    [SerializeField] ComputeShader densityShader;
    [SerializeField] GameObject simObject;
    [SerializeField] float smoothingRadius = 0.1f;

    float smoothingRadiusSq;
    float kernelConstant;
    float kernelVolume;

    int kernel;

    void Start()
    {
        kernel = densityShader.FindKernel("DensityField");
    }

    void Update()
    {
        UpdateConstants();
    }

    public void BindBuffer(ComputeBuffer positionBuffer)
    {
        densityShader.SetBuffer(kernel, "Positions", positionBuffer); 
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