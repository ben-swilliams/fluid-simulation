using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Common;

public class Simulate : MonoBehaviour
{
    /*
    Inspector properties
    */

    [Header("Simulation Settings")]
    [SerializeField] float simulationSpeed = 1f;
    [SerializeField] float smoothingRadius = 0.1f;
    [SerializeField] int stepSize = 10;
    [SerializeField] int binNumber = 200000;
    [SerializeField] bool indexHash = true;
    [SerializeField] int densityTextureRes = 100;


    /*
    Private properties
    */
    Spawn spawner;
    Draw drawer;
    Compute compute;
    bool started;
    float physicsTimeStep;
    float accumulator = 0f;
    int maxStepsPerFrame = 3;

    int instanceCount;

    RenderTexture densityTex;

    /*
    Public getters
    */
    public bool Started => started;
    public float SmoothingRadius => smoothingRadius;

    public RenderTexture DensityTex => densityTex;

    void Start()
    {
        spawner = GetComponent<Spawn>();
        drawer = GetComponent<Draw>();
    }

    void Update()
    {
        HandleKeyPresses();

        if (started)
        {
            AdvanceFrame();
            if (drawer.DrawTarget != Draw.DrawMethod.Particles) DispatchTextureWrite();
        }

        drawer.DrawFrame(densityTex, started);
    }


    void AdvanceFrame()
    {
        accumulator += Time.deltaTime;

        int stepsThisFrame = 0;
        while (accumulator >= physicsTimeStep && stepsThisFrame < maxStepsPerFrame)
        {
            compute.RunPhysicsStep(binNumber);
            compute.UpdateColours(drawer.ColourProperty);

            accumulator -= physicsTimeStep;
            stepsThisFrame++;
        }

        // Prevent spiral of death - if we're too far behind, reset accumulator
        if (accumulator > physicsTimeStep * maxStepsPerFrame)
        {
            accumulator = 0f;
        }
    }

    void OnValidate()
    {
        ValidateInspectorProperties();

        if (!Application.isPlaying || !started) return;

        UpdateVariables();
        UpdateBoundary();
    }

    void StartSimulation()
    {
        instanceCount = spawner.InstanceCount;

        if (indexHash)
            binNumber = Utils.CalculateCellNumber(GetComponentInChildren<Container>().Boundary, smoothingRadius);

        compute = GetComponent<Compute>();
        compute.Initialise(binNumber, spawner.ExtractPositions(), Utils.GenerateVelocityData(instanceCount));

        InitialiseVariables();
        UpdateBoundary();
        BindExternalBuffers();
        InitialiseLeapFrogVelocities();

        started = true;
    }

    void InitialiseLeapFrogVelocities()
    {
        object[] halfStep = new object[] { "deltaTime", physicsTimeStep * 0.5f };
        // Set half timestep for initialization
        compute.SetValues(halfStep);

        compute.RunPhysicsStep(binNumber);

        object[] fullStep = new object[] { "deltaTime", physicsTimeStep };
        compute.SetValues(fullStep);
    }

    void BindExternalBuffers()
    {
        drawer.BindBuffers(compute.Positions, compute.Colours);
        drawer.UpdateSize(spawner.Size);
    }

    void InitialiseVariables()
    {
        object[] keyValues =
        {
            "size", spawner.Size,
            "instanceCount", instanceCount
        };
        compute.SetValues(keyValues);

        UpdateMouseForce(Vector3.zero, 0, 0);
        UpdateVariables();
        UpdateBoundary();

        drawer.BindTexture(densityTex);
    }

    void UpdateVariables()
    {
        physicsTimeStep = 1f / Utils.SolverSteps(compute.PressureSolver);
        float deltaTime = physicsTimeStep * simulationSpeed;
        float particleSpacing = spawner.Size + spawner.Spacing;
        float particleMass = particleSpacing * particleSpacing * particleSpacing;
        float kernelConstant = 8f / (Mathf.PI * Mathf.Pow(smoothingRadius, 3));
        float gradConstant = 6 * kernelConstant / smoothingRadius;

        object[] keyValues =
        {
            "deltaTime", deltaTime,
            "smoothingRadius", smoothingRadius,
            "particleMass", particleMass,
            "kernelConstant", kernelConstant,
            "gradConstant", gradConstant,
            "tableSize", binNumber,
            "useIndex", indexHash ? 1 : 0,
        };

        compute.deltaTime = deltaTime;
        compute.smoothingRadius = smoothingRadius;
        compute.SetValues(keyValues);

        UpdateBoundary();
        UpdateDensityTexture();
    }

