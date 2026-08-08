using UnityEngine;
using UnityEngine.InputSystem;
using System;

public class PlayerInventory : MonoBehaviour
{
    [Header("Hotbar")]
    public int hotbarSize = 9;
    public BuildingData[] hotbar;
    public int selectedIndex = 0;

    [Header("For test potom udalit")]
    public BuildingData[] buildersPrefabs;


    public event Action<int> OnSelectionChanged;

    private InputSystem_Actions inputActions;

    void Awake()
    {
        inputActions = new InputSystem_Actions();

        if (hotbar == null || hotbar.Length != hotbarSize)
            hotbar = new BuildingData[hotbarSize];


        Testfillhotbar();
        var tt = FindFirstObjectByType<InventoryUI>();
        tt.RefreshHotbar();
    }

    void OnEnable()
    {
        inputActions.Enable();
    }

    void OnDisable()
    {
        inputActions.Disable();
    }

    void Update()
    {
        // Надёжное чтение колёсика мыши
        if (Mouse.current == null) return;

        Vector2 scroll = Mouse.current.scroll.ReadValue();

        if (scroll.y > 0.1f)          // вверх
        {
            selectedIndex--;
            if (selectedIndex < 0)
                selectedIndex = hotbarSize - 1;

            OnSelectionChanged?.Invoke(selectedIndex);
        }
        else if (scroll.y < -0.1f)   // вниз
        {
            selectedIndex++;
            if (selectedIndex >= hotbarSize)
                selectedIndex = 0;

            OnSelectionChanged?.Invoke(selectedIndex);
        }
    }

    public BuildingData GetSelectedBuilding()
    {
        if (selectedIndex < 0 || selectedIndex >= hotbar.Length)
            return null;

        return hotbar[selectedIndex];
    }

    public void SetHotbarSlot(int index, BuildingData building)
    {
        if (index < 0 || index >= hotbarSize) return;
        hotbar[index] = building;
    }

    public void Testfillhotbar()
    {
        for (int i = 0; i < 4; i++)
        {
            hotbar[i] = buildersPrefabs[Math.Clamp(i,0,3)];
        }
    }
}