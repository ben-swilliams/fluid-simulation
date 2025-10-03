using UnityEngine;

class MouseForce : MonoBehaviour
{
    /*
    Inspector properties
    */
    [SerializeField] float radius = 5f;
    [SerializeField] float power = 3f;

    void Update()
    {
        if (UnityEngine.InputSystem.Mouse.current.leftButton.isPressed)
        {
            Vector2 pos = FindClickPos();
            GetComponent<Simulate>().UpdateMouseForce(pos, radius, power);
        }
        else
        {
            GetComponent<Simulate>().UpdateMouseForce(Vector2.zero, 0, 0);
        }
    }

    void OnValidate()
    {
        radius = Mathf.Max(0, radius);

        if (!Application.isPlaying) return;
    }

    Vector2 FindClickPos()
    {
        Vector3 pos = Camera.main.ScreenToWorldPoint(UnityEngine.InputSystem.Mouse.current.position.ReadValue());
        pos.z = 0;

        return pos;
    }
}