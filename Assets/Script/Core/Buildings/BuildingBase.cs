using UnityEngine;

public abstract class BuildingBase : MonoBehaviour
{
    [Header("Data")]
    public BuildingData data;

    [Header("Sockets")]
    public BuildingSocket[] inputSockets;
    public BuildingSocket[] outputSockets;

    // Вызывается сразу после установки игроком
    public virtual void OnPlaced()
    {
        // Можно переопределять в дочерних классах
    }

    // Вызывается при удалении здания
    public virtual void OnRemoved()
    {
        // Отключаем все сокеты
        if (inputSockets != null)
        {
            foreach (var socket in inputSockets)
                socket.DisconnectBelt();
        }

        if (outputSockets != null)
        {
            foreach (var socket in outputSockets)
                socket.DisconnectBelt();
        }
    }

    // Попытка принять предмет (используется конвейерами и другими зданиями)
    public virtual bool TryReceiveItem(ItemData item, BuildingSocket fromSocket)
    {
        return false;
    }

    // Удобный метод — есть ли свободный выход
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