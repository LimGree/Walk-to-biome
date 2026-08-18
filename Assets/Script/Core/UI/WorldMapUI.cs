using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class WorldMapUI : MonoBehaviour
{
    public static WorldMapUI Instance { get; private set; }

    public bool IsOpen { get; private set; }

    static readonly Color BuildingColor = new Color(0.55f, 1f, 0.82f, 1f);
    static readonly Color BeltColor = new Color(0.82f, 0.62f, 0.28f, 1f);

    InputSystem_Actions input;
    VisualElement fullRoot;
    VisualElement miniFrame;
    Image miniImage;
    Image fullImage;
    VisualElement miniMarker;
    VisualElement fullMarker;
    Label tooltip;
    Texture2D mapTex;
    Rect viewUv = new Rect(0f, 0f, 1f, 1f);
    Rect miniUv = new Rect(0f, 0f, 1f, 1f);
    bool dragging;
    Vector2 lastMouse;
    float miniZoom = 0.18f;
    float miniSize = 220f;

    void Awake()
    {
        Instance = this;
        input = KeybindStore.Shared;
        miniZoom = PlayerPrefs.GetFloat("MiniMapZoom", 0.18f);
        miniSize = PlayerPrefs.GetFloat("MiniMapSize", 220f);
        BuildUi();
        ApplyMiniSize();
    }

    void OnEnable()
    {
        if (input != null)
            input.Player.MoveSelection.performed += OnMapToggle;
    }

    void OnDisable()
    {
        if (input != null)
            input.Player.MoveSelection.performed -= OnMapToggle;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        if (mapTex != null)
            Destroy(mapTex);
    }

    public float MiniZoom
    {
        get => miniZoom;
        set
        {
            miniZoom = Mathf.Clamp(value, 0.06f, 1f);
            PlayerPrefs.SetFloat("MiniMapZoom", miniZoom);
        }
    }

    public float MiniSize
    {
        get => miniSize;
        set
        {
            miniSize = Mathf.Clamp(value, 140f, 360f);
            PlayerPrefs.SetFloat("MiniMapSize", miniSize);
            ApplyMiniSize();
        }
    }

    void LateUpdate()
    {
        if (mapTex == null)
            Rebuild();

        UpdateMiniView();
        UpdateMarker(miniMarker, miniImage, miniUv);

        if (IsOpen)
        {
            if (fullImage != null)
                fullImage.uv = viewUv;
            UpdateMarker(fullMarker, fullImage, viewUv);
        }
    }

    void OnMapToggle(InputAction.CallbackContext ctx)
    {
        PlayerBuilder builder = GameManager.Instance != null
            ? GameManager.Instance.playerBuilder
            : FindFirstObjectByType<PlayerBuilder>();
        if (KeybindStore.BlocksGameplayInput)
            return;
        if (builder != null && builder.isBuildMode)
            return;
        if (GameManager.Instance != null && GameManager.Instance.IsPaused)
            return;
        if (MachineUI.Instance != null && MachineUI.Instance.IsOpen)
            return;

        SetOpen(!IsOpen);
    }

    public void SetOpen(bool open)
    {
        IsOpen = open;
        IndustryUi.Show(fullRoot, open);
        IndustryUi.Show(miniFrame, !open);
        if (open)
        {
            viewUv = new Rect(0f, 0f, 1f, 1f);
            dragging = false;
            Rebuild();
        }
        if (GameManager.Instance != null)
            GameManager.Instance.RestoreGameplayFocus();
    }

    public void Rebuild()
    {
        WorldBiomeMap map = WorldBiomeMap.Instance;
        if (map == null || !map.IsReady || map.BiomeTexture == null)
            return;

        int w = map.MapWidth;
        int h = map.MapHeight;
        if (mapTex == null || mapTex.width != w || mapTex.height != h)
        {
            if (mapTex != null)
                Destroy(mapTex);
            mapTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            mapTex.filterMode = FilterMode.Point;
            mapTex.wrapMode = TextureWrapMode.Clamp;
        }

        Color[] pixels = map.BiomeTexture.GetPixels();
        var scatter = WorldResourceScatterer.Instance;
        if (scatter != null)
        {
            var veins = scatter.Veins;
            for (int i = 0; i < veins.Count; i++)
                PaintCell(pixels, w, h, veins[i].cell, veins[i].color);
        }

        PaintBuildings(pixels, w, h, map);
        mapTex.SetPixels(pixels);
        mapTex.Apply(false, false);
        if (miniImage != null)
            miniImage.image = mapTex;
        if (fullImage != null)
            fullImage.image = mapTex;
    }

    static void PaintBuildings(Color[] pixels, int w, int h, WorldBiomeMap map)
    {
        var objects = new List<GameObject>(64);
        GridOccupancy.CollectAllOccupiedObjects(objects, new HashSet<int>());
        var cells = new List<Vector2Int>(8);
        for (int i = 0; i < objects.Count; i++)
        {
            GameObject go = objects[i];
            if (go == null)
                continue;
            Color color = go.GetComponent<Conveyor>() != null ? BeltColor : BuildingColor;
            if (!GridOccupancy.TryGetCells(go, cells))
                continue;
            for (int c = 0; c < cells.Count; c++)
                PaintCell(pixels, w, h, cells[c], color);
        }
    }

    static void PaintCell(Color[] pixels, int w, int h, Vector2Int cell, Color color)
    {
        WorldBiomeMap map = WorldBiomeMap.Instance;
        if (map == null)
            return;
        int x = cell.x - map.MapMinX;
        int z = cell.y - map.MapMinZ;
        if (x < 0 || z < 0 || x >= w || z >= h)
            return;
        pixels[z * w + x] = color;
    }

    void BuildUi()
    {
        VisualElement root = IndustryUi.Mount(this, 95);
        miniFrame = IndustryUi.El("Mini", "mini-map", "map-frame");
        miniImage = MakeMapImage("MiniImage");
        miniMarker = IndustryUi.El("MiniMark", "player-mark");
        miniMarker.pickingMode = PickingMode.Ignore;
        miniImage.Add(miniMarker);
        miniFrame.Add(miniImage);
        miniImage.RegisterCallback<WheelEvent>(OnMiniWheel);
        miniImage.RegisterCallback<PointerMoveEvent>(evt => UpdateTooltip(miniImage, miniUv, evt.localPosition, evt.position));
        miniImage.RegisterCallback<PointerLeaveEvent>(_ => IndustryUi.Show(tooltip, false));
        root.Add(miniFrame);

        fullRoot = IndustryUi.Screen("FullMap");
        fullRoot.Add(IndustryUi.El("Dim", "dim"));
        var wrap = IndustryUi.El("FullWrap", "map-frame", "map-full-wrap");
        fullImage = MakeMapImage("FullImage");
        fullImage.AddToClassList("map-full");
        fullMarker = IndustryUi.El("FullMark", "player-mark");
        fullMarker.pickingMode = PickingMode.Ignore;
        fullImage.Add(fullMarker);
        wrap.Add(fullImage);
        fullRoot.Add(wrap);
        fullImage.RegisterCallback<WheelEvent>(OnFullWheel);
        fullImage.RegisterCallback<PointerDownEvent>(OnFullDown);
        fullImage.RegisterCallback<PointerMoveEvent>(OnFullMove);
        fullImage.RegisterCallback<PointerUpEvent>(OnFullUp);
        fullImage.RegisterCallback<PointerLeaveEvent>(_ =>
        {
            if (!IsOpen)
                return;
            IndustryUi.Show(tooltip, false);
        });
        IndustryUi.Show(fullRoot, false);
        root.Add(fullRoot);

        tooltip = IndustryUi.Text("Tip", "", "tooltip");
        tooltip.pickingMode = PickingMode.Ignore;
        IndustryUi.Show(tooltip, false);
        root.Add(tooltip);
    }

    static Image MakeMapImage(string name)
    {
        var image = new Image { name = name };
        image.scaleMode = ScaleMode.StretchToFill;
        image.uv = new Rect(0f, 0f, 1f, 1f);
        return image;
    }

    void ApplyMiniSize()
    {
        if (miniFrame == null || miniImage == null)
            return;
        miniFrame.style.width = miniSize + 12f;
        miniFrame.style.height = miniSize + 12f;
        miniImage.style.width = miniSize;
        miniImage.style.height = miniSize;
    }

    void UpdateMiniView()
    {
        if (miniImage == null || WorldBiomeMap.Instance == null || !WorldBiomeMap.Instance.IsReady)
            return;

        Vector2 center = PlayerUv();
        float z = Mathf.Clamp(miniZoom, 0.06f, 1f);
        miniUv = new Rect(center.x - z * 0.5f, center.y - z * 0.5f, z, z);
        miniUv.x = Mathf.Clamp(miniUv.x, 0f, 1f - miniUv.width);
        miniUv.y = Mathf.Clamp(miniUv.y, 0f, 1f - miniUv.height);
        miniImage.uv = miniUv;
    }

    void OnMiniWheel(WheelEvent evt)
    {
        if (IsOpen)
            return;
        MiniZoom *= evt.delta.y > 0f ? 0.85f : 1.18f;
        evt.StopPropagation();
    }

    void OnFullWheel(WheelEvent evt)
    {
        if (!IsOpen)
            return;
        ZoomAt(fullImage, evt.localMousePosition, evt.delta.y > 0f ? 0.82f : 1.22f);
        evt.StopPropagation();
    }

    void OnFullDown(PointerDownEvent evt)
    {
        if (!IsOpen || evt.button != 0)
            return;
        dragging = true;
        lastMouse = (Vector2)evt.position;
        fullImage.CapturePointer(evt.pointerId);
        evt.StopPropagation();
    }

    void OnFullMove(PointerMoveEvent evt)
    {
        if (IsOpen)
            UpdateTooltip(fullImage, viewUv, evt.localPosition, evt.position);
        if (!dragging)
            return;

        Vector2 delta = (Vector2)evt.position - lastMouse;
        lastMouse = (Vector2)evt.position;
        float w = Mathf.Max(1f, fullImage.resolvedStyle.width);
        float h = Mathf.Max(1f, fullImage.resolvedStyle.height);
        viewUv.x -= delta.x / w * viewUv.width;
        viewUv.y += delta.y / h * viewUv.height;
        ClampView();
        evt.StopPropagation();
    }

    void OnFullUp(PointerUpEvent evt)
    {
        if (!dragging)
            return;
        dragging = false;
        if (fullImage.HasPointerCapture(evt.pointerId))
            fullImage.ReleasePointer(evt.pointerId);
    }

    void ZoomAt(Image image, Vector2 local, float factor)
    {
        if (image == null)
            return;
        float w = Mathf.Max(1f, image.resolvedStyle.width);
        float h = Mathf.Max(1f, image.resolvedStyle.height);
        float nx = Mathf.Clamp01(local.x / w);
        float ny = 1f - Mathf.Clamp01(local.y / h);
        float worldU = viewUv.x + nx * viewUv.width;
        float worldV = viewUv.y + ny * viewUv.height;
        float zoom = Mathf.Clamp(viewUv.width * factor, 0.06f, 1f);
        viewUv.width = zoom;
        viewUv.height = zoom;
        viewUv.x = worldU - nx * zoom;
        viewUv.y = worldV - ny * zoom;
        ClampView();
    }

    void ClampView()
    {
        viewUv.width = Mathf.Clamp(viewUv.width, 0.06f, 1f);
        viewUv.height = viewUv.width;
        viewUv.x = Mathf.Clamp(viewUv.x, 0f, 1f - viewUv.width);
        viewUv.y = Mathf.Clamp(viewUv.y, 0f, 1f - viewUv.height);
    }

    Vector2 PlayerUv()
    {
        WorldBiomeMap map = WorldBiomeMap.Instance;
        if (map == null || !map.IsReady)
            return new Vector2(0.5f, 0.5f);

        PlayerBuilder builder = GameManager.Instance != null
            ? GameManager.Instance.playerBuilder
            : FindFirstObjectByType<PlayerBuilder>();
        if (builder == null)
            return new Vector2(0.5f, 0.5f);

        Vector2Int cell = GridSystem.Instance != null
            ? GridSystem.Instance.WorldToCell(builder.transform.position)
            : new Vector2Int(Mathf.RoundToInt(builder.transform.position.x), Mathf.RoundToInt(builder.transform.position.z));
        return new Vector2(
            (cell.x - map.MapMinX + 0.5f) / map.MapWidth,
            (cell.y - map.MapMinZ + 0.5f) / map.MapHeight);
    }

    void UpdateTooltip(Image image, Rect uv, Vector2 local, Vector2 panelPos)
    {
        if (tooltip == null || image == null)
            return;
        if (!LocalToCell(image, uv, local, out Vector2Int cell))
        {
            IndustryUi.Show(tooltip, false);
            return;
        }

        string label = LabelAt(cell);
        if (string.IsNullOrEmpty(label))
        {
            IndustryUi.Show(tooltip, false);
            return;
        }

        tooltip.text = label;
        tooltip.style.left = panelPos.x + 18f;
        tooltip.style.top = panelPos.y - 18f;
        IndustryUi.Show(tooltip, true);
    }

    static bool LocalToCell(Image image, Rect uv, Vector2 local, out Vector2Int cell)
    {
        cell = default;
        WorldBiomeMap map = WorldBiomeMap.Instance;
        if (map == null || !map.IsReady || image == null)
            return false;
        float w = image.resolvedStyle.width;
        float h = image.resolvedStyle.height;
        if (w < 1f || h < 1f)
            return false;

        float nx = Mathf.Clamp01(local.x / w);
        float ny = 1f - Mathf.Clamp01(local.y / h);
        float u = uv.x + nx * uv.width;
        float v = uv.y + ny * uv.height;
        int x = map.MapMinX + Mathf.FloorToInt(u * map.MapWidth);
        int z = map.MapMinZ + Mathf.FloorToInt(v * map.MapHeight);
        cell = new Vector2Int(x, z);
        return true;
    }

    static string LabelAt(Vector2Int cell)
    {
        if (WorldResourceScatterer.Instance != null
            && WorldResourceScatterer.Instance.TryGetVeinLabel(cell, out string vein))
            return vein;

        GameObject obj = GridOccupancy.GetAt(cell);
        if (obj == null)
            return null;

        Conveyor belt = obj.GetComponent<Conveyor>();
        if (belt != null)
            return "Конвейер";

        BuildingBase building = obj.GetComponent<BuildingBase>();
        if (building != null && building.data != null && !string.IsNullOrEmpty(building.data.displayName))
            return building.data.displayName;
        return obj.name;
    }

    void UpdateMarker(VisualElement marker, Image image, Rect uv)
    {
        if (marker == null || image == null || WorldBiomeMap.Instance == null || !WorldBiomeMap.Instance.IsReady)
            return;

        PlayerBuilder builder = GameManager.Instance != null
            ? GameManager.Instance.playerBuilder
            : FindFirstObjectByType<PlayerBuilder>();
        if (builder == null)
            return;

        Vector2Int cell = GridSystem.Instance != null
            ? GridSystem.Instance.WorldToCell(builder.transform.position)
            : new Vector2Int(Mathf.RoundToInt(builder.transform.position.x), Mathf.RoundToInt(builder.transform.position.z));

        WorldBiomeMap map = WorldBiomeMap.Instance;
        float u = (cell.x - map.MapMinX + 0.5f) / map.MapWidth;
        float v = (cell.y - map.MapMinZ + 0.5f) / map.MapHeight;
        if (u < uv.x || v < uv.y || u > uv.x + uv.width || v > uv.y + uv.height)
        {
            IndustryUi.Show(marker, false);
            return;
        }

        float w = image.resolvedStyle.width;
        float h = image.resolvedStyle.height;
        if (w < 1f || h < 1f)
            return;

        IndustryUi.Show(marker, true);
        float lx = (u - uv.x) / uv.width;
        float ly = (v - uv.y) / uv.height;
        marker.style.left = lx * w - 6f;
        marker.style.top = (1f - ly) * h - 8f;
        marker.style.rotate = new Rotate(Angle.Degrees(-builder.transform.eulerAngles.y));
    }
}
