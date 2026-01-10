using UnityEngine;

public class Cam : MonoBehaviour
{
    [SerializeField] float distance = 3;
    [SerializeField] float angle = 15;
    [SerializeField] float orbitSpeed = 1f;

    float currentOrbitAngle = 0f;

    void Update()
    {
        if (Application.isPlaying)
        {
            currentOrbitAngle += orbitSpeed * Time.deltaTime;
        }
        SetCameraSettings();
    }

    void OnValidate()
    {
        if (!Application.isPlaying) currentOrbitAngle = 0f;
        SetCameraSettings();
    }

    void SetCameraSettings()
    {
        Camera.main.transform.position = CalculateCameraPosition();
        Camera.main.transform.LookAt(Vector3.zero);
    }

    Vector3 CalculateCameraPosition()
    {
        float angleRad = -angle * Mathf.Deg2Rad;

        float x = -distance * Mathf.Cos(angleRad) * Mathf.Sin(currentOrbitAngle);
        float y = -distance * Mathf.Sin(angleRad);
        float z = distance * Mathf.Cos(angleRad) * Mathf.Cos(currentOrbitAngle);
        return new Vector3(x, y, z);
    }
}
