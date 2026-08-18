using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class MachineUI : MonoBehaviour
{
    public static MachineUI Instance { get; private set; }

    [Header("Legacy uGUI (hidden)")]
    public GameObject machinePanel;
    public GameObject extractorContent;
    public GameObject smelterContent;
    public GameObject assemblerContent;
    public GameObject researchLabContent;

    [Header("Data")]
    public ItemData[] availableResources;
    public RecipeData[] allRecipes;

    public bool IsOpen { get; private set; }

    VisualElement overlay;
    Label statusLabel;
    VisualElement progress;
    VisualElement tabRow;
    Button tabResearch;
    Button tabBelts;
    Button tabStats;
    VisualElement bodyHost;
    ScrollView bodyList;
    VisualElement researchPage;
    VisualElement beltPage;
    VisualElement statsPage;
    ScrollView researchList;
    ScrollView beltList;
    ScrollView statsList;
    VisualElement storageGrid;
    VisualElement storageScroll;
    Label storageSummary;
    VisualElement armGrid;
    VisualElement armScroll;
    Label armSummary;
    readonly List<VisualElement> storageSlots = new List<VisualElement>();

    BuildingBase currentBuilding;
    int labTab;
    float nextStatsRefresh;

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    void OnEnable()
    {
        if (BeltSpeedSystem.Instance != null)
            BeltSpeedSystem.Instance.OnChanged += OnBeltChanged;
    }

    void OnDisable()
    {
        if (BeltSpeedSystem.Instance != null)
            BeltSpeedSystem.Instance.OnChanged -= OnBeltChanged;
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
        Build();
        IndustryUi.HideLegacy(this, machinePanel, extractorContent, smelterContent, assemblerContent, researchLabContent);
        IndustryUi.DisableHudCanvas(this);
    }

    void Update()
    {
        if (IsOpen && currentBuilding != null)
            UpdateProgress();
    }

    void Build()
    {
        VisualElement root = IndustryUi.Mount(this, 110);
        overlay = IndustryUi.OverlayPanel("Building", null, Close);
        VisualElement panel = IndustryUi.PanelOf(overlay);
        statusLabel = IndustryUi.Text("Status", "", "muted");
        panel.Add(statusLabel);
        progress = IndustryUi.ProgressBar("Progress");
        panel.Add(progress);

        tabRow = IndustryUi.El("Tabs", "tab-row");
        tabResearch = IndustryUi.TabBtn("Исследования", () => OpenLabTab(0));
        tabBelts = IndustryUi.TabBtn("Конвейеры", () => OpenLabTab(1));
        tabStats = IndustryUi.TabBtn("Статистика", () => OpenLabTab(2));
        tabRow.Add(tabResearch);
        tabRow.Add(tabBelts);
        tabRow.Add(tabStats);
        panel.Add(tabRow);

        bodyHost = IndustryUi.El("Body", "col", "grow");
        bodyList = IndustryUi.Scroll("BodyList");
        bodyHost.Add(bodyList);

        researchPage = IndustryUi.El("ResearchPage", "col", "grow");
        researchList = IndustryUi.Scroll("ResearchList");
        researchPage.Add(researchList);

        beltPage = IndustryUi.El("BeltPage", "col", "grow");
        beltList = IndustryUi.Scroll("BeltList");
        beltPage.Add(beltList);

        statsPage = IndustryUi.El("StatsPage", "col", "grow");
        statsList = IndustryUi.Scroll("StatsList");
        statsPage.Add(statsList);

        storageSummary = IndustryUi.Text("StorageSum", "", "body-text");
        storageScroll = IndustryUi.Scroll("StorageScroll");
        storageGrid = IndustryUi.El("StorageGrid", "grid");
        storageScroll.Add(storageGrid);
        armSummary = IndustryUi.Text("ArmSum", "", "body-text");
        armScroll = IndustryUi.Scroll("ArmScroll");
        armGrid = IndustryUi.El("ArmGrid", "col");
        armScroll.Add(armGrid);

        bodyHost.Add(researchPage);
        bodyHost.Add(beltPage);
        bodyHost.Add(statsPage);
        bodyHost.Add(storageSummary);
        bodyHost.Add(storageScroll);
        bodyHost.Add(armSummary);
        bodyHost.Add(armScroll);
        panel.Add(bodyHost);

        IndustryUi.Show(overlay, false);
        root.Add(overlay);
        HidePages();
    }

    public void Open(BuildingBase building)
    {
        if (building == null)
            return;
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);
        if (overlay == null)
            Build();
        if (GameManager.Instance != null && GameManager.Instance.IsPaused)
            return;
        if (WalletHud.Instance != null && WalletHud.Instance.IsShopOpen)
            WalletHud.Instance.SetShopOpen(false);
        if (SelectionActionsUI.Instance != null && SelectionActionsUI.Instance.IsOpen)
            SelectionActionsUI.Instance.SetOpen(false);

        currentBuilding = building;
        IsOpen = true;
        HidePages();

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
        else if (building is ResearchLab)
        {
            SetHeader("Research Lab", building);
            ShowResearchLabUI();
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
            IndustryUi.Show(bodyList, true);
        }

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

    public void Close()
    {
        IsOpen = false;
        currentBuilding = null;
        IndustryUi.Show(overlay, false);
        if (GameManager.Instance != null)
            GameManager.Instance.RestoreGameplayFocus();
        else
        {
            UnityEngine.Cursor.lockState = CursorLockMode.Locked;
            UnityEngine.Cursor.visible = false;
            SetPlayerControl(true);
        }
    }

    void SetHeader(string title, BuildingBase building)
    {
        Sprite icon = building != null && building.data != null ? building.data.icon : null;
        IndustryUi.SetHeader(overlay, title, icon);
    }

    static void SetPlayerControl(bool enabled)
    {
        PlayerMovement movement = FindFirstObjectByType<PlayerMovement>();
        if (movement == null)
            return;
        movement.canMove = enabled;
        movement.canLook = enabled;
    }

    void HidePages()
    {
        IndustryUi.Show(tabRow, false);
        IndustryUi.Show(bodyList, false);
        IndustryUi.Show(researchPage, false);
        IndustryUi.Show(beltPage, false);
        IndustryUi.Show(statsPage, false);
        IndustryUi.Show(storageSummary, false);
        IndustryUi.Show(storageScroll, false);
        IndustryUi.Show(armSummary, false);
        IndustryUi.Show(armScroll, false);
        IndustryUi.Show(progress, false);
        if (statusLabel != null)
            statusLabel.text = "";
    }

    void ShowOilExtractorUI(OilExtractor oil)
    {
        IndustryUi.Show(bodyList, true);
        bodyList.Clear();
        var outputs = new List<ItemStack>();
        if (oil.resource != null)
            outputs.Add(new ItemStack(oil.resource, oil.CurrentItemsPerCycle));
        string title = oil.resource != null ? "Добыча  " + oil.resource.displayName : "Нет нефти";
        bodyList.Add(IndustryUi.RecipeCard(title, null, outputs, true, null));
        bodyList.Add(IndustryUi.ActionCard(
            "Скважина",
            oil.RichnessName + "  ·  " + oil.CurrentInterval.ToString("0.##") + " с / цикл  ·  бесконечный запас",
            false,
            null));
    }

    void ShowWaterExtractorUI(WaterExtractor pump)
    {
        IndustryUi.Show(bodyList, true);
        bodyList.Clear();
        var outputs = new List<ItemStack>();
        if (pump.resource != null)
            outputs.Add(new ItemStack(pump.resource, pump.CurrentItemsPerCycle));
        string title = pump.resource != null ? "Добыча  " + pump.resource.displayName : "Нет воды";
        bodyList.Add(IndustryUi.RecipeCard(title, null, outputs, true, null));
        bodyList.Add(IndustryUi.ActionCard(
            "Источник",
            pump.CurrentInterval.ToString("0.##") + " с / цикл  ·  бесконечный запас",
            false,
            null));
    }

    void ShowExtractorUI(Extractor extractor)
    {
        IndustryUi.Show(bodyList, true);
        bodyList.Clear();
        var outputs = new List<ItemStack>();
        if (extractor.resource != null)
            outputs.Add(new ItemStack(extractor.resource, extractor.CurrentItemsPerCycle));
        string title = extractor.resource != null
            ? "Mining  " + extractor.resource.displayName
            : "No resource node";
        bodyList.Add(IndustryUi.RecipeCard(title, null, outputs, true, null));

        string stats = $"{extractor.CurrentInterval:0.##} с / цикл  ·  {extractor.CurrentItemsPerCycle} шт";
        if (extractor.CanUpgrade)
        {
            string next = $"{extractor.upgradedExtractInterval:0.##} с / цикл  ·  {Mathf.Max(1, extractor.upgradedItemsPerCycle)} шт";
            int cost = Economy.UpgradeCost(extractor);
            bool canPay = PlayerWallet.Instance == null || PlayerWallet.Instance.CanAfford(cost);
            bodyList.Add(IndustryUi.ActionCard(
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
                }));
        }
        else
        {
            bodyList.Add(IndustryUi.ActionCard("Улучшено до ур. 2", stats, false, null));
        }
    }

    void ShowCrafterUI(CrafterBuilding crafter)
    {
        IndustryUi.Show(bodyList, true);
        IndustryUi.Show(progress, true);
        RefreshRecipeList(crafter);
    }

    void RefreshRecipeList(CrafterBuilding crafter)
    {
        if (bodyList == null)
            return;
        bodyList.Clear();
        if (crafter == null)
            return;

        RecipeData[] catalog = GameDatabase.AllRecipes();
        BuildingData thisBuildingData = crafter.data;
        RecipeData selected = crafter.currentRecipe;

        if (catalog != null)
        {
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
                bodyList.Add(IndustryUi.RecipeCard(
                    recipe,
                    selected == recipe,
                    () =>
                    {
                        crafter.SetRecipe(captured);
                        RefreshRecipeList(crafter);
                    }));
            }
        }

        if (statusLabel != null)
            statusLabel.text = selected != null ? "Selected: " + selected.displayName : "Select a recipe";
        AddCrafterUpgradeButton(crafter);
    }

    void AddCrafterUpgradeButton(CrafterBuilding crafter)
    {
        if (bodyList == null || crafter == null)
            return;

        string speed = "×" + crafter.CraftSpeed.ToString("0.##");
        if (crafter.CanUpgradeBuilding)
        {
            int cost = Economy.UpgradeCost(crafter);
            bool canPay = PlayerWallet.Instance == null || PlayerWallet.Instance.CanAfford(cost);
            bodyList.Add(IndustryUi.ActionCard(
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
                }));
        }
        else if (crafter.ReadLevel() >= 2)
        {
            bodyList.Add(IndustryUi.ActionCard("Улучшено до ур. 2", "Скорость крафта " + speed, false, null));
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

    void ShowStorageUI(StorageContainer storage)
    {
        IndustryUi.Show(storageSummary, true);
        IndustryUi.Show(storageScroll, true);
        RefreshStorageSlots(storage);
    }

    void RefreshStorageSlots(StorageContainer storage)
    {
        if (storage == null || storageGrid == null)
            return;

        int count = storage.SlotCount;
        while (storageSlots.Count < count)
        {
            VisualElement cell = IndustryUi.StorageCell();
            storageSlots.Add(cell);
            storageGrid.Add(cell);
        }

        for (int i = 0; i < storageSlots.Count; i++)
        {
            IndustryUi.Show(storageSlots[i], i < count);
            if (i < count)
                IndustryUi.BindStorage(storageSlots[i], storage.GetSlot(i));
        }

        if (storageSummary != null)
        {
            string typeName = storage.StoredType != null ? storage.StoredType.displayName : "пусто";
            storageSummary.text = $"{typeName}  ·  стеки {storage.UsedSlotCount} / {count}";
        }
    }

    void ShowRoboticArmUI(RoboticArm arm)
    {
        IndustryUi.Show(armSummary, true);
        IndustryUi.Show(armScroll, true);
        RefreshArmFilter(arm);
    }

    void RefreshArmFilter(RoboticArm arm)
    {
        if (arm == null || armGrid == null)
            return;

        armGrid.Clear();
        armGrid.Add(IndustryUi.FilterCard(
            null,
            "Любые предметы",
            arm.filter == null ? "выбрано" : "без фильтра",
            arm.filter == null,
            () =>
            {
                arm.SetFilter(null);
                RefreshArmFilter(arm);
            }));

        var seen = new HashSet<string>();
        ItemData[] items = GameDatabase.AllItems();
        if (items != null)
        {
            for (int i = 0; i < items.Length; i++)
            {
                ItemData item = items[i];
                if (item == null || string.IsNullOrEmpty(item.id) || !seen.Add(item.id))
                    continue;
                ItemData captured = item;
                bool selected = arm.filter == item;
                armGrid.Add(IndustryUi.FilterCard(
                    item.icon,
                    item.displayName,
                    selected ? "фильтр" : "брать только это",
                    selected,
                    () =>
                    {
                        arm.SetFilter(captured);
                        RefreshArmFilter(arm);
                    }));
            }
        }

        UpdateArmSummary(arm);
    }

    void UpdateArmSummary(RoboticArm arm)
    {
        if (armSummary == null || arm == null)
            return;
        string name = arm.filter != null ? arm.filter.displayName : "любые";
        string held = arm.HeldItem != null ? arm.HeldItem.displayName : "пусто";
        armSummary.text = $"Фильтр: {name}   ·   в руке: {held}";
    }

    void ShowResearchLabUI()
    {
        IndustryUi.Show(tabRow, true);
        IndustryUi.Show(progress, true);
        OpenLabTab(labTab);
    }

    void OnBeltChanged()
    {
        if (IsOpen && currentBuilding is ResearchLab && labTab == 1)
            RebuildBeltTree();
    }

    void OpenLabTab(int tab)
    {
        labTab = tab;
        IndustryUi.Show(researchPage, tab == 0);
        IndustryUi.Show(beltPage, tab == 1);
        IndustryUi.Show(statsPage, tab == 2);
        IndustryUi.SetOn(tabResearch, tab == 0, "tab-on");
        IndustryUi.SetOn(tabBelts, tab == 1, "tab-on");
        IndustryUi.SetOn(tabStats, tab == 2, "tab-on");

        if (tab == 0)
            FillResearchTab();
        else if (tab == 1)
            RebuildBeltTree();
        else
            RebuildStatsList();
    }

    void FillResearchTab()
    {
        if (researchList == null || ResearchSystem.Instance == null)
            return;

        researchList.Clear();
        foreach (ResearchNodeData node in ResearchSystem.Instance.GetAllNodes())
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
            researchList.Add(IndustryUi.ResearchCard(
                node,
                status,
                canStart,
                () =>
                {
                    if (ResearchSystem.Instance.SetCurrentResearch(captured))
                        Close();
                }));
        }
    }

    void RebuildBeltTree()
    {
        if (beltList == null)
            return;

        beltList.Clear();
        BeltSpeedSystem belts = BeltSpeedSystem.Instance;
        int level = belts != null ? belts.Level : 0;
        int have = belts != null ? belts.GearsTowardNext : 0;
        ItemData gear = GameDatabase.FindItem("gear");
        Sprite gearIcon = gear != null ? gear.icon : null;

        beltList.Add(IndustryUi.Text("Head", "Дерево прокачки конвейеров", "title"));
        beltList.Add(IndustryUi.Text(
            "Hint",
            "Сдавайте шестерёнки в лабораторию. Лишние (не нужные исследованию) идут в это дерево. Уже стоящие ленты тоже ускоряются.",
            "muted"));

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

            beltList.Add(BeltNode(
                "Ур. " + i + "  ·  " + state,
                speedText,
                costText,
                gearIcon,
                unlocked,
                next));
        }
    }

    static VisualElement BeltNode(string title, string speed, string cost, Sprite icon, bool unlocked, bool next)
    {
        var row = IndustryUi.El("Belt", "card");
        if (next)
            row.AddToClassList("card-on");
        var mark = IndustryUi.El("Mark", "belt-mark");
        mark.style.backgroundColor = next
            ? new Color(0.86f, 0.65f, 0.29f)
            : unlocked
                ? new Color(0.43f, 0.77f, 0.51f)
                : new Color(0.35f, 0.38f, 0.4f);
        row.Add(mark);
        row.Add(IndustryUi.Icon(icon, "icon-32"));
        var col = IndustryUi.El("C", "col", "grow");
        col.Add(IndustryUi.Text("T", title, unlocked || next ? "body-text" : "muted"));
        col.Add(IndustryUi.Text("S", speed, "gold"));
        col.Add(IndustryUi.Text("K", cost, next ? "warn" : "muted"));
        row.Add(col);
        return row;
    }

    void RebuildStatsList()
    {
        if (statsList == null)
            return;

        statsList.Clear();
        ProductionStats stats = ProductionStats.Instance;
        PlayerWallet wallet = PlayerWallet.Instance;

        AddMoneyStat(
            GameHudIcons.Coin,
            "Монеты",
            wallet != null ? wallet.Coins : 0,
            stats != null ? stats.CoinsPerMinute() : 0f,
            stats != null ? stats.CoinsSpentPerMinute() : 0f,
            stats != null ? stats.CoinsGainedTotal : 0,
            stats != null ? stats.CoinsSpentTotal : 0);
        AddMoneyStat(
            GameHudIcons.Ruby,
            "Рубины",
            wallet != null ? wallet.Rubies : 0,
            stats != null ? stats.RubiesPerMinute() : 0f,
            0f,
            stats != null ? stats.RubiesGainedTotal : 0,
            0);

        if (stats != null)
        {
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
                statsList.Add(IndustryUi.StatRow(
                    item != null ? item.icon : null,
                    item != null ? item.displayName : id,
                    "всего +" + made + "   −" + used
                    + "   ·   +" + stats.ProducedPerMinute(id).ToString("0.#") + "/мин   −"
                    + stats.ConsumedPerMinute(id).ToString("0.#") + "/мин"));
            }
        }

        nextStatsRefresh = Time.unscaledTime + 0.6f;
    }

    void AddMoneyStat(Sprite icon, string name, int now, float plusMin, float minusMin, int gained, int spent)
    {
        string detail = "сейчас " + now + "   всего +" + gained + (spent > 0 ? "  −" + spent : "")
            + "   ·   +" + plusMin.ToString("0.#") + "/мин"
            + (minusMin > 0f ? "   −" + minusMin.ToString("0.#") + "/мин" : "");
        statsList.Add(IndustryUi.StatRow(icon, name, detail));
    }

    void UpdateProgress()
    {
        if (currentBuilding is CrafterBuilding crafter)
        {
            float t = 0f;
            if (crafter.currentRecipe != null && crafter.currentRecipe.craftTime > 0f)
                t = crafter.craftProgress / crafter.currentRecipe.craftTime;
            IndustryUi.SetProgress(progress, t);
        }
        else if (currentBuilding is StorageContainer storage)
        {
            RefreshStorageSlots(storage);
        }
        else if (currentBuilding is RoboticArm arm)
        {
            UpdateArmSummary(arm);
        }
        else if (currentBuilding is ResearchLab)
        {
            float t = ResearchSystem.Instance != null
                ? ResearchSystem.Instance.GetCurrentProgress01()
                : 0f;
            IndustryUi.SetProgress(progress, t);
            if (statusLabel != null)
            {
                string name = ResearchSystem.Instance != null && ResearchSystem.Instance.CurrentResearch != null
                    ? ResearchSystem.Instance.CurrentResearch.displayName
                    : "None";
                statusLabel.text = $"{name}: {(t * 100f):0}%";
            }

            if (labTab == 2 && Time.unscaledTime >= nextStatsRefresh)
                RebuildStatsList();
        }
    }
}
