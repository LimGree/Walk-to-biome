using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventoryUI : MonoBehaviour
{
    [Header("References")]
    public PlayerInventory inventory;
    public Transform hotbarParent;
    public GameObject slotPrefab;

    private Image[] slotIcons;
    private Image[] slotHighlights;

    void Start()
    {
        if (inventory != null)
        {
            inventory.OnSelectionChanged += UpdateSelection;
            inventory.OnHotbarChanged += RefreshHotbar;
        }

        StyleHotbar();
        CreateHotbar();
        UpdateSelection(inventory != null ? inventory.selectedIndex : 0);
    }

    void OnDestroy()
    {
        if (inventory != null)
        {
            inventory.OnSelectionChanged -= UpdateSelection;
            inventory.OnHotbarChanged -= RefreshHotbar;
        }
    }

    void StyleHotbar()
    {
        if (hotbarParent == null)
            return;

        Image bar = hotbarParent.GetComponent<Image>();
        if (bar != null)
            UiTheme.StyleImage(bar, new Color(0.08f, 0.09f, 0.11f, 0.88f));

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

    // Можно вызывать, когда меняется содержимое hotbar'а
    [ContextMenu("Refresh Hotbar")]
    public void RefreshHotbar()
    {
        CreateHotbar();
        UpdateSelection(inventory.selectedIndex);
    }
}