using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InputHintUI : MonoBehaviour
{
    const int MaxHints = 10;

    PlayerBuilder builder;
    PlayerInventory inventory;
    PlayerInteractor interactor;
    BuildSelectionController selection;

    readonly List<HintRow> rows = new List<HintRow>(MaxHints);
    readonly StringBuilder key = new StringBuilder(256);
    string lastKey;

    struct HintRow
    {
        public GameObject root;
        public TextMeshProUGUI keyText;
        public TextMeshProUGUI labelText;
    }

    void Start()
    {
        BindRefs();
        BuildUi();
        KeybindStore.Changed += OnBindsChanged;
    }

    void OnDestroy()
    {
        KeybindStore.Changed -= OnBindsChanged;
    }

    void OnBindsChanged()
    {
        lastKey = null;
    }

    void BindRefs()
    {
        if (builder == null)
            builder = GameManager.Instance != null
                ? GameManager.Instance.playerBuilder
                : FindFirstObjectByType<PlayerBuilder>();
        if (builder == null)
            builder = FindFirstObjectByType<PlayerBuilder>();
        if (inventory == null && builder != null)
            inventory = builder.inventory;
        if (inventory == null)
            inventory = FindFirstObjectByType<PlayerInventory>();
        if (interactor == null)
            interactor = FindFirstObjectByType<PlayerInteractor>();
        if (selection == null && builder != null)
            selection = builder.Selection;
        if (selection == null)
            selection = FindFirstObjectByType<BuildSelectionController>();
    }

    void BuildUi()
    {
        GameObject canvasGo = new GameObject("InputHintCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(transform, false);
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 80;
        canvas.pixelPerfect = false;

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject panel = new GameObject("Hints", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        panel.transform.SetParent(canvasGo.transform, false);
        RectTransform panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0f, 0f);
        panelRt.anchorMax = new Vector2(0f, 0f);
        panelRt.pivot = new Vector2(0f, 0f);
        panelRt.anchoredPosition = new Vector2(28f, 28f);
        panelRt.sizeDelta = new Vector2(420f, 0f);

        Image bg = panel.GetComponent<Image>();
        UiTheme.StyleImage(bg, new Color(UiTheme.Panel.r, UiTheme.Panel.g, UiTheme.Panel.b, 0.78f));
        bg.raycastTarget = false;

        VerticalLayoutGroup layout = panel.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(14, 16, 10, 10);
        layout.spacing = 4f;
        layout.childAlignment = TextAnchor.LowerLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fit = panel.GetComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        for (int i = 0; i < MaxHints; i++)
            rows.Add(CreateRow(panel.transform));
    }

    HintRow CreateRow(Transform parent)
    {
        GameObject row = new GameObject("Hint", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        row.transform.SetParent(parent, false);

        HorizontalLayoutGroup rowLayout = row.GetComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 10f;
        rowLayout.childAlignment = TextAnchor.MiddleLeft;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = false;

        ContentSizeFitter fit = row.GetComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Image keyBg = UiTheme.AddImage(row.transform, "Key", new Vector2(86f, 24f), UiTheme.Chip);
        keyBg.raycastTarget = false;
        LayoutElement keyLe = keyBg.gameObject.AddComponent<LayoutElement>();
        keyLe.minWidth = 78f;
        keyLe.preferredWidth = 86f;
        keyLe.minHeight = 24f;

        TextMeshProUGUI keyText = UiTheme.AddText(keyBg.transform, "KeyText", "B", 16f, UiTheme.Accent);
        keyText.alignment = TextAlignmentOptions.Center;
        keyText.fontStyle = FontStyles.Bold;
        RectTransform keyRt = keyText.rectTransform;
        keyRt.anchorMin = Vector2.zero;
        keyRt.anchorMax = Vector2.one;
        keyRt.offsetMin = Vector2.zero;
        keyRt.offsetMax = Vector2.zero;

        TextMeshProUGUI label = UiTheme.AddText(row.transform, "Label", "", 18f, UiTheme.Text);
        label.alignment = TextAlignmentOptions.MidlineLeft;
        LayoutElement labelLe = label.gameObject.AddComponent<LayoutElement>();
        labelLe.minWidth = 180f;
        labelLe.preferredWidth = 280f;

        row.SetActive(false);
        return new HintRow { root = row, keyText = keyText, labelText = label };
    }

    void LateUpdate()
    {
        BindRefs();
        Apply(Collect());
    }

    List<(string key, string label)> Collect()
    {
        var hints = new List<(string, string)>(MaxHints);

        if (GameManager.Instance != null && GameManager.Instance.IsPaused)
        {
            Add(hints, KeybindStore.Hint("Pause"), "продолжить");
            return hints;
        }

        if (InventoryUI.Instance != null && InventoryUI.Instance.IsBagOpen)
        {
            Add(hints, KeybindStore.Hint("Inventory"), "закрыть инвентарь");
            Add(hints, "ЛКМ", "в свободный слот");
            Add(hints, "перетащи", "в хотбар / обратно");
            Add(hints, "ПКМ", "убрать из хотбара");
            Add(hints, KeybindStore.Hint("Pause"), "закрыть");
            return hints;
        }

        if (WalletHud.Instance != null && WalletHud.Instance.IsShopOpen)
        {
            Add(hints, ShopHint(), "закрыть магазин");
            Add(hints, KeybindStore.Hint("Pause"), "закрыть");
            return hints;
        }

        if (SelectionActionsUI.Instance != null && SelectionActionsUI.Instance.IsOpen)
        {
            Add(hints, SelectionHint(), "закрыть выделенные");
            Add(hints, KeybindStore.Hint("Pause"), "закрыть");
            return hints;
        }

        if (MachineUI.Instance != null && MachineUI.Instance.IsOpen)
        {
            Add(hints, KeybindStore.Hint("Pause"), "закрыть");
            return hints;
        }

        if (WorldMapUI.Instance != null && WorldMapUI.Instance.IsOpen)
        {
            Add(hints, "колесо", "масштаб");
            Add(hints, "ЛКМ", "двигать карту");
            Add(hints, KeybindStore.Hint("MoveSelection"), "закрыть карту");
            Add(hints, KeybindStore.Hint("Pause"), "закрыть карту");
            return hints;
        }

        if (builder == null || !builder.isBuildMode)
        {
            Add(hints, KeybindStore.Hint("BuildMode"), "режим строительства");
            Add(hints, KeybindStore.Hint("MoveSelection"), "карта");
            Add(hints, ShopHint(), "магазин");
            Add(hints, "колесо на карте", "масштаб");
            if (interactor != null && interactor.HasInteractableTarget)
                Add(hints, KeybindStore.Hint("Interact"), interactor.InteractableHint);
            Add(hints, KeybindStore.Hint("Pause"), "пауза");
            return hints;
        }

        if (selection != null && selection.IsPasteActive)
        {
            Add(hints, KeybindStore.Hint("Place"), "подтвердить вставку");
            Add(hints, KeybindStore.Hint("Rotate"), "повернуть группу");
            Add(hints, Combo("Modifier", "Rotate"), "повернуть на месте");
            Add(hints, KeybindStore.Hint("ClearSelection"), "отмена");
            Add(hints, "взгляд в сторону", "отмена");
            return hints;
        }

        if (selection != null && selection.IsMoveActive)
        {
            Add(hints, KeybindStore.Hint("Place"), "подтвердить перенос");
            Add(hints, KeybindStore.Hint("Rotate"), "повернуть группу");
            Add(hints, Combo("Modifier", "Rotate"), "повернуть на месте");
            Add(hints, KeybindStore.Hint("ClearSelection"), "отмена");
            Add(hints, "взгляд в сторону", "отмена");
            return hints;
        }

        if (builder.IsLineStrokeActive)
        {
            Add(hints, "отпусти " + KeybindStore.Hint("Place"), "поставить линию");
            Add(hints, "взгляд в сторону", "отмена");
            return hints;
        }

        if (selection != null && selection.IsSelectionMode)
        {
            Add(hints, KeybindStore.Hint("Place"), "выделить клетки");
            if (selection.HasSelectedBuildings)
            {
                Add(hints, KeybindStore.Hint("Copy"), "копировать");
                Add(hints, KeybindStore.Hint("MoveSelection"), "переместить");
                Add(hints, KeybindStore.Hint("Rotate"), "повернуть вокруг центра");
                Add(hints, Combo("Modifier", "Rotate"), "повернуть на месте");
                Add(hints, KeybindStore.Hint("Delete"), "удалить");
                Add(hints, SelectionHint(), "настройки выделенных");
                Add(hints, KeybindStore.Hint("ClearSelection"), "сбросить выделение");
            }
            if (selection.HasClipboard)
                Add(hints, KeybindStore.Hint("Paste"), "вставить");
            Add(hints, KeybindStore.Hint("SelectMode"), "выйти из редактирования");
            Add(hints, KeybindStore.Hint("BuildMode"), "выйти из стройки");
            return hints;
        }

        Add(hints, KeybindStore.Hint("BuildMode"), "выйти из стройки");
        Add(hints, KeybindStore.Hint("Inventory"), "инвентарь зданий");
        if (builder.HasHeldBuilding)
        {
            Add(hints, KeybindStore.Hint("Place"), "установить");
            Add(hints, KeybindStore.Hint("Rotate"), "повернуть");
        }
        else
        {
            Add(hints, KeybindStore.Hint("SelectMode"), "режим редактирования");
            if (selection != null && selection.HasClipboard)
                Add(hints, KeybindStore.Hint("Paste"), "вставить");
        }

        Add(hints, KeybindStore.Hint("Demolish"), "снести");
        Add(hints, ShopHint(), "магазин");
        Add(hints, "колесо", "хотбар");
        Add(hints, KeybindStore.Hint("Pause"), "пауза");
        return hints;
    }

    static string Combo(string modifierAction, string actionName)
    {
        return KeybindStore.Hint(modifierAction) + "+" + KeybindStore.Hint(actionName);
    }

    static string ShopHint()
    {
        string hint = KeybindStore.Hint("Shop");
        return hint == "—" ? "H" : hint;
    }

    static string SelectionHint()
    {
        string hint = KeybindStore.Hint("SelectionPanel");
        return hint == "—" ? "O" : hint;
    }

    static void Add(List<(string, string)> hints, string key, string label)
    {
        if (hints.Count >= MaxHints)
            return;
        hints.Add((key, label));
    }

    void Apply(List<(string key, string label)> hints)
    {
        key.Length = 0;
        for (int i = 0; i < hints.Count; i++)
        {
            key.Append(hints[i].key);
            key.Append('|');
            key.Append(hints[i].label);
            key.Append(';');
        }

        string now = key.ToString();
        if (now == lastKey)
            return;
        lastKey = now;

        for (int i = 0; i < rows.Count; i++)
        {
            if (i >= hints.Count)
            {
                if (rows[i].root.activeSelf)
                    rows[i].root.SetActive(false);
                continue;
            }

            if (!rows[i].root.activeSelf)
                rows[i].root.SetActive(true);
            rows[i].keyText.text = hints[i].key;
            rows[i].labelText.text = hints[i].label;
        }
    }
}
