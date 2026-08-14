using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class UiFactory
{
    public static GameObject CreateRecipeCard(Transform parent, RecipeData recipe, bool selected, System.Action onClick)
    {
        GameObject card = CreateCard(parent, "RecipeCard", selected);
        Button button = card.GetComponent<Button>();
        button.onClick.AddListener(() => onClick?.Invoke());

        Image output = CreateIcon(card.transform, "Output", Vector2.zero, new Vector2(88f, 88f));
        RectTransform iconRt = output.rectTransform;
        iconRt.anchorMin = new Vector2(0f, 0.5f);
        iconRt.anchorMax = new Vector2(0f, 0.5f);
        iconRt.pivot = new Vector2(0f, 0.5f);
        iconRt.anchoredPosition = new Vector2(18f, 0f);
        ItemData outItem = FirstItem(recipe != null ? recipe.outputs : null);
        SetIcon(output, outItem != null ? outItem.icon : null);

        TextMeshProUGUI title = UiTheme.AddText(card.transform, "Title", recipe != null ? recipe.displayName : "Recipe", 24f, UiTheme.Text);
        title.fontStyle = FontStyles.Bold;
        RectTransform titleRt = title.rectTransform;
        titleRt.anchorMin = new Vector2(0f, 0.52f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.offsetMin = new Vector2(124f, 4f);
        titleRt.offsetMax = new Vector2(-18f, -10f);

        TextMeshProUGUI io = UiTheme.AddText(card.transform, "IO", FormatRecipeIO(recipe), 18f, UiTheme.TextDim);
        RectTransform ioRt = io.rectTransform;
        ioRt.anchorMin = new Vector2(0f, 0f);
        ioRt.anchorMax = new Vector2(1f, 0.52f);
        ioRt.offsetMin = new Vector2(124f, 12f);
        ioRt.offsetMax = new Vector2(-18f, -4f);

        return card;
    }

    public static GameObject CreateBuildingCard(Transform parent, BuildingData data, bool unlocked, System.Action onClick)
    {
        GameObject card = CreateCard(parent, "BuildingCard", false);
        Image bg = card.GetComponent<Image>();
        if (!unlocked)
            bg.color = UiTheme.Locked;

        Button button = card.GetComponent<Button>();
        button.interactable = unlocked;
        button.onClick.AddListener(() => onClick?.Invoke());

        Image icon = CreateIcon(card.transform, "Icon", new Vector2(0f, 18f), new Vector2(96f, 96f));
        SetIcon(icon, data != null ? data.icon : null);
        if (!unlocked)
            icon.color = new Color(1f, 1f, 1f, 0.35f);

        TextMeshProUGUI title = UiTheme.AddText(
            card.transform,
            "Title",
            data != null ? data.displayName : "Building",
            18f,
            unlocked ? UiTheme.Text : UiTheme.TextDim);
        title.alignment = TextAlignmentOptions.Center;
        RectTransform titleRt = title.rectTransform;
        titleRt.anchorMin = new Vector2(0f, 0f);
        titleRt.anchorMax = new Vector2(1f, 0f);
        titleRt.pivot = new Vector2(0.5f, 0f);
        titleRt.anchoredPosition = new Vector2(0f, 10f);
        titleRt.sizeDelta = new Vector2(-12f, 28f);

        if (!unlocked)
        {
            TextMeshProUGUI lockText = UiTheme.AddText(card.transform, "Lock", "LOCKED", 12f, UiTheme.Warn);
            lockText.alignment = TextAlignmentOptions.Center;
            RectTransform lockRt = lockText.rectTransform;
            lockRt.anchoredPosition = new Vector2(0f, 42f);
            lockRt.sizeDelta = new Vector2(100f, 18f);
        }

        return card;
    }

    public static GameObject CreateResearchCard(Transform parent, ResearchNodeData node, string status, bool canStart, System.Action onClick)
    {
        GameObject card = CreateCard(parent, "ResearchCard", status == "ACTIVE");
        Button button = card.GetComponent<Button>();
        button.interactable = canStart;
        button.onClick.AddListener(() => onClick?.Invoke());

        Image icon = CreateIcon(card.transform, "Icon", Vector2.zero, new Vector2(92f, 92f));
        RectTransform iconRt = icon.rectTransform;
        iconRt.anchorMin = new Vector2(0f, 0.5f);
        iconRt.anchorMax = new Vector2(0f, 0.5f);
        iconRt.pivot = new Vector2(0f, 0.5f);
        iconRt.anchoredPosition = new Vector2(16f, 0f);
        SetIcon(icon, node != null ? node.icon : null);

        TextMeshProUGUI title = UiTheme.AddText(card.transform, "Title", node != null ? node.displayName : "Research", 24f, UiTheme.Text);
        title.fontStyle = FontStyles.Bold;
        RectTransform titleRt = title.rectTransform;
        titleRt.anchorMin = new Vector2(0f, 0.48f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.offsetMin = new Vector2(128f, 2f);
        titleRt.offsetMax = new Vector2(-120f, -10f);

        TextMeshProUGUI cost = UiTheme.AddText(card.transform, "Cost", FormatResearchCost(node), 18f, UiTheme.TextDim);
        RectTransform costRt = cost.rectTransform;
        costRt.anchorMin = new Vector2(0f, 0f);
        costRt.anchorMax = new Vector2(1f, 0.52f);
        costRt.offsetMin = new Vector2(128f, 12f);
        costRt.offsetMax = new Vector2(-18f, -4f);

        Color badgeColor = status == "DONE" ? UiTheme.Ok : status == "ACTIVE" ? UiTheme.Accent : status == "LOCKED" ? UiTheme.Warn : UiTheme.TextDim;
        TextMeshProUGUI badge = UiTheme.AddText(card.transform, "Status", status, 16f, badgeColor);
        badge.alignment = TextAlignmentOptions.MidlineRight;
        badge.fontStyle = FontStyles.Bold;
        RectTransform badgeRt = badge.rectTransform;
        badgeRt.anchorMin = new Vector2(1f, 0.55f);
        badgeRt.anchorMax = new Vector2(1f, 1f);
        badgeRt.pivot = new Vector2(1f, 1f);
        badgeRt.anchoredPosition = new Vector2(-12f, -8f);
        badgeRt.sizeDelta = new Vector2(80f, 22f);

        return card;
    }

    public static GameObject CreateStorageSlot(Transform parent)
    {
        GameObject slot = new GameObject("StorageSlot", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        slot.transform.SetParent(parent, false);
        UiTheme.StyleImage(slot.GetComponent<Image>(), UiTheme.Card);

        Image icon = CreateIcon(slot.transform, "Icon", new Vector2(0f, 8f), new Vector2(72f, 72f));
        icon.enabled = false;

        TextMeshProUGUI count = UiTheme.AddText(slot.transform, "Count", "", 16f, UiTheme.Text);
        count.alignment = TextAlignmentOptions.BottomRight;
        count.fontStyle = FontStyles.Bold;
        RectTransform countRt = count.rectTransform;
        countRt.anchorMin = new Vector2(0f, 0f);
        countRt.anchorMax = new Vector2(1f, 0f);
        countRt.pivot = new Vector2(1f, 0f);
        countRt.offsetMin = new Vector2(6f, 4f);
        countRt.offsetMax = new Vector2(-8f, 26f);

        return slot;
    }

    public static void BindStorageSlot(GameObject slot, ItemStack stack)
    {
        if (slot == null)
            return;

        Transform iconTf = slot.transform.Find("Icon");
        Transform countTf = slot.transform.Find("Count");
        Image icon = iconTf != null ? iconTf.GetComponent<Image>() : null;
        TextMeshProUGUI count = countTf != null ? countTf.GetComponent<TextMeshProUGUI>() : null;

        bool hasItem = stack != null && !stack.IsEmpty && stack.item != null;
        if (icon != null)
        {
            icon.sprite = hasItem ? stack.item.icon : null;
            icon.enabled = hasItem && stack.item.icon != null;
            icon.color = Color.white;
        }

        if (count != null)
            count.text = hasItem ? stack.amount.ToString() : "";
    }

    public static GameObject CreateHotbarSlot(Transform parent, int index)
    {
        GameObject slot = new GameObject("Slot_" + index, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        slot.transform.SetParent(parent, false);
        UiTheme.StyleImage(slot.GetComponent<Image>(), UiTheme.Card);

        Image highlight = UiTheme.AddImage(slot.transform, "Highlight", Vector2.zero, UiTheme.AccentDim);
        highlight.gameObject.name = "Highlight";
        Stretch(highlight.rectTransform, 0f);
        highlight.enabled = false;

        Image icon = UiTheme.AddImage(slot.transform, "Icon", new Vector2(48f, 48f), Color.white);
        icon.sprite = null;
        icon.enabled = false;
        icon.preserveAspect = true;
        icon.type = Image.Type.Simple;

        TextMeshProUGUI key = UiTheme.AddText(slot.transform, "Key", (index + 1).ToString(), 12f, UiTheme.TextDim);
        key.alignment = TextAlignmentOptions.TopLeft;
        RectTransform keyRt = key.rectTransform;
        keyRt.anchorMin = new Vector2(0f, 1f);
        keyRt.anchorMax = new Vector2(0f, 1f);
        keyRt.pivot = new Vector2(0f, 1f);
        keyRt.anchoredPosition = new Vector2(6f, -4f);
        keyRt.sizeDelta = new Vector2(20f, 16f);

        return slot;
    }

    static GameObject CreateCard(Transform parent, string name, bool selected)
    {
        GameObject card = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        card.transform.SetParent(parent, false);
        Image bg = card.GetComponent<Image>();
        UiTheme.StyleImage(bg, selected ? UiTheme.AccentDim : UiTheme.Card);

        ColorBlock colors = ColorBlock.defaultColorBlock;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.7f);
        colors.colorMultiplier = 1f;
        card.GetComponent<Button>().colors = colors;
        card.GetComponent<Button>().targetGraphic = bg;
        return card;
    }

    static Image CreateIcon(Transform parent, string name, Vector2 pos, Vector2 size)
    {
        Image image = UiTheme.AddImage(parent, name, size, Color.white);
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.color = Color.white;
        RectTransform rt = image.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        return image;
    }

    static void SetIcon(Image image, Sprite sprite)
    {
        if (image == null)
            return;
        image.sprite = sprite;
        image.enabled = sprite != null;
        image.color = Color.white;
    }

    static void Stretch(RectTransform rt, float inset)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    static ItemData FirstItem(System.Collections.Generic.List<ItemStack> stacks)
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

    static string FormatRecipeIO(RecipeData recipe)
    {
        if (recipe == null)
            return "";

        string inputs = FormatStacks(recipe.inputs);
        string outputs = FormatStacks(recipe.outputs);
        return inputs + "  →  " + outputs;
    }

    static string FormatStacks(System.Collections.Generic.List<ItemStack> stacks)
    {
        if (stacks == null || stacks.Count == 0)
            return "—";

        var parts = new System.Text.StringBuilder();
        for (int i = 0; i < stacks.Count; i++)
        {
            ItemStack stack = stacks[i];
            if (stack == null || stack.item == null)
                continue;
            if (parts.Length > 0)
                parts.Append(" + ");
            parts.Append(stack.amount).Append('×').Append(stack.item.displayName);
        }

        return parts.Length > 0 ? parts.ToString() : "—";
    }

    static string FormatResearchCost(ResearchNodeData node)
    {
        if (node == null || node.requiredItems == null || node.requiredItems.Count == 0)
            return "No cost";
        return FormatStacks(node.requiredItems);
    }
}
