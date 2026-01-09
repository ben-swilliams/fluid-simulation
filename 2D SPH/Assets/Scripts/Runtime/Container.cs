using Unity.Mathematics;
using UnityEngine;

[ExecuteAlways]
public class Container : MonoBehaviour
{
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
        if (transform.localScale != lastScale)
        {
            OnValidate();
            lastScale = transform.localScale;
        }
    }

    void OnValidate()
    {
        ClampScale();

        if (!Application.isPlaying) return;

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

    public Matrix4x4 NormalisedMatrix()
    {
        Vector3 scale = transform.lossyScale;
        Vector3 position = transform.position;
        Quaternion rotation = transform.rotation;

        Matrix4x4 worldToLocal = Matrix4x4.TRS(position, rotation, scale).inverse;

        Matrix4x4 translateMatrix = Matrix4x4.Translate(Vector3.one * 0.5f);

        return translateMatrix * worldToLocal;
    }
}