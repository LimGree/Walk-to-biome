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


    [Header("All recipes in game")]
    public RecipeData[] allRecipes;

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
        else if (building is Constructor constructor)
        {
            titleText.text = "Constructor";
            ShowConstructorUI(constructor);
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

        foreach (Transform child in extractorButtonsParent)
            Destroy(child.gameObject);

        if (resourceButtonPrefab == null) return;

        GameObject info = Instantiate(resourceButtonPrefab, extractorButtonsParent);
        var text = info.GetComponentInChildren<TextMeshProUGUI>();
        if (text != null)
        {
            text.text = extractor.resource != null
                ? $"Mining: {extractor.resource.displayName}"
                : "No resource node!";
        }

        var btn = info.GetComponent<Button>();
        if (btn != null) btn.interactable = false;
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
        // Список рецептов живёт в SmelterContent.
        if (smelterContent != null)
            smelterContent.SetActive(true);
        RefreshRecipeList(assembler);
    }

    // ================== CONSTRUCTOR ==================

    void ShowConstructorUI(Constructor constructor)
    {
        if (smelterContent != null)
            smelterContent.SetActive(true);
        RefreshRecipeList(constructor);
    }

    void RefreshRecipeList(BuildingBase building)
    {
        if (recipeButtonsParent == null) return;

        foreach (Transform child in recipeButtonsParent)
            Destroy(child.gameObject);

        EnsureRecipeCatalog();

        if (allRecipes == null || recipeButtonPrefab == null || building == null)
            return;

        BuildingData thisBuildingData = building.data;

        foreach (var recipe in allRecipes)
        {
            if (recipe == null) continue;

            // 1. Рецепт принадлежит этому зданию
            if (recipe.requiredBuilding != null && thisBuildingData != null
                && recipe.requiredBuilding != thisBuildingData)
                continue;

            // 2. Рецепт открыт
            if (ResearchSystem.Instance != null
                && !ResearchSystem.Instance.IsRecipeUnlocked(recipe))
                continue;

            GameObject btnObj = Instantiate(recipeButtonPrefab, recipeButtonsParent);

            var text = btnObj.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
                text.text = recipe.displayName;

            RecipeData captured = recipe;
            btnObj.GetComponent<Button>().onClick.AddListener(() =>
            {
                ApplyRecipe(building, captured);

                if (currentRecipeText != null)
                    currentRecipeText.text = captured.displayName;
            });
        }

        if (currentRecipeText != null)
            currentRecipeText.text = GetCurrentRecipeName(building);
    }

    static void ApplyRecipe(BuildingBase building, RecipeData recipe)
    {
        if (building is Smelter smelter)
            smelter.SetRecipe(recipe);
        else if (building is Assembler assembler)
            assembler.SetRecipe(recipe);
        else if (building is Constructor constructor)
            constructor.SetRecipe(recipe);
    }

    static string GetCurrentRecipeName(BuildingBase building)
    {
        RecipeData recipe = null;
        if (building is Smelter smelter)
            recipe = smelter.currentRecipe;
        else if (building is Assembler assembler)
            recipe = assembler.currentRecipe;
        else if (building is Constructor constructor)
            recipe = constructor.currentRecipe;

        return recipe != null ? recipe.displayName : "No recipe";
    }

    void EnsureRecipeCatalog()
    {
        if (allRecipes != null && allRecipes.Length > 0)
            return;

        allRecipes = Resources.FindObjectsOfTypeAll<RecipeData>();
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
            else if (ResearchSystem.Instance.CurrentResearch == node)
                status = " [ACTIVE]";
            else if (!ResearchSystem.Instance.CanStartResearch(node))
                status = " [LOCKED]";

            if (text != null)
                text.text = node.displayName + status;

            ResearchNodeData captured = node;
            btnObj.GetComponent<Button>().onClick.AddListener(() =>
            {
                if (ResearchSystem.Instance.SetCurrentResearch(captured))
                {
                    Debug.Log($"[MachineUI] Мировое исследование: {captured.displayName}");
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
        else if (currentBuilding is Constructor constructor && progressSlider != null)
        {
            if (constructor.currentRecipe != null && constructor.currentRecipe.craftTime > 0f)
                progressSlider.value = constructor.craftProgress / constructor.currentRecipe.craftTime;
            else
                progressSlider.value = 0f;
        }
        else if (currentBuilding is ResearchLab)
        {
            float progress = ResearchSystem.Instance != null
                ? ResearchSystem.Instance.GetCurrentProgress01()
                : 0f;
            if (researchProgressSlider != null)
                researchProgressSlider.value = progress;
            if (researchProgressText != null)
            {
                string name = ResearchSystem.Instance != null && ResearchSystem.Instance.CurrentResearch != null
                    ? ResearchSystem.Instance.CurrentResearch.displayName
                    : "None";
                researchProgressText.text = $"{name}: {(progress * 100f):0}%";
            }
        }
    }
}