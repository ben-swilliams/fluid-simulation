using UnityEngine;

public class PRenderer : MonoBehaviour
{
    [Header("Shaders")]
    [SerializeField] Shader shader;

    [Header("Appearance Settings")]
    [SerializeField] Mesh mesh;

    Simulation sim;
    Material instanceMaterial;
    Bounds bounds;

    uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
    ComputeBuffer argsBuffer;

    void Start()
    {
        sim = GetComponentInParent<Simulation>();
        instanceMaterial = new Material(shader);
        bounds = new Bounds(Vector2.zero, Vector2.one * 1000f);
        InitialiseArgsBuffer();
        InitialiseVariables();
    }

    void Update()
    {
        Graphics.DrawMeshInstancedIndirect(
            mesh,
            0,
            instanceMaterial,
            bounds,
            argsBuffer
        );
    }

    void InitialiseArgsBuffer()
    {
        argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);

        args[0] = (uint)mesh.GetIndexCount(0);
        args[1] = (uint)sim.InstanceCount;
        args[2] = (uint)mesh.GetIndexStart(0);
        args[3] = (uint)mesh.GetBaseVertex(0);
        argsBuffer.SetData(args);
    }

    void InitialiseVariables()
    {
        instanceMaterial.SetFloat("size", sim.Size);
        instanceMaterial.SetBuffer("Positions", sim.positionBuffer);
    }

    void OnDestroy()
    {
        if (argsBuffer != null)
        {
            argsBuffer.Release();
            argsBuffer = null;
        }
    }

}
