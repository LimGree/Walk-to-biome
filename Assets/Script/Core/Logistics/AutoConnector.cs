using UnityEngine;

public static class AutoConnector
{
    [Header("Connection Settings")]
    public static float connectDistance = 1.5f;

    // Насколько сильно направления должны совпадать.
    // 1 = идеально совпадают, 0 = перпендикулярны.
    private const float directionThreshold = 0.7f;

    /// <summary>
    /// Вызывать после установки здания или конвейера.
    /// </summary>
    public static void TryAutoConnect(GameObject placedObject)
    {
        if (placedObject == null)
            return;

        // Конвейер
        ConveyorBelt belt = placedObject.GetComponent<ConveyorBelt>();
        if (belt != null)
        {
            ConnectConveyor(belt);
            return;
        }

        // Здание
        BuildingBase building = placedObject.GetComponent<BuildingBase>();
        if (building != null)
        {
            ConnectBuilding(building);
        }
    }

    // =========================================================
    // CONVEYOR
    // =========================================================

    private static void ConnectConveyor(ConveyorBelt belt)
    {
        if (belt == null)
            return;

        if (belt.startPoint == null || belt.endPoint == null)
        {
            Debug.LogWarning(
                $"[AutoConnect] У ленты {belt.name} не назначены startPoint/endPoint."
            );

            return;
        }

        Debug.Log($"[AutoConnect] Проверяем подключения ленты {belt.name}");

        // -----------------------------------------------------
        // 1. START ленты
        // Ищем OUTPUT здания, которое должно подавать предметы
        // на эту ленту.
        // -----------------------------------------------------

        if (belt.connectedOutputSocket == null)
        {
            BuildingSocket outputSocket =
                FindBestOutputForBeltStart(belt);

            if (outputSocket != null)
            {
                ConnectOutputToBelt(outputSocket, belt);
            }
        }

        // -----------------------------------------------------
        // 2. END ленты
        // Ищем INPUT здания, в которое лента должна отдавать.
        // -----------------------------------------------------

        if (belt.connectedInputSocket == null)
        {
            BuildingSocket inputSocket =
                FindBestInputForBeltEnd(belt);

            if (inputSocket != null)
            {
                ConnectBeltToInput(belt, inputSocket);
            }
        }

        // -----------------------------------------------------
        // 3. START ленты ← предыдущая лента
        // -----------------------------------------------------

        if (belt.connectedOutputSocket == null)
        {
            ConveyorBelt previousBelt =
                FindPreviousBelt(belt);

            if (previousBelt != null)
            {
                ConnectBelts(previousBelt, belt);
            }
        }

        // -----------------------------------------------------
        // 4. END ленты → следующая лента
        // -----------------------------------------------------

        if (belt.nextBelt == null)
        {
            ConveyorBelt nextBelt =
                FindNextBelt(belt);

            if (nextBelt != null)
            {
                ConnectBelts(belt, nextBelt);
            }
        }
    }

    // =========================================================
    // BUILDING
    // =========================================================

    private static void ConnectBuilding(BuildingBase building)
    {
        if (building == null)
            return;

        Debug.Log($"[AutoConnect] Проверяем подключения здания {building.name}");

        // -----------------------------------------------------
        // OUTPUTS
        // -----------------------------------------------------

        if (building.outputSockets != null)
        {
            foreach (BuildingSocket socket in building.outputSockets)
            {
                if (socket == null)
                    continue;

                if (socket.connectedBelt != null)
                    continue;

                ConveyorBelt belt =
                    FindBestBeltForOutput(socket);

                if (belt != null)
                {
                    ConnectOutputToBelt(socket, belt);
                }
            }
        }

        // -----------------------------------------------------
        // INPUTS
        // -----------------------------------------------------

        if (building.inputSockets != null)
        {
            foreach (BuildingSocket socket in building.inputSockets)
            {
                if (socket == null)
                    continue;

                if (socket.connectedBelt != null)
                    continue;

                ConveyorBelt belt =
                    FindBestBeltForInput(socket);

                if (belt != null)
                {
                    ConnectBeltToInput(belt, socket);
                }
            }
        }
    }

    // =========================================================
    // BUILDING OUTPUT → BELT
    // =========================================================

    private static void ConnectOutputToBelt(
        BuildingSocket output,
        ConveyorBelt belt)
    {
        if (output == null || belt == null)
            return;

        // Output уже занят
        if (output.connectedBelt != null &&
            output.connectedBelt != belt)
        {
            Debug.LogWarning(
                $"[AutoConnect] Output {output.name} уже подключён к {output.connectedBelt.name}"
            );

            return;
        }

        // START уже имеет источник
        if (belt.connectedOutputSocket != null &&
            belt.connectedOutputSocket != output)
        {
            Debug.LogWarning(
                $"[AutoConnect] START ленты {belt.name} уже подключён к {belt.connectedOutputSocket.name}"
            );

            return;
        }

        output.ConnectBelt(belt);

        belt.connectedOutputSocket = output;

        Debug.Log(
            $"[AutoConnect] OUTPUT {output.name} → BELT {belt.name}"
        );
    }

