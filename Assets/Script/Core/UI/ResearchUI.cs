using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class ResearchUI : MonoBehaviour
{
    public static ResearchUI Instance { get; private set; }

    [Header("References")]
    public ResearchSystem researchSystem;
    public GameObject menuPanel;
    public Transform nodesParent;
    public GameObject nodeButtonPrefab;

    public bool IsOpen { get; private set; }

    VisualElement overlay;
    ScrollView list;
    InputSystem_Actions inputActions;

    void Awake()
    {
        Instance = this;
        inputActions = KeybindStore.Shared;
    }

    void Start()
    {
        Build();
        IndustryUi.HideLegacy(this, menuPanel);
        IndustryUi.DisableHudCanvas(this);
        IndustryUi.Show(overlay, false);
        IsOpen = false;
    }

    void OnEnable()
    {
        if (inputActions != null)
            inputActions.Player.Research.performed += OnResearchToggle;
    }

    void OnDisable()
    {
        if (inputActions != null)
            inputActions.Player.Research.performed -= OnResearchToggle;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Build()
    {
        VisualElement root = IndustryUi.Mount(this, 105);
        overlay = IndustryUi.OverlayPanel("Исследования", null, Close);
        VisualElement panel = IndustryUi.PanelOf(overlay);
        list = IndustryUi.Scroll("Nodes");
        panel.Add(list);
        root.Add(overlay);
    }

    void OnResearchToggle(InputAction.CallbackContext ctx)
    {
        if (KeybindStore.BlocksGameplayInput)
            return;
        if (GameManager.Instance != null && GameManager.Instance.IsPaused)
            return;
        if (MachineUI.Instance != null && MachineUI.Instance.IsOpen)
            return;
        ToggleMenu();
    }

    public void ToggleMenu()
    {
        if (IsOpen)
            Close();
        else
            Open();
    }

    public void Open()
    {
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);
        if (overlay == null)
            Build();
        IsOpen = true;
        RefreshList();
        IndustryUi.Show(overlay, true);
        if (GameManager.Instance != null)
            GameManager.Instance.RestoreGameplayFocus();
        else
        {
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
        }
    }

    public void Close()
    {
        IsOpen = false;
        IndustryUi.Show(overlay, false);
        if (GameManager.Instance != null)
            GameManager.Instance.RestoreGameplayFocus();
        else
        {
            UnityEngine.Cursor.lockState = CursorLockMode.Locked;
            UnityEngine.Cursor.visible = false;
        }
    }

    void RefreshList()
    {
        if (list == null)
            return;
        list.Clear();
        ResearchSystem system = researchSystem != null ? researchSystem : ResearchSystem.Instance;
        if (system == null)
            return;

        foreach (ResearchNodeData node in system.GetAllNodes())
        {
            if (node == null)
                continue;
            string status = "READY";
            bool canStart = system.CanStartResearch(node);
            if (system.IsResearchUnlocked(node))
            {
                status = "DONE";
                canStart = false;
            }
            else if (system.CurrentResearch == node)
            {
                status = "ACTIVE";
                canStart = false;
            }
            else if (!canStart)
            {
                status = "LOCKED";
            }

            ResearchNodeData captured = node;
            list.Add(IndustryUi.ResearchCard(
                node,
                status,
                canStart,
                () =>
                {
                    if (system.SetCurrentResearch(captured))
                        Close();
                }));
        }
    }
}
