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
            inventory.OnSelectionChanged += UpdateSelection;

        CreateHotbar();
        UpdateSelection(inventory != null ? inventory.selectedIndex : 0);
    }

    void OnDestroy()
    {
        if (inventory != null)
            inventory.OnSelectionChanged -= UpdateSelection;
    }

    void CreateHotbar()
    {
        if (hotbarParent == null || slotPrefab == null || inventory == null) return;

        foreach (Transform child in hotbarParent)
            Destroy(child.gameObject);

        int size = inventory.hotbarSize;
        slotIcons = new Image[size];
        slotHighlights = new Image[size];

        for (int i = 0; i < size; i++)
        {
            GameObject slot = Instantiate(slotPrefab, hotbarParent);

            slotIcons[i] = slot.transform.Find("Icon")?.GetComponent<Image>();
            slotHighlights[i] = slot.transform.Find("Highlight")?.GetComponent<Image>();

            // Показываем иконку здания, если есть
            if (inventory.hotbar[i] != null && slotIcons[i] != null)
            {
                slotIcons[i].sprite = inventory.hotbar[i].icon;
                slotIcons[i].enabled = true;
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
            if (slotHighlights[i] != null)
                slotHighlights[i].enabled = (i == selectedIndex);
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