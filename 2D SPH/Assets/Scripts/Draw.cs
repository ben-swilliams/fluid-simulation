using System;
using UnityEngine;

public class Draw : MonoBehaviour
{
    public enum Property { Velocity, Density, Pressure }

    /*
    Inspector properties
    */
    [Header("Shaders")]
    [SerializeField] ComputeShader cubesCompute;
    [SerializeField] ComputeShader renderArgsCompute;
    [SerializeField] Shader particleShader;
    [SerializeField] Shader cubesShader;

    [Header("Appearance Settings")]
    [SerializeField, Range(0, 4)] int sphereResolution = 2;
    [SerializeField] Color fastColour;
    [SerializeField] Color slowColour;
    [SerializeField] Color fluidColour;
    [SerializeField] float maxSpeed = 10f;
    [SerializeField] float maxDensityFluctuation = 0.1f;
    [SerializeField] float maxPressure = 5000f;
    [SerializeField] Property colourProperty;
    [SerializeField, InspectorName("Billboard?")] bool billboard = false;
    
    [Header("Marching cubes")]
    [SerializeField] float isoLevel = 5;
    [SerializeField] bool useMarchingCubes = false;

    /*
    Private properties
    */
    Material particleMaterial;
    Material cubesMaterial;
    Bounds bounds;
    Mesh mesh;

    uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
    ComputeBuffer particleArgsBuffer;
    ComputeBuffer cubesArgsBuffer;
    ComputeBuffer initialColourBuffer;

    float lowHue;
    float highHue;
    ShaderHelper simShader;

    MarchingCubes marchingCubes;

    /*
    Public getters
    */
    public Property ColourProperty => colourProperty;

    public float MaxSpeed
    {
        get => maxSpeed;
        set
        {
            maxSpeed = value;
            SetColourValues();
        }
    }

    public float MaxDensityFluctuation
    {
        get => maxDensityFluctuation;
        set
        {
            maxDensityFluctuation = value;
            SetColourValues();
        }
    }

    public float MaxPressure
    {
        get => maxPressure;
        set
        {
            maxPressure = value;
            SetColourValues();
        }
    }

    public void SetProperty(int index)
    {
        colourProperty = (Property)index;
        SetColourValues();
    }

    void Start()
    {
        particleMaterial = new Material(particleShader);
        particleMaterial.enableInstancing = true;
        particleMaterial.SetInt("billboard", billboard ? 1 : 0);

        bounds = new Bounds(Vector3.zero, Vector3.one * 1000f);

        if (!billboard) mesh = MeshGenerator.GenerateSphere(sphereResolution);
        else mesh = MeshGenerator.GenerateQuad();

        Color.RGBToHSV(slowColour, out lowHue, out _, out _);
        Color.RGBToHSV(fastColour, out highHue, out _, out _);

        marchingCubes = new MarchingCubes(cubesCompute);
    }

    void OnValidate()
    {
        maxSpeed = Mathf.Max(0, maxSpeed);
        maxDensityFluctuation = Mathf.Clamp01(maxDensityFluctuation);
        maxPressure = Mathf.Max(0, maxPressure);

        Color.RGBToHSV(slowColour, out lowHue, out _, out _);
        Color.RGBToHSV(fastColour, out highHue, out _, out _);

        if (!Application.isPlaying || particleMaterial == null) return;

        particleMaterial.SetInt("billboard", billboard ? 1 : 0);

        SetColourValues();

        if (!billboard) mesh = MeshGenerator.GenerateSphere(sphereResolution);
        else mesh = MeshGenerator.GenerateQuad();
    }

    void OnDestroy()
    {
        CleanupBuffers();
    }

    void InitialiseArgsBuffer(int instanceCount)
    {
        if (particleArgsBuffer != null) particleArgsBuffer.Release();

        particleArgsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);

        args[0] = mesh.GetIndexCount(0);
        args[1] = (uint)instanceCount;
        args[2] = mesh.GetIndexStart(0);
        args[3] = mesh.GetBaseVertex(0);
        particleArgsBuffer.SetData(args);
    }

    void CleanupBuffers()
    {
        if (particleArgsBuffer != null)
            particleArgsBuffer.Release();
        if (initialColourBuffer != null)
            initialColourBuffer.Release();

        marchingCubes.CleanupBuffers();
    }

    void InitialiseColoursBuffer()
    {
        if (initialColourBuffer != null) initialColourBuffer.Release();
        
        int instanceCount = (int)args[1];
        Vector3[] colours = new Vector3[instanceCount];
        for (int i = 0; i < instanceCount; i++)
        {
            colours[i] = new Vector3(Color.white.r, Color.white.g, Color.white.b);
        }

        initialColourBuffer = new ComputeBuffer(instanceCount, sizeof(float) * 3);
        initialColourBuffer.SetData(colours);
        BindColours(initialColourBuffer);
        SetColourValues();
    }

    void BindColours(ComputeBuffer colourBuffer)
    {
        particleMaterial.SetBuffer("colours", colourBuffer);
    }

    void SetColourValues()
    {
        simShader.SetValues(new object[]
        {
            "lowHue", lowHue,
            "highHue", highHue,
            "maxSpeed", maxSpeed,
            "maxDensityFluctuation", maxDensityFluctuation,
            "maxPressure", maxPressure
        });
    }

    void DrawMesh(ComputeBuffer triangles)
    {
        // We need to dispatch a compute to set render args as it changes on each draw
        // and we don't wanna force a read-back to the CPU
        if (!cubesMaterial) cubesMaterial = new Material(cubesShader);

        cubesMaterial.SetBuffer("VertexBuffer", triangles);
        cubesMaterial.SetColor("col", fluidColour);
        
        if (cubesArgsBuffer == null)
        {
            cubesArgsBuffer = new ComputeBuffer(5, sizeof(uint), ComputeBufferType.IndirectArguments);
            renderArgsCompute.SetBuffer(0, "RenderArgs", cubesArgsBuffer);
        }

        ComputeBuffer.CopyCount(triangles, cubesArgsBuffer, 0);
        renderArgsCompute.Dispatch(0, 1, 1, 1);
        
        Graphics.DrawProceduralIndirect(cubesMaterial, bounds, MeshTopology.Triangles, cubesArgsBuffer);
    }

    public void SetComputeShader(ShaderHelper shader)
    {
        simShader = shader;
    }

    public void DrawFrame(RenderTexture densityTex)
    {
        if (particleArgsBuffer == null) return;

        if (useMarchingCubes)
        {
            ComputeBuffer triangles = marchingCubes.Run(densityTex, isoLevel);
            DrawMesh(triangles);
        }
        else {
                Graphics.DrawMeshInstancedIndirect(
                mesh,
                0,
                particleMaterial,
                bounds,
                particleArgsBuffer
            );
        }
    }

    public void UpdateSize(float size)
    {
        particleMaterial.SetFloat("size", size);
    }

    public void UpdateContainerSize(Vector3 containerSize)
    {
        marchingCubes.UpdateContainerSize(containerSize);
    }

    public void BindPositions(ComputeBuffer positionBuffer)
    {
        InitialiseArgsBuffer(positionBuffer.count);
        InitialiseColoursBuffer();
        particleMaterial.SetBuffer("positions", positionBuffer);
    }

    public void BindBuffers(ComputeBuffer positionBuffer, ComputeBuffer colourBuffer)
    {
        BindPositions(positionBuffer);
        BindColours(colourBuffer);
    }
}
