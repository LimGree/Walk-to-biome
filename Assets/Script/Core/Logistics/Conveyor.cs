using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Один объект конвейера. Форма (прямой / угол) — только визуал по соседям.
/// Объект никогда не пересоздаётся при смене формы.
/// </summary>
public class Conveyor : BuildingBase
{
    [Header("Belt")]
    public float speed = 2.5f;
    public int maxItems = 3;
    public float itemHeight = 0.42f;
    public float itemScale = 0.38f;

    [Header("Debug")]
    public bool showDebug;

    public Vector2Int Cell => BuildingLinker.WorldToCell(transform.position);
    public Vector2Int ExitDir { get; private set; } = new Vector2Int(0, 1);
    public Vector2Int EntryDir { get; private set; } = new Vector2Int(0, 1);
    public bool IsCorner => EntryDir != ExitDir;

    public BuildingSocket InputSocket =>
        inputSockets != null && inputSockets.Length > 0 ? inputSockets[0] : null;

    public BuildingSocket OutputSocket =>
        outputSockets != null && outputSockets.Length > 0 ? outputSockets[0] : null;

    bool isLive;
    GameObject straightVisual;
    GameObject cornerVisual;
    readonly List<BeltCargo> cargo = new List<BeltCargo>(4);

    class BeltCargo
    {
        public ItemData item;
        public float progress;
        public Transform visual;
    }

    void Awake()
    {
        EnsureSetup();
        RefreshDirectionsFromTransform();
    }

    public override void OnPlaced()
    {
        isLive = true;
        EnsureSetup();
        RefreshDirectionsFromTransform();
        base.OnPlaced();
    }

    public override void OnRemoved()
    {
        isLive = false;
        ClearCargo();
        base.OnRemoved();
    }

    public override void OnRotated()
    {
        RefreshDirectionsFromTransform();
        base.OnRotated();
    }

    public void PreparePreview(BuildingData previewData)
    {
        isLive = false;
        if (previewData != null)
            data = previewData;
        EnsureSetup();
    }

    public void Preview(Vector3 worldPos, Quaternion rotation)
    {
        isLive = false;
        transform.SetPositionAndRotation(worldPos, rotation);
        RefreshDirectionsFromTransform();
        EntryDir = DetectEntryDir(worldPos, ExitDir);
        ApplyVisual();
        PlaceSockets();
    }

    public void RefreshShape()
    {
        EnsureSetup();
        RefreshDirectionsFromTransform();
        EntryDir = DetectEntryDir(transform.position, ExitDir);
        ApplyVisual();
        PlaceSockets();
    }

    public bool IsFedBy(BuildingBase source)
    {
        if (source == null)
            return false;

        Vector2Int sourceCell = Cell - EntryDir;
        return BuildingLinker.OccupiesCell(source, sourceCell);
    }

    public override bool CanAcceptFrom(BuildingBase source)
    {
        return IsFedBy(source);
    }

    public override bool TryReceiveItem(ItemData item, BuildingSocket fromSocket)
    {
        if (!isLive || item == null || !CanAccept())
            return false;

        SpawnCargo(item, 0f, null);
        return true;
    }

    public bool TryAcceptTransfer(ItemData item, Transform visual)
    {
        if (!isLive || item == null || !CanAccept())
            return false;

        SpawnCargo(item, 0f, visual);
        return true;
    }

    float ItemGap => 1f / Mathf.Max(1, maxItems);

    bool CanAccept()
    {
        if (cargo.Count >= Mathf.Max(1, maxItems))
            return false;

        float nearestToEntry = 1f;
        for (int i = 0; i < cargo.Count; i++)
        {
            if (cargo[i].progress < nearestToEntry)
                nearestToEntry = cargo[i].progress;
        }

        return nearestToEntry >= ItemGap;
    }

