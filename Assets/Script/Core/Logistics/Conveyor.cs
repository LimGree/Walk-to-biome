using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Форма ленты по входам. Выход всегда вперёд (поворот всего здания).
/// </summary>
public enum BeltShape
{
    Straight,
    Corner,
    Tee,
    Sides,
    Triple
}

/// <summary>
/// Один объект конвейера. Форма (прямой / угол / T / бока / три входа) — только визуал по соседям.
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
    public BeltShape Shape { get; private set; } = BeltShape.Straight;
    public bool IsCorner => Shape == BeltShape.Corner;

    public BuildingSocket InputSocket =>
        inputSockets != null && inputSockets.Length > 0 ? inputSockets[0] : null;

    public BuildingSocket OutputSocket =>
        outputSockets != null && outputSockets.Length > 0 ? outputSockets[0] : null;

    bool isLive;
    bool isPreview;
    bool fromBack;
    bool fromLeft;
    bool fromRight;
    GameObject straightVisual;
    GameObject cornerVisual;
    GameObject teeVisual;
    GameObject sidesVisual;
    GameObject tripleVisual;
    Vector3 cornerAuthScale = Vector3.one;
    Vector3 teeAuthScale = Vector3.one;
    readonly List<BeltCargo> cargo = new List<BeltCargo>(4);

    class BeltCargo
    {
        public ItemData item;
        public float progress;
        public Transform visual;
        public Vector2Int entryDir;
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
        isPreview = true;
        isLive = false;
        if (previewData != null)
            data = previewData;
        DestroyCreatedVisuals();
        EnsureSetup();
    }

    public void Preview(Vector3 worldPos, Quaternion rotation)
    {
        isLive = false;
        transform.SetPositionAndRotation(worldPos, rotation);
        RefreshDirectionsFromTransform();
        DetectIncoming(worldPos, ExitDir);
        ApplyVisual();
        PlaceSockets();
    }

    public void RefreshShape()
    {
        EnsureSetup();
        RefreshDirectionsFromTransform();
        DetectIncoming(transform.position, ExitDir);
        ApplyVisual();
        PlaceSockets();
    }

    public bool IsFedBy(BuildingBase source)
    {
        if (source == null)
            return false;

        if (OccupiesInputSide(source))
            return true;

        return BuildingLinker.IsAdjacentTo(source, Cell)
            && BuildingLinker.HasOutputToward(source, Cell);
    }

    public BuildingSocket GetInputFrom(BuildingBase source)
    {
        if (source == null || inputSockets == null)
            return InputSocket;

        for (int i = 0; i < inputSockets.Length; i++)
        {
            BuildingSocket socket = inputSockets[i];
            if (socket == null)
                continue;
            if (BuildingLinker.OccupiesCell(source, BuildingLinker.GetSocketFrontCell(socket)))
                return socket;
        }

        return InputSocket;
    }

    public override bool CanAcceptFrom(BuildingBase source)
    {
        return IsFedBy(source);
    }

    protected virtual bool AcceptsItem(ItemData item)
    {
        return item != null && !item.isFluid;
    }

    protected virtual bool ShowCargoVisual => true;

    public override bool TryReceiveItem(ItemData item, BuildingSocket fromSocket)
    {
        if (!isLive || !AcceptsItem(item) || !CanAccept())
            return false;

        SpawnCargo(item, 0f, null, InferEntryDir(fromSocket, null));
        return true;
    }

    public bool TryAcceptTransfer(ItemData item, Transform visual, BuildingBase source = null)
    {
        if (!isLive || !AcceptsItem(item) || !CanAccept())
            return false;

        SpawnCargo(item, 0f, visual, InferEntryDir(null, source, visual));
        return true;
    }

    public bool HasRoomForItem()
    {
        return isLive && CanAccept();
    }

    public bool TryStealMatching(ItemData filter, out ItemData item, out Transform visual)
    {
        item = null;
        visual = null;
        if (!isLive)
            return false;

        int best = -1;
        float bestProgress = -1f;
        for (int i = 0; i < cargo.Count; i++)
        {
            BeltCargo entry = cargo[i];
            if (entry == null || entry.item == null)
                continue;
            if (filter != null && entry.item != filter)
                continue;
            if (entry.progress > bestProgress)
            {
                best = i;
                bestProgress = entry.progress;
            }
        }

        if (best < 0)
            return false;

        BeltCargo stolen = cargo[best];
        item = stolen.item;
        visual = stolen.visual;
        cargo.RemoveAt(best);
        if (visual != null)
            visual.SetParent(null, true);
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
        float boost = BeltSpeedSystem.Instance != null ? BeltSpeedSystem.Instance.Multiplier : 1f;
        float move = speed * boost * Time.deltaTime / Mathf.Max(0.05f, cell);
        float gap = ItemGap;

        for (int i = 0; i < cargo.Count; i++)
        {
            float limit = 1f;
            float progress = cargo[i].progress;
            for (int j = 0; j < cargo.Count; j++)
            {
                if (j == i)
                    continue;
                float other = cargo[j].progress;
                if (other > progress)
                    limit = Mathf.Min(limit, other - gap);
            }
            if (limit < 0f)
                limit = 0f;
            if (progress < limit)
                cargo[i].progress = Mathf.Min(limit, progress + move);
        }

        int front = -1;
        float best = 0.999f;
        for (int i = 0; i < cargo.Count; i++)
        {
            if (cargo[i].progress >= best)
            {
                best = cargo[i].progress;
                front = i;
            }
        }
        if (front >= 0)
        {
            if (TryHandOff(cargo[front]))
                cargo.RemoveAt(front);
            else
                cargo[front].progress = 1f;
        }

        RefreshCargoVisuals();
    }

    void RefreshCargoVisuals()
    {
        bool show = ShowCargoVisual && WorldView.InRange(transform.position);
        for (int i = 0; i < cargo.Count; i++)
        {
            BeltCargo item = cargo[i];
            if (show)
            {
                if (item.visual == null)
                    item.visual = CreateItemVisual(item.item);
                UpdateCargoVisual(item);
            }
            else if (item.visual != null)
            {
                DestroyVisual(item.visual);
                item.visual = null;
            }
        }
    }

    bool TryHandOff(BeltCargo item)
    {
        Vector2Int nextCell = Cell + ExitDir;
        BuildingBase dest = BuildingLinker.GetBuildingAt(nextCell);
        if (dest == null)
            return false;

        Conveyor nextBelt = dest as Conveyor;
        if (nextBelt != null)
            return nextBelt.TryAcceptTransfer(item.item, item.visual, this);

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

    void SpawnCargo(ItemData item, float progress, Transform existingVisual, Vector2Int entryDir)
    {
        if (entryDir.x == 0 && entryDir.y == 0)
            entryDir = ExitDir;

        BeltCargo cargoItem = new BeltCargo
        {
            item = item,
            progress = progress,
            visual = existingVisual,
            entryDir = entryDir
        };

        bool show = ShowCargoVisual && WorldView.InRange(transform.position);
        if (show)
        {
            if (cargoItem.visual == null)
                cargoItem.visual = CreateItemVisual(item);
            else
                PrepareExistingVisual(cargoItem.visual);
        }
        else if (cargoItem.visual != null)
        {
            DestroyVisual(cargoItem.visual);
            cargoItem.visual = null;
        }

        cargo.Add(cargoItem);
        if (cargoItem.visual != null)
            UpdateCargoVisual(cargoItem);
    }

    void UpdateCargoVisual(BeltCargo item)
    {
        if (item.visual == null)
            return;

        item.visual.position = EvaluatePath(item.progress, item.entryDir);

        SpriteRenderer sprite = item.visual.GetComponent<SpriteRenderer>();
        if (sprite != null)
        {
            Camera cam = WorldView.Cam;
            if (cam != null)
            {
                Vector3 toCam = item.visual.position - cam.transform.position;
                if (toCam.sqrMagnitude > 0.0001f)
                    item.visual.rotation = Quaternion.LookRotation(toCam.normalized, Vector3.up);
            }
            return;
        }

        Vector3 look = EvaluatePath(Mathf.Min(1f, item.progress + 0.05f), item.entryDir) - item.visual.position;
        look.y = 0f;
        if (look.sqrMagnitude > 0.0001f)
            item.visual.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
    }

    Vector3 EvaluatePath(float t)
    {
        return EvaluatePath(t, EntryDir);
    }

    Vector3 EvaluatePath(float t, Vector2Int entryDir)
    {
        if (entryDir.x == 0 && entryDir.y == 0)
            entryDir = ExitDir;

        float cell = GridFootprint.CellSize;
        Vector3 up = Vector3.up * itemHeight;
        Vector3 start = transform.position - BuildingLinker.CardinalToWorld(entryDir) * (cell * 0.5f) + up;
        Vector3 mid = transform.position + up;
        Vector3 end = transform.position + BuildingLinker.CardinalToWorld(ExitDir) * (cell * 0.5f) + up;

        t = Mathf.Clamp01(t);
        if (entryDir == ExitDir)
            return Vector3.Lerp(start, end, t);

        if (t < 0.5f)
            return Vector3.Lerp(start, mid, t * 2f);
        return Vector3.Lerp(mid, end, (t - 0.5f) * 2f);
    }

    Transform CreateItemVisual(ItemData item)
    {
        return BeltItemView.Create(item, itemScale);
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
        BeltItemView.ApplyWorldCullLayer(visual.gameObject);
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

    void DetectIncoming(Vector3 worldPos, Vector2Int exitDir)
    {
        Vector2Int cell = BuildingLinker.WorldToCell(worldPos);
        Vector2Int back = new Vector2Int(-exitDir.x, -exitDir.y);
        Vector2Int left = new Vector2Int(-exitDir.y, exitDir.x);
        Vector2Int right = new Vector2Int(exitDir.y, -exitDir.x);

        fromBack = HasIncomingFrom(cell + back, cell);
        fromLeft = HasIncomingFrom(cell + left, cell);
        fromRight = HasIncomingFrom(cell + right, cell);

        if (fromBack && fromLeft && fromRight)
            Shape = BeltShape.Triple;
        else if (fromLeft && fromRight)
            Shape = BeltShape.Sides;
        else if (fromBack && (fromLeft || fromRight))
            Shape = BeltShape.Tee;
        else if (fromLeft || fromRight)
            Shape = BeltShape.Corner;
        else
            Shape = BeltShape.Straight;

        if (fromBack)
            EntryDir = exitDir;
        else if (fromLeft)
            EntryDir = new Vector2Int(-left.x, -left.y);
        else if (fromRight)
            EntryDir = new Vector2Int(-right.x, -right.y);
        else
            EntryDir = exitDir;
    }

    bool HasIncomingFrom(Vector2Int neighborCell, Vector2Int selfCell)
    {
        BuildingBase other = BuildingLinker.GetBuildingAt(neighborCell);
        if (other == null || other == this)
            return false;

        return BuildingLinker.HasOutputToward(other, selfCell);
    }

    bool OccupiesInputSide(BuildingBase source)
    {
        Vector2Int back = new Vector2Int(-ExitDir.x, -ExitDir.y);
        Vector2Int left = new Vector2Int(-ExitDir.y, ExitDir.x);
        Vector2Int right = new Vector2Int(ExitDir.y, -ExitDir.x);
        return BuildingLinker.OccupiesCell(source, Cell + back)
            || BuildingLinker.OccupiesCell(source, Cell + left)
            || BuildingLinker.OccupiesCell(source, Cell + right);
    }

    Vector2Int InferEntryDir(BuildingSocket fromSocket, BuildingBase source, Transform visual = null)
    {
        if (source != null)
        {
            Vector2Int fromSource = EntryDirFromSource(source);
            if (fromSource.x != 0 || fromSource.y != 0)
                return fromSource;
        }

        if (fromSocket != null)
        {
            BuildingBase owner = fromSocket.Owner;
            if (owner != null && owner != this)
            {
                Vector2Int fromOwner = EntryDirFromSource(owner);
                if (fromOwner.x != 0 || fromOwner.y != 0)
                    return fromOwner;
            }
        }

        if (visual != null)
        {
            Vector2Int fromWorld = EntryDirFromWorld(visual.position);
            if (fromWorld.x != 0 || fromWorld.y != 0)
                return fromWorld;
        }

        return ExitDir;
    }

    Vector2Int EntryDirFromSource(BuildingBase source)
    {
        if (source == null)
            return Vector2Int.zero;

        Vector2Int back = new Vector2Int(-ExitDir.x, -ExitDir.y);
        Vector2Int left = new Vector2Int(-ExitDir.y, ExitDir.x);
        Vector2Int right = new Vector2Int(ExitDir.y, -ExitDir.x);

        if (BuildingLinker.OccupiesCell(source, Cell + back))
            return ExitDir;
        if (BuildingLinker.OccupiesCell(source, Cell + left))
            return new Vector2Int(-left.x, -left.y);
        if (BuildingLinker.OccupiesCell(source, Cell + right))
            return new Vector2Int(-right.x, -right.y);

        return EntryDirFromWorld(source.transform.position);
    }

    Vector2Int EntryDirFromWorld(Vector3 world)
    {
        Vector2Int fromCell = BuildingLinker.WorldToCell(world);
        Vector2Int delta = Cell - fromCell;
        Vector2Int fromFront = new Vector2Int(-ExitDir.x, -ExitDir.y);
        if (Mathf.Abs(delta.x) + Mathf.Abs(delta.y) == 1)
        {
            if (delta == fromFront)
                return Vector2Int.zero;
            return delta;
        }

        Vector3 local = world - transform.position;
        local.y = 0f;
        if (local.sqrMagnitude < 0.0001f)
            return Vector2Int.zero;

        Vector2Int inward = BuildingLinker.ToCardinal(local);
        Vector2Int travel = new Vector2Int(-inward.x, -inward.y);
        if (travel == fromFront)
            return Vector2Int.zero;
        return travel;
    }

    void ApplyVisual()
    {
        EnsureSetup();

        BeltShape shown = ResolveShownShape();

        if (straightVisual != null)
            straightVisual.SetActive(shown == BeltShape.Straight);

        SetShapeVisual(cornerVisual, shown == BeltShape.Corner, OrientCornerVisual);
        SetShapeVisual(teeVisual, shown == BeltShape.Tee, OrientTeeVisual);
        SetShapeVisual(sidesVisual, shown == BeltShape.Sides, null);
        SetShapeVisual(tripleVisual, shown == BeltShape.Triple, null);
    }

    BeltShape ResolveShownShape()
    {
        BeltShape shown = Shape;
        if (shown == BeltShape.Triple && tripleVisual == null)
        {
            if (sidesVisual != null && fromLeft && fromRight)
                return BeltShape.Sides;
            if (teeVisual != null && (fromLeft || fromRight))
                return BeltShape.Tee;
            if (cornerVisual != null && (fromLeft || fromRight) && !fromBack)
                return BeltShape.Corner;
            return BeltShape.Straight;
        }

        if (shown == BeltShape.Sides && sidesVisual == null)
            return BeltShape.Straight;

        if (shown == BeltShape.Tee && teeVisual == null)
        {
            if (cornerVisual != null && !fromBack)
                return BeltShape.Corner;
            return BeltShape.Straight;
        }

        if (shown == BeltShape.Corner && cornerVisual == null)
            return BeltShape.Straight;

        return shown;
    }

    static void SetShapeVisual(GameObject visual, bool on, System.Action orient)
    {
        if (visual == null)
            return;
        visual.SetActive(on);
        if (on && orient != null)
            orient();
    }

    void OrientCornerVisual()
    {
        if (cornerVisual == null)
            return;

        Vector3 entry = BuildingLinker.CardinalToWorld(EntryDir);
        Vector3 exit = BuildingLinker.CardinalToWorld(ExitDir);
        float turnY = Vector3.Cross(entry, exit).y;

        // Модель угла: вход local -Z, выход local +X (поворот направо).
        if (turnY >= 0f)
        {
            cornerVisual.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
            ApplyAuthScale(cornerVisual.transform, cornerAuthScale, false);
        }
        else
        {
            cornerVisual.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            ApplyAuthScale(cornerVisual.transform, cornerAuthScale, true);
        }
    }

    void OrientTeeVisual()
    {
        if (teeVisual == null)
            return;

        // Модель T: выход +Z, зад -Z, боковой вход +X. Зеркало по X для левого бока.
        ApplyAuthScale(teeVisual.transform, teeAuthScale, fromLeft && !fromRight);
    }

    static void ApplyAuthScale(Transform t, Vector3 auth, bool mirrorX)
    {
        if (t == null)
            return;
        if (Mathf.Abs(auth.x) < 0.0001f && Mathf.Abs(auth.y) < 0.0001f && Mathf.Abs(auth.z) < 0.0001f)
            auth = Vector3.one;
        t.localScale = new Vector3(
            mirrorX ? -Mathf.Abs(auth.x) : Mathf.Abs(auth.x),
            auth.y,
            auth.z);
    }

    void PlaceSockets()
    {
        EnsureSockets();

        if (OutputSocket != null)
            OutputSocket.transform.localPosition = new Vector3(0f, 0.3f, 0.5f);

        PlaceNamedSocket("StartPoint", new Vector3(0f, 0.3f, -0.5f));
        PlaceNamedSocket("StartPointLeft", new Vector3(-0.5f, 0.3f, 0f));
        PlaceNamedSocket("StartPointRight", new Vector3(0.5f, 0.3f, 0f));
        PlaceNamedSocket("InputSocket", new Vector3(0f, 0.3f, -0.5f));
    }

    void PlaceNamedSocket(string socketName, Vector3 localPos)
    {
        Transform t = transform.Find(socketName);
        if (t != null)
            t.localPosition = localPos;
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
        BuildingSocket output = FindOrCreateSocket("EndPoint", SocketType.Output, new Vector3(0f, 0.3f, 0.5f));
        if (output == null)
            output = FindOrCreateSocket("OutputSocket", SocketType.Output, new Vector3(0f, 0.3f, 0.5f));
        outputSockets = new[] { output };

        BuildingSocket back = FindOrCreateSocket("StartPoint", SocketType.Input, new Vector3(0f, 0.3f, -0.5f));
        if (back == null)
            back = FindOrCreateSocket("InputSocket", SocketType.Input, new Vector3(0f, 0.3f, -0.5f));
        BuildingSocket left = FindOrCreateSocket("StartPointLeft", SocketType.Input, new Vector3(-0.5f, 0.3f, 0f));
        BuildingSocket right = FindOrCreateSocket("StartPointRight", SocketType.Input, new Vector3(0.5f, 0.3f, 0f));
        inputSockets = new[] { back, left, right };
    }

    BuildingSocket FindOrCreateSocket(string socketName, SocketType type, Vector3 localPos)
    {
        Transform existing = transform.Find(socketName);
        if (existing == null && socketName == "EndPoint")
            existing = transform.Find("OutputSocket");
        if (existing == null && socketName == "StartPoint")
            existing = transform.Find("InputSocket");

        GameObject go = existing != null ? existing.gameObject : new GameObject(socketName);
        go.name = socketName;
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
        {
            cornerVisual = CreateChildVisual("CornerVisual", data != null ? data.cornerPrefab : null, data != null ? data.cornerGhostPrefab : null);
            cornerAuthScale = ReadAuthScale(cornerVisual);
        }
        if (teeVisual == null)
        {
            teeVisual = CreateChildVisual("TeeVisual", data != null ? data.teePrefab : null, data != null ? data.teeGhostPrefab : null);
            teeAuthScale = ReadAuthScale(teeVisual);
        }
        if (sidesVisual == null)
            sidesVisual = CreateChildVisual("SidesVisual", data != null ? data.sidesPrefab : null, data != null ? data.sidesGhostPrefab : null);
        if (tripleVisual == null)
            tripleVisual = CreateChildVisual("TripleVisual", data != null ? data.triplePrefab : null, data != null ? data.tripleGhostPrefab : null);
    }

    void DestroyCreatedVisuals()
    {
        DestroyVisualRoot(ref cornerVisual);
        DestroyVisualRoot(ref teeVisual);
        DestroyVisualRoot(ref sidesVisual);
        DestroyVisualRoot(ref tripleVisual);
    }

    static void DestroyVisualRoot(ref GameObject visual)
    {
        if (visual == null)
            return;
        Destroy(visual);
        visual = null;
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
            if (n.IndexOf("Tee", System.StringComparison.OrdinalIgnoreCase) >= 0)
                continue;
            if (n.IndexOf("Sides", System.StringComparison.OrdinalIgnoreCase) >= 0)
                continue;
            if (n.IndexOf("Triple", System.StringComparison.OrdinalIgnoreCase) >= 0)
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

    GameObject CreateChildVisual(string visualName, GameObject livePrefab, GameObject ghostPrefab)
    {
        GameObject src = isPreview && ghostPrefab != null ? ghostPrefab : livePrefab;
        if (src == null)
            src = livePrefab;
        if (src == null)
            return null;

        GameObject visual = Instantiate(src, transform);
        visual.name = visualName;
        visual.transform.localPosition = Vector3.zero;
        MatchLayer(visual, gameObject.layer);
        StripRuntimeComponents(visual);
        visual.SetActive(false);
        return visual;
    }

    static Vector3 ReadAuthScale(GameObject go)
    {
        if (go == null)
            return Vector3.one;
        Vector3 s = go.transform.localScale;
        return new Vector3(Mathf.Abs(s.x), s.y, s.z);
    }

    static void MatchLayer(GameObject go, int layer)
    {
        if (go == null)
            return;
        go.layer = layer;
        Transform t = go.transform;
        for (int i = 0; i < t.childCount; i++)
            MatchLayer(t.GetChild(i).gameObject, layer);
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

    public override void WriteSave(BuildingSaveData save)
    {
        base.WriteSave(save);
        if (save == null)
            return;
        save.cargo = new List<BeltItemSave>(cargo.Count);
        for (int i = 0; i < cargo.Count; i++)
        {
            BeltCargo entry = cargo[i];
            if (entry == null || entry.item == null || string.IsNullOrEmpty(entry.item.id))
                continue;
            save.cargo.Add(new BeltItemSave
            {
                itemId = entry.item.id,
                progress = entry.progress,
                entryX = entry.entryDir.x,
                entryY = entry.entryDir.y,
                exitX = ExitDir.x,
                exitY = ExitDir.y
            });
        }
    }

    public override void ReadSave(BuildingSaveData save)
    {
        base.ReadSave(save);
        ClearCargo();
        if (save == null || save.cargo == null)
            return;

        for (int i = 0; i < save.cargo.Count; i++)
        {
            BeltItemSave entry = save.cargo[i];
            ItemData item = GameDatabase.FindItem(entry.itemId);
            if (item == null)
                continue;
            Vector2Int entryDir = new Vector2Int(entry.entryX, entry.entryY);
            SpawnCargo(item, Mathf.Clamp01(entry.progress), null, entryDir);
        }
    }

    protected override void LateUpdate()
    {
        base.LateUpdate();
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
        if (fromBack)
            Gizmos.DrawRay(pos, -BuildingLinker.CardinalToWorld(ExitDir) * 0.6f);
        if (fromLeft)
        {
            Vector2Int left = new Vector2Int(-ExitDir.y, ExitDir.x);
            Gizmos.DrawRay(pos, BuildingLinker.CardinalToWorld(left) * 0.6f);
        }
        if (fromRight)
        {
            Vector2Int right = new Vector2Int(ExitDir.y, -ExitDir.x);
            Gizmos.DrawRay(pos, BuildingLinker.CardinalToWorld(right) * 0.6f);
        }
    }
#endif
}
