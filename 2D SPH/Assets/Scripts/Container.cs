using UnityEngine;
using UnityEngine.UIElements;

public class Container : MonoBehaviour
{
    public Vector2 size = new Vector2(5f, 5f);
    LineRenderer lr;
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
    }

    void SetPoints()
    {
        lr.SetPosition(0, new Vector3(-size.x / 2f, -size.y / 2f));
        lr.SetPosition(1, new Vector3(size.x / 2f, -size.y / 2f));
        lr.SetPosition(2, new Vector3(size.x / 2f, size.y / 2f));
        lr.SetPosition(3, new Vector3(-size.x / 2f, size.y / 2f));
    }
}
