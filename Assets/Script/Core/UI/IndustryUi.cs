using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Image = UnityEngine.UIElements.Image;

public static class IndustryUi
{
    static readonly Dictionary<int, PanelSettings> Panels = new Dictionary<int, PanelSettings>();
    static StyleSheet sheet;
    static Font font;
    static ThemeStyleSheet runtimeTheme;

    public static VisualElement Mount(MonoBehaviour host, int sortingOrder)
    {
        if (host == null)
            return null;

        string childName = "UITK_" + host.GetType().Name;
        Transform existing = host.transform.Find(childName);
        GameObject go = existing != null ? existing.gameObject : new GameObject(childName);
        go.transform.SetParent(host.transform, false);
        UIDocument doc = go.GetComponent<UIDocument>();
        if (doc == null)
            doc = go.AddComponent<UIDocument>();
        doc.panelSettings = Settings(sortingOrder);
        VisualElement root = doc.rootVisualElement;
        root.Clear();
        root.AddToClassList("root");
        root.pickingMode = PickingMode.Ignore;
        ApplyFont(root);
        root.style.color = new Color(0.91f, 0.93f, 0.92f);
        root.style.fontSize = 16;
        StyleSheet theme = Theme();
        if (theme != null && !root.styleSheets.Contains(theme))
            root.styleSheets.Add(theme);
        return root;
    }

    public static PanelSettings Settings(int sortingOrder)
    {
        if (Panels.TryGetValue(sortingOrder, out PanelSettings existing) && existing != null)
            return existing;

        PanelSettings settings = ScriptableObject.CreateInstance<PanelSettings>();
        settings.name = "IndustryPanel_" + sortingOrder;
        settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
        settings.referenceResolution = new Vector2Int(1920, 1080);
        settings.match = 0.5f;
        settings.sortingOrder = sortingOrder;
        ThemeStyleSheet tss = RuntimeTheme();
        if (tss != null)
            settings.themeStyleSheet = tss;
        Panels[sortingOrder] = settings;
        return settings;
    }

    static ThemeStyleSheet RuntimeTheme()
    {
        if (runtimeTheme != null)
            return runtimeTheme;
        runtimeTheme = Resources.Load<ThemeStyleSheet>("UI/IndustryTheme");
        if (runtimeTheme != null)
            return runtimeTheme;
        runtimeTheme = Resources.Load<ThemeStyleSheet>("UI/UnityDefaultRuntimeTheme");
        if (runtimeTheme != null)
            return runtimeTheme;
#if UNITY_EDITOR
        runtimeTheme = UnityEditor.AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(
            "Packages/com.unity.ui/PackageResources/StyleSheets/Generated/Default.tss");
        if (runtimeTheme == null)
            runtimeTheme = UnityEditor.AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(
                "Assets/UI Toolkit/UnityThemes/UnityDefaultRuntimeTheme.tss");
#endif
        return runtimeTheme;
    }

    static Font UiFont()
    {
        if (font != null)
            return font;
        font = Resources.Load<Font>("UI/LiberationSans");
        if (font == null)
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (font == null)
            font = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Arial", "Tahoma" }, 16);
        return font;
    }

