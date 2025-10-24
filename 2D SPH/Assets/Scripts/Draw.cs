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
    [SerializeField] float maxProp = 10f;
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
        if (argsBuffer == null) return;

        Graphics.DrawMeshInstancedIndirect(
            mesh,
            0,
            instanceMaterial,
            bounds,
            argsBuffer
        );
    }

    void OnValidate()
    {
        maxProp = Mathf.Max(0, maxProp);

        Color.RGBToHSV(slowColour, out lowHue, out _, out _);
        Color.RGBToHSV(fastColour, out highHue, out _, out _);

        if (!Application.isPlaying || instanceMaterial == null) return;

        instanceMaterial.SetFloat("maxProp", maxProp);
        instanceMaterial.SetFloat("lowHue", lowHue);
        instanceMaterial.SetFloat("highHue", highHue);
        instanceMaterial.SetInteger("useVelocities", colourProperty == Property.Velocity ? 1 : 0);

        if (colourProperty != Property.Velocity)
        {
            ShaderHelper shader = GetComponent<Simulate>().Shader;
            ComputeBuffer propertyBuffer = colourProperty == Property.Density ? shader.Densities : shader.Pressures;
            instanceMaterial.SetBuffer("properties", propertyBuffer);
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

    public void BindBuffers(ComputeBuffer positionBuffer, ComputeBuffer velocityBuffer, ComputeBuffer densityBuffer, ComputeBuffer pressureBuffer, float size)
    {
        InitialiseArgsBuffer(positionBuffer.count);
        instanceMaterial.SetFloat("size", size);
        instanceMaterial.SetFloat("lowHue", lowHue);
        instanceMaterial.SetFloat("highHue", highHue);
        instanceMaterial.SetFloat("maxProp", maxProp);
        instanceMaterial.SetInteger("useVelocities", colourProperty == Property.Velocity ? 1 : 0);
        instanceMaterial.SetBuffer("positions", positionBuffer);
        instanceMaterial.SetBuffer("velocities", velocityBuffer);

        // Bind the correct property buffer based on colourProperty
        ComputeBuffer propertyBuffer = colourProperty == Property.Density ? densityBuffer : pressureBuffer;
        instanceMaterial.SetBuffer("properties", propertyBuffer);
    }
}
