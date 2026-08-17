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
    int labTab;
    GameObject labChrome;
    GameObject labBody;
    GameObject researchPage;
    GameObject beltPage;
    GameObject statsPage;
    Transform beltList;
    Transform statsList;
    bool labCaptured;
    float nextStatsRefresh;


    [Header("All recipes in game")]
    public RecipeData[] allRecipes;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (BeltSpeedSystem.Instance != null)
            BeltSpeedSystem.Instance.OnChanged -= OnBeltChanged;
        if (Instance == this)
            Instance = null;
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
            int cost = Economy.UpgradeCost(extractor);
            bool canPay = PlayerWallet.Instance == null || PlayerWallet.Instance.CanAfford(cost);
            UiFactory.CreateActionButton(
                extractorButtonsParent,
                "Прокачать  →  ур. 2  ·  " + cost + " монет",
                "Сейчас: " + stats + "   |   После: " + next,
                canPay,
                () =>
                {
                    if (TryPaidUpgrade(extractor))
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

        AddCrafterUpgradeButton(crafter);
    }

    void AddCrafterUpgradeButton(CrafterBuilding crafter)
    {
        if (recipeButtonsParent == null || crafter == null)
            return;

        string speed = "×" + crafter.CraftSpeed.ToString("0.##");
        if (crafter.CanUpgradeBuilding)
        {
            int cost = Economy.UpgradeCost(crafter);
            bool canPay = PlayerWallet.Instance == null || PlayerWallet.Instance.CanAfford(cost);
            UiFactory.CreateActionButton(
                recipeButtonsParent,
                "Прокачать  →  ур. 2  ·  " + cost + " монет",
                "Скорость крафта " + speed + "  →  ×2",
                canPay,
                () =>
                {
                    if (TryPaidUpgrade(crafter))
                    {
                        string title = crafter.data != null ? crafter.data.displayName : crafter.GetType().Name;
                        SetHeader(title + "  ·  ур. " + crafter.ReadLevel(), crafter);
                        RefreshRecipeList(crafter);
                    }
                });
        }
        else if (crafter.ReadLevel() >= 2)
        {
            UiFactory.CreateActionButton(
                recipeButtonsParent,
                "Улучшено до ур. 2",
                "Скорость крафта " + speed,
                false,
                null);
        }
    }

    static bool TryPaidUpgrade(BuildingBase building)
    {
        if (building == null || !building.CanUpgradeBuilding)
            return false;
        int cost = Economy.UpgradeCost(building);
        if (PlayerWallet.Instance != null && !PlayerWallet.Instance.TrySpendCoins(cost))
            return false;
        if (building.TryUpgradeBuilding())
            return true;
        if (PlayerWallet.Instance != null)
            PlayerWallet.Instance.AddCoins(cost);
        return false;
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
        if (researchLabContent != null)
            researchLabContent.SetActive(true);
        EnsureLabChrome();
        OpenLabTab(labTab);
    }

    void EnsureLabChrome()
    {
        if (researchLabContent == null || labChrome != null)
            return;

        labChrome = new GameObject("LabTabs", typeof(RectTransform));
        labChrome.transform.SetParent(researchLabContent.transform, false);
        RectTransform tabsRt = labChrome.GetComponent<RectTransform>();
        tabsRt.anchorMin = new Vector2(0f, 1f);
        tabsRt.anchorMax = new Vector2(1f, 1f);
        tabsRt.pivot = new Vector2(0.5f, 1f);
        tabsRt.anchoredPosition = Vector2.zero;
        tabsRt.sizeDelta = new Vector2(0f, 56f);
        tabsRt.offsetMin = new Vector2(12f, tabsRt.offsetMin.y);
        tabsRt.offsetMax = new Vector2(-12f, tabsRt.offsetMax.y);

        HorizontalLayoutGroup row = labChrome.AddComponent<HorizontalLayoutGroup>();
        row.spacing = 8f;
        row.childForceExpandHeight = true;
        row.childForceExpandWidth = true;
        row.padding = new RectOffset(0, 0, 6, 6);
        row.childAlignment = TextAnchor.MiddleCenter;

        AddLabTabButton("Исследования", 0);
        AddLabTabButton("Конвейеры", 1);
        AddLabTabButton("Статистика", 2);

        labBody = new GameObject("LabBody", typeof(RectTransform));
        labBody.transform.SetParent(researchLabContent.transform, false);
        RectTransform bodyRt = labBody.GetComponent<RectTransform>();
        bodyRt.anchorMin = Vector2.zero;
        bodyRt.anchorMax = Vector2.one;
        bodyRt.offsetMin = new Vector2(12f, 12f);
        bodyRt.offsetMax = new Vector2(-12f, -64f);

        researchPage = CreateLabPage("ResearchPage");
        beltPage = CreateLabPage("BeltPage");
        statsPage = CreateLabPage("StatsPage");

        beltList = CreateScrollColumn(beltPage.transform, "BeltTree");
        statsList = CreateScrollColumn(statsPage.transform, "StatsList");

        CaptureResearchIntoPage();
        if (BeltSpeedSystem.Instance != null)
            BeltSpeedSystem.Instance.OnChanged += OnBeltChanged;
    }

    void OnBeltChanged()
    {
        if (IsOpen && currentBuilding is ResearchLab && labTab == 1)
            RebuildBeltTree();
    }

    GameObject CreateLabPage(string name)
    {
        GameObject page = new GameObject(name, typeof(RectTransform));
        page.transform.SetParent(labBody.transform, false);
        RectTransform rt = page.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        page.SetActive(false);
        return page;
    }

    static Transform CreateScrollColumn(Transform parent, string name)
    {
        GameObject content = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(parent, false);
        RectTransform rt = content.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        UiTheme.EnsureVerticalScroll(rt);
        return content.transform;
    }

    void CaptureResearchIntoPage()
    {
        if (labCaptured || researchPage == null || researchLabContent == null)
            return;
        labCaptured = true;

        var move = new List<Transform>();
        foreach (Transform child in researchLabContent.transform)
        {
            if (child == null)
                continue;
            if (child.gameObject == labChrome || child.gameObject == labBody)
                continue;
            move.Add(child);
        }

        for (int i = 0; i < move.Count; i++)
            move[i].SetParent(researchPage.transform, false);
    }

    void AddLabTabButton(string label, int tab)
    {
        GameObject go = new GameObject("Tab" + tab, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(labChrome.transform, false);
        UiTheme.StyleImage(go.GetComponent<Image>(), tab == labTab ? UiTheme.AccentDim : UiTheme.Card);
        Button button = go.GetComponent<Button>();
        int captured = tab;
        button.onClick.AddListener(() => OpenLabTab(captured));
        TextMeshProUGUI text = UiTheme.AddText(go.transform, "Label", label, 18f, UiTheme.Text);
        text.alignment = TextAlignmentOptions.Center;
        RectTransform textRt = text.rectTransform;
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;
    }

    void OpenLabTab(int tab)
    {
        labTab = tab;
        if (researchPage != null)
            researchPage.SetActive(tab == 0);
        if (beltPage != null)
            beltPage.SetActive(tab == 1);
        if (statsPage != null)
            statsPage.SetActive(tab == 2);

        if (labChrome != null)
        {
            for (int i = 0; i < labChrome.transform.childCount; i++)
            {
                Image image = labChrome.transform.GetChild(i).GetComponent<Image>();
                if (image != null)
                    image.color = i == tab ? UiTheme.AccentDim : UiTheme.Card;
            }
        }

        if (tab == 0)
            FillResearchTab();
        else if (tab == 1)
            RebuildBeltTree();
        else
            RebuildStatsList();
    }

    void FillResearchTab()
    {
        if (researchButtonsParent == null || ResearchSystem.Instance == null)
            return;

        foreach (Transform child in researchButtonsParent)
            Destroy(child.gameObject);

        foreach (var node in ResearchSystem.Instance.GetAllNodes())
        {
            if (node == null)
                continue;

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

    void RebuildBeltTree()
    {
        if (beltList == null)
            return;

        for (int i = beltList.childCount - 1; i >= 0; i--)
            Destroy(beltList.GetChild(i).gameObject);

        BeltSpeedSystem belts = BeltSpeedSystem.Instance;
        int level = belts != null ? belts.Level : 0;
        int have = belts != null ? belts.GearsTowardNext : 0;
        ItemData gear = GameDatabase.FindItem("gear");
        Sprite gearIcon = gear != null ? gear.icon : null;

        TextMeshProUGUI head = UiTheme.AddText(beltList, "Head", "Дерево прокачки конвейеров", 22f, UiTheme.Accent);
        head.fontStyle = FontStyles.Bold;
        LayoutText(head, 32f);
        TextMeshProUGUI hint = UiTheme.AddText(
            beltList,
            "Hint",
            "Сдавайте шестерёнки в лабораторию. Лишние (не нужные исследованию) идут в это дерево. Уже стоящие ленты тоже ускоряются.",
            16f,
            UiTheme.TextDim);
        hint.enableWordWrapping = true;
        LayoutText(hint, 56f);

        int lastShown = level + 1;
        for (int i = 0; i <= lastShown; i++)
        {
            bool unlocked = i <= level;
            bool next = i == level + 1;
            float speedNow = Economy.BeltMultiplier(i);
            float speedPrev = i == 0 ? speedNow : Economy.BeltMultiplier(i - 1);
            int cost = i == 0 ? 0 : Economy.BeltUpgradeCost(i);
            string state = unlocked ? (i == level ? "текущий" : "открыт") : "следующий";
            string speedText = i == 0
                ? "Скорость ×" + speedNow.ToString("0.##")
                : "Скорость ×" + speedPrev.ToString("0.##") + "  →  ×" + speedNow.ToString("0.##");
            string costText = i == 0
                ? "Цена: старт"
                : next
                    ? "Цена: " + have + " / " + cost + " шестерёнок"
                    : "Цена: " + cost + " шестерёнок";

            AddBeltNode(
                beltList,
                i,
                "Ур. " + i + "  ·  " + state,
                speedText,
                costText,
                gearIcon,
                unlocked,
                next,
                i < lastShown);
        }
    }

    void AddBeltNode(Transform parent, int level, string title, string speed, string cost, Sprite icon, bool unlocked, bool next, bool connector)
    {
        GameObject row = new GameObject("Belt_" + level, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        row.transform.SetParent(parent, false);
        Image bg = row.GetComponent<Image>();
        UiTheme.StyleImage(bg, next ? UiTheme.AccentDim : unlocked ? UiTheme.Card : UiTheme.Chip);
        row.GetComponent<LayoutElement>().preferredHeight = 96f;
        row.GetComponent<LayoutElement>().minHeight = 96f;

        Image mark = UiTheme.AddImage(row.transform, "Mark", new Vector2(18f, 18f), next ? UiTheme.Warn : unlocked ? UiTheme.Ok : UiTheme.Muted);
        RectTransform markRt = mark.rectTransform;
        markRt.anchorMin = new Vector2(0f, 0.5f);
        markRt.anchorMax = new Vector2(0f, 0.5f);
        markRt.pivot = new Vector2(0.5f, 0.5f);
        markRt.anchoredPosition = new Vector2(28f, 8f);
        markRt.sizeDelta = new Vector2(18f, 18f);

        if (connector)
        {
            Image line = UiTheme.AddImage(row.transform, "Line", new Vector2(4f, 28f), UiTheme.Muted);
            RectTransform lineRt = line.rectTransform;
            lineRt.anchorMin = new Vector2(0f, 0f);
            lineRt.anchorMax = new Vector2(0f, 0f);
            lineRt.pivot = new Vector2(0.5f, 0f);
            lineRt.anchoredPosition = new Vector2(28f, 2f);
            lineRt.sizeDelta = new Vector2(4f, 26f);
        }

        if (icon != null)
        {
            Image gear = UiTheme.AddImage(row.transform, "Icon", new Vector2(36f, 36f), Color.white);
            gear.type = Image.Type.Simple;
            gear.preserveAspect = true;
            gear.sprite = icon;
            RectTransform iconRt = gear.rectTransform;
            iconRt.anchorMin = new Vector2(0f, 0.5f);
            iconRt.anchorMax = new Vector2(0f, 0.5f);
            iconRt.anchoredPosition = new Vector2(68f, 0f);
            iconRt.sizeDelta = new Vector2(36f, 36f);
        }

        TextMeshProUGUI titleText = UiTheme.AddText(row.transform, "Title", title, 20f, unlocked || next ? UiTheme.Text : UiTheme.TextDim);
        titleText.fontStyle = FontStyles.Bold;
        RectTransform titleRt = titleText.rectTransform;
        titleRt.anchorMin = new Vector2(0f, 0.58f);
        titleRt.anchorMax = new Vector2(1f, 0.95f);
        titleRt.offsetMin = new Vector2(100f, 0f);
        titleRt.offsetMax = new Vector2(-12f, 0f);

        TextMeshProUGUI speedText = UiTheme.AddText(row.transform, "Speed", speed, 16f, UiTheme.Accent);
        RectTransform speedRt = speedText.rectTransform;
        speedRt.anchorMin = new Vector2(0f, 0.3f);
        speedRt.anchorMax = new Vector2(1f, 0.58f);
        speedRt.offsetMin = new Vector2(100f, 0f);
        speedRt.offsetMax = new Vector2(-12f, 0f);

        TextMeshProUGUI costText = UiTheme.AddText(row.transform, "Cost", cost, 16f, next ? UiTheme.Warn : UiTheme.TextDim);
        RectTransform costRt = costText.rectTransform;
        costRt.anchorMin = new Vector2(0f, 0.05f);
        costRt.anchorMax = new Vector2(1f, 0.3f);
        costRt.offsetMin = new Vector2(100f, 0f);
        costRt.offsetMax = new Vector2(-12f, 0f);
    }

    void RebuildStatsList()
    {
        if (statsList == null)
            return;

        for (int i = statsList.childCount - 1; i >= 0; i--)
            Destroy(statsList.GetChild(i).gameObject);

        ProductionStats stats = ProductionStats.Instance;
        PlayerWallet wallet = PlayerWallet.Instance;

        AddStatMoneyRow(statsList, GameHudIcons.Coin, "Монеты",
            wallet != null ? wallet.Coins : 0,
            stats != null ? stats.CoinsPerMinute() : 0f,
            stats != null ? stats.CoinsSpentPerMinute() : 0f,
            stats != null ? stats.CoinsGainedTotal : 0,
            stats != null ? stats.CoinsSpentTotal : 0);
        AddStatMoneyRow(statsList, GameHudIcons.Ruby, "Рубины",
            wallet != null ? wallet.Rubies : 0,
            stats != null ? stats.RubiesPerMinute() : 0f,
            0f,
            stats != null ? stats.RubiesGainedTotal : 0,
            0);

        if (stats == null)
            return;

        var ids = new HashSet<string>();
        foreach (var pair in stats.ProducedTotal)
            ids.Add(pair.Key);
        foreach (var pair in stats.ConsumedTotal)
            ids.Add(pair.Key);
        var sorted = new List<string>(ids);
        sorted.Sort();

        for (int i = 0; i < sorted.Count; i++)
        {
            string id = sorted[i];
            ItemData item = GameDatabase.FindItem(id);
            stats.ProducedTotal.TryGetValue(id, out int made);
            stats.ConsumedTotal.TryGetValue(id, out int used);
            AddStatItemRow(
                statsList,
                item != null ? item.icon : null,
                item != null ? item.displayName : id,
                made,
                used,
                stats.ProducedPerMinute(id),
                stats.ConsumedPerMinute(id));
        }

        nextStatsRefresh = Time.unscaledTime + 0.6f;
    }

    void AddStatMoneyRow(Transform parent, Sprite icon, string name, int now, float plusMin, float minusMin, int gained, int spent)
    {
        string sub = "сейчас " + now + "   всего +" + gained + (spent > 0 ? "  −" + spent : "");
        string rates = "+" + plusMin.ToString("0.#") + "/мин" + (minusMin > 0f ? "   −" + minusMin.ToString("0.#") + "/мин" : "");
        AddStatItemRow(parent, icon, name, sub, rates);
    }

    void AddStatItemRow(Transform parent, Sprite icon, string name, int made, int used, float plusMin, float minusMin)
    {
        AddStatItemRow(
            parent,
            icon,
            name,
            "всего +" + made + "   −" + used,
            "+" + plusMin.ToString("0.#") + "/мин   −" + minusMin.ToString("0.#") + "/мин");
    }

    void AddStatItemRow(Transform parent, Sprite icon, string name, string totals, string rates)
    {
        GameObject row = new GameObject("Stat", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        row.transform.SetParent(parent, false);
        UiTheme.StyleImage(row.GetComponent<Image>(), UiTheme.Card);
        row.GetComponent<LayoutElement>().preferredHeight = 64f;
        row.GetComponent<LayoutElement>().minHeight = 64f;

        Image image = UiTheme.AddImage(row.transform, "Icon", new Vector2(40f, 40f), Color.white);
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.sprite = icon;
        image.enabled = icon != null;
        RectTransform iconRt = image.rectTransform;
        iconRt.anchorMin = new Vector2(0f, 0.5f);
        iconRt.anchorMax = new Vector2(0f, 0.5f);
        iconRt.anchoredPosition = new Vector2(32f, 0f);
        iconRt.sizeDelta = new Vector2(40f, 40f);

        TextMeshProUGUI title = UiTheme.AddText(row.transform, "Name", name, 18f, UiTheme.Text);
        title.fontStyle = FontStyles.Bold;
        RectTransform titleRt = title.rectTransform;
        titleRt.anchorMin = new Vector2(0f, 0.5f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.offsetMin = new Vector2(64f, 0f);
        titleRt.offsetMax = new Vector2(-12f, -4f);

        TextMeshProUGUI sub = UiTheme.AddText(row.transform, "Totals", totals + "   ·   " + rates, 15f, UiTheme.TextDim);
        RectTransform subRt = sub.rectTransform;
        subRt.anchorMin = new Vector2(0f, 0f);
        subRt.anchorMax = new Vector2(1f, 0.52f);
        subRt.offsetMin = new Vector2(64f, 6f);
        subRt.offsetMax = new Vector2(-12f, 0f);
    }

    static void LayoutText(TextMeshProUGUI text, float height)
    {
        LayoutElement layout = text.gameObject.AddComponent<LayoutElement>();
        layout.preferredHeight = height;
        layout.minHeight = height;
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

            if (labTab == 2 && Time.unscaledTime >= nextStatsRefresh)
                RebuildStatsList();
        }
    }
}