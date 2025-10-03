using UnityEngine;

public class Draw : MonoBehaviour
{
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

    /*
    Private properties
    */
    Material instanceMaterial;
    Bounds bounds;

    uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
    ComputeBuffer argsBuffer;

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
        maxSpeed = Mathf.Max(0, maxSpeed);
        if (!Application.isPlaying || instanceMaterial == null) return;

        instanceMaterial.SetFloat("maxSpeed", maxSpeed);
        instanceMaterial.SetVector("slowColour", slowColour);
        instanceMaterial.SetVector("fastColour", fastColour);
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

    public void BindBuffers(ComputeBuffer positionBuffer, ComputeBuffer velocityBuffer, float size)
    {
        InitialiseArgsBuffer(positionBuffer.count);
        instanceMaterial.SetFloat("size", size);
        instanceMaterial.SetFloat("maxSpeed", maxSpeed);
        instanceMaterial.SetBuffer("positions", positionBuffer);
        instanceMaterial.SetBuffer("velocities", velocityBuffer);
    }
}
