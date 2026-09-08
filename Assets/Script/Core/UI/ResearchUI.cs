using System.Collections.Generic;
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
    VisualElement treeHost;
    VisualElement detail;
    Label detailTitle;
    Label detailBody;
    Label detailStatus;
    VisualElement detailNeed;
    VisualElement detailUnlocks;
    Button detailStart;
    ResearchNodeData selected;
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
        if (ResearchSystem.Instance != null)
        {
            ResearchSystem.Instance.OnUnlocksChanged -= RefreshList;
            ResearchSystem.Instance.OnResearchProgressChanged -= RefreshList;
            ResearchSystem.Instance.OnUnlocksChanged += RefreshList;
            ResearchSystem.Instance.OnResearchProgressChanged += RefreshList;
        }
    }

    void OnEnable()
    {
        if (inputActions != null)
            inputActions.Player.Research.performed += OnResearchToggle;
        if (ResearchSystem.Instance != null)
        {
            ResearchSystem.Instance.OnUnlocksChanged += RefreshList;
            ResearchSystem.Instance.OnResearchProgressChanged += RefreshList;
        }
    }

    void OnDisable()
    {
        if (inputActions != null)
            inputActions.Player.Research.performed -= OnResearchToggle;
        if (ResearchSystem.Instance != null)
        {
            ResearchSystem.Instance.OnUnlocksChanged -= RefreshList;
            ResearchSystem.Instance.OnResearchProgressChanged -= RefreshList;
        }
    }

    void OnDestroy()
    {
        if (ResearchSystem.Instance != null)
        {
            ResearchSystem.Instance.OnUnlocksChanged -= RefreshList;
            ResearchSystem.Instance.OnResearchProgressChanged -= RefreshList;
        }
        if (Instance == this)
            Instance = null;
    }

    void Build()
    {
        VisualElement root = IndustryUi.Mount(this, 105);
        overlay = IndustryUi.OverlayPanel(UiLocale.T("overlay.research"), null, Close);
        VisualElement panel = IndustryUi.PanelOf(overlay);
        var layout = IndustryUi.El("Layout", "research-layout");
        var scroll = IndustryUi.Scroll("TreeScroll");
        treeHost = IndustryUi.El("Tree", "research-tree");
        scroll.Add(treeHost);
        layout.Add(scroll);

        detail = IndustryUi.El("Detail", "research-detail", "col");
        detailTitle = IndustryUi.Text("DT", UiLocale.T("research.pick"), "heading-3");
        detailStatus = IndustryUi.Text("DS", "", "badge", "badge-ready");
        detailBody = IndustryUi.Text("DB", "", "muted");
        detailNeed = IndustryUi.El("Need", "row", "io-row");
        detailUnlocks = IndustryUi.El("Unlock", "row", "io-row");
        detailStart = IndustryUi.Btn(UiLocale.T("research.start"), OnStartClicked, "btn-primary");
        detailStart.name = "ResearchStart";
        detail.Add(detailTitle);
        detail.Add(detailStatus);
        detail.Add(detailBody);
        detail.Add(IndustryUi.Text("NL", UiLocale.T("research.need"), "label-caps"));
        detail.Add(detailNeed);
        detail.Add(IndustryUi.Text("UL", UiLocale.T("research.unlocks"), "label-caps"));
        detail.Add(detailUnlocks);
        detail.Add(detailStart);
        layout.Add(detail);
        panel.Add(layout);
        root.Add(overlay);
        ShowDetail(null);
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
        KeybindStore.SuppressGameplay();
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
        if (treeHost == null)
            return;
        treeHost.Clear();
        ResearchSystem system = researchSystem != null ? researchSystem : ResearchSystem.Instance;
        if (system == null)
            return;

        List<ResearchNodeData> nodes = system.GetAllNodes();
        var depths = new Dictionary<ResearchNodeData, int>();
        int maxDepth = 0;
        for (int i = 0; i < nodes.Count; i++)
        {
            int d = Depth(nodes[i], depths, 0);
            if (d > maxDepth)
                maxDepth = d;
        }

        var columns = new List<VisualElement>(maxDepth + 1);
        for (int d = 0; d <= maxDepth; d++)
        {
            var col = IndustryUi.El("Col" + d, "research-col");
            columns.Add(col);
            treeHost.Add(col);
        }

        for (int i = 0; i < nodes.Count; i++)
        {
            ResearchNodeData node = nodes[i];
            if (node == null)
                continue;
            int d = 0;
            depths.TryGetValue(node, out d);
            if (d < 0 || d >= columns.Count)
                d = 0;
            string status = NodeStatus(system, node);
            bool canStart = system.CanStartResearch(node);
            ResearchNodeData captured = node;
            VisualElement card = IndustryUi.ResearchCard(node, status, canStart, () => ShowDetail(captured));
            if (selected == node)
                card.AddToClassList("is-selected");
            columns[d].Add(card);
        }

        if (selected != null)
            ShowDetail(selected);
    }

    void ShowDetail(ResearchNodeData node)
    {
        selected = node;
        ResearchSystem system = researchSystem != null ? researchSystem : ResearchSystem.Instance;
        if (node == null || system == null)
        {
            detailTitle.text = UiLocale.T("research.pick");
            detailBody.text = UiLocale.T("research.pick_body");
            detailStatus.text = "";
            detailNeed.Clear();
            detailUnlocks.Clear();
            IndustryUi.Show(detailStart, false);
            return;
        }

        string status = NodeStatus(system, node);
        detailTitle.text = node.displayName;
        detailBody.text = string.IsNullOrEmpty(node.description) ? "" : node.description;
        detailStatus.text = status == "DONE" ? UiLocale.T("research.done")
            : status == "ACTIVE" ? UiLocale.T("research.active")
            : status == "LOCKED" ? UiLocale.T("research.locked")
            : UiLocale.T("research.ready");
        detailStatus.EnableInClassList("badge", true);
        detailStatus.EnableInClassList("badge-done", status == "DONE");
        detailStatus.EnableInClassList("badge-running", status == "ACTIVE");
        detailStatus.EnableInClassList("badge-locked", status == "LOCKED");
        detailStatus.EnableInClassList("badge-ready", status == "READY");
        detailNeed.Clear();
        IndustryUi.AddStacks(detailNeed, node.requiredItems);
        if (node.requiredResearches != null)
        {
            for (int i = 0; i < node.requiredResearches.Count; i++)
            {
                ResearchNodeData req = node.requiredResearches[i];
                if (req != null)
                    detailNeed.Add(IndustryUi.Text("R", req.displayName, "caption"));
            }
        }

        detailUnlocks.Clear();
        if (node.unlockedBuildings != null)
        {
            for (int i = 0; i < node.unlockedBuildings.Count; i++)
            {
                BuildingData b = node.unlockedBuildings[i];
                if (b != null)
                    detailUnlocks.Add(IndustryUi.StackChip(b.icon, 0));
            }
        }

        if (node.unlockedRecipes != null)
        {
            for (int i = 0; i < node.unlockedRecipes.Count; i++)
            {
                ItemData output = IndustryUi.FirstItem(node.unlockedRecipes[i] != null ? node.unlockedRecipes[i].outputs : null);
                if (output != null)
                    detailUnlocks.Add(IndustryUi.StackChip(output.icon, 0));
            }
        }

        bool canStart = system.CanStartResearch(node);
        IndustryUi.Show(detailStart, canStart);
        IndustryUi.SetButtonLabel(detailStart, UiLocale.T("research.start"));
    }

    void OnStartClicked()
    {
        ResearchSystem system = researchSystem != null ? researchSystem : ResearchSystem.Instance;
        if (system == null || selected == null)
            return;
        if (system.SetCurrentResearch(selected))
        {
            UiAudio.PlayConfirm();
            UiNotification.Push(UiLocale.T("research.started"), selected.displayName, UiStatus.Running);
            Close();
        }
    }

    static string NodeStatus(ResearchSystem system, ResearchNodeData node)
    {
        if (system.IsResearchUnlocked(node))
            return "DONE";
        if (system.CurrentResearch == node)
            return "ACTIVE";
        if (system.CanStartResearch(node))
            return "READY";
        return "LOCKED";
    }

    static int Depth(ResearchNodeData node, Dictionary<ResearchNodeData, int> memo, int guard)
    {
        if (node == null)
            return 0;
        if (memo.TryGetValue(node, out int cached))
            return cached;
        if (guard > 16)
            return 0;
        int depth = 0;
        if (node.requiredResearches != null)
        {
            for (int i = 0; i < node.requiredResearches.Count; i++)
            {
                int d = Depth(node.requiredResearches[i], memo, guard + 1) + 1;
                if (d > depth)
                    depth = d;
            }
        }

        memo[node] = depth;
        return depth;
    }
}
