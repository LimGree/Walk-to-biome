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
        if (menuPanel != null)
            menuPanel.SetActive(false);

        IsOpen = false;
        CreateButtons();
    }

    // Клавиша B обрабатывается в PlayerBuilder (единая точка входа Build Mode)

    void CreateButtons()
    {
        if (buttonsParent == null || buttonPrefab == null) return;

        foreach (Transform child in buttonsParent)
            Destroy(child.gameObject);

        if (availableBuildings == null) return;

        for (int i = 0; i < availableBuildings.Length; i++)
        {
            BuildingData data = availableBuildings[i];
            if (data == null) continue;

            bool unlocked = ResearchSystem.Instance == null
                || ResearchSystem.Instance.IsBuildingUnlocked(data);

            GameObject btnObj = Instantiate(buttonPrefab, buttonsParent);

            var text = btnObj.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
                text.text = unlocked ? data.displayName : data.displayName + " [LOCKED]";

            var image = btnObj.transform.Find("Icon")?.GetComponent<Image>();
            if (image != null && data.icon != null)
                image.sprite = data.icon;

            var button = btnObj.GetComponent<Button>();
            if (button == null) continue;

            button.interactable = unlocked;

            int index = i;
            button.onClick.AddListener(() => SelectBuilding(index));
        }
    }

    void SelectBuilding(int index)
    {
        if (inventory == null || availableBuildings == null || index < 0 || index >= availableBuildings.Length)
            return;

        inventory.SetHotbarSlot(0, availableBuildings[index]);
        inventory.selectedIndex = 0;

        // Закрываем меню, но оставляем Build Mode (можно ставить из hotbar)
        CloseMenu(restorePlayerControl: true);
        playerBuilder?.OnBuildMenuClosedAfterSelection();
    }

    public void OpenMenu()
    {
        IsOpen = true;
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
