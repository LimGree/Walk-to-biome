using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Перекрёсток ленты: 1 вход, 3 выхода. Предметы едут по клетке, как на конвейере.
/// </summary>
public class Splitter : BuildingBase
{
    [Header("Belt")]
    public float speed = 2.5f;
    public int maxItems = 3;
    public float itemHeight = 0.35f;
    public float itemScale = 0.28f;

    [Header("Debug")]
    public bool showDebug;

    public Vector2Int Cell => BuildingLinker.WorldToCell(transform.position);

    public BuildingSocket InputSocket =>
        inputSockets != null && inputSockets.Length > 0 ? inputSockets[0] : null;

    bool isLive;
    int nextOutput;
    float resolvedHeight;
    readonly List<Cargo> cargo = new List<Cargo>(4);

    class Cargo
    {
        public ItemData item;
        public float progress;
        public Transform visual;
        public Vector2Int entryDir;
        public Vector2Int exitDir;
    }

    void Awake()
    {
        EnsureSetup();
    }

    public override void OnPlaced()
    {
        isLive = true;
        EnsureSetup();
        nextOutput = 0;
        base.OnPlaced();
    }

    public override void OnRemoved()
    {
        isLive = false;
        ClearCargo();
        base.OnRemoved();
    }

    public bool IsFedBy(BuildingBase source)
    {
        if (source == null)
            return false;

        return BuildingLinker.OccupiesCell(source, Cell + InputOutward());
    }

    public override bool CanAcceptFrom(BuildingBase source)
    {
        return IsFedBy(source);
    }

    public override bool TryReceiveItem(ItemData item, BuildingSocket fromSocket)
    {
        if (!isLive || item == null || !CanAccept())
            return false;

        SpawnCargo(item, null, InferEntryDir(fromSocket), Vector2Int.zero);
        return true;
    }

    public bool TryAcceptTransfer(ItemData item, Transform visual)
    {
        if (!isLive || item == null || !CanAccept())
            return false;

        Vector3 from = visual != null ? visual.position : transform.position;
        SpawnCargo(item, visual, InferEntryFromWorld(from), Vector2Int.zero);
        return true;
    }

    float ItemGap => 1f / Mathf.Max(1, maxItems);

    bool CanAccept()
    {
        if (cargo.Count >= Mathf.Max(1, maxItems))
            return false;

        float nearest = 1f;
        for (int i = 0; i < cargo.Count; i++)
        {
            if (cargo[i].progress < nearest)
                nearest = cargo[i].progress;
        }

        return nearest >= ItemGap;
    }

    void Update()
    {
        if (!isLive)
            return;

        float cell = GridFootprint.CellSize;
        float move = speed * Time.deltaTime / Mathf.Max(0.05f, cell);
        float gap = ItemGap;

        cargo.Sort((a, b) => b.progress.CompareTo(a.progress));

        for (int i = 0; i < cargo.Count; i++)
        {
            float limit = i == 0 ? 1f : cargo[i - 1].progress - gap;
            if (limit < 0f)
                limit = 0f;

            Cargo item = cargo[i];
            if (item.progress < limit)
                item.progress = Mathf.Min(limit, item.progress + move);
        }

        if (cargo.Count > 0 && cargo[0].progress >= 0.999f)
        {
            if (TryHandOff(cargo[0]))
                cargo.RemoveAt(0);
            else
                cargo[0].progress = 1f;
        }

        for (int i = 0; i < cargo.Count; i++)
            UpdateCargoVisual(cargo[i]);
    }

    bool TryHandOff(Cargo item)
    {
        Vector2Int nextCell = Cell + item.exitDir;
        BuildingBase dest = BuildingLinker.GetBuildingAt(nextCell);
        if (dest == null)
            return false;

        Conveyor nextBelt = dest as Conveyor;
        if (nextBelt != null)
            return nextBelt.TryAcceptTransfer(item.item, item.visual);

        Splitter nextSplit = dest as Splitter;
        if (nextSplit != null)
        {
            if (!nextSplit.CanAcceptFrom(this))
                return false;
            return nextSplit.TryAcceptTransfer(item.item, item.visual);
        }

        if (!dest.CanAcceptFrom(this))
            return false;

        BuildingSocket destInput = dest.inputSockets != null && dest.inputSockets.Length > 0
            ? dest.inputSockets[0]
            : null;
        if (!dest.TryReceiveItem(item.item, destInput))
            return false;

        BeltItemView.Destroy(item.visual);
        item.visual = null;
        return true;
    }

    void SpawnCargo(ItemData item, Transform visual, Vector2Int entryDir, Vector2Int exitDir)
    {
        if (entryDir.x == 0 && entryDir.y == 0)
            entryDir = Opposite(InputOutward());
        if (exitDir.x == 0 && exitDir.y == 0)
            exitDir = ChooseExitDir(entryDir);

        Cargo cargoItem = new Cargo
        {
            item = item,
            progress = 0f,
            visual = visual,
            entryDir = entryDir,
            exitDir = exitDir
        };

        if (cargoItem.visual == null)
            cargoItem.visual = BeltItemView.Create(item, itemScale);
        else
            BeltItemView.Prepare(cargoItem.visual);

        if (cargoItem.visual != null)
            cargoItem.visual.SetParent(transform, true);

        cargo.Add(cargoItem);
        UpdateCargoVisual(cargoItem);
    }

    void UpdateCargoVisual(Cargo item)
    {
        Vector3 pos = EvaluatePath(item, item.progress);
        Vector3 look = EvaluatePath(item, Mathf.Min(1f, item.progress + 0.05f)) - pos;
        BeltItemView.Update(item.visual, pos, look);
    }

    Vector3 EvaluatePath(Cargo item, float t)
    {
        float cell = GridFootprint.CellSize;
        Vector3 up = Vector3.up * resolvedHeight;
        Vector3 start = transform.position - BuildingLinker.CardinalToWorld(item.entryDir) * (cell * 0.5f) + up;
        Vector3 mid = transform.position + up;
        Vector3 end = transform.position + BuildingLinker.CardinalToWorld(item.exitDir) * (cell * 0.5f) + up;

        t = Mathf.Clamp01(t);
        if (item.entryDir == item.exitDir)
            return Vector3.Lerp(start, end, t);

        if (t < 0.5f)
            return Vector3.Lerp(start, mid, t * 2f);
        return Vector3.Lerp(mid, end, (t - 0.5f) * 2f);
    }

    Vector2Int InferEntryDir(BuildingSocket fromSocket)
    {
        if (fromSocket != null)
        {
            BuildingBase src = fromSocket.Owner;
            if (src != null && src != this)
                return InferEntryFromWorld(src.transform.position);
        }

        return Opposite(InputOutward());
    }

    Vector2Int InferEntryFromWorld(Vector3 world)
    {
        Vector2Int fromCell = BuildingLinker.WorldToCell(world);
        Vector2Int delta = Cell - fromCell;
        if (Mathf.Abs(delta.x) + Mathf.Abs(delta.y) == 1)
            return delta;

        Vector3 local = world - transform.position;
        local.y = 0f;
        if (local.sqrMagnitude > 0.0001f)
            return Opposite(BuildingLinker.ToCardinal(local));

        return Opposite(InputOutward());
    }

    Vector2Int ChooseExitDir(Vector2Int entryDir)
    {
        EnsureSetup();
        if (outputSockets == null || outputSockets.Length == 0)
            return entryDir.x == 0 && entryDir.y == 0
                ? BuildingLinker.ToCardinal(transform.forward)
                : entryDir;

        Vector2Int fallback = Vector2Int.zero;
        int count = outputSockets.Length;
        for (int n = 0; n < count; n++)
        {
            int index = (nextOutput + n) % count;
            BuildingSocket socket = outputSockets[index];
            if (socket == null)
                continue;

            Vector2Int dir = BuildingLinker.ToCardinal(socket.GetOutward());
            if (dir == Opposite(entryDir))
                continue;

            if (fallback.x == 0 && fallback.y == 0)
                fallback = dir;

            BuildingBase dest = BuildingLinker.GetBuildingAt(Cell + dir);
            if (dest == null || dest == this)
                continue;

            nextOutput = (index + 1) % count;
            return dir;
        }

        if (fallback.x != 0 || fallback.y != 0)
        {
            nextOutput = (nextOutput + 1) % count;
            return fallback;
        }

        return entryDir;
    }

