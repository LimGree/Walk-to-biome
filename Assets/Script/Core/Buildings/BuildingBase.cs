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
    }

    public virtual void OnRemoved()
    {
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
