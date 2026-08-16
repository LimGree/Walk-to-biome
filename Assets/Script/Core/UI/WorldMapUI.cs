using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class WorldMapUI : MonoBehaviour
{
    public static WorldMapUI Instance { get; private set; }

    public bool IsOpen { get; private set; }

    static readonly Color BuildingColor = new Color(0.55f, 1f, 0.82f, 1f);
    static readonly Color BeltColor = new Color(0.82f, 0.62f, 0.28f, 1f);

    InputSystem_Actions input;
    RawImage miniImage;
    RawImage fullImage;
    RectTransform miniMarker;
    RectTransform fullMarker;
    RectTransform miniViewRt;
    RectTransform fullViewRt;
    RectTransform miniFrameRt;
    GameObject fullRoot;
    GameObject tooltipRoot;
    TextMeshProUGUI tooltipText;
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
        input.Player.MoveSelection.performed += OnMapToggle;
    }

    void OnDisable()
    {
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
        UpdateMarker(miniMarker, miniSize, miniUv);

        if (IsOpen)
        {
            HandleZoomPan();
            if (fullImage != null)
                fullImage.uvRect = viewUv;
            UpdateMarker(fullMarker, 820f, viewUv);
            UpdateTooltip(fullViewRt, viewUv);
        }
        else
        {
            UpdateTooltip(miniViewRt, miniUv);
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
        if (fullRoot != null)
            fullRoot.SetActive(open);
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
            miniImage.texture = mapTex;
        if (fullImage != null)
            fullImage.texture = mapTex;
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

    void HandleZoomPan()
    {
        if (Mouse.current == null || fullViewRt == null)
            return;

        Vector2 mouse = Mouse.current.position.ReadValue();
        bool over = RectTransformUtility.RectangleContainsScreenPoint(fullViewRt, mouse, null);

        Vector2 scroll = Mouse.current.scroll.ReadValue();
        if (over && Mathf.Abs(scroll.y) > 0.01f)
            ZoomAt(mouse, scroll.y > 0f ? 0.82f : 1.22f);

        if (Mouse.current.leftButton.wasPressedThisFrame && over)
        {
            dragging = true;
            lastMouse = mouse;
        }
        if (dragging)
        {
            if (!Mouse.current.leftButton.isPressed)
            {
                dragging = false;
            }
            else
            {
                Vector2 delta = mouse - lastMouse;
                lastMouse = mouse;
                Vector2 size = fullViewRt.rect.size;
                if (size.x > 1f && size.y > 1f)
                {
                    viewUv.x -= delta.x / size.x * viewUv.width;
                    viewUv.y -= delta.y / size.y * viewUv.height;
                    ClampView();
                }
            }
        }
    }

    void ZoomAt(Vector2 screen, float factor)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(fullViewRt, screen, null, out Vector2 local))
            return;

        Vector2 size = fullViewRt.rect.size;
        float nx = Mathf.Clamp01(local.x / size.x + 0.5f);
        float ny = Mathf.Clamp01(local.y / size.y + 0.5f);
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

    void UpdateMiniView()
    {
        if (miniImage == null || WorldBiomeMap.Instance == null || !WorldBiomeMap.Instance.IsReady)
            return;

        if (!IsOpen && Mouse.current != null && miniViewRt != null)
        {
            Vector2 mouse = Mouse.current.position.ReadValue();
            if (RectTransformUtility.RectangleContainsScreenPoint(miniViewRt, mouse, null))
            {
                Vector2 scroll = Mouse.current.scroll.ReadValue();
                if (Mathf.Abs(scroll.y) > 0.01f)
                    MiniZoom *= scroll.y > 0f ? 0.85f : 1.18f;
            }
        }

        Vector2 center = PlayerUv();
        float z = Mathf.Clamp(miniZoom, 0.06f, 1f);
        miniUv = new Rect(center.x - z * 0.5f, center.y - z * 0.5f, z, z);
        miniUv.x = Mathf.Clamp(miniUv.x, 0f, 1f - miniUv.width);
        miniUv.y = Mathf.Clamp(miniUv.y, 0f, 1f - miniUv.height);
        miniImage.uvRect = miniUv;
    }

    void ApplyMiniSize()
    {
        if (miniFrameRt == null)
            return;
        miniFrameRt.sizeDelta = new Vector2(miniSize + 12f, miniSize + 12f);
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

    void UpdateTooltip(RectTransform view, Rect uv)
    {
        if (tooltipRoot == null || view == null || Mouse.current == null)
            return;

        Vector2 mouse = Mouse.current.position.ReadValue();
        if (!RectTransformUtility.RectangleContainsScreenPoint(view, mouse, null)
            || !ScreenToCell(view, uv, mouse, out Vector2Int cell))
        {
            tooltipRoot.SetActive(false);
            return;
        }

        string label = LabelAt(cell);
        if (string.IsNullOrEmpty(label))
        {
            tooltipRoot.SetActive(false);
            return;
        }

        tooltipRoot.SetActive(true);
        tooltipText.text = label;
        RectTransform tipRt = tooltipRoot.GetComponent<RectTransform>();
        tipRt.position = mouse + new Vector2(18f, -18f);
    }

    static bool ScreenToCell(RectTransform view, Rect uv, Vector2 screen, out Vector2Int cell)
    {
        cell = default;
        WorldBiomeMap map = WorldBiomeMap.Instance;
        if (map == null || !map.IsReady)
            return false;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(view, screen, null, out Vector2 local))
            return false;

        Vector2 size = view.rect.size;
        if (size.x < 1f || size.y < 1f)
            return false;

        float nx = Mathf.Clamp01(local.x / size.x + 0.5f);
        float ny = Mathf.Clamp01(local.y / size.y + 0.5f);
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

    void UpdateMarker(RectTransform marker, float size, Rect uv)
    {
        if (marker == null || WorldBiomeMap.Instance == null || !WorldBiomeMap.Instance.IsReady)
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
            marker.gameObject.SetActive(false);
            return;
        }

        marker.gameObject.SetActive(true);
        float lx = (u - uv.x) / uv.width - 0.5f;
        float ly = (v - uv.y) / uv.height - 0.5f;
        marker.anchoredPosition = new Vector2(lx * size, ly * size);
        marker.localRotation = Quaternion.Euler(0f, 0f, -builder.transform.eulerAngles.y);
    }

    void BuildUi()
    {
        GameObject canvasGo = new GameObject("WorldMapCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(transform, false);
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        miniImage = CreateMapFrame(canvasGo.transform, "Minimap", new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-28f, -28f), new Vector2(miniSize, miniSize), out miniMarker);
        miniViewRt = miniImage.rectTransform;
        miniFrameRt = miniImage.transform.parent as RectTransform;
        miniImage.raycastTarget = true;

        fullRoot = new GameObject("FullMap", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        fullRoot.transform.SetParent(canvasGo.transform, false);
        RectTransform fullRt = fullRoot.GetComponent<RectTransform>();
        fullRt.anchorMin = Vector2.zero;
        fullRt.anchorMax = Vector2.one;
        fullRt.offsetMin = Vector2.zero;
        fullRt.offsetMax = Vector2.zero;
        Image dim = fullRoot.GetComponent<Image>();
        dim.color = new Color(0.03f, 0.07f, 0.05f, 0.72f);
        dim.raycastTarget = true;

        fullImage = CreateMapFrame(fullRoot.transform, "Full", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(820f, 820f), out fullMarker);
        fullViewRt = fullImage.rectTransform;
        fullImage.raycastTarget = true;
        fullRoot.SetActive(false);

        tooltipRoot = new GameObject("Tooltip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        tooltipRoot.transform.SetParent(canvasGo.transform, false);
        RectTransform tipRt = tooltipRoot.GetComponent<RectTransform>();
        tipRt.pivot = new Vector2(0f, 1f);
        tipRt.sizeDelta = new Vector2(240f, 36f);
        UiTheme.StyleImage(tooltipRoot.GetComponent<Image>(), UiTheme.Panel);
        tooltipRoot.GetComponent<Image>().raycastTarget = false;
        tooltipText = UiTheme.AddText(tooltipRoot.transform, "Text", "", 20f, UiTheme.Text);
        tooltipText.alignment = TextAlignmentOptions.MidlineLeft;
        RectTransform textRt = tooltipText.rectTransform;
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(10f, 4f);
        textRt.offsetMax = new Vector2(-10f, -4f);
        tooltipRoot.SetActive(false);
    }

    static RawImage CreateMapFrame(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, out RectTransform marker)
    {
        GameObject frame = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        frame.transform.SetParent(parent, false);
        RectTransform frameRt = frame.GetComponent<RectTransform>();
        frameRt.anchorMin = anchor;
        frameRt.anchorMax = anchor;
        frameRt.pivot = pivot;
        frameRt.anchoredPosition = pos;
        frameRt.sizeDelta = size + new Vector2(12f, 12f);
        UiTheme.StyleImage(frame.GetComponent<Image>(), UiTheme.Panel);
        frame.GetComponent<Image>().raycastTarget = false;

        GameObject imgGo = new GameObject("Image", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        imgGo.transform.SetParent(frame.transform, false);
        RectTransform imgRt = imgGo.GetComponent<RectTransform>();
        imgRt.anchorMin = Vector2.zero;
        imgRt.anchorMax = Vector2.one;
        imgRt.offsetMin = new Vector2(6f, 6f);
        imgRt.offsetMax = new Vector2(-6f, -6f);
        RawImage raw = imgGo.GetComponent<RawImage>();
        raw.color = Color.white;
        raw.raycastTarget = false;

        GameObject mark = new GameObject("Player", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        mark.transform.SetParent(imgGo.transform, false);
        marker = mark.GetComponent<RectTransform>();
        marker.anchorMin = new Vector2(0.5f, 0.5f);
        marker.anchorMax = new Vector2(0.5f, 0.5f);
        marker.sizeDelta = new Vector2(12f, 16f);
        Image markImg = mark.GetComponent<Image>();
        markImg.color = new Color(1f, 0.95f, 0.35f, 1f);
        markImg.raycastTarget = false;

        return raw;
    }
}
