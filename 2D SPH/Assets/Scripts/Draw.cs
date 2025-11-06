using UnityEngine;

public class Draw : MonoBehaviour
{
    private enum Property { Velocity, Density, Pressure }

    /*
    Inspector properties
    */
    [Header("Shaders")]
    [SerializeField] Shader shader;

    [Header("Appearance Settings")]
    [SerializeField] Mesh mesh;
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

    uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
    ComputeBuffer argsBuffer;

    ShaderHelper computeShader;

    float lowHue;
    float highHue;

    void Start()
    {
        instanceMaterial = new Material(shader);
        bounds = new Bounds(Vector3.zero, Vector3.one * 1000f);
    }

    void OnValidate()
    {
        maxSpeed = Mathf.Max(0, maxSpeed);
        maxDensityFluctuation = Mathf.Clamp01(maxDensityFluctuation);
        maxPressure = Mathf.Max(0, maxPressure);

        Color.RGBToHSV(slowColour, out lowHue, out _, out _);
        Color.RGBToHSV(fastColour, out highHue, out _, out _);

        if (!Application.isPlaying || instanceMaterial == null) return;

        computeShader.SetValues(new object[]
        {
            "lowHue", lowHue,
            "highHue", highHue,
            "maxSpeed", maxSpeed,
            "maxDensityFluctuation", maxDensityFluctuation,
            "maxPressure", maxPressure
        });
    }

    void OnDestroy()
    {
        CleanupArgsBuffer();
    }

    void InitialiseArgsBuffer(int instanceCount)
    {
        CleanupArgsBuffer();

        argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);

        args[0] = mesh.GetIndexCount(0);
        args[1] = (uint)instanceCount;
        args[2] = mesh.GetIndexStart(0);
        args[3] = mesh.GetBaseVertex(0);
        argsBuffer.SetData(args);
    }

    void CleanupArgsBuffer()
    {
        if (argsBuffer == null) return;

        argsBuffer.Release();
        argsBuffer = null;
    }

    Vector3 ColourTargets()
    {
        return new Vector3(colourProperty == Property.Velocity ? 1 : 0,
                           colourProperty == Property.Density ? 1 : 0,
                           colourProperty == Property.Pressure ? 1 : 0);
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
