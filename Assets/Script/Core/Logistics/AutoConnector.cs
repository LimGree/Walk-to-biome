using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Сеточные соединения по правилам из промт.txt:
/// связь только Exit→Entry/Input или Output→Entry/Input.
/// Без merge/split, без FindObjectsByType в горячем пути.
/// </summary>
public static class AutoConnector
{
    public static bool showDebug = false;

    private static readonly Vector2Int[] NeighborOffsets =
    {
        new Vector2Int(0, 1),
        new Vector2Int(1, 0),
        new Vector2Int(0, -1),
        new Vector2Int(-1, 0)
    };

    // ------------------------------------------------------------------
    // Public API
    // ------------------------------------------------------------------

    /// <summary>Clear + connect клетки и 4 соседей.</summary>
    public static void ReconnectNeighborhood(Vector2Int cell)
    {
        if (GridSystem.Instance == null)
            return;

        var objects = new List<GameObject>(8);
        var seen = new HashSet<int>();
        CollectNeighborhoodInto(cell, objects, seen);
        ReconnectObjectList(objects);
    }

    /// <summary>Clear + connect footprint объекта и всех соседей.</summary>
    public static void ReconnectObject(GameObject obj)
    {
        if (obj == null || GridSystem.Instance == null)
            return;

        List<Vector2Int> cells = GetOccupiedCells(obj);
        if (cells.Count == 0)
            return;

        var objects = new List<GameObject>(16);
        var seen = new HashSet<int>();
        for (int i = 0; i < cells.Count; i++)
            CollectNeighborhoodInto(cells[i], objects, seen);

        ReconnectObjectList(objects);
    }

    /// <summary>Один clear+connect для набора освобождённых клеток (demolish multi-cell).</summary>
    public static void ReconnectCells(List<Vector2Int> cells)
    {
        if (cells == null || cells.Count == 0 || GridSystem.Instance == null)
            return;

        var objects = new List<GameObject>(16);
        var seen = new HashSet<int>();
        for (int i = 0; i < cells.Count; i++)
            CollectNeighborhoodInto(cells[i], objects, seen);

        ReconnectObjectList(objects);
    }

    /// <summary>
    /// Полный reconnect по GridOccupancy (без FindObjectsByType).
    /// Собирает уникальные GO из всех занятых клеток.
    /// </summary>
    public static void ReconnectAll()
    {
        if (GridSystem.Instance == null)
            return;

        var objects = new List<GameObject>(64);
        var seen = new HashSet<int>();

        // Обход через известные объекты: belts/buildings регистрируются в occupancy
        // Собираем, сканируя bounding box активных объектов — только occupancy.
        // GridOccupancy не экспортирует все ключи; используем registered objects API.
        GridOccupancy.CollectAllOccupiedObjects(objects, seen);
        ReconnectObjectList(objects);
        Log($"[GridConnect] ReconnectAll: {objects.Count} objects");
    }

    public static void TryAutoConnect(GameObject placedObject)
    {
        ReconnectObject(placedObject);
    }

    public static void OnRotated(GameObject rotatedObject)
    {
        ReconnectObject(rotatedObject);
    }

    public static void OnCellFreed(Vector2Int cell)
    {
        ReconnectNeighborhood(cell);
    }

    static void ReconnectObjectList(List<GameObject> objects)
    {
        // Убрать уничтоженные / неактивные
        for (int i = objects.Count - 1; i >= 0; i--)
        {
            GameObject o = objects[i];
            if (o == null || !o.activeInHierarchy)
                objects.RemoveAt(i);
        }

        for (int i = 0; i < objects.Count; i++)
            ClearObjectLinks(objects[i]);

        for (int i = 0; i < objects.Count; i++)
        {
            if (objects[i] != null)
                ConnectObjectOnly(objects[i]);
        }
    }

    // ------------------------------------------------------------------
    // Clear
    // ------------------------------------------------------------------

    public static void ClearObjectLinks(GameObject obj)
    {
        if (obj == null) return;

        ConveyorBelt belt = obj.GetComponent<ConveyorBelt>();
        if (belt != null)
        {
            ClearBeltLinks(belt);
            return;
        }

        BuildingBase building = obj.GetComponent<BuildingBase>();
        if (building != null)
            ClearBuildingLinks(building);
    }

    public static void ClearBeltLinks(ConveyorBelt belt)
    {
        if (belt == null) return;

        // Сокеты симметрично
        BuildingSocket outSock = belt.connectedOutputSocket;
        belt.connectedOutputSocket = null;
        if (outSock != null && outSock.connectedBelt == belt)
            outSock.DisconnectBeltOnly();

        BuildingSocket inSock = belt.connectedInputSocket;
        belt.connectedInputSocket = null;
        if (inSock != null && inSock.connectedBelt == belt)
            inSock.DisconnectBeltOnly();

        // Кто указывал на нас nextBelt
        ConveyorBelt oldNext = belt.nextBelt;
        belt.ClearOutgoingBelts();
        belt.prevBelt = null;

        if (GridSystem.Instance == null)
            return;

        Vector2Int cell = GridSystem.Instance.WorldToCell(belt.transform.position);
        for (int i = 0; i < NeighborOffsets.Length; i++)
        {
            GameObject n = GridOccupancy.GetAt(cell + NeighborOffsets[i]);
            if (n == null) continue;

            ConveyorBelt other = n.GetComponent<ConveyorBelt>();
            if (other == null) continue;

            if (other.nextBelt == belt)
            {
                other.nextBelt = null;
                other.ClearOutgoingBelts();
            }
            if (other.prevBelt == belt)
                other.prevBelt = null;
        }

        // oldNext.prev мог указывать на нас
        if (oldNext != null && oldNext.prevBelt == belt)
            oldNext.prevBelt = null;
    }

