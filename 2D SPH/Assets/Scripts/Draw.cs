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
        sim = GetComponentInParent<Simulate>();
        spawner = GetComponentInParent<Spawn>();
        instanceMaterial = new Material(shader);
        bounds = new Bounds(Vector2.zero, Vector2.one * 1000f);
        InitialiseArgsBuffer();
        UpdatePositions();
    }

    void Update()
    {
        instanceMaterial.SetFloat("size", spawner.Size);
        if (sim.Started) UpdatePositions();

        Graphics.DrawMeshInstancedIndirect(
            mesh,
            0,
            instanceMaterial,
            bounds,
            argsBuffer
        );

        if (UnityEngine.InputSystem.Keyboard.current.spaceKey.wasPressedThisFrame && !sim.Started)
        {
            CleanupPositionBuffer();
        }
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

        InitialiseArgsBuffer();

        positionsBuffer = sim.Started ? sim.positionBuffer : CreateSpawnBuffer();

        instanceMaterial.SetBuffer("positions", positionsBuffer);
    }

    ComputeBuffer CreateSpawnBuffer()
    {
        CleanupPositionBuffer();
        positionsBuffer = new ComputeBuffer(spawner.InstanceCount, sizeof(float) * 2);
        positionsBuffer.SetData(spawner.Positions);

        return positionsBuffer;
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
