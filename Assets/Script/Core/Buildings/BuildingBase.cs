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
    [Tooltip("Буфер выхода, если приёмник занят.")]
    public int maxOutputBuffer = 8;

    [Header("Footprint gizmo")]
    public bool drawFootprintGizmo = true;

    private readonly Queue<ItemData> outputBuffer = new Queue<ItemData>();

    public int OutputBufferCount => outputBuffer.Count;
    public int OutputBufferFree => Mathf.Max(0, maxOutputBuffer - outputBuffer.Count);

    public Vector2Int FootprintSize
    {
        get
        {
            Vector2Int size = data != null ? data.size : Vector2Int.one;
            return GridFootprint.GetRotatedSize(size, transform.eulerAngles.y);
        }
    }

    public virtual void OnPlaced()
    {
        RegisterOnGrid();
        BuildingLinker.RelinkAround(this);
    }

    public virtual void OnRemoved()
    {
        List<BuildingBase> neighbors = new List<BuildingBase>(8);
        BuildingLinker.CollectNeighbors(this, neighbors);
        DisconnectAllSockets();
        GridOccupancy.Unregister(gameObject);
        BuildingLinker.Relink(neighbors);
    }

    public virtual void OnRotated()
    {
        BuildingLinker.RelinkAround(this);
    }

    protected virtual void OnDestroy()
    {
        GridOccupancy.Unregister(gameObject);
    }

    public void ReRegisterOnGrid()
    {
        GridOccupancy.Unregister(gameObject);
        RegisterOnGrid();
    }

    protected void RegisterOnGrid()
    {
        if (GridSystem.Instance == null)
            return;

        GridFootprint.Register(gameObject, transform.position, FootprintSize);
    }

    void DisconnectAllSockets()
    {
        if (inputSockets != null)
        {
            for (int i = 0; i < inputSockets.Length; i++)
            {
                if (inputSockets[i] != null)
                    inputSockets[i].DisconnectAll();
            }
        }

        if (outputSockets != null)
        {
            for (int i = 0; i < outputSockets.Length; i++)
            {
                if (outputSockets[i] != null)
                    outputSockets[i].DisconnectAll();
            }
        }
    }

    public virtual bool TryReceiveItem(ItemData item, BuildingSocket fromSocket)
    {
        return false;
    }

    /// <summary>
    /// Можно ли принять предмет от этого соседа: только со стороны входного сокета.
    /// </summary>
    public virtual bool CanAcceptFrom(BuildingBase source)
    {
        return BuildingLinker.HasInputFrom(this, source);
    }

    public bool HasOutputSpace(int count = 1)
    {
        return count <= 0 || outputBuffer.Count + count <= maxOutputBuffer;
    }

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

    protected virtual bool TryPushToConnections(ItemData item)
    {
        if (item == null || outputSockets == null)
            return false;

        for (int i = 0; i < outputSockets.Length; i++)
        {
            BuildingSocket socket = outputSockets[i];
            if (socket == null)
                continue;

            if (socket.connectedSocket != null)
            {
                BuildingBase linked = socket.connectedSocket.GetComponentInParent<BuildingBase>();
                if (linked != null
                    && linked.CanAcceptFrom(this)
                    && linked.TryReceiveItem(item, socket.connectedSocket))
                    return true;
            }

            BuildingBase front = BuildingLinker.GetBuildingAt(BuildingLinker.GetSocketFrontCell(socket));
            if (front != null
                && front != this
                && front.CanAcceptFrom(this)
                && front.TryReceiveItem(item, socket))
                return true;
        }

        return false;
    }

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

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!drawFootprintGizmo)
            return;

        Vector2Int size = data != null ? data.size : Vector2Int.one;
        size = GridFootprint.GetRotatedSize(size, transform.eulerAngles.y);

        GridFootprint.DrawFootprintGizmo(transform.position, size, new Color(0.2f, 0.9f, 1f, 0.95f));
        GridFootprint.DrawCellQuadsGizmo(transform.position, size, new Color(0.2f, 1f, 0.4f, 0.55f));

        Vector3 worldSize = GridFootprint.GetWorldSize(size);
        float cell = GridFootprint.CellSize;
        Vector3 cubeSize = new Vector3(worldSize.x * 0.98f, cell * 0.5f, worldSize.z * 0.98f);
        Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.35f);
        Gizmos.DrawWireCube(transform.position + Vector3.up * (cubeSize.y * 0.5f), cubeSize);
    }
#endif
}
