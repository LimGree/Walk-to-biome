using System.Collections.Generic;
using UnityEngine;

public class ChemicalPlant : CrafterBuilding
{
    protected override string WorkClip => "bld_chem_loop";
    static readonly Vector2Int[] Cardinals =
    {
        new Vector2Int(0, 1),
        new Vector2Int(1, 0),
        new Vector2Int(0, -1),
        new Vector2Int(-1, 0)
    };

    readonly List<Vector2Int> cells = new List<Vector2Int>(16);

    void Awake()
    {
        EnsureSetup();
    }

    public override void OnPlaced()
    {
        EnsureSetup();
        base.OnPlaced();
    }

    protected override bool AcceptsItem(ItemData item)
    {
        if (item == null)
            return false;
        if (currentRecipe == null || currentRecipe.inputs == null)
            return false;

        for (int i = 0; i < currentRecipe.inputs.Count; i++)
        {
            if (currentRecipe.inputs[i].item == item)
                return true;
        }

        return false;
    }

    protected override bool TryPushToConnections(ItemData item)
    {
        if (item == null)
            return false;
        if (base.TryPushToConnections(item))
            return true;
        if (item.isFluid)
            return false;
        return TryPushToAdjacentBelts(item);
    }

    void EnsureSetup()
    {
        DisableChildColliders();
        EnsureCollider();
        EnsureSockets();
    }

    void DisableChildColliders()
    {
        Collider[] cols = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] != null && cols[i].gameObject != gameObject)
                cols[i].enabled = false;
        }
    }

    void EnsureCollider()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null)
            box = gameObject.AddComponent<BoxCollider>();

        Vector3 world = GridFootprint.GetWorldSize(FootprintSize);
        Vector3 lossy = transform.lossyScale;
        box.size = new Vector3(
            world.x * 0.88f / Mathf.Max(0.01f, lossy.x),
            1.1f / Mathf.Max(0.01f, lossy.y),
            world.z * 0.88f / Mathf.Max(0.01f, lossy.z)
        );
        box.center = new Vector3(0f, 0.55f, 0f);
        box.enabled = true;
    }

    void EnsureSockets()
    {
        BuildingSocket solidIn = FindOrCreateSocket("InputSocket", SocketType.Input, new Vector3(0f, 0.3f, -1.5f));
        BuildingSocket fluidIn = FindOrCreateSocket("InputSocketFluid", SocketType.Input, new Vector3(-1.5f, 0.3f, 0f));
        BuildingSocket output = FindOrCreateSocket("OutPutSocket", SocketType.Output, new Vector3(0f, 0.3f, 1.5f));
        inputSockets = new[] { solidIn, fluidIn };
        outputSockets = new[] { output };
    }

    BuildingSocket FindOrCreateSocket(string socketName, SocketType type, Vector3 localPos)
    {
        Transform existing = transform.Find(socketName);
        GameObject go;
        if (existing != null)
        {
            go = existing.gameObject;
            Vector3 local = existing.localPosition;
            local.y = 0f;
            if (local.sqrMagnitude < 0.04f)
                existing.localPosition = localPos;
        }
        else
        {
            go = new GameObject(socketName);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
        }

        BuildingSocket socket = go.GetComponent<BuildingSocket>();
        if (socket == null)
            socket = go.AddComponent<BuildingSocket>();
        socket.socketType = type;
        return socket;
    }

    bool TryPushToAdjacentBelts(ItemData item)
    {
        GridFootprint.CollectCells(transform.position, FootprintSize, cells);
        for (int i = 0; i < cells.Count; i++)
        {
            for (int d = 0; d < Cardinals.Length; d++)
            {
                BuildingBase other = BuildingLinker.GetBuildingAt(cells[i] + Cardinals[d]);
                Conveyor belt = other as Conveyor;
                if (belt == null || belt is Pipe)
                    continue;
                if (belt.TryAcceptTransfer(item, null))
                    return true;
            }
        }

        return false;
    }
}
