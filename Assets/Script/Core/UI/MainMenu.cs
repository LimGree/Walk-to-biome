using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class MainMenu : MonoBehaviour
{
    public const string GameSceneName = "SampleScene";
    public const string MenuSceneName = "MainMenu";
    public const string LoadingSceneName = "Loading";

    VisualElement home;
    VisualElement settings;
    VisualElement keys;
    VisualElement worlds;
    VisualElement create;
    ScrollView worldList;
    TextField nameField;
    Label zoomLabel;
    Label sizeLabel;
    Slider zoomSlider;
    Slider sizeSlider;
    Button hintsBtn;

    void Awake()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        UnityEngine.Cursor.lockState = CursorLockMode.None;
        UnityEngine.Cursor.visible = true;
        BuildUi();
        UiLocale.Changed += BuildUi;
    }

    void OnDestroy()
    {
        UiLocale.Changed -= BuildUi;
    }

    public static void LoadGame()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene(LoadingSceneName);
    }

    public static void LoadMenu()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        if (GameManager.Instance != null)
            GameManager.Instance.PrepareLeaveGameplay();
        SceneManager.LoadScene(MenuSceneName);
    }

    void BuildUi()
    {
        string page = CurrentPage();
        VisualElement root = IndustryUi.Mount(this, 10);
        var bg = IndustryUi.El("Bg", "bg-menu");
        root.Add(bg);

        home = MenuPanel("Home");
        home.Add(IndustryUi.Text("Title", "WALK", "display"));
        home.Add(IndustryUi.Text("Title2", "OF INDUSTRY", "title-hero"));
        home.Add(IndustryUi.Text("Tag", GameBranding.Tagline, "tagline"));
        home.Add(IndustryUi.Btn(UiLocale.T("menu.continue"), ContinueLatest, "btn-primary"));
        home.Add(IndustryUi.Btn(UiLocale.T("menu.worlds"), ShowWorlds));
        home.Add(IndustryUi.Btn(UiLocale.T("menu.settings"), ShowSettings));
        home.Add(IndustryUi.Btn(UiLocale.T("menu.exit"), Quit, "btn-ghost"));
        bg.Add(home);

        worlds = MenuPanel("Worlds");
        worlds.style.width = 560;
        worlds.Add(IndustryUi.Text("Title", UiLocale.T("menu.worlds"), "title-hero"));
        worldList = new ScrollView();
        worldList.AddToClassList("scroll");
        worldList.style.maxHeight = 420;
        worlds.Add(worldList);
        worlds.Add(IndustryUi.Btn(UiLocale.T("menu.new_world"), ShowCreate, "btn-primary"));
        worlds.Add(IndustryUi.Btn(UiLocale.T("menu.back"), ShowHome, "btn-ghost"));
        bg.Add(worlds);

        create = MenuPanel("Create");
        create.Add(IndustryUi.Text("Title", UiLocale.T("menu.new_world"), "title-hero"));
        nameField = new TextField { value = UiLocale.T("menu.default_world", WorldCatalog.ListWorlds().Count + 1) };
        nameField.AddToClassList("field");
        nameField.label = "";
        nameField.style.unityTextAlign = TextAnchor.MiddleLeft;
        create.Add(nameField);
        create.Add(IndustryUi.Btn(UiLocale.T("menu.create"), CreateAndPlay, "btn-primary"));
        create.Add(IndustryUi.Btn(UiLocale.T("menu.back"), ShowWorlds, "btn-ghost"));
        bg.Add(create);

        settings = MenuPanel("Settings");
        settings.Add(IndustryUi.Text("Title", UiLocale.T("settings.title"), "title-hero"));
        settings.Add(IndustryUi.Text("G0", UiLocale.T("settings.general"), "settings-group"));
        settings.Add(UiLocale.LanguageRow());
        settings.Add(IndustryUi.Text("G1", UiLocale.T("settings.gameplay"), "settings-group"));
        zoomLabel = IndustryUi.Text("ZoomL", "", "muted");
        settings.Add(zoomLabel);
        zoomSlider = new Slider(0.06f, 1f) { value = PlayerPrefs.GetFloat("MiniMapZoom", 0.18f) };
        zoomSlider.RegisterValueChangedCallback(evt =>
        {
            PlayerPrefs.SetFloat("MiniMapZoom", evt.newValue);
            RefreshLabels();
        });
        settings.Add(zoomSlider);
        sizeLabel = IndustryUi.Text("SizeL", "", "muted");
        settings.Add(sizeLabel);
        sizeSlider = new Slider(140f, 360f) { value = PlayerPrefs.GetFloat("MiniMapSize", 220f) };
        sizeSlider.RegisterValueChangedCallback(evt =>
        {
            PlayerPrefs.SetFloat("MiniMapSize", evt.newValue);
            RefreshLabels();
        });
        settings.Add(sizeSlider);
        hintsBtn = IndustryUi.Btn("", ToggleHints);
        settings.Add(hintsBtn);
        settings.Add(IndustryUi.Text("G2", UiLocale.T("settings.controls"), "settings-group"));
        settings.Add(IndustryUi.Btn(UiLocale.T("settings.keybinds"), ShowKeys));
        settings.Add(IndustryUi.Btn(UiLocale.T("menu.back"), ShowHome, "btn-ghost"));
        bg.Add(settings);

        keys = MenuPanel("Keys");
        keys.style.width = 720;
        KeybindSettingsUI.Fill(keys, ShowSettings);
        bg.Add(keys);

        if (page == "settings")
            ShowSettings();
        else if (page == "worlds")
            ShowWorlds();
        else if (page == "create")
            ShowCreate();
        else if (page == "keys")
            ShowKeys();
        else
            ShowHome();
        RefreshLabels();
    }

    string CurrentPage()
    {
        if (settings != null && settings.resolvedStyle.display == DisplayStyle.Flex)
            return "settings";
        if (worlds != null && worlds.resolvedStyle.display == DisplayStyle.Flex)
            return "worlds";
        if (create != null && create.resolvedStyle.display == DisplayStyle.Flex)
            return "create";
        if (keys != null && keys.resolvedStyle.display == DisplayStyle.Flex)
            return "keys";
        return "home";
    }

    VisualElement MenuPanel(string name)
    {
        var panel = IndustryUi.El(name, "panel", "panel-menu");
        IndustryUi.Show(panel, false);
        return panel;
    }

    void ShowSettings()
    {
        HideAll();
        IndustryUi.Show(settings, true);
        RefreshLabels();
    }

    void ShowKeys()
    {
        HideAll();
        IndustryUi.Show(keys, true);
    }

    void ShowHome()
    {
        HideAll();
        IndustryUi.Show(home, true);
    }

    void ShowWorlds()
    {
        HideAll();
        IndustryUi.Show(worlds, true);
        RefreshWorldList();
    }

    void ShowCreate()
    {
        HideAll();
        IndustryUi.Show(create, true);
        if (nameField != null)
            nameField.value = UiLocale.T("menu.default_world", WorldCatalog.ListWorlds().Count + 1);
    }

    void HideAll()
    {
        IndustryUi.Show(home, false);
        IndustryUi.Show(settings, false);
        IndustryUi.Show(keys, false);
        IndustryUi.Show(worlds, false);
        IndustryUi.Show(create, false);
    }

    void PlayWorld(WorldInfo world)
    {
        WorldCatalog.SetActive(world);
        LoadGame();
    }

    void CreateAndPlay()
    {
        WorldInfo world = WorldCatalog.CreateWorld(nameField != null ? nameField.value : "");
        PlayWorld(world);
    }

    void RefreshWorldList()
    {
        if (worldList == null)
            return;
        worldList.Clear();
        List<WorldInfo> list = WorldCatalog.ListWorlds();
        if (list.Count == 0)
        {
            worldList.Add(IndustryUi.Text("Empty", UiLocale.T("menu.empty_worlds"), "muted"));
            return;
        }

        for (int i = 0; i < list.Count; i++)
        {
            WorldInfo world = list[i];
            var card = IndustryUi.El(world.id, "card", "world-card");
            card.Add(IndustryUi.Text("Idx", UiLocale.T("menu.world_n", (i + 1).ToString("00")), "label-caps"));
            card.Add(IndustryUi.Text("Name", world.name, "heading-3"));
            card.Add(IndustryUi.Text("Played", UiLocale.T("menu.last_played", world.lastPlayed), "caption"));
            card.Add(IndustryUi.Text("Seed", UiLocale.T("menu.seed", world.seed), "caption"));
            var actions = IndustryUi.El("A", "row");
            actions.Add(IndustryUi.Btn(UiLocale.T("menu.play"), () => PlayWorld(world), "btn-small", "btn-primary"));
            WorldInfo captured = world;
            actions.Add(IndustryUi.Btn(UiLocale.T("menu.delete"), () =>
            {
                UiModal.Confirm(
                    UiLocale.T("menu.delete_title"),
                    UiLocale.T("menu.delete_body", captured.name),
                    UiLocale.T("menu.delete"),
                    () =>
                    {
                        WorldCatalog.DeleteWorld(captured.id);
                        RefreshWorldList();
                    });
            }, "btn-small", "btn-danger"));
            card.Add(actions);
            worldList.Add(card);
        }
    }

    void ContinueLatest()
    {
        List<WorldInfo> list = WorldCatalog.ListWorlds();
        if (list.Count == 0)
        {
            ShowCreate();
            return;
        }
        PlayWorld(list[0]);
    }

    void RefreshLabels()
    {
        float zoom = PlayerPrefs.GetFloat("MiniMapZoom", 0.18f);
        float size = PlayerPrefs.GetFloat("MiniMapSize", 220f);
        if (zoomLabel != null)
            zoomLabel.text = UiLocale.T("settings.minimap_zoom", Mathf.RoundToInt(zoom * 100f));
        if (sizeLabel != null)
            sizeLabel.text = UiLocale.T("settings.minimap_size", Mathf.RoundToInt(size));
        if (hintsBtn != null)
            IndustryUi.SetButtonLabel(hintsBtn, InputHintUI.HintsEnabled
                ? UiLocale.T("settings.hints_on")
                : UiLocale.T("settings.hints_off"));
    }

    void ToggleHints()
    {
        InputHintUI.HintsEnabled = !InputHintUI.HintsEnabled;
        RefreshLabels();
    }

    static void Quit()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
