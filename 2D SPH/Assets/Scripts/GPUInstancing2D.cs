using UnityEngine;

public class GPUInstancing2D : MonoBehaviour
{
    [Header("Instancing Settings")]
    public Material instanceMaterial;
    public Mesh quadMesh;
    public int instanceCount = 1000;
    public Vector2 spawnArea = new Vector2(10f, 10f);
    
    private ComputeBuffer positionBuffer;
    private ComputeBuffer argsBuffer;
    private uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
    private Matrix4x4[] matrices;
    
    void Start()
    {
        if (quadMesh == null)
        {
            quadMesh = CreateQuadMesh();
        }
        
        GenerateInstanceData();
        
        SetupBuffers();
    }
    
    void GenerateInstanceData()
    {
        matrices = new Matrix4x4[instanceCount];
        
        for (int i = 0; i < instanceCount; i++)
        {
            Vector3 position = new Vector3(
                UnityEngine.Random.Range(-spawnArea.x, spawnArea.x),
                UnityEngine.Random.Range(-spawnArea.y, spawnArea.y),
                0f
            );
            
            matrices[i] = Matrix4x4.TRS(position, Quaternion.identity, Vector3.one);
        }
    }
    
    void SetupBuffers()
    {
        positionBuffer = new ComputeBuffer(instanceCount, sizeof(float) * 16);
        positionBuffer.SetData(matrices);
        
        argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        
        args[0] = (uint)quadMesh.GetIndexCount(0);
        args[1] = (uint)instanceCount;
        args[2] = (uint)quadMesh.GetIndexStart(0);
        args[3] = (uint)quadMesh.GetBaseVertex(0);
        argsBuffer.SetData(args);
        
        instanceMaterial.SetBuffer("_PositionBuffer", positionBuffer);
    }
    
    void Update()
    {
        Graphics.DrawMeshInstancedIndirect(
            quadMesh, 
            0, 
            instanceMaterial, 
            new Bounds(Vector3.zero, Vector3.one * 1000f), 
            argsBuffer
        );
    }
    
    Mesh CreateQuadMesh()
    {
        Mesh mesh = new Mesh();
        
        Vector3[] vertices = new Vector3[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3(0.5f, -0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f)
        };
        
        Vector2[] uv = new Vector2[]
        {
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(1, 1),
            new Vector2(0, 1)
        };
        
        int[] triangles = new int[]
        {
            0, 1, 2,
            0, 2, 3
        };
        
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        
        return mesh;
    }
    
    void OnDestroy()
    {
        if (positionBuffer != null)
            positionBuffer.Release();
        if (argsBuffer != null)
            argsBuffer.Release();
    }
}