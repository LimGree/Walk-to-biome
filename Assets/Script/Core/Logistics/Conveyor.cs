using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Клетка ленты. Выход = transform.forward. Форма — таблица [[BeltRules]] по маске входов.
/// Сокетов нет: соседство + ExitDir.
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

    public static readonly Dictionary<Vector2Int, Vector2Int> PreviewExits =
        new Dictionary<Vector2Int, Vector2Int>(32);

    static readonly List<Conveyor> WorldVisualOverride = new List<Conveyor>(16);

    public Vector2Int Cell => BuildingLinker.WorldToCell(transform.position);
    public Vector2Int ExitDir { get; private set; } = new Vector2Int(0, 1);
    public BeltInMask InMask { get; private set; }
    public BeltShape Shape { get; private set; } = BeltShape.Straight;
    public bool IsPreview => isPreview;
    public bool FromBack => BeltRules.Has(InMask, BeltInMask.Back);
    public bool FromLeft => BeltRules.Has(InMask, BeltInMask.Left);
    public bool FromRight => BeltRules.Has(InMask, BeltInMask.Right);

    public struct Incoming
    {
        public BeltInMask mask;
        public BeltShape shape;
    }

    bool isLive;
    bool isPreview;
    BeltInMask lastServed = BeltInMask.None;
    GameObject straightVisual;
    GameObject cornerVisual;
    GameObject teeVisual;
    GameObject sidesVisual;
    GameObject tripleVisual;
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
        RefreshExitFromTransform();
        ClearBeltSockets();
    }

    public override void OnPlaced()
    {
        isLive = true;
        EnsureSetup();
        RefreshExitFromTransform();
        ClearBeltSockets();
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
        RefreshExitFromTransform();
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
        ClearBeltSockets();
    }

    public void Preview(Vector3 worldPos, Quaternion rotation)
    {
        isLive = false;
        transform.SetPositionAndRotation(worldPos, rotation);
        ApplyIncoming(ComputeIncoming());
    }

    /// <summary>
    /// Только картинка. Симуляция (маска, груз) не меняется.
    /// Нужно, чтобы гост показал, какой формы станет уже стоящая лента.
    /// </summary>
    public void ShowIncomingVisual(Incoming incoming)
    {
        BeltShape simShape = Shape;
        BeltInMask simMask = InMask;
        Shape = incoming.shape;
        InMask = incoming.mask;
        ApplyVisual();
        Shape = simShape;
        InMask = simMask;
    }

    public void RestoreVisual()
    {
        ApplyVisual();
    }

    public static void RegisterPreviewExit(Vector3 worldPos, float yaw)
    {
        PreviewExits[BuildingLinker.WorldToCell(worldPos)] = BuildingLinker.ExitDirFromYaw(yaw);
    }

    public static void ApplyWorldVisualOverrides()
    {
        ClearWorldVisualOverrides();
        if (PreviewExits.Count == 0)
            return;

        var seen = new HashSet<int>();
        foreach (KeyValuePair<Vector2Int, Vector2Int> kv in PreviewExits)
        {
            Vector2Int front = kv.Key + kv.Value;
            if (PreviewExits.ContainsKey(front))
                continue;

            Conveyor belt = BuildingLinker.GetBuildingAt(front) as Conveyor;
            if (belt == null || belt.isPreview)
                continue;
            if (!seen.Add(belt.GetInstanceID()))
                continue;

            belt.ShowIncomingVisual(belt.ComputeIncoming());
            WorldVisualOverride.Add(belt);
        }
    }

    public static void ClearWorldVisualOverrides()
    {
        for (int i = 0; i < WorldVisualOverride.Count; i++)
        {
            if (WorldVisualOverride[i] != null)
                WorldVisualOverride[i].RestoreVisual();
        }
        WorldVisualOverride.Clear();
    }

    public Incoming ComputeIncoming()
    {
        EnsureSetup();
        RefreshExitFromTransform();
        Vector2Int cell = Cell;
        Vector2Int exit = ExitDir;
        Incoming incoming;
        incoming.mask = BeltRules.MaskFromFlags(
            NeighborFeeds(cell + BeltRules.BackNeighbor(exit), cell),
            NeighborFeeds(cell + BeltRules.LeftNeighbor(exit), cell),
            NeighborFeeds(cell + BeltRules.RightNeighbor(exit), cell));
        incoming.shape = BeltRules.Shape(incoming.mask);
        return incoming;
    }

    public void ApplyIncoming(Incoming incoming)
    {
        InMask = incoming.mask;
        Shape = incoming.shape;
        ApplyVisual();
        RemapCargoToValidEntries();
    }

    static bool NeighborFeeds(Vector2Int neighborCell, Vector2Int selfCell)
    {
        if (PreviewExits.TryGetValue(neighborCell, out Vector2Int previewExit))
            return neighborCell + previewExit == selfCell;

        BuildingBase other = BuildingLinker.GetBuildingAt(neighborCell);
        if (other == null)
            return false;
        return BuildingLinker.FeedsInto(other, selfCell);
    }

    public bool IsFedBy(BuildingBase source)
    {
        if (source == null)
            return false;
        return BuildingLinker.FeedsInto(source, Cell) && SideFromSource(source) != BeltInMask.None;
    }

    public bool AcceptsFromCell(Vector2Int fromCell)
    {
        Vector2Int delta = fromCell - Cell;
        if (Mathf.Abs(delta.x) + Mathf.Abs(delta.y) != 1)
            return false;
        return delta != ExitDir;
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
        BuildingBase source = SourceFromSocket(fromSocket);
        Vector2Int entry = InferEntryDir(fromSocket, source);
        return TryAccept(item, null, entry);
    }

    public bool TryAcceptTransfer(ItemData item, Transform visual, BuildingBase source = null)
    {
        Vector2Int entry = InferEntryDir(null, source, visual);
        return TryAccept(item, visual, entry);
    }

    bool TryAccept(ItemData item, Transform visual, Vector2Int entry)
    {
        if (!isLive || !AcceptsItem(item))
            return false;
        if (!IsValidEntry(entry))
            entry = ExitDir;
        if (!IsValidEntry(entry) || !CanAccept(entry))
            return false;
        SpawnCargo(item, 0f, visual, entry);
        return true;
    }

    public bool HasRoomForItem()
    {
        return isLive && cargo.Count < Mathf.Max(1, maxItems);
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

    bool CanAccept(Vector2Int entryDir)
    {
        if (cargo.Count >= Mathf.Max(1, maxItems))
            return false;

        float nearestToEntry = 1f;
        bool any = false;
        for (int i = 0; i < cargo.Count; i++)
        {
            if (cargo[i].entryDir != entryDir)
                continue;
            any = true;
            if (cargo[i].progress < nearestToEntry)
                nearestToEntry = cargo[i].progress;
        }

        return !any || nearestToEntry >= ItemGap;
    }

    bool IsValidEntry(Vector2Int entry)
    {
        if (entry.x == 0 && entry.y == 0)
            return false;
        return BeltRules.SideFromTravel(ExitDir, entry) != BeltInMask.None;
    }

    void Update()
    {
        if (!isLive)
            return;

        float cell = GridFootprint.CellSize;
        float boost = BeltSpeedSystem.Instance != null ? BeltSpeedSystem.Instance.Multiplier : 1f;
        float move = speed * boost * Time.deltaTime / Mathf.Max(0.05f, cell);
        float gap = ItemGap;
        BeltInMask served = NextServedSide();

        for (int i = 0; i < cargo.Count; i++)
        {
            float progress = cargo[i].progress;
            float limit = progress >= BeltRules.MergeT ? 1f : BeltRules.MergeT;
            for (int j = 0; j < cargo.Count; j++)
            {
                if (j == i)
                    continue;
                float other = cargo[j].progress;
                if (progress < BeltRules.MergeT && other >= BeltRules.MergeT)
                    continue;
                if (progress < BeltRules.MergeT
                    && cargo[j].entryDir != cargo[i].entryDir
                    && other < BeltRules.MergeT)
                    continue;
                if (other > progress)
                    limit = Mathf.Min(limit, other - gap);
            }

            if (progress < BeltRules.MergeT)
            {
                BeltInMask side = BeltRules.SideFromTravel(ExitDir, cargo[i].entryDir);
                if (served != BeltInMask.None
                    && side != BeltInMask.None
                    && side != served
                    && progress + move >= BeltRules.MergeT)
                    continue;
            }

            if (limit < 0f)
                limit = 0f;
            if (progress < limit)
                cargo[i].progress = Mathf.Min(limit, progress + move);
            if (progress < BeltRules.MergeT && cargo[i].progress >= BeltRules.MergeT && served != BeltInMask.None)
                lastServed = served;
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
                    item.visual = BeltItemView.Create(item.item, itemScale);
                UpdateCargoVisual(item);
            }
            else if (item.visual != null)
            {
                BeltItemView.Destroy(item.visual);
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

        BeltItemView.Destroy(item.visual);
        item.visual = null;
        return true;
    }

    void SpawnCargo(ItemData item, float progress, Transform existingVisual, Vector2Int entryDir)
    {
        if (!IsValidEntry(entryDir))
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
                cargoItem.visual = BeltItemView.Create(item, itemScale);
            else
                BeltItemView.Prepare(cargoItem.visual);
        }
        else if (cargoItem.visual != null)
        {
            BeltItemView.Destroy(cargoItem.visual);
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

        BeltInMask side = BeltRules.SideFromTravel(ExitDir, item.entryDir);
        Vector3 pos = BeltRules.PathWorld(transform, side, item.progress, itemHeight);
        Vector3 look = BeltRules.PathWorld(transform, side, Mathf.Min(1f, item.progress + 0.05f), itemHeight) - pos;
        BeltItemView.Update(item.visual, pos, look);
    }

    void ClearCargo()
    {
        for (int i = 0; i < cargo.Count; i++)
            BeltItemView.Destroy(cargo[i].visual);
        cargo.Clear();
    }

    BeltInMask NextServedSide()
    {
        BeltInMask waiting = BeltInMask.None;
        for (int i = 0; i < cargo.Count; i++)
        {
            if (cargo[i].progress >= BeltRules.MergeT)
                continue;
            waiting |= BeltRules.SideFromTravel(ExitDir, cargo[i].entryDir);
        }

        BeltInMask[] order = { BeltInMask.Back, BeltInMask.Left, BeltInMask.Right };
        int start = 0;
        for (int i = 0; i < order.Length; i++)
        {
            if (order[i] == lastServed)
            {
                start = (i + 1) % order.Length;
                break;
            }
        }

        for (int n = 0; n < order.Length; n++)
        {
            BeltInMask bit = order[(start + n) % order.Length];
            if (BeltRules.Has(waiting, bit))
                return bit;
        }

        return BeltInMask.None;
    }

    void RemapCargoToValidEntries()
    {
        Vector2Int fallback = ExitDir;
        if (BeltRules.Has(InMask, BeltInMask.Left))
            fallback = BeltRules.Travel(ExitDir, BeltInMask.Left);
        else if (BeltRules.Has(InMask, BeltInMask.Right))
            fallback = BeltRules.Travel(ExitDir, BeltInMask.Right);

        for (int i = 0; i < cargo.Count; i++)
        {
            if (!IsValidEntry(cargo[i].entryDir))
                cargo[i].entryDir = fallback;
        }
    }

    void RefreshExitFromTransform()
    {
        ExitDir = BuildingLinker.ToCardinal(transform.forward);
    }

    static BuildingBase SourceFromSocket(BuildingSocket fromSocket)
    {
        if (fromSocket == null)
            return null;
        BuildingBase owner = fromSocket.Owner;
        if (owner != null)
            return owner;
        if (fromSocket.connectedSocket != null)
            return fromSocket.connectedSocket.Owner;
        return null;
    }

    Vector2Int InferEntryDir(BuildingSocket fromSocket, BuildingBase source, Transform visual = null)
    {
        BuildingBase src = source;
        if (src == null)
            src = SourceFromSocket(fromSocket);
        if (src == this)
            src = fromSocket != null && fromSocket.connectedSocket != null
                ? fromSocket.connectedSocket.Owner
                : null;

        BeltInMask fromSource = SideFromSource(src);
        if (fromSource != BeltInMask.None)
            return BeltRules.Travel(ExitDir, fromSource);

        if (visual != null)
        {
            Vector2Int fromWorld = EntryFromWorld(visual.position);
            if (IsValidEntry(fromWorld))
                return fromWorld;
        }

        return Vector2Int.zero;
    }

    BeltInMask SideFromSource(BuildingBase source)
    {
        if (source == null || source == this)
            return BeltInMask.None;

        Vector2Int exit = ExitDir;
        if (BuildingLinker.OccupiesCell(source, Cell + BeltRules.BackNeighbor(exit)))
            return BeltInMask.Back;
        if (BuildingLinker.OccupiesCell(source, Cell + BeltRules.LeftNeighbor(exit)))
            return BeltInMask.Left;
        if (BuildingLinker.OccupiesCell(source, Cell + BeltRules.RightNeighbor(exit)))
            return BeltInMask.Right;
        return BeltInMask.None;
    }

    Vector2Int EntryFromWorld(Vector3 world)
    {
        Vector2Int fromCell = BuildingLinker.WorldToCell(world);
        Vector2Int delta = Cell - fromCell;
        if (Mathf.Abs(delta.x) + Mathf.Abs(delta.y) == 1)
            return IsValidEntry(delta) ? delta : Vector2Int.zero;

        Vector3 local = world - transform.position;
        local.y = 0f;
        if (local.sqrMagnitude < 0.0001f)
            return Vector2Int.zero;
        Vector2Int inward = BuildingLinker.ToCardinal(local);
        Vector2Int travel = new Vector2Int(-inward.x, -inward.y);
        return IsValidEntry(travel) ? travel : Vector2Int.zero;
    }

    void ApplyVisual()
    {
        EnsureSetup();
        BeltShape shown = ResolveShownShape();
        BeltRules.GetVisual(shown, InMask, out float extraYaw, out bool mirrorX);

        if (straightVisual != null)
            straightVisual.SetActive(shown == BeltShape.Straight);

        PresentVisual(cornerVisual, shown == BeltShape.Corner, extraYaw, mirrorX);
        PresentVisual(teeVisual, shown == BeltShape.Tee, extraYaw, mirrorX);
        PresentVisual(sidesVisual, shown == BeltShape.Sides, extraYaw, mirrorX);
        PresentVisual(tripleVisual, shown == BeltShape.Triple, extraYaw, mirrorX);
        RefreshArrows();
    }

    public bool ShouldShowIoArrow(SocketArrow arrow)
    {
        if (arrow == null || !arrow.gameObject.activeInHierarchy)
            return false;

        RefreshExitFromTransform();
        BeltShape shown = ResolveShownShape();
        if (!ArrowBelongsToShownVisual(arrow, shown))
            return false;

        if (shown == BeltShape.Tee || shown == BeltShape.Sides || shown == BeltShape.Triple)
            return true;

        Vector2Int delta = ArrowNeighborDelta(arrow);
        if (delta.x == 0 && delta.y == 0)
            return false;
        return !HasLogisticsAt(Cell + delta);
    }

    void RefreshArrows()
    {
        SocketArrow[] arrows = GetComponentsInChildren<SocketArrow>(true);
        for (int i = 0; i < arrows.Length; i++)
        {
            if (arrows[i] != null)
                arrows[i].Apply();
        }
    }

    bool ArrowBelongsToShownVisual(SocketArrow arrow, BeltShape shown)
    {
        Transform t = arrow.transform;
        if (UnderVisual(t, tripleVisual))
            return shown == BeltShape.Triple;
        if (UnderVisual(t, sidesVisual))
            return shown == BeltShape.Sides;
        if (UnderVisual(t, teeVisual))
            return shown == BeltShape.Tee;
        if (UnderVisual(t, cornerVisual))
            return shown == BeltShape.Corner;
        if (UnderVisual(t, straightVisual))
            return shown == BeltShape.Straight;
        return shown == BeltShape.Straight;
    }

    static bool UnderVisual(Transform t, GameObject visual)
    {
        return visual != null && t.IsChildOf(visual.transform);
    }

    Vector2Int ArrowNeighborDelta(SocketArrow arrow)
    {
        Vector3 local = transform.InverseTransformPoint(arrow.transform.position);
        local.y = 0f;
        if (local.sqrMagnitude < 0.04f)
            return Vector2Int.zero;
        if (Mathf.Abs(local.z) >= Mathf.Abs(local.x))
            return local.z >= 0f ? ExitDir : BeltRules.BackNeighbor(ExitDir);
        return local.x >= 0f ? BeltRules.RightNeighbor(ExitDir) : BeltRules.LeftNeighbor(ExitDir);
    }

    bool HasLogisticsAt(Vector2Int cell)
    {
        if (cell == Cell)
            return false;
        if (PreviewExits.ContainsKey(cell))
            return true;

        Conveyor other = BuildingLinker.GetBuildingAt(cell) as Conveyor;
        if (other == null || other == this || other.IsPreview)
            return false;
        return (this is Pipe) == (other is Pipe);
    }

    BeltShape ResolveShownShape()
    {
        BeltShape shown = Shape;
        if (shown == BeltShape.Triple && tripleVisual == null)
        {
            if (sidesVisual != null && FromLeft && FromRight)
                return BeltShape.Sides;
            if (teeVisual != null && (FromLeft || FromRight))
                return BeltShape.Tee;
            if (cornerVisual != null && (FromLeft || FromRight) && !FromBack)
                return BeltShape.Corner;
            return BeltShape.Straight;
        }

        if (shown == BeltShape.Sides && sidesVisual == null)
            return BeltShape.Straight;
        if (shown == BeltShape.Tee && teeVisual == null)
            return (cornerVisual != null && !FromBack) ? BeltShape.Corner : BeltShape.Straight;
        if (shown == BeltShape.Corner && cornerVisual == null)
            return BeltShape.Straight;
        return shown;
    }

    static void PresentVisual(GameObject visual, bool on, float extraYaw, bool mirrorX)
    {
        if (visual == null)
            return;
        visual.SetActive(on);
        if (!on)
            return;
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.Euler(0f, extraYaw, 0f);
        Vector3 scale = visual.transform.localScale;
        float ax = Mathf.Abs(scale.x);
        if (ax < 0.0001f)
            ax = 1f;
        visual.transform.localScale = new Vector3(mirrorX ? -ax : ax, scale.y, scale.z);
    }

    void EnsureSetup()
    {
        EnsureCollider();
        EnsureVisuals();
        ClearBeltSockets();
    }

    void ClearBeltSockets()
    {
        inputSockets = System.Array.Empty<BuildingSocket>();
        outputSockets = System.Array.Empty<BuildingSocket>();
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

    void EnsureVisuals()
    {
        if (straightVisual == null)
            straightVisual = FindStraightVisual();

        if (cornerVisual == null)
            cornerVisual = CreateChildVisual("CornerVisual", data != null ? data.cornerPrefab : null, data != null ? data.cornerGhostPrefab : null);
        if (teeVisual == null)
            teeVisual = CreateChildVisual("TeeVisual", data != null ? data.teePrefab : null, data != null ? data.teeGhostPrefab : null);
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
        visual.SetActive(false);
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
        visual.transform.localRotation = Quaternion.identity;
        MatchLayer(visual, gameObject.layer);
        StripRuntimeComponents(visual);
        visual.SetActive(false);
        return visual;
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
        if (root == null)
            return;

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = false;
        }

        MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] != null)
                behaviours[i].enabled = false;
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

        RefreshExitFromTransform();
        float half = GridFootprint.CellSize * 0.5f;
        BeltInMask[] sides = { BeltInMask.Back, BeltInMask.Left, BeltInMask.Right };
        for (int s = 0; s < sides.Length; s++)
        {
            if (InMask != BeltInMask.None && !BeltRules.Has(InMask, sides[s]) && sides[s] != BeltInMask.Back)
                continue;
            if (InMask != BeltInMask.None && sides[s] == BeltInMask.Back && !FromBack && (FromLeft || FromRight))
                continue;

            Gizmos.color = sides[s] == BeltInMask.Back ? Color.cyan : Color.yellow;
            Vector3 prev = transform.TransformPoint(BeltRules.PathLocal(sides[s], 0f, half, itemHeight));
            for (int i = 1; i <= 8; i++)
            {
                Vector3 next = transform.TransformPoint(BeltRules.PathLocal(sides[s], i / 8f, half, itemHeight));
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }

        Gizmos.color = Color.green;
        Gizmos.DrawRay(transform.position + Vector3.up * 0.2f, BuildingLinker.CardinalToWorld(ExitDir) * 0.6f);
    }
#endif
}
