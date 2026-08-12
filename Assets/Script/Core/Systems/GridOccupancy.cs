using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Словарь занятости клеток сетки. Источник истины для размещения и AutoConnector.
/// Не требует компонента в сцене — статический сервис рядом с GridSystem.
/// </summary>
public static class GridOccupancy
{
    private static readonly Dictionary<Vector2Int, GameObject> occupiedCells =
        new Dictionary<Vector2Int, GameObject>();

    private static readonly Dictionary<GameObject, List<Vector2Int>> objectCells =
        new Dictionary<GameObject, List<Vector2Int>>();

    public static bool IsCellFree(Vector2Int cell)
    {
        if (!occupiedCells.TryGetValue(cell, out GameObject obj))
            return true;

        // Мёртвые ссылки не блокируют клетку
        if (obj == null)
        {
            occupiedCells.Remove(cell);
            return true;
        }

        return false;
    }

    public static bool IsAreaFree(Vector2Int origin, Vector2Int size)
    {
        size = NormalizeSize(size);

        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
            {
                if (!IsCellFree(origin + new Vector2Int(x, y)))
                    return false;
            }
        }

        return true;
    }

    public static void Register(GameObject obj, Vector2Int cell)
    {
        Register(obj, cell, Vector2Int.one);
    }

    public static void Register(GameObject obj, Vector2Int origin, Vector2Int size)
    {
        if (obj == null)
            return;

        // Перерегистрация: сначала снимаем старые клетки
        Unregister(obj);

        size = NormalizeSize(size);
        var cells = new List<Vector2Int>(size.x * size.y);

        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
            {
                Vector2Int cell = origin + new Vector2Int(x, y);

                if (occupiedCells.TryGetValue(cell, out GameObject existing) && existing != null && existing != obj)
                {
                    Debug.LogWarning(
                        $"[GridOccupancy] Cell {cell} already occupied by {existing.name}, " +
                        $"overwriting with {obj.name}");
                }

                occupiedCells[cell] = obj;
                cells.Add(cell);
            }
        }

        objectCells[obj] = cells;
    }

    public static void Unregister(GameObject obj)
    {
        if (obj == null)
            return;

        if (!objectCells.TryGetValue(obj, out List<Vector2Int> cells))
            return;

        for (int i = 0; i < cells.Count; i++)
        {
            Vector2Int cell = cells[i];
            if (occupiedCells.TryGetValue(cell, out GameObject owner) && owner == obj)
                occupiedCells.Remove(cell);
        }

        objectCells.Remove(obj);
    }

    public static void Unregister(Vector2Int cell)
    {
        if (!occupiedCells.TryGetValue(cell, out GameObject obj))
            return;

        if (obj != null)
            Unregister(obj);
        else
            occupiedCells.Remove(cell);
    }

    public static GameObject GetAt(Vector2Int cell)
    {
        if (!occupiedCells.TryGetValue(cell, out GameObject obj))
            return null;

        if (obj == null)
        {
            occupiedCells.Remove(cell);
            return null;
        }

        return obj;
    }

    /// <summary>
    /// Размер с учётом поворота на 90° (Y). size.x = ширина по X, size.y = глубина по Z.
    /// </summary>
    public static Vector2Int GetRotatedSize(Vector2Int size, float rotationY)
    {
        size = NormalizeSize(size);
        int quarter = Mathf.RoundToInt(rotationY / 90f) % 4;
        if (quarter < 0) quarter += 4;

        if (quarter == 1 || quarter == 3)
            return new Vector2Int(size.y, size.x);

        return size;
    }

    public static Vector2Int NormalizeSize(Vector2Int size)
    {
        return new Vector2Int(Mathf.Max(1, size.x), Mathf.Max(1, size.y));
    }

    /// <summary>
    /// Для тестов / смены сцены.
    /// </summary>
    public static void ClearAll()
    {
        occupiedCells.Clear();
        objectCells.Clear();
    }
}
