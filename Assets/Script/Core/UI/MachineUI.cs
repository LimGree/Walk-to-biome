using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class MachineUI : MonoBehaviour
{
    public static MachineUI Instance { get; private set; }

    [Header("Main Panel")]
    public GameObject machinePanel;
    public TextMeshProUGUI titleText;
    public Button closeButton;

    [Header("Content Roots")]
    public GameObject extractorContent;
    public GameObject smelterContent;
    public GameObject assemblerContent;
    public GameObject researchLabContent;

    [Header("Extractor UI")]
    public Transform extractorButtonsParent;
    public GameObject resourceButtonPrefab;
    public ItemData[] availableResources;          // Iron Ore, Copper Ore, Coal...

    [Header("Smelter / Assembler UI")]
    public Transform recipeButtonsParent;
    public GameObject recipeButtonPrefab;
    public TextMeshProUGUI currentRecipeText;
    public Slider progressSlider;

    [Header("Research Lab UI")]
    public Transform researchButtonsParent;
    public GameObject researchButtonPrefab;
    public TextMeshProUGUI researchProgressText;
    public Slider researchProgressSlider;

    // Runtime
    private BuildingBase currentBuilding;
    private bool isOpen = false;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        if (machinePanel != null)
            machinePanel.SetActive(false);

        if (closeButton != null)
            closeButton.onClick.AddListener(Close);
    }

    void Update()
    {
        // Закрытие по Escape
        if (isOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Close();
        }

        // Обновляем прогресс, если панель открыта
        if (isOpen && currentBuilding != null)
        {
            UpdateProgress();
        }
    }

    // ================== ОТКРЫТИЕ ==================

    public void Open(BuildingBase building)
    {
        if (building == null) return;

        currentBuilding = building;
        isOpen = true;

        // Прячем все контенты
        HideAllContents();

        // Показываем нужный
        if (building is Extractor extractor)
        {
            titleText.text = "Extractor";
            ShowExtractorUI(extractor);
        }
        else if (building is Smelter smelter)
        {
            titleText.text = "Smelter";
            ShowSmelterUI(smelter);
        }
        else if (building is Assembler assembler)
        {
            titleText.text = "Assembler";
            ShowAssemblerUI(assembler);
        }
        else if (building is ResearchLab lab)
        {
            titleText.text = "Research Lab";
            ShowResearchLabUI(lab);
        }
        else
        {
            titleText.text = building.data != null ? building.data.displayName : "Building";
        }

        machinePanel.SetActive(true);

        // Курсор
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // === Отключаем движение и камеру ===
        SetPlayerControl(false);
    }

    public void Close()
    {
        isOpen = false;
        currentBuilding = null;

        if (machinePanel != null)
            machinePanel.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        SetPlayerControl(true);
    }
    void SetPlayerControl(bool enabled)
    {
        var movement = FindFirstObjectByType<PlayerMovement>();
        if (movement != null)
        {
            movement.canMove = enabled;
            movement.canLook = enabled;
        }

        // Можно также отключать строительство, если нужно
        // var builder = FindFirstObjectByType<PlayerBuilder>();
        // if (builder != null) builder.SetBuildMode(enabled);
    }

    void HideAllContents()
    {
        if (extractorContent) extractorContent.SetActive(false);
        if (smelterContent) smelterContent.SetActive(false);
        if (assemblerContent) assemblerContent.SetActive(false);
        if (researchLabContent) researchLabContent.SetActive(false);
    }

    // ================== EXTRACTOR ==================

    void ShowExtractorUI(Extractor extractor)
    {
        extractorContent.SetActive(true);

        // Очищаем старые кнопки
        foreach (Transform child in extractorButtonsParent)
            Destroy(child.gameObject);

        if (availableResources == null || resourceButtonPrefab == null) return;

        foreach (var resource in availableResources)
        {
            if (resource == null) continue;

            GameObject btnObj = Instantiate(resourceButtonPrefab, extractorButtonsParent);
            var text = btnObj.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null) text.text = resource.displayName;

            ItemData captured = resource;
            btnObj.GetComponent<Button>().onClick.AddListener(() =>
            {
                extractor.resource = captured;
                Debug.Log($"[MachineUI] Extractor теперь добывает: {captured.displayName}");
                Close();
            });
        }
    }

    // ================== SMELTER ==================

    void ShowSmelterUI(Smelter smelter)
    {
        smelterContent.SetActive(true);
        RefreshRecipeList(smelter);
    }

    // ================== ASSEMBLER ==================

    void ShowAssemblerUI(Assembler assembler)
    {
        assemblerContent.SetActive(true);
        RefreshRecipeList(assembler);
    }

    void RefreshRecipeList(BuildingBase building)
    {
        // Пока просто показываем все рецепты.
        // Позже будем фильтровать по ResearchSystem и requiredBuilding.

        foreach (Transform child in recipeButtonsParent)
            Destroy(child.gameObject);

        // TODO: Здесь нужно будет брать список доступных рецептов
        // Пока для теста можно оставить пустым или добавить поле public RecipeData[] testRecipes;
    }

    // ================== RESEARCH LAB ==================

    void ShowResearchLabUI(ResearchLab lab)
    {
        researchLabContent.SetActive(true);

        foreach (Transform child in researchButtonsParent)
            Destroy(child.gameObject);

        if (ResearchSystem.Instance == null || researchButtonPrefab == null) return;

        foreach (var node in ResearchSystem.Instance.GetAllNodes())
        {
            if (node == null) continue;

            GameObject btnObj = Instantiate(researchButtonPrefab, researchButtonsParent);
            var text = btnObj.GetComponentInChildren<TextMeshProUGUI>();

            string status = "";
            if (ResearchSystem.Instance.IsResearchUnlocked(node))
                status = " [DONE]";
            else if (!ResearchSystem.Instance.CanStartResearch(node))
                status = " [LOCKED]";

            if (text != null)
                text.text = node.displayName + status;

            ResearchNodeData captured = node;
            btnObj.GetComponent<Button>().onClick.AddListener(() =>
            {
                if (ResearchSystem.Instance.CanStartResearch(captured))
                {
                    lab.SetResearch(captured);
                    Debug.Log($"[MachineUI] Начато исследование: {captured.displayName}");
                    Close();
                }
            });
        }
    }

    // ================== PROGRESS ==================

    void UpdateProgress()
    {
        if (currentBuilding is Smelter smelter && progressSlider != null)
        {
            if (smelter.currentRecipe != null)
                progressSlider.value = smelter.craftProgress / smelter.currentRecipe.craftTime;
            else
                progressSlider.value = 0f;
        }
        else if (currentBuilding is Assembler assembler && progressSlider != null)
        {
            if (assembler.currentRecipe != null)
                progressSlider.value = assembler.craftProgress / assembler.currentRecipe.craftTime;
            else
                progressSlider.value = 0f;
        }
        else if (currentBuilding is ResearchLab lab)
        {
            float progress = lab.GetProgress();
            if (researchProgressSlider != null)
                researchProgressSlider.value = progress;
            if (researchProgressText != null)
                researchProgressText.text = $"Progress: {(progress * 100f):0}%";
        }
    }
}