    // =========================================================
    // BELT → BUILDING INPUT
    // =========================================================

    private static void ConnectBeltToInput(
        ConveyorBelt belt,
        BuildingSocket input)
    {
        if (belt == null || input == null)
            return;

        // Input уже занят другой лентой
        if (input.connectedBelt != null &&
            input.connectedBelt != belt)
        {
            Debug.LogWarning(
                $"[AutoConnect] Input {input.name} уже подключён к {input.connectedBelt.name}"
            );

            return;
        }

        // END уже подключён к другому зданию
        if (belt.connectedInputSocket != null &&
            belt.connectedInputSocket != input)
        {
            Debug.LogWarning(
                $"[AutoConnect] END ленты {belt.name} уже подключён к {belt.connectedInputSocket.name}"
            );

            return;
        }

        input.ConnectBelt(belt);

        belt.connectedInputSocket = input;

        Debug.Log(
            $"[AutoConnect] BELT {belt.name} → INPUT {input.name}"
        );
    }

    // =========================================================
    // BELT → BELT
    // =========================================================

    private static void ConnectBelts(
        ConveyorBelt from,
        ConveyorBelt to)
    {
        if (from == null || to == null || from == to)
            return;

        // У from уже есть следующая лента
        if (from.nextBelt != null &&
            from.nextBelt != to)
        {
            Debug.LogWarning(
                $"[AutoConnect] Лента {from.name} уже имеет nextBelt = {from.nextBelt.name}"
            );

            return;
        }

        // У to уже есть предыдущая лента.
        // Предыдущая лента хранится через connectedOutputSocket
        // только для здания, поэтому здесь проверяем,
        // не пытаются ли две ленты занять один START.
        if (to.connectedOutputSocket != null)
        {
            return;
        }

        from.nextBelt = to;

        Debug.Log(
            $"[AutoConnect] BELT {from.name} → BELT {to.name}"
        );
    }

    // =========================================================
    // SEARCH: BUILDING OUTPUT → BELT START
    // =========================================================

    private static BuildingSocket FindBestOutputForBeltStart(
        ConveyorBelt belt)
    {
        BuildingSocket best = null;
        float bestScore = float.MinValue;

        Vector3 position = belt.startPoint.position;
        Vector3 beltDirection =
            (belt.endPoint.position - belt.startPoint.position).normalized;

        BuildingSocket[] sockets =
            Object.FindObjectsByType<BuildingSocket>(
                FindObjectsSortMode.None
            );

        foreach (BuildingSocket socket in sockets)
        {
            if (socket == null)
                continue;

            if (socket.socketType != SocketType.Output)
                continue;

            if (socket.connectedBelt != null &&
                socket.connectedBelt != belt)
                continue;

            float distance =
                Vector3.Distance(position, socket.transform.position);

            if (distance > connectDistance)
                continue;

            // Output должен смотреть примерно в сторону START → END.
            float direction =
                Vector3.Dot(
                    socket.transform.forward.normalized,
                    beltDirection
                );

            if (direction < directionThreshold)
                continue;

            float score =
                (1f - distance / connectDistance) +
                direction;

            if (score > bestScore)
            {
                bestScore = score;
                best = socket;
            }
        }

        return best;
    }

    // =========================================================
    // SEARCH: BELT END → BUILDING INPUT
    // =========================================================

    private static BuildingSocket FindBestInputForBeltEnd(
        ConveyorBelt belt)
    {
        BuildingSocket best = null;
        float bestScore = float.MinValue;

        Vector3 position = belt.endPoint.position;

        Vector3 beltDirection =
            (belt.endPoint.position - belt.startPoint.position).normalized;

        BuildingSocket[] sockets =
            Object.FindObjectsByType<BuildingSocket>(
                FindObjectsSortMode.None
            );

        foreach (BuildingSocket socket in sockets)
        {
            if (socket == null)
                continue;

            if (socket.socketType != SocketType.Input)
                continue;

            if (socket.connectedBelt != null &&
                socket.connectedBelt != belt)
                continue;

            float distance =
                Vector3.Distance(position, socket.transform.position);

            if (distance > connectDistance)
                continue;

            // Для Input ожидаем, что его forward смотрит
            // навстречу движению предмета.
            float direction =
                Vector3.Dot(
                    -socket.transform.forward.normalized,
                    beltDirection
                );

            if (direction < directionThreshold)
                continue;

            float score =
                (1f - distance / connectDistance) +
                direction;

            if (score > bestScore)
            {
                bestScore = score;
                best = socket;
            }
        }

        return best;
    }

    // =========================================================
    // SEARCH: BUILDING OUTPUT → EXISTING BELT
    // =========================================================

