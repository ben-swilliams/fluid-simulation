using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class Container : MonoBehaviour
{
    /*
    Inspector settings
    */
    [Header("Size settings")]
    [SerializeField] Vector3 boundary = new Vector3(5f, 5f, 5f);
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
    public Vector3 Boundary => boundary;

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

        Gizmos.DrawWireCube(transform.position, new Vector3(boundary.x, boundary.y, boundary.z));
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

        Vector3 half = boundary / 2f + Vector3.one * offset;

        Vector3[] corners = new Vector3[8]
        {
            new Vector3(-half.x, -half.y, -half.z),
            new Vector3( half.x, -half.y, -half.z),
            new Vector3( half.x,  half.y, -half.z),
            new Vector3(-half.x,  half.y, -half.z),
            new Vector3(-half.x, -half.y,  half.z),
            new Vector3( half.x, -half.y,  half.z),
            new Vector3( half.x,  half.y,  half.z),
            new Vector3(-half.x,  half.y,  half.z)
        };

        int[] edgeIndices = new int[]
        {
            0, 1, 1, 2, 2, 3, 3, 0,
            4, 5, 5, 6, 6, 7, 7, 4,
            0, 4, 1, 5, 2, 6, 3, 7
        };

        lr.positionCount = edgeIndices.Length;

        for (int i = 0; i < edgeIndices.Length; i++)
        {
            lr.SetPosition(i, corners[edgeIndices[i]]);
        }
    }
}