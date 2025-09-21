using System;
using UnityEngine;
using UnityEngine.Rendering.Universal.Internal;

public class Particle : MonoBehaviour
{
    [Header("Shaders")]
    [SerializeField] Shader shader;
    [SerializeField] ComputeShader computeShader;

    [Header("Instancing Settings")]
    [SerializeField] Mesh mesh;
    [SerializeField] int instanceCount = 1000;
    [SerializeField] Vector2 spawnArea = new Vector2(10f, 10f);
    [SerializeField] float size = 1;

    [Header("Simulation Settings")]
    [SerializeField] Container container;
    [SerializeField] Vector2 gravity = new Vector2(0, -9.8f);
    [SerializeField] float initSpeed = 5f;
    [SerializeField] float dampingFactor = 0.9f;

    static int threadGroupSize = 64;
    Material instanceMaterial;
    ComputeBuffer positionBuffer;
    ComputeBuffer velocityBuffer;
    ComputeBuffer argsBuffer;
    Bounds bounds = new Bounds(Vector3.zero, Vector3.one * 1000f);
    uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
    int kernel;
    
    void Start()
    {
        instanceMaterial = new Material(shader);
        
        SetupBuffers();
        InitialiseVariables();
    }

    Vector2[] GeneratePositionData()
    {
        Vector2[] positions = new Vector2[instanceCount];

        for (int i = 0; i < instanceCount; i++)
        {
            Vector2 position = new Vector2(
                UnityEngine.Random.Range(-spawnArea.x/2 + size/2, spawnArea.x/2 - size/2),
                UnityEngine.Random.Range(-spawnArea.y/2 + size/2, spawnArea.y/2 - size/2)
            );

            positions[i] = position;
        }

        return positions;
    }

    Vector2[] GenerateVelocityData()
    {
        Vector2[] velocities = new Vector2[instanceCount];

        for (int i = 0; i < instanceCount; i++)
        {
            float random = UnityEngine.Random.Range(0f, 2 * Mathf.PI);
            Vector2 vel = new Vector2(Mathf.Cos(random), Mathf.Sin(random)) * initSpeed;
            velocities[i] = vel;
        }

        return velocities;
    }

    void SetupBuffers()
    {
        Vector2[] positions = GeneratePositionData();
        positionBuffer = new ComputeBuffer(instanceCount, sizeof(float) * 2);
        positionBuffer.SetData(positions);

        Vector2[] velocities = GenerateVelocityData();
        velocityBuffer = new ComputeBuffer(instanceCount, sizeof(float) * 2);
        velocityBuffer.SetData(velocities);

        InitialiseArgsBuffer();
    }

    void InitialiseArgsBuffer()
    {
        argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);

        args[0] = (uint)mesh.GetIndexCount(0);
        args[1] = (uint)instanceCount;
        args[2] = (uint)mesh.GetIndexStart(0);
        args[3] = (uint)mesh.GetBaseVertex(0);
        argsBuffer.SetData(args);
    }

    void InitialiseVariables()
    {
        instanceMaterial.SetFloat("size", size);
        instanceMaterial.SetBuffer("Positions", positionBuffer);

        kernel = computeShader.FindKernel("Gravity");

        computeShader.SetFloat("size", size);
        computeShader.SetFloat("dampingFactor", dampingFactor);
        computeShader.SetVector("containerSize", container.size);
        computeShader.SetVector("gravity", gravity);
        computeShader.SetInt("instanceCount", instanceCount);
        computeShader.SetInt("threadGroupSize", threadGroupSize);

        computeShader.SetBuffer(kernel, "Positions", positionBuffer);
        computeShader.SetBuffer(kernel, "Velocities", velocityBuffer);
    }
    
    void Update()
    {
        computeShader.SetFloat("deltaTime", Time.deltaTime);

        int threadGroups = Mathf.CeilToInt(instanceCount / (float)threadGroupSize);
        computeShader.Dispatch(kernel, threadGroups, 1, 1);

        Graphics.DrawMeshInstancedIndirect(
            mesh, 
            0,
            instanceMaterial, 
            bounds,
            argsBuffer
        );
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.white;

        Gizmos.DrawWireCube(transform.position, new Vector3(spawnArea.x, spawnArea.y, 0f));
    }
    
    void OnDestroy()
    {
        if (positionBuffer != null)
            positionBuffer.Release();
        if (velocityBuffer != null)
            velocityBuffer.Release();
        if (argsBuffer != null)
                argsBuffer.Release();
    }
}