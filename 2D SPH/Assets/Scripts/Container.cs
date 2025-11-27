using UnityEngine;

[ExecuteAlways]
public class Container : MonoBehaviour
{
    /*
    Inspector settings
    */
    [Header("Size settings")]

    [Header("Miscallaneous")]
    [SerializeField] Mesh mesh;

    /*
    Private properties
    */
    Vector3 lastScale;

    /*
    Public getters
    */
    public Vector3 Boundary => transform.localScale;

    void Start()
    {
        ClampScale();
    }

    void Update()
    {
        if (transform.localScale != lastScale) {
            OnValidate();
            lastScale = transform.localScale;
        }
    }

    void OnValidate()
    {
        ClampScale();

        GetComponentInParent<Simulate>().UpdateBoundary();
        GetComponentInParent<Simulate>().ValidateInspectorProperties();
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;

        Gizmos.DrawWireCube(transform.position, transform.localScale);
    }

    void ClampScale()
    {
        transform.localScale = new Vector3(
            Mathf.Max(0.01f, transform.localScale.x),
            Mathf.Max(0.01f, transform.localScale.y),
            Mathf.Max(0.01f, transform.localScale.z)
        );
    }
}