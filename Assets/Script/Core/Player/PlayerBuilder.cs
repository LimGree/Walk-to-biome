using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerBuilder : MonoBehaviour
{
    [Header("References")]
    public PlayerInventory inventory;
    public PlayerMovement playerMovement;
    public BuildMenuUI buildMenuUI;
    public Camera playerCamera;
    public LayerMask buildLayer = ~0;
    public LayerMask collisionLayer = ~0;
    public LayerMask demolishLayer = ~0;

    [Header("Settings")]
    public float maxBuildDistance = 12f;
    public Material ghostValidMaterial;
    public Material ghostInvalidMaterial;

    public bool IsBuildModeActive => isBuildMode && currentBuildingData != null;
    public bool HasPlacementTarget { get; private set; }
    public Vector3 CurrentPlacementPosition { get; private set; }
    public bool CanPlaceCurrent { get; private set; }
    public Vector2Int CurrentFootprintSize { get; private set; } = Vector2Int.one;

    public bool isBuildMode = false;
    public event Action<bool> OnBuildModeChanged;

    public bool BlocksBuildInput => selection != null && selection.BlocksBuildInput;
    public bool IsLineStrokeActive => strokeActive;
    public bool HasHeldBuilding => currentBuildingData != null;
    public GameObject CurrentGhost => currentGhost;
    public BuildingData CurrentBuildingData => currentBuildingData;
    public BuildSelectionController Selection => selection;

    BuildSelectionController selection;
    private InputSystem_Actions inputActions;
    private GameObject currentGhost;
    private BuildingData currentBuildingData;
    private float currentRotationY = 0f;
    private bool canPlace = false;
    private int indexBuilding = 0;

    struct LineSlot
    {
        public Vector2Int min;
        public Vector3 pos;
        public bool valid;
    }

    bool strokeActive;
    BuildingData strokeBuilding;
    float strokeYaw;
    Vector2Int strokeSize = Vector2Int.one;
    Vector2Int strokeStartMin;
    Vector2Int? strokeAxis;
    readonly List<LineSlot> strokeSlots = new List<LineSlot>(16);
    readonly List<GameObject> lineGhosts = new List<GameObject>(16);
    const int MaxLineBuildings = 64;

    void Awake()
    {
        inputActions = KeybindStore.Shared;

        if (inventory == null)
            inventory = GetComponent<PlayerInventory>();

        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>();

        if (playerCamera == null)
            playerCamera = Camera.main;

        if (buildMenuUI == null)
            buildMenuUI = FindFirstObjectByType<BuildMenuUI>();

        selection = GetComponent<BuildSelectionController>();
        if (selection == null)
            selection = gameObject.AddComponent<BuildSelectionController>();
    }

    void OnEnable()
    {
        inputActions.Player.Place.started += OnPlaceStarted;
        inputActions.Player.Place.canceled += OnPlaceCanceled;
        inputActions.Player.Demolish.performed += OnDemolish;
        inputActions.Player.Rotate.performed += OnRotate;
        inputActions.Player.BuildMode.performed += OnBuildModeToggle;
    }

    void OnDisable()
    {
        inputActions.Player.Place.started -= OnPlaceStarted;
        inputActions.Player.Place.canceled -= OnPlaceCanceled;
        inputActions.Player.Demolish.performed -= OnDemolish;
        inputActions.Player.Rotate.performed -= OnRotate;
        inputActions.Player.BuildMode.performed -= OnBuildModeToggle;
        EndStroke();
        DestroyGhost();
        ClearPlacementTarget();
    }

    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsPaused)
        {
            EndStroke();
            return;
        }

        BuildingData selected = inventory != null ? inventory.GetSelectedBuilding() : null;
        if (selected != currentBuildingData)
        {
            EndStroke();
            currentBuildingData = selected;
            RecreateGhost();
        }

        if (!isBuildMode)
        {
            EndStroke();
            DestroyGhost();
            ClearPlacementTarget();
            return;
        }

        if (buildMenuUI != null && buildMenuUI.IsOpen)
        {
            EndStroke();
            if (currentGhost != null)
                currentGhost.SetActive(false);
            ClearPlacementTarget();
            return;
        }

        UpdateAim();

        if (BlocksBuildInput)
        {
            EndStroke();
            if (currentGhost != null)
                currentGhost.SetActive(false);
            return;
        }

        if (IsBuildModeActive)
        {
            if (strokeActive)
                currentRotationY = strokeYaw;

            UpdateGhost();
            TickLineStroke();
        }
        else
        {
            EndStroke();
            DestroyGhost();
        }
    }

    bool IsGameplayBuildInputBlocked()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsPaused)
            return true;
        if (MachineUI.Instance != null && MachineUI.Instance.IsOpen)
            return true;
        if (buildMenuUI != null && buildMenuUI.IsOpen)
            return true;
        if (WalletHud.Instance != null && WalletHud.Instance.IsShopOpen)
            return true;
        if (SelectionActionsUI.Instance != null && SelectionActionsUI.Instance.IsOpen)
            return true;
        return false;
    }

    void OnRotate(InputAction.CallbackContext ctx)
    {
        if (!isBuildMode || strokeActive || IsGameplayBuildInputBlocked() || BlocksBuildInput)
            return;
        HandleRotateKey();
    }

    void OnDemolish(InputAction.CallbackContext ctx)
    {
        if (!isBuildMode || IsGameplayBuildInputBlocked() || BlocksBuildInput)
            return;
        EndStroke();
        TryDemolish();
    }

    void HandleRotateKey()
    {
        if (HasHeldBuilding)
        {
            currentRotationY += 90f;
            if (currentRotationY >= 360f)
                currentRotationY = 0f;
            if (currentGhost != null)
                currentGhost.transform.rotation = Quaternion.Euler(0f, currentRotationY, 0f);
            return;
        }

        TryRotatePlacedBuilding();
    }

    bool TryRotatePlacedBuilding()
    {
        if (playerCamera == null || GridSystem.Instance == null)
            return false;

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, maxBuildDistance, demolishLayer))
            return false;
        if (IsStrokeGhost(hit.transform))
            return false;

        BuildingBase building = hit.collider.GetComponentInParent<BuildingBase>();
        if (building == null)
            return false;

        float prevYaw = building.transform.eulerAngles.y;
        float newYaw = prevYaw + 90f;
        building.transform.rotation = Quaternion.Euler(0f, newYaw, 0f);

        if (building.data != null)
        {
            Vector2Int newSize = GridFootprint.GetRotatedSize(building.data.size, newYaw);
            Vector2Int minCell = GridFootprint.GetMinCell(building.transform.position, newSize);

            GridOccupancy.Unregister(building.gameObject);
            if (!GridOccupancy.IsAreaFree(minCell, newSize))
            {
                building.transform.rotation = Quaternion.Euler(0f, prevYaw, 0f);
                building.ReRegisterOnGrid();
                Debug.LogWarning($"[Builder] Rotate blocked: footprint occupied for {building.name}");
                return true;
            }

            GridOccupancy.Register(building.gameObject, minCell, newSize);
        }
        else
        {
            building.ReRegisterOnGrid();
        }

        building.OnRotated();
        return true;
    }

    void OnBuildModeToggle(InputAction.CallbackContext ctx)
    {
        if (GameManager.Instance != null && GameManager.Instance.IsPaused)
            return;
        if (MachineUI.Instance != null && MachineUI.Instance.IsOpen)
            return;

        if (!isBuildMode)
            EnterBuildMode(openMenu: false);
        else
            ExitBuildMode();
    }

    public void EnterBuildMode(bool openMenu)
    {
        isBuildMode = true;

        if (buildMenuUI != null && buildMenuUI.IsOpen)
            buildMenuUI.CloseMenu(restorePlayerControl: false);

        ApplyGameplayCursorAndControl(buildMenuOpen: false);
        OnBuildModeChanged?.Invoke(true);
    }

    public void ExitBuildMode()
    {
        isBuildMode = false;
        EndStroke();
        DestroyGhost();
        ClearPlacementTarget();

        if (buildMenuUI != null && buildMenuUI.IsOpen)
            buildMenuUI.CloseMenu(restorePlayerControl: false);

        ApplyGameplayCursorAndControl(buildMenuOpen: false);
        OnBuildModeChanged?.Invoke(false);
    }

    public void OnBuildMenuClosedAfterSelection()
    {
        ApplyGameplayCursorAndControl(buildMenuOpen: false);
    }

    void ApplyGameplayCursorAndControl(bool buildMenuOpen)
    {
        if (buildMenuOpen)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (playerMovement != null)
            {
                playerMovement.canMove = false;
                playerMovement.canLook = false;
            }
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            if (playerMovement != null)
            {
                playerMovement.canMove = true;
                playerMovement.canLook = true;
            }
        }
    }

    public void SetBuildMode(bool enabled)
    {
        if (enabled)
            EnterBuildMode(openMenu: false);
        else
            ExitBuildMode();
    }

    void RecreateGhost()
    {
        ClearLineGhosts();
        DestroyGhost();

        if (!isBuildMode || currentBuildingData == null)
            return;

        GameObject ghostSource = currentBuildingData.ghostPrefab != null
            ? currentBuildingData.ghostPrefab
            : currentBuildingData.prefab;
        if (ghostSource == null)
            return;

        currentGhost = Instantiate(ghostSource);

        foreach (var col in currentGhost.GetComponentsInChildren<Collider>(true))
            col.enabled = false;

        Conveyor belt = currentGhost.GetComponent<Conveyor>();
        if (belt == null && currentBuildingData.IsConveyor)
            belt = currentGhost.AddComponent<Conveyor>();
        if (belt != null)
            belt.PreparePreview(currentBuildingData);
    }

    void DestroyGhost()
    {
        ClearLineGhosts();
        if (currentGhost != null)
        {
            Destroy(currentGhost);
            currentGhost = null;
        }
    }

    void ClearLineGhosts()
    {
        for (int i = 0; i < lineGhosts.Count; i++)
        {
            if (lineGhosts[i] != null)
                Destroy(lineGhosts[i]);
        }
        lineGhosts.Clear();
    }

    void ClearPlacementTarget()
    {
        HasPlacementTarget = false;
        CanPlaceCurrent = false;
        CurrentFootprintSize = Vector2Int.one;
    }

    public bool TryGetAimCell(out Vector2Int cell, out Vector3 worldPos)
    {
        cell = default;
        worldPos = default;
        if (!IsAimingAtBuildSurface())
            return false;
        worldPos = CurrentPlacementPosition;
        cell = GridFootprint.GetMinCell(worldPos, Vector2Int.one);
        return true;
    }

    void UpdateAim()
    {
        if (playerCamera == null)
        {
            ClearPlacementTarget();
            return;
        }

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, maxBuildDistance, buildLayer))
        {
            canPlace = false;
            ClearPlacementTarget();
            return;
        }

        Quaternion rot = Quaternion.Euler(0f, currentRotationY, 0f);
        Vector2Int size = currentBuildingData != null ? GetPlacementSize(rot) : Vector2Int.one;
        Vector3 placePos = currentBuildingData != null
            ? SnapToGrid(hit.point)
            : (GridSystem.Instance != null
                ? GridSystem.Instance.SnapToGrid(hit.point)
                : GridFootprint.SnapCenter(hit.point, Vector2Int.one));

        CurrentPlacementPosition = placePos;
        CurrentFootprintSize = size;
        HasPlacementTarget = true;
        CanPlaceCurrent = currentBuildingData != null && IsPlacementValid(placePos, rot);
        canPlace = CanPlaceCurrent;
    }

    void UpdateGhost()
    {
        if (currentGhost == null)
        {
            if (currentBuildingData != null)
                RecreateGhost();
            if (currentGhost == null)
                return;
        }

        if (!HasPlacementTarget)
        {
            currentGhost.SetActive(false);
            return;
        }

        Vector3 placePos = CurrentPlacementPosition;
        currentGhost.transform.position = placePos;
        currentGhost.transform.rotation = Quaternion.Euler(0f, currentRotationY, 0f);

        Conveyor ghostBelt = currentGhost.GetComponent<Conveyor>();
        if (ghostBelt != null)
            ghostBelt.Preview(placePos, currentGhost.transform.rotation);

        canPlace = IsPlacementValid(placePos, currentGhost.transform.rotation);
        CanPlaceCurrent = canPlace;

        Material mat = canPlace ? ghostValidMaterial : ghostInvalidMaterial;
        if (mat != null)
        {
            foreach (var r in currentGhost.GetComponentsInChildren<Renderer>())
                r.material = mat;
        }

        currentGhost.SetActive(true);
    }

    Vector3 SnapToGrid(Vector3 position)
    {
        Vector2Int size = GetPlacementSize(Quaternion.Euler(0f, currentRotationY, 0f));
        if (GridSystem.Instance != null)
            return GridSystem.Instance.SnapFootprintCenter(position, size);
        return GridFootprint.SnapCenter(position, size);
    }

    bool IsPlacementValid(Vector3 position, Quaternion rotation, bool checkLabLimit = true)
    {
        Vector2Int size = GetPlacementSize(rotation);

        if (GridSystem.Instance != null)
        {
            Vector2Int minCell = GridFootprint.GetMinCell(position, size);
            if (!GridOccupancy.IsAreaFree(minCell, size))
                return false;
            bool allowWater = currentBuildingData != null && currentBuildingData.allowOnWater;
            bool requireWater = currentBuildingData != null && currentBuildingData.requiresWater;
            if (!WorldBiomeMap.CanBuild(minCell, size, allowWater, requireWater))
                return false;
        }

        if (checkLabLimit
            && IsResearchLabData(currentBuildingData)
            && ResearchSystem.Instance != null
            && !ResearchSystem.Instance.CanPlaceAnotherLab())
        {
            return false;
        }

        if (PlayerWallet.Instance != null
            && !PlayerWallet.Instance.CanAfford(Economy.BuildCost(currentBuildingData)))
        {
            return false;
        }

        if (NeedsResourceNode(currentBuildingData))
        {
            Vector2Int minCell = GridFootprint.GetMinCell(position, size);
            if (!ResourceNode.HasNodeInArea(minCell, size))
                return false;
        }

        float cell = GridFootprint.CellSize;
        Vector3 half = GridFootprint.GetHalfExtents(size);
        Vector3 halfExtents = new Vector3(
            Mathf.Max(0.1f, half.x - 0.01f),
            0.9f,
            Mathf.Max(0.1f, half.z - 0.01f)
        );
        Vector3 areaCenter = position + Vector3.up * halfExtents.y;

        Collider[] ghostColliders = null;
        if (currentGhost != null)
        {
            ghostColliders = currentGhost.GetComponentsInChildren<Collider>();
            foreach (var col in ghostColliders)
                col.enabled = true;
        }

        LayerMask checkMask = collisionLayer;
        if (checkMask == 0)
        {
            int floorLayer = LayerMask.NameToLayer("Floor");
            checkMask = floorLayer != -1 ? buildLayer & ~(1 << floorLayer) : buildLayer;
        }

        Collider[] overlaps = Physics.OverlapBox(
            areaCenter,
            halfExtents,
            Quaternion.identity,
            checkMask
        );

        if (ghostColliders != null)
        {
            foreach (var col in ghostColliders)
                col.enabled = false;
        }

        foreach (Collider overlap in overlaps)
        {
            if (overlap == null) continue;
            if (IsStrokeGhost(overlap.transform))
                continue;
            if (IsNonBlockingCollider(overlap))
                continue;
            return false;
        }

        return true;
    }

    bool IsStrokeGhost(Transform t)
    {
        if (t == null)
            return false;
        if (currentGhost != null && t.IsChildOf(currentGhost.transform))
            return true;
        for (int i = 0; i < lineGhosts.Count; i++)
        {
            if (lineGhosts[i] != null && t.IsChildOf(lineGhosts[i].transform))
                return true;
        }
        return false;
    }

    Vector2Int GetPlacementSize(Quaternion rotation)
    {
        Vector2Int size = currentBuildingData != null ? currentBuildingData.size : Vector2Int.one;
        return GridFootprint.GetRotatedSize(size, rotation.eulerAngles.y);
    }

    static bool IsResearchLabData(BuildingData data)
    {
        return data != null && data.id == "research_lab";
    }

    public static bool IsExtractorData(BuildingData data)
    {
        return data != null && data.id == "extractor";
    }

    public static bool NeedsResourceNode(BuildingData data)
    {
        if (data == null)
            return false;
        return data.requiresResourceNode || IsExtractorData(data);
    }

    static bool IsNonBlockingCollider(Collider col)
    {
        if (col == null)
            return true;

        // Занятость клеток уже проверена через GridOccupancy.
        // Коллайдеры зданий/ленты не должны блокировать соседнюю клетку.
        if (col.GetComponentInParent<BuildingBase>() != null)
            return true;

        if (col.GetComponentInParent<ResourceNode>() != null)
            return true;

        if (col.name.IndexOf("BeltItem", System.StringComparison.OrdinalIgnoreCase) >= 0
            || (col.transform.parent != null
                && col.transform.parent.name.IndexOf("BeltItem", System.StringComparison.OrdinalIgnoreCase) >= 0))
            return true;

        int floorLayer = LayerMask.NameToLayer("Floor");
        if (floorLayer != -1 && col.gameObject.layer == floorLayer)
            return true;

        string n = col.gameObject.name;
        if (n.IndexOf("Floor", System.StringComparison.OrdinalIgnoreCase) >= 0
            || n.IndexOf("Ground", System.StringComparison.OrdinalIgnoreCase) >= 0
            || n.IndexOf("Terrain", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        return false;
    }

    void OnPlaceStarted(InputAction.CallbackContext ctx)
    {
        if (BlocksBuildInput)
            return;
        if (IsGameplayBuildInputBlocked() || !IsBuildModeActive || !IsAimingAtBuildSurface())
            return;
        if (currentBuildingData == null || currentBuildingData.prefab == null)
            return;
        if (ResearchSystem.Instance != null
            && !ResearchSystem.Instance.IsBuildingUnlocked(currentBuildingData))
        {
            Debug.LogWarning($"[Builder] Locked: {currentBuildingData.displayName}");
            return;
        }

        BeginStroke();
    }

    void OnPlaceCanceled(InputAction.CallbackContext ctx)
    {
        if (BlocksBuildInput)
            return;
        if (!strokeActive)
            return;

        if (IsGameplayBuildInputBlocked() || !IsBuildModeActive || !IsAimingAtBuildSurface())
        {
            EndStroke();
            return;
        }

        RebuildStrokeSlots();
        CommitStroke();
    }

    void BeginStroke()
    {
        Quaternion rot = Quaternion.Euler(0f, currentRotationY, 0f);
        strokeActive = true;
        strokeBuilding = currentBuildingData;
        strokeYaw = currentRotationY;
        strokeSize = GetPlacementSize(rot);
        strokeStartMin = GridFootprint.GetMinCell(CurrentPlacementPosition, strokeSize);
        strokeAxis = null;
        strokeSlots.Clear();
    }

    void EndStroke()
    {
        strokeActive = false;
        strokeBuilding = null;
        strokeAxis = null;
        strokeSlots.Clear();
        ClearLineGhosts();
        if (currentGhost != null && isBuildMode)
            currentGhost.SetActive(HasPlacementTarget);
    }

    bool IsAimingAtBuildSurface()
    {
        if (playerCamera == null)
            return false;

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, maxBuildDistance, buildLayer))
            return false;

        CurrentPlacementPosition = SnapToGrid(hit.point);
        HasPlacementTarget = true;
        return true;
    }

    void TickLineStroke()
    {
        if (!strokeActive)
            return;

        if (IsGameplayBuildInputBlocked()
            || !IsBuildModeActive
            || currentBuildingData != strokeBuilding
            || !HasPlacementTarget)
        {
            EndStroke();
            return;
        }

        currentRotationY = strokeYaw;
        RebuildStrokeSlots();
        RefreshLineGhosts();
    }

    void RebuildStrokeSlots()
    {
        strokeSlots.Clear();
        if (!strokeActive)
            return;

        Vector2Int currentMin = GridFootprint.GetMinCell(CurrentPlacementPosition, strokeSize);
        Vector2Int delta = currentMin - strokeStartMin;

        if (!strokeAxis.HasValue
            && (Mathf.Abs(delta.x) >= strokeSize.x || Mathf.Abs(delta.y) >= strokeSize.y))
        {
            strokeAxis = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y)
                ? new Vector2Int(1, 0)
                : new Vector2Int(0, 1);
        }

        int extra = 0;
        int dir = 1;
        int step = 1;
        if (strokeAxis.HasValue)
        {
            int axisDelta = strokeAxis.Value.x != 0 ? delta.x : delta.y;
            step = strokeAxis.Value.x != 0 ? strokeSize.x : strokeSize.y;
            dir = axisDelta >= 0 ? 1 : -1;
            extra = Mathf.Min(Mathf.Abs(axisDelta) / step, MaxLineBuildings - 1);
        }

        Quaternion rot = Quaternion.Euler(0f, strokeYaw, 0f);
        bool isLab = IsResearchLabData(strokeBuilding);
        int labCapacity = GetLabCapacity();
        int labUsed = 0;
        int pieceCost = Economy.BuildCost(strokeBuilding);
        int coinBudget = PlayerWallet.Instance != null && pieceCost > 0
            ? PlayerWallet.Instance.Coins / pieceCost
            : extra + 1;
        int paid = 0;

        for (int i = 0; i <= extra; i++)
        {
            Vector2Int min = strokeAxis.HasValue
                ? LineMinAt(i, dir, step)
                : strokeStartMin;
            Vector3 pos = GridFootprint.MinCellToCenter(min, strokeSize, CurrentPlacementPosition.y);
            bool valid = IsPlacementValid(pos, rot, checkLabLimit: false);
            if (valid && isLab)
            {
                valid = labUsed < labCapacity;
                if (valid)
                    labUsed++;
            }

            if (valid)
            {
                if (paid >= coinBudget)
                    valid = false;
                else
                    paid++;
            }

            strokeSlots.Add(new LineSlot { min = min, pos = pos, valid = valid });
        }

        if (strokeSlots.Count > 0)
        {
            CurrentPlacementPosition = strokeSlots[strokeSlots.Count - 1].pos;
            CurrentFootprintSize = strokeSize;
        }
    }

    int GetLabCapacity()
    {
        if (!IsResearchLabData(strokeBuilding) || ResearchSystem.Instance == null)
            return MaxLineBuildings;
        return Mathf.Max(0, ResearchSystem.Instance.GetMaxResearchLabs()
            - ResearchSystem.Instance.CountPlacedLabs());
    }

    Vector2Int LineMinAt(int index, int dir, int step)
    {
        return strokeStartMin + new Vector2Int(
            strokeAxis.Value.x * index * step * dir,
            strokeAxis.Value.y * index * step * dir);
    }

    void RefreshLineGhosts()
    {
        if (currentGhost != null)
            currentGhost.SetActive(false);

        bool lineOk = strokeSlots.Count > 0;
        for (int i = 0; i < strokeSlots.Count; i++)
        {
            if (!strokeSlots[i].valid)
                lineOk = false;
        }

        while (lineGhosts.Count < strokeSlots.Count)
            lineGhosts.Add(CreateLineGhost());

        for (int i = lineGhosts.Count - 1; i >= strokeSlots.Count; i--)
        {
            if (lineGhosts[i] != null)
                Destroy(lineGhosts[i]);
            lineGhosts.RemoveAt(i);
        }

        Quaternion rot = Quaternion.Euler(0f, strokeYaw, 0f);
        Material mat = lineOk ? ghostValidMaterial : ghostInvalidMaterial;
        for (int i = 0; i < strokeSlots.Count; i++)
        {
            GameObject ghost = lineGhosts[i];
            if (ghost == null)
            {
                ghost = CreateLineGhost();
                lineGhosts[i] = ghost;
            }
            if (ghost == null)
                continue;

            Vector3 pos = strokeSlots[i].pos;
            ghost.SetActive(true);
            ghost.transform.SetPositionAndRotation(pos, rot);

            Conveyor belt = ghost.GetComponent<Conveyor>();
            if (belt != null)
                belt.Preview(pos, rot);

            if (mat != null)
            {
                foreach (var r in ghost.GetComponentsInChildren<Renderer>())
                    r.material = mat;
            }
        }

        canPlace = lineOk;
        CanPlaceCurrent = lineOk;
    }

    GameObject CreateLineGhost()
    {
        if (strokeBuilding == null)
            return null;

        GameObject source = strokeBuilding.ghostPrefab != null
            ? strokeBuilding.ghostPrefab
            : strokeBuilding.prefab;
        if (source == null)
            return null;

        GameObject ghost = Instantiate(source);
        foreach (var col in ghost.GetComponentsInChildren<Collider>(true))
            col.enabled = false;

        Conveyor belt = ghost.GetComponent<Conveyor>();
        if (belt == null && strokeBuilding.IsConveyor)
            belt = ghost.AddComponent<Conveyor>();
        if (belt != null)
            belt.PreparePreview(strokeBuilding);

        return ghost;
    }

    void CommitStroke()
    {
        if (!strokeActive)
            return;

        bool lineOk = strokeSlots.Count > 0;
        for (int i = 0; i < strokeSlots.Count; i++)
        {
            if (!strokeSlots[i].valid)
            {
                lineOk = false;
                break;
            }
        }

        if (!lineOk)
        {
            EndStroke();
            return;
        }

        int lineCost = Economy.BuildCost(currentBuildingData) * strokeSlots.Count;
        if (PlayerWallet.Instance != null && !PlayerWallet.Instance.CanAfford(lineCost))
        {
            EndStroke();
            return;
        }

        Quaternion rot = Quaternion.Euler(0f, strokeYaw, 0f);
        for (int i = 0; i < strokeSlots.Count; i++)
            SpawnAt(strokeSlots[i].pos, rot);

        EndStroke();
    }

    void SpawnAt(Vector3 placePos, Quaternion placeRot)
    {
        if (currentBuildingData == null || currentBuildingData.prefab == null)
            return;

        int cost = Economy.BuildCost(currentBuildingData);
        if (PlayerWallet.Instance != null && !PlayerWallet.Instance.TrySpendCoins(cost))
            return;

        GameObject go = Instantiate(currentBuildingData.prefab, placePos, placeRot);
        go.name = go.name + $"{indexBuilding}";
        indexBuilding += 1;

        BuildingBase buildingBase = go.GetComponent<BuildingBase>();
        if (buildingBase != null)
        {
            buildingBase.data = currentBuildingData;
            buildingBase.OnPlaced();
        }
        else
        {
            RegisterGenericOnGrid(go);
        }
    }

    void RegisterGenericOnGrid(GameObject obj)
    {
        if (obj == null || GridSystem.Instance == null)
            return;

        Vector2Int size = GetPlacementSize(obj.transform.rotation);
        GridFootprint.Register(obj, obj.transform.position, size);
    }

    void TryDemolish()
    {
        if (playerCamera == null)
            return;

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, maxBuildDistance, demolishLayer))
            return;

        BuildingBase building = hit.collider.GetComponentInParent<BuildingBase>();
        if (building == null)
            return;

        building.OnRemoved();
        Destroy(building.gameObject);
    }
}