    public void ValidateInspectorProperties()
    {
        simulationSpeed = Mathf.Clamp(simulationSpeed, 0, 1);
        smoothingRadius = Mathf.Max(0.01f, smoothingRadius);
        stepSize = Mathf.Max(0, stepSize);
        binNumber = indexHash ? Utils.CalculateCellNumber(GetComponentInChildren<Container>().Boundary, smoothingRadius) : Mathf.Max(1, binNumber);
    }

    void HandleKeyPresses()
    {
        if (UnityEngine.InputSystem.Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            if (!started)
                StartSimulation();
            else
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        if (UnityEngine.InputSystem.Keyboard.current.rightArrowKey.wasPressedThisFrame)
        {
            if (simulationSpeed != 0) return;
            simulationSpeed = 1;
            UpdateVariables();
            for (int _ = 0; _ < stepSize; _++) AdvanceFrame();
            simulationSpeed = 0;
            UpdateVariables();
        }

        HandleSpeedControls();
    }

    void HandleSpeedControls()
    {
        if (UnityEngine.InputSystem.Keyboard.current.downArrowKey.wasPressedThisFrame)
        {
            simulationSpeed = Mathf.Max(0, simulationSpeed - 0.1f);
            UpdateVariables();
        }

        if (UnityEngine.InputSystem.Keyboard.current.upArrowKey.wasPressedThisFrame)
        {
            simulationSpeed = Mathf.Min(1, simulationSpeed + 0.1f);
            UpdateVariables();
        }

        if (UnityEngine.InputSystem.Keyboard.current.enterKey.wasPressedThisFrame)
        {
            simulationSpeed = simulationSpeed == 1 ? 0 : 1;
            UpdateVariables();
        }
    }

    void UpdateDensityTexture()
    {
        if (compute == null) return;

        Vector3 bounds = GetComponentInChildren<Container>().Boundary;
        float maxAxis = Mathf.Max(bounds.x, bounds.y, bounds.z);
        int width = Mathf.RoundToInt(bounds.x / maxAxis * densityTextureRes);
        int height = Mathf.RoundToInt(bounds.y / maxAxis * densityTextureRes);
        int depth = Mathf.RoundToInt(bounds.z / maxAxis * densityTextureRes);

        if (densityTex == null || densityTex.width != width || densityTex.height != height || densityTex.volumeDepth != depth)
        {
            if (densityTex != null) densityTex.Release();

            densityTex = Utils.CreateDensityTexture(width, height, depth);
            compute.SetTexture(densityTex);
        }
    }

    void DispatchTextureWrite()
    {
        int dispatchX = Mathf.CeilToInt(densityTex.width / 8f);
        int dispatchY = Mathf.CeilToInt(densityTex.height / 8f);
        int dispatchZ = Mathf.CeilToInt(densityTex.volumeDepth / 8f);

        compute.WriteToDensityTexture(dispatchX, dispatchY, dispatchZ);
    }

    public void UpdateMouseForce(Vector3 origin, float radius, float power)
    {
        if (compute == null) return;

        compute.SetPressureValues(new object[]
        {
            "mousePos", origin,
            "mouseRadius", radius,
            "power", power
        });
    }

    public void UpdateBoundary()
    {
        if (compute == null || spawner == null) return;

        float cellSize = 2f * smoothingRadius;

        Container container = GetComponentInChildren<Container>();

        int maxX = Mathf.CeilToInt(container.Boundary.x / cellSize) - 1;
        int maxY = Mathf.CeilToInt(container.Boundary.y / cellSize) - 1;
        int maxZ = Mathf.CeilToInt(container.Boundary.z / cellSize) - 1;

        object[] values = {
            "containerSize", container.Boundary,
            "maxCornerX", maxX,
            "maxCornerY", maxY,
            "maxCornerZ", maxZ
        };
        
        compute.SetValues(values);

        drawer.UpdateContainerSize(container.Boundary);
    }
}