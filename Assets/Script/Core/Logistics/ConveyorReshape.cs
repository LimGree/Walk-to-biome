using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Reshape соседей: Straight → Corner когда поток должен повернуть (промт §5).
/// Reconnect вызывается ОДИН раз в конце корневого вызова.
/// </summary>
public static class ConveyorReshape
{
    public static bool showDebug = false;
    public static BuildingData DefaultConveyorData { get; set; }

    private static readonly Vector2Int[] NeighborOffsets =
    {
        new Vector2Int(0, 1),
        new Vector2Int(1, 0),
        new Vector2Int(0, -1),
        new Vector2Int(-1, 0)
    };

    private static int reshapeDepth;
    private const int MaxReshapeDepth = 4;
    private static readonly HashSet<Vector2Int> reshapedCellsThisCall = new HashSet<Vector2Int>();

    /// <summary>
    /// После Place / Rotate: обойти соседей и перестроить Straight→Corner.
    /// В конце — один ReconnectObject(placed).
    /// </summary>
    public static void TryReshapeNeighborsAfterPlace(GameObject placedObject)
    {
        if (placedObject == null || GridSystem.Instance == null)
            return;

        if (reshapeDepth >= MaxReshapeDepth)
            return;

        reshapeDepth++;
        bool rootCall = reshapeDepth == 1;
        if (rootCall)
            reshapedCellsThisCall.Clear();

        try
        {
            List<Vector2Int> occupied = GetOccupiedCells(placedObject);
            if (occupied.Count == 0)
                return;

            var candidateBelts = new List<ConveyorBelt>();
            var seen = new HashSet<int>();

            for (int i = 0; i < occupied.Count; i++)
            {
                Vector2Int cell = occupied[i];
                for (int n = 0; n < NeighborOffsets.Length; n++)
                {
                    Vector2Int neighborCell = cell + NeighborOffsets[n];
                    if (occupied.Contains(neighborCell))
                        continue;

                    GameObject obj = GridOccupancy.GetAt(neighborCell);
                    if (obj == null) continue;

                    ConveyorBelt belt = obj.GetComponent<ConveyorBelt>();
                    if (belt == null || belt.isCorner)
                        continue;

                    if (!seen.Add(belt.GetInstanceID()))
                        continue;

                    candidateBelts.Add(belt);
                }
            }

            for (int i = 0; i < candidateBelts.Count; i++)
            {
                if (candidateBelts[i] != null)
                    TryReshapeStraightBelt(candidateBelts[i], placedObject);
            }

            // Один reconnect на корневом вызове
            if (rootCall && placedObject != null)
                AutoConnector.ReconnectObject(placedObject);
        }
        finally
        {
            reshapeDepth--;
        }
    }

    public static ConveyorBelt TryReshapeStraightBelt(ConveyorBelt belt, GameObject reasonPlaced)
    {
        if (belt == null || belt.isCorner || GridSystem.Instance == null)
            return null;

        Vector2Int cell = GridSystem.Instance.WorldToCell(belt.transform.position);
        if (reshapedCellsThisCall.Contains(cell))
            return null;

        BuildingData data = ResolveConveyorData(belt);
        if (data == null || data.cornerPrefab == null)
            return null;

        float preferredYaw = GetPreferredYaw(belt);
        Vector2Int fwd = ConveyorPlacement.WorldToCardinal(ConveyorPlacement.YawToForward(preferredYaw));

        // Прямая цепочка вперёд важнее — не ломаем (§7.6)
        if (ConveyorPlacement.NeighborCanAcceptFromUs(cell, fwd))
            return null;

        ConveyorPlacement.Result resolved;
        bool forcedSideTurn = false;

        if (TryBuildCornerTowardReason(data, cell, preferredYaw, fwd, reasonPlaced, out resolved))
        {
            forcedSideTurn = true;
        }
        else
        {
            resolved = ConveyorPlacement.ResolveForReshape(data, cell, preferredYaw);
            if (!resolved.isCorner || resolved.prefab == null)
                return null;

            // Reshape только если exit угла целится в reason (если reason задан)
            if (reasonPlaced != null && !ExitTargetsObject(resolved, cell, reasonPlaced))
                return null;
        }

        if (!resolved.isCorner || resolved.prefab == null)
            return null;

        ConveyorBelt newCorner = PerformReshape(belt, data, cell, resolved);
        if (newCorner == null)
            return null;

        // Выровнять поставленную прямую ленту под exit угла (без reconnect — он в корне)
        if (reasonPlaced != null)
            AlignPlacedBeltToCornerExit(reasonPlaced, newCorner);

        if (forcedSideTurn)
            Log($"[Reshape] Side-turn toward {reasonPlaced?.name}");

        return newCorner;
    }