    private static ConveyorBelt FindBestBeltForOutput(
        BuildingSocket output)
    {
        ConveyorBelt best = null;
        float bestScore = float.MinValue;

        ConveyorBelt[] belts =
            Object.FindObjectsByType<ConveyorBelt>(
                FindObjectsSortMode.None
            );

        foreach (ConveyorBelt belt in belts)
        {
            if (belt == null ||
                belt.startPoint == null ||
                belt.endPoint == null)
                continue;

            if (belt.connectedOutputSocket != null)
                continue;

            float distance =
                Vector3.Distance(
                    output.transform.position,
                    belt.startPoint.position
                );

            if (distance > connectDistance)
                continue;

            Vector3 beltDirection =
                (belt.endPoint.position - belt.startPoint.position).normalized;

            float direction =
                Vector3.Dot(
                    output.transform.forward.normalized,
                    beltDirection
                );

            if (direction < directionThreshold)
                continue;

            float score =
                (1f - distance / connectDistance) +
                direction;

            if (score > bestScore)
            {
                bestScore = score;
                best = belt;
            }
        }

        return best;
    }

    // =========================================================
    // SEARCH: BELT END → BUILDING INPUT
    // =========================================================

    private static ConveyorBelt FindBestBeltForInput(
        BuildingSocket input)
    {
        ConveyorBelt best = null;
        float bestScore = float.MinValue;

        ConveyorBelt[] belts =
            Object.FindObjectsByType<ConveyorBelt>(
                FindObjectsSortMode.None
            );

        foreach (ConveyorBelt belt in belts)
        {
            if (belt == null ||
                belt.startPoint == null ||
                belt.endPoint == null)
                continue;

            if (belt.connectedInputSocket != null)
                continue;

            float distance =
                Vector3.Distance(
                    input.transform.position,
                    belt.endPoint.position
                );

            if (distance > connectDistance)
                continue;

            Vector3 beltDirection =
                (belt.endPoint.position - belt.startPoint.position).normalized;

            float direction =
                Vector3.Dot(
                    -input.transform.forward.normalized,
                    beltDirection
                );

            if (direction < directionThreshold)
                continue;

            float score =
                (1f - distance / connectDistance) +
                direction;

            if (score > bestScore)
            {
                bestScore = score;
                best = belt;
            }
        }

        return best;
    }

    // =========================================================
    // SEARCH: PREVIOUS BELT
    // =========================================================

    private static ConveyorBelt FindPreviousBelt(
        ConveyorBelt belt)
    {
        ConveyorBelt best = null;
        float bestScore = float.MinValue;

        ConveyorBelt[] belts =
            Object.FindObjectsByType<ConveyorBelt>(
                FindObjectsSortMode.None
            );

        Vector3 startPosition = belt.startPoint.position;
        Vector3 direction =
            (belt.endPoint.position - belt.startPoint.position).normalized;

        foreach (ConveyorBelt other in belts)
        {
            if (other == null ||
                other == belt ||
                other.startPoint == null ||
                other.endPoint == null)
                continue;

            // У другой ленты уже есть следующий объект.
            if (other.nextBelt != null)
                continue;

            float distance =
                Vector3.Distance(
                    other.endPoint.position,
                    startPosition
                );

            if (distance > connectDistance)
                continue;

            Vector3 otherDirection =
                (other.endPoint.position -
                 other.startPoint.position).normalized;

            // Предыдущая лента должна смотреть примерно
            // в том же направлении.
            float directionMatch =
                Vector3.Dot(otherDirection, direction);

            if (directionMatch < directionThreshold)
                continue;

            float score =
                (1f - distance / connectDistance) +
                directionMatch;

            if (score > bestScore)
            {
                bestScore = score;
                best = other;
            }
        }

        return best;
    }

    // =========================================================
    // SEARCH: NEXT BELT
    // =========================================================

    private static ConveyorBelt FindNextBelt(
        ConveyorBelt belt)
    {
        ConveyorBelt best = null;
        float bestScore = float.MinValue;

        ConveyorBelt[] belts =
            Object.FindObjectsByType<ConveyorBelt>(
                FindObjectsSortMode.None
            );

        Vector3 endPosition = belt.endPoint.position;

        Vector3 direction =
            (belt.endPoint.position - belt.startPoint.position).normalized;

        foreach (ConveyorBelt other in belts)
        {
            if (other == null ||
                other == belt ||
                other.startPoint == null ||
                other.endPoint == null)
                continue;

            // START другой ленты уже занят.
            if (other.connectedOutputSocket != null)
                continue;

            float distance =
                Vector3.Distance(
                    endPosition,
                    other.startPoint.position
                );

            if (distance > connectDistance)
                continue;

            Vector3 otherDirection =
                (other.endPoint.position -
                 other.startPoint.position).normalized;

            float directionMatch =
                Vector3.Dot(direction, otherDirection);

            if (directionMatch < directionThreshold)
                continue;

            float score =
                (1f - distance / connectDistance) +
                directionMatch;

            if (score > bestScore)
            {
                bestScore = score;
                best = other;
            }
        }

        return best;
    }
}