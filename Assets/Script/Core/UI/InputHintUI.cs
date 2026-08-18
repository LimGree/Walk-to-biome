using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

public class InputHintUI : MonoBehaviour
{
    const int MaxHints = 10;

    PlayerBuilder builder;
    PlayerInventory inventory;
    PlayerInteractor interactor;
    BuildSelectionController selection;

    VisualElement bar;
    readonly StringBuilder key = new StringBuilder(256);
    string lastKey;

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
        VisualElement root = IndustryUi.Mount(this, 90);
        bar = IndustryUi.El("Hints", "hint-bar");
        bar.pickingMode = PickingMode.Ignore;
        root.Add(bar);
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

        if (ResearchUI.Instance != null && ResearchUI.Instance.IsOpen)
        {
            Add(hints, KeybindStore.Hint("Research"), "закрыть исследования");
            Add(hints, KeybindStore.Hint("Pause"), "закрыть");
            return hints;
        }

        if (BuildMenuUI.Instance != null && BuildMenuUI.Instance.IsOpen)
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

        if (bar == null)
            return;
        bar.Clear();
        for (int i = 0; i < hints.Count; i++)
        {
            var chip = IndustryUi.El("H", "hint");
            chip.pickingMode = PickingMode.Ignore;
            chip.Add(IndustryUi.Text("K", hints[i].key, "hint-key"));
            chip.Add(IndustryUi.Text("L", hints[i].label, "hint-label"));
            bar.Add(chip);
        }
    }
}
