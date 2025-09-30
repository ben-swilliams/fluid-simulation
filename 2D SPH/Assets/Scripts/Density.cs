using UnityEngine;

class Density : MonoBehaviour
{
    /*
    Inspector settings
    */
    [SerializeField] ComputeShader densityShader;
    [SerializeField] float smoothingRadius = 0.1f;
    [SerializeField] Color color;

    /*
    Private properties
    */
    // Density fluid texture size
    const int TEX_WIDTH = 1024;
    const int TEX_HEIGHT = 1024;

    // Kernel constants
    float smoothingRadiusSq;
    float kernelConstant;
    float kernelVolume;
    int kernel;

    /*
    Public getters
    */
    public RenderTexture densityField { get; private set; }

    void Start()
    {
        kernel = densityShader.FindKernel("DensityField");
        densityField = new RenderTexture(TEX_WIDTH, TEX_HEIGHT, 0, RenderTextureFormat.ARGBFloat);
        densityField.enableRandomWrite = true;
        densityField.Create();

        densityShader.SetTexture(kernel, "Field", densityField);
        densityShader.SetInts("fieldSize", new int[] { TEX_WIDTH, TEX_HEIGHT });
        UpdateConstants();
        UpdateBoundary();
    }

    void Update()
    {
        densityShader.Dispatch(kernel, TEX_WIDTH / 8, TEX_HEIGHT / 8, 1);
    }

    void OnValidate()
    {
        smoothingRadius = Mathf.Max(0, smoothingRadius);
        UpdateConstants();
    }

    void UpdateConstants()
    {
        smoothingRadiusSq = smoothingRadius * smoothingRadius;
        kernelConstant = 315 / (64 * Mathf.PI * Mathf.Pow(smoothingRadiusSq, 4.5f));
        kernelVolume = kernelConstant * Mathf.PI * Mathf.Pow(smoothingRadiusSq, 4) * 0.25f;

        densityShader.SetFloat("smoothingRadiusSq", smoothingRadiusSq);
        densityShader.SetFloat("kernelConstant", kernelConstant);
        densityShader.SetFloat("kernelVolume", kernelVolume);
        densityShader.SetVector("color", color);
    }

    public void UpdateBoundary()
    {
        Vector2 bounds = GetComponentInChildren<Container>().Boundary;
        Vector2 worldMin = -0.5f * bounds;
        Vector2 worldMax = 0.5f * bounds;

        densityShader.SetVector("worldMin", worldMin);
        densityShader.SetVector("worldMax", worldMax);
    }

    public void BindBuffer(ComputeBuffer positionBuffer)
    {
        densityShader.SetBuffer(kernel, "Positions", positionBuffer);
        densityShader.SetInt("instanceCount", positionBuffer.count);
    }

}