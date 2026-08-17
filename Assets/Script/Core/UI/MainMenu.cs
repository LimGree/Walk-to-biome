using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    public const string GameSceneName = "SampleScene";
    public const string MenuSceneName = "MainMenu";
    public const string LoadingSceneName = "Loading";

    GameObject home;
    GameObject settings;
    GameObject keys;
    GameObject worlds;
    GameObject create;
    Transform worldList;
    TMP_InputField nameField;
    TextMeshProUGUI zoomLabel;
    TextMeshProUGUI sizeLabel;

    void Awake()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
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
        GameObject canvasGo = new GameObject("MenuCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject bg = new GameObject("Bg", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        bg.transform.SetParent(canvasGo.transform, false);
        RectTransform bgRt = bg.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;
        Image bgImg = bg.GetComponent<Image>();
        bgImg.color = new Color(0.04f, 0.10f, 0.07f, 1f);
        bgImg.raycastTarget = true;

        home = CreatePanel(canvasGo.transform, "Home", new Vector2(640f, 560f));
        TextMeshProUGUI title = UiTheme.AddText(home.transform, "Title", GameBranding.TitleCaps, 40f, UiTheme.Accent);
        title.alignment = TextAlignmentOptions.Center;
        title.fontStyle = FontStyles.Bold;
        Stretch(title.rectTransform, 0.78f, 0.96f, 16f);

        TextMeshProUGUI sub = UiTheme.AddText(home.transform, "Sub", GameBranding.Tagline, 16f, UiTheme.TextDim);
        sub.alignment = TextAlignmentOptions.Center;
        sub.enableWordWrapping = true;
        Stretch(sub.rectTransform, 0.62f, 0.78f, 20f);

        CreateButton(home.transform, "Play", "Миры", new Vector2(0.56f, 0.40f), ShowWorlds);
        CreateButton(home.transform, "Settings", "Настройки", new Vector2(0.34f, 0.18f), ShowSettings);
        CreateButton(home.transform, "Quit", "Выход", new Vector2(0.12f, -0.02f), Quit);

        BuildWorlds(canvasGo.transform);
        BuildCreate(canvasGo.transform);

        settings = CreatePanel(canvasGo.transform, "Settings", new Vector2(640f, 560f));
        TextMeshProUGUI setTitle = UiTheme.AddText(settings.transform, "Title", "НАСТРОЙКИ", 40f, UiTheme.Accent);
        setTitle.alignment = TextAlignmentOptions.Center;
        setTitle.fontStyle = FontStyles.Bold;
        Stretch(setTitle.rectTransform, 0.86f, 1f, 16f);

        float zoom = PlayerPrefs.GetFloat("MiniMapZoom", 0.18f);
        float size = PlayerPrefs.GetFloat("MiniMapSize", 220f);
        zoomLabel = AddSlider(settings.transform, "Zoom", "Масштаб миникарты", 0.62f, 0.06f, 1f, zoom, v =>
        {
            PlayerPrefs.SetFloat("MiniMapZoom", v);
            RefreshLabels();
        });
        sizeLabel = AddSlider(settings.transform, "Size", "Размер миникарты", 0.42f, 140f, 360f, size, v =>
        {
            PlayerPrefs.SetFloat("MiniMapSize", v);
            RefreshLabels();
        });

        CreateButton(settings.transform, "Keys", "Клавиши", new Vector2(0.30f, 0.18f), ShowKeys);
        CreateButton(settings.transform, "Back", "Назад", new Vector2(0.14f, 0.02f), ShowHome);
        settings.SetActive(false);

        keys = KeybindSettingsUI.Create(canvasGo.transform, ShowSettings).gameObject;
        keys.SetActive(false);
        RefreshLabels();
    }

    void ShowSettings()
    {
        HideAll();
        settings.SetActive(true);
        RefreshLabels();
    }

    void ShowKeys()
    {
        HideAll();
        if (keys != null)
            keys.SetActive(true);
    }

    void ShowHome()
    {
        HideAll();
        home.SetActive(true);
    }

    void ShowWorlds()
    {
        HideAll();
        worlds.SetActive(true);
        RefreshWorldList();
    }

    void ShowCreate()
    {
        HideAll();
        create.SetActive(true);
        if (nameField != null)
            nameField.text = "Мир " + (WorldCatalog.ListWorlds().Count + 1);
    }

    void HideAll()
    {
        if (home != null) home.SetActive(false);
        if (settings != null) settings.SetActive(false);
        if (keys != null) keys.SetActive(false);
        if (worlds != null) worlds.SetActive(false);
        if (create != null) create.SetActive(false);
    }

    void PlayWorld(WorldInfo world)
    {
        WorldCatalog.SetActive(world);
        LoadGame();
    }

    void CreateAndPlay()
    {
        string name = nameField != null ? nameField.text : "";
        WorldInfo world = WorldCatalog.CreateWorld(name);
        PlayWorld(world);
    }

    void BuildWorlds(Transform canvas)
    {
        worlds = CreatePanel(canvas, "Worlds", new Vector2(720f, 620f));
        TextMeshProUGUI title = UiTheme.AddText(worlds.transform, "Title", "МИРЫ", 40f, UiTheme.Accent);
        title.alignment = TextAlignmentOptions.Center;
        title.fontStyle = FontStyles.Bold;
        Stretch(title.rectTransform, 0.88f, 1f, 16f);

        GameObject scrollGo = new GameObject("List", typeof(RectTransform), typeof(ScrollRect));
        scrollGo.transform.SetParent(worlds.transform, false);
        RectTransform scrollRt = scrollGo.GetComponent<RectTransform>();
        scrollRt.anchorMin = new Vector2(0.06f, 0.18f);
        scrollRt.anchorMax = new Vector2(0.94f, 0.86f);
        scrollRt.offsetMin = Vector2.zero;
        scrollRt.offsetMax = Vector2.zero;

        GameObject content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(scrollGo.transform, false);
        RectTransform contentRt = content.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.anchoredPosition = Vector2.zero;
        VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollGo.GetComponent<ScrollRect>().content = contentRt;
        scrollGo.GetComponent<ScrollRect>().horizontal = false;
        worldList = content.transform;

        CreateButton(worlds.transform, "New", "Создать мир", new Vector2(0.16f, 0.08f), ShowCreate);
        CreateButton(worlds.transform, "Back", "Назад", new Vector2(0.06f, -0.02f), ShowHome);
        worlds.SetActive(false);
    }

    void BuildCreate(Transform canvas)
    {
        create = CreatePanel(canvas, "Create", new Vector2(560f, 360f));
        TextMeshProUGUI title = UiTheme.AddText(create.transform, "Title", "НОВЫЙ МИР", 36f, UiTheme.Accent);
        title.alignment = TextAlignmentOptions.Center;
        title.fontStyle = FontStyles.Bold;
        Stretch(title.rectTransform, 0.72f, 0.96f, 16f);

        GameObject fieldGo = new GameObject("Name", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        fieldGo.transform.SetParent(create.transform, false);
        RectTransform fieldRt = fieldGo.GetComponent<RectTransform>();
        fieldRt.anchorMin = new Vector2(0.1f, 0.48f);
        fieldRt.anchorMax = new Vector2(0.9f, 0.64f);
        fieldRt.offsetMin = Vector2.zero;
        fieldRt.offsetMax = Vector2.zero;
        UiTheme.StyleImage(fieldGo.GetComponent<Image>(), UiTheme.Chip);

        TextMeshProUGUI fieldText = UiTheme.AddText(fieldGo.transform, "Text", "Новый мир", 24f, UiTheme.Text);
        Stretch(fieldText.rectTransform, 0f, 1f, 10f);
        nameField = fieldGo.AddComponent<TMP_InputField>();
        nameField.textComponent = fieldText;
        nameField.text = "Новый мир";

        CreateButton(create.transform, "Create", "Создать", new Vector2(0.36f, 0.18f), CreateAndPlay);
        CreateButton(create.transform, "Back", "Назад", new Vector2(0.12f, -0.02f), ShowWorlds);
        create.SetActive(false);
    }

    void RefreshWorldList()
    {
        if (worldList == null)
            return;
        foreach (Transform child in worldList)
            Destroy(child.gameObject);

        List<WorldInfo> list = WorldCatalog.ListWorlds();
        if (list.Count == 0)
        {
            TextMeshProUGUI empty = UiTheme.AddText(worldList, "Empty", "Пока нет миров", 22f, UiTheme.TextDim);
            empty.alignment = TextAlignmentOptions.Center;
            LayoutElement le = empty.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 40f;
            return;
        }

        for (int i = 0; i < list.Count; i++)
            CreateWorldRow(list[i]);
    }

    void CreateWorldRow(WorldInfo world)
    {
        GameObject row = new GameObject(world.id, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        row.transform.SetParent(worldList, false);
        UiTheme.StyleImage(row.GetComponent<Image>(), UiTheme.Card);
        LayoutElement le = row.AddComponent<LayoutElement>();
        le.preferredHeight = 72f;
        le.minHeight = 72f;

        TextMeshProUGUI name = UiTheme.AddText(row.transform, "Name", world.name, 24f, UiTheme.Text);
        RectTransform nRt = name.rectTransform;
        nRt.anchorMin = new Vector2(0.04f, 0.45f);
        nRt.anchorMax = new Vector2(0.55f, 0.92f);
        nRt.offsetMin = Vector2.zero;
        nRt.offsetMax = Vector2.zero;

        TextMeshProUGUI meta = UiTheme.AddText(row.transform, "Meta", "сид " + world.seed + "  ·  " + world.lastPlayed, 16f, UiTheme.TextDim);
        RectTransform mRt = meta.rectTransform;
        mRt.anchorMin = new Vector2(0.04f, 0.08f);
        mRt.anchorMax = new Vector2(0.55f, 0.48f);
        mRt.offsetMin = Vector2.zero;
        mRt.offsetMax = Vector2.zero;

        CreateSmallButton(row.transform, "Play", "Играть", new Vector2(0.58f, 0.18f), new Vector2(0.78f, 0.82f), () => PlayWorld(world));
        CreateSmallButton(row.transform, "Del", "Х", new Vector2(0.81f, 0.18f), new Vector2(0.96f, 0.82f), () =>
        {
            WorldCatalog.DeleteWorld(world.id);
            RefreshWorldList();
        });
    }

    static void CreateSmallButton(Transform parent, string name, string label, Vector2 min, Vector2 max, UnityEngine.Events.UnityAction onClick)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        Image img = go.GetComponent<Image>();
        UiTheme.StyleImage(img, UiTheme.Chip);
        Button btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);
        TextMeshProUGUI text = UiTheme.AddText(go.transform, "Label", label, 18f, UiTheme.Text);
        text.alignment = TextAlignmentOptions.Center;
        Stretch(text.rectTransform, 0f, 1f, 2f);
    }

    void RefreshLabels()
    {
        float zoom = PlayerPrefs.GetFloat("MiniMapZoom", 0.18f);
        float size = PlayerPrefs.GetFloat("MiniMapSize", 220f);
        if (zoomLabel != null)
            zoomLabel.text = "Масштаб миникарты  " + Mathf.RoundToInt(zoom * 100f) + "%";
        if (sizeLabel != null)
            sizeLabel.text = "Размер миникарты  " + Mathf.RoundToInt(size) + " px";
    }

    static void Quit()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    static GameObject CreatePanel(Transform parent, string name, Vector2 size)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(parent, false);
        RectTransform rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        UiTheme.StyleImage(panel.GetComponent<Image>(), UiTheme.Panel);
        return panel;
    }

    static void Stretch(RectTransform rt, float yMin, float yMax, float pad)
    {
        rt.anchorMin = new Vector2(0f, yMin);
        rt.anchorMax = new Vector2(1f, yMax);
        rt.offsetMin = new Vector2(pad, 0f);
        rt.offsetMax = new Vector2(-pad, 0f);
    }

    static void CreateButton(Transform parent, string name, string label, Vector2 yRange, UnityEngine.Events.UnityAction onClick)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.16f, yRange.y);
        rt.anchorMax = new Vector2(0.84f, yRange.x);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        Image img = go.GetComponent<Image>();
        UiTheme.StyleImage(img, UiTheme.Card);
        Button btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);
        TextMeshProUGUI text = UiTheme.AddText(go.transform, "Label", label, 28f, UiTheme.Text);
        text.alignment = TextAlignmentOptions.Center;
        Stretch(text.rectTransform, 0f, 1f, 8f);
    }

    TextMeshProUGUI AddSlider(Transform parent, string name, string title, float y, float min, float max, float value, UnityEngine.Events.UnityAction<float> onChange)
    {
        GameObject row = new GameObject(name, typeof(RectTransform));
        row.transform.SetParent(parent, false);
        RectTransform rowRt = row.GetComponent<RectTransform>();
        rowRt.anchorMin = new Vector2(0.08f, y);
        rowRt.anchorMax = new Vector2(0.92f, y + 0.16f);
        rowRt.offsetMin = Vector2.zero;
        rowRt.offsetMax = Vector2.zero;

        TextMeshProUGUI label = UiTheme.AddText(row.transform, "Label", title, 22f, UiTheme.Text);
        Stretch(label.rectTransform, 0.55f, 1f, 0f);

        GameObject sliderGo = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
        sliderGo.transform.SetParent(row.transform, false);
        RectTransform sRt = sliderGo.GetComponent<RectTransform>();
        sRt.anchorMin = new Vector2(0f, 0.05f);
        sRt.anchorMax = new Vector2(1f, 0.5f);
        sRt.offsetMin = Vector2.zero;
        sRt.offsetMax = Vector2.zero;

        Image bg = UiTheme.AddImage(sliderGo.transform, "Bg", Vector2.zero, UiTheme.Chip);
        Stretch(bg.rectTransform, 0.35f, 0.65f, 0f);
        bg.raycastTarget = true;
        Image fill = UiTheme.AddImage(sliderGo.transform, "Fill", Vector2.zero, UiTheme.Accent);
        Stretch(fill.rectTransform, 0.35f, 0.65f, 0f);
        fill.raycastTarget = false;
        Image handle = UiTheme.AddImage(sliderGo.transform, "Handle", new Vector2(18f, 18f), UiTheme.Primary);
        handle.rectTransform.sizeDelta = new Vector2(18f, 22f);
        handle.raycastTarget = true;

        Slider slider = sliderGo.GetComponent<Slider>();
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = value;
        slider.targetGraphic = handle;
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.onValueChanged.AddListener(onChange);
        return label;
    }
}
