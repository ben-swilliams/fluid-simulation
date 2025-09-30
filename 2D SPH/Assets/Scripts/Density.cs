using UnityEngine;

class Density : MonoBehaviour
{
    [SerializeField] ComputeShader densityShader;
    [SerializeField] float smoothingRadius = 0.1f;

    float smoothingRadiusSq;
    float kernelConstant;
    float kernelVolume;

    int kernel;

    RenderTexture densityField;

    void Start()
    {
        kernel = densityShader.FindKernel("DensityField");
    }

    void Update()
    {
        UpdateConstants();
    }

    void OnValidate()
    {
        smoothingRadius = Mathf.Max(0, smoothingRadius);
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