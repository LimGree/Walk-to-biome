using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class KeybindSettingsUI : MonoBehaviour
{
    readonly List<Row> rows = new List<Row>(24);
    GameObject listenOverlay;
    TextMeshProUGUI listenText;
    Coroutine delayedRebind;

    struct Row
    {
        public KeybindStore.Entry entry;
        public TextMeshProUGUI keyText;
        public Image keyBg;
        public Button bindButton;
        public Button resetButton;
    }

    public static KeybindSettingsUI Create(Transform parent, Action onBack)
    {
        GameObject panel = new GameObject("Keys", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(parent, false);
        RectTransform rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(780f, 740f);
        UiTheme.StyleImage(panel.GetComponent<Image>(), UiTheme.Panel);

        KeybindSettingsUI ui = panel.AddComponent<KeybindSettingsUI>();
        ui.Build(onBack);
        return ui;
    }

    void OnEnable()
    {
        KeybindStore.Changed += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        KeybindStore.Changed -= Refresh;
        KeybindStore.CancelListen();
        if (delayedRebind != null)
        {
            StopCoroutine(delayedRebind);
            delayedRebind = null;
        }
    }

    void OnDestroy()
    {
        KeybindStore.Changed -= Refresh;
        KeybindStore.CancelListen();
    }

    void Build(Action onBack)
    {
        TextMeshProUGUI title = UiTheme.AddText(transform, "Title", "КЛАВИШИ", 36f, UiTheme.Accent);
        title.alignment = TextAlignmentOptions.Center;
        title.fontStyle = FontStyles.Bold;
        Stretch(title.rectTransform, 0.91f, 0.99f, 16f);

        TextMeshProUGUI hint = UiTheme.AddText(transform, "Hint", "Нажмите клавишу в списке, затем новую кнопку", 16f, UiTheme.TextDim);
        hint.alignment = TextAlignmentOptions.Center;
        Stretch(hint.rectTransform, 0.86f, 0.91f, 20f);

        GameObject scrollGo = new GameObject("List", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        scrollGo.transform.SetParent(transform, false);
        RectTransform scrollRt = scrollGo.GetComponent<RectTransform>();
        scrollRt.anchorMin = new Vector2(0.05f, 0.16f);
        scrollRt.anchorMax = new Vector2(0.95f, 0.85f);
        scrollRt.offsetMin = Vector2.zero;
        scrollRt.offsetMax = Vector2.zero;
        Image scrollImg = scrollGo.GetComponent<Image>();
        scrollImg.color = new Color(1f, 1f, 1f, 0.01f);
        scrollImg.raycastTarget = true;

        GameObject content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(scrollGo.transform, false);
        RectTransform contentRt = content.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.anchoredPosition = Vector2.zero;
        contentRt.sizeDelta = new Vector2(0f, 0f);

        VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 6f;
        layout.padding = new RectOffset(4, 16, 4, 4);
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scroll = scrollGo.GetComponent<ScrollRect>();
        scroll.content = contentRt;
        scroll.viewport = scrollRt;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 48f;

        List<KeybindStore.Entry> entries = KeybindStore.BuildEntries();
        for (int i = 0; i < entries.Count; i++)
            rows.Add(CreateRow(content.transform, entries[i]));

        CreateBottomButton(transform, "Reset", "Сбросить всё", new Vector2(0.06f, 0.03f), new Vector2(0.48f, 0.13f), () =>
        {
            KeybindStore.ResetAll();
            Refresh();
        });
        CreateBottomButton(transform, "Back", "Назад", new Vector2(0.52f, 0.03f), new Vector2(0.94f, 0.13f), () =>
        {
            KeybindStore.CancelListen();
            onBack?.Invoke();
        });

        listenOverlay = new GameObject("Listen", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        listenOverlay.transform.SetParent(transform, false);
        RectTransform listenRt = listenOverlay.GetComponent<RectTransform>();
        listenRt.anchorMin = Vector2.zero;
        listenRt.anchorMax = Vector2.one;
        listenRt.offsetMin = Vector2.zero;
        listenRt.offsetMax = Vector2.zero;
        Image listenImg = listenOverlay.GetComponent<Image>();
        listenImg.color = new Color(0.03f, 0.07f, 0.05f, 0.86f);
        listenImg.raycastTarget = true;
        listenText = UiTheme.AddText(listenOverlay.transform, "Text", "Нажмите клавишу или кнопку мыши\nEsc — отмена", 26f, UiTheme.Accent);
        listenText.alignment = TextAlignmentOptions.Center;
        Stretch(listenText.rectTransform, 0.35f, 0.65f, 24f);
        listenOverlay.SetActive(false);

        Refresh();
    }

    Row CreateRow(Transform parent, KeybindStore.Entry entry)
    {
        GameObject row = new GameObject(entry.actionName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        row.transform.SetParent(parent, false);
        UiTheme.StyleImage(row.GetComponent<Image>(), UiTheme.Card);
        LayoutElement le = row.AddComponent<LayoutElement>();
        le.preferredHeight = 48f;
        le.minHeight = 48f;

        TextMeshProUGUI label = UiTheme.AddText(row.transform, "Label", entry.label, 20f, UiTheme.Text);
        RectTransform labelRt = label.rectTransform;
        labelRt.anchorMin = new Vector2(0.03f, 0.1f);
        labelRt.anchorMax = new Vector2(0.48f, 0.9f);
        labelRt.offsetMin = Vector2.zero;
        labelRt.offsetMax = Vector2.zero;

        GameObject keyGo = new GameObject("Key", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        keyGo.transform.SetParent(row.transform, false);
        RectTransform keyRt = keyGo.GetComponent<RectTransform>();
        keyRt.anchorMin = new Vector2(0.50f, 0.14f);
        keyRt.anchorMax = new Vector2(0.84f, 0.86f);
        keyRt.offsetMin = Vector2.zero;
        keyRt.offsetMax = Vector2.zero;
        Image keyBg = keyGo.GetComponent<Image>();
        UiTheme.StyleImage(keyBg, UiTheme.Chip);
        Button bindButton = keyGo.GetComponent<Button>();
        bindButton.targetGraphic = keyBg;
        TextMeshProUGUI keyText = UiTheme.AddText(keyGo.transform, "Value", "—", 18f, UiTheme.Accent);
        keyText.alignment = TextAlignmentOptions.Center;
        keyText.fontStyle = FontStyles.Bold;
        Stretch(keyText.rectTransform, 0f, 1f, 4f);

        GameObject resetGo = new GameObject("Reset", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        resetGo.transform.SetParent(row.transform, false);
        RectTransform resetRt = resetGo.GetComponent<RectTransform>();
        resetRt.anchorMin = new Vector2(0.86f, 0.14f);
        resetRt.anchorMax = new Vector2(0.97f, 0.86f);
        resetRt.offsetMin = Vector2.zero;
        resetRt.offsetMax = Vector2.zero;
        Image resetBg = resetGo.GetComponent<Image>();
        UiTheme.StyleImage(resetBg, UiTheme.Chip);
        Button resetButton = resetGo.GetComponent<Button>();
        resetButton.targetGraphic = resetBg;
        TextMeshProUGUI resetLabel = UiTheme.AddText(resetGo.transform, "Label", "↻", 22f, UiTheme.TextDim);
        resetLabel.alignment = TextAlignmentOptions.Center;
        Stretch(resetLabel.rectTransform, 0f, 1f, 0f);

        Row built = new Row
        {
            entry = entry,
            keyText = keyText,
            keyBg = keyBg,
            bindButton = bindButton,
            resetButton = resetButton
        };

        KeybindStore.Entry captured = entry;
        bindButton.onClick.AddListener(() => BeginRebind(captured));
        resetButton.onClick.AddListener(() =>
        {
            if (KeybindStore.IsListening)
                return;
            KeybindStore.ResetBinding(captured);
        });
        return built;
    }

    void BeginRebind(KeybindStore.Entry entry)
    {
        if (KeybindStore.IsListening)
            return;
        if (delayedRebind != null)
            StopCoroutine(delayedRebind);
        ShowListen(true, entry.label);
        delayedRebind = StartCoroutine(StartRebindNextFrame(entry));
    }

    IEnumerator StartRebindNextFrame(KeybindStore.Entry entry)
    {
        yield return null;
        delayedRebind = null;
        KeybindStore.StartRebind(entry, () =>
        {
            ShowListen(false, "");
            Refresh();
        });
    }

    void ShowListen(bool show, string actionLabel)
    {
        if (listenOverlay != null)
            listenOverlay.SetActive(show);
        if (show && listenText != null)
            listenText.text = actionLabel + "\nНажмите клавишу или кнопку мыши\nEsc — отмена";
    }

    public void Refresh()
    {
        Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < rows.Count; i++)
        {
            string path = KeybindStore.EffectivePath(rows[i].entry);
            if (string.IsNullOrEmpty(path))
                continue;
            counts.TryGetValue(path, out int n);
            counts[path] = n + 1;
        }

        for (int i = 0; i < rows.Count; i++)
        {
            Row row = rows[i];
            if (row.keyText == null)
                continue;
            string path = KeybindStore.EffectivePath(row.entry);
            bool conflict = !string.IsNullOrEmpty(path) && counts.TryGetValue(path, out int n) && n > 1;
            row.keyText.text = KeybindStore.Format(row.entry);
            row.keyText.color = conflict ? UiTheme.Warn : UiTheme.Accent;
            if (row.keyBg != null)
                row.keyBg.color = conflict ? new Color(UiTheme.Warn.r, UiTheme.Warn.g, UiTheme.Warn.b, 0.28f) : UiTheme.Chip;
        }
    }

    static void Stretch(RectTransform rt, float yMin, float yMax, float pad)
    {
        rt.anchorMin = new Vector2(0f, yMin);
        rt.anchorMax = new Vector2(1f, yMax);
        rt.offsetMin = new Vector2(pad, 0f);
        rt.offsetMax = new Vector2(-pad, 0f);
    }

    static void CreateBottomButton(Transform parent, string name, string label, Vector2 min, Vector2 max, Action onClick)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        Image img = go.GetComponent<Image>();
        UiTheme.StyleImage(img, UiTheme.Card);
        Button btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => onClick?.Invoke());
        TextMeshProUGUI text = UiTheme.AddText(go.transform, "Label", label, 24f, UiTheme.Text);
        text.alignment = TextAlignmentOptions.Center;
        Stretch(text.rectTransform, 0f, 1f, 6f);
    }
}
