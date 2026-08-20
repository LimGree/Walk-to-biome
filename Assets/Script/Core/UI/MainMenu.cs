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
        VisualElement root = IndustryUi.Mount(this, 10);
        var bg = IndustryUi.El("Bg", "bg-menu");
        root.Add(bg);

        home = MenuPanel("Home");
        home.Add(IndustryUi.Text("Title", GameBranding.TitleCaps, "title-hero"));
        home.Add(IndustryUi.Text("Tag", GameBranding.Tagline, "tagline"));
        home.Add(IndustryUi.Btn("Миры", ShowWorlds, "btn-primary"));
        home.Add(IndustryUi.Btn("Настройки", ShowSettings));
        home.Add(IndustryUi.Btn("Выход", Quit));
        bg.Add(home);

        worlds = MenuPanel("Worlds");
        worlds.Add(IndustryUi.Text("Title", "МИРЫ", "title-hero"));
        worldList = new ScrollView();
        worldList.AddToClassList("scroll");
        worldList.style.maxHeight = 360;
        worlds.Add(worldList);
        worlds.Add(IndustryUi.Btn("Создать мир", ShowCreate, "btn-primary"));
        worlds.Add(IndustryUi.Btn("Назад", ShowHome));
        bg.Add(worlds);

        create = MenuPanel("Create");
        create.Add(IndustryUi.Text("Title", "НОВЫЙ МИР", "title-hero"));
        nameField = new TextField { value = "Новый мир" };
        nameField.AddToClassList("field");
        create.Add(nameField);
        create.Add(IndustryUi.Btn("Создать", CreateAndPlay, "btn-primary"));
        create.Add(IndustryUi.Btn("Назад", ShowWorlds));
        bg.Add(create);

        settings = MenuPanel("Settings");
        settings.Add(IndustryUi.Text("Title", "НАСТРОЙКИ", "title-hero"));
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
        settings.Add(IndustryUi.Btn("Клавиши", ShowKeys));
        settings.Add(IndustryUi.Btn("Назад", ShowHome));
        bg.Add(settings);

        keys = MenuPanel("Keys");
        keys.style.width = 720;
        keys.Add(IndustryUi.Text("Title", "КЛАВИШИ", "title-hero"));
        keys.Add(IndustryUi.Text("Hint", "Нажмите клавишу, затем новую кнопку", "muted"));
        var keyScroll = new ScrollView();
        keyScroll.style.maxHeight = 420;
        keys.Add(keyScroll);
        BuildKeyRows(keyScroll);
        keys.Add(IndustryUi.Btn("Сбросить", () =>
        {
            KeybindStore.ResetAll();
            keyScroll.Clear();
            BuildKeyRows(keyScroll);
        }));
        keys.Add(IndustryUi.Btn("Назад", ShowSettings));
        bg.Add(keys);

        ShowHome();
        RefreshLabels();
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
            nameField.value = "Мир " + (WorldCatalog.ListWorlds().Count + 1);
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
            worldList.Add(IndustryUi.Text("Empty", "Пока нет миров", "muted"));
            return;
        }

        for (int i = 0; i < list.Count; i++)
        {
            WorldInfo world = list[i];
            var card = IndustryUi.El(world.id, "card");
            var col = IndustryUi.El("Meta", "col", "grow");
            col.Add(IndustryUi.Text("Name", world.name, "body-text"));
            col.Add(IndustryUi.Text("Info", "сид " + world.seed + "  ·  " + world.lastPlayed, "muted"));
            card.Add(col);
            card.Add(IndustryUi.Btn("Играть", () => PlayWorld(world), "btn-small", "btn-primary"));
            card.Add(IndustryUi.Btn("Х", () =>
            {
                WorldCatalog.DeleteWorld(world.id);
                RefreshWorldList();
            }, "btn-small", "btn-danger"));
            worldList.Add(card);
        }
    }

    void BuildKeyRows(VisualElement parent)
    {
        List<KeybindStore.Entry> entries = KeybindStore.BuildEntries();
        for (int i = 0; i < entries.Count; i++)
        {
            KeybindStore.Entry entry = entries[i];
            var row = IndustryUi.El(entry.actionName, "card");
            row.Add(IndustryUi.Text("L", entry.label, "body-text", "grow"));
            var key = IndustryUi.Btn(KeybindStore.Format(entry), null, "btn-small");
            key.clicked += () =>
            {
                key.Q<Label>(className: "btn-label").text = "...";
                KeybindStore.StartRebind(entry, () =>
                {
                    key.Q<Label>(className: "btn-label").text = KeybindStore.Format(entry);
                });
            };
            row.Add(key);
            parent.Add(row);
        }
    }

    void RefreshLabels()
    {
        float zoom = PlayerPrefs.GetFloat("MiniMapZoom", 0.18f);
        float size = PlayerPrefs.GetFloat("MiniMapSize", 220f);
        if (zoomLabel != null)
            zoomLabel.text = "Масштаб миникарты  " + Mathf.RoundToInt(zoom * 100f) + "%";
        if (sizeLabel != null)
            sizeLabel.text = "Размер миникарты  " + Mathf.RoundToInt(size) + " px";
        if (hintsBtn != null)
            IndustryUi.SetButtonLabel(hintsBtn, InputHintUI.HintsEnabled
                ? "Подсказки управления  ·  вкл"
                : "Подсказки управления  ·  выкл");
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
