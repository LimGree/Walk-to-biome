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

    GameObject armFilterContent;
    Transform armFilterParent;
    TextMeshProUGUI armFilterSummary;
    Image headerIcon;

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

        PrepareScrollList(recipeButtonsParent, new Vector2(880f, 156f));
        PrepareScrollList(researchButtonsParent, new Vector2(880f, 176f));
        PrepareScrollList(extractorButtonsParent, new Vector2(880f, 156f));

        EnsureHeaderIcon();

        if (closeButton != null)
        {
            Image closeImage = closeButton.GetComponent<Image>();
            UiTheme.StyleImage(closeImage, UiTheme.Danger);
            var closeText = closeButton.GetComponentInChildren<TextMeshProUGUI>();
            if (closeText != null)
            {
                closeText.text = "✕";
                UiTheme.StyleText(closeText, 22f, Color.white, FontStyles.Bold);
                closeText.alignment = TextAlignmentOptions.Center;
            }
        }
    }

    void EnsureHeaderIcon()
    {
        if (headerIcon != null || titleText == null)
            return;

        Transform parent = titleText.transform.parent;
        if (parent == null)
            return;

        GameObject go = new GameObject("HeaderIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        headerIcon = go.GetComponent<Image>();
        headerIcon.preserveAspect = true;
        headerIcon.raycastTarget = false;
        headerIcon.color = Color.white;

        RectTransform iconRt = headerIcon.rectTransform;
        RectTransform titleRt = titleText.rectTransform;
        iconRt.anchorMin = titleRt.anchorMin;
        iconRt.anchorMax = new Vector2(titleRt.anchorMin.x, titleRt.anchorMax.y);
        iconRt.pivot = new Vector2(0f, 0.5f);
        iconRt.anchoredPosition = titleRt.anchoredPosition;
        iconRt.sizeDelta = new Vector2(48f, 48f);

        titleRt.offsetMin = new Vector2(titleRt.offsetMin.x + 58f, titleRt.offsetMin.y);
    }

    void SetHeader(string title, BuildingBase building)
    {
        if (titleText != null)
            titleText.text = title;

        EnsureHeaderIcon();
        if (headerIcon == null)
            return;

        Sprite icon = building != null && building.data != null ? building.data.icon : null;
        headerIcon.sprite = icon;
        headerIcon.enabled = icon != null;
        headerIcon.color = Color.white;
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
        if (GameManager.Instance != null && GameManager.Instance.IsPaused)
            return;

        currentBuilding = building;
        IsOpen = true;

        // Прячем все контенты
        HideAllContents();

        if (building is OilExtractor oil)
        {
            SetHeader("Нефтекачалка  ·  " + oil.RichnessName, building);
            ShowOilExtractorUI(oil);
        }
        else if (building is WaterExtractor water)
        {
            SetHeader(water.data != null ? water.data.displayName : "Водокачка", building);
            ShowWaterExtractorUI(water);
        }
        else if (building is Extractor extractor)
        {
            SetHeader("Extractor  ·  ур. " + extractor.level, building);
            ShowExtractorUI(extractor);
        }
        else if (building is CrafterBuilding crafter)
        {
            string title = building.data != null ? building.data.displayName : crafter.GetType().Name;
            Assembler assembler = crafter as Assembler;
            if (assembler != null)
                title += "  ·  ур. " + assembler.level;
            SetHeader(title, building);
            ShowCrafterUI(crafter);
        }
        else if (building is ResearchLab lab)
        {
            SetHeader("Research Lab", building);
            ShowResearchLabUI(lab);
        }
        else if (building is StorageContainer storage)
        {
            SetHeader(building.data != null ? building.data.displayName : "Склад", building);
            ShowStorageUI(storage);
        }
        else if (building is RoboticArm arm)
        {
            SetHeader("Роборука", building);
            ShowRoboticArmUI(arm);
        }
        else
        {
            SetHeader(building.data != null ? building.data.displayName : "Building", building);
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

        if (GameManager.Instance != null)
            GameManager.Instance.RestoreGameplayFocus();
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            SetPlayerControl(true);
        }
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
        if (armFilterContent) armFilterContent.SetActive(false);
    }

    void ShowOilExtractorUI(OilExtractor oil)
    {
        if (extractorContent != null)
            extractorContent.SetActive(true);
        if (extractorButtonsParent == null)
            return;

        foreach (Transform child in extractorButtonsParent)
            Destroy(child.gameObject);

        var fake = ScriptableObject.CreateInstance<RecipeData>();
        fake.displayName = oil.resource != null
            ? "Добыча  " + oil.resource.displayName
            : "Нет нефти";
        if (oil.resource != null)
            fake.outputs.Add(new ItemStack(oil.resource, oil.CurrentItemsPerCycle));

        UiFactory.CreateRecipeCard(extractorButtonsParent, fake, true, null);
        Object.Destroy(fake);

        string stats = oil.RichnessName + "  ·  " + oil.CurrentInterval.ToString("0.##") + " с / цикл";
        UiFactory.CreateActionButton(
            extractorButtonsParent,
            "Скважина",
            stats + "  ·  бесконечный запас",
            false,
            null);
    }

    void ShowWaterExtractorUI(WaterExtractor pump)
    {
        if (extractorContent != null)
            extractorContent.SetActive(true);
        if (extractorButtonsParent == null)
            return;

        foreach (Transform child in extractorButtonsParent)
            Destroy(child.gameObject);

        var fake = ScriptableObject.CreateInstance<RecipeData>();
        fake.displayName = pump.resource != null
            ? "Добыча  " + pump.resource.displayName
            : "Нет воды";
        if (pump.resource != null)
            fake.outputs.Add(new ItemStack(pump.resource, pump.CurrentItemsPerCycle));

        UiFactory.CreateRecipeCard(extractorButtonsParent, fake, true, null);
        Object.Destroy(fake);

        string stats = pump.CurrentInterval.ToString("0.##") + " с / цикл";
        UiFactory.CreateActionButton(
            extractorButtonsParent,
            "Источник",
            stats + "  ·  бесконечный запас",
            false,
            null);
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
            fake.outputs.Add(new ItemStack(extractor.resource, extractor.CurrentItemsPerCycle));

        UiFactory.CreateRecipeCard(extractorButtonsParent, fake, true, null);
        Object.Destroy(fake);

        string stats = $"{extractor.CurrentInterval:0.##} с / цикл  ·  {extractor.CurrentItemsPerCycle} шт";
        if (extractor.CanUpgrade)
        {
            string next = $"{extractor.upgradedExtractInterval:0.##} с / цикл  ·  {Mathf.Max(1, extractor.upgradedItemsPerCycle)} шт";
            UiFactory.CreateActionButton(
                extractorButtonsParent,
                "Прокачать  →  ур. 2",
                "Сейчас: " + stats + "   |   После: " + next,
                true,
                () =>
                {
                    if (extractor.TryUpgrade())
                    {
                        SetHeader("Extractor  ·  ур. " + extractor.level, extractor);
                        ShowExtractorUI(extractor);
                    }
                });
        }
        else
        {
            UiFactory.CreateActionButton(
                extractorButtonsParent,
                "Улучшено до ур. 2",
                stats,
                false,
                null);
        }
    }

    void ShowCrafterUI(CrafterBuilding crafter)
    {
        if (smelterContent != null)
            smelterContent.SetActive(true);
        RefreshRecipeList(crafter);
    }

    void RefreshRecipeList(CrafterBuilding crafter)
    {
        if (recipeButtonsParent == null) return;

        foreach (Transform child in recipeButtonsParent)
            Destroy(child.gameObject);

        RecipeData[] catalog = GameDatabase.AllRecipes();
        if (crafter == null)
            return;

        BuildingData thisBuildingData = crafter.data;
        RecipeData selected = crafter.currentRecipe;

        for (int i = 0; i < catalog.Length; i++)
        {
            RecipeData recipe = catalog[i];
            if (recipe == null)
                continue;
            if (!recipe.AllowsBuilding(thisBuildingData))
                continue;
            if (ResearchSystem.Instance != null && !ResearchSystem.Instance.IsRecipeUnlocked(recipe))
                continue;

            RecipeData captured = recipe;
            UiFactory.CreateRecipeCard(
                recipeButtonsParent,
                recipe,
                selected == recipe,
                () =>
                {
                    crafter.SetRecipe(captured);
                    RefreshRecipeList(crafter);
                });
        }

        if (currentRecipeText != null)
            currentRecipeText.text = selected != null ? "Selected: " + selected.displayName : "Select a recipe";

        Assembler assembler = crafter as Assembler;
        if (assembler != null)
            AddAssemblerUpgradeButton(assembler);
    }

    void AddAssemblerUpgradeButton(Assembler assembler)
    {
        if (recipeButtonsParent == null || assembler == null)
            return;

        string speed = "×" + assembler.CraftSpeed.ToString("0.##");
        if (assembler.CanUpgrade)
        {
            UiFactory.CreateActionButton(
                recipeButtonsParent,
                "Прокачать  →  ур. 2",
                "Скорость крафта " + speed + "  →  ×" + Mathf.Max(1f, assembler.upgradedCraftSpeed).ToString("0.##"),
                true,
                () =>
                {
                    if (assembler.TryUpgrade())
                    {
                        SetHeader("Assembler  ·  ур. " + assembler.level, assembler);
                        RefreshRecipeList(assembler);
                    }
                });
        }
        else
        {
            UiFactory.CreateActionButton(
                recipeButtonsParent,
                "Улучшено до ур. 2",
                "Скорость крафта " + speed,
                false,
                null);
        }
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

    // ================== ROBOTIC ARM ==================

    void ShowRoboticArmUI(RoboticArm arm)
    {
        EnsureArmFilterUi();
        if (armFilterContent != null)
            armFilterContent.SetActive(true);
        RefreshArmFilter(arm);
    }

    void EnsureArmFilterUi()
    {
        if (armFilterContent != null)
            return;

        Transform host = null;
        if (extractorContent != null)
            host = extractorContent.transform.parent;
        if (host == null && machinePanel != null)
            host = machinePanel.transform;
        if (host == null)
            return;

        armFilterContent = new GameObject("ArmFilterContent", typeof(RectTransform));
        armFilterContent.transform.SetParent(host, false);
        RectTransform root = armFilterContent.GetComponent<RectTransform>();
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;

        armFilterSummary = UiTheme.AddText(root, "Summary", "", 20f, UiTheme.Text);
        RectTransform summaryRt = armFilterSummary.rectTransform;
        summaryRt.anchorMin = new Vector2(0f, 1f);
        summaryRt.anchorMax = new Vector2(1f, 1f);
        summaryRt.pivot = new Vector2(0.5f, 1f);
        summaryRt.anchoredPosition = new Vector2(0f, -8f);
        summaryRt.sizeDelta = new Vector2(-32f, 32f);

        GameObject gridGo = new GameObject("Filters", typeof(RectTransform));
        gridGo.transform.SetParent(root, false);
        RectTransform gridRt = gridGo.GetComponent<RectTransform>();
        gridRt.anchorMin = new Vector2(0f, 0f);
        gridRt.anchorMax = new Vector2(1f, 1f);
        gridRt.offsetMin = new Vector2(16f, 16f);
        gridRt.offsetMax = new Vector2(-16f, -48f);
        armFilterParent = gridRt;

        UiTheme.EnsureGrid(armFilterParent, new Vector2(280f, 80f), new Vector2(10f, 10f), 2);
        UiTheme.EnsureVerticalScroll(gridRt);
    }

    void RefreshArmFilter(RoboticArm arm)
    {
        EnsureArmFilterUi();
        if (arm == null || armFilterParent == null)
            return;

        foreach (Transform child in armFilterParent)
            Destroy(child.gameObject);

        UiFactory.CreateFilterCard(
            armFilterParent,
            null,
            "Любые предметы",
            arm.filter == null ? "выбрано" : "без фильтра",
            arm.filter == null,
            () =>
            {
                arm.SetFilter(null);
                RefreshArmFilter(arm);
            });

        var seen = new HashSet<string>();
        ItemData[] items = GameDatabase.AllItems();
        for (int i = 0; i < items.Length; i++)
        {
            ItemData item = items[i];
            if (item == null || string.IsNullOrEmpty(item.id) || !seen.Add(item.id))
                continue;

            ItemData captured = item;
            bool selected = arm.filter == item;
            UiFactory.CreateFilterCard(
                armFilterParent,
                item.icon,
                item.displayName,
                selected ? "фильтр" : "брать только это",
                selected,
                () =>
                {
                    arm.SetFilter(captured);
                    RefreshArmFilter(arm);
                });
        }

        if (armFilterSummary != null)
        {
            string name = arm.filter != null ? arm.filter.displayName : "любые";
            string held = arm.HeldItem != null ? arm.HeldItem.displayName : "пусто";
            armFilterSummary.text = $"Фильтр: {name}   ·   в руке: {held}";
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
        if (currentBuilding is CrafterBuilding crafter && progressSlider != null)
        {
            if (crafter.currentRecipe != null && crafter.currentRecipe.craftTime > 0f)
                progressSlider.value = crafter.craftProgress / crafter.currentRecipe.craftTime;
            else
                progressSlider.value = 0f;
        }
        else if (currentBuilding is StorageContainer storage)
        {
            RefreshStorageSlots(storage);
        }
        else if (currentBuilding is RoboticArm arm && armFilterSummary != null)
        {
            string name = arm.filter != null ? arm.filter.displayName : "любые";
            string held = arm.HeldItem != null ? arm.HeldItem.displayName : "пусто";
            armFilterSummary.text = $"Фильтр: {name}   ·   в руке: {held}";
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