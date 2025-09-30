using UnityEngine;
using UnityEngine.UIElements;

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

    public void BindBuffer(ComputeBuffer positionBuffer, float size)
    {
        InitialiseArgsBuffer(positionBuffer.count);
        instanceMaterial.SetFloat("size", size);
        instanceMaterial.SetBuffer("positions", positionBuffer);
    }

    void InitialiseArgsBuffer(int instanceCount)
    {
        CleanupArgsBuffer();

        argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);

        args[0] = (uint)mesh.GetIndexCount(0);
        args[1] = (uint)instanceCount;
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
    }

}
