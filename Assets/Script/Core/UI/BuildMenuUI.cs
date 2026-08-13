using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BuildMenuUI : MonoBehaviour
{
    [Header("References")]
    public PlayerInventory inventory;
    public PlayerBuilder playerBuilder;
    public GameObject menuPanel;
    public Transform buttonsParent;
    public GameObject buttonPrefab;

    [Header("Data")]
    public BuildingData[] availableBuildings;

    public bool IsOpen { get; private set; }

    void Awake()
    {
        if (playerBuilder == null)
            playerBuilder = FindFirstObjectByType<PlayerBuilder>();

        if (inventory == null && playerBuilder != null)
            inventory = playerBuilder.inventory;
    }

    void Start()
    {
        ApplyChrome();

        if (menuPanel != null)
            menuPanel.SetActive(false);

        IsOpen = false;
        gameObject.SetActive(false);
    }

    void ApplyChrome()
    {
        Image panelImage = menuPanel != null ? menuPanel.GetComponent<Image>() : null;
        UiTheme.StyleImage(panelImage, UiTheme.Panel);

        if (buttonsParent != null)
            UiTheme.EnsureGrid(buttonsParent, new Vector2(200f, 210f), new Vector2(16f, 16f), 4);
    }

    // Клавиша B обрабатывается в PlayerBuilder (единая точка входа Build Mode)

    void CreateButtons()
    {
        if (buttonsParent == null) return;

        foreach (Transform child in buttonsParent)
            Destroy(child.gameObject);

        BuildingData[] catalog = ResolveCatalog();
        if (catalog == null) return;

        for (int i = 0; i < catalog.Length; i++)
        {
            BuildingData data = catalog[i];
            if (data == null) continue;

            bool unlocked = ResearchSystem.Instance == null
                || ResearchSystem.Instance.IsBuildingUnlocked(data);

            BuildingData captured = data;
            UiFactory.CreateBuildingCard(buttonsParent, data, unlocked, () => SelectBuilding(captured));
        }
    }

    BuildingData[] ResolveCatalog()
    {
        if (availableBuildings != null && availableBuildings.Length > 0)
            return availableBuildings;
        if (inventory != null && inventory.allBuildings != null && inventory.allBuildings.Length > 0)
            return inventory.allBuildings;
        return null;
    }

    void SelectBuilding(BuildingData building)
    {
        if (inventory == null || building == null)
            return;

        inventory.SetHotbarSlot(0, building);
        inventory.selectedIndex = 0;

        // Закрываем меню, но оставляем Build Mode (можно ставить из hotbar)
        CloseMenu(restorePlayerControl: true);
        playerBuilder?.OnBuildMenuClosedAfterSelection();
    }

    public void OpenMenu()
    {
        IsOpen = true;
        CreateButtons();
        if (menuPanel != null)
            menuPanel.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SetPlayerControl(false);
    }

    public void ToggleMenu()
    {
        if (IsOpen)
            CloseMenu(restorePlayerControl: true);
        else
            OpenMenu();
    }

    /// <param name="restorePlayerControl">
    /// true — вернуть canMove/canLook и lock курсора.
    /// false — только скрыть панель (полный выход из Build Mode сделает PlayerBuilder).
    /// </param>
    public void CloseMenu(bool restorePlayerControl = true)
    {
        IsOpen = false;
        if (menuPanel != null)
            menuPanel.SetActive(false);

        if (restorePlayerControl)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            SetPlayerControl(true);
        }
    }

    void SetPlayerControl(bool enabled)
    {
        PlayerMovement movement = null;

        if (playerBuilder != null && playerBuilder.playerMovement != null)
            movement = playerBuilder.playerMovement;
        else
            movement = FindFirstObjectByType<PlayerMovement>();

        if (movement != null)
        {
            movement.canMove = enabled;
            movement.canLook = enabled;
        }
    }

    void OnEnable()
    {
        if (ResearchSystem.Instance != null)
            ResearchSystem.Instance.OnUnlocksChanged += CreateButtons;
    }

    void OnDisable()
    {
        if (ResearchSystem.Instance != null)
            ResearchSystem.Instance.OnUnlocksChanged -= CreateButtons;
    }
}
