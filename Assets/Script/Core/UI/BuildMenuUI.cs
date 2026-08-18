using UnityEngine;
using UnityEngine.UIElements;

public class BuildMenuUI : MonoBehaviour
{
    public static BuildMenuUI Instance { get; private set; }

    [Header("References")]
    public PlayerInventory inventory;
    public PlayerBuilder playerBuilder;
    public GameObject menuPanel;
    public Transform buttonsParent;
    public GameObject buttonPrefab;

    [Header("Data")]
    public BuildingData[] availableBuildings;

    public bool IsOpen { get; private set; }

    VisualElement overlay;
    ScrollView grid;

    void Awake()
    {
        Instance = this;
        if (playerBuilder == null)
            playerBuilder = FindFirstObjectByType<PlayerBuilder>();
        if (inventory == null && playerBuilder != null)
            inventory = playerBuilder.inventory;
    }

    void Start()
    {
        Build();
        IndustryUi.HideLegacy(this, menuPanel);
        IndustryUi.DisableHudCanvas(this);
        IndustryUi.Show(overlay, false);
        IsOpen = false;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        if (ResearchSystem.Instance != null)
            ResearchSystem.Instance.OnUnlocksChanged -= CreateButtons;
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

    void Build()
    {
        VisualElement root = IndustryUi.Mount(this, 100);
        overlay = IndustryUi.OverlayPanel("Строительство", null, () => CloseMenu(true));
        VisualElement panel = IndustryUi.PanelOf(overlay);
        grid = IndustryUi.Scroll("BuildGrid");
        var wrap = IndustryUi.El("Grid", "grid");
        wrap.name = "Cards";
        grid.Add(wrap);
        panel.Add(grid);
        root.Add(overlay);
    }

    void CreateButtons()
    {
        if (grid == null)
            return;
        VisualElement wrap = grid.Q("Cards") ?? grid.contentContainer;
        wrap.Clear();
        if (!wrap.ClassListContains("grid"))
            wrap.AddToClassList("grid");

        BuildingData[] catalog = ResolveCatalog();
        if (catalog == null)
            return;

        for (int i = 0; i < catalog.Length; i++)
        {
            BuildingData data = catalog[i];
            if (data == null)
                continue;
            bool unlocked = ResearchSystem.Instance == null
                || ResearchSystem.Instance.IsBuildingUnlocked(data);
            BuildingData captured = data;
            wrap.Add(IndustryUi.BuildingCard(data, unlocked, null, false, () => SelectBuilding(captured)));
        }
    }

    BuildingData[] ResolveCatalog()
    {
        if (availableBuildings != null && availableBuildings.Length > 0)
            return availableBuildings;
        BuildingData[] fromDb = GameDatabase.AllBuildings();
        if (fromDb != null && fromDb.Length > 0)
            return fromDb;
        if (inventory != null && inventory.allBuildings != null && inventory.allBuildings.Length > 0)
            return inventory.allBuildings;
        return null;
    }

    void SelectBuilding(BuildingData building)
    {
        if (inventory == null || building == null)
            return;
        inventory.Equip(building);
        CloseMenu(restorePlayerControl: true);
        playerBuilder?.OnBuildMenuClosedAfterSelection();
    }

    public void OpenMenu()
    {
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);
        IsOpen = true;
        if (overlay == null)
            Build();
        CreateButtons();
        IndustryUi.Show(overlay, true);
        if (GameManager.Instance != null)
            GameManager.Instance.RestoreGameplayFocus();
        else
        {
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
            SetPlayerControl(false);
        }
    }

    public void ToggleMenu()
    {
        if (IsOpen)
            CloseMenu(restorePlayerControl: true);
        else
            OpenMenu();
    }

    public void CloseMenu(bool restorePlayerControl = true)
    {
        IsOpen = false;
        IndustryUi.Show(overlay, false);
        if (!restorePlayerControl)
            return;
        if (GameManager.Instance != null)
            GameManager.Instance.RestoreGameplayFocus();
        else
        {
            UnityEngine.Cursor.lockState = CursorLockMode.Locked;
            UnityEngine.Cursor.visible = false;
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
        if (movement == null)
            return;
        movement.canMove = enabled;
        movement.canLook = enabled;
    }
}
