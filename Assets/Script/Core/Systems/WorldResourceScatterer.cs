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
    public int minDepositsPerType = 4;
    public int treeDeposits = 18;
    public int ironDeposits = 6;
    public int copperDeposits = 6;
    public int stoneDeposits = 4;
    public int coalDeposits = 3;
    public int sulfurDeposits = 3;
    public int nodesPerCluster = 22;
    public int clusterRadius = 14;
    public int nodeGap = 2;
    public int clusterGap = 14;

    [Header("Streaming")]
    public float visualRadius = 38f;

    public static WorldResourceScatterer Instance { get; private set; }
    public IReadOnlyList<VeinMark> Veins => veins;
    public bool IsScattered { get; private set; }

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
        public Renderer[] renderers;
        public bool shown;
    }

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        if (WorldBiomeMap.Instance != null && WorldBiomeMap.Instance.IsReady)
            Scatter();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void LateUpdate()
    {
        UpdateVisibility();
    }

    [ContextMenu("Scatter")]
    public void Scatter()
    {
        WorldBiomeMap map = WorldBiomeMap.Instance;
        if (map == null || !map.IsReady)
            return;

        IsScattered = false;
        ResolvePrefabs();
        ClearSpawned();
        used.Clear();
        clusterCenters.Clear();
        veins.Clear();
        veinLookup.Clear();
        visuals.Clear();
        rng = new System.Random(map.seed * 31 + 9);

        root = new GameObject("WorldResources").transform;

        pendingKind = "sand";
        SpawnBiomeDeposits(WorldBiome.Beach, sandPrefab, minDepositsPerType, clusterGap);
        pendingKind = "tree";
        SpawnBiomeDeposits(WorldBiome.Forest, treePrefab, treeDeposits, 9);
        SpawnBiomeDeposits(WorldBiome.Woodland, treePrefab, Mathf.Max(8, treeDeposits / 2), 9);
        SpawnMountainOres(map);
        HideAllVisuals();
        IsScattered = true;
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

    int SpawnBiomeDeposits(WorldBiome biome, GameObject prefab, int deposits, int gap)
    {
        if (prefab == null || deposits <= 0)
            return 0;

        var cells = new List<Vector2Int>(256);
        WorldBiomeMap.Instance.CollectBiomeCells(biome, cells);
        Shuffle(cells);

        int made = 0;
        for (int i = 0; i < cells.Count && made < deposits; i++)
        {
            if (!CanStartCluster(cells[i], gap))
                continue;
            if (SpawnCluster(cells[i], cells, prefab) >= 15)
                made++;
        }
        return made;
    }

    void SpawnMountainOres(WorldBiomeMap map)
    {
        int count = map.MountainCount;
        if (count <= 0)
            return;

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
        AssignOre(infos, assigned, MountainOre.Coal, needsPeak: false, limit: coalDeposits, preferSmaller: true);
        AssignOre(infos, assigned, MountainOre.Stone, needsPeak: false, limit: stoneDeposits, preferSmaller: true);
        AssignOre(infos, assigned, MountainOre.Iron, needsPeak: true, limit: ironDeposits, preferSmaller: false);
        AssignOre(infos, assigned, MountainOre.Copper, needsPeak: true, limit: copperDeposits, preferSmaller: false);
        AssignOre(infos, assigned, MountainOre.Sulfur, needsPeak: false, limit: sulfurDeposits, preferSmaller: true);

        for (int i = 0; i < infos.Count; i++)
        {
            if (!assigned[i])
                continue;
            MountainOre ore = infos[i].ore;
            int clusters = 1;
            if (ore == MountainOre.Iron || ore == MountainOre.Copper)
                clusters = 3;
            else if (ore == MountainOre.Stone)
                clusters = 1;
            SpawnMountain(map, infos[i], clusters);
        }
    }

    void AssignOre(List<MountainInfo> infos, bool[] assigned, MountainOre ore, bool needsPeak, int limit, bool preferSmaller)
    {
        int left = Mathf.Max(1, limit);
        int start = preferSmaller ? infos.Count - 1 : 0;
        int step = preferSmaller ? -1 : 1;
        for (int i = start; i >= 0 && i < infos.Count && left > 0; i += step)
        {
            if (assigned[i])
                continue;
            if (needsPeak && infos[i].peaks < 4)
                continue;
            if (ore == MountainOre.Stone && infos[i].slopes < 8)
                continue;
            if (infos[i].cells.Count < 20)
                continue;

            MountainInfo info = infos[i];
            info.ore = ore;
            infos[i] = info;
            assigned[i] = true;
            left--;
        }
    }

    void SpawnMountain(WorldBiomeMap map, MountainInfo info, int clusters)
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
        clusters = Mathf.Max(1, clusters);

        pendingKind = info.ore == MountainOre.Coal ? "coal"
            : info.ore == MountainOre.Copper ? "copper"
            : info.ore == MountainOre.Iron ? "iron"
            : info.ore == MountainOre.Sulfur ? "sulfur"
            : "stone";

        int made = 0;
        for (int i = 0; i < valid.Count && made < clusters; i++)
        {
            if (!CanStartCluster(valid[i], clusterGap))
                continue;
            if (SpawnCluster(valid[i], valid, prefab) >= 15)
                made++;
        }
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
                return biome == WorldBiome.MountainPeak;
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

    int SpawnCluster(Vector2Int center, List<Vector2Int> pool, GameObject prefab)
    {
        const int minVeins = 15;
        int want = Mathf.Max(minVeins, nodesPerCluster);
        List<Vector2Int> pick = null;

        for (int radius = clusterRadius; radius <= clusterRadius + 16; radius += 4)
        {
            var candidates = new List<Vector2Int>(128);
            for (int i = 0; i < pool.Count; i++)
            {
                if (Chebyshev(pool[i], center) <= radius)
                    candidates.Add(pool[i]);
            }
            Shuffle(candidates);

            pick = new List<Vector2Int>(want);
            var reserved = new HashSet<Vector2Int>();
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

            if (pick.Count >= minVeins)
                break;
        }

        if (pick == null || pick.Count < minVeins)
            return 0;

        for (int i = 0; i < pick.Count; i++)
            SpawnNode(prefab, pick[i]);

        clusterCenters.Add(center);
        return pick.Count;
    }

    static bool FarFromSet(Vector2Int cell, HashSet<Vector2Int> set, int gap)
    {
        for (int z = -gap + 1; z < gap; z++)
        {
            for (int x = -gap + 1; x < gap; x++)
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
        for (int z = -gap + 1; z < gap; z++)
        {
            for (int x = -gap + 1; x < gap; x++)
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
        GameObject go = Instantiate(prefab, pos, Quaternion.Euler(0f, yaw, 0f), root);
        go.name = prefab.name + "_" + cell.x + "_" + cell.y;

        ResourceNode node = go.GetComponent<ResourceNode>();
        if (node == null)
            node = go.AddComponent<ResourceNode>();
        node.RegisterCell();

        Renderer[] rends = go.GetComponentsInChildren<Renderer>(true);
        visuals.Add(new NodeVisual { pos = pos, renderers = rends, shown = true });
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
        if (player == null)
        {
            PlayerBuilder builder = GameManager.Instance != null
                ? GameManager.Instance.playerBuilder
                : FindFirstObjectByType<PlayerBuilder>();
            if (builder != null)
                player = builder.transform;
            if (player == null)
                return;
        }

        Vector3 p = player.position;
        float r2 = visualRadius * visualRadius;
        for (int i = 0; i < visuals.Count; i++)
        {
            Vector3 d = visuals[i].pos - p;
            d.y = 0f;
            SetShown(i, d.sqrMagnitude <= r2);
        }
    }

    void SetShown(int index, bool show)
    {
        NodeVisual vis = visuals[index];
        if (vis.shown == show)
            return;
        vis.shown = show;
        visuals[index] = vis;
        if (vis.renderers == null)
            return;
        for (int i = 0; i < vis.renderers.Length; i++)
        {
            if (vis.renderers[i] != null)
                vis.renderers[i].enabled = show;
        }
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
    }

    static GameObject LoadPrefab(string name)
    {
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
        return Resources.Load<GameObject>("resourses/" + name);
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
