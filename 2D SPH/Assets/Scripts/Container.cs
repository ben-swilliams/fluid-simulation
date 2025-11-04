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
    Private properties
    */
    private Vector3 lastScale;

    /*
    Public getters
    */
    public Vector3 Boundary => transform.localScale;

    void Start()
    {
        ClampScale();
        lastScale = transform.localScale;
    }

    void Update()
    {
        if (transform.hasChanged) OnValidate();
    }

    void LateUpdate()
    {
        if (lastScale != transform.localScale)
        {
            ClampScale();
        }
    }

    void OnValidate()
    {
        if (!Application.isPlaying) return;

        GetComponentInParent<Simulate>().UpdateBoundary();
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