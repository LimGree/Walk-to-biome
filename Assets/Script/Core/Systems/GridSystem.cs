using UnityEngine;

public class GridSystem : MonoBehaviour
{
    public static GridSystem Instance { get; private set; }

    [Header("Settings")]
    public float cellSize = 1f;
    public Vector3 origin = Vector3.zero;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public Vector3 SnapToGrid(Vector3 worldPosition)
    {
        float x = Mathf.Round((worldPosition.x - origin.x) / cellSize) * cellSize + origin.x;
        float z = Mathf.Round((worldPosition.z - origin.z) / cellSize) * cellSize + origin.z;

        // Y обычно оставляем как есть (высота поверхности)
        return new Vector3(x, worldPosition.y, z);
    }

    public Vector2Int WorldToCell(Vector3 worldPosition)
    {
        int x = Mathf.RoundToInt((worldPosition.x - origin.x) / cellSize);
        int z = Mathf.RoundToInt((worldPosition.z - origin.z) / cellSize);
        return new Vector2Int(x, z);
    }

    public Vector3 CellToWorld(Vector2Int cell)
    {
        return new Vector3(
            cell.x * cellSize + origin.x,
            0f,
            cell.y * cellSize + origin.z
        );
    }

    // Для отладки
    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        Gizmos.color = new Color(1f, 1f, 1f, 0.05f);
        // Можно нарисовать сетку вокруг игрока, если нужно
    }
}