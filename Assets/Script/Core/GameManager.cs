using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("References")]
    public PlayerInventory playerInventory;
    public PlayerBuilder playerBuilder;
    public ResearchSystem researchSystem;

    [Header("Game State")]
    public bool isPaused = false;

    public bool IsPaused => isPaused;

    InputSystem_Actions inputActions;
    GameObject pauseRoot;
    GameObject pauseHome;
    GameObject pauseSettings;
    GameObject pauseKeys;
    TextMeshProUGUI miniZoomLabel;
    TextMeshProUGUI miniSizeLabel;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        inputActions = KeybindStore.Shared;
        if (GetComponent<InputHintUI>() == null)
            gameObject.AddComponent<InputHintUI>();
        if (GetComponent<WorldMapUI>() == null)
            gameObject.AddComponent<WorldMapUI>();
        if (GetComponent<PlayerWallet>() == null)
            gameObject.AddComponent<PlayerWallet>();
        if (GetComponent<ProductionStats>() == null)
            gameObject.AddComponent<ProductionStats>();
        if (GetComponent<BeltSpeedSystem>() == null)
            gameObject.AddComponent<BeltSpeedSystem>();
        if (GetComponent<WalletHud>() == null)
            gameObject.AddComponent<WalletHud>();
        if (GetComponent<SelectionActionsUI>() == null)
            gameObject.AddComponent<SelectionActionsUI>();
    }

    void OnEnable()
    {
        inputActions.Player.Pause.performed += OnPausePerformed;
    }

    void OnDisable()
    {
        if (inputActions != null)
            inputActions.Player.Pause.performed -= OnPausePerformed;
    }

    void OnPausePerformed(InputAction.CallbackContext context)
    {
        if (KeybindStore.BlocksGameplayInput)
            return;

        if (InventoryUI.Instance != null && InventoryUI.Instance.IsBagOpen)
        {
            InventoryUI.Instance.SetBagOpen(false);
            return;
        }

        if (WalletHud.Instance != null && WalletHud.Instance.IsShopOpen)
        {
            WalletHud.Instance.SetShopOpen(false);
            return;
        }

        if (SelectionActionsUI.Instance != null && SelectionActionsUI.Instance.IsOpen)
        {
            SelectionActionsUI.Instance.Toggle();
            return;
        }

        if (MachineUI.Instance != null && MachineUI.Instance.IsOpen)
        {
            MachineUI.Instance.Close();
            return;
        }

        if (WorldMapUI.Instance != null && WorldMapUI.Instance.IsOpen)
        {
            WorldMapUI.Instance.SetOpen(false);
            return;
        }

        TogglePause();
    }

    public void TogglePause()
    {
        SetPaused(!isPaused);
    }

    public void SetPaused(bool paused)
    {
        isPaused = paused;
        Time.timeScale = paused ? 0f : 1f;
        AudioListener.pause = paused;
        if (paused && WorldMapUI.Instance != null && WorldMapUI.Instance.IsOpen)
            WorldMapUI.Instance.SetOpen(false);
        if (paused && InventoryUI.Instance != null && InventoryUI.Instance.IsBagOpen)
            InventoryUI.Instance.SetBagOpen(false);
        if (paused && WalletHud.Instance != null && WalletHud.Instance.IsShopOpen)
            WalletHud.Instance.SetShopOpen(false);

        EnsurePauseOverlay();
        if (pauseRoot != null)
            pauseRoot.SetActive(paused);
        if (paused)
            ShowPauseHome();

        RestoreGameplayFocus();
    }

    public void RestoreGameplayFocus()
    {
        bool uiOpen = MachineUI.Instance != null && MachineUI.Instance.IsOpen;
        bool mapOpen = WorldMapUI.Instance != null && WorldMapUI.Instance.IsOpen;
        bool bagOpen = InventoryUI.Instance != null && InventoryUI.Instance.IsBagOpen;
        bool freeCursor = isPaused || uiOpen || mapOpen || bagOpen;

        Cursor.lockState = freeCursor ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = freeCursor;
        SetPlayerControl(!isPaused && !uiOpen && !mapOpen && !bagOpen);
    }

    static void SetPlayerControl(bool enabled)
    {
        PlayerMovement movement = Object.FindFirstObjectByType<PlayerMovement>();
        if (movement == null)
            return;

        movement.canMove = enabled;
        movement.canLook = enabled;
    }

    void EnsurePauseOverlay()
    {
        if (pauseRoot != null)
            return;

        GameObject canvasGo = new GameObject("PauseCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        pauseRoot = new GameObject("PauseOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        pauseRoot.transform.SetParent(canvasGo.transform, false);
        RectTransform rootRt = pauseRoot.GetComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;
        Image dim = pauseRoot.GetComponent<Image>();
        dim.color = new Color(0.03f, 0.08f, 0.05f, 0.78f);
        dim.raycastTarget = true;

        pauseHome = CreatePausePanel(pauseRoot.transform, "Home", new Vector2(520f, 480f));
        TextMeshProUGUI title = UiTheme.AddText(pauseHome.transform, "Title", "ПАУЗА", 52f, UiTheme.Accent);
        title.alignment = TextAlignmentOptions.Center;
        title.fontStyle = FontStyles.Bold;
        Stretch(title.rectTransform, 0.78f, 1f, 16f);

        CreatePauseButton(pauseHome.transform, "Continue", "Продолжить", new Vector2(0.72f, 0.56f), () => SetPaused(false));
        CreatePauseButton(pauseHome.transform, "Settings", "Настройки", new Vector2(0.52f, 0.36f), ShowSettings);
        CreatePauseButton(pauseHome.transform, "Save", "Сохранить", new Vector2(0.34f, 0.20f), () =>
        {
            if (SaveSystem.Instance != null)
                SaveSystem.Instance.SaveGame();
        });
        CreatePauseButton(pauseHome.transform, "ToMenu", "В меню", new Vector2(0.14f, -0.02f), () => MainMenu.LoadMenu());

        pauseSettings = CreatePausePanel(pauseRoot.transform, "Settings", new Vector2(640f, 560f));
        TextMeshProUGUI setTitle = UiTheme.AddText(pauseSettings.transform, "Title", "НАСТРОЙКИ", 40f, UiTheme.Accent);
        setTitle.alignment = TextAlignmentOptions.Center;
        setTitle.fontStyle = FontStyles.Bold;
        Stretch(setTitle.rectTransform, 0.86f, 1f, 16f);

        miniZoomLabel = AddSliderRow(pauseSettings.transform, "Zoom", "Масштаб миникарты", 0.62f, 0.06f, 1f,
            WorldMapUI.Instance != null ? WorldMapUI.Instance.MiniZoom : 0.18f,
            v =>
            {
                if (WorldMapUI.Instance != null)
                    WorldMapUI.Instance.MiniZoom = v;
                RefreshSettingLabels();
            });
        miniSizeLabel = AddSliderRow(pauseSettings.transform, "Size", "Размер миникарты", 0.42f, 140f, 360f,
            WorldMapUI.Instance != null ? WorldMapUI.Instance.MiniSize : 220f,
            v =>
            {
                if (WorldMapUI.Instance != null)
                    WorldMapUI.Instance.MiniSize = v;
                RefreshSettingLabels();
            });

        CreatePauseButton(pauseSettings.transform, "Keys", "Клавиши", new Vector2(0.30f, 0.18f), ShowKeys);
        CreatePauseButton(pauseSettings.transform, "Back", "Назад", new Vector2(0.14f, 0.02f), ShowPauseHome);
        pauseSettings.SetActive(false);

        pauseKeys = KeybindSettingsUI.Create(pauseRoot.transform, ShowSettings).gameObject;
        pauseKeys.SetActive(false);
        RefreshSettingLabels();

        pauseRoot.SetActive(false);
    }

    void ShowSettings()
    {
        if (pauseHome != null)
            pauseHome.SetActive(false);
        if (pauseKeys != null)
            pauseKeys.SetActive(false);
        if (pauseSettings != null)
            pauseSettings.SetActive(true);
        RefreshSettingLabels();
    }

    void ShowKeys()
    {
        if (pauseHome != null)
            pauseHome.SetActive(false);
        if (pauseSettings != null)
            pauseSettings.SetActive(false);
        if (pauseKeys != null)
            pauseKeys.SetActive(true);
    }

    void ShowPauseHome()
    {
        if (pauseSettings != null)
            pauseSettings.SetActive(false);
        if (pauseKeys != null)
            pauseKeys.SetActive(false);
        if (pauseHome != null)
            pauseHome.SetActive(true);
    }

    void RefreshSettingLabels()
    {
        if (WorldMapUI.Instance == null)
            return;
        if (miniZoomLabel != null)
            miniZoomLabel.text = "Масштаб миникарты  " + Mathf.RoundToInt(WorldMapUI.Instance.MiniZoom * 100f) + "%";
        if (miniSizeLabel != null)
            miniSizeLabel.text = "Размер миникарты  " + Mathf.RoundToInt(WorldMapUI.Instance.MiniSize) + " px";
    }

    static GameObject CreatePausePanel(Transform parent, string name, Vector2 size)
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

    static void CreatePauseButton(Transform parent, string name, string label, Vector2 yRange, UnityEngine.Events.UnityAction onClick)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.18f, yRange.y);
        rt.anchorMax = new Vector2(0.82f, yRange.x);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        Image img = go.GetComponent<Image>();
        UiTheme.StyleImage(img, UiTheme.Card);
        Button btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);
        TextMeshProUGUI text = UiTheme.AddText(go.transform, "Label", label, 26f, UiTheme.Text);
        text.alignment = TextAlignmentOptions.Center;
        Stretch(text.rectTransform, 0f, 1f, 8f);
    }

    TextMeshProUGUI AddSliderRow(Transform parent, string name, string title, float y, float min, float max, float value, UnityEngine.Events.UnityAction<float> onChange)
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

    public void PrepareLeaveGameplay()
    {
        if (SaveSystem.Instance != null)
            SaveSystem.Instance.SaveGame();
        isPaused = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        if (ResearchSystem.Instance != null)
            Destroy(ResearchSystem.Instance.gameObject);
        WorldCatalog.ClearActive();
        Instance = null;
        Destroy(gameObject);
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}
