using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class WorldResourceScatterer : MonoBehaviour
{
    enum MountainOre
    {
        Stone,
        Coal,
        Copper,
        Iron,
        Sulfur
    }

    [Header("Prefabs (Assets/prefabs/resourses)")]
    public GameObject sandPrefab;
    public GameObject stonePrefab;
    public GameObject coalPrefab;
    public GameObject copperPrefab;
    public GameObject ironPrefab;
    public GameObject sulfurPrefab;
    public GameObject treePrefab;

    [Header("Deposits")]
    [Tooltip("Россыпь с зазором: к каждой ноде можно подвести ленту.")]
    public int treeDeposits = 40;
    public int ironDeposits = 14;
    public int copperDeposits = 14;
    public int stoneDeposits = 12;
    public int coalDeposits = 12;
    public int sulfurDeposits = 8;
    public int sandDeposits = 14;
    public int nodesPerCluster = 20;
    public int clusterRadius = 11;
    public int nodeGap = 2;
    public int clusterGap = 9;

    [Header("Streaming")]
    public float visualRadius = 38f;

    public static WorldResourceScatterer Instance { get; private set; }
    public IReadOnlyList<VeinMark> Veins => veins;
    public bool IsScattered { get; private set; }
    public float ScatterProgress { get; private set; }

    Transform root;
    Transform player;
    readonly HashSet<Vector2Int> used = new HashSet<Vector2Int>();
    readonly List<Vector2Int> clusterCenters = new List<Vector2Int>();
    readonly List<VeinMark> veins = new List<VeinMark>(256);
    readonly Dictionary<Vector2Int, string> veinLookup = new Dictionary<Vector2Int, string>();
    readonly List<NodeVisual> visuals = new List<NodeVisual>(256);
    System.Random rng;
    string pendingKind;

    public struct VeinMark
    {
        public Vector2Int cell;
        public Color color;
        public string label;
    }

    struct NodeVisual
    {
        public Vector3 pos;
        public GameObject prefab;
        public GameObject stub;
        public GameObject visual;
        public float yaw;
        public bool shown;
    }

    Vector3 lastCullPos = new Vector3(99999f, 0f, 0f);
    float lastCullTime = -10f;

    void Awake()
    {
        Instance = this;
    }

    void OnEnable()
    {
        GameSettings.Changed += SyncVisualRadius;
        SyncVisualRadius();
    }

    void OnDisable()
    {
        GameSettings.Changed -= SyncVisualRadius;
    }

    void SyncVisualRadius()
    {
        visualRadius = GameSettings.RenderDistance;
        lastCullTime = -10f;
    }

    Coroutine scatterRoutine;

    void Start()
    {
        if (WorldBiomeMap.Instance != null && WorldBiomeMap.Instance.IsReady)
            Scatter();
    }

    void OnDestroy()
    {
        GameSettings.Changed -= SyncVisualRadius;
        if (Instance == this)
            Instance = null;
    }

    void LateUpdate()
    {
        if (!WorldView.HasPlayer)
            return;
        Vector3 p = WorldView.PlayerPos;
        if ((p - lastCullPos).sqrMagnitude < 9f && Time.unscaledTime - lastCullTime < 0.2f)
            return;
        lastCullPos = p;
        lastCullTime = Time.unscaledTime;
        UpdateVisibility();
    }

    [ContextMenu("Scatter")]
    public void Scatter()
    {
        if (!isActiveAndEnabled)
            return;
        if (scatterRoutine != null)
            StopCoroutine(scatterRoutine);
        scatterRoutine = StartCoroutine(ScatterRoutine());
    }

    IEnumerator ScatterRoutine()
    {
        WorldBiomeMap map = WorldBiomeMap.Instance;
        if (map == null || !map.IsReady)
            yield break;

        IsScattered = false;
        ScatterProgress = 0f;
        ResolvePrefabs();
        ClearSpawned();
        used.Clear();
        clusterCenters.Clear();
        veins.Clear();
        veinLookup.Clear();
        visuals.Clear();
        rng = new System.Random(map.seed * 31 + 9);
        root = new GameObject("WorldResources").transform;
        yield return null;

        pendingKind = "sand";
        yield return SpawnBiomeDepositsRoutine(WorldBiome.Beach, sandPrefab, sandDeposits, clusterGap, 0.02f, 0.18f);
        pendingKind = "tree";
        yield return SpawnBiomeDepositsRoutine(WorldBiome.Forest, treePrefab, treeDeposits, 6, 0.18f, 0.48f);
        yield return SpawnBiomeDepositsRoutine(WorldBiome.Woodland, treePrefab, Mathf.Max(12, treeDeposits / 2), 7, 0.48f, 0.62f);
        yield return SpawnMountainOresRoutine(map);
        HideAllVisuals();
        ScatterProgress = 1f;
        IsScattered = true;
        scatterRoutine = null;
        if (WorldMapUI.Instance != null)
            WorldMapUI.Instance.Rebuild();
    }

    void ClearSpawned()
    {
        if (root != null)
            Destroy(root.gameObject);
        GameObject existing = GameObject.Find("WorldResources");
        if (existing != null)
            Destroy(existing);
    }

    IEnumerator SpawnBiomeDepositsRoutine(WorldBiome biome, GameObject prefab, int deposits, int gap, float p0, float p1)
    {
        if (prefab == null || deposits <= 0)
            yield break;

        var cells = new List<Vector2Int>(256);
        WorldBiomeMap.Instance.CollectBiomeCells(biome, cells);
        Shuffle(cells);

        int made = 0;
        int want = Mathf.Max(10, nodesPerCluster);
        int steps = 0;
        for (int i = 0; i < cells.Count && made < deposits; i++)
        {
            if (!CanStartCluster(cells[i], gap))
                continue;
            if (SpawnScatter(cells[i], cells, prefab, want) >= 5)
            {
                made++;
                ScatterProgress = Mathf.Lerp(p0, p1, made / (float)Mathf.Max(1, deposits));
            }
            steps++;
            if (steps % 2 == 0)
                yield return null;
        }
        ScatterProgress = p1;
    }

    IEnumerator SpawnMountainOresRoutine(WorldBiomeMap map)
    {
        int count = map.MountainCount;
        if (count <= 0)
        {
            ScatterProgress = 0.95f;
            yield break;
        }

        var infos = new List<MountainInfo>(count);
        for (int id = 0; id < count; id++)
        {
            var cells = new List<Vector2Int>(64);
            map.CollectMountainCells(id, cells);
            if (cells.Count == 0)
                continue;

            int peaks = 0;
            int slopes = 0;
            for (int i = 0; i < cells.Count; i++)
            {
                WorldBiome b = map.Get(cells[i]);
                if (b == WorldBiome.MountainPeak)
                    peaks++;
                else if (b == WorldBiome.MountainSlope)
                    slopes++;
            }

            infos.Add(new MountainInfo
            {
                id = id,
                cells = cells,
                peaks = peaks,
                slopes = slopes,
                ore = MountainOre.Stone
            });
        }

        infos.Sort((a, b) => b.cells.Count.CompareTo(a.cells.Count));

        var assigned = new bool[infos.Count];
        AssignMountainOres(infos, assigned);

        int done = 0;
        int total = Mathf.Max(1, infos.Count);
        for (int i = 0; i < infos.Count; i++)
        {
            if (!assigned[i])
                continue;
            SpawnMountain(map, infos[i]);
            done++;
            ScatterProgress = Mathf.Lerp(0.62f, 0.95f, done / (float)total);
            yield return null;
        }
        ScatterProgress = 0.95f;
    }

    void AssignMountainOres(List<MountainInfo> infos, bool[] assigned)
    {
        var peak = new List<int>(infos.Count);
        var other = new List<int>(infos.Count);
        for (int i = 0; i < infos.Count; i++)
        {
            if (infos[i].cells.Count < 12)
                continue;
            if (infos[i].peaks >= 2)
                peak.Add(i);
            else
                other.Add(i);
        }

        int peakShare = Mathf.Max(1, peak.Count / 2);
        int ironWant = Mathf.Clamp(Mathf.Max(2, ironDeposits / 5), 1, peakShare);
        int copperWant = Mathf.Clamp(Mathf.Max(2, copperDeposits / 5), 1, peak.Count - ironWant);
        if (peak.Count == 1)
        {
            ironWant = 1;
            copperWant = 0;
        }

        AssignSpread(infos, assigned, peak, MountainOre.Iron, ironWant);
        AssignSpread(infos, assigned, peak, MountainOre.Copper, copperWant);
        if (CountOre(infos, assigned, MountainOre.Copper) == 0 && other.Count > 0)
            AssignSpread(infos, assigned, other, MountainOre.Copper, 1);
        if (CountOre(infos, assigned, MountainOre.Iron) == 0 && other.Count > 0)
            AssignSpread(infos, assigned, other, MountainOre.Iron, 1);

        AssignSpread(infos, assigned, other, MountainOre.Coal, Mathf.Max(2, coalDeposits / 5));
        AssignSpread(infos, assigned, other, MountainOre.Stone, Mathf.Max(2, stoneDeposits / 5));
        AssignSpread(infos, assigned, other, MountainOre.Sulfur, Mathf.Max(1, sulfurDeposits / 5));
        FillLeftoverBalanced(infos, assigned);
    }

    void AssignSpread(List<MountainInfo> infos, bool[] assigned, List<int> pool, MountainOre ore, int want)
    {
        int made = 0;
        while (made < want)
        {
            int best = -1;
            int bestScore = int.MinValue;
            for (int p = 0; p < pool.Count; p++)
            {
                int i = pool[p];
                if (assigned[i])
                    continue;
                int same = MinChebyshevToOre(infos, assigned, i, ore);
                int any = MinChebyshevToAssigned(infos, assigned, i);
                int score = same * 3 + any;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }

            if (best < 0)
                break;
            MountainInfo info = infos[best];
            info.ore = ore;
            infos[best] = info;
            assigned[best] = true;
            made++;
        }
    }

    int CountOre(List<MountainInfo> infos, bool[] assigned, MountainOre ore)
    {
        int n = 0;
        for (int i = 0; i < infos.Count; i++)
        {
            if (assigned[i] && infos[i].ore == ore)
                n++;
        }

        return n;
    }

    int MinChebyshevToOre(List<MountainInfo> infos, bool[] assigned, int index, MountainOre ore)
    {
        Vector2Int center = MountainCenter(infos[index]);
        int best = 9999;
        bool any = false;
        for (int i = 0; i < infos.Count; i++)
        {
            if (!assigned[i] || infos[i].ore != ore)
                continue;
            any = true;
            int d = Chebyshev(center, MountainCenter(infos[i]));
            if (d < best)
                best = d;
        }

        return any ? best : 9999;
    }

    int MinChebyshevToAssigned(List<MountainInfo> infos, bool[] assigned, int index)
    {
        Vector2Int center = MountainCenter(infos[index]);
        int best = 9999;
        bool any = false;
        for (int i = 0; i < infos.Count; i++)
        {
            if (!assigned[i])
                continue;
            any = true;
            int d = Chebyshev(center, MountainCenter(infos[i]));
            if (d < best)
                best = d;
        }

        return any ? best : 9999;
    }

    static Vector2Int MountainCenter(MountainInfo info)
    {
        if (info.cells == null || info.cells.Count == 0)
            return Vector2Int.zero;
        int x = 0;
        int y = 0;
        for (int i = 0; i < info.cells.Count; i++)
        {
            x += info.cells[i].x;
            y += info.cells[i].y;
        }

        return new Vector2Int(x / info.cells.Count, y / info.cells.Count);
    }

    void FillLeftoverBalanced(List<MountainInfo> infos, bool[] assigned)
    {
        for (int i = 0; i < infos.Count; i++)
        {
            if (assigned[i] || infos[i].cells.Count < 12)
                continue;

            MountainOre ore = PickRarestAllowed(infos, assigned, infos[i]);
            MountainInfo info = infos[i];
            info.ore = ore;
            infos[i] = info;
            assigned[i] = true;
        }
    }

    MountainOre PickRarestAllowed(List<MountainInfo> infos, bool[] assigned, MountainInfo mountain)
    {
        MountainOre[] peak = { MountainOre.Iron, MountainOre.Copper };
        MountainOre[] slope = { MountainOre.Coal, MountainOre.Stone, MountainOre.Sulfur };
        MountainOre[] pool = mountain.peaks >= 2 ? peak : slope;
        MountainOre best = pool[0];
        int lowest = int.MaxValue;
        for (int i = 0; i < pool.Length; i++)
        {
            int n = CountOre(infos, assigned, pool[i]);
            if (n < lowest)
            {
                lowest = n;
                best = pool[i];
            }
        }

        return best;
    }

    void SpawnMountain(WorldBiomeMap map, MountainInfo info)
    {
        GameObject prefab = PrefabOf(info.ore);
        if (prefab == null)
            return;

        var valid = new List<Vector2Int>(info.cells.Count);
        for (int i = 0; i < info.cells.Count; i++)
        {
            WorldBiome biome = map.Get(info.cells[i]);
            if (OreAllows(info.ore, biome))
                valid.Add(info.cells[i]);
        }

        Shuffle(valid);

        pendingKind = info.ore == MountainOre.Coal ? "coal"
            : info.ore == MountainOre.Copper ? "copper"
            : info.ore == MountainOre.Iron ? "iron"
            : info.ore == MountainOre.Sulfur ? "sulfur"
            : "stone";

        int want = Mathf.Max(12, nodesPerCluster);
        if (info.ore == MountainOre.Iron || info.ore == MountainOre.Copper)
            want = Mathf.Max(18, nodesPerCluster);
        else if (info.ore == MountainOre.Stone)
            want = Mathf.Max(12, nodesPerCluster - 4);

        int clusters = 1;
        if (info.cells.Count > 90)
            clusters = 2;

        int minAccept = 5;
        int made = 0;
        int localGap = Mathf.Max(6, clusterGap);
        for (int i = 0; i < valid.Count && made < clusters; i++)
        {
            if (!CanStartCluster(valid[i], localGap))
                continue;
            if (SpawnScatter(valid[i], valid, prefab, want) >= minAccept)
                made++;
        }

        if (made == 0 && valid.Count >= 4)
            SpawnScatter(valid[0], valid, prefab, want);
    }

    static bool OreAllows(MountainOre ore, WorldBiome biome)
    {
        switch (ore)
        {
            case MountainOre.Stone:
                return biome == WorldBiome.MountainSlope;
            case MountainOre.Coal:
            case MountainOre.Sulfur:
                return WorldBiomeMap.IsMountain(biome);
            case MountainOre.Copper:
            case MountainOre.Iron:
                return WorldBiomeMap.IsMountain(biome);
            default:
                return false;
        }
    }

    GameObject PrefabOf(MountainOre ore)
    {
        switch (ore)
        {
            case MountainOre.Stone: return stonePrefab;
            case MountainOre.Coal: return coalPrefab;
            case MountainOre.Copper: return copperPrefab;
            case MountainOre.Iron: return ironPrefab;
            case MountainOre.Sulfur: return sulfurPrefab;
            default: return null;
        }
    }

    bool CanStartCluster(Vector2Int cell, int gap)
    {
        if (used.Contains(cell) || ResourceNode.HasNode(cell))
            return false;
        for (int i = 0; i < clusterCenters.Count; i++)
        {
            if (Chebyshev(clusterCenters[i], cell) < gap)
                return false;
        }
        return true;
    }

    int SpawnScatter(Vector2Int center, List<Vector2Int> pool, GameObject prefab, int want)
    {
        if (prefab == null)
            return 0;

        int radius = Mathf.Max(4, clusterRadius);
        var pick = new List<Vector2Int>(want);
        var reserved = new HashSet<Vector2Int>();

        var candidates = new List<Vector2Int>(64);
        for (int i = 0; i < pool.Count; i++)
        {
            if (Chebyshev(pool[i], center) <= radius)
                candidates.Add(pool[i]);
        }
        Shuffle(candidates);

        for (int i = 0; i < candidates.Count && pick.Count < want; i++)
        {
            Vector2Int cell = candidates[i];
            if (used.Contains(cell) || reserved.Contains(cell) || ResourceNode.HasNode(cell))
                continue;
            if (!FarFromUsed(cell, nodeGap) || !FarFromSet(cell, reserved, nodeGap))
                continue;
            pick.Add(cell);
            reserved.Add(cell);
        }

        if (pick.Count < 5)
            return 0;

        for (int i = 0; i < pick.Count; i++)
            SpawnNode(prefab, pick[i]);

        clusterCenters.Add(center);
        return pick.Count;
    }

    static bool FarFromSet(Vector2Int cell, HashSet<Vector2Int> set, int gap)
    {
        int g = Mathf.Max(1, gap);
        for (int z = -g + 1; z < g; z++)
        {
            for (int x = -g + 1; x < g; x++)
            {
                if (x == 0 && z == 0)
                    continue;
                if (set.Contains(cell + new Vector2Int(x, z)))
                    return false;
            }
        }
        return true;
    }

    bool FarFromUsed(Vector2Int cell, int gap)
    {
        int g = Mathf.Max(1, gap);
        for (int z = -g + 1; z < g; z++)
        {
            for (int x = -g + 1; x < g; x++)
            {
                if (x == 0 && z == 0)
                    continue;
                if (used.Contains(cell + new Vector2Int(x, z)))
                    return false;
            }
        }
        return true;
    }

    bool SpawnNode(GameObject prefab, Vector2Int cell)
    {
        if (prefab == null)
            return false;

        Vector3 pos = WorldBiomeMap.Instance.CellWorld(cell);
        float yaw = rng.Next(0, 4) * 90f;
        GameObject stub = new GameObject(prefab.name + "_" + cell.x + "_" + cell.y);
        stub.transform.SetParent(root, false);
        stub.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));

        ResourceNode node = stub.AddComponent<ResourceNode>();
        node.resource = ResourceOf(prefab);
        node.RegisterCell();

        visuals.Add(new NodeVisual
        {
            pos = pos,
            prefab = prefab,
            stub = stub,
            visual = null,
            yaw = yaw,
            shown = false
        });
        veins.Add(new VeinMark
        {
            cell = cell,
            color = ColorOfKind(pendingKind),
            label = LabelOfKind(pendingKind)
        });
        veinLookup[cell] = LabelOfKind(pendingKind);

        used.Add(cell);
        return true;
    }

    static readonly Dictionary<int, ItemData> prefabResource = new Dictionary<int, ItemData>();

    static ItemData ResourceOf(GameObject prefab)
    {
        if (prefab == null)
            return null;
        int id = prefab.GetInstanceID();
        if (prefabResource.TryGetValue(id, out ItemData cached))
            return cached;
        ResourceNode node = prefab.GetComponent<ResourceNode>();
        if (node == null)
            node = prefab.GetComponentInChildren<ResourceNode>(true);
        ItemData resource = node != null ? node.resource : null;
        prefabResource[id] = resource;
        return resource;
    }

    public bool TryGetVeinLabel(Vector2Int cell, out string label)
    {
        return veinLookup.TryGetValue(cell, out label);
    }

    static string LabelOfKind(string kind)
    {
        switch (kind)
        {
            case "sand": return "Песок";
            case "tree": return "Дерево";
            case "coal": return "Уголь";
            case "copper": return "Медь";
            case "iron": return "Железо";
            case "sulfur": return "Сера";
            default: return "Камень";
        }
    }

    static Color ColorOfKind(string kind)
    {
        switch (kind)
        {
            case "sand": return new Color(0.95f, 0.86f, 0.32f);
            case "tree": return new Color(0.07f, 0.22f, 0.08f);
            case "coal": return new Color(0.07f, 0.07f, 0.08f);
            case "copper": return new Color(0.86f, 0.46f, 0.16f);
            case "iron": return new Color(0.72f, 0.24f, 0.18f);
            case "sulfur": return new Color(0.92f, 0.82f, 0.18f);
            default: return new Color(0.78f, 0.78f, 0.80f);
        }
    }

    void HideAllVisuals()
    {
        for (int i = 0; i < visuals.Count; i++)
            SetShown(i, false);
    }

    void UpdateVisibility()
    {
        if (visuals.Count == 0)
            return;

        Vector3 p = WorldView.PlayerPos;
        float r2 = visualRadius * visualRadius;
        int budget = 6;
        for (int i = 0; i < visuals.Count; i++)
        {
            Vector3 d = visuals[i].pos - p;
            d.y = 0f;
            bool want = d.sqrMagnitude <= r2;
            if (want && visuals[i].visual == null)
            {
                if (budget <= 0)
                    continue;
                budget--;
            }
            SetShown(i, want);
        }
    }

    void SetShown(int index, bool show)
    {
        NodeVisual vis = visuals[index];
        if (show)
        {
            if (vis.visual == null)
                AttachVisual(ref vis);
            vis.shown = vis.visual != null;
        }
        else
        {
            if (vis.visual != null)
            {
                Destroy(vis.visual);
                vis.visual = null;
            }
            vis.shown = false;
        }
        visuals[index] = vis;
    }

    static void AttachVisual(ref NodeVisual vis)
    {
        if (vis.stub == null || vis.prefab == null)
            return;
        GameObject visual = Object.Instantiate(vis.prefab, vis.stub.transform, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        ResourceNode extra = visual.GetComponent<ResourceNode>();
        if (extra == null)
            extra = visual.GetComponentInChildren<ResourceNode>(true);
        if (extra != null)
            Object.Destroy(extra);
        ResourceNode stubNode = vis.stub.GetComponent<ResourceNode>();
        if (stubNode != null)
            stubNode.RegisterCell();
        vis.visual = visual;
    }

    static int Chebyshev(Vector2Int a, Vector2Int b)
    {
        return Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
    }

    void Shuffle(List<Vector2Int> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            Vector2Int t = list[i];
            list[i] = list[j];
            list[j] = t;
        }
    }

    void ResolvePrefabs()
    {
        if (sandPrefab == null) sandPrefab = LoadPrefab("sand");
        if (stonePrefab == null) stonePrefab = LoadPrefab("stone");
        if (coalPrefab == null) coalPrefab = LoadPrefab("stone_coal_ore");
        if (copperPrefab == null) copperPrefab = LoadPrefab("stone_cooper_ore");
        if (ironPrefab == null) ironPrefab = LoadPrefab("stone_iron_ore");
        if (sulfurPrefab == null) sulfurPrefab = LoadPrefab("sulfur");
        if (treePrefab == null) treePrefab = LoadPrefab("tree");

        if (sandPrefab == null && stonePrefab == null && treePrefab == null)
            Debug.LogError("WorldResourceScatterer: no resource prefabs in the player. Add them to Resources/ResourceNodePrefabs.");
    }

    static GameObject LoadPrefab(string name)
    {
        ResourceNodePrefabs catalog = Resources.Load<ResourceNodePrefabs>("ResourceNodePrefabs");
        if (catalog != null)
        {
            GameObject fromCatalog = catalog.Get(name);
            if (fromCatalog != null)
                return fromCatalog;
        }

        GameObject fromResources = Resources.Load<GameObject>("resourses/" + name);
        if (fromResources != null)
            return fromResources;

#if UNITY_EDITOR
        string[] guids = AssetDatabase.FindAssets(name + " t:Prefab", new[] { "Assets/prefabs/resourses" });
        for (int i = 0; i < (guids != null ? guids.Length : 0); i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (string.IsNullOrEmpty(path))
                continue;
            string file = System.IO.Path.GetFileNameWithoutExtension(path);
            if (file == name)
                return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
        if (guids != null && guids.Length > 0)
            return AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[0]));
#endif
        Debug.LogWarning("WorldResourceScatterer: missing prefab " + name);
        return null;
    }

    struct MountainInfo
    {
        public int id;
        public List<Vector2Int> cells;
        public int peaks;
        public int slopes;
        public MountainOre ore;
    }
}
