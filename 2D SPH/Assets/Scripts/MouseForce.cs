using UnityEngine;

class MouseForce : MonoBehaviour
{
    /*
    Inspector properties
    */
    [SerializeField] Material targetMaterial;

    [Header("Force settings")]
    [SerializeField] float power = 10f;
    [SerializeField] float radius = 1f;

    GameObject sphere;

    /*
    Private properties
    */
    Vector3 mousePos;
    float distance = 0f;
    float scrollSpeed = 10f;

    bool repulse = false;

    void Start()
    {
        sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.GetComponent<Renderer>().material = targetMaterial;
        sphere.SetActive(true);
    }

    void Update()
    {
        mousePos = FindClickPos();
        HandleScroll();
        HandleClick();
    }

    void OnValidate()
    {
        power = Mathf.Max(0, power);

        if (!Application.isPlaying || sphere == null) return;

        sphere.transform.localScale = Vector3.one * radius;
    }

    void HandleScroll()
    {
        float scroll = UnityEngine.InputSystem.Mouse.current.scroll.ReadValue().y;
        distance += scroll * scrollSpeed * Time.deltaTime;
        distance = Mathf.Max(0, distance);
        sphere.transform.position = mousePos;
    }

    void HandleClick()
    {
        float signedPower = repulse ? -1 * power : power;
        if (UnityEngine.InputSystem.Mouse.current.leftButton.isPressed)
        {
            sphere.transform.position = mousePos;
            GetComponent<Simulate>().UpdateMouseForce(mousePos, radius * 0.5f, signedPower);
        }
        else
        {
            if (UnityEngine.InputSystem.Mouse.current.rightButton.wasPressedThisFrame)
            {
                repulse = !repulse;
                Color sphereColor = repulse ? Color.black : Color.white;
                sphereColor.a = 0.3f;
                sphere.GetComponent<Renderer>().material.color = sphereColor;
            }
            GetComponent<Simulate>().UpdateMouseForce(Vector3.zero, 0, 0);
        }
    }

    Vector3 FindClickPos()
    {
        Vector3 mouse = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
        mouse.z = distance;
        Vector3 pos = Camera.main.ScreenToWorldPoint(mouse);

        return pos;
    }
}