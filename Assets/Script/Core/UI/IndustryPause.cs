using UnityEngine;
using UnityEngine.UIElements;

public class IndustryPause
{
    VisualElement root;
    VisualElement home;
    VisualElement settings;
    VisualElement keys;
    Label zoomLabel;
    Label sizeLabel;
    Button hintsBtn;

    public bool Visible => root != null && root.style.display == DisplayStyle.Flex;

    public void Build(MonoBehaviour host)
    {
        if (root != null)
            return;
        VisualElement mount = IndustryUi.Mount(host, 500);
        root = IndustryUi.Screen("Pause");
        root.Add(IndustryUi.El("Dim", "dim"));
        mount.Add(root);

        home = IndustryUi.El("Home", "panel", "panel-menu");
        home.Add(IndustryUi.Text("T", "ПАУЗА", "title-hero"));
        home.Add(IndustryUi.Btn("Продолжить", () => GameManager.Instance.SetPaused(false), "btn-primary"));
        home.Add(IndustryUi.Btn("Настройки", ShowSettings));
        home.Add(IndustryUi.Btn("Сохранить", () =>
        {
            if (SaveSystem.Instance != null)
                SaveSystem.Instance.SaveGame();
        }));
        home.Add(IndustryUi.Btn("В меню", () => MainMenu.LoadMenu()));
        root.Add(home);

        settings = IndustryUi.El("Settings", "panel", "panel-menu");
        settings.Add(IndustryUi.Text("T", "НАСТРОЙКИ", "title-hero"));
        zoomLabel = IndustryUi.Text("Z", "", "muted");
        settings.Add(zoomLabel);
        var zoom = new Slider(0.06f, 1f);
        zoom.value = WorldMapUI.Instance != null ? WorldMapUI.Instance.MiniZoom : 0.18f;
        zoom.RegisterValueChangedCallback(evt =>
        {
            if (WorldMapUI.Instance != null)
                WorldMapUI.Instance.MiniZoom = evt.newValue;
            Refresh();
        });
        settings.Add(zoom);
        sizeLabel = IndustryUi.Text("S", "", "muted");
        settings.Add(sizeLabel);
        var size = new Slider(140f, 360f);
        size.value = WorldMapUI.Instance != null ? WorldMapUI.Instance.MiniSize : 220f;
        size.RegisterValueChangedCallback(evt =>
        {
            if (WorldMapUI.Instance != null)
                WorldMapUI.Instance.MiniSize = evt.newValue;
            Refresh();
        });
        settings.Add(size);
        hintsBtn = IndustryUi.Btn("Подсказки управления  ·  вкл", ToggleHints);
        settings.Add(hintsBtn);
        RefreshHintsButton();
        settings.Add(IndustryUi.Btn("Клавиши", ShowKeys));
        settings.Add(IndustryUi.Btn("Назад", ShowHome));
        root.Add(settings);

        keys = IndustryUi.El("Keys", "panel", "panel-menu");
        keys.style.width = 720;
        keys.Add(IndustryUi.Text("T", "КЛАВИШИ", "title-hero"));
        var scroll = new ScrollView();
        scroll.style.maxHeight = 380;
        keys.Add(scroll);
        var entries = KeybindStore.BuildEntries();
        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            var row = IndustryUi.El("R", "card");
            row.Add(IndustryUi.Text("L", entry.label, "body-text", "grow"));
            var key = IndustryUi.Btn(KeybindStore.Format(entry), null, "btn-small");
            key.clicked += () =>
            {
                var lab = key.Q<Label>(className: "btn-label");
                if (lab != null) lab.text = "...";
                KeybindStore.StartRebind(entry, () =>
                {
                    if (lab != null) lab.text = KeybindStore.Format(entry);
                });
            };
            row.Add(key);
            scroll.Add(row);
        }
        keys.Add(IndustryUi.Btn("Назад", ShowSettings));
        root.Add(keys);

        IndustryUi.Show(root, false);
        ShowHome();
    }

    public void SetVisible(bool on)
    {
        IndustryUi.Show(root, on);
        if (on)
            ShowHome();
    }

    public void ShowHome()
    {
        IndustryUi.Show(home, true);
        IndustryUi.Show(settings, false);
        IndustryUi.Show(keys, false);
    }

    void ShowSettings()
    {
        IndustryUi.Show(home, false);
        IndustryUi.Show(settings, true);
        IndustryUi.Show(keys, false);
        Refresh();
    }

    void ShowKeys()
    {
        IndustryUi.Show(home, false);
        IndustryUi.Show(settings, false);
        IndustryUi.Show(keys, true);
    }

    void Refresh()
    {
        if (WorldMapUI.Instance != null)
        {
            if (zoomLabel != null)
                zoomLabel.text = "Масштаб миникарты  " + Mathf.RoundToInt(WorldMapUI.Instance.MiniZoom * 100f) + "%";
            if (sizeLabel != null)
                sizeLabel.text = "Размер миникарты  " + Mathf.RoundToInt(WorldMapUI.Instance.MiniSize) + " px";
        }
        RefreshHintsButton();
    }

    void ToggleHints()
    {
        InputHintUI.HintsEnabled = !InputHintUI.HintsEnabled;
        RefreshHintsButton();
    }

    void RefreshHintsButton()
    {
        if (hintsBtn == null)
            return;
        IndustryUi.SetButtonLabel(hintsBtn, InputHintUI.HintsEnabled
            ? "Подсказки управления  ·  вкл"
            : "Подсказки управления  ·  выкл");
    }
}
