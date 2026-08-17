using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SelectionActionsUI : MonoBehaviour
{
    BuildSelectionController selection;
    GameObject root;
    GameObject panel;
    Transform list;
    TextMeshProUGUI summary;
    GameObject recipePanel;
    Transform recipeList;
    string recipeTypeId;
    int lastCount = -1;

    void Start()
    {
        selection = FindFirstObjectByType<BuildSelectionController>();
        Build();
        SetOpen(false);
    }

    void Update()
    {
        if (selection == null)
            selection = FindFirstObjectByType<BuildSelectionController>();
        int count = selection != null ? selection.SelectedBuildings.Count : 0;
        if (count != lastCount)
        {
            lastCount = count;
            if (count == 0)
                SetOpen(false);
            RefreshOpenButton();
            if (panel != null && panel.activeSelf)
                Rebuild();
        }
    }

    void Build()
    {
        GameObject canvasGo = new GameObject("SelectionActions", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 70;
        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        root = canvasGo;

        Button open = CreateButton(canvasGo.transform, "Open", "Выделенные", new Vector2(1f, 1f), new Vector2(-24f, -18f), new Vector2(220f, 52f), new Vector2(1f, 1f));
        open.onClick.AddListener(Toggle);

        panel = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(canvasGo.transform, false);
        UiTheme.StylePanel(panel, new Vector2(640f, 720f));
        RectTransform panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(1f, 0.5f);
        panelRt.anchorMax = new Vector2(1f, 0.5f);
        panelRt.pivot = new Vector2(1f, 0.5f);
        panelRt.anchoredPosition = new Vector2(-24f, -20f);

        TextMeshProUGUI title = UiTheme.AddText(panel.transform, "Title", "Выделенные здания", 26f, UiTheme.Accent);
        title.fontStyle = FontStyles.Bold;
        Stretch(title.rectTransform, 0.06f, 0.78f, 0.9f, 0.97f);

        Button close = CreateButton(panel.transform, "Close", "✕", new Vector2(1f, 1f), new Vector2(-16f, -16f), new Vector2(44f, 44f), new Vector2(1f, 1f));
        close.onClick.AddListener(() => SetOpen(false));

        summary = UiTheme.AddText(panel.transform, "Summary", "", 16f, UiTheme.TextDim);
        Stretch(summary.rectTransform, 0.06f, 0.94f, 0.84f, 0.9f);

        GameObject scroll = new GameObject("List", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        scroll.transform.SetParent(panel.transform, false);
        list = scroll.transform;
        RectTransform listRt = scroll.GetComponent<RectTransform>();
        listRt.anchorMin = new Vector2(0.05f, 0.06f);
        listRt.anchorMax = new Vector2(0.95f, 0.83f);
        listRt.offsetMin = Vector2.zero;
        listRt.offsetMax = Vector2.zero;
        VerticalLayoutGroup layout = scroll.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        layout.childAlignment = TextAnchor.UpperCenter;
        scroll.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        UiTheme.EnsureVerticalScroll(listRt);

        recipePanel = new GameObject("Recipes", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        recipePanel.transform.SetParent(canvasGo.transform, false);
        UiTheme.StylePanel(recipePanel, new Vector2(520f, 620f));
        RectTransform recRt = recipePanel.GetComponent<RectTransform>();
        recRt.anchoredPosition = new Vector2(-200f, 0f);
        recipePanel.SetActive(false);

        TextMeshProUGUI recTitle = UiTheme.AddText(recipePanel.transform, "Title", "Рецепт для типа", 22f, UiTheme.Accent);
        Stretch(recTitle.rectTransform, 0.06f, 0.8f, 0.9f, 0.97f);
        Button recClose = CreateButton(recipePanel.transform, "Close", "✕", new Vector2(1f, 1f), new Vector2(-16f, -16f), new Vector2(44f, 44f), new Vector2(1f, 1f));
        recClose.onClick.AddListener(() => recipePanel.SetActive(false));

        GameObject recListGo = new GameObject("RecipeList", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        recListGo.transform.SetParent(recipePanel.transform, false);
        recipeList = recListGo.transform;
        RectTransform recListRt = recListGo.GetComponent<RectTransform>();
        recListRt.anchorMin = new Vector2(0.05f, 0.06f);
        recListRt.anchorMax = new Vector2(0.95f, 0.88f);
        recListRt.offsetMin = Vector2.zero;
        recListRt.offsetMax = Vector2.zero;
        VerticalLayoutGroup recLayout = recListGo.GetComponent<VerticalLayoutGroup>();
        recLayout.spacing = 6f;
        recLayout.childForceExpandHeight = false;
        recLayout.childForceExpandWidth = true;
        recListGo.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        UiTheme.EnsureVerticalScroll(recListRt);
    }

    void Toggle()
    {
        if (selection == null || selection.SelectedBuildings.Count == 0)
            return;
        SetOpen(panel == null || !panel.activeSelf);
    }

    void SetOpen(bool open)
    {
        if (panel != null)
            panel.SetActive(open);
        if (!open && recipePanel != null)
            recipePanel.SetActive(false);
        if (open)
            Rebuild();
    }

    void RefreshOpenButton()
    {
        Transform btn = root != null ? root.transform.Find("Open") : null;
        if (btn != null)
            btn.gameObject.SetActive(selection != null && selection.SelectedBuildings.Count > 0);
    }

    void Rebuild()
    {
        if (list == null || selection == null)
            return;

        for (int i = list.childCount - 1; i >= 0; i--)
            Destroy(list.GetChild(i).gameObject);

        var groups = new Dictionary<string, List<BuildingBase>>();
        IReadOnlyList<BuildingBase> buildings = selection.SelectedBuildings;
        for (int i = 0; i < buildings.Count; i++)
        {
            BuildingBase b = buildings[i];
            if (b == null || b.data == null)
                continue;
            string key = b.data.id;
            if (!groups.TryGetValue(key, out List<BuildingBase> bucket))
            {
                bucket = new List<BuildingBase>();
                groups[key] = bucket;
            }

            bucket.Add(b);
        }

        if (summary != null)
            summary.text = "Зданий: " + buildings.Count + "   ·   типов: " + groups.Count;

        foreach (var pair in groups)
        {
            List<BuildingBase> bucket = pair.Value;
            BuildingData data = bucket[0].data;
            int upgradable = 0;
            int upgradeCost = 0;
            bool crafters = false;
            for (int i = 0; i < bucket.Count; i++)
            {
                if (bucket[i].CanUpgradeBuilding)
                {
                    upgradable++;
                    upgradeCost += Economy.UpgradeCost(bucket[i]);
                }

                if (bucket[i] is CrafterBuilding)
                    crafters = true;
            }

            string title = (data != null ? data.displayName : pair.Key) + "  ×" + bucket.Count;
            string sub = upgradable > 0
                ? "Прокачка " + upgradable + " шт  ·  " + upgradeCost + " монет"
                : "Прокачка недоступна";
            bool canPay = PlayerWallet.Instance == null || PlayerWallet.Instance.CanAfford(upgradeCost);
            UiFactory.CreateActionButton(list, title, sub, upgradable > 0 && canPay, () => UpgradeGroup(bucket));

            if (crafters)
            {
                string typeId = pair.Key;
                UiFactory.CreateActionButton(list, "Сменить рецепт — " + (data != null ? data.displayName : typeId), "для всех выделенных этого типа", true, () => OpenRecipes(typeId, bucket));
            }
        }
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
        recipePanel.SetActive(true);
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
            UiFactory.CreateRecipeCard(recipeList, recipe, false, () => ApplyRecipe(captured));
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

        recipePanel.SetActive(false);
        Rebuild();
    }

    static Button CreateButton(Transform parent, string name, string label, Vector2 anchor, Vector2 pos, Vector2 size, Vector2 pivot)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        UiTheme.StyleImage(go.GetComponent<Image>(), UiTheme.Card);
        TextMeshProUGUI text = UiTheme.AddText(go.transform, "Label", label, 18f, UiTheme.Text);
        text.alignment = TextAlignmentOptions.Center;
        Stretch(text.rectTransform, 0f, 1f, 0f, 1f);
        return go.GetComponent<Button>();
    }

    static void Stretch(RectTransform rt, float xMin, float xMax, float yMin, float yMax)
    {
        rt.anchorMin = new Vector2(xMin, yMin);
        rt.anchorMax = new Vector2(xMax, yMax);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
