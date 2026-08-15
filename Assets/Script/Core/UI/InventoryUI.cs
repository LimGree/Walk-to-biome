using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventoryUI : MonoBehaviour
{
    [Header("References")]
    public PlayerInventory inventory;
    public Transform hotbarParent;
    public GameObject slotPrefab;
    public PlayerBuilder playerBuilder;

    [Header("Show / hide")]
    public float slideDistance = 140f;
    public float slideSpeed = 7.5f;
    public float bounce = 1.15f;

    Image[] slotIcons;
    Image[] slotHighlights;
    RectTransform barRt;
    Vector2 shownPos;
    Vector2 hiddenPos;
    float anim;
    bool wantVisible;

    void Start()
    {
        if (inventory != null)
        {
            inventory.OnSelectionChanged += UpdateSelection;
            inventory.OnHotbarChanged += RefreshHotbar;
        }

        if (playerBuilder == null)
            playerBuilder = FindFirstObjectByType<PlayerBuilder>();
        if (playerBuilder != null)
            playerBuilder.OnBuildModeChanged += OnBuildModeChanged;

        StyleHotbar();
        CreateHotbar();
        UpdateSelection(inventory != null ? inventory.selectedIndex : 0);

        CacheSlide();
        wantVisible = playerBuilder != null && playerBuilder.isBuildMode;
        anim = wantVisible ? 1f : 0f;
        ApplySlide(anim);
    }

    void OnDestroy()
    {
        if (inventory != null)
        {
            inventory.OnSelectionChanged -= UpdateSelection;
            inventory.OnHotbarChanged -= RefreshHotbar;
        }

        if (playerBuilder != null)
            playerBuilder.OnBuildModeChanged -= OnBuildModeChanged;
    }

    void Update()
    {
        if (barRt == null)
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
    }

    void CacheSlide()
    {
        barRt = hotbarParent as RectTransform;
        if (barRt == null)
            return;

        shownPos = barRt.anchoredPosition;
        hiddenPos = shownPos + new Vector2(0f, -Mathf.Abs(slideDistance));
    }

    void ApplySlide(float t)
    {
        if (barRt == null)
            return;

        float e = wantVisible ? EaseOutBack(t) : 1f - EaseOutBack(1f - t);
        barRt.anchoredPosition = Vector2.LerpUnclamped(hiddenPos, shownPos, e);
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

    void CreateHotbar()
    {
        if (hotbarParent == null || inventory == null) return;

        foreach (Transform child in hotbarParent)
            Destroy(child.gameObject);

        int size = inventory.hotbarSize;
        slotIcons = new Image[size];
        slotHighlights = new Image[size];

        for (int i = 0; i < size; i++)
        {
            GameObject slot = UiFactory.CreateHotbarSlot(hotbarParent, i);
            RectTransform rt = slot.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(80f, 80f);

            slotIcons[i] = slot.transform.Find("Icon")?.GetComponent<Image>();
            slotHighlights[i] = slot.transform.Find("Highlight")?.GetComponent<Image>();

            if (inventory.hotbar[i] != null && slotIcons[i] != null)
            {
                slotIcons[i].sprite = inventory.hotbar[i].icon;
                slotIcons[i].enabled = inventory.hotbar[i].icon != null;
            }
            else if (slotIcons[i] != null)
            {
                slotIcons[i].enabled = false;
            }
        }
    }

    void UpdateSelection(int selectedIndex)
    {
        if (slotHighlights == null) return;

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

    [ContextMenu("Refresh Hotbar")]
    public void RefreshHotbar()
    {
        CreateHotbar();
        UpdateSelection(inventory.selectedIndex);
    }
}
