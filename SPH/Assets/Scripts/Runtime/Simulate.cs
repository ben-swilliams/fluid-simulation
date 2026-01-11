using UnityEngine;
using UnityEngine.SceneManagement;
using Common;
using UnityEngine.Rendering.Universal;

public class Simulate : Tweakable
{
    /*
    Inspector properties
    */

    [Header("Simulation Settings")]
    [SerializeField] float smoothingRadius = 0.1f;
    [SerializeField] int stepSize = 10;
    [SerializeField] int binNumber = 200000;
    [SerializeField] bool indexHash = true;
    [SerializeField] int densityTextureRes = 100;
    [SerializeField] float densityTextureRadius = 1f;

    [Header("Light Settings")]
    [SerializeField] Color lightColor;
    [SerializeField] float lightIntensity;
    [SerializeField] float lightRotation;


    /*
    Private properties
    */
    Spawn spawner;
    Draw drawer;
    Compute compute;
    Container container;
    bool started;
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
        container = GetComponentInChildren<Container>();
    }

    void Update()
    {
        HandleKeyPresses();

        if (started)
        {
            AdvanceFrame();
            if (drawer.DrawTarget != Draw.DrawMethod.Particles) DispatchTextureWrite();
        }

        drawer.DrawFrame(spawner.InstanceCount, densityTex, started, compute?.RestDensity ?? 0, container.NormalisedMatrix());
    }


    void AdvanceFrame()
    {
        accumulator += Time.deltaTime;

        int stepsThisFrame = 0;
        while (accumulator >= compute.PhysicsTimeStep && stepsThisFrame < maxStepsPerFrame)
        {
            compute.RunPhysicsStep(binNumber);
            compute.UpdateColours(drawer.ColourProperty);

            accumulator -= compute.PhysicsTimeStep;
            stepsThisFrame++;
        }

        // Prevent spiral of death - if we're too far behind, reset accumulator
        if (accumulator > compute.PhysicsTimeStep * maxStepsPerFrame)
        {
            accumulator = 0f;
        }
    }

    void OnValidate()
    {
        UpdateSettings();
    }

    void StartSimulation()
    {
        instanceCount = spawner.InstanceCount;

        if (indexHash)
            binNumber = Utils.CalculateCellNumber(container.Boundary, smoothingRadius);

        compute = GetComponent<Compute>();
        compute.Initialise(binNumber, spawner.ExtractPositions(), Utils.GenerateVelocityData(instanceCount));

        InitialiseVariables();
        UpdateBoundary();
        BindExternalBuffers();

        started = true;
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
    }

    void UpdateVariables()
    {
        float particleSpacing = spawner.Size + spawner.Spacing;
        float particleMass = particleSpacing * particleSpacing * particleSpacing;
        float kernelConstant = 8f / (Mathf.PI * Mathf.Pow(smoothingRadius, 3));
        float gradConstant = 6 * kernelConstant / smoothingRadius;

        object[] keyValues =
        {
            "smoothingRadius", smoothingRadius,
            "particleMass", particleMass,
            "kernelConstant", kernelConstant,
            "gradConstant", gradConstant,
            "tableSize", binNumber,
            "useIndex", indexHash ? 1 : 0,
            "densityTexRadius", densityTextureRadius,
        };

        if (compute)
        {
            compute.smoothingRadius = smoothingRadius;
            compute.SetValues(keyValues);
            compute.UpdateSettings();
        }

        UpdateBoundary();
        UpdateDensityTexture();
    }

    void UpdateLight()
    {
        Light sun = RenderSettings.sun;
        sun.intensity = lightIntensity;
        sun.color = lightColor;
        
        Vector3 oldAngles = sun.transform.eulerAngles;
        sun.transform.eulerAngles = new Vector3(lightRotation, oldAngles.y, oldAngles.z);
    }

    public override void UpdateSettings()
    {
    
        ValidateInspectorProperties();
        UpdateLight();

        if (!Application.isPlaying || !started) return;

        UpdateVariables();
        UpdateBoundary();
    }

    public void ValidateInspectorProperties()
    {
        smoothingRadius = Mathf.Max(0.01f, smoothingRadius);
        stepSize = Mathf.Max(0, stepSize);

        if (container)
            binNumber = indexHash ? Utils.CalculateCellNumber(container.Boundary, smoothingRadius) : Mathf.Max(1, binNumber);
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
            if (compute.SimulationSpeed != 0) return;
            compute.SetSimSpeed(1);
            UpdateVariables();
            for (int _ = 0; _ < stepSize; _++) AdvanceFrame();
            compute.SetSimSpeed(0);
            UpdateVariables();
        }

        if (UnityEngine.InputSystem.Keyboard.current.digit0Key.wasPressedThisFrame)
            SceneManager.LoadScene("Scene");
        if (UnityEngine.InputSystem.Keyboard.current.digit1Key.wasPressedThisFrame)
            SceneManager.LoadScene("WCSPH");
        if (UnityEngine.InputSystem.Keyboard.current.digit2Key.wasPressedThisFrame)
            SceneManager.LoadScene("PCISPH");
        if (UnityEngine.InputSystem.Keyboard.current.digit3Key.wasPressedThisFrame)
            SceneManager.LoadScene("IISPH");

        HandleSpeedControls();
    }

    void HandleSpeedControls()
    {
        if (UnityEngine.InputSystem.Keyboard.current.downArrowKey.wasPressedThisFrame)
        {
            compute.SetSimSpeed(compute.SimulationSpeed - 0.1f);
            UpdateVariables();
        }

        if (UnityEngine.InputSystem.Keyboard.current.upArrowKey.wasPressedThisFrame)
        {
            compute.SetSimSpeed(compute.SimulationSpeed + 0.1f);
            UpdateVariables();
        }

        if (UnityEngine.InputSystem.Keyboard.current.enterKey.wasPressedThisFrame)
        {
            compute.SetSimSpeed(compute.SimulationSpeed == 1 ? 0 : 1);
            UpdateVariables();
        }
    }

    void UpdateDensityTexture()
    {
        if (compute == null) return;

        Vector3 bounds = container.Boundary;
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

        float cellSize = smoothingRadius;

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