using UnityEngine;

class DensityField : MonoBehaviour
{
    /*
    Inspector settings
    */
    [Header("Shaders")]
    [SerializeField] ComputeShader densityShader;
    [SerializeField] Color color;
    [SerializeField] Vector2 resolution = new Vector2(512, 512);

    /*
    Private properties
    */
    float smoothingRadius;
    int kernel;
    bool isEnabled = false;

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
        if (isEnabled) densityShader.Dispatch(kernel, (int)resolution.x / 8, (int)resolution.y / 8, 1);

        if (UnityEngine.InputSystem.Keyboard.current.dKey.wasPressedThisFrame)
        {
            isEnabled = !isEnabled;
            if (!isEnabled) ClearTexture();
        }
    }

    void OnValidate()
    {
        resolution.x = Mathf.Max(8, Mathf.RoundToInt(resolution.x / 8) * 8);
        resolution.y = Mathf.Max(8, Mathf.RoundToInt(resolution.y / 8) * 8);
        
        if (!isEnabled) ClearTexture();
        
        if (Application.isPlaying)
        {
            InitialiseShader();
            UpdateConstants();
            GetComponent<Container>().ApplyTexture();
        }
    }

    void InitialiseShader()
    {
        kernel = densityShader.FindKernel("DensityField");
        
        if (densityField != null) densityField.Release();
        
        densityField = new RenderTexture((int)resolution.x, (int)resolution.y, 0, RenderTextureFormat.ARGBFloat);
        densityField.enableRandomWrite = true;
        densityField.Create();

        densityShader.SetTexture(kernel, "Field", densityField);
    }

    void UpdateConstants()
    {
        float kernelConstant = 315 / (64 * Mathf.PI * Mathf.Pow(smoothingRadius, 9f));

        densityShader.SetInts("fieldSize", new int[] { (int)resolution.x, (int)resolution.y });
        densityShader.SetFloat("smoothingRadius", smoothingRadius);
        densityShader.SetFloat("kernelConstant", kernelConstant);
        densityShader.SetVector("color", color);
    }

    void ClearTexture()
    {
        if (densityField == null) return;
        
        RenderTexture prev = RenderTexture.active;

        RenderTexture.active = densityField;
        GL.Clear(true, true, Color.clear);

        RenderTexture.active = prev;
    }

    public void UpdateSmoothingRadius()
    {
        smoothingRadius = GetComponentInParent<Simulate>().SmoothingRadius;
        UpdateConstants();
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
        densityShader.SetBuffer(kernel, "PredictedPositions", positionBuffer);
        densityShader.SetInt("instanceCount", positionBuffer.count);
    }
}