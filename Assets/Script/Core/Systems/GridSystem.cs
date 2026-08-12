using UnityEngine;

public class GridSystem : MonoBehaviour
{
    public static GridSystem Instance { get; private set; }

    [Header("Settings")]
    public float cellSize = 1f;
    public Vector3 origin = Vector3.zero;

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        // Статический словарь клеток: сброс на старте сцены (важно при Domain Reload Off)
        GridOccupancy.ClearAll();
        EnsureComponent<BuildGridVisualizer>();
    }

    void EnsureComponent<T>() where T : Component
    {
        if (GetComponent<T>() == null)
            gameObject.AddComponent<T>();
    }

    public Vector3 SnapToGrid(Vector3 worldPosition)
    {
        float x = Mathf.Round((worldPosition.x - origin.x) / cellSize) * cellSize + origin.x;
        float z = Mathf.Round((worldPosition.z - origin.z) / cellSize) * cellSize + origin.z;

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

    public Vector3 GetCellCenter(Vector2Int cell, float y)
    {
        Vector3 world = CellToWorld(cell);
        world.y = y;
        return world;
    }

    public Vector3 SnapToCellCenter(Vector3 worldPosition)
    {
        Vector2Int cell = WorldToCell(worldPosition);
        return GetCellCenter(cell, worldPosition.y);
    }
}
