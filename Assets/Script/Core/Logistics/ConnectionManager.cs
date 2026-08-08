using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ConnectionManager : MonoBehaviour
{
    public static ConnectionManager Instance { get; private set; }

    [Header("Connection Settings")]
    public float connectDistance = 1.5f;

    private const float DirectionThreshold = 0.7f;

    private readonly List<BuildingSocket> socketQuery = new List<BuildingSocket>();
    private readonly List<ConveyorBelt> beltQuery = new List<ConveyorBelt>();
    private readonly HashSet<GameObject> reconnectBuffer = new HashSet<GameObject>();

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
        {
            Destroy(this);
            return;
        }

        if (GetComponent<ConveyorNetwork>() == null)
            gameObject.AddComponent<ConveyorNetwork>();
    }

    public void OnPlaced(GameObject placedObject)
    {
        if (placedObject == null)
            return;

        ConveyorBelt belt = placedObject.GetComponent<ConveyorBelt>();
        if (belt != null)
        {
            ConnectConveyor(belt);
            return;
        }

        BuildingBase building = placedObject.GetComponent<BuildingBase>();
        if (building != null)
            ConnectBuilding(building);
    }

    public void RemovePlacedObject(GameObject placedObject)
    {
        if (placedObject == null)
            return;

        CollectReconnectCandidates(placedObject, reconnectBuffer);

        BuildingBase building = placedObject.GetComponent<BuildingBase>();
        if (building != null)
            building.OnRemoved();

        DisconnectObject(placedObject);

        reconnectBuffer.Remove(placedObject);
        Destroy(placedObject);

        foreach (GameObject neighbor in reconnectBuffer)
            OnPlaced(neighbor);

        reconnectBuffer.Clear();
    }

    public void DisconnectObject(GameObject placedObject)
    {
        if (placedObject == null)
            return;

        ConveyorBelt belt = placedObject.GetComponent<ConveyorBelt>();
        if (belt != null)
            DisconnectBelt(belt);

        BuildingBase building = placedObject.GetComponent<BuildingBase>();
        if (building != null)
            DisconnectBuilding(building);
    }

    void ConnectConveyor(ConveyorBelt belt)
    {
        if (belt == null || belt.startPoint == null || belt.endPoint == null)
        {
            Debug.LogWarning($"[ConnectionManager] У ленты {belt?.name} не назначены startPoint/endPoint.");
            return;
        }

        if (belt.connectedOutputSocket == null)
        {
            BuildingSocket outputSocket = FindBestOutputForBeltStart(belt);
            if (outputSocket != null)
                ConnectOutputToBelt(outputSocket, belt);
        }

        if (belt.connectedInputSocket == null)
        {
            BuildingSocket inputSocket = FindBestInputForBeltEnd(belt);
            if (inputSocket != null)
                ConnectBeltToInput(belt, inputSocket);
        }

        if (belt.connectedOutputSocket == null && belt.prevBelt == null)
        {
            ConveyorBelt previousBelt = FindPreviousBelt(belt);
            if (previousBelt != null)
                ConnectBelts(previousBelt, belt);
        }

        if (belt.nextBelt == null)
        {
            ConveyorBelt nextBelt = FindNextBelt(belt);
            if (nextBelt != null)
                ConnectBelts(belt, nextBelt);
        }
    }

    void ConnectBuilding(BuildingBase building)
    {
        if (building == null)
            return;

        if (building.outputSockets != null)
        {
            foreach (BuildingSocket socket in building.outputSockets)
            {
                if (socket == null || socket.connectedBelt != null)
                    continue;

                ConveyorBelt belt = FindBestBeltForOutput(socket);
                if (belt != null)
                    ConnectOutputToBelt(socket, belt);
            }
        }

        if (building.inputSockets != null)
        {
            foreach (BuildingSocket socket in building.inputSockets)
            {
                if (socket == null || socket.connectedBelt != null)
                    continue;

                ConveyorBelt belt = FindBestBeltForInput(socket);
                if (belt != null)
                    ConnectBeltToInput(belt, socket);
            }
        }
    }

    void ConnectOutputToBelt(BuildingSocket output, ConveyorBelt belt)
    {
        if (output == null || belt == null)
            return;

        if (output.connectedBelt != null && output.connectedBelt != belt)
            return;

        if (belt.connectedOutputSocket != null && belt.connectedOutputSocket != output)
            return;

        if (belt.prevBelt != null)
            DisconnectBelts(belt.prevBelt, belt);

        output.ConnectBelt(belt);
        belt.connectedOutputSocket = output;
    }

    void ConnectBeltToInput(ConveyorBelt belt, BuildingSocket input)
    {
        if (belt == null || input == null)
            return;

        if (input.connectedBelt != null && input.connectedBelt != belt)
            return;

        if (belt.connectedInputSocket != null && belt.connectedInputSocket != input)
            return;

        input.ConnectBelt(belt);
        belt.connectedInputSocket = input;
    }

    void ConnectBelts(ConveyorBelt from, ConveyorBelt to)
    {
        if (from == null || to == null || from == to)
            return;

        if (from.nextBelt != null && from.nextBelt != to)
            return;

        if (to.prevBelt != null && to.prevBelt != from)
            return;

        if (to.connectedOutputSocket != null)
            return;

        from.nextBelt = to;
        to.prevBelt = from;
    }

    void DisconnectBelt(ConveyorBelt belt)
    {
        if (belt == null)
            return;

        belt.ClearItems();

        if (belt.connectedOutputSocket != null)
        {
            BuildingSocket output = belt.connectedOutputSocket;
            if (output.connectedBelt == belt)
                output.DisconnectBelt();
            belt.connectedOutputSocket = null;
        }

        if (belt.connectedInputSocket != null)
        {
            BuildingSocket input = belt.connectedInputSocket;
            if (input.connectedBelt == belt)
                input.DisconnectBelt();
            belt.connectedInputSocket = null;
        }

        if (belt.prevBelt != null)
        {
            if (belt.prevBelt.nextBelt == belt)
                belt.prevBelt.nextBelt = null;
            belt.prevBelt = null;
        }

        if (belt.nextBelt != null)
        {
            if (belt.nextBelt.prevBelt == belt)
                belt.nextBelt.prevBelt = null;
            belt.nextBelt = null;
        }
    }

    void DisconnectBuilding(BuildingBase building)
    {
        if (building == null)
            return;

        if (building.outputSockets != null)
        {
            foreach (BuildingSocket socket in building.outputSockets)
            {
                if (socket == null || socket.connectedBelt == null)
                    continue;

                ConveyorBelt belt = socket.connectedBelt;
                socket.DisconnectBelt();

                if (belt.connectedOutputSocket == socket)
                    belt.connectedOutputSocket = null;
            }
        }

        if (building.inputSockets != null)
        {
            foreach (BuildingSocket socket in building.inputSockets)
            {
                if (socket == null || socket.connectedBelt == null)
                    continue;

                ConveyorBelt belt = socket.connectedBelt;
                socket.DisconnectBelt();

                if (belt.connectedInputSocket == socket)
                    belt.connectedInputSocket = null;
            }
        }
    }

    void CollectReconnectCandidates(GameObject placedObject, HashSet<GameObject> results)
    {
        results.Clear();

        ConveyorBelt belt = placedObject.GetComponent<ConveyorBelt>();
        if (belt != null)
            CollectBeltNeighbors(belt, results);

        BuildingBase building = placedObject.GetComponent<BuildingBase>();
        if (building != null)
            CollectBuildingNeighbors(building, results);
    }

    void CollectBeltNeighbors(ConveyorBelt belt, HashSet<GameObject> results)
    {
        if (belt.prevBelt != null)
            results.Add(belt.prevBelt.gameObject);

        if (belt.nextBelt != null)
            results.Add(belt.nextBelt.gameObject);

        if (belt.connectedOutputSocket != null)
        {
            GameObject sourceBuilding = belt.connectedOutputSocket.GetComponentInParent<BuildingBase>()?.gameObject;
            if (sourceBuilding != null)
                results.Add(sourceBuilding);
        }

        if (belt.connectedInputSocket != null)
        {
            GameObject targetBuilding = belt.connectedInputSocket.GetComponentInParent<BuildingBase>()?.gameObject;
            if (targetBuilding != null)
                results.Add(targetBuilding);
        }
    }

    void CollectBuildingNeighbors(BuildingBase building, HashSet<GameObject> results)
    {
        AddSocketNeighbor(building.outputSockets, results);
        AddSocketNeighbor(building.inputSockets, results);
    }

    void AddSocketNeighbor(BuildingSocket[] sockets, HashSet<GameObject> results)
    {
        if (sockets == null)
            return;

        foreach (BuildingSocket socket in sockets)
        {
            if (socket == null || socket.connectedBelt == null)
                continue;

            results.Add(socket.connectedBelt.gameObject);
        }
    }

    void DisconnectBelts(ConveyorBelt from, ConveyorBelt to)
    {
        if (from != null && from.nextBelt == to)
            from.nextBelt = null;

        if (to != null && to.prevBelt == from)
            to.prevBelt = null;
    }

    BuildingSocket FindBestOutputForBeltStart(ConveyorBelt belt)
    {
        BuildingSocket best = null;
        float bestScore = float.MinValue;

        Vector3 position = belt.startPoint.position;
        Vector3 beltDirection = (belt.endPoint.position - belt.startPoint.position).normalized;

        QuerySockets(position, SocketType.Output);

        foreach (BuildingSocket socket in socketQuery)
        {
            if (socket.connectedBelt != null && socket.connectedBelt != belt)
                continue;

            float distance = Vector3.Distance(position, socket.transform.position);
            if (distance > connectDistance)
                continue;

            float direction = Vector3.Dot(socket.transform.forward.normalized, beltDirection);
            if (direction < DirectionThreshold)
                continue;

            float score = (1f - distance / connectDistance) + direction;
            if (score > bestScore)
            {
                bestScore = score;
                best = socket;
            }
        }

        return best;
    }

    BuildingSocket FindBestInputForBeltEnd(ConveyorBelt belt)
    {
        BuildingSocket best = null;
        float bestScore = float.MinValue;

        Vector3 position = belt.endPoint.position;
        Vector3 beltDirection = (belt.endPoint.position - belt.startPoint.position).normalized;

        QuerySockets(position, SocketType.Input);

        foreach (BuildingSocket socket in socketQuery)
        {
            if (socket.connectedBelt != null && socket.connectedBelt != belt)
                continue;

            float distance = Vector3.Distance(position, socket.transform.position);
            if (distance > connectDistance)
                continue;

            float direction = Vector3.Dot(-socket.transform.forward.normalized, beltDirection);
            if (direction < DirectionThreshold)
                continue;

            float score = (1f - distance / connectDistance) + direction;
            if (score > bestScore)
            {
                bestScore = score;
                best = socket;
            }
        }

        return best;
    }

    ConveyorBelt FindBestBeltForOutput(BuildingSocket output)
    {
        ConveyorBelt best = null;
        float bestScore = float.MinValue;

        QueryBeltsNearStart(output.transform.position);

        foreach (ConveyorBelt belt in beltQuery)
        {
            if (belt.startPoint == null || belt.endPoint == null)
                continue;

            if (belt.connectedOutputSocket != null || belt.prevBelt != null)
                continue;

            float distance = Vector3.Distance(output.transform.position, belt.startPoint.position);
            if (distance > connectDistance)
                continue;

            Vector3 beltDirection = (belt.endPoint.position - belt.startPoint.position).normalized;
            float direction = Vector3.Dot(output.transform.forward.normalized, beltDirection);
            if (direction < DirectionThreshold)
                continue;

            float score = (1f - distance / connectDistance) + direction;
            if (score > bestScore)
            {
                bestScore = score;
                best = belt;
            }
        }

        return best;
    }

    ConveyorBelt FindBestBeltForInput(BuildingSocket input)
    {
        ConveyorBelt best = null;
        float bestScore = float.MinValue;

        QueryBeltsNearEnd(input.transform.position);

        foreach (ConveyorBelt belt in beltQuery)
        {
            if (belt.startPoint == null || belt.endPoint == null)
                continue;

            if (belt.connectedInputSocket != null)
                continue;

            float distance = Vector3.Distance(input.transform.position, belt.endPoint.position);
            if (distance > connectDistance)
                continue;

            Vector3 beltDirection = (belt.endPoint.position - belt.startPoint.position).normalized;
            float direction = Vector3.Dot(-input.transform.forward.normalized, beltDirection);
            if (direction < DirectionThreshold)
                continue;

            float score = (1f - distance / connectDistance) + direction;
            if (score > bestScore)
            {
                bestScore = score;
                best = belt;
            }
        }

        return best;
    }

    ConveyorBelt FindPreviousBelt(ConveyorBelt belt)
    {
        ConveyorBelt best = null;
        float bestScore = float.MinValue;

        Vector3 startPosition = belt.startPoint.position;
        Vector3 direction = (belt.endPoint.position - belt.startPoint.position).normalized;

        QueryBeltsNearEnd(startPosition);

        foreach (ConveyorBelt other in beltQuery)
        {
            if (other == belt || other.startPoint == null || other.endPoint == null)
                continue;

            if (other.nextBelt != null)
                continue;

            float distance = Vector3.Distance(other.endPoint.position, startPosition);
            if (distance > connectDistance)
                continue;

            Vector3 otherDirection = (other.endPoint.position - other.startPoint.position).normalized;
            float directionMatch = Vector3.Dot(otherDirection, direction);
            if (directionMatch < DirectionThreshold)
                continue;

            float score = (1f - distance / connectDistance) + directionMatch;
            if (score > bestScore)
            {
                bestScore = score;
                best = other;
            }
        }

        return best;
    }

    ConveyorBelt FindNextBelt(ConveyorBelt belt)
    {
        ConveyorBelt best = null;
        float bestScore = float.MinValue;

        Vector3 endPosition = belt.endPoint.position;
        Vector3 direction = (belt.endPoint.position - belt.startPoint.position).normalized;

        QueryBeltsNearStart(endPosition);

        foreach (ConveyorBelt other in beltQuery)
        {
            if (other == belt || other.startPoint == null || other.endPoint == null)
                continue;

            if (other.prevBelt != null || other.connectedOutputSocket != null)
                continue;

            float distance = Vector3.Distance(endPosition, other.startPoint.position);
            if (distance > connectDistance)
                continue;

            Vector3 otherDirection = (other.endPoint.position - other.startPoint.position).normalized;
            float directionMatch = Vector3.Dot(direction, otherDirection);
            if (directionMatch < DirectionThreshold)
                continue;

            float score = (1f - distance / connectDistance) + directionMatch;
            if (score > bestScore)
            {
                bestScore = score;
                best = other;
            }
        }

        return best;
    }

    void QuerySockets(Vector3 position, SocketType type)
    {
        if (ConveyorNetwork.Instance == null)
            return;

        ConveyorNetwork.Instance.QuerySocketsNear(position, connectDistance, type, socketQuery);
    }

    void QueryBeltsNearStart(Vector3 position)
    {
        if (ConveyorNetwork.Instance == null)
            return;

        ConveyorNetwork.Instance.QueryBeltsNearStartPoint(position, connectDistance, beltQuery);
    }

    void QueryBeltsNearEnd(Vector3 position)
    {
        if (ConveyorNetwork.Instance == null)
            return;

        ConveyorNetwork.Instance.QueryBeltsNearEndPoint(position, connectDistance, beltQuery);
    }
}
