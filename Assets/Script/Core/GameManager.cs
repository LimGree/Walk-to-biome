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

        inputActions = new InputSystem_Actions();
    }

    void OnEnable()
    {
        inputActions.Enable();
        inputActions.Player.Pause.performed += OnPausePerformed;
    }

    void OnDisable()
    {
        inputActions.Player.Pause.performed -= OnPausePerformed;
        inputActions.Disable();
    }

    void OnPausePerformed(InputAction.CallbackContext context)
    {
        if (MachineUI.Instance != null && MachineUI.Instance.IsOpen)
        {
            MachineUI.Instance.Close();
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

        EnsurePauseOverlay();
        if (pauseRoot != null)
            pauseRoot.SetActive(paused);

        RestoreGameplayFocus();
    }

    public void RestoreGameplayFocus()
    {
        bool uiOpen = MachineUI.Instance != null && MachineUI.Instance.IsOpen;
        bool freeCursor = isPaused || uiOpen;

        Cursor.lockState = freeCursor ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = freeCursor;
        SetPlayerControl(!isPaused && !uiOpen);
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

        GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(pauseRoot.transform, false);
        RectTransform panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0.5f);
        panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(520f, 240f);
        UiTheme.StyleImage(panel.GetComponent<Image>(), UiTheme.Panel);

        TextMeshProUGUI title = UiTheme.AddText(panel.transform, "Title", "ПАУЗА", 56f, UiTheme.Accent);
        title.alignment = TextAlignmentOptions.Center;
        title.fontStyle = FontStyles.Bold;
        RectTransform titleRt = title.rectTransform;
        titleRt.anchorMin = new Vector2(0f, 0.45f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.offsetMin = new Vector2(20f, 0f);
        titleRt.offsetMax = new Vector2(-20f, -18f);

        TextMeshProUGUI hint = UiTheme.AddText(panel.transform, "Hint", "ESC — продолжить игру", 22f, UiTheme.Text);
        hint.alignment = TextAlignmentOptions.Center;
        RectTransform hintRt = hint.rectTransform;
        hintRt.anchorMin = new Vector2(0f, 0f);
        hintRt.anchorMax = new Vector2(1f, 0.48f);
        hintRt.offsetMin = new Vector2(20f, 24f);
        hintRt.offsetMax = new Vector2(-20f, -8f);

        pauseRoot.SetActive(false);
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}
