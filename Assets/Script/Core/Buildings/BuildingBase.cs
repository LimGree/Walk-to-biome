using System.Collections.Generic;
using UnityEngine;

public abstract class BuildingBase : MonoBehaviour
{
    [Header("Data")]
    public BuildingData data;

    [Header("Sockets")]
    public BuildingSocket[] inputSockets;
    public BuildingSocket[] outputSockets;

    [Header("Output buffer")]
    [Tooltip("Сколько предметов держать, если лента/приёмник заняты. Craft не стартует без места.")]
    public int maxOutputBuffer = 8;

    private readonly Queue<ItemData> outputBuffer = new Queue<ItemData>();

    public int OutputBufferCount => outputBuffer.Count;
    public int OutputBufferFree => Mathf.Max(0, maxOutputBuffer - outputBuffer.Count);

    public virtual void OnPlaced()
    {
        RegisterOnGrid();
        // Reshape/Reconnect — снаружи (PlayerBuilder), один раз
    }

    public virtual void OnRemoved()
    {
        AutoConnector.ClearBuildingLinks(this);
        GridOccupancy.Unregister(gameObject);
    }

    void OnDestroy()
    {
        GridOccupancy.Unregister(gameObject);
    }

    /// <summary>Перерегистрация после rotate на месте (size может смениться).</summary>
    public void ReRegisterOnGrid()
    {
        GridOccupancy.Unregister(gameObject);
        RegisterOnGrid();
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

    /// <summary>Свободные слоты output-буфера.</summary>
    public bool HasOutputSpace(int count = 1)
    {
        return count <= 0 || outputBuffer.Count + count <= maxOutputBuffer;
    }

    /// <summary>
    /// Выдать предмет: сначала в соединения, иначе в буфер (без потери).
    /// </summary>
    protected bool TryOutputToAny(ItemData item)
    {
        if (item == null) return false;

        if (TryPushToConnections(item))
            return true;

        if (outputBuffer.Count < maxOutputBuffer)
        {
            outputBuffer.Enqueue(item);
            return true;
        }

        return false;
    }

    /// <summary>Только внешние соединения (без буфера).</summary>
    protected bool TryPushToConnections(ItemData item)
    {
        if (item == null || outputSockets == null)
            return false;

        for (int i = 0; i < outputSockets.Length; i++)
        {
            BuildingSocket socket = outputSockets[i];
            if (socket == null) continue;

            if (socket.connectedBelt != null && socket.connectedBelt.TryAccept(item))
                return true;

            if (socket.connectedSocket != null)
            {
                BuildingBase targetBuilding = socket.connectedSocket.GetComponentInParent<BuildingBase>();
                if (targetBuilding != null
                    && targetBuilding.TryReceiveItem(item, socket.connectedSocket))
                    return true;
            }
        }

        return false;
    }

    /// <summary>Слить output-буфер в ленты/соседей. Вызывать из Update машин.</summary>
    protected void FlushOutputBuffer()
    {
        while (outputBuffer.Count > 0)
        {
            ItemData item = outputBuffer.Peek();
            if (!TryPushToConnections(item))
                break;
            outputBuffer.Dequeue();
        }
    }

    protected virtual void LateUpdate()
    {
        if (outputBuffer.Count > 0)
            FlushOutputBuffer();
    }
}