    void Update()
    {
        if (!isLive)
            return;

        float cell = GridFootprint.CellSize;
        float move = speed * Time.deltaTime / Mathf.Max(0.05f, cell);
        float gap = ItemGap;

        // Ближе к выходу — раньше. Никто не обгоняет соседа впереди.
        cargo.Sort(CompareByProgressDesc);

        for (int i = 0; i < cargo.Count; i++)
        {
            float limit = i == 0 ? 1f : cargo[i - 1].progress - gap;
            if (limit < 0f)
                limit = 0f;

            BeltCargo item = cargo[i];
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

    static int CompareByProgressDesc(BeltCargo a, BeltCargo b)
    {
        return b.progress.CompareTo(a.progress);
    }

    bool TryHandOff(BeltCargo item)
    {
        Vector2Int nextCell = Cell + ExitDir;
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

        DestroyVisual(item.visual);
        item.visual = null;
        return true;
    }

    void SpawnCargo(ItemData item, float progress, Transform existingVisual)
    {
        BeltCargo cargoItem = new BeltCargo
        {
            item = item,
            progress = progress,
            visual = existingVisual
        };

        if (cargoItem.visual == null)
            cargoItem.visual = CreateItemVisual(item);
        else
            PrepareExistingVisual(cargoItem.visual);

        cargo.Add(cargoItem);
        UpdateCargoVisual(cargoItem);
    }

    void UpdateCargoVisual(BeltCargo item)
    {
        if (item.visual == null)
            return;

        item.visual.position = EvaluatePath(item.progress);

        SpriteRenderer sprite = item.visual.GetComponent<SpriteRenderer>();
        if (sprite != null)
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                Vector3 toCam = item.visual.position - cam.transform.position;
                if (toCam.sqrMagnitude > 0.0001f)
                    item.visual.rotation = Quaternion.LookRotation(toCam.normalized, Vector3.up);
            }
            return;
        }

        Vector3 look = EvaluatePath(Mathf.Min(1f, item.progress + 0.05f)) - item.visual.position;
        look.y = 0f;
        if (look.sqrMagnitude > 0.0001f)
            item.visual.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
    }

    Vector3 EvaluatePath(float t)
    {
        float cell = GridFootprint.CellSize;
        Vector3 up = Vector3.up * itemHeight;
        Vector3 start = transform.position - BuildingLinker.CardinalToWorld(EntryDir) * (cell * 0.5f) + up;
        Vector3 mid = transform.position + up;
        Vector3 end = transform.position + BuildingLinker.CardinalToWorld(ExitDir) * (cell * 0.5f) + up;

        t = Mathf.Clamp01(t);
        if (!IsCorner)
            return Vector3.Lerp(start, end, t);

        if (t < 0.5f)
            return Vector3.Lerp(start, mid, t * 2f);
        return Vector3.Lerp(mid, end, (t - 0.5f) * 2f);
    }

    Transform CreateItemVisual(ItemData item)
    {
        GameObject root = new GameObject(item != null ? "BeltItem_" + item.id : "BeltItem");
        bool built = TryAttachWorldModel(root, item) || TryAttachIcon(root, item);
        if (!built)
            AttachFallbackCube(root);

        DisableColliders(root);
        return root.transform;
    }

    bool TryAttachWorldModel(GameObject root, ItemData item)
    {
        if (item == null || item.worldPrefab == null)
            return false;

        GameObject model = Instantiate(item.worldPrefab, root.transform);
        model.name = "Model";
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        DisableColliders(model);

        if (!FitChildToSize(root.transform, model.transform, itemScale))
        {
            Destroy(model);
            return false;
        }

        return true;
    }

    bool TryAttachIcon(GameObject root, ItemData item)
    {
        if (item == null || item.icon == null)
            return false;

        SpriteRenderer sr = root.AddComponent<SpriteRenderer>();
        sr.sprite = item.icon;
        sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        sr.receiveShadows = false;

        float maxDim = Mathf.Max(item.icon.bounds.size.x, item.icon.bounds.size.y, 0.001f);
        root.transform.localScale = Vector3.one * (itemScale / maxDim);
        return true;
    }

