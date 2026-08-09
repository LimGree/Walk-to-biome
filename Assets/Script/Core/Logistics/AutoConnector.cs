using UnityEngine;
using System.Collections.Generic;

public static class AutoConnector
{
    // 4 направления по сетке (в мировых координатах)
    private static readonly Vector2Int[] NeighborOffsets =
    {
        new Vector2Int(0, 1),   // North (+Z)
        new Vector2Int(1, 0),   // East  (+X)
        new Vector2Int(0, -1),  // South (-Z)
        new Vector2Int(-1, 0)   // West  (-X)
    };

    /// <summary>
    /// Вызывать после установки здания или конвейера.
    /// </summary>
    public static void TryAutoConnect(GameObject placedObject)
    {
        if (placedObject == null || GridSystem.Instance == null)
            return;

        // Конвейер
        ConveyorBelt belt = placedObject.GetComponent<ConveyorBelt>();
        if (belt != null)
        {
            ConnectConveyorAndNeighbors(belt);
            return;
        }

        // Здание
        BuildingBase building = placedObject.GetComponent<BuildingBase>();
        if (building != null)
        {
            ConnectBuildingAndNeighbors(building);
        }
    }

    /// <summary>
    /// Вызывать при повороте здания/ленты (клавиша R).
    /// </summary>
    public static void OnRotated(GameObject rotatedObject)
    {
        // Просто переподключаем, как будто только что поставили
        TryAutoConnect(rotatedObject);
    }

    // =========================================================
    // CONVEYOR
    // =========================================================

    private static void ConnectConveyorAndNeighbors(ConveyorBelt belt)
    {
        if (belt == null || belt.startPoint == null || belt.endPoint == null)
            return;

        Vector2Int cell = GridSystem.Instance.WorldToCell(belt.transform.position);

        // Просто пытаемся соединиться с 4 соседями (без очистки)
        foreach (var offset in NeighborOffsets)
        {
            Vector2Int neighborCell = cell + offset;
            TryConnectBeltToNeighbor(belt, neighborCell, offset);
        }

        // Просим соседей тоже попробовать соединиться с нами
        RefreshNeighborsSoft(cell);
    }

    private static void TryConnectBeltToNeighbor(ConveyorBelt belt, Vector2Int neighborCell, Vector2Int offset)
    {
        ConveyorBelt otherBelt = FindBeltInCell(neighborCell);
        BuildingBase building = FindBuildingInCell(neighborCell);

        Vector3 beltDir = GetBeltDirection(belt);

        // ---------- Лента → Лента ----------
        if (otherBelt != null)
        {
            Vector3 otherDir = GetBeltDirection(otherBelt);

            // Мы смотрим на соседа и направления совпадают → мы next для него? нет:
            // Если наш end смотрит на start соседа
            if (IsDirectionMatch(beltDir, offset) && IsDirectionMatch(otherDir, offset))
            {
                if (belt.nextBelt == null)
                {
                    belt.nextBelt = otherBelt;
                    Debug.Log($"[GridConnect] {belt.name} → {otherBelt.name}");
                }
            }
            // Сосед смотрит на нас
            else if (IsDirectionMatch(otherDir, -offset) && IsDirectionMatch(beltDir, -offset) == false)
            {
                if (otherBelt.nextBelt == null)
                {
                    otherBelt.nextBelt = belt;
                    Debug.Log($"[GridConnect] {otherBelt.name} → {belt.name}");
                }
            }
        }

        // ---------- Лента ↔ Здание ----------
        if (building != null)
        {
            // Случай 1: лента отдаёт в Input здания (мы смотрим на здание)
            if (IsDirectionMatch(beltDir, offset))
            {
                BuildingSocket input = FindSocketInDirection(building, SocketType.Input, -offset);
                if (input != null)
                {
                    ConnectBeltToInput(belt, input);
                }
            }
            // Случай 2: Output здания отдаёт на ленту (здание смотрит на нас)
            else
            {
                BuildingSocket output = FindSocketInDirection(building, SocketType.Output, offset);
                if (output != null)
                {
                    ConnectOutputToBelt(output, belt);
                }
            }
        }
    }

    // =========================================================
    // BUILDING
    // =========================================================

    private static void ConnectBuildingAndNeighbors(BuildingBase building)
    {
        if (building == null)
            return;

        Vector2Int cell = GridSystem.Instance.WorldToCell(building.transform.position);

        foreach (var offset in NeighborOffsets)
        {
            Vector2Int neighborCell = cell + offset;
            TryConnectBuildingToNeighbor(building, neighborCell, offset);
        }

        RefreshNeighborsSoft(cell);
    }

    private static void RefreshNeighborsSoft(Vector2Int cell)
    {
        foreach (var offset in NeighborOffsets)
        {
            Vector2Int neighborCell = cell + offset;

            ConveyorBelt belt = FindBeltInCell(neighborCell);
            if (belt != null)
            {
                foreach (var off in NeighborOffsets)
                    TryConnectBeltToNeighbor(belt, neighborCell + off, off);
            }

            BuildingBase building = FindBuildingInCell(neighborCell);
            if (building != null)
            {
                foreach (var off in NeighborOffsets)
                    TryConnectBuildingToNeighbor(building, neighborCell + off, off);
            }
        }
    }

