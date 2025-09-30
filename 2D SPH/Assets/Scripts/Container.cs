using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class Container : MonoBehaviour
{
    [SerializeField] Vector2 boundary = new Vector2(5f, 5f);

    MeshFilter meshFilter;
    MeshRenderer meshRenderer;
    Mesh mesh;

    public Vector2 Boundary => boundary;

    void Start()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.material = new Material(Shader.Find("Unlit/Texture"));

        CreateQuad();
        UpdateFieldSize();
        ApplyTexture();
    }

    void OnValidate()
    {
        if (!Application.isPlaying) return;

        UpdateFieldSize();
        ApplyTexture();
    }

    void CreateQuad()
    {
        mesh = new Mesh();
        mesh.name = "ContainerQuad";

        Vector3[] vertices = new Vector3[4]
        {
            new Vector3(-0.5f, -0.5f, 0),
            new Vector3(0.5f, -0.5f, 0),
            new Vector3(-0.5f, 0.5f, 0),
            new Vector3(0.5f, 0.5f, 0)
        };

        int[] triangles = new int[6] { 0, 2, 1, 2, 3, 1 };

        Vector2[] uv = new Vector2[4]
        {
            new Vector2(0,0),
            new Vector2(1,0),
            new Vector2(0,1),
            new Vector2(1,1)
        };

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uv;

        meshFilter.mesh = mesh;
    }

    void UpdateFieldSize()
    {
        transform.localScale = new Vector3(boundary.x, boundary.y, 1);
    }

    void ApplyTexture()
    {
        Density density = GetComponentInParent<Density>();
        if (density != null && density.DensityField != null)
        {
            meshRenderer.material.mainTexture = density.DensityField;
        }
    }
}
