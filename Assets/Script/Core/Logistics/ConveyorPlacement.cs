using UnityEngine;

/// <summary>
/// Выбор прямой / угловой ленты и ориентации при установке одного «Conveyor».
/// </summary>
public static class ConveyorPlacement
{
    public struct Result
    {
        public bool isCorner;
        public float rotationY;
        public bool mirrorX; // left-turn на right-turn префабе
        public GameObject prefab;
    }

    /// <summary>
    /// Решает форму и yaw для клетки <paramref name="cell"/> при preferredRotationY (ghost / R).
    /// </summary>
    public static Result Resolve(BuildingData data, Vector2Int cell, float preferredRotationY)
    {
        Vector3 forward = YawToForward(preferredRotationY);
        Vector2Int fwd = WorldToCardinal(forward);
        Vector2Int back = -fwd;
        Vector2Int left = new Vector2Int(-fwd.y, fwd.x);   // left of forward on XZ
        Vector2Int right = new Vector2Int(fwd.y, -fwd.x);

        bool hasBackInput = NeighborCanFeedUs(cell, back);
        bool frontFree = GridOccupancy.IsCellFree(cell + fwd);
        bool hasFrontAccept = NeighborCanAcceptFromUs(cell, fwd);
        bool hasLeftOut = NeighborCanAcceptFromUs(cell, left);
        bool hasRightOut = NeighborCanAcceptFromUs(cell, right);

        bool wantCorner = false;
        Vector2Int exitDir = fwd;

        // Приоритет (практичный + близкий к ТЗ):
        // 1) Вход сзади + явное продолжение СПЕРЕДИ → Straight
        // 2) Вход сзади + цель СБОКУ (и нет accept спереди) → Corner
        // 3) Вход сзади + спереди свободно/нет цели → Straight
        // 4) Вход сбоку + выход вперёд → Corner
        // 5) Иначе Straight
        if (hasBackInput && hasFrontAccept)
        {
            wantCorner = false;
        }
        else if (hasBackInput && (hasLeftOut || hasRightOut))
        {
            wantCorner = true;
            if (hasRightOut && !hasLeftOut)
                exitDir = right;
            else if (hasLeftOut && !hasRightOut)
                exitDir = left;
            else
                exitDir = right;
        }
        else if (hasBackInput && (frontFree || !hasLeftOut && !hasRightOut))
        {
            wantCorner = false;
        }
        else if (!hasBackInput)
        {
            if (NeighborCanFeedUs(cell, left) && (hasFrontAccept || frontFree))
            {
                Result r = BuildCornerResult(
                    data,
                    entryTravel: CardinalToWorld(-left),
                    exitTravel: CardinalToWorld(fwd),
                    preferredRotationY);
                if (r.prefab != null)
                    return r;
            }

            if (NeighborCanFeedUs(cell, right) && (hasFrontAccept || frontFree))
            {
                Result r = BuildCornerResult(
                    data,
                    entryTravel: CardinalToWorld(-right),
                    exitTravel: CardinalToWorld(fwd),
                    preferredRotationY);
                if (r.prefab != null)
                    return r;
            }

            wantCorner = false;
        }

        if (wantCorner)
        {
            Vector3 entryTravel = CardinalToWorld(fwd); // поток сзади → вперёд
            Vector3 exitTravel = CardinalToWorld(exitDir);
            Result corner = BuildCornerResult(data, entryTravel, exitTravel, preferredRotationY);
            if (corner.prefab != null)
                return corner;
        }

        return BuildStraightResult(data, preferredRotationY);
    }

    /// <summary>
    /// Для reshape уже стоящей ленты: учитывает текущий yaw потока.
    /// Может выбрать Corner при боковом accept даже без источника сзади
    /// (у ленты уже есть entry direction).
    /// </summary>
    public static Result ResolveForReshape(BuildingData data, Vector2Int cell, float preferredRotationY)
    {
        Result primary = Resolve(data, cell, preferredRotationY);
        if (primary.isCorner)
            return primary;

        Vector3 forward = YawToForward(preferredRotationY);
        Vector2Int fwd = WorldToCardinal(forward);
        Vector2Int left = new Vector2Int(-fwd.y, fwd.x);
        Vector2Int right = new Vector2Int(fwd.y, -fwd.x);

        bool hasFrontAccept = NeighborCanAcceptFromUs(cell, fwd);
        bool hasLeftOut = NeighborCanAcceptFromUs(cell, left);
        bool hasRightOut = NeighborCanAcceptFromUs(cell, right);

        // Прямая цепочка вперёд важнее — не ломаем
        if (hasFrontAccept)
            return primary;

        if (hasLeftOut || hasRightOut)
        {
            Vector2Int exitDir = hasRightOut && !hasLeftOut ? right
                : hasLeftOut && !hasRightOut ? left
                : right;

            Result corner = BuildCornerResult(
                data,
                entryTravel: CardinalToWorld(fwd),
                exitTravel: CardinalToWorld(exitDir),
                preferredRotationY);

            if (corner.isCorner && corner.prefab != null)
                return corner;
        }

        return primary;
    }

    static Result BuildStraightResult(BuildingData data, float rotationY)
    {
        GameObject prefab = null;
        if (data != null)
            prefab = data.GetConveyorPrefab(isCorner: false);

        return new Result
        {
            isCorner = false,
            rotationY = NormalizeYaw(rotationY),
            mirrorX = false,
            prefab = prefab
        };
    }

