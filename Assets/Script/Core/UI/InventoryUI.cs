using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventoryUI : MonoBehaviour
{
    public static InventoryUI Instance { get; private set; }

    [Header("References")]
    public PlayerInventory inventory;
    public Transform hotbarParent;
    public GameObject slotPrefab;
    public PlayerBuilder playerBuilder;

    [Header("Show / hide")]
    public float slideDistance = 140f;
    public float slideSpeed = 7.5f;
    public float bounce = 1.15f;

    public bool IsBagOpen { get; private set; }

    Image[] slotIcons;
    Image[] slotHighlights;
    GameObject[] slotRoots;
    Image emptyHighlight;
    Image emptyBg;
    RectTransform clusterRt;
    GameObject emptyTool;
    GameObject bagRoot;
    Transform bagGrid;
    RectTransform dragGhost;
    Image dragGhostIcon;
    Vector2 shownPos;
    Vector2 hiddenPos;
    float anim;
    bool wantVisible;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        if (inventory != null)
        {
            inventory.OnSelectionChanged += UpdateSelection;
            inventory.OnHotbarChanged += RefreshHotbar;
        }
        if (ResearchSystem.Instance != null)
            ResearchSystem.Instance.OnUnlocksChanged += RefreshHotbar;

        if (playerBuilder == null)
            playerBuilder = FindFirstObjectByType<PlayerBuilder>();
        if (playerBuilder != null)
            playerBuilder.OnBuildModeChanged += OnBuildModeChanged;

        EnsureCluster();
        StyleHotbar();
        EnsureHotbarSlots();
        BindHotbarIcons();
        UpdateSelection(inventory != null ? inventory.selectedIndex : 0);

        CacheSlide();
        wantVisible = playerBuilder != null && playerBuilder.isBuildMode;
        anim = wantVisible ? 1f : 0f;
        ApplySlide(anim);
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        if (inventory != null)
        {
            inventory.OnSelectionChanged -= UpdateSelection;
            inventory.OnHotbarChanged -= RefreshHotbar;
        }

        if (playerBuilder != null)
            playerBuilder.OnBuildModeChanged -= OnBuildModeChanged;
        if (ResearchSystem.Instance != null)
            ResearchSystem.Instance.OnUnlocksChanged -= RefreshHotbar;
    }

    void Update()
    {
        if (clusterRt == null)
            return;

        float target = wantVisible ? 1f : 0f;
        if (Mathf.Approximately(anim, target))
            return;

        anim = Mathf.MoveTowards(anim, target, Time.unscaledDeltaTime * slideSpeed);
        ApplySlide(anim);
    }

    void OnBuildModeChanged(bool enabled)
    {
        wantVisible = enabled;
        if (!enabled)
            SetBagOpen(false);
    }

    public void ToggleBag()
    {
        SetBagOpen(!IsBagOpen);
    }

    public void SetBagOpen(bool open)
    {
        if (IsBagOpen == open)
        {
            if (open)
                RefreshBag();
            return;
        }

        IsBagOpen = open;
        if (open)
        {
            EnsureBag();
            RefreshBag();
        }

        if (bagRoot != null)
            bagRoot.SetActive(open);

        if (GameManager.Instance != null)
            GameManager.Instance.RestoreGameplayFocus();
        else
        {
            Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = open;
        }
    }

    void EnsureCluster()
    {
        if (hotbarParent == null || clusterRt != null)
            return;

        RectTransform bar = hotbarParent as RectTransform;
        Transform parent = hotbarParent.parent;
        int sibling = hotbarParent.GetSiblingIndex();

        GameObject cluster = new GameObject("HotbarCluster", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        cluster.transform.SetParent(parent, false);
        cluster.transform.SetSiblingIndex(sibling);
        clusterRt = cluster.GetComponent<RectTransform>();
        clusterRt.anchorMin = bar.anchorMin;
        clusterRt.anchorMax = bar.anchorMax;
        clusterRt.pivot = bar.pivot;
        clusterRt.anchoredPosition = bar.anchoredPosition;
        clusterRt.sizeDelta = bar.sizeDelta;

        HorizontalLayoutGroup clusterLayout = cluster.GetComponent<HorizontalLayoutGroup>();
        clusterLayout.childAlignment = TextAnchor.MiddleCenter;
        clusterLayout.spacing = 28f;
        clusterLayout.childControlWidth = false;
        clusterLayout.childControlHeight = false;
        clusterLayout.childForceExpandWidth = false;
        clusterLayout.childForceExpandHeight = false;
        cluster.GetComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        cluster.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        hotbarParent.SetParent(clusterRt, false);
    }

    void CacheSlide()
    {
        if (clusterRt == null)
            clusterRt = hotbarParent as RectTransform;
        if (clusterRt == null)
            return;

        shownPos = clusterRt.anchoredPosition;
        hiddenPos = shownPos + new Vector2(0f, -Mathf.Abs(slideDistance));
    }

    void ApplySlide(float t)
    {
        if (clusterRt == null)
            return;

        float e = wantVisible ? EaseOutBack(t) : 1f - EaseOutBack(1f - t);
        clusterRt.anchoredPosition = Vector2.LerpUnclamped(hiddenPos, shownPos, e);
    }

    float EaseOutBack(float t)
    {
        t = Mathf.Clamp01(t);
        if (t <= 0f || t >= 1f)
            return t;

        float overshoot = 1f + bounce;
        return 1f + overshoot * Mathf.Pow(t - 1f, 3f) + bounce * Mathf.Pow(t - 1f, 2f);
    }

    void StyleHotbar()
    {
        if (hotbarParent == null)
            return;

        Image bar = hotbarParent.GetComponent<Image>();
        if (bar != null)
            UiTheme.StyleImage(bar, UiTheme.Panel);

        HorizontalLayoutGroup row = hotbarParent.GetComponent<HorizontalLayoutGroup>();
        if (row == null)
            row = hotbarParent.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.childAlignment = TextAnchor.MiddleCenter;
        row.spacing = 8f;
        row.padding = new RectOffset(12, 12, 8, 8);
        row.childControlWidth = false;
        row.childControlHeight = false;
        row.childForceExpandWidth = false;
        row.childForceExpandHeight = false;

        ContentSizeFitter fit = hotbarParent.GetComponent<ContentSizeFitter>();
        if (fit == null)
            fit = hotbarParent.gameObject.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    void EnsureHotbarSlots()
    {
        if (hotbarParent == null || inventory == null)
            return;

        int size = inventory.hotbarSize;
        if (slotRoots == null || slotRoots.Length != size || slotRoots[0] == null)
        {
            for (int i = hotbarParent.childCount - 1; i >= 0; i--)
                DestroyImmediate(hotbarParent.GetChild(i).gameObject);

            slotRoots = new GameObject[size];
            slotIcons = new Image[size];
            slotHighlights = new Image[size];

            for (int i = 0; i < size; i++)
            {
                GameObject slot = UiFactory.CreateHotbarSlot(hotbarParent, i);
                slot.GetComponent<RectTransform>().sizeDelta = new Vector2(80f, 80f);
                slotRoots[i] = slot;
                slotIcons[i] = slot.transform.Find("Icon")?.GetComponent<Image>();
                slotHighlights[i] = slot.transform.Find("Highlight")?.GetComponent<Image>();
                HotbarSlotView view = slot.GetComponent<HotbarSlotView>();
                if (view == null)
                    view = slot.AddComponent<HotbarSlotView>();
                view.Bind(this, i);
            }
        }

        EnsureSingleEmptyTool();
    }

    void EnsureSingleEmptyTool()
    {
        if (clusterRt == null)
            return;

        for (int i = clusterRt.childCount - 1; i >= 0; i--)
        {
            Transform child = clusterRt.GetChild(i);
            if (child == null || child == hotbarParent)
                continue;
            if (emptyTool != null && child.gameObject == emptyTool)
                continue;
            if (child.name == "EmptyTool" || child.name.StartsWith("Slot_"))
                DestroyImmediate(child.gameObject);
        }

        if (emptyTool != null)
            return;

        GameObject slot = UiFactory.CreateHotbarSlot(clusterRt, 9);
        slot.name = "EmptyTool";
        emptyTool = slot;
        slot.GetComponent<RectTransform>().sizeDelta = new Vector2(80f, 80f);
        Image bg = slot.GetComponent<Image>();
        UiTheme.StyleImage(bg, UiTheme.Chip);
        emptyBg = bg;

        Transform key = slot.transform.Find("Key");
        if (key != null)
        {
            TextMeshProUGUI keyText = key.GetComponent<TextMeshProUGUI>();
            if (keyText != null)
                keyText.text = "0";
        }

        Image icon = slot.transform.Find("Icon")?.GetComponent<Image>();
        if (icon != null)
            icon.enabled = false;

        TextMeshProUGUI label = UiTheme.AddText(slot.transform, "EmptyLabel", "выбор", 13f, UiTheme.TextDim);
        label.alignment = TextAlignmentOptions.Center;
        RectTransform labelRt = label.rectTransform;
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = new Vector2(6f, 8f);
        labelRt.offsetMax = new Vector2(-6f, -8f);

        emptyHighlight = slot.transform.Find("Highlight")?.GetComponent<Image>();
        Button button = slot.GetComponent<Button>();
        if (button == null)
            button = slot.AddComponent<Button>();
        button.targetGraphic = bg;
        button.onClick.AddListener(() =>
        {
            if (inventory != null)
                inventory.SelectEmptyTool();
        });
    }

    void BindHotbarIcons()
    {
        if (inventory == null || slotIcons == null)
            return;
        for (int i = 0; i < slotIcons.Length; i++)
        {
            BuildingData building = inventory.hotbar != null && i < inventory.hotbar.Length
                ? inventory.hotbar[i]
                : null;
            if (slotIcons[i] == null)
                continue;
            slotIcons[i].sprite = building != null ? building.icon : null;
            slotIcons[i].enabled = building != null && building.icon != null;
        }
    }

    void UpdateSelection(int selectedIndex)
    {
        if (slotHighlights != null)
        {
            for (int i = 0; i < slotHighlights.Length; i++)
            {
                bool on = i == selectedIndex;
                if (slotHighlights[i] != null)
                    slotHighlights[i].enabled = on;
                Image bg = slotHighlights[i] != null
                    ? slotHighlights[i].transform.parent.GetComponent<Image>()
                    : null;
                if (bg != null)
                    bg.color = on ? UiTheme.AccentDim : UiTheme.Card;
            }
        }

        bool emptyOn = inventory != null && inventory.IsEmptyToolSelected;
        if (emptyHighlight != null)
            emptyHighlight.enabled = emptyOn;
        if (emptyBg != null)
            emptyBg.color = emptyOn ? UiTheme.AccentDim : UiTheme.Chip;
    }

    [ContextMenu("Refresh Hotbar")]
    public void RefreshHotbar()
    {
        EnsureHotbarSlots();
        BindHotbarIcons();
        UpdateSelection(inventory != null ? inventory.selectedIndex : 0);
        if (IsBagOpen)
            RefreshBag();
    }

    public void OnHotbarClicked(int index, PointerEventData eventData)
    {
        if (inventory == null)
            return;
        if (eventData != null && eventData.button == PointerEventData.InputButton.Right && IsBagOpen)
        {
            inventory.UnequipSlot(index);
            return;
        }

        inventory.SelectSlot(index);
    }

    public void OnBagClicked(BuildingData building, PointerEventData eventData)
    {
        if (inventory == null || building == null)
            return;
        if (eventData != null && eventData.button == PointerEventData.InputButton.Right)
        {
            int have = inventory.IndexOf(building);
            if (have >= 0)
                inventory.UnequipSlot(have);
            return;
        }

        inventory.EquipToFirstEmpty(building);
    }

    public void BeginBagDrag(BuildingData building, PointerEventData eventData)
    {
        if (building == null)
            return;
        ShowDragGhost(building.icon, eventData);
    }

    public void BeginHotbarDrag(int index, PointerEventData eventData)
    {
        if (inventory == null || !IsBagOpen)
            return;
        BuildingData building = inventory.hotbar != null && index >= 0 && index < inventory.hotbar.Length
            ? inventory.hotbar[index]
            : null;
        if (building == null)
            return;
        ShowDragGhost(building.icon, eventData);
    }

    public void UpdateDrag(PointerEventData eventData)
    {
        if (dragGhost == null || eventData == null)
            return;
        dragGhost.position = eventData.position;
    }

    public void EndBagDrag(BuildingData building, PointerEventData eventData)
    {
        HideDragGhost();
        if (building == null || inventory == null)
            return;

        int slot = HitHotbarSlot(eventData);
        if (slot >= 0)
            inventory.SwapOrPlace(building, slot);
    }

    public void EndHotbarDrag(int index, PointerEventData eventData)
    {
        HideDragGhost();
        if (inventory == null || !IsBagOpen)
            return;

        int dest = HitHotbarSlot(eventData);
        BuildingData building = inventory.hotbar != null && index >= 0 && index < inventory.hotbar.Length
            ? inventory.hotbar[index]
            : null;
        if (building == null)
            return;

        if (dest >= 0)
        {
            inventory.SwapOrPlace(building, dest);
            return;
        }

        if (HitBag(eventData))
            inventory.UnequipSlot(index);
    }

    int HitHotbarSlot(PointerEventData eventData)
    {
        if (eventData == null || EventSystem.current == null || slotRoots == null)
            return -1;

        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, hits);
        for (int i = 0; i < hits.Count; i++)
        {
            HotbarSlotView view = hits[i].gameObject.GetComponentInParent<HotbarSlotView>();
            if (view != null)
                return view.index;
        }

        return -1;
    }

    bool HitBag(PointerEventData eventData)
    {
        if (bagRoot == null || eventData == null || EventSystem.current == null)
            return false;

        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, hits);
        for (int i = 0; i < hits.Count; i++)
        {
            if (hits[i].gameObject.transform.IsChildOf(bagRoot.transform) || hits[i].gameObject == bagRoot)
                return true;
        }

        return false;
    }

    void ShowDragGhost(Sprite icon, PointerEventData eventData)
    {
        EnsureDragGhost();
        if (dragGhost == null)
            return;
        dragGhost.gameObject.SetActive(true);
        if (dragGhostIcon != null)
        {
            dragGhostIcon.sprite = icon;
            dragGhostIcon.enabled = icon != null;
        }
        UpdateDrag(eventData);
    }

    void HideDragGhost()
    {
        if (dragGhost != null)
            dragGhost.gameObject.SetActive(false);
    }

    void EnsureDragGhost()
    {
        if (dragGhost != null)
            return;

        Canvas canvas = GetComponentInParent<Canvas>();
        Transform parent = canvas != null ? canvas.transform : transform;
        GameObject go = new GameObject("DragGhost", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        dragGhost = go.GetComponent<RectTransform>();
        dragGhost.sizeDelta = new Vector2(64f, 64f);
        dragGhostIcon = go.GetComponent<Image>();
        dragGhostIcon.raycastTarget = false;
        dragGhostIcon.preserveAspect = true;
        dragGhostIcon.color = new Color(1f, 1f, 1f, 0.9f);
        go.SetActive(false);
    }

    void EnsureBag()
    {
        if (bagRoot != null)
            return;

        Canvas canvas = GetComponentInParent<Canvas>();
        Transform parent = canvas != null ? canvas.transform : transform;

        bagRoot = new GameObject("BuildingBag", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        bagRoot.transform.SetParent(parent, false);
        RectTransform rt = bagRoot.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.18f);
        rt.anchorMax = new Vector2(0.5f, 0.18f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 120f);
        rt.sizeDelta = new Vector2(860f, 420f);
        UiTheme.StyleImage(bagRoot.GetComponent<Image>(), UiTheme.Panel);

        TextMeshProUGUI title = UiTheme.AddText(bagRoot.transform, "Title", "ИНВЕНТАРЬ ЗДАНИЙ", 22f, UiTheme.Accent);
        title.alignment = TextAlignmentOptions.Center;
        title.fontStyle = FontStyles.Bold;
        RectTransform titleRt = title.rectTransform;
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.anchoredPosition = new Vector2(0f, -10f);
        titleRt.sizeDelta = new Vector2(-24f, 28f);

        TextMeshProUGUI hint = UiTheme.AddText(bagRoot.transform, "Hint", "ЛКМ — в свободный слот   перетащи в хотбар   ПКМ по слоту — убрать", 15f, UiTheme.TextDim);
        hint.alignment = TextAlignmentOptions.Center;
        RectTransform hintRt = hint.rectTransform;
        hintRt.anchorMin = new Vector2(0f, 1f);
        hintRt.anchorMax = new Vector2(1f, 1f);
        hintRt.pivot = new Vector2(0.5f, 1f);
        hintRt.anchoredPosition = new Vector2(0f, -38f);
        hintRt.sizeDelta = new Vector2(-24f, 22f);

        GameObject gridGo = new GameObject("Grid", typeof(RectTransform));
        gridGo.transform.SetParent(bagRoot.transform, false);
        RectTransform gridRt = gridGo.GetComponent<RectTransform>();
        gridRt.anchorMin = Vector2.zero;
        gridRt.anchorMax = Vector2.one;
        gridRt.offsetMin = new Vector2(16f, 16f);
        gridRt.offsetMax = new Vector2(-16f, -68f);
        bagGrid = gridRt;
        UiTheme.EnsureGrid(bagGrid, new Vector2(150f, 168f), new Vector2(12f, 12f), 5);
        UiTheme.EnsureVerticalScroll(gridRt);
        bagRoot.SetActive(false);
    }

    void RefreshBag()
    {
        EnsureBag();
        if (bagGrid == null || inventory == null)
            return;

        foreach (Transform child in bagGrid)
            Destroy(child.gameObject);

        List<BuildingData> unlocked = inventory.GetUnlockedBuildings();
        for (int i = 0; i < unlocked.Count; i++)
        {
            BuildingData building = unlocked[i];
            int slot = inventory.IndexOf(building);
            bool onBar = slot >= 0;
            string subtitle = onBar ? "хотбар  " + (slot + 1) : "в свободный слот";
            GameObject card = UiFactory.CreateBuildingCard(bagGrid, building, true, null);
            Button button = card.GetComponent<Button>();
            if (button != null)
                button.onClick.RemoveAllListeners();
            BagBuildingCard drag = card.GetComponent<BagBuildingCard>();
            if (drag == null)
                drag = card.AddComponent<BagBuildingCard>();
            drag.Bind(this, building);

            if (onBar)
            {
                Image bg = card.GetComponent<Image>();
                if (bg != null)
                    bg.color = UiTheme.AccentDim;
            }

            TextMeshProUGUI extra = UiTheme.AddText(card.transform, "BagMark", subtitle, 13f, onBar ? UiTheme.Accent : UiTheme.TextDim);
            extra.alignment = TextAlignmentOptions.Center;
            RectTransform extraRt = extra.rectTransform;
            extraRt.anchorMin = new Vector2(0f, 0f);
            extraRt.anchorMax = new Vector2(1f, 0f);
            extraRt.pivot = new Vector2(0.5f, 0f);
            extraRt.anchoredPosition = new Vector2(0f, 4f);
            extraRt.sizeDelta = new Vector2(-8f, 18f);
        }
    }
}
