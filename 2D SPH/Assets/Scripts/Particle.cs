using UnityEditor.ShaderGraph.Internal;
using UnityEngine;

public class GPUInstancing2D : MonoBehaviour
{
    [Header("Instancing Settings")]
    [SerializeField] Shader shader;
    [SerializeField] Mesh mesh;
    [SerializeField] int instanceCount = 1000;
    [SerializeField] Vector2 spawnArea = new Vector2(10f, 10f);
    [SerializeField] float size = 1;


    Material instanceMaterial;
    ComputeBuffer positionBuffer;
    ComputeBuffer argsBuffer;
    uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
    
    void Start()
    {
        instanceMaterial = new Material(shader); 
        
        GenerateInstanceData();
        
        SetupBuffers();
    }

    Vector2[] GenerateInstanceData()
    {
        Vector2[] positions = new Vector2[instanceCount];

        for (int i = 0; i < instanceCount; i++)
        {
            Vector2 position = new Vector2(
                UnityEngine.Random.Range(-spawnArea.x, spawnArea.x),
                UnityEngine.Random.Range(-spawnArea.y, spawnArea.y)
            );

            positions[i] = position;
        }

        return positions;
    }
    
    void SetupBuffers()
    {
        Vector2[] positions = GenerateInstanceData();
        positionBuffer = new ComputeBuffer(instanceCount, sizeof(float) * 2);
        positionBuffer.SetData(positions);
        
        argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        
        args[0] = (uint)mesh.GetIndexCount(0);
        args[1] = (uint)instanceCount;
        args[2] = (uint)mesh.GetIndexStart(0);
        args[3] = (uint)mesh.GetBaseVertex(0);
        argsBuffer.SetData(args);
        
        instanceMaterial.SetBuffer("Positions", positionBuffer);
        instanceMaterial.SetFloat("size", size);
    }
    
    void Update()
    {
        Graphics.DrawMeshInstancedIndirect(
            mesh, 
            0,
            instanceMaterial, 
            new Bounds(Vector3.zero, Vector3.one * 1000f), 
            argsBuffer
        );
    }
    
    void OnDestroy()
    {
        if (positionBuffer != null)
            positionBuffer.Release();
        if (argsBuffer != null)
            argsBuffer.Release();
    }
}