    static bool TryBuildCornerTowardReason(
        BuildingData data,
        Vector2Int beltCell,
        float preferredYaw,
        Vector2Int flowFwd,
        GameObject reasonPlaced,
        out ConveyorPlacement.Result resolved)
    {
        resolved = default;
        if (reasonPlaced == null)
            return false;

        ConveyorBelt reasonBelt = reasonPlaced.GetComponent<ConveyorBelt>();
        // Не форсим угол ради другого угла
        if (reasonBelt != null && reasonBelt.isCorner)
            return false;

        List<Vector2Int> reasonCells = GetOccupiedCells(reasonPlaced);
        Vector2Int? sideOffset = null;

        for (int i = 0; i < reasonCells.Count; i++)
        {
            Vector2Int off = reasonCells[i] - beltCell;
            int manh = Mathf.Abs(off.x) + Mathf.Abs(off.y);
            if (manh != 1)
                continue;

            if (off == flowFwd || off == -flowFwd)
                return false; // торец, не бок

            sideOffset = off;
            break;
        }

        if (sideOffset == null)
            return false;

        if (!IsValidSideTurnTarget(reasonPlaced, beltCell, sideOffset.Value))
            return false;

        resolved = ConveyorPlacement.BuildCornerResultPublic(
            data,
            entryTravel: ConveyorPlacement.CardinalToWorld(flowFwd),
            exitTravel: ConveyorPlacement.CardinalToWorld(sideOffset.Value),
            preferredYaw);

        return resolved.isCorner && resolved.prefab != null;
    }

    /// <summary>
    /// Боковой reason: здание с Input на ленту, ИЛИ лента (только что поставленная — выровняем).
    /// Не для «любого» здания без Input.
    /// </summary>
    static bool IsValidSideTurnTarget(GameObject reason, Vector2Int beltCell, Vector2Int offsetToReason)
    {
        if (reason == null) return false;

        // Лента сбоку — по §2.4 допускаем (align подстроит направление)
        if (reason.GetComponent<ConveyorBelt>() != null)
            return true;

        BuildingBase building = reason.GetComponent<BuildingBase>();
        if (building != null)
        {
            // Input должен смотреть на ленту
            return ConveyorPlacement.NeighborCanAcceptFromUs(beltCell, offsetToReason);
        }

        return false;
    }

    public static void AlignPlacedBeltToCornerExit(GameObject placed, ConveyorBelt corner)
    {
        if (placed == null || corner == null) return;

        ConveyorBelt belt = placed.GetComponent<ConveyorBelt>();
        if (belt == null || belt.isCorner) return;

        AlignStraightBeltFlow(belt, corner.GetExitDirection());
    }

    /// <summary>Повернуть Straight без reconnect (caller reconnect'ит).</summary>
    public static void AlignStraightBeltFlow(ConveyorBelt belt, Vector3 flowDir)
    {
        if (belt == null || belt.isCorner) return;

        flowDir.y = 0f;
        if (flowDir.sqrMagnitude < 0.0001f) return;
        flowDir.Normalize();

        if (Vector3.Dot(belt.GetEntryDirection(), flowDir) > 0.85f)
            return;

        float yaw = Mathf.Atan2(flowDir.x, flowDir.z) * Mathf.Rad2Deg;
        yaw = ConveyorPlacement.NormalizeYaw(yaw);
        belt.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        Vector3 scale = belt.transform.localScale;
        scale.x = Mathf.Abs(scale.x);
        belt.transform.localScale = scale;

        Log($"[Reshape] Aligned {belt.name} yaw={yaw}");
    }

