using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class ResearchUI : MonoBehaviour
{
    [Header("References")]
    public ResearchSystem researchSystem;
    public GameObject menuPanel;
    public Transform nodesParent;
    public GameObject nodeButtonPrefab;

    private bool isOpen = false;
    private InputSystem_Actions inputActions;

    void Awake()
    {
        inputActions = new InputSystem_Actions();
    }

    void Start()
    {
        if (menuPanel != null)
            menuPanel.SetActive(false);
    }

    void OnEnable()
    {
        inputActions.Enable();
        inputActions.Player.Research.performed += OnResearchToggle;
    }

    void OnDisable()
    {
        inputActions.Player.Research.performed -= OnResearchToggle;
        inputActions.Disable();
    }

    void OnResearchToggle(InputAction.CallbackContext ctx)
    {
        if (GameManager.Instance != null && GameManager.Instance.IsPaused)
            return;
        if (MachineUI.Instance != null && MachineUI.Instance.IsOpen)
            return;
        ToggleMenu();
    }

    public void ToggleMenu()
    {
        isOpen = !isOpen;

        if (menuPanel != null)
            menuPanel.SetActive(isOpen);

        Cursor.lockState = isOpen ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = isOpen;

        if (isOpen)
            RefreshList();
    }

    void RefreshList()
    {
        if (nodesParent == null || nodeButtonPrefab == null || researchSystem == null) return;

        foreach (Transform child in nodesParent)
            Destroy(child.gameObject);

        foreach (var node in researchSystem.GetAllNodes())
        {
            GameObject btn = Instantiate(nodeButtonPrefab, nodesParent);

            var text = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
            {
                string status = researchSystem.IsResearchUnlocked(node) ? " [DONE]" : "";
                text.text = node.displayName + status;
            }

            // Можно добавить блокировку кнопок и т.д.
        }
    }
}