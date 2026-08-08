using UnityEngine;
using UnityEngine.InputSystem;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("References")]
    public PlayerInventory playerInventory;
    public PlayerBuilder playerBuilder;
    public ResearchSystem researchSystem;

    [Header("Game State")]
    public bool isPaused = false;

    private InputSystem_Actions inputActions;

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

        // Подписываемся один раз
        inputActions.Player.Pause.performed += OnPausePerformed; // временно на Jump
        // Лучше потом сделать отдельный action "Pause"
    }

    void OnDisable()
    {
        inputActions.Player.Pause.performed -= OnPausePerformed;
        inputActions.Disable();
    }

    private void OnPausePerformed(InputAction.CallbackContext context)
    {
        TogglePause();
    }

    public void TogglePause()
    {
        isPaused = !isPaused;

        Time.timeScale = isPaused ? 0f : 1f;

        Cursor.lockState = isPaused ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = isPaused;

        // Здесь потом можно открывать/закрывать меню паузы
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}