    static ConveyorBelt PerformReshape(
        ConveyorBelt oldBelt,
        BuildingData data,
        Vector2Int cell,
        ConveyorPlacement.Result resolved)
    {
        reshapedCellsThisCall.Add(cell);

        Vector3 worldPos = oldBelt.transform.position;
        if (GridSystem.Instance != null)
        {
            Vector3 center = GridSystem.Instance.GetCellCenter(cell, worldPos.y);
            worldPos = new Vector3(center.x, worldPos.y, center.z);
        }

        List<ConveyorBelt.ItemSnapshot> items = oldBelt.CaptureItemSnapshots();
        BuildingData savedData = oldBelt.buildingData != null ? oldBelt.buildingData : data;

        AutoConnector.ClearBeltLinks(oldBelt);
        oldBelt.OnRemoved();
        oldBelt.ClearItems();
        oldBelt.suppressDestroyCleanup = true;
        Object.Destroy(oldBelt.gameObject);

        Quaternion rot = Quaternion.Euler(0f, resolved.rotationY, 0f);
        GameObject go = Object.Instantiate(resolved.prefab, worldPos, rot);
        go.name = resolved.prefab.name + "_reshaped";

        if (resolved.mirrorX)
        {
            Vector3 scale = go.transform.localScale;
            scale.x = -Mathf.Abs(scale.x);
            go.transform.localScale = scale;
        }

        ConveyorBelt newBelt = go.GetComponent<ConveyorBelt>();
        if (newBelt == null)
        {
            Debug.LogError("[Reshape] cornerPrefab has no ConveyorBelt");
            Object.Destroy(go);
            return null;
        }

        newBelt.buildingData = savedData;
        newBelt.isCorner = true;
        newBelt.OnPlaced(); // только Register
        newBelt.RestoreItemSnapshots(items);

        Log($"[Reshape] {cell} Straight→Corner yaw={resolved.rotationY} mirror={resolved.mirrorX}");
        return newBelt;
    }

    static BuildingData ResolveConveyorData(ConveyorBelt belt)
    {
        if (belt != null && belt.buildingData != null)
            return belt.buildingData;

        if (DefaultConveyorData != null && DefaultConveyorData.cornerPrefab != null)
            return DefaultConveyorData;

        PlayerInventory inv = Object.FindFirstObjectByType<PlayerInventory>();
        if (inv != null && inv.allBuildings != null)
        {
            for (int i = 0; i < inv.allBuildings.Length; i++)
            {
                BuildingData b = inv.allBuildings[i];
                if (b != null && b.IsConveyor && b.cornerPrefab != null)
                {
                    DefaultConveyorData = b;
                    return b;
                }
            }
        }

        return DefaultConveyorData;
    }

    static float GetPreferredYaw(ConveyorBelt belt)
    {
        Vector3 entry = belt.GetEntryDirection();
        entry.y = 0f;
        if (entry.sqrMagnitude < 0.0001f)
            entry = belt.transform.forward;
        entry.y = 0f;
        if (entry.sqrMagnitude < 0.0001f)
            entry = Vector3.forward;

        float yaw = Mathf.Atan2(entry.x, entry.z) * Mathf.Rad2Deg;
        return ConveyorPlacement.NormalizeYaw(yaw);
    }

    public static Vector3 GetExitDirectionFromResult(ConveyorPlacement.Result r)
    {
        Vector3 entry = ConveyorPlacement.YawToForward(r.rotationY);
        if (!r.isCorner)
            return entry;

        Vector3 right = Vector3.Cross(Vector3.up, entry);
        if (right.sqrMagnitude < 0.0001f)
            right = Vector3.right;
        else
            right.Normalize();

        return r.mirrorX ? -right : right;
    }

    static bool ExitTargetsObject(ConveyorPlacement.Result r, Vector2Int beltCell, GameObject target)
    {
        if (target == null) return false;

        Vector2Int exitOff = ConveyorPlacement.WorldToCardinal(GetExitDirectionFromResult(r));
        Vector2Int exitCell = beltCell + exitOff;

        List<Vector2Int> targetCells = GetOccupiedCells(target);
        for (int i = 0; i < targetCells.Count; i++)
        {
            if (targetCells[i] == exitCell)
                return true;
        }

        GameObject at = GridOccupancy.GetAt(exitCell);
        return at != null && (at == target
            || at.transform.IsChildOf(target.transform)
            || target.transform.IsChildOf(at.transform));
    }

    public static List<Vector2Int> GetOccupiedCells(GameObject obj)
    {
        var cells = new List<Vector2Int>();
        if (obj == null || GridSystem.Instance == null)
            return cells;

        Vector2Int origin = GridSystem.Instance.WorldToCell(obj.transform.position);

        BuildingBase building = obj.GetComponent<BuildingBase>();
        if (building != null)
        {
            Vector2Int size = building.data != null ? building.data.size : Vector2Int.one;
            size = GridOccupancy.GetRotatedSize(size, obj.transform.eulerAngles.y);
            size = GridOccupancy.NormalizeSize(size);
            for (int x = 0; x < size.x; x++)
            {
                for (int y = 0; y < size.y; y++)
                    cells.Add(origin + new Vector2Int(x, y));
            }
            return cells;
        }

        cells.Add(origin);
        return cells;
    }

    static void Log(string msg)
    {
        if (showDebug)
            Debug.Log(msg);
    }
}