    private static void TryConnectBuildingToNeighbor(BuildingBase building, Vector2Int neighborCell, Vector2Int offset)
    {
        ConveyorBelt belt = FindBeltInCell(neighborCell);
        if (belt == null) return;

        Vector3 beltDir = GetBeltDirection(belt);

        // Output здания → лента
        BuildingSocket output = FindSocketInDirection(building, SocketType.Output, offset);
        if (output != null && IsDirectionMatch(beltDir, offset))
        {
            ConnectOutputToBelt(output, belt);
        }

        // Лента → Input здания
        // Лента должна смотреть на здание (противоположное offset)
        if (IsDirectionMatch(beltDir, -offset))
        {
            BuildingSocket input = FindSocketInDirection(building, SocketType.Input, offset);
            if (input != null)
            {
                ConnectBeltToInput(belt, input);
            }
        }
    }

    // =========================================================
    // ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ
    // =========================================================

    //private static void RefreshNeighbors(Vector2Int cell)
    //{
    //    foreach (var offset in NeighborOffsets)
    //    {
    //        Vector2Int neighborCell = cell + offset;

    //        ConveyorBelt belt = FindBeltInCell(neighborCell);
    //        if (belt != null)
    //        {
    //            // Переподключаем соседа (без рекурсии глубже)
    //            ClearBeltConnections(belt);
    //            foreach (var off in NeighborOffsets)
    //                TryConnectBeltToNeighbor(belt, neighborCell + off, off);
    //        }

    //        BuildingBase building = FindBuildingInCell(neighborCell);
    //        if (building != null)
    //        {
    //            ClearBuildingConnections(building);
    //            foreach (var off in NeighborOffsets)
    //                TryConnectBuildingToNeighbor(building, neighborCell + off, off);
    //        }
    //    }
    //}

    private static Vector3 GetBeltDirection(ConveyorBelt belt)
    {
        if (belt.startPoint == null || belt.endPoint == null)
            return belt.transform.forward;

        return (belt.endPoint.position - belt.startPoint.position).normalized;
    }

    private static bool IsDirectionMatch(Vector3 direction, Vector2Int offset)
    {
        Vector3 target = new Vector3(offset.x, 0f, offset.y).normalized;
        return Vector3.Dot(direction.normalized, target) > 0.7f;
    }

    private static BuildingSocket FindSocketInDirection(BuildingBase building, SocketType type, Vector2Int offset)
    {
        BuildingSocket[] sockets = type == SocketType.Input ? building.inputSockets : building.outputSockets;
        if (sockets == null) return null;

        Vector3 desiredDir = new Vector3(offset.x, 0f, offset.y).normalized;

        BuildingSocket best = null;
        float bestDot = 0.5f;

        foreach (var socket in sockets)
        {
            if (socket == null) continue;
            float dot = Vector3.Dot(socket.transform.forward.normalized, desiredDir);
            if (dot > bestDot)
            {
                bestDot = dot;
                best = socket;
            }
        }

        return best;
    }

    // --- Поиск объектов в клетке ---

    private static ConveyorBelt FindBeltInCell(Vector2Int cell)
    {
        ConveyorBelt[] belts = Object.FindObjectsByType<ConveyorBelt>(FindObjectsSortMode.None);
        foreach (var belt in belts)
        {
            if (belt == null) continue;
            Vector2Int beltCell = GridSystem.Instance.WorldToCell(belt.transform.position);
            if (beltCell == cell) return belt;
        }
        return null;
    }

    private static BuildingBase FindBuildingInCell(Vector2Int cell)
    {
        BuildingBase[] buildings = Object.FindObjectsByType<BuildingBase>(FindObjectsSortMode.None);
        foreach (var b in buildings)
        {
            if (b == null) continue;
            Vector2Int bCell = GridSystem.Instance.WorldToCell(b.transform.position);
            if (bCell == cell) return b;
        }
        return null;
    }

    // --- Реальные соединения ---

    private static void ConnectOutputToBelt(BuildingSocket output, ConveyorBelt belt)
    {
        if (output == null || belt == null) return;
        if (output.connectedBelt != null && output.connectedBelt != belt) return;
        if (belt.connectedOutputSocket != null && belt.connectedOutputSocket != output) return;

        output.ConnectBelt(belt);
        belt.connectedOutputSocket = output;
        Debug.Log($"[GridConnect] OUTPUT {output.name} → {belt.name}");
    }

    private static void ConnectBeltToInput(ConveyorBelt belt, BuildingSocket input)
    {
        if (belt == null || input == null) return;
        if (input.connectedBelt != null && input.connectedBelt != belt) return;
        if (belt.connectedInputSocket != null && belt.connectedInputSocket != input) return;

        input.ConnectBelt(belt);
        belt.connectedInputSocket = input;
        Debug.Log($"[GridConnect] {belt.name} → INPUT {input.name}");
    }
}