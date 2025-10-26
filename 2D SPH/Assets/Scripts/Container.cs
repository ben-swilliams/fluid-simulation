using UnityEngine;

public class Container : MonoBehaviour
{
    /*
    Inspector settings
    */
    [Header("Size settings")]

    [Header("Miscallaneous")]
    [SerializeField] Mesh mesh;

    /*
    Public getters
    */
    public Vector3 Boundary => transform.localScale;

    void Update()
    {
        if (transform.hasChanged) OnValidate();
    }
    void OnValidate()
    {
        if (!Application.isPlaying) return;

        GetComponentInParent<Simulate>().UpdateBoundary();
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;

        Gizmos.DrawWireCube(transform.position,transform.localScale);
    }
}