    static void ApplyFont(VisualElement el)
    {
        Font face = UiFont();
        if (el == null || face == null)
            return;
        el.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromFont(face));
        el.style.unityFont = face;
    }

    static VisualTreeAsset LoadTree(string typeName)
    {
        VisualTreeAsset tree = Resources.Load<VisualTreeAsset>("UI/" + typeName);
        if (tree != null)
            return tree;
        if (typeName.EndsWith("UI"))
            tree = Resources.Load<VisualTreeAsset>("UI/" + typeName.Substring(0, typeName.Length - 2));
        if (tree != null)
            return tree;
        if (typeName == "WalletHud")
            return Resources.Load<VisualTreeAsset>("UI/Wallet");
        if (typeName == "SelectionActionsUI")
            return Resources.Load<VisualTreeAsset>("UI/Selection");
        if (typeName == "LoadingScreen")
            return Resources.Load<VisualTreeAsset>("UI/Loading");
        return null;
    }

    public static StyleSheet Theme()
    {
        if (sheet == null)
            sheet = Resources.Load<StyleSheet>("UI/Industry");
        return sheet;
    }

    public static VisualElement Screen(string name, params string[] classes)
    {
        var el = new VisualElement { name = name };
        el.AddToClassList("screen");
        AddClasses(el, classes);
        return el;
    }

    public static VisualElement El(string name, params string[] classes)
    {
        var el = new VisualElement { name = name };
        AddClasses(el, classes);
        return el;
    }

    public static Label Text(string name, string value, params string[] classes)
    {
        var label = new Label(value) { name = name };
        AddClasses(label, classes);
        ApplyFont(label);
        label.style.color = new Color(0.91f, 0.93f, 0.92f);
        return label;
    }

    public static Button Btn(string label, Action onClick, params string[] classes)
    {
        var button = new Button(() => onClick?.Invoke()) { text = "" };
        AddClasses(button, "btn");
        AddClasses(button, classes);
        ApplyFont(button);
        button.style.color = new Color(0.91f, 0.93f, 0.92f);
        button.style.fontSize = 20;
        var text = new Label(label);
        text.AddToClassList("btn-label");
        ApplyFont(text);
        text.style.color = new Color(0.91f, 0.93f, 0.92f);
        text.style.fontSize = 20;
        button.Add(text);
        return button;
    }

    public static VisualElement Icon(Sprite sprite, params string[] classes)
    {
        var image = new Image();
        AddClasses(image, classes);
        if (sprite != null)
        {
            image.sprite = sprite;
            image.scaleMode = ScaleMode.ScaleToFit;
        }
        image.style.display = sprite != null ? DisplayStyle.Flex : DisplayStyle.None;
        return image;
    }

    public static void SetIcon(Image image, Sprite sprite)
    {
        if (image == null)
            return;
        image.sprite = sprite;
        image.scaleMode = ScaleMode.ScaleToFit;
        image.style.display = sprite != null ? DisplayStyle.Flex : DisplayStyle.None;
    }

    public static VisualElement OverlayPanel(string title, Sprite icon, Action onClose)
    {
        VisualTreeAsset tree = Resources.Load<VisualTreeAsset>("UI/Overlay");
        VisualElement screen;
        if (tree != null)
        {
            var host = new VisualElement { name = "OverlayHost" };
            tree.CloneTree(host);
            screen = host.Q("Overlay") ?? host;
        }
        else
        {
            screen = Screen("Overlay");
            screen.Add(El("Dim", "dim"));
            var panel = El("Panel", "panel", "panel-wide");
            var header = El("Header", "header");
            header.Add(Icon(icon, "header-icon"));
            header.Add(Text("Title", title, "title"));
            header.Add(Btn("✕", onClose, "close"));
            panel.Add(header);
            screen.Add(panel);
        }

        Label titleLabel = screen.Q<Label>("Title");
        if (titleLabel != null)
            titleLabel.text = title ?? "";
        Image headerIcon = screen.Q<Image>("HeaderIcon");
        if (headerIcon == null)
            headerIcon = screen.Q<Image>(className: "header-icon");
        SetIcon(headerIcon, icon);
        Button close = screen.Q<Button>("Close");
        if (close != null)
            close.clicked += () => onClose?.Invoke();
        return screen;
    }

    public static VisualElement PanelOf(VisualElement overlay)
    {
        return overlay != null ? overlay.Q("Panel") : null;
    }

    public static void Show(VisualElement el, bool on)
    {
        if (el != null)
            el.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
    }

    public static void HideLegacy(Component host, params GameObject[] roots)
    {
        if (host != null)
        {
            Transform keep = host.transform;
            for (int i = 0; i < keep.childCount; i++)
            {
                Transform child = keep.GetChild(i);
                if (child == null || child.name.StartsWith("UITK_"))
                    continue;
                child.gameObject.SetActive(false);
            }
        }

        if (roots == null)
            return;
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject go = roots[i];
            if (go == null || (host != null && go == host.gameObject))
                continue;
            go.SetActive(false);
        }
    }

    public static void DisableHudCanvas(Component host)
    {
        if (host == null)
            return;
        Canvas canvas = host.GetComponent<Canvas>();
        if (canvas == null)
            canvas = host.GetComponentInParent<Canvas>();
        if (canvas == null || canvas.renderMode == RenderMode.WorldSpace)
            return;
        canvas.enabled = false;
        var ray = canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>();
        if (ray != null)
            ray.enabled = false;
    }

    public static void SetHeader(VisualElement overlay, string title, Sprite icon)
    {
        Label label = overlay != null ? overlay.Q<Label>("Title") : null;
        if (label != null)
            label.text = title ?? "";
        Image image = overlay != null ? overlay.Q<Image>(className: "header-icon") : null;
        SetIcon(image, icon);
    }

    public static void SetButtonLabel(Button button, string label)
    {
        Label text = button != null ? button.Q<Label>(className: "btn-label") : null;
        if (text != null)
            text.text = label ?? "";
    }

    public static void SetOn(VisualElement el, bool on, string className)
    {
        if (el == null || string.IsNullOrEmpty(className))
            return;
        if (on)
            el.AddToClassList(className);
        else
            el.RemoveFromClassList(className);
    }

    public static ScrollView Scroll(string name = "Scroll")
    {
        var scroll = new ScrollView { name = name };
        scroll.AddToClassList("scroll");
        scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        scroll.style.flexGrow = 1;
        scroll.style.flexShrink = 1;
        scroll.style.minHeight = 0;
        return scroll;
    }

    public static VisualElement ProgressBar(string name)
    {
        var track = El(name, "progress-track");
        track.Add(El("Fill", "progress-fill"));
        return track;
    }

    public static void SetProgress(VisualElement track, float t)
    {
        VisualElement fill = track != null ? track.Q("Fill") : null;
        if (fill != null)
            fill.style.width = Length.Percent(Mathf.Clamp01(t) * 100f);
    }

    public static Button TabBtn(string label, Action onClick)
    {
        Button button = Btn(label, onClick, "tab");
        button.RemoveFromClassList("btn");
        return button;
    }

    public static Button CardButton(Action onClick, params string[] classes)
    {
        var button = new Button(() => onClick?.Invoke()) { text = "" };
        AddClasses(button, "card");
        AddClasses(button, classes);
        ApplyFont(button);
        return button;
    }

    public static ItemData FirstItem(IList<ItemStack> stacks)
    {
        if (stacks == null)
            return null;
        for (int i = 0; i < stacks.Count; i++)
        {
            if (stacks[i] != null && stacks[i].item != null)
                return stacks[i].item;
        }
        return null;
    }

    public static VisualElement StackChip(Sprite sprite, int amount)
    {
        var chip = El("Chip", "stack-chip");
        chip.Add(Icon(sprite, "stack-icon"));
        if (amount > 0)
            chip.Add(Text("N", amount.ToString(), "stack-count"));
        return chip;
    }

    public static void AddStacks(VisualElement row, IList<ItemStack> stacks)
    {
        if (row == null)
            return;
        int added = 0;
        if (stacks != null)
        {
            for (int i = 0; i < stacks.Count; i++)
            {
                ItemStack stack = stacks[i];
                if (stack == null || stack.item == null)
                    continue;
                row.Add(StackChip(stack.item.icon, stack.amount));
                added++;
            }
        }

        if (added == 0)
            row.Add(Text("None", "—", "muted"));
    }

    public static VisualElement RecipeCard(RecipeData recipe, bool selected, Action onClick)
    {
        return RecipeCard(
            recipe != null ? recipe.displayName : "Recipe",
            recipe != null ? recipe.inputs : null,
            recipe != null ? recipe.outputs : null,
            selected,
            onClick);
    }

    public static VisualElement RecipeCard(
        string title,
        IList<ItemStack> inputs,
        IList<ItemStack> outputs,
        bool selected,
        Action onClick)
    {
        Button card = CardButton(onClick, selected ? "card-on" : null);
        ItemData first = FirstItem(outputs);
        card.Add(Icon(first != null ? first.icon : null, "card-icon"));
        var col = El("Col", "col", "grow");
        col.Add(Text("T", title ?? "Recipe", "body-text"));
        var io = El("IO", "row", "io-row");
        AddStacks(io, inputs);
        io.Add(Text("Arr", " → ", "gold"));
        AddStacks(io, outputs);
        col.Add(io);
        card.Add(col);
        return card;
    }

    public static VisualElement ActionCard(string title, string subtitle, bool enabled, Action onClick)
    {
        Button card = CardButton(enabled ? onClick : null);
        if (!enabled)
        {
            card.AddToClassList("card-locked");
            card.SetEnabled(false);
        }
        var col = El("Col", "col", "grow");
        col.Add(Text("T", title ?? "", enabled ? "gold" : "muted"));
        col.Add(Text("S", subtitle ?? "", "muted"));
        card.Add(col);
        return card;
    }

    public static VisualElement FilterCard(Sprite icon, string title, string subtitle, bool selected, Action onClick)
    {
        Button card = CardButton(onClick, selected ? "card-on" : null);
        card.Add(Icon(icon, "icon-48"));
        var col = El("Col", "col", "grow");
        col.Add(Text("T", title ?? "", selected ? "gold" : "body-text"));
        col.Add(Text("S", subtitle ?? "", "muted"));
        card.Add(col);
        return card;
    }

    public static VisualElement BuildingCard(BuildingData data, bool unlocked, string subtitle, bool marked, Action onClick)
    {
        VisualElement card = onClick != null && unlocked
            ? CardButton(onClick, "card-building")
            : El("BuildingCard", "card", "card-building");
        if (marked)
            card.AddToClassList("card-on");
        if (!unlocked)
        {
            card.AddToClassList("card-locked");
            card.SetEnabled(false);
        }

        card.Add(Icon(data != null ? data.icon : null, "card-icon"));
        string title = data != null ? data.displayName : "Building";
        int cost = Economy.BuildCost(data);
        if (cost > 0)
            title += "  ·  " + cost + "¤";
        card.Add(Text("T", title, unlocked ? "body-text" : "muted"));
        if (!unlocked)
            card.Add(Text("Lock", "LOCKED", "warn"));
        else if (!string.IsNullOrEmpty(subtitle))
            card.Add(Text("Sub", subtitle, marked ? "gold" : "muted"));
        return card;
    }

    public static VisualElement ResearchCard(ResearchNodeData node, string status, bool canStart, Action onClick)
    {
        Button card = CardButton(canStart ? onClick : null);
        if (status == "ACTIVE")
            card.AddToClassList("card-on");
        if (!canStart)
            card.SetEnabled(false);
        card.Add(Icon(node != null ? node.icon : null, "card-icon"));
        var col = El("Col", "col", "grow");
        var head = El("H", "row");
        head.Add(Text("T", node != null ? node.displayName : "Research", "body-text", "grow"));
        string badge = status == "DONE" ? "ok" : status == "ACTIVE" ? "gold" : status == "LOCKED" ? "warn" : "muted";
        head.Add(Text("S", status ?? "", badge));
        col.Add(head);
        var need = El("Need", "row", "io-row");
        need.Add(Text("L", "нужно", "muted"));
        AddStacks(need, node != null ? node.requiredItems : null);
        col.Add(need);
        var reward = El("Rew", "row", "io-row");
        reward.Add(Text("R", "даст", "muted"));
        if (node != null)
        {
            if (node.unlockedBuildings != null)
            {
                for (int i = 0; i < node.unlockedBuildings.Count; i++)
                {
                    BuildingData building = node.unlockedBuildings[i];
                    if (building != null)
                        reward.Add(StackChip(building.icon, 0));
                }
            }

            if (node.unlockedRecipes != null)
            {
                for (int i = 0; i < node.unlockedRecipes.Count; i++)
                {
                    ItemData output = FirstItem(node.unlockedRecipes[i] != null ? node.unlockedRecipes[i].outputs : null);
                    if (output != null)
                        reward.Add(StackChip(output.icon, 0));
                }
            }
        }

        col.Add(reward);
        card.Add(col);
        return card;
    }

    public static VisualElement StatRow(Sprite icon, string name, string detail)
    {
        var row = El("Stat", "card");
        row.Add(Icon(icon, "icon-48"));
        var col = El("C", "col", "grow");
        col.Add(Text("N", name ?? "", "body-text"));
        col.Add(Text("D", detail ?? "", "muted"));
        row.Add(col);
        return row;
    }

    public static VisualElement StorageCell()
    {
        var slot = El("Slot", "storage-slot");
        slot.Add(Icon(null, "card-icon"));
        slot.Add(Text("Count", "", "stack-count"));
        return slot;
    }

    public static void BindStorage(VisualElement slot, ItemStack stack)
    {
        if (slot == null)
            return;
        bool has = stack != null && !stack.IsEmpty && stack.item != null;
        SetIcon(slot.Q<Image>(), has ? stack.item.icon : null);
        Label count = slot.Q<Label>("Count");
        if (count != null)
            count.text = has ? stack.amount.ToString() : "";
    }

    static void AddClasses(VisualElement el, params string[] classes)
    {
        if (classes == null)
            return;
        for (int i = 0; i < classes.Length; i++)
        {
            if (!string.IsNullOrEmpty(classes[i]))
                el.AddToClassList(classes[i]);
        }
    }
}
