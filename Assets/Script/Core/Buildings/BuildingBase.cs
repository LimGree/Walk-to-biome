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
            if (socket.connectedBelt != null && socket.connectedBelt.TryAccept(item))
                return true;
        }
        return false;
    }
}
