using UnityEngine;
using UnityEngine.UIElements;

public class IndustryPause
{
    MonoBehaviour host;
    VisualElement root;
    VisualElement home;
    VisualElement settings;
    VisualElement keys;
    Label zoomLabel;
    Label sizeLabel;
    Button hintsBtn;
    bool listening;

    public bool Visible => root != null && root.style.display == DisplayStyle.Flex;

    public void Build(MonoBehaviour owner)
    {
        host = owner;
        if (!listening)
        {
            UiLocale.Changed += Relocalize;
            listening = true;
        }
        if (root != null)
            return;
        VisualElement mount = IndustryUi.Mount(host, 500);
        root = IndustryUi.Screen("Pause");
        root.Add(IndustryUi.El("Dim", "dim"));
        mount.Add(root);

        home = IndustryUi.El("Home", "panel", "panel-menu");
        home.Add(IndustryUi.Text("T", UiLocale.T("pause.title"), "title-hero"));
        home.Add(IndustryUi.Btn(UiLocale.T("pause.resume"), () => GameManager.Instance.SetPaused(false), "btn-primary"));
        home.Add(IndustryUi.Btn(UiLocale.T("pause.save"), () =>
        {
            if (SaveSystem.Instance != null)
                SaveSystem.Instance.SaveGame();
            UiNotification.Push(UiLocale.T("pause.saved"), "", UiStatus.Completed);
        }));
        home.Add(IndustryUi.El("Div1", "divider"));
        home.Add(IndustryUi.Btn(UiLocale.T("menu.settings"), ShowSettings));
        home.Add(IndustryUi.Btn(UiLocale.T("pause.controls"), ShowKeys));
        home.Add(IndustryUi.El("Div2", "divider"));
        home.Add(IndustryUi.Btn(UiLocale.T("pause.exit"), () =>
        {
            UiModal.Confirm(
                UiLocale.T("pause.exit_title"),
                UiLocale.T("pause.exit_body"),
                UiLocale.T("pause.exit"),
                () => MainMenu.LoadMenu());
        }, "btn-danger"));
        home.Add(IndustryUi.Text("Esc", UiLocale.T("pause.esc"), "esc-hint"));
        root.Add(home);

        settings = IndustryUi.El("Settings", "panel", "panel-menu");
        settings.Add(IndustryUi.Text("T", UiLocale.T("settings.title"), "title-hero"));
        settings.Add(IndustryUi.Text("G0", UiLocale.T("settings.general"), "settings-group"));
        settings.Add(UiLocale.LanguageRow());
        settings.Add(IndustryUi.Text("G", UiLocale.T("settings.gameplay"), "settings-group"));
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
        settings.Add(IndustryUi.Text("G2", UiLocale.T("settings.controls"), "settings-group"));
        settings.Add(IndustryUi.Btn(UiLocale.T("settings.keybinds"), ShowKeys));
        settings.Add(IndustryUi.Btn(UiLocale.T("menu.back"), ShowHome, "btn-ghost"));
        root.Add(settings);

        keys = IndustryUi.El("Keys", "panel", "panel-menu");
        keys.style.width = 720;
        KeybindSettingsUI.Fill(keys, ShowSettings);
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
                zoomLabel.text = UiLocale.T("settings.minimap_zoom", Mathf.RoundToInt(WorldMapUI.Instance.MiniZoom * 100f));
            if (sizeLabel != null)
                sizeLabel.text = UiLocale.T("settings.minimap_size", Mathf.RoundToInt(WorldMapUI.Instance.MiniSize));
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
            ? UiLocale.T("settings.hints_on")
            : UiLocale.T("settings.hints_off"));
    }

    void Relocalize()
    {
        if (host == null)
            return;
        bool vis = Visible;
        bool onSettings = settings != null && settings.resolvedStyle.display == DisplayStyle.Flex;
        bool onKeys = keys != null && keys.resolvedStyle.display == DisplayStyle.Flex;
        root = null;
        home = null;
        settings = null;
        keys = null;
        Build(host);
        SetVisible(vis);
        if (!vis)
            return;
        if (onKeys)
            ShowKeys();
        else if (onSettings)
            ShowSettings();
        else
            ShowHome();
    }
}
