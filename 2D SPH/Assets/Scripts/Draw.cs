using UnityEngine;

public class Draw : MonoBehaviour
{
    public enum Property { Velocity, Density, Pressure }

    /*
    Inspector properties
    */
    [Header("Shaders")]
    [SerializeField] Shader shader;

    [Header("Appearance Settings")]
    [SerializeField, Range(0, 4)] int sphereResolution = 2;
    [SerializeField] Color fastColour;
    [SerializeField] Color slowColour;
    [SerializeField] float maxSpeed = 10f;
    [SerializeField] float maxDensityFluctuation = 0.1f;
    [SerializeField] float maxPressure = 5000f;
    [SerializeField] Property colourProperty;

    /*
    Private properties
    */
    Material instanceMaterial;
    Bounds bounds;
    Mesh mesh;

    uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
    ComputeBuffer argsBuffer;
    ComputeBuffer initialColourBuffer;

    ShaderHelper computeShader;

    float lowHue;
    float highHue;

    /*
    Public getters
    */
    public Property ColourProperty => colourProperty;

    void Start()
    {
        instanceMaterial = new Material(shader);
        bounds = new Bounds(Vector3.zero, Vector3.one * 1000f);
        mesh = SphereGenerator.GenerateSphere(sphereResolution);
    }

    void OnValidate()
    {
        maxSpeed = Mathf.Max(0, maxSpeed);
        maxDensityFluctuation = Mathf.Clamp01(maxDensityFluctuation);
        maxPressure = Mathf.Max(0, maxPressure);

        Color.RGBToHSV(slowColour, out lowHue, out _, out _);
        Color.RGBToHSV(fastColour, out highHue, out _, out _);

        if (!Application.isPlaying || instanceMaterial == null) return;

        if (computeShader == null)
            computeShader = GetComponent<Simulate>().Shader;

        computeShader.SetValues(new object[]
        {
            "lowHue", lowHue,
            "highHue", highHue,
            "maxSpeed", maxSpeed,
            "maxDensityFluctuation", maxDensityFluctuation,
            "maxPressure", maxPressure
        });

        mesh = SphereGenerator.GenerateSphere(sphereResolution);
    }

    void OnDestroy()
    {
        CleanupBuffers();
    }

    void InitialiseArgsBuffer(int instanceCount)
    {
        if (argsBuffer != null) argsBuffer.Release();

        argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);

        args[0] = mesh.GetIndexCount(0);
        args[1] = (uint)instanceCount;
        args[2] = mesh.GetIndexStart(0);
        args[3] = mesh.GetBaseVertex(0);
        argsBuffer.SetData(args);
    }

    void CleanupBuffers()
    {
        if (argsBuffer != null)
            argsBuffer.Release();
        if (initialColourBuffer != null)
            initialColourBuffer.Release();
    }

    void InitialiseColoursBuffer()
    {
        if (initialColourBuffer != null) initialColourBuffer.Release();
        
        int instanceCount = (int)args[1];
        Color[] colors = new Color[instanceCount];
        for (int i = 0; i < instanceCount; i++)
        {
            colors[i] = Color.white;
        }

        initialColourBuffer = new ComputeBuffer(instanceCount, sizeof(float) * 4);
        initialColourBuffer.SetData(colors);
        BindColours(initialColourBuffer);
    }

    void BindColours(ComputeBuffer colourBuffer)
    {
        instanceMaterial.SetBuffer("colours", colourBuffer);
    }


    public void DrawFrame()
    {
        if (argsBuffer == null) return;

        Graphics.DrawMeshInstancedIndirect(
            mesh,
            0,
            instanceMaterial,
            bounds,
            argsBuffer
        );
    }

    public void UpdateSize(float size)
    {
        instanceMaterial.SetFloat("size", size);
    }

    public void BindPositions(ComputeBuffer positionBuffer)
    {

        InitialiseArgsBuffer(positionBuffer.count);
        InitialiseColoursBuffer();
        instanceMaterial.SetBuffer("positions", positionBuffer);
    }

    public void BindBuffers(ComputeBuffer positionBuffer, ComputeBuffer colourBuffer)
    {
        if (computeShader == null)
            computeShader = GetComponent<Simulate>().Shader;

        BindPositions(positionBuffer);
        BindColours(colourBuffer);
    }
}
