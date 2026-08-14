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

    GameObject storageContent;
    Transform storageSlotsParent;
    TextMeshProUGUI storageSummaryText;

    public bool IsOpen { get; private set; }

    private BuildingBase currentBuilding;


    [Header("All recipes in game")]
    public RecipeData[] allRecipes;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        ApplyChrome();

        if (machinePanel != null)
            machinePanel.SetActive(false);

        if (closeButton != null)
            closeButton.onClick.AddListener(Close);
    }

    void ApplyChrome()
    {
        Image panelImage = machinePanel != null ? machinePanel.GetComponent<Image>() : null;
        if (panelImage == null && machinePanel != null)
            panelImage = machinePanel.AddComponent<Image>();
        UiTheme.StyleImage(panelImage, UiTheme.Panel);

        if (titleText != null)
        {
            UiTheme.StyleText(titleText, 30f, UiTheme.Text, FontStyles.Bold);
            titleText.alignment = TextAlignmentOptions.MidlineLeft;
        }

        if (currentRecipeText != null)
        {
            UiTheme.StyleText(currentRecipeText, 20f, UiTheme.Accent);
            currentRecipeText.alignment = TextAlignmentOptions.MidlineLeft;
        }

        if (researchProgressText != null)
        {
            UiTheme.StyleText(researchProgressText, 20f, UiTheme.Text);
            researchProgressText.alignment = TextAlignmentOptions.MidlineLeft;
        }

        UiTheme.StyleSlider(progressSlider);
        UiTheme.StyleSlider(researchProgressSlider);

        PrepareScrollList(recipeButtonsParent, new Vector2(880f, 128f));
        PrepareScrollList(researchButtonsParent, new Vector2(880f, 136f));
        PrepareScrollList(extractorButtonsParent, new Vector2(880f, 128f));

        if (closeButton != null)
        {
            Image closeImage = closeButton.GetComponent<Image>();
            UiTheme.StyleImage(closeImage, new Color(0.85f, 0.28f, 0.30f, 0.9f));
            var closeText = closeButton.GetComponentInChildren<TextMeshProUGUI>();
            if (closeText != null)
            {
                closeText.text = "✕";
                UiTheme.StyleText(closeText, 22f, Color.white, FontStyles.Bold);
                closeText.alignment = TextAlignmentOptions.Center;
            }
        }
    }

    static void PrepareScrollList(Transform list, Vector2 cell)
    {
        if (list == null)
            return;

        UiTheme.EnsureGrid(list, cell, new Vector2(12f, 12f), 1);
        UiTheme.EnsureVerticalScroll(list as RectTransform);
    }

    void Update()
    {
        // Закрытие по Escape
        if (IsOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Close();
        }

        // Обновляем прогресс, если панель открыта
        if (IsOpen && currentBuilding != null)
        {
            UpdateProgress();
        }
    }

    // ================== ОТКРЫТИЕ ==================

    public void Open(BuildingBase building)
    {
        if (building == null) return;

        currentBuilding = building;
        IsOpen = true;

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
        else if (building is StorageContainer storage)
        {
            titleText.text = building.data != null ? building.data.displayName : "Склад";
            ShowStorageUI(storage);
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
        IsOpen = false;
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
        if (storageContent) storageContent.SetActive(false);
    }

    // ================== EXTRACTOR ==================

    void ShowExtractorUI(Extractor extractor)
    {
        extractorContent.SetActive(true);

        foreach (Transform child in extractorButtonsParent)
            Destroy(child.gameObject);

        var fake = ScriptableObject.CreateInstance<RecipeData>();
        fake.displayName = extractor.resource != null
            ? "Mining  " + extractor.resource.displayName
            : "No resource node";
        if (extractor.resource != null)
            fake.outputs.Add(new ItemStack(extractor.resource, 1));

        UiFactory.CreateRecipeCard(extractorButtonsParent, fake, true, null);
        Object.Destroy(fake);
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

        if (allRecipes == null || building == null)
            return;

        BuildingData thisBuildingData = building.data;
        RecipeData selected = GetCurrentRecipe(building);

        foreach (var recipe in allRecipes)
        {
            if (recipe == null) continue;

            if (recipe.requiredBuilding != null && thisBuildingData != null
                && recipe.requiredBuilding != thisBuildingData)
                continue;

            if (ResearchSystem.Instance != null
                && !ResearchSystem.Instance.IsRecipeUnlocked(recipe))
                continue;

            RecipeData captured = recipe;
            UiFactory.CreateRecipeCard(
                recipeButtonsParent,
                recipe,
                selected == recipe,
                () =>
                {
                    ApplyRecipe(building, captured);
                    RefreshRecipeList(building);
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

    static RecipeData GetCurrentRecipe(BuildingBase building)
    {
        if (building is Smelter smelter)
            return smelter.currentRecipe;
        if (building is Assembler assembler)
            return assembler.currentRecipe;
        if (building is Constructor constructor)
            return constructor.currentRecipe;
        return null;
    }

    static string GetCurrentRecipeName(BuildingBase building)
    {
        RecipeData recipe = GetCurrentRecipe(building);
        return recipe != null ? "Selected: " + recipe.displayName : "Select a recipe";
    }

    void EnsureRecipeCatalog()
    {
        if (allRecipes != null && allRecipes.Length > 0)
            return;

        allRecipes = Resources.FindObjectsOfTypeAll<RecipeData>();
    }

    // ================== STORAGE ==================

    void ShowStorageUI(StorageContainer storage)
    {
        EnsureStorageUi();
        if (storageContent != null)
            storageContent.SetActive(true);
        RefreshStorageSlots(storage);
    }

    void EnsureStorageUi()
    {
        if (storageContent != null)
            return;

        Transform host = null;
        if (extractorContent != null)
            host = extractorContent.transform.parent;
        if (host == null && machinePanel != null)
            host = machinePanel.transform;
        if (host == null)
            return;

        storageContent = new GameObject("StorageContent", typeof(RectTransform));
        storageContent.transform.SetParent(host, false);
        RectTransform root = storageContent.GetComponent<RectTransform>();
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;

        storageSummaryText = UiTheme.AddText(root, "Summary", "", 20f, UiTheme.Text);
        RectTransform summaryRt = storageSummaryText.rectTransform;
        summaryRt.anchorMin = new Vector2(0f, 1f);
        summaryRt.anchorMax = new Vector2(1f, 1f);
        summaryRt.pivot = new Vector2(0.5f, 1f);
        summaryRt.anchoredPosition = new Vector2(0f, -8f);
        summaryRt.sizeDelta = new Vector2(-32f, 32f);

        GameObject gridGo = new GameObject("Slots", typeof(RectTransform));
        gridGo.transform.SetParent(root, false);
        RectTransform gridRt = gridGo.GetComponent<RectTransform>();
        gridRt.anchorMin = new Vector2(0f, 0f);
        gridRt.anchorMax = new Vector2(1f, 1f);
        gridRt.offsetMin = new Vector2(16f, 16f);
        gridRt.offsetMax = new Vector2(-16f, -48f);
        storageSlotsParent = gridRt;

        UiTheme.EnsureGrid(storageSlotsParent, new Vector2(220f, 220f), new Vector2(16f, 16f), 2);
        UiTheme.EnsureVerticalScroll(gridRt);
    }

    void RefreshStorageSlots(StorageContainer storage)
    {
        EnsureStorageUi();
        if (storage == null || storageSlotsParent == null)
            return;

        int count = storage.SlotCount;
        while (storageSlotsParent.childCount < count)
            UiFactory.CreateStorageSlot(storageSlotsParent);

        for (int i = 0; i < storageSlotsParent.childCount; i++)
        {
            GameObject slot = storageSlotsParent.GetChild(i).gameObject;
            slot.SetActive(i < count);
            if (i < count)
                UiFactory.BindStorageSlot(slot, storage.GetSlot(i));
        }

        if (storageSummaryText != null)
        {
            string typeName = storage.StoredType != null ? storage.StoredType.displayName : "пусто";
            storageSummaryText.text = $"{typeName}  ·  стеки {storage.UsedSlotCount} / {count}";
        }
    }

    // ================== RESEARCH LAB ==================

    void ShowResearchLabUI(ResearchLab lab)
    {
        researchLabContent.SetActive(true);

        foreach (Transform child in researchButtonsParent)
            Destroy(child.gameObject);

        if (ResearchSystem.Instance == null)
            return;

        foreach (var node in ResearchSystem.Instance.GetAllNodes())
        {
            if (node == null) continue;

            string status = "READY";
            bool canStart = ResearchSystem.Instance.CanStartResearch(node);
            if (ResearchSystem.Instance.IsResearchUnlocked(node))
            {
                status = "DONE";
                canStart = false;
            }
            else if (ResearchSystem.Instance.CurrentResearch == node)
            {
                status = "ACTIVE";
                canStart = false;
            }
            else if (!canStart)
            {
                status = "LOCKED";
            }

            ResearchNodeData captured = node;
            UiFactory.CreateResearchCard(
                researchButtonsParent,
                node,
                status,
                canStart,
                () =>
                {
                    if (ResearchSystem.Instance.SetCurrentResearch(captured))
                        Close();
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
        else if (currentBuilding is StorageContainer storage)
        {
            RefreshStorageSlots(storage);
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