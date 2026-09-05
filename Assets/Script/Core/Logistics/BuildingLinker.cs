using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Авто-связи зданий и конвейеров по сетке.
/// Никаких графов и пересоздания объектов — только соседние клетки.
/// </summary>
public static class BuildingLinker
{
    static readonly Vector2Int[] Cardinals =
    {
        new Vector2Int(0, 1),
        new Vector2Int(1, 0),
        new Vector2Int(0, -1),
        new Vector2Int(-1, 0)
    };

    static readonly List<Vector2Int> CellBuffer = new List<Vector2Int>(16);
    static readonly List<BuildingBase> NeighborBuffer = new List<BuildingBase>(16);
    static readonly List<BuildingBase> AllBuffer = new List<BuildingBase>(256);

    public static bool SuppressRelink;

    public static Vector2Int ToCardinal(Vector3 worldDir)
    {
        if (Mathf.Abs(worldDir.x) >= Mathf.Abs(worldDir.z))
            return new Vector2Int(worldDir.x >= 0f ? 1 : -1, 0);
        return new Vector2Int(0, worldDir.z >= 0f ? 1 : -1);
    }

    public static Vector3 CardinalToWorld(Vector2Int dir)
    {
        return new Vector3(dir.x, 0f, dir.y);
    }

    public static Vector2Int WorldToCell(Vector3 world)
    {
        if (GridSystem.Instance != null)
            return GridSystem.Instance.WorldToCell(world);
        return new Vector2Int(Mathf.RoundToInt(world.x), Mathf.RoundToInt(world.z));
    }

    public static BuildingBase GetBuildingAt(Vector2Int cell)
    {
        GameObject obj = GridOccupancy.GetAt(cell);
        return obj != null ? obj.GetComponent<BuildingBase>() : null;
    }

    public static Vector2Int GetSocketFrontCell(BuildingSocket socket)
    {
        if (socket == null)
            return Vector2Int.zero;

        float cell = GridFootprint.CellSize;
        Vector3 outward = socket.GetOutward();
        Vector3 probe = socket.transform.position + outward * (cell * 0.55f);
        return WorldToCell(probe);
    }

    public static void RelinkAround(BuildingBase building)
    {
        if (building == null)
            return;

        CollectSelfAndNeighbors(building, NeighborBuffer);
        Relink(NeighborBuffer);
    }

    static readonly List<GameObject> OccupantBuffer = new List<GameObject>(256);

    public static void CollectPlaced(List<BuildingBase> results)
    {
        if (results == null)
            return;
        results.Clear();
        GridOccupancy.CollectOccupants(OccupantBuffer);
        for (int i = 0; i < OccupantBuffer.Count; i++)
        {
            GameObject go = OccupantBuffer[i];
            if (go == null)
                continue;
            BuildingBase building = go.GetComponent<BuildingBase>();
            if (building != null)
                results.Add(building);
        }
    }

    public static void RelinkAll()
    {
        CollectPlaced(AllBuffer);
        Relink(AllBuffer);
    }

    public static void Relink(List<BuildingBase> buildings)
    {
        if (buildings == null)
            return;

        for (int i = 0; i < buildings.Count; i++)
        {
            Conveyor belt = buildings[i] as Conveyor;
            if (belt != null)
                belt.RefreshShape();
        }

        for (int i = 0; i < buildings.Count; i++)
            RelinkOutputs(buildings[i]);
    }

    public static void CollectNeighbors(BuildingBase building, List<BuildingBase> results)
    {
        if (results == null)
            return;

        results.Clear();
        if (building == null)
            return;

        CollectAdjacent(building, results, includeSelf: false);
    }

    static void CollectSelfAndNeighbors(BuildingBase building, List<BuildingBase> results)
    {
        results.Clear();
        if (building == null)
            return;

        CollectAdjacent(building, results, includeSelf: true);
    }

    static void CollectAdjacent(BuildingBase building, List<BuildingBase> results, bool includeSelf)
    {
        if (includeSelf)
            results.Add(building);

        GridFootprint.CollectCells(building.transform.position, building.FootprintSize, CellBuffer);
        for (int i = 0; i < CellBuffer.Count; i++)
        {
            Vector2Int cell = CellBuffer[i];
            for (int d = 0; d < Cardinals.Length; d++)
            {
                BuildingBase other = GetBuildingAt(cell + Cardinals[d]);
                if (other == null || other == building)
                    continue;
                if (!results.Contains(other))
                    results.Add(other);
            }
        }
    }

    static void RelinkOutputs(BuildingBase building)
    {
        if (building == null || building.outputSockets == null)
            return;

        for (int i = 0; i < building.outputSockets.Length; i++)
        {
            BuildingSocket output = building.outputSockets[i];
            if (output == null)
                continue;

            output.DisconnectSocket();

            BuildingBase target = FindOutputTarget(building, output);
            if (target == null)
                continue;

            BuildingSocket input = FindAcceptingInput(target, building, output);
            if (input != null)
                output.ConnectSocket(input);
        }
    }

    static BuildingBase FindOutputTarget(BuildingBase building, BuildingSocket output)
    {
        BuildingBase front = GetBuildingAt(GetSocketFrontCell(output));
        if (front != null && front != building && !(front is RoboticArm))
            return front;

        Vector2Int outward = ToCardinal(output.GetOutward());
        GridFootprint.CollectCells(building.transform.position, building.FootprintSize, CellBuffer);
        for (int i = 0; i < CellBuffer.Count; i++)
        {
            BuildingBase other = GetBuildingAt(CellBuffer[i] + outward);
            if (other != null && other != building && !(other is RoboticArm))
                return other;
        }

        return null;
    }

    static BuildingSocket FindAcceptingInput(BuildingBase target, BuildingBase from, BuildingSocket fromOutput)
    {
        Conveyor belt = target as Conveyor;
        if (belt != null)
        {
            if (!belt.IsFedBy(from))
                return null;
            BuildingSocket beltInput = belt.GetInputFrom(from);
            return beltInput != null ? beltInput : belt.InputSocket;
        }

        Splitter splitter = target as Splitter;
        if (splitter != null)
        {
            if (!splitter.IsFedBy(from))
                return null;
            return splitter.InputSocket;
        }

        if (target.inputSockets == null)
            return null;

        for (int i = 0; i < target.inputSockets.Length; i++)
        {
            BuildingSocket input = target.inputSockets[i];
            if (input == null)
                continue;
            if (input.connectedSocket != null && input.connectedSocket != fromOutput)
                continue;

            Vector2Int inputFront = GetSocketFrontCell(input);
            if (OccupiesCell(from, inputFront))
                return input;
        }

        return null;
    }

    public static bool HasInputFrom(BuildingBase target, BuildingBase source)
    {
        if (target == null || source == null || target == source)
            return false;

        Conveyor belt = target as Conveyor;
        if (belt != null)
            return belt.IsFedBy(source);

        Splitter splitter = target as Splitter;
        if (splitter != null)
            return splitter.IsFedBy(source);

        if (target.inputSockets == null || target.inputSockets.Length == 0)
            return false;

        for (int i = 0; i < target.inputSockets.Length; i++)
        {
            BuildingSocket input = target.inputSockets[i];
            if (input == null)
                continue;

            if (OccupiesCell(source, GetSocketFrontCell(input)))
                return true;
        }

        return false;
    }

    static void CollectCellsSafe(BuildingBase building)
    {
        if (building == null)
        {
            CellBuffer.Clear();
            return;
        }

        GridFootprint.CollectCells(building.transform.position, building.FootprintSize, CellBuffer);
    }

    public static bool OccupiesCell(BuildingBase building, Vector2Int cell)
    {
        if (building == null)
            return false;

        var cells = new List<Vector2Int>(8);
        GridFootprint.CollectCells(building.transform.position, building.FootprintSize, cells);
        for (int i = 0; i < cells.Count; i++)
        {
            if (cells[i] == cell)
                return true;
        }

        return false;
    }

    public static bool HasOutputToward(BuildingBase building, Vector2Int targetCell)
    {
        if (building == null)
            return false;

        Conveyor belt = building as Conveyor;
        if (belt != null)
            return belt.Cell + belt.ExitDir == targetCell;

        if (building.outputSockets == null)
            return false;

        for (int i = 0; i < building.outputSockets.Length; i++)
        {
            BuildingSocket output = building.outputSockets[i];
            if (output == null)
                continue;

            if (GetSocketFrontCell(output) == targetCell)
                return true;

            Vector2Int outward = ToCardinal(output.GetOutward());
            if (IsAdjacentTo(building, targetCell)
                && OccupiesCell(building, targetCell - outward))
                return true;

            CollectCellsSafe(building);
            for (int c = 0; c < CellBuffer.Count; c++)
            {
                if (CellBuffer[c] + outward == targetCell)
                    return true;
            }
        }

        return false;
    }

    public static bool IsAdjacentTo(BuildingBase building, Vector2Int cell)
    {
        if (building == null)
            return false;

        GridFootprint.CollectCells(building.transform.position, building.FootprintSize, CellBuffer);
        for (int i = 0; i < CellBuffer.Count; i++)
        {
            Vector2Int delta = cell - CellBuffer[i];
            if (Mathf.Abs(delta.x) + Mathf.Abs(delta.y) == 1)
                return true;
        }

        return false;
    }
}