    public static void ClearBuildingLinks(BuildingBase building)
    {
        if (building == null) return;

        if (building.inputSockets != null)
        {
            for (int i = 0; i < building.inputSockets.Length; i++)
            {
                if (building.inputSockets[i] != null)
                    building.inputSockets[i].DisconnectAll();
            }
        }

        if (building.outputSockets != null)
        {
            for (int i = 0; i < building.outputSockets.Length; i++)
            {
                if (building.outputSockets[i] != null)
                    building.outputSockets[i].DisconnectAll();
            }
        }
    }

    // ------------------------------------------------------------------
    // Connect — только свои исходящие (не чужие)
    // ------------------------------------------------------------------

    static void ConnectObjectOnly(GameObject obj)
    {
        if (obj == null) return;

        ConveyorBelt belt = obj.GetComponent<ConveyorBelt>();
        if (belt != null)
        {
            ConnectConveyorLocal(belt);
            return;
        }

        BuildingBase building = obj.GetComponent<BuildingBase>();
        if (building != null)
            ConnectBuildingLocal(building);
    }

    static void ConnectConveyorLocal(ConveyorBelt belt)
    {
        if (belt == null || GridSystem.Instance == null)
            return;
        // start/end могут отсутствовать на broken prefab — fallback на transform
        Vector2Int cell = GridSystem.Instance.WorldToCell(belt.transform.position);
        Vector3 exit = belt.GetExitDirection();

        for (int i = 0; i < NeighborOffsets.Length; i++)
        {
            Vector2Int offset = NeighborOffsets[i];
            if (!IsDirectionMatch(exit, offset))
                continue;

            Vector2Int neighborCell = cell + offset;
            TryConnectBeltExitToNeighbor(belt, neighborCell, offset);
        }
    }

    /// <summary>
    /// Лента смотрит exit на neighborCell: либо nextBelt, либо Input здания.
    /// </summary>
    static void TryConnectBeltExitToNeighbor(ConveyorBelt belt, Vector2Int neighborCell, Vector2Int offset)
    {
        ConveyorBelt other = FindBeltInCell(neighborCell);
        if (other != null)
        {
            // Exit → Entry (строго). Параллельные ленты не сливаются.
            if (IsDirectionMatch(other.GetEntryDirection(), offset))
            {
                belt.SetNextBelt(other);
                if (other.prevBelt == null)
                    other.prevBelt = belt;
                Log($"[GridConnect] {belt.name} → {other.name}");
            }
            return;
        }

        BuildingBase building = FindBuildingInCell(neighborCell);
        if (building == null)
            return;

        // Exit ленты → Input здания (Input смотрит на ленту = -offset)
        BuildingSocket input = FindSocketFacing(building, SocketType.Input, -offset);
        if (input != null)
            ConnectBeltToInput(belt, input);
    }

    static void ConnectBuildingLocal(BuildingBase building)
    {
        if (building == null || GridSystem.Instance == null)
            return;

        List<Vector2Int> cells = GetOccupiedCells(building.gameObject);
        var cellSet = new HashSet<Vector2Int>(cells);

        for (int c = 0; c < cells.Count; c++)
        {
            Vector2Int cell = cells[c];
            for (int i = 0; i < NeighborOffsets.Length; i++)
            {
                Vector2Int offset = NeighborOffsets[i];
                Vector2Int neighborCell = cell + offset;
                if (cellSet.Contains(neighborCell))
                    continue;

                TryConnectBuildingToward(building, neighborCell, offset);
            }
        }
    }

    /// <summary>offset = от здания к соседу.</summary>
    static void TryConnectBuildingToward(BuildingBase building, Vector2Int neighborCell, Vector2Int offset)
    {
        // Output здания смотрит на соседа (offset)
        BuildingSocket output = FindSocketFacing(building, SocketType.Output, offset);

        ConveyorBelt belt = FindBeltInCell(neighborCell);
        if (belt != null)
        {
            if (output != null && IsDirectionMatch(belt.GetEntryDirection(), offset))
                ConnectOutputToBelt(output, belt);

            // Лента → Input: exit ленты на здание (-offset), Input смотрит на ленту (offset)
            if (IsDirectionMatch(belt.GetExitDirection(), -offset))
            {
                BuildingSocket input = FindSocketFacing(building, SocketType.Input, offset);
                if (input != null)
                    ConnectBeltToInput(belt, input);
            }
            return;
        }

        BuildingBase neighbor = FindBuildingInCell(neighborCell);
        if (neighbor == null || neighbor == building)
            return;

        if (output == null)
            return;

        BuildingSocket neighborInput = FindSocketFacing(neighbor, SocketType.Input, -offset);
        if (neighborInput == null)
            return;

        // Output и Input смотрят друг на друга
        Vector3 outF = output.transform.forward;
        Vector3 inF = neighborInput.transform.forward;
        outF.y = 0f;
        inF.y = 0f;
        if (outF.sqrMagnitude < 0.0001f || inF.sqrMagnitude < 0.0001f)
            return;

        if (Vector3.Dot(outF.normalized, -inF.normalized) > 0.5f)
            output.ConnectSocket(neighborInput);
    }

