using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class Container : MonoBehaviour
{
    /*
    Inspector settings
    */
    [SerializeField] Vector2 boundary = new Vector2(5f, 5f);
    [SerializeField] Mesh mesh;

    /*
    Private properties
    */
    MeshFilter meshFilter;
    MeshRenderer meshRenderer;

    /*
    Public getters
    */
    public Vector2 Boundary => boundary;

    void Start()
    {
        InitialiseMesh();
        UpdateFieldSize();
        ApplyTexture();
    }

    void OnValidate()
    {
        if (!Application.isPlaying) return;

        UpdateFieldSize();
        GetComponentInParent<Simulate>().UpdateBoundary();
        GetComponentInParent<Density>().UpdateBoundary();
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;

        Gizmos.DrawWireCube(transform.position, new Vector3(boundary.x, boundary.y, 0f));
    }

    void InitialiseMesh()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshFilter.mesh = mesh;
        meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.material = new Material(Shader.Find("Unlit/Texture"));
    }

    void UpdateFieldSize()
    {
        transform.localScale = new Vector3(boundary.x, boundary.y, 1);
    }

    void ApplyTexture()
    {
        Density density = GetComponentInParent<Density>();
        meshRenderer.material.mainTexture = density.densityField;
    }
}
