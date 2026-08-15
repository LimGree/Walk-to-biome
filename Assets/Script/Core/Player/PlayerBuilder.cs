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

    private InputSystem_Actions inputActions;
    private GameObject currentGhost;
    private BuildingData currentBuildingData;
    private float currentRotationY = 0f;
    private bool canPlace = false;
    private int indexBuilding = 0;

    bool strokeActive;
    BuildingData strokeBuilding;
    float strokeYaw;
    Vector2Int strokeSize = Vector2Int.one;
    Vector2Int strokeStartMin;
    Vector2Int? strokeAxis;
    readonly HashSet<Vector2Int> strokePlacedMins = new HashSet<Vector2Int>();
    const int MaxLineBuildings = 64;

    void Awake()
    {
        inputActions = new InputSystem_Actions();

        if (inventory == null)
            inventory = GetComponent<PlayerInventory>();

        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>();

        if (playerCamera == null)
            playerCamera = Camera.main;

        if (buildMenuUI == null)
            buildMenuUI = FindFirstObjectByType<BuildMenuUI>();
    }

    void OnEnable()
    {
        inputActions.Enable();
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
        inputActions.Disable();
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

        if (IsBuildModeActive)
        {
            if (buildMenuUI != null && buildMenuUI.IsOpen)
            {
                EndStroke();
                if (currentGhost != null)
                    currentGhost.SetActive(false);
                ClearPlacementTarget();
            }
            else
            {
                if (strokeActive)
                    currentRotationY = strokeYaw;

                UpdateGhost();
                TickLineStroke();
            }
        }
        else
        {
            EndStroke();
            DestroyGhost();
            ClearPlacementTarget();
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
        return false;
    }

    void OnRotate(InputAction.CallbackContext ctx)
    {
        if (!isBuildMode || strokeActive || IsGameplayBuildInputBlocked())
            return;
        HandleRotateKey();
    }

    void OnDemolish(InputAction.CallbackContext ctx)
    {
        if (!isBuildMode || IsGameplayBuildInputBlocked())
            return;
        EndStroke();
        TryDemolish();
    }

    void HandleRotateKey()
    {
        if (TryRotatePlacedBuilding())
            return;

        currentRotationY += 90f;
        if (currentRotationY >= 360f)
            currentRotationY = 0f;

        if (currentGhost != null)
            currentGhost.transform.rotation = Quaternion.Euler(0f, currentRotationY, 0f);
    }

    bool TryRotatePlacedBuilding()
    {
        if (playerCamera == null || GridSystem.Instance == null)
            return false;

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, maxBuildDistance, demolishLayer))
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
        if (currentGhost != null)
        {
            Destroy(currentGhost);
            currentGhost = null;
        }
    }

    void ClearPlacementTarget()
    {
        HasPlacementTarget = false;
        CanPlaceCurrent = false;
        CurrentFootprintSize = Vector2Int.one;
    }

    void UpdateGhost()
    {
        if (currentGhost == null)
        {
            if (currentBuildingData != null)
                RecreateGhost();
            if (currentGhost == null)
            {
                ClearPlacementTarget();
                return;
            }
        }

        if (playerCamera == null)
        {
            currentGhost.SetActive(false);
            ClearPlacementTarget();
            return;
        }

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, maxBuildDistance, buildLayer))
        {
            Vector3 placePos = SnapToGrid(hit.point);
            currentGhost.transform.position = placePos;
            currentGhost.transform.rotation = Quaternion.Euler(0f, currentRotationY, 0f);

            Conveyor ghostBelt = currentGhost.GetComponent<Conveyor>();
            if (ghostBelt != null)
                ghostBelt.Preview(placePos, currentGhost.transform.rotation);

            canPlace = IsPlacementValid(placePos, currentGhost.transform.rotation);

            Material mat = canPlace ? ghostValidMaterial : ghostInvalidMaterial;
            if (mat != null)
            {
                foreach (var r in currentGhost.GetComponentsInChildren<Renderer>())
                    r.material = mat;
            }

            CurrentPlacementPosition = placePos;
            CurrentFootprintSize = GetPlacementSize(currentGhost.transform.rotation);
            HasPlacementTarget = true;
            CanPlaceCurrent = canPlace;
            currentGhost.SetActive(true);
        }
        else
        {
            currentGhost.SetActive(false);
            canPlace = false;
            ClearPlacementTarget();
        }
    }

    Vector3 SnapToGrid(Vector3 position)
    {
        Vector2Int size = GetPlacementSize(Quaternion.Euler(0f, currentRotationY, 0f));
        if (GridSystem.Instance != null)
            return GridSystem.Instance.SnapFootprintCenter(position, size);
        return GridFootprint.SnapCenter(position, size);
    }

    bool IsPlacementValid(Vector3 position, Quaternion rotation)
    {
        Vector2Int size = GetPlacementSize(rotation);

        if (GridSystem.Instance != null)
        {
            Vector2Int minCell = GridFootprint.GetMinCell(position, size);
            if (!GridOccupancy.IsAreaFree(minCell, size))
                return false;
        }

        if (IsResearchLabData(currentBuildingData)
            && ResearchSystem.Instance != null
            && !ResearchSystem.Instance.CanPlaceAnotherLab())
        {
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
            if (currentGhost != null && overlap.transform.IsChildOf(currentGhost.transform))
                continue;
            if (IsNonBlockingCollider(overlap))
                continue;
            return false;
        }

        return true;
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
        if (IsGameplayBuildInputBlocked() || !IsBuildModeActive || !HasPlacementTarget)
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
        TrySpawnAt(CurrentPlacementPosition);
    }

    void OnPlaceCanceled(InputAction.CallbackContext ctx)
    {
        EndStroke();
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
        strokePlacedMins.Clear();
    }

    void EndStroke()
    {
        strokeActive = false;
        strokeBuilding = null;
        strokeAxis = null;
        strokePlacedMins.Clear();
    }

    void TickLineStroke()
    {
        if (!strokeActive)
            return;

        if (!inputActions.Player.Place.IsPressed
            || IsGameplayBuildInputBlocked()
            || !IsBuildModeActive
            || currentBuildingData != strokeBuilding)
        {
            EndStroke();
            return;
        }

        currentRotationY = strokeYaw;
        if (!HasPlacementTarget)
            return;

        Vector2Int currentMin = GridFootprint.GetMinCell(CurrentPlacementPosition, strokeSize);
        Vector2Int delta = currentMin - strokeStartMin;

        if (!strokeAxis.HasValue)
        {
            if (Mathf.Abs(delta.x) < strokeSize.x && Mathf.Abs(delta.y) < strokeSize.y)
                return;

            strokeAxis = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y)
                ? new Vector2Int(1, 0)
                : new Vector2Int(0, 1);
        }

        int axisDelta = strokeAxis.Value.x != 0 ? delta.x : delta.y;
        int step = strokeAxis.Value.x != 0 ? strokeSize.x : strokeSize.y;
        int dir = axisDelta >= 0 ? 1 : -1;
        int extra = Mathf.Abs(axisDelta) / step;
        extra = Mathf.Min(extra, MaxLineBuildings - 1);

        SnapGhostToLineSlot(extra, dir);

        for (int i = 0; i <= extra; i++)
        {
            Vector2Int min = LineMinAt(i, dir, step);
            if (strokePlacedMins.Contains(min))
                continue;

            Vector3 pos = GridFootprint.MinCellToCenter(min, strokeSize, CurrentPlacementPosition.y);
            if (!TrySpawnAt(pos)
                && IsResearchLabData(strokeBuilding)
                && ResearchSystem.Instance != null
                && !ResearchSystem.Instance.CanPlaceAnotherLab())
            {
                EndStroke();
                return;
            }
        }
    }

    Vector2Int LineMinAt(int index, int dir, int step)
    {
        return strokeStartMin + new Vector2Int(
            strokeAxis.Value.x * index * step * dir,
            strokeAxis.Value.y * index * step * dir);
    }

    void SnapGhostToLineSlot(int extra, int dir)
    {
        if (currentGhost == null || !strokeAxis.HasValue)
            return;

        int step = strokeAxis.Value.x != 0 ? strokeSize.x : strokeSize.y;
        Vector2Int min = LineMinAt(extra, dir, step);
        Vector3 pos = GridFootprint.MinCellToCenter(min, strokeSize, currentGhost.transform.position.y);
        Quaternion rot = Quaternion.Euler(0f, strokeYaw, 0f);
        currentGhost.transform.SetPositionAndRotation(pos, rot);

        Conveyor ghostBelt = currentGhost.GetComponent<Conveyor>();
        if (ghostBelt != null)
            ghostBelt.Preview(pos, rot);

        canPlace = IsPlacementValid(pos, rot);
        Material mat = canPlace ? ghostValidMaterial : ghostInvalidMaterial;
        if (mat != null)
        {
            foreach (var r in currentGhost.GetComponentsInChildren<Renderer>())
                r.material = mat;
        }

        CurrentPlacementPosition = pos;
        CurrentFootprintSize = strokeSize;
        HasPlacementTarget = true;
        CanPlaceCurrent = canPlace;
    }

    bool TrySpawnAt(Vector3 placePos)
    {
        if (currentBuildingData == null || currentBuildingData.prefab == null)
            return false;

        Quaternion placeRot = Quaternion.Euler(0f, strokeActive ? strokeYaw : currentRotationY, 0f);
        Vector2Int size = GetPlacementSize(placeRot);
        Vector2Int min = GridFootprint.GetMinCell(placePos, size);

        if (strokeActive && strokePlacedMins.Contains(min))
            return false;

        if (IsResearchLabData(currentBuildingData)
            && ResearchSystem.Instance != null
            && !ResearchSystem.Instance.CanPlaceAnotherLab())
        {
            if (strokePlacedMins.Count == 0)
            {
                Debug.LogWarning(
                    $"[Builder] Research Lab limit: {ResearchSystem.Instance.CountPlacedLabs()}/" +
                    $"{ResearchSystem.Instance.GetMaxResearchLabs()}");
            }
            return false;
        }

        if (!IsPlacementValid(placePos, placeRot))
            return false;

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

        if (strokeActive)
            strokePlacedMins.Add(min);

        return true;
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
