using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BuildMenuUI : MonoBehaviour
{
    [Header("References")]
    public PlayerInventory inventory;
    public GameObject menuPanel;
    public Transform buttonsParent;
    public GameObject buttonPrefab;          // кнопка-префаб

    [Header("Data")]
    public BuildingData[] availableBuildings;

    private bool isOpen = false;

    void Start()
    {
        if (menuPanel != null)
            menuPanel.SetActive(false);

        CreateButtons();
    }

    void Update()
    {
        // Открытие/закрытие по клавише B (пример)
        if (Input.GetKeyDown(KeyCode.B))
        {
            ToggleMenu();
        }
    }

    void CreateButtons()
    {
        if (buttonsParent == null || buttonPrefab == null) return;

        foreach (Transform child in buttonsParent)
            Destroy(child.gameObject);

        for (int i = 0; i < availableBuildings.Length; i++)
        {
            BuildingData data = availableBuildings[i];
            GameObject btnObj = Instantiate(buttonPrefab, buttonsParent);

            // Текст
            var text = btnObj.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null) text.text = data.displayName;

            // Картинка
            var image = btnObj.transform.Find("Icon")?.GetComponent<Image>();
            if (image != null && data.icon != null)
                image.sprite = data.icon;

            // Нажатие
            int index = i; // важно для замыкания
            btnObj.GetComponent<Button>().onClick.AddListener(() => SelectBuilding(index));
        }
    }

    void SelectBuilding(int index)
    {
        if (inventory == null || index >= availableBuildings.Length) return;

        // Кладём выбранное здание в первый слот hotbar'а (можно улучшить)
        inventory.SetHotbarSlot(0, availableBuildings[index]);
        inventory.selectedIndex = 0;

        CloseMenu();
    }

    public void ToggleMenu()
    {
        isOpen = !isOpen;
        if (menuPanel != null)
            menuPanel.SetActive(isOpen);

        Cursor.lockState = isOpen ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = isOpen;
    }

    public void CloseMenu()
    {
        isOpen = false;
        if (menuPanel != null)
            menuPanel.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}