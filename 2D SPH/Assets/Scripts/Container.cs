using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class Container : MonoBehaviour
{
    /*
    Inspector settings
    */
    [Header("Size settings")]
    [SerializeField] Vector2 boundary = new Vector2(5f, 5f);
    [SerializeField] float thickness = 0.1f;

    [Header("Miscallaneous")]
    [SerializeField] Mesh mesh;

    /*
    Private properties
    */
    LineRenderer lr;

    /*
    Public getters
    */
    public Vector2 Boundary => boundary;

    void Start()
    {
        SetupBorder();
    }

    void OnValidate()
    {
        thickness = Mathf.Max(0, thickness);

        if (!Application.isPlaying) return;

        SetupBorder();
        GetComponentInParent<Simulate>().UpdateBoundary();
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;

        Gizmos.DrawWireCube(transform.position, new Vector3(boundary.x, boundary.y, 0f));
    }

    void SetupBorder()
    {
        lr = GetComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.loop = true;
        lr.positionCount = 4;
        lr.startColor = Color.white;
        lr.endColor = Color.white;
        lr.startWidth = thickness;
        lr.endWidth = thickness;
        SetPoints();
    }

    void SetPoints()
    {
        float offset = thickness / 2f;
        
        lr.SetPosition(0, new Vector3(-boundary.x / 2f - offset, -boundary.y / 2f - offset));
        lr.SetPosition(1, new Vector3(boundary.x / 2f + offset, -boundary.y / 2f - offset));
        lr.SetPosition(2, new Vector3(boundary.x / 2f + offset, boundary.y / 2f + offset));
        lr.SetPosition(3, new Vector3(-boundary.x / 2f - offset, boundary.y / 2f + offset));
    }
}