    Vector2Int InputOutward()
    {
        if (InputSocket != null)
            return BuildingLinker.ToCardinal(InputSocket.GetOutward());

        return new Vector2Int(1, 0);
    }

    static Vector2Int Opposite(Vector2Int dir)
    {
        return new Vector2Int(-dir.x, -dir.y);
    }

    void ClearCargo()
    {
        for (int i = 0; i < cargo.Count; i++)
            BeltItemView.Destroy(cargo[i].visual);
        cargo.Clear();
    }

    void EnsureSetup()
    {
        DisableChildColliders();
        EnsureCollider();
        BindPrefabSockets();
        resolvedHeight = ResolveItemHeight();
    }

    void DisableChildColliders()
    {
        Collider[] childCols = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < childCols.Length; i++)
        {
            if (childCols[i] != null && childCols[i].gameObject != gameObject)
                childCols[i].enabled = false;
        }
    }

    void EnsureCollider()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null)
            box = gameObject.AddComponent<BoxCollider>();

        float cell = GridFootprint.CellSize * 0.72f;
        Vector3 lossy = transform.lossyScale;
        box.size = new Vector3(
            cell / Mathf.Max(0.01f, lossy.x),
            0.35f / Mathf.Max(0.01f, lossy.y),
            cell / Mathf.Max(0.01f, lossy.z)
        );
        box.center = new Vector3(0f, 0.18f, 0f);
        box.enabled = true;
    }

    void BindPrefabSockets()
    {
        if (NeedSocket(inputSockets))
        {
            inputSockets = new[]
            {
                FindOrCreateSocket("InputSocket", SocketType.Input, new Vector3(0.5f, 0.3f, 0f))
            };
        }
        else
        {
            inputSockets[0].socketType = SocketType.Input;
        }

        if (NeedSocket(outputSockets) || CountValid(outputSockets) < 3)
        {
            outputSockets = new[]
            {
                FindOrCreateSocket("OutputSocket", SocketType.Output, new Vector3(0f, 0.3f, -0.5f)),
                FindOrCreateSocket("OutputSocket (1)", SocketType.Output, new Vector3(0f, 0.3f, 0.5f)),
                FindOrCreateSocket("OutputSocket (2)", SocketType.Output, new Vector3(-0.5f, 0.3f, 0f))
            };
        }
        else
        {
            for (int i = 0; i < outputSockets.Length; i++)
            {
                if (outputSockets[i] != null)
                    outputSockets[i].socketType = SocketType.Output;
            }
        }
    }

    static int CountValid(BuildingSocket[] sockets)
    {
        if (sockets == null)
            return 0;

        int count = 0;
        for (int i = 0; i < sockets.Length; i++)
        {
            if (sockets[i] != null)
                count++;
        }

        return count;
    }

    static bool NeedSocket(BuildingSocket[] sockets)
    {
        return sockets == null || sockets.Length == 0 || sockets[0] == null;
    }

    BuildingSocket FindOrCreateSocket(string socketName, SocketType type, Vector3 localPos)
    {
        Transform existing = transform.Find(socketName);
        GameObject go;
        if (existing != null)
        {
            go = existing.gameObject;
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

    float ResolveItemHeight()
    {
        float height = Mathf.Max(0.12f, itemHeight);
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled)
                continue;
            if (renderer.transform.name.IndexOf("BeltItem", System.StringComparison.OrdinalIgnoreCase) >= 0)
                continue;
            if (renderer.transform.GetComponentInParent<BuildingSocket>() != null)
                continue;

            float surface = renderer.bounds.max.y - transform.position.y;
            if (surface > 0.05f && surface < 1.25f)
                height = Mathf.Max(height, surface + 0.06f);
        }

        return height;
    }

    protected override void LateUpdate()
    {
    }

    protected override void OnDestroy()
    {
        ClearCargo();
        base.OnDestroy();
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!showDebug)
            return;

        Vector3 pos = transform.position + Vector3.up * 0.25f;
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(pos, BuildingLinker.CardinalToWorld(InputOutward()) * 0.6f);
        if (outputSockets == null)
            return;

        Gizmos.color = Color.cyan;
        for (int i = 0; i < outputSockets.Length; i++)
        {
            if (outputSockets[i] != null)
                Gizmos.DrawRay(pos, outputSockets[i].GetOutward() * 0.8f);
        }
    }
#endif
}