    // ------------------------------------------------------------------
    // Socket links
    // ------------------------------------------------------------------

    static void ConnectOutputToBelt(BuildingSocket output, ConveyorBelt belt)
    {
        if (output == null || belt == null) return;
        if (output.socketType != SocketType.Output) return;

        if (output.connectedBelt == belt && belt.connectedOutputSocket == output)
            return;

        // Уже заняты другим — после clear не должны быть
        if (output.connectedBelt != null && output.connectedBelt != belt)
            return;
        if (belt.connectedOutputSocket != null && belt.connectedOutputSocket != output)
            return;

        output.ConnectBelt(belt);
        Log($"[GridConnect] OUTPUT {output.name} → {belt.name}");
    }

    static void ConnectBeltToInput(ConveyorBelt belt, BuildingSocket input)
    {
        if (belt == null || input == null) return;
        if (input.socketType != SocketType.Input) return;

        if (input.connectedBelt == belt && belt.connectedInputSocket == input)
            return;

        if (input.connectedBelt != null && input.connectedBelt != belt)
            return;
        if (belt.connectedInputSocket != null && belt.connectedInputSocket != input)
            return;

        input.ConnectBelt(belt);
        Log($"[GridConnect] {belt.name} → INPUT {input.name}");
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    static void CollectNeighborhoodInto(Vector2Int cell, List<GameObject> objects, HashSet<int> seen)
    {
        TryAddCellObject(cell, objects, seen);
        for (int i = 0; i < NeighborOffsets.Length; i++)
            TryAddCellObject(cell + NeighborOffsets[i], objects, seen);
    }

    static void TryAddCellObject(Vector2Int cell, List<GameObject> objects, HashSet<int> seen)
    {
        GameObject obj = GridOccupancy.GetAt(cell);
        if (obj == null) return;
        if (obj.GetComponent<ConveyorBelt>() == null && obj.GetComponent<BuildingBase>() == null)
            return;

        int id = obj.GetInstanceID();
        if (!seen.Add(id))
            return;

        objects.Add(obj);
    }

    static List<Vector2Int> GetOccupiedCells(GameObject obj)
    {
        return ConveyorReshape.GetOccupiedCells(obj);
    }

    static bool IsDirectionMatch(Vector3 direction, Vector2Int offset)
    {
        if (direction.sqrMagnitude < 0.0001f)
            return false;
        Vector3 target = new Vector3(offset.x, 0f, offset.y);
        if (target.sqrMagnitude < 0.0001f)
            return false;
        return Vector3.Dot(direction.normalized, target.normalized) > 0.7f;
    }

    /// <summary>Сокет, чей forward лучше всего смотрит в desiredOffset (мир).</summary>
    static BuildingSocket FindSocketFacing(BuildingBase building, SocketType type, Vector2Int desiredOffset)
    {
        BuildingSocket[] sockets = type == SocketType.Input ? building.inputSockets : building.outputSockets;
        if (sockets == null) return null;

        Vector3 desired = new Vector3(desiredOffset.x, 0f, desiredOffset.y);
        if (desired.sqrMagnitude < 0.0001f)
            return null;
        desired.Normalize();

        BuildingSocket best = null;
        float bestDot = 0.5f;

        for (int i = 0; i < sockets.Length; i++)
        {
            BuildingSocket socket = sockets[i];
            if (socket == null) continue;
            if (socket.socketType != type) continue;

            Vector3 f = socket.transform.forward;
            f.y = 0f;
            if (f.sqrMagnitude < 0.0001f) continue;

            float dot = Vector3.Dot(f.normalized, desired);
            if (dot > bestDot)
            {
                bestDot = dot;
                best = socket;
            }
        }

        return best;
    }

    static ConveyorBelt FindBeltInCell(Vector2Int cell)
    {
        GameObject obj = GridOccupancy.GetAt(cell);
        if (obj == null) return null;
        return obj.GetComponent<ConveyorBelt>();
    }

    static BuildingBase FindBuildingInCell(Vector2Int cell)
    {
        GameObject obj = GridOccupancy.GetAt(cell);
        if (obj == null) return null;
        return obj.GetComponent<BuildingBase>();
    }

    static void Log(string message)
    {
        if (showDebug)
            Debug.Log(message);
    }
}
