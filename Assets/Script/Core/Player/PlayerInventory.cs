using UnityEngine;
using UnityEngine.InputSystem;
using System;

public class PlayerInventory : MonoBehaviour
{
    [Header("Hotbar")]
    public int hotbarSize = 9;
    public BuildingData[] hotbar;
    public int selectedIndex = 0;

    [Header("Каталог (все здания игры)")]
    public BuildingData[] allBuildings;   // вместо buildersPrefabs

    public event Action<int> OnSelectionChanged;
    public event Action OnHotbarChanged;

    private InputSystem_Actions inputActions;

    void Awake()
    {
        inputActions = new InputSystem_Actions();

        if (hotbar == null || hotbar.Length != hotbarSize)
            hotbar = new BuildingData[hotbarSize];
    }

    void Start()
    {
        if (ResearchSystem.Instance != null)
            ResearchSystem.Instance.OnUnlocksChanged += RefreshHotbarFromUnlocks;

        RefreshHotbarFromUnlocks();
    }

    void OnDestroy()
    {
        if (ResearchSystem.Instance != null)
            ResearchSystem.Instance.OnUnlocksChanged -= RefreshHotbarFromUnlocks;
    }

    void OnEnable()
    {
        inputActions?.Enable();
        if (inputActions != null)
            inputActions.Player.HotbarScroll.performed += OnHotbarScroll;
    }

    void OnDisable()
    {
        if (inputActions != null)
            inputActions.Player.HotbarScroll.performed -= OnHotbarScroll;
        inputActions?.Disable();
    }

    void OnHotbarScroll(InputAction.CallbackContext ctx)
    {
        if (MachineUI.Instance != null && MachineUI.Instance.IsOpen)
            return;
        if (GameManager.Instance != null && GameManager.Instance.IsPaused)
            return;

        PlayerBuilder builder = GameManager.Instance != null
            ? GameManager.Instance.playerBuilder
            : FindFirstObjectByType<PlayerBuilder>();
        if (builder == null || !builder.isBuildMode)
            return;

        Vector2 scroll = ctx.ReadValue<Vector2>();

        if (scroll.y > 0.1f)
        {
            selectedIndex--;
            if (selectedIndex < 0) selectedIndex = hotbarSize - 1;
            OnSelectionChanged?.Invoke(selectedIndex);
        }
        else if (scroll.y < -0.1f)
        {
            selectedIndex++;
            if (selectedIndex >= hotbarSize) selectedIndex = 0;
            OnSelectionChanged?.Invoke(selectedIndex);
        }
    }

    public void RefreshHotbarFromUnlocks()
    {
        Array.Clear(hotbar, 0, hotbar.Length);

        if (allBuildings == null || ResearchSystem.Instance == null)
            return;

        int slot = 0;
        foreach (var b in allBuildings)
        {
            if (b == null) continue;
            if (!ResearchSystem.Instance.IsBuildingUnlocked(b)) continue;
            if (slot >= hotbarSize) break;

            hotbar[slot++] = b;
        }

        OnHotbarChanged?.Invoke();
        OnSelectionChanged?.Invoke(selectedIndex);

        var ui = FindFirstObjectByType<InventoryUI>();
        if (ui != null) ui.RefreshHotbar();
    }

    public BuildingData GetSelectedBuilding()
    {
        if (selectedIndex < 0 || selectedIndex >= hotbar.Length)
            return null;

        var b = hotbar[selectedIndex];
        if (b != null && ResearchSystem.Instance != null
            && !ResearchSystem.Instance.IsBuildingUnlocked(b))
            return null;

        return b;
    }

    public void SetHotbarSlot(int index, BuildingData building)
    {
        if (index < 0 || index >= hotbarSize) return;

        if (building != null && ResearchSystem.Instance != null
            && !ResearchSystem.Instance.IsBuildingUnlocked(building))
        {
            Debug.LogWarning($"[Inventory] {building.displayName} ещё не открыто");
            return;
        }

        hotbar[index] = building;
        OnHotbarChanged?.Invoke();
    }
}