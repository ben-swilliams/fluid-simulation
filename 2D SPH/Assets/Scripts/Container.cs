using UnityEngine;
using UnityEngine.UIElements;

public class Container : MonoBehaviour
{
    /*
    Inspector properties
    */
    [SerializeField] Vector2 boundary = new Vector2(5f, 5f);

    /*
    Private properties
    */
    LineRenderer lr;

    /*
    Public getters
    */
    public Vector2 Boundary => boundary;
    void Setup()
    {
        lr = GetComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.loop = true;
        lr.positionCount = 4;
    }
    void Start()
    {
        Setup();

        SetPoints();
    }

    void Update()
    {
        SetPoints();
    }

    void OnValidate()
    {
        Setup();
        SetPoints();
        GetComponentInParent<Simulate>().UpdateVariables();
    }

    void SetPoints()
    {
        lr.SetPosition(0, new Vector3(-boundary.x / 2f, -boundary.y / 2f));
        lr.SetPosition(1, new Vector3(boundary.x / 2f, -boundary.y / 2f));
        lr.SetPosition(2, new Vector3(boundary.x / 2f, boundary.y / 2f));
        lr.SetPosition(3, new Vector3(-boundary.x / 2f, boundary.y / 2f));
    }
}
