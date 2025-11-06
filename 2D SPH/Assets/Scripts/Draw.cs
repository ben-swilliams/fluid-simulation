using UnityEngine;

public class Draw : MonoBehaviour
{
    public enum Property { Velocity, Density, Pressure }

    /*
    Inspector properties
    */
    [Header("Shaders")]
    [SerializeField] Shader shader;

    [Header("Appearance Settings")]
    [SerializeField, Range(0, 4)] int sphereResolution = 2;
    [SerializeField] Color fastColour;
    [SerializeField] Color slowColour;
    [SerializeField] float maxSpeed = 10f;
    [SerializeField] float maxDensityFluctuation = 0.1f;
    [SerializeField] float maxPressure = 5000f;
    [SerializeField] Property colourProperty;

    /*
    Private properties
    */
    Material instanceMaterial;
    Bounds bounds;
    Mesh mesh;

    uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
    ComputeBuffer argsBuffer;

    ShaderHelper computeShader;

    float lowHue;
    float highHue;

    /*
    Public getters
    */
    public Property ColourProperty => colourProperty;

    void Start()
    {
        instanceMaterial = new Material(shader);
        bounds = new Bounds(Vector3.zero, Vector3.one * 1000f);
        mesh = GenerateSphere();
    }

    void OnValidate()
    {
        maxSpeed = Mathf.Max(0, maxSpeed);
        maxDensityFluctuation = Mathf.Clamp01(maxDensityFluctuation);
        maxPressure = Mathf.Max(0, maxPressure);

        Color.RGBToHSV(slowColour, out lowHue, out _, out _);
        Color.RGBToHSV(fastColour, out highHue, out _, out _);

        if (!Application.isPlaying || instanceMaterial == null) return;

        if (computeShader == null)
            computeShader = GetComponent<Simulate>().Shader;

        computeShader.SetValues(new object[]
        {
            "lowHue", lowHue,
            "highHue", highHue,
            "maxSpeed", maxSpeed,
            "maxDensityFluctuation", maxDensityFluctuation,
            "maxPressure", maxPressure
        });

        mesh = GenerateSphere();
    }

    void OnDestroy()
    {
        CleanupArgsBuffer();
    }

    void InitialiseArgsBuffer(int instanceCount)
    {
        CleanupArgsBuffer();

        argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);

        args[0] = mesh.GetIndexCount(0);
        args[1] = (uint)instanceCount;
        args[2] = mesh.GetIndexStart(0);
        args[3] = mesh.GetBaseVertex(0);
        argsBuffer.SetData(args);
    }

    void CleanupArgsBuffer()
    {
        if (argsBuffer == null) return;

        argsBuffer.Release();
        argsBuffer = null;
    }

    void BindColours(ComputeBuffer colourBuffer)
    {
        instanceMaterial.SetBuffer("colours", colourBuffer);
    }

    Mesh GenerateSphere()
	{
		Mesh sphere = new Mesh();
        sphere.name = "Icosphere";

        var vertices = new System.Collections.Generic.List<Vector3>();
        var triangles = new System.Collections.Generic.List<int>();

        float t = (1.0f + Mathf.Sqrt(5.0f)) / 2.0f;

        vertices.Add(new Vector3(-1,  t,  0).normalized);
        vertices.Add(new Vector3( 1,  t,  0).normalized);
        vertices.Add(new Vector3(-1, -t,  0).normalized);
        vertices.Add(new Vector3( 1, -t,  0).normalized);

        vertices.Add(new Vector3( 0, -1,  t).normalized);
        vertices.Add(new Vector3( 0,  1,  t).normalized);
        vertices.Add(new Vector3( 0, -1, -t).normalized);
        vertices.Add(new Vector3( 0,  1, -t).normalized);

        vertices.Add(new Vector3( t,  0, -1).normalized);
        vertices.Add(new Vector3( t,  0,  1).normalized);
        vertices.Add(new Vector3(-t,  0, -1).normalized);
        vertices.Add(new Vector3(-t,  0,  1).normalized);

        // 20 faces of icosahedron
        int[][] faces = new int[][] {
            new int[] {0, 11, 5}, new int[] {0, 5, 1}, new int[] {0, 1, 7}, new int[] {0, 7, 10}, new int[] {0, 10, 11},
            new int[] {1, 5, 9}, new int[] {5, 11, 4}, new int[] {11, 10, 2}, new int[] {10, 7, 6}, new int[] {7, 1, 8},
            new int[] {3, 9, 4}, new int[] {3, 4, 2}, new int[] {3, 2, 6}, new int[] {3, 6, 8}, new int[] {3, 8, 9},
            new int[] {4, 9, 5}, new int[] {2, 4, 11}, new int[] {6, 2, 10}, new int[] {8, 6, 7}, new int[] {9, 8, 1}
        };

        foreach (var face in faces)
        {
            triangles.Add(face[0]);
            triangles.Add(face[1]);
            triangles.Add(face[2]);
        }

        // Subdivide based on sphereResolution
        int subdivisions = Mathf.Clamp(sphereResolution, 0, 4); // Max 4 subdivisions
        for (int i = 0; i < subdivisions; i++)
        {
            var newTriangles = new System.Collections.Generic.List<int>();
            var midpointCache = new System.Collections.Generic.Dictionary<long, int>();

            for (int tri = 0; tri < triangles.Count; tri += 3)
            {
                int v0 = triangles[tri];
                int v1 = triangles[tri + 1];
                int v2 = triangles[tri + 2];

                int m01 = GetMidpoint(v0, v1, vertices, midpointCache);
                int m12 = GetMidpoint(v1, v2, vertices, midpointCache);
                int m20 = GetMidpoint(v2, v0, vertices, midpointCache);

                newTriangles.Add(v0); newTriangles.Add(m01); newTriangles.Add(m20);
                newTriangles.Add(v1); newTriangles.Add(m12); newTriangles.Add(m01);
                newTriangles.Add(v2); newTriangles.Add(m20); newTriangles.Add(m12);
                newTriangles.Add(m01); newTriangles.Add(m12); newTriangles.Add(m20);
            }

            triangles = newTriangles;
        }

        sphere.vertices = vertices.ToArray();
        sphere.triangles = triangles.ToArray();
        sphere.RecalculateNormals();
        sphere.RecalculateBounds();

        return sphere;
	}

    int GetMidpoint(int v0, int v1, System.Collections.Generic.List<Vector3> vertices, System.Collections.Generic.Dictionary<long, int> cache)
    {
        // Create unique key for edge
        long key = ((long)Mathf.Min(v0, v1) << 32) | (long)Mathf.Max(v0, v1);

        if (cache.TryGetValue(key, out int midpoint))
            return midpoint;

        Vector3 mid = ((vertices[v0] + vertices[v1]) / 2.0f).normalized;
        int index = vertices.Count;
        vertices.Add(mid);
        cache[key] = index;

        return index;
    }

    public void DrawFrame()
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

    public void UpdateSize(float size)
    {
        instanceMaterial.SetFloat("size", size);
    }

    public void BindPositions(ComputeBuffer positionBuffer)
    {

        InitialiseArgsBuffer(positionBuffer.count);
        instanceMaterial.SetBuffer("positions", positionBuffer);
    }

    public void BindBuffers(ComputeBuffer positionBuffer, ComputeBuffer colourBuffer)
    {
        if (computeShader == null)
            computeShader = GetComponent<Simulate>().Shader;

        BindPositions(positionBuffer);
        BindColours(colourBuffer);
    }
}
