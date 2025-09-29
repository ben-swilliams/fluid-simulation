using UnityEngine;

public class Spawn : MonoBehaviour
{
    /*
    Inspector properties
    */
    [SerializeField] int instanceCount = 10;
    [SerializeField] float size = 0.1f;
    [SerializeField] Vector2 spawnArea = new Vector2(10f, 10f);

    /*
    Public getters
    */
    public int InstanceCount => instanceCount;
    public float Size => size;
    public Vector2[] Positions => positions;

    /*
    Private properties
    */
    Vector2[] positions;

    void Start()
    {
        positions = GeneratePositions();
    }

    Vector2[] GenerateRandomPositions()
    {
        Vector2[] positions = new Vector2[instanceCount];

        for (int i = 0; i < instanceCount; i++)
        {
            positions[i] = new Vector2(
                Random.Range(-spawnArea.x / 2 + size / 2, spawnArea.x / 2 - size / 2),
                Random.Range(-spawnArea.y / 2 + size / 2, spawnArea.y / 2 - size / 2)
            );
        }

        return positions;
    }

    Vector2[] GeneratePositions()
    {
        return GenerateRandomPositions();
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.white;

        Gizmos.DrawWireCube(transform.position, new Vector3(spawnArea.x, spawnArea.y, 0f));
    }

    void OnValidate()
    {
        Draw drawer = GetComponentInChildren<Draw>();
        if (drawer == null) return;

        if (positions != null && positions.Length != instanceCount)
        {
            positions = GeneratePositions();
            drawer.UpdatePositions();
        }
    }
}
