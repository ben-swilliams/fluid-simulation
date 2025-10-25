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
    [SerializeField] float maxVelocity = 10f;
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

    float lowHue;
    float highHue;

    void Start()
    {
        instanceMaterial = new Material(shader);
        bounds = new Bounds(Vector2.zero, Vector2.one * 1000f);
    }

    void Update()
    {
    }

    void OnValidate()
    {
        maxVelocity = Mathf.Max(0, maxVelocity);
        maxDensityFluctuation = Mathf.Clamp01(maxDensityFluctuation);
        maxPressure = Mathf.Max(0, maxPressure);

        Color.RGBToHSV(slowColour, out lowHue, out _, out _);
        Color.RGBToHSV(fastColour, out highHue, out _, out _);

        if (!Application.isPlaying || instanceMaterial == null) return;

        instanceMaterial.SetFloat("lowHue", lowHue);
        instanceMaterial.SetFloat("highHue", highHue);
        instanceMaterial.SetVector("colourTarget", ColourTargets());
        instanceMaterial.SetVector("propMaxes", new Vector3(maxVelocity, maxDensityFluctuation, maxPressure));
        instanceMaterial.SetInteger("useVelocities", colourProperty == Property.Velocity ? 1 : 0);

        if (colourProperty != Property.Velocity)
        {
            ShaderHelper shader = GetComponent<Simulate>().Shader;
            ComputeBuffer propertyBuffer = colourProperty == Property.Density ? shader.Densities : shader.Pressures;
            if (propertyBuffer != null) instanceMaterial.SetBuffer("properties", propertyBuffer);
        }
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

    public void UpdateVariables(float size, float restDensity)
    {
        if (size != -1) instanceMaterial.SetFloat("size", size);
        if (restDensity != -1) instanceMaterial.SetFloat("restDensity", restDensity);
    }

    public void BindBuffers(ComputeBuffer positionBuffer, ComputeBuffer velocityBuffer, ComputeBuffer densityBuffer, ComputeBuffer pressureBuffer)
    {
        InitialiseArgsBuffer(positionBuffer.count);
        instanceMaterial.SetFloat("lowHue", lowHue);
        instanceMaterial.SetFloat("highHue", highHue);
        instanceMaterial.SetVector("colourTarget", ColourTargets());
        instanceMaterial.SetVector("propMaxes", new Vector3(maxVelocity, maxDensityFluctuation, maxPressure));
        instanceMaterial.SetInteger("useVelocities", colourProperty == Property.Velocity ? 1 : 0);
        instanceMaterial.SetBuffer("positions", positionBuffer);
        instanceMaterial.SetBuffer("velocities", velocityBuffer);

        // Bind the correct property buffer based on colourProperty
        ComputeBuffer propertyBuffer = colourProperty == Property.Density ? densityBuffer : pressureBuffer;
        instanceMaterial.SetBuffer("properties", propertyBuffer);
    }
}
