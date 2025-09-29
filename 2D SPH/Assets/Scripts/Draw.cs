using UnityEditor;
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

    /*
    Private properties
    */
    Simulate sim;
    Spawn spawner;
    Material instanceMaterial;
    Bounds bounds;
    ComputeBuffer positionsBuffer;

    uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
    ComputeBuffer argsBuffer;

    void Start()
    {
        spawner = GetComponentInParent<Spawn>();
        instanceMaterial = new Material(shader);
        bounds = new Bounds(Vector2.zero, Vector2.one * 1000f);
        InitialiseArgsBuffer();
        UpdatePositions();
    }

    void Update()
    {
        instanceMaterial.SetFloat("size", spawner.Size);

        Graphics.DrawMeshInstancedIndirect(
            mesh,
            0,
            instanceMaterial,
            bounds,
            argsBuffer
        );
    }

    void CleanupPositionBuffer()
    {
        if (positionsBuffer == null) return;

        positionsBuffer.Release();
        positionsBuffer = null;
    }

    public void UpdatePositions()
    {
        if (spawner == null) return;

        // IF sim started, get from sim, else spawneriew

        InitialiseArgsBuffer();

        CleanupPositionBuffer();
        positionsBuffer = new ComputeBuffer(spawner.InstanceCount, sizeof(float) * 2);
        positionsBuffer.SetData(spawner.Positions);

        instanceMaterial.SetBuffer("positions", positionsBuffer);
    }

    void InitialiseArgsBuffer()
    {
        CleanupArgsBuffer();

        argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);

        args[0] = (uint)mesh.GetIndexCount(0);
        args[1] = (uint)spawner.InstanceCount;
        args[2] = (uint)mesh.GetIndexStart(0);
        args[3] = (uint)mesh.GetBaseVertex(0);
        argsBuffer.SetData(args);
    }

    void CleanupArgsBuffer()
    {
        if (argsBuffer == null) return;

        argsBuffer.Release();
        argsBuffer = null;
    }

    void OnDestroy()
    {
        CleanupArgsBuffer();
        CleanupPositionBuffer();
    }

}
