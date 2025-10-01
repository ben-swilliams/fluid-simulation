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
    Public getters
    */
    public Vector2 Boundary => boundary;

    void OnValidate()
    {
        if (!Application.isPlaying) return;

        GetComponentInParent<Simulate>().UpdateBoundary();
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;

        Gizmos.DrawWireCube(transform.position, new Vector3(boundary.x, boundary.y, 0f));
    }
}
