using UnityEngine;

class DensityField : MonoBehaviour
{
    /*
    Inspector settings
    */
    [SerializeField] ComputeShader densityShader;
    [SerializeField] Color color;
    [SerializeField] bool isEnabled = true;

    /*
    Private properties
    */
    const int TEX_WIDTH = 1024;
    const int TEX_HEIGHT = 1024;

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
        InitialiseShader();
        UpdateConstants();
        UpdateBoundary();
    }

    void Update()
    {
        if (isEnabled) densityShader.Dispatch(kernel, TEX_WIDTH / 8, TEX_HEIGHT / 8, 1);

        if (UnityEngine.InputSystem.Keyboard.current.dKey.wasPressedThisFrame)
        {
            isEnabled = !isEnabled;
            if (!isEnabled) ClearTexture();
        }
    }

    void OnValidate()
    {
        if (!isEnabled) ClearTexture();
        UpdateConstants();
    }

    void InitialiseShader()
    {
        kernel = densityShader.FindKernel("DensityField");
        densityField = new RenderTexture(TEX_WIDTH, TEX_HEIGHT, 0, RenderTextureFormat.ARGBFloat);
        densityField.enableRandomWrite = true;
        densityField.Create();

        densityShader.SetTexture(kernel, "Field", densityField);
        densityShader.SetInts("fieldSize", new int[] { TEX_WIDTH, TEX_HEIGHT });
    }

    void UpdateConstants()
    {
        UpdateSmoothingRadius();
        kernelConstant = 315 / (64 * Mathf.PI * Mathf.Pow(smoothingRadiusSq, 4.5f));
        kernelVolume = kernelConstant * Mathf.PI * Mathf.Pow(smoothingRadiusSq, 4) * 0.25f;

        densityShader.SetFloat("smoothingRadiusSq", smoothingRadiusSq);
        densityShader.SetFloat("kernelConstant", kernelConstant);
        densityShader.SetFloat("kernelVolume", kernelVolume);
        densityShader.SetVector("color", color);
    }

    void ClearTexture()
    {
        RenderTexture prev = RenderTexture.active;

        RenderTexture.active = densityField;
        GL.Clear(true, true, Color.clear);

        RenderTexture.active = prev;
    }

    public void UpdateSmoothingRadius()
    {
        float smoothingRadius = GetComponentInParent<Simulate>().SmoothingRadius;
        smoothingRadiusSq = smoothingRadius * smoothingRadius;
    }

    public void UpdateBoundary()
    {
        Vector2 bounds = GetComponent<Container>().Boundary;
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