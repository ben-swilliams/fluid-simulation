using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
class MouseForce : MonoBehaviour
{
    /*
    Inspector properties
    */
    [SerializeField] float power = 10f;

    /*
    Private properties
    */
    LineRenderer lr;
    Vector3 mousePos;
    float radius = 5f;
    float scrollSpeed = 10f;
    int segments = 100;

    bool repulse = false;

    void Start()
    {
        lr = GetComponent<LineRenderer>();
        
        // Draw on top of particles
        lr.material.renderQueue = 4000;
    }

    void Update()
    {
        mousePos = FindClickPos();
        HandleScroll();
        HandleClick();
        DrawRadius();
    }

    void OnValidate()
    {
        power = Mathf.Max(0, power);
    }

    void HandleScroll()
    {
        float scroll = UnityEngine.InputSystem.Mouse.current.scroll.ReadValue().y;
        radius += scroll * scrollSpeed * Time.deltaTime;
        radius = Mathf.Max(0, radius);
    }

    void HandleClick()
    {
        float signedPower = repulse ? -1 * power : power;
        if (UnityEngine.InputSystem.Mouse.current.leftButton.isPressed)
        {
            GetComponent<Simulate>().UpdateMouseForce(mousePos, radius, signedPower);
        }
        else
        {
            if (UnityEngine.InputSystem.Mouse.current.rightButton.wasPressedThisFrame) repulse = !repulse;
            GetComponent<Simulate>().UpdateMouseForce(Vector2.zero, 0, 0);
        }
    }

    Vector3 FindClickPos()
    {
        Vector3 pos = Camera.main.ScreenToWorldPoint(UnityEngine.InputSystem.Mouse.current.position.ReadValue());
        pos.z = 0;

        return pos;
    }

    void DrawRadius()
    {
        lr.startColor = repulse ? Color.black : Color.white;
        lr.endColor = repulse ? Color.black : Color.white;

        Vector3[] positions = new Vector3[segments];
        float angleStep = 2 * Mathf.PI / segments;

        for (int i = 0; i < segments; i++)
        {
            float angle = i * angleStep;
            positions[i] = mousePos + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0);
        }

        lr.positionCount = segments;
        lr.SetPositions(positions);
    }
}