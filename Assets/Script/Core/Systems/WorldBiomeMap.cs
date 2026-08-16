using UnityEngine;

public enum WorldBiome
{
    Field,
    Woodland,
    Forest,
    Beach,
    Lake,
    Ocean,
    MountainPeak,
    MountainSlope
}

/// <summary>
/// Клеточная карта биомов + покраска terrain. Вода (озеро/океан) — нельзя строить.
/// </summary>
public class WorldBiomeMap : MonoBehaviour
{
    public static WorldBiomeMap Instance { get; private set; }

    [Header("Source")]
    public Terrain terrain;
    public int seed = 17;

    [Header("Layout")]
    [Range(2, 40)] public int oceanWidth = 14;
    [Range(1, 8)] public int beachWidth = 2;
    [Range(0.55f, 0.9f)] public float lakeThreshold = 0.74f;
    [Range(0.4f, 0.8f)] public float forestThreshold = 0.58f;
    [Range(0.15f, 0.55f)] public float woodlandThreshold = 0.34f;
    [Range(0.55f, 0.9f)] public float mountainPeakThreshold = 0.72f;
    [Range(1, 10)] public int mountainSlopeWidth = 4;

    [Header("Colors")]
    public Color forest = new Color(0.13f, 0.30f, 0.16f);
    public Color woodland = new Color(0.30f, 0.50f, 0.24f);
    public Color field = new Color(0.58f, 0.74f, 0.34f);
    public Color beach = new Color(0.86f, 0.74f, 0.42f);
    public Color lake = new Color(0.30f, 0.62f, 0.76f);
    public Color ocean = new Color(0.11f, 0.32f, 0.52f);
    public Color mountainPeak = new Color(0.28f, 0.28f, 0.30f);
    public Color mountainSlope = new Color(0.58f, 0.58f, 0.60f);

    WorldBiome[] cells;
    int[] mountainIds;
    int mountainCount;
    int minX;
    int minZ;
    int width;
    int height;
    bool ready;

    MeshRenderer overlay;
    Texture2D biomeTexture;

    public bool IsReady => ready;
    public int MapMinX => minX;
    public int MapMinZ => minZ;
    public int MapWidth => width;
    public int MapHeight => height;
    public Texture2D BiomeTexture => biomeTexture;

    void Awake()
    {
        Instance = this;
        if (terrain == null)
            terrain = FindFirstObjectByType<Terrain>();
        if (WorldCatalog.HasActive)
            seed = WorldCatalog.Active.seed;
        Generate();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    [ContextMenu("Regenerate")]
    public void Generate()
    {
        if (terrain == null)
            terrain = FindFirstObjectByType<Terrain>();
        if (terrain == null || terrain.terrainData == null)
        {
            ready = false;
            return;
        }

        ResolveBounds();
        BuildMap();
        ready = true;

        try
        {
            PaintOverlay();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Biome overlay skipped: " + e.Message);
        }

        WorldResourceScatterer scatter = GetComponent<WorldResourceScatterer>();
        if (scatter != null)
            scatter.Scatter();
    }

    void ResolveBounds()
    {
        Vector3 pos = terrain.GetPosition();
        Vector3 size = terrain.terrainData.size;
        Vector2Int a = WorldToCell(pos + new Vector3(0.51f, 0f, 0.51f));
        Vector2Int b = WorldToCell(pos + new Vector3(size.x - 0.51f, 0f, size.z - 0.51f));
        minX = Mathf.Min(a.x, b.x);
        minZ = Mathf.Min(a.y, b.y);
        width = Mathf.Abs(b.x - a.x) + 1;
        height = Mathf.Abs(b.y - a.y) + 1;
        width = Mathf.Max(8, width);
        height = Mathf.Max(8, height);
    }

    static Vector2Int WorldToCell(Vector3 world)
    {
        if (GridSystem.Instance != null)
            return GridSystem.Instance.WorldToCell(world);
        return new Vector2Int(Mathf.RoundToInt(world.x), Mathf.RoundToInt(world.z));
    }

    void BuildMap()
    {
        cells = new WorldBiome[width * height];
        float seedOff = seed * 17.13f;

        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                int edge = Mathf.Min(x, z, width - 1 - x, height - 1 - z);
                float wobble = (Mathf.PerlinNoise(x * 0.08f + seedOff, z * 0.08f) - 0.5f) * 4f;
                if (edge + wobble < oceanWidth)
                {
                    Set(x, z, WorldBiome.Ocean);
                    continue;
                }

                if (LakeNoise(x, z, seedOff) > lakeThreshold)
                {
                    Set(x, z, WorldBiome.Lake);
                    continue;
                }

                if (MountainNoise(x, z, seedOff) > mountainPeakThreshold)
                {
                    Set(x, z, WorldBiome.MountainPeak);
                    continue;
                }

                float veg = VegNoise(x, z, seedOff);
                if (veg > forestThreshold)
                    Set(x, z, WorldBiome.Forest);
                else if (veg > woodlandThreshold)
                    Set(x, z, WorldBiome.Woodland);
                else
                    Set(x, z, WorldBiome.Field);
            }
        }

        ApplySlopes();
        ApplyBeaches();
        BuildMountainIds();
    }

    void ApplyBeaches()
    {
        int ring = Mathf.Max(1, beachWidth);
        var next = new WorldBiome[cells.Length];
        System.Array.Copy(cells, next, cells.Length);

        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                WorldBiome current = GetLocal(x, z);
                if (current == WorldBiome.Lake || current == WorldBiome.Ocean || current == WorldBiome.MountainPeak)
                    continue;
                if (NearLake(x, z, ring))
                    next[z * width + x] = WorldBiome.Beach;
            }
        }

        cells = next;
    }

    void ApplySlopes()
    {
        int ring = Mathf.Max(1, mountainSlopeWidth);
        var next = new WorldBiome[cells.Length];
        System.Array.Copy(cells, next, cells.Length);

        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                WorldBiome current = GetLocal(x, z);
                if (current == WorldBiome.Ocean || current == WorldBiome.Lake || current == WorldBiome.MountainPeak)
                    continue;
                if (NearBiome(x, z, ring, WorldBiome.MountainPeak))
                    next[z * width + x] = WorldBiome.MountainSlope;
            }
        }

        cells = next;
    }

    void BuildMountainIds()
    {
        mountainIds = new int[cells.Length];
        for (int i = 0; i < mountainIds.Length; i++)
            mountainIds[i] = -1;

        mountainCount = 0;
        var stack = new System.Collections.Generic.Stack<int>();

        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                int i = z * width + x;
                if (mountainIds[i] >= 0 || !IsMountain(GetLocal(x, z)))
                    continue;

                int id = mountainCount++;
                stack.Push(i);
                mountainIds[i] = id;
                while (stack.Count > 0)
                {
                    int cur = stack.Pop();
                    int cx = cur % width;
                    int cz = cur / width;
                    TryPushMountain(stack, cx + 1, cz, id);
                    TryPushMountain(stack, cx - 1, cz, id);
                    TryPushMountain(stack, cx, cz + 1, id);
                    TryPushMountain(stack, cx, cz - 1, id);
                }
            }
        }
    }

    void TryPushMountain(System.Collections.Generic.Stack<int> stack, int x, int z, int id)
    {
        if (x < 0 || z < 0 || x >= width || z >= height)
            return;
        int i = z * width + x;
        if (mountainIds[i] >= 0 || !IsMountain(GetLocal(x, z)))
            return;
        mountainIds[i] = id;
        stack.Push(i);
    }

    public static bool IsMountain(WorldBiome biome)
    {
        return biome == WorldBiome.MountainPeak || biome == WorldBiome.MountainSlope;
    }

    public int MountainCount => mountainCount;

    public int GetMountainId(Vector2Int cell)
    {
        if (!ready || mountainIds == null)
            return -1;
        int x = cell.x - minX;
        int z = cell.y - minZ;
        if (x < 0 || z < 0 || x >= width || z >= height)
            return -1;
        return mountainIds[z * width + x];
    }

    public Vector3 CellWorld(Vector2Int cell)
    {
        float y = 0f;
        if (terrain != null)
        {
            Vector3 probe = new Vector3(cell.x + 0f, 0f, cell.y + 0f);
            if (GridSystem.Instance != null)
                probe = GridSystem.Instance.GetCellCenter(cell, 0f);
            y = terrain.SampleHeight(probe) + terrain.GetPosition().y;
        }
        if (GridSystem.Instance != null)
            return GridSystem.Instance.GetCellCenter(cell, y);
        return new Vector3(cell.x, y, cell.y);
    }

    public void CollectBiomeCells(WorldBiome biome, System.Collections.Generic.List<Vector2Int> results)
    {
        if (results == null || !ready)
            return;
        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                if (GetLocal(x, z) == biome)
                    results.Add(new Vector2Int(minX + x, minZ + z));
            }
        }
    }

    public void CollectMountainCells(int mountainId, System.Collections.Generic.List<Vector2Int> results)
    {
        if (results == null || !ready || mountainIds == null)
            return;
        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                if (mountainIds[z * width + x] == mountainId)
                    results.Add(new Vector2Int(minX + x, minZ + z));
            }
        }
    }

    bool NearBiome(int x, int z, int radius, WorldBiome biome)
    {
        int x0 = Mathf.Max(0, x - radius);
        int x1 = Mathf.Min(width - 1, x + radius);
        int z0 = Mathf.Max(0, z - radius);
        int z1 = Mathf.Min(height - 1, z + radius);
        for (int zz = z0; zz <= z1; zz++)
        {
            for (int xx = x0; xx <= x1; xx++)
            {
                if (GetLocal(xx, zz) == biome)
                    return true;
            }
        }
        return false;
    }

    bool NearLake(int x, int z, int radius)
    {
        int x0 = Mathf.Max(0, x - radius);
        int x1 = Mathf.Min(width - 1, x + radius);
        int z0 = Mathf.Max(0, z - radius);
        int z1 = Mathf.Min(height - 1, z + radius);
        for (int zz = z0; zz <= z1; zz++)
        {
            for (int xx = x0; xx <= x1; xx++)
            {
                if (GetLocal(xx, zz) == WorldBiome.Lake)
                    return true;
            }
        }
        return false;
    }

    float LakeNoise(int x, int z, float seedOff)
    {
        float wx = x + (Mathf.PerlinNoise(x * 0.02f + seedOff, z * 0.02f) - 0.5f) * 18f;
        float wz = z + (Mathf.PerlinNoise(x * 0.02f + 40f + seedOff, z * 0.02f) - 0.5f) * 18f;
        float a = Mathf.PerlinNoise(wx * 0.028f + seedOff, wz * 0.028f);
        float b = Mathf.PerlinNoise(wx * 0.07f + 90f + seedOff, wz * 0.07f);
        return a * 0.72f + b * 0.28f;
    }

    float MountainNoise(int x, int z, float seedOff)
    {
        float wx = x + (Mathf.PerlinNoise(x * 0.015f + seedOff + 3f, z * 0.015f) - 0.5f) * 22f;
        float wz = z + (Mathf.PerlinNoise(x * 0.015f + seedOff + 8f, z * 0.015f) - 0.5f) * 22f;
        float a = Mathf.PerlinNoise(wx * 0.018f + seedOff + 21f, wz * 0.018f);
        float b = Mathf.PerlinNoise(wx * 0.05f + seedOff + 55f, wz * 0.05f);
        return a * 0.75f + b * 0.25f;
    }

    float VegNoise(int x, int z, float seedOff)
    {
        float a = Mathf.PerlinNoise(x * 0.04f + seedOff + 11f, z * 0.04f);
        float b = Mathf.PerlinNoise(x * 0.11f + seedOff + 70f, z * 0.11f);
        return a * 0.65f + b * 0.35f;
    }

    void Set(int x, int z, WorldBiome biome)
    {
        cells[z * width + x] = biome;
    }

    WorldBiome GetLocal(int x, int z)
    {
        if (x < 0 || z < 0 || x >= width || z >= height)
            return WorldBiome.Ocean;
        return cells[z * width + x];
    }

    public WorldBiome Get(Vector2Int cell)
    {
        if (!ready)
            return WorldBiome.Field;
        return GetLocal(cell.x - minX, cell.y - minZ);
    }

    public bool IsWater(Vector2Int cell)
    {
        WorldBiome biome = Get(cell);
        return biome == WorldBiome.Lake || biome == WorldBiome.Ocean;
    }

    public static bool CanBuild(Vector2Int origin, Vector2Int size, bool allowWater = false, bool requireWater = false)
    {
        if (Instance == null || !Instance.ready)
            return true;

        size = GridOccupancy.NormalizeSize(size);
        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
            {
                bool water = Instance.IsWater(origin + new Vector2Int(x, y));
                if (requireWater)
                {
                    if (!water)
                        return false;
                }
                else if (water && !allowWater)
                {
                    return false;
                }
            }
        }
        return true;
    }

    public Color ColorOf(WorldBiome biome)
    {
        switch (biome)
        {
            case WorldBiome.Forest: return forest;
            case WorldBiome.Woodland: return woodland;
            case WorldBiome.Field: return field;
            case WorldBiome.Beach: return beach;
            case WorldBiome.Lake: return lake;
            case WorldBiome.MountainPeak: return mountainPeak;
            case WorldBiome.MountainSlope: return mountainSlope;
            default: return ocean;
        }
    }

    void PaintOverlay()
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;

        var pixels = new Color32[width * height];
        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                Color c = ColorOf(GetLocal(x, z));
                float n = (Mathf.PerlinNoise(x * 0.35f, z * 0.35f) - 0.5f) * 0.06f;
                c.r = Mathf.Clamp01(c.r + n);
                c.g = Mathf.Clamp01(c.g + n);
                c.b = Mathf.Clamp01(c.b + n);
                pixels[z * width + x] = c;
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply(false, false);
        biomeTexture = tex;

        if (overlay == null)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "BiomeOverlay";
            go.transform.SetParent(terrain.transform, false);
            Collider col = go.GetComponent<Collider>();
            if (col != null)
                Destroy(col);
            overlay = go.GetComponent<MeshRenderer>();
            overlay.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            overlay.receiveShadows = false;
        }

        Vector3 pos = terrain.GetPosition();
        Vector3 size = terrain.terrainData.size;
        overlay.transform.position = new Vector3(
            pos.x + size.x * 0.5f,
            0.01f,
            pos.z + size.z * 0.5f);
        overlay.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        overlay.transform.localScale = new Vector3(size.x, size.z, 1f);

        overlay.sharedMaterial = RuntimeMaterials.Create(tex, Color.white);
    }
}
