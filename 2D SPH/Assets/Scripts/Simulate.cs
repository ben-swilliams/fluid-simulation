using UnityEngine;

public class Simulate : MonoBehaviour
{

    /*
    Inspector properties
    */
    [Header("Shaders")]
    [SerializeField] ComputeShader computeShader;

    [Header("Simulation Settings")]
    [SerializeField] float initSpeed = 5f;

    [SerializeField] Vector2 gravity = new Vector2(0, -9.8f);
    [SerializeField] float dampingFactor = 0.9f;

    /*
    Private properties
    */
    static int threadGroupSize = 64;
    int kernel;
    Spawn spawner;

    /*
    Public getters
    */
    public ComputeBuffer positionBuffer { get; private set; }
    public ComputeBuffer velocityBuffer { get; private set; }
    
    void Start()
    {
        spawner = GetComponent<Spawn>();
        SetupBuffers();
        InitialiseVariables();
    }

    Vector2[] GenerateVelocityData()
    {
        Vector2[] velocities = new Vector2[spawner.InstanceCount];

        for (int i = 0; i < spawner.InstanceCount; i++)
        {
            float random = Random.Range(0f, 2 * Mathf.PI);
            Vector2 vel = new Vector2(Mathf.Cos(random), Mathf.Sin(random)) * initSpeed;
            velocities[i] = vel;
        }

        return velocities;
    }

    void SetupBuffers()
    {
        positionBuffer = new ComputeBuffer(spawner.InstanceCount, sizeof(float) * 2);
        positionBuffer.SetData(spawner.Positions);

        Vector2[] velocities = GenerateVelocityData();
        velocityBuffer = new ComputeBuffer(spawner.InstanceCount, sizeof(float) * 2);
        velocityBuffer.SetData(velocities);
    }

    void InitialiseVariables()
    {
        computeShader.SetInt("threadGroupSize", threadGroupSize);
        UpdateVariables();

        kernel = computeShader.FindKernel("Gravity");

        computeShader.SetBuffer(kernel, "Positions", positionBuffer);
        computeShader.SetBuffer(kernel, "Velocities", velocityBuffer);
    }

    public void UpdateVariables()
    {
        computeShader.SetFloat("dampingFactor", dampingFactor);
        computeShader.SetVector("containerSize", GetComponentInChildren<Container>().Boundary);
        computeShader.SetVector("gravity", gravity);
    }
    
    void Update()
    {
        computeShader.SetFloat("deltaTime", Time.deltaTime);

        int threadGroups = Mathf.CeilToInt(spawner.InstanceCount / (float)threadGroupSize);
        if (spawner.InstanceCount == 0) return;
        computeShader.Dispatch(kernel, threadGroups, 1, 1);
    }



    void OnValidate()
    {
        UpdateVariables();
    }
    
    void OnDestroy()
    {
        if (positionBuffer != null)
            positionBuffer.Release();
        if (velocityBuffer != null)
            velocityBuffer.Release();
    }
}