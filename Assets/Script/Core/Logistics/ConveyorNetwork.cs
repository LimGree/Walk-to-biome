using System.Collections.Generic;
using UnityEngine;

public class ConveyorNetwork : MonoBehaviour
{
    public static ConveyorNetwork Instance { get; private set; }

    private readonly List<ConveyorBelt> allBelts = new List<ConveyorBelt>();
    private readonly Dictionary<Vector2Int, List<BuildingSocket>> socketsByCell = new Dictionary<Vector2Int, List<BuildingSocket>>();
    private readonly Dictionary<Vector2Int, List<ConveyorBelt>> beltStartsByCell = new Dictionary<Vector2Int, List<ConveyorBelt>>();
    private readonly Dictionary<Vector2Int, List<ConveyorBelt>> beltEndsByCell = new Dictionary<Vector2Int, List<ConveyorBelt>>();

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    public void RegisterBelt(ConveyorBelt belt)
    {
        if (belt == null || allBelts.Contains(belt))
            return;

        allBelts.Add(belt);
        RegisterBeltPoint(belt, belt.startPoint, beltStartsByCell);
        RegisterBeltPoint(belt, belt.endPoint, beltEndsByCell);
    }

    public void UnregisterBelt(ConveyorBelt belt)
    {
        if (belt == null)
            return;

        UnregisterBeltPoint(belt, belt.startPoint, beltStartsByCell);
        UnregisterBeltPoint(belt, belt.endPoint, beltEndsByCell);
        allBelts.Remove(belt);
    }

    public void RegisterSocket(BuildingSocket socket)
    {
        if (socket == null || GridSystem.Instance == null)
            return;

        Vector2Int cell = GridSystem.Instance.WorldToCell(socket.transform.position);
        AddToCellMap(socketsByCell, cell, socket);
    }

    public void UnregisterSocket(BuildingSocket socket)
    {
        if (socket == null || GridSystem.Instance == null)
            return;

        Vector2Int cell = GridSystem.Instance.WorldToCell(socket.transform.position);
        RemoveFromCellMap(socketsByCell, cell, socket);
    }

    public void QuerySocketsNear(
        Vector3 worldPosition,
        float radius,
        SocketType type,
        List<BuildingSocket> results)
    {
        results.Clear();
        QueryCellsNear(worldPosition, radius, socketsByCell, (socket) =>
        {
            if (socket != null && socket.socketType == type)
                results.Add(socket);
        });
    }

    public void QueryBeltsNearStart(Vector3 worldPosition, float radius, List<ConveyorBelt> results)
    {
        results.Clear();
        QueryBeltsNearPoint(worldPosition, radius, beltStartsByCell, results);
    }

    public void QueryBeltsNearEnd(Vector3 worldPosition, float radius, List<ConveyorBelt> results)
    {
        results.Clear();
        QueryBeltsNearPoint(worldPosition, radius, beltEndsByCell, results);
    }

    public void QueryBeltsNearEndPoint(Vector3 worldPosition, float radius, List<ConveyorBelt> results)
    {
        results.Clear();
        QueryBeltsNearPoint(worldPosition, radius, beltEndsByCell, results, useEndPoint: true);
    }

    public void QueryBeltsNearStartPoint(Vector3 worldPosition, float radius, List<ConveyorBelt> results)
    {
        results.Clear();
        QueryBeltsNearPoint(worldPosition, radius, beltStartsByCell, results, useEndPoint: false);
    }

    void QueryBeltsNearPoint(
        Vector3 worldPosition,
        float radius,
        Dictionary<Vector2Int, List<ConveyorBelt>> map,
        List<ConveyorBelt> results,
        bool useEndPoint = false)
    {
        HashSet<ConveyorBelt> unique = new HashSet<ConveyorBelt>();

        QueryCellsNear(worldPosition, radius, map, (belt) =>
        {
            if (belt == null || !unique.Add(belt))
                return;

            Transform point = useEndPoint ? belt.endPoint : belt.startPoint;
            if (point == null)
                return;

            if (Vector3.Distance(worldPosition, point.position) <= radius)
                results.Add(belt);
        });
    }

    void QueryCellsNear<T>(
        Vector3 worldPosition,
        float radius,
        Dictionary<Vector2Int, List<T>> map,
        System.Action<T> visitor) where T : Object
    {
        if (GridSystem.Instance == null || visitor == null)
            return;

        float cellSize = GridSystem.Instance.cellSize;
        int cellRadius = Mathf.CeilToInt(radius / cellSize) + 1;
        Vector2Int center = GridSystem.Instance.WorldToCell(worldPosition);

        for (int dx = -cellRadius; dx <= cellRadius; dx++)
        {
            for (int dz = -cellRadius; dz <= cellRadius; dz++)
            {
                Vector2Int cell = new Vector2Int(center.x + dx, center.y + dz);
                if (!map.TryGetValue(cell, out List<T> entries))
                    continue;

                for (int i = 0; i < entries.Count; i++)
                    visitor(entries[i]);
            }
        }
    }

    static void RegisterBeltPoint(
        ConveyorBelt belt,
        Transform point,
        Dictionary<Vector2Int, List<ConveyorBelt>> map)
    {
        if (belt == null || point == null || GridSystem.Instance == null)
            return;

        Vector2Int cell = GridSystem.Instance.WorldToCell(point.position);
        AddToCellMap(map, cell, belt);
    }

    static void UnregisterBeltPoint(
        ConveyorBelt belt,
        Transform point,
        Dictionary<Vector2Int, List<ConveyorBelt>> map)
    {
        if (belt == null || point == null || GridSystem.Instance == null)
            return;

        Vector2Int cell = GridSystem.Instance.WorldToCell(point.position);
        RemoveFromCellMap(map, cell, belt);
    }

    static void AddToCellMap<T>(Dictionary<Vector2Int, List<T>> map, Vector2Int cell, T item) where T : Object
    {
        if (!map.TryGetValue(cell, out List<T> list))
        {
            list = new List<T>();
            map[cell] = list;
        }

        if (!list.Contains(item))
            list.Add(item);
    }

    static void RemoveFromCellMap<T>(Dictionary<Vector2Int, List<T>> map, Vector2Int cell, T item) where T : Object
    {
        if (!map.TryGetValue(cell, out List<T> list))
            return;

        list.Remove(item);
        if (list.Count == 0)
            map.Remove(cell);
    }
}
