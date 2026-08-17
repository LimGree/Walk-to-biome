using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class SelectionActionsUI : MonoBehaviour
{
    public static SelectionActionsUI Instance { get; private set; }

    BuildSelectionController selection;
    GameObject overlay;
    GameObject mainView;
    GameObject recipeView;
    Transform list;
    Transform recipeList;
    TextMeshProUGUI summary;
    TextMeshProUGUI recipeTitle;
    string recipeTypeId;
    int lastCount = -1;
    InputAction panelAction;

    public bool IsOpen => overlay != null && overlay.activeSelf;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        selection = FindFirstObjectByType<BuildSelectionController>();
        Build();
        SetOpen(false);
        BindInput();
    }

    void OnEnable()
    {
        BindInput();
    }

    void OnDisable()
    {
        UnbindInput();
    }

    void OnDestroy()
    {
        UnbindInput();
        if (Instance == this)
            Instance = null;
    }

    void BindInput()
    {
        if (panelAction != null)
            return;
        InputSystem_Actions actions = KeybindStore.Shared;
        panelAction = actions != null ? actions.asset.FindAction("Player/SelectionPanel", false) : null;
        if (panelAction != null)
            panelAction.performed += OnPanelPerformed;
    }

    void UnbindInput()
    {
        if (panelAction != null)
            panelAction.performed -= OnPanelPerformed;
        panelAction = null;
    }

    void OnPanelPerformed(InputAction.CallbackContext ctx)
    {
        if (KeybindStore.BlocksGameplayInput)
            return;
        if (GameManager.Instance != null && GameManager.Instance.IsPaused)
            return;
        Toggle();
    }

    void Update()
    {
        if (selection == null)
            selection = FindFirstObjectByType<BuildSelectionController>();

        if (panelAction == null
            && !KeybindStore.BlocksGameplayInput
            && Keyboard.current != null
            && Keyboard.current.oKey.wasPressedThisFrame)
        {
            Toggle();
        }

        int count = selection != null ? selection.SelectedBuildings.Count : 0;
        if (count != lastCount)
        {
            lastCount = count;
            if (IsOpen)
                Rebuild();
        }
    }

    void Build()
    {
        GameObject canvasGo = OverlayUi.CreateCanvas(transform, "SelectionActions", 85);

        overlay = new GameObject("Overlay", typeof(RectTransform));
        overlay.transform.SetParent(canvasGo.transform, false);
        RectTransform overlayRt = overlay.GetComponent<RectTransform>();
        overlayRt.anchorMin = Vector2.zero;
        overlayRt.anchorMax = Vector2.one;
        overlayRt.offsetMin = Vector2.zero;
        overlayRt.offsetMax = Vector2.zero;

        OverlayUi.CreateDim(overlay.transform);
        GameObject panel = OverlayUi.CreatePanel(overlay.transform);
        OverlayUi.CreateHeader(panel.transform, null, "Выделенные здания", () => SetOpen(false));
        Transform body = OverlayUi.CreateBody(panel.transform);

        summary = UiTheme.AddText(body, "Summary", "", 20f, UiTheme.TextDim);
        RectTransform summaryRt = summary.rectTransform;
        summaryRt.anchorMin = new Vector2(0f, 1f);
        summaryRt.anchorMax = new Vector2(1f, 1f);
        summaryRt.pivot = new Vector2(0.5f, 1f);
        summaryRt.anchoredPosition = Vector2.zero;
        summaryRt.sizeDelta = new Vector2(0f, 40f);

        mainView = new GameObject("Main", typeof(RectTransform));
        mainView.transform.SetParent(body, false);
        StretchBelow(mainView.GetComponent<RectTransform>(), 48f);
        list = OverlayUi.CreateScrollColumn(mainView.transform, "List");

        recipeView = new GameObject("Recipes", typeof(RectTransform));
        recipeView.transform.SetParent(body, false);
        StretchBelow(recipeView.GetComponent<RectTransform>(), 48f);
        recipeView.SetActive(false);

        recipeTitle = UiTheme.AddText(recipeView.transform, "RecipeTitle", "Рецепт", 22f, UiTheme.Accent);
        recipeTitle.fontStyle = FontStyles.Bold;
        RectTransform recTitleRt = recipeTitle.rectTransform;
        recTitleRt.anchorMin = new Vector2(0f, 1f);
        recTitleRt.anchorMax = new Vector2(0.78f, 1f);
        recTitleRt.pivot = new Vector2(0f, 1f);
        recTitleRt.anchoredPosition = Vector2.zero;
        recTitleRt.sizeDelta = new Vector2(0f, 40f);

        Button back = OverlayUi.CreateIconButton(recipeView.transform, "Back", "Назад", new Vector2(1f, 1f), new Vector2(-70f, -20f), new Vector2(140f, 40f));
        back.onClick.AddListener(ShowMain);
        var backLabel = back.GetComponentInChildren<TextMeshProUGUI>();
        if (backLabel != null)
            backLabel.fontSize = 20f;

        GameObject recBody = new GameObject("RecipeBody", typeof(RectTransform));
        recBody.transform.SetParent(recipeView.transform, false);
        StretchBelow(recBody.GetComponent<RectTransform>(), 48f);
        recipeList = OverlayUi.CreateScrollColumn(recBody.transform, "RecipeList");
    }

    public void Toggle()
    {
        SetOpen(!IsOpen);
    }

    public void SetOpen(bool open)
    {
        if (overlay != null)
            overlay.SetActive(open);
        if (!open)
            ShowMain();
        if (open && WalletHud.Instance != null && WalletHud.Instance.IsShopOpen)
            WalletHud.Instance.SetShopOpen(false);
        if (open)
            Rebuild();
        if (GameManager.Instance != null)
            GameManager.Instance.RestoreGameplayFocus();
    }

    void ShowMain()
    {
        if (mainView != null)
            mainView.SetActive(true);
        if (recipeView != null)
            recipeView.SetActive(false);
    }

    void Rebuild()
    {
        if (list == null)
            return;

        for (int i = list.childCount - 1; i >= 0; i--)
            Destroy(list.GetChild(i).gameObject);

        var groups = new Dictionary<string, List<BuildingBase>>();
        IReadOnlyList<BuildingBase> buildings = selection != null
            ? selection.SelectedBuildings
            : (IReadOnlyList<BuildingBase>)System.Array.Empty<BuildingBase>();

        for (int i = 0; i < buildings.Count; i++)
        {
            BuildingBase b = buildings[i];
            if (b == null || b.data == null)
                continue;
            if (!groups.TryGetValue(b.data.id, out List<BuildingBase> bucket))
            {
                bucket = new List<BuildingBase>();
                groups[b.data.id] = bucket;
            }

            bucket.Add(b);
        }

        if (summary != null)
            summary.text = buildings.Count == 0
                ? "Ничего не выделено. В режиме редактирования выдели здания и нажми O."
                : "Выделено " + buildings.Count + "   ·   типов " + groups.Count;

        foreach (var pair in groups)
            AddGroupCard(pair.Value);
    }

    void AddGroupCard(List<BuildingBase> bucket)
    {
        BuildingData data = bucket[0].data;
        int upgradable = 0;
        int upgradeCost = 0;
        bool crafters = false;
        int maxLevel = 1;
        for (int i = 0; i < bucket.Count; i++)
        {
            maxLevel = Mathf.Max(maxLevel, bucket[i].ReadLevel());
            if (bucket[i].CanUpgradeBuilding)
            {
                upgradable++;
                upgradeCost += Economy.UpgradeCost(bucket[i]);
            }

            if (bucket[i] is CrafterBuilding)
                crafters = true;
        }

        GameObject card = new GameObject("Group", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        card.transform.SetParent(list, false);
        UiTheme.StyleImage(card.GetComponent<Image>(), UiTheme.Card);
        OverlayUi.LayoutHeight(card.GetComponent<LayoutElement>(), crafters ? 168f : 128f);

        Image icon = OverlayUi.CreateSprite(card.transform, "Icon", data != null ? data.icon : null, new Vector2(88f, 88f));
        RectTransform iconRt = icon.rectTransform;
        iconRt.anchorMin = new Vector2(0f, 0.5f);
        iconRt.anchorMax = new Vector2(0f, 0.5f);
        iconRt.anchoredPosition = new Vector2(64f, 0f);
        iconRt.sizeDelta = new Vector2(88f, 88f);

        TextMeshProUGUI title = UiTheme.AddText(
            card.transform,
            "Title",
            (data != null ? data.displayName : "Здание") + "  ×" + bucket.Count,
            26f,
            UiTheme.Text);
        title.fontStyle = FontStyles.Bold;
        RectTransform titleRt = title.rectTransform;
        titleRt.anchorMin = new Vector2(0f, 0.62f);
        titleRt.anchorMax = new Vector2(1f, 0.95f);
        titleRt.offsetMin = new Vector2(130f, 0f);
        titleRt.offsetMax = new Vector2(-20f, 0f);

        bool canPay = upgradable > 0 && (PlayerWallet.Instance == null || PlayerWallet.Instance.CanAfford(upgradeCost));
        string sub = upgradable > 0
            ? "Можно прокачать " + upgradable + " шт  ·  ур. до 2"
            : "Макс. ур. " + maxLevel;
        TextMeshProUGUI subText = UiTheme.AddText(card.transform, "Sub", sub, 18f, UiTheme.TextDim);
        RectTransform subRt = subText.rectTransform;
        subRt.anchorMin = new Vector2(0f, 0.4f);
        subRt.anchorMax = new Vector2(1f, 0.62f);
        subRt.offsetMin = new Vector2(130f, 0f);
        subRt.offsetMax = new Vector2(-20f, 0f);

        Button upgrade = MakeCardButton(card.transform, "Upgrade", 130f, crafters ? 62f : 18f, 280f, 44f);
        SetButtonVisual(upgrade, canPay, GameHudIcons.Coin, canPay ? "Прокачать  " + upgradeCost : "Нет прокачки");
        if (canPay)
            upgrade.onClick.AddListener(() => UpgradeGroup(bucket));

        if (crafters)
        {
            Button recipe = MakeCardButton(card.transform, "Recipe", 430f, 18f, 280f, 44f);
            SetButtonVisual(recipe, true, data != null ? data.icon : null, "Сменить рецепт");
            string typeId = data != null ? data.id : "";
            recipe.onClick.AddListener(() => OpenRecipes(typeId, bucket));
        }
    }

    static Button MakeCardButton(Transform parent, string name, float x, float y, float w, float h)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0f, 0f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
        UiTheme.StyleImage(go.GetComponent<Image>(), UiTheme.Chip);
        return go.GetComponent<Button>();
    }

    static void SetButtonVisual(Button button, bool enabled, Sprite icon, string label)
    {
        button.interactable = enabled;
        Image bg = button.GetComponent<Image>();
        if (bg != null)
            bg.color = enabled ? UiTheme.Chip : UiTheme.Locked;

        if (icon != null)
        {
            Image image = OverlayUi.CreateSprite(button.transform, "Icon", icon, new Vector2(28f, 28f));
            RectTransform iconRt = image.rectTransform;
            iconRt.anchorMin = new Vector2(0f, 0.5f);
            iconRt.anchorMax = new Vector2(0f, 0.5f);
            iconRt.anchoredPosition = new Vector2(22f, 0f);
            iconRt.sizeDelta = new Vector2(28f, 28f);
        }

        TextMeshProUGUI text = UiTheme.AddText(button.transform, "Label", label, 18f, enabled ? UiTheme.Accent : UiTheme.TextDim);
        text.alignment = TextAlignmentOptions.MidlineLeft;
        RectTransform textRt = text.rectTransform;
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(icon != null ? 44f : 12f, 0f);
        textRt.offsetMax = new Vector2(-8f, 0f);
    }

    void UpgradeGroup(List<BuildingBase> bucket)
    {
        for (int i = 0; i < bucket.Count; i++)
        {
            BuildingBase b = bucket[i];
            if (b == null || !b.CanUpgradeBuilding)
                continue;
            int cost = Economy.UpgradeCost(b);
            if (PlayerWallet.Instance != null && !PlayerWallet.Instance.TrySpendCoins(cost))
                break;
            if (!b.TryUpgradeBuilding() && PlayerWallet.Instance != null)
                PlayerWallet.Instance.AddCoins(cost);
        }

        Rebuild();
    }

    void OpenRecipes(string typeId, List<BuildingBase> bucket)
    {
        recipeTypeId = typeId;
        mainView.SetActive(false);
        recipeView.SetActive(true);
        recipeTitle.text = bucket[0].data != null ? "Рецепт: " + bucket[0].data.displayName : "Рецепт";

        for (int i = recipeList.childCount - 1; i >= 0; i--)
            Destroy(recipeList.GetChild(i).gameObject);

        BuildingData data = bucket[0].data;
        RecipeData[] catalog = GameDatabase.AllRecipes();
        for (int i = 0; i < catalog.Length; i++)
        {
            RecipeData recipe = catalog[i];
            if (recipe == null || !recipe.AllowsBuilding(data))
                continue;
            if (ResearchSystem.Instance != null && !ResearchSystem.Instance.IsRecipeUnlocked(recipe))
                continue;

            RecipeData captured = recipe;
            GameObject card = UiFactory.CreateRecipeCard(recipeList, recipe, false, () => ApplyRecipe(captured));
            OverlayUi.LayoutHeight(card.GetComponent<LayoutElement>() ?? card.AddComponent<LayoutElement>(), 108f);
        }
    }

    void ApplyRecipe(RecipeData recipe)
    {
        if (selection == null || recipe == null)
            return;
        IReadOnlyList<BuildingBase> buildings = selection.SelectedBuildings;
        for (int i = 0; i < buildings.Count; i++)
        {
            CrafterBuilding crafter = buildings[i] as CrafterBuilding;
            if (crafter == null || crafter.data == null || crafter.data.id != recipeTypeId)
                continue;
            crafter.SetRecipe(recipe);
        }

        ShowMain();
        Rebuild();
    }

    static void StretchBelow(RectTransform rt, float top)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = new Vector2(0f, -top);
    }
}
