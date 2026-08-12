using UnityEngine;

public abstract class BuildingBase : MonoBehaviour
{
    [Header("Data")]
    public BuildingData data;

    [Header("Sockets")]
    public BuildingSocket[] inputSockets;
    public BuildingSocket[] outputSockets;

    public virtual void OnPlaced()
    {
        RegisterOnGrid();
    }

    public virtual void OnRemoved()
    {
        GridOccupancy.Unregister(gameObject);

        if (inputSockets != null)
        {
            foreach (var socket in inputSockets)
                if (socket != null) socket.DisconnectAll();
        }

        if (outputSockets != null)
        {
            foreach (var socket in outputSockets)
                if (socket != null) socket.DisconnectAll();
        }
    }

    void OnDestroy()
    {
        // Страховка, если объект уничтожили без OnRemoved
        GridOccupancy.Unregister(gameObject);
    }

    protected void RegisterOnGrid()
    {
        if (GridSystem.Instance == null)
            return;

        Vector2Int origin = GridSystem.Instance.WorldToCell(transform.position);
        Vector2Int size = data != null ? data.size : Vector2Int.one;
        size = GridOccupancy.GetRotatedSize(size, transform.eulerAngles.y);
        GridOccupancy.Register(gameObject, origin, size);
    }

    public virtual bool TryReceiveItem(ItemData item, BuildingSocket fromSocket)
    {
        return false;
    }

    protected bool TryOutputToAny(ItemData item)
    {
        if (outputSockets == null) return false;

        foreach (var socket in outputSockets)
        {
            if (socket == null) continue;

            // 1. Через конвейер
            if (socket.connectedBelt != null && socket.connectedBelt.TryAccept(item))
                return true;

            // 2. Прямое соединение сокет → сокет
            if (socket.connectedSocket != null)
            {
                BuildingBase targetBuilding = socket.connectedSocket.GetComponentInParent<BuildingBase>();
                if (targetBuilding != null && targetBuilding.TryReceiveItem(item, socket.connectedSocket))
                    return true;
            }
        }

        return false;
    }

}