    void AttachFallbackCube(GameObject root)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "Cube";
        cube.transform.SetParent(root.transform, false);
        cube.transform.localScale = Vector3.one * itemScale;
        DisableColliders(cube);
    }

    static bool FitChildToSize(Transform root, Transform child, float targetSize)
    {
        Renderer[] renderers = child.GetComponentsInChildren<Renderer>();
        if (renderers == null || renderers.Length == 0)
            return false;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                bounds.Encapsulate(renderers[i].bounds);
        }

        float maxDim = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
        if (maxDim < 0.0001f)
            return false;

        float scale = targetSize / maxDim;
        child.localScale *= scale;

        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                bounds.Encapsulate(renderers[i].bounds);
        }

        child.position += root.position - bounds.center;
        return true;
    }

    static void DisableColliders(GameObject go)
    {
        if (go == null)
            return;

        Collider[] cols = go.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] != null)
                cols[i].enabled = false;
        }
    }

    void PrepareExistingVisual(Transform visual)
    {
        if (visual == null)
            return;

        visual.SetParent(null, true);
        DisableColliders(visual.gameObject);
    }

    void DestroyVisual(Transform visual)
    {
        if (visual != null)
            Destroy(visual.gameObject);
    }

    void ClearCargo()
    {
        for (int i = 0; i < cargo.Count; i++)
            DestroyVisual(cargo[i].visual);
        cargo.Clear();
    }

    void RefreshDirectionsFromTransform()
    {
        ExitDir = BuildingLinker.ToCardinal(transform.forward);
        if (EntryDir.x == 0 && EntryDir.y == 0)
            EntryDir = ExitDir;
    }

    Vector2Int DetectEntryDir(Vector3 worldPos, Vector2Int exitDir)
    {
        Vector2Int cell = BuildingLinker.WorldToCell(worldPos);
        Vector2Int back = new Vector2Int(-exitDir.x, -exitDir.y);
        Vector2Int left = new Vector2Int(-exitDir.y, exitDir.x);
        Vector2Int right = new Vector2Int(exitDir.y, -exitDir.x);

        if (HasIncomingFrom(cell + back, cell))
            return exitDir;
        if (HasIncomingFrom(cell + left, cell))
            return new Vector2Int(-left.x, -left.y);
        if (HasIncomingFrom(cell + right, cell))
            return new Vector2Int(-right.x, -right.y);

        return exitDir;
    }

    bool HasIncomingFrom(Vector2Int neighborCell, Vector2Int selfCell)
    {
        BuildingBase other = BuildingLinker.GetBuildingAt(neighborCell);
        if (other == null || other == this)
            return false;

        return BuildingLinker.HasOutputToward(other, selfCell);
    }

    void ApplyVisual()
    {
        EnsureSetup();

        bool useCorner = IsCorner && cornerVisual != null;

        if (straightVisual != null)
            straightVisual.SetActive(!useCorner);

        if (cornerVisual != null)
        {
            cornerVisual.SetActive(useCorner);
            if (useCorner)
                OrientCornerVisual();
        }
    }

    void OrientCornerVisual()
    {
        Vector3 entry = BuildingLinker.CardinalToWorld(EntryDir);
        Vector3 exit = BuildingLinker.CardinalToWorld(ExitDir);
        float turnY = Vector3.Cross(entry, exit).y;

        // Модель угла: вход local -Z, выход local +X (поворот направо).
        if (turnY >= 0f)
        {
            cornerVisual.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
            cornerVisual.transform.localScale = Vector3.one;
        }
        else
        {
            cornerVisual.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            cornerVisual.transform.localScale = new Vector3(-1f, 1f, 1f);
        }
    }

    void PlaceSockets()
    {
        EnsureSockets();

        if (OutputSocket != null)
            OutputSocket.transform.localPosition = new Vector3(0f, 0.3f, 0.5f);

        if (InputSocket != null)
        {
            Vector3 localEntry = transform.InverseTransformDirection(BuildingLinker.CardinalToWorld(EntryDir));
            InputSocket.transform.localPosition = new Vector3(
                -Mathf.Round(localEntry.x) * 0.5f,
                0.3f,
                -Mathf.Round(localEntry.z) * 0.5f);
        }
    }

    void EnsureSetup()
    {
        EnsureCollider();
        EnsureSockets();
        EnsureVisuals();
    }

    void EnsureCollider()
    {
        Collider[] childCols = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < childCols.Length; i++)
        {
            if (childCols[i] != null && childCols[i].gameObject != gameObject)
                childCols[i].enabled = false;
        }

        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null)
            box = gameObject.AddComponent<BoxCollider>();

        // Коллайдер должен оставаться ВНУТРИ своей клетки (учёта scale префаба 0.8/0.9).
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

    void EnsureSockets()
    {
        if (NeedSocket(outputSockets, SocketType.Output))
        {
            outputSockets = new[]
            {
                CreateSocket("OutputSocket", SocketType.Output, new Vector3(0f, 0.3f, 0.5f))
            };
        }

        if (NeedSocket(inputSockets, SocketType.Input))
        {
            inputSockets = new[]
            {
                CreateSocket("InputSocket", SocketType.Input, new Vector3(0f, 0.3f, -0.5f))
            };
        }
    }

    static bool NeedSocket(BuildingSocket[] sockets, SocketType type)
    {
        if (sockets == null || sockets.Length == 0 || sockets[0] == null)
            return true;
        sockets[0].socketType = type;
        return false;
    }

    BuildingSocket CreateSocket(string socketName, SocketType type, Vector3 localPos)
    {
        Transform existing = transform.Find(socketName);
        GameObject go = existing != null ? existing.gameObject : new GameObject(socketName);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.identity;

        BuildingSocket socket = go.GetComponent<BuildingSocket>();
        if (socket == null)
            socket = go.AddComponent<BuildingSocket>();
        socket.socketType = type;
        return socket;
    }

    void EnsureVisuals()
    {
        if (straightVisual == null)
            straightVisual = FindStraightVisual();

        if (cornerVisual == null)
            CreateCornerVisual();
    }

    GameObject FindStraightVisual()
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child == null)
                continue;

            string n = child.name;
            if (n.IndexOf("Corner", System.StringComparison.OrdinalIgnoreCase) >= 0)
                continue;
            if (n.IndexOf("Socket", System.StringComparison.OrdinalIgnoreCase) >= 0)
                continue;
            if (n.IndexOf("Point", System.StringComparison.OrdinalIgnoreCase) >= 0)
                continue;
            if (n.IndexOf("BeltItem", System.StringComparison.OrdinalIgnoreCase) >= 0)
                continue;
            if (child.GetComponentInChildren<Renderer>() == null)
                continue;

            return child.gameObject;
        }

        return null;
    }

    void CreateCornerVisual()
    {
        GameObject src = data != null ? data.cornerPrefab : null;
        if (src == null)
            return;

        cornerVisual = Instantiate(src, transform);
        cornerVisual.name = "CornerVisual";
        cornerVisual.transform.localPosition = Vector3.zero;
        cornerVisual.transform.localRotation = Quaternion.identity;
        cornerVisual.transform.localScale = Vector3.one;
        StripRuntimeComponents(cornerVisual);
        cornerVisual.SetActive(false);
    }

    static void StripRuntimeComponents(GameObject root)
    {
        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            Destroy(colliders[i]);

        MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] != null)
                Destroy(behaviours[i]);
        }
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

        Vector3 pos = transform.position + Vector3.up * 0.2f;
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(pos, BuildingLinker.CardinalToWorld(ExitDir));
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(pos, -BuildingLinker.CardinalToWorld(EntryDir) * 0.6f);
    }
#endif
}