    /// <summary>
    /// Corner prefab convention (local): entry along +Z (forward), exit along +X (right).
    /// Left turn → mirrorX.
    /// </summary>
    public static Result BuildCornerResultPublic(BuildingData data, Vector3 entryTravel, Vector3 exitTravel, float fallbackYaw)
    {
        return BuildCornerResult(data, entryTravel, exitTravel, fallbackYaw);
    }

    static Result BuildCornerResult(BuildingData data, Vector3 entryTravel, Vector3 exitTravel, float fallbackYaw)
    {
        entryTravel.y = 0f;
        exitTravel.y = 0f;
        if (entryTravel.sqrMagnitude < 0.0001f)
            entryTravel = YawToForward(fallbackYaw);
        if (exitTravel.sqrMagnitude < 0.0001f)
            exitTravel = Quaternion.Euler(0f, 90f, 0f) * entryTravel;

        entryTravel.Normalize();
        exitTravel.Normalize();

        // cross.y > 0 → exit is to the right of entry (Unity left-handed: forward×right = up)
        float crossY = Vector3.Cross(entryTravel, exitTravel).y;
        bool rightTurn = crossY >= 0f;

        float yaw = Mathf.Atan2(entryTravel.x, entryTravel.z) * Mathf.Rad2Deg;

        GameObject prefab = data != null ? data.GetConveyorPrefab(isCorner: true) : null;
        if (prefab == null && data != null)
            prefab = data.GetConveyorPrefab(isCorner: false);

        return new Result
        {
            isCorner = prefab != null && data != null && data.cornerPrefab != null,
            rotationY = NormalizeYaw(yaw),
            mirrorX = !rightTurn,
            prefab = prefab
        };
    }

    // ------------------------------------------------------------------
    // Neighbor queries
    // ------------------------------------------------------------------

    /// <param name="fromNeighborOffset">Направление от нашей клетки к соседу-источнику.</param>
    public static bool NeighborCanFeedUs(Vector2Int ourCell, Vector2Int fromNeighborOffset)
    {
        GameObject obj = GridOccupancy.GetAt(ourCell + fromNeighborOffset);
        if (obj == null) return false;

        ConveyorBelt belt = obj.GetComponent<ConveyorBelt>();
        if (belt != null)
        {
            // Сосед смотрит на нас: exit ≈ -fromNeighborOffset
            Vector3 towardUs = CardinalToWorld(-fromNeighborOffset);
            return Vector3.Dot(belt.GetExitDirection(), towardUs) > 0.7f;
        }

        BuildingBase building = obj.GetComponent<BuildingBase>();
        if (building != null)
        {
            // Output здания смотрит на нас: forward ≈ -fromNeighborOffset
            return HasSocketFacing(building, SocketType.Output, -fromNeighborOffset);
        }

        return false;
    }

    /// <param name="toNeighborOffset">Направление от нас к соседу-приёмнику.</param>
    public static bool NeighborCanAcceptFromUs(Vector2Int ourCell, Vector2Int toNeighborOffset)
    {
        GameObject obj = GridOccupancy.GetAt(ourCell + toNeighborOffset);
        if (obj == null) return false;

        ConveyorBelt belt = obj.GetComponent<ConveyorBelt>();
        if (belt != null)
        {
            // Строго: Entry соседа продолжает наш Exit (промт §1).
            // Для Straight entry==exit — цепочка работает.
            Vector3 ourExit = CardinalToWorld(toNeighborOffset);
            return Vector3.Dot(belt.GetEntryDirection(), ourExit) > 0.7f;
        }

        BuildingBase building = obj.GetComponent<BuildingBase>();
        if (building != null)
        {
            // Input смотрит на нас (на ленту): forward ≈ -toNeighborOffset
            return HasSocketFacing(building, SocketType.Input, -toNeighborOffset);
        }

        return false;
    }

    static bool HasSocketFacing(BuildingBase building, SocketType type, Vector2Int worldOffset)
    {
        BuildingSocket[] sockets = type == SocketType.Input ? building.inputSockets : building.outputSockets;
        if (sockets == null) return false;

        Vector3 desired = CardinalToWorld(worldOffset);
        foreach (var s in sockets)
        {
            if (s == null) continue;
            Vector3 f = s.transform.forward;
            f.y = 0f;
            if (f.sqrMagnitude < 0.0001f) continue;
            if (Vector3.Dot(f.normalized, desired) > 0.5f)
                return true;
        }

        return false;
    }

    public static Vector3 YawToForward(float yawDegrees)
    {
        float rad = yawDegrees * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)).normalized;
    }

    public static Vector2Int WorldToCardinal(Vector3 dir)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f)
            return new Vector2Int(0, 1);

        if (Mathf.Abs(dir.x) >= Mathf.Abs(dir.z))
            return dir.x >= 0f ? new Vector2Int(1, 0) : new Vector2Int(-1, 0);

        return dir.z >= 0f ? new Vector2Int(0, 1) : new Vector2Int(0, -1);
    }

    public static Vector3 CardinalToWorld(Vector2Int c)
    {
        return new Vector3(c.x, 0f, c.y);
    }

    public static float NormalizeYaw(float yaw)
    {
        yaw %= 360f;
        if (yaw < 0f) yaw += 360f;
        // snap to 90
        return Mathf.Round(yaw / 90f) * 90f;
    }
}
