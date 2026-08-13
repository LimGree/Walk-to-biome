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
    public Key rotateKey = Key.R;
    public Key buildMenuKey = Key.B;

    public bool IsBuildModeActive => isBuildMode && currentBuildingData != null;
    public bool HasPlacementTarget { get; private set; }
    public Vector3 CurrentPlacementPosition { get; private set; }
    public bool CanPlaceCurrent { get; private set; }
    public Vector2Int CurrentFootprintSize { get; private set; } = Vector2Int.one;

    public bool isBuildMode = false;

    private InputSystem_Actions inputActions;
    private GameObject currentGhost;
    private BuildingData currentBuildingData;
    private float currentRotationY = 0f;
    private bool canPlace = false;
    private int indexBuilding = 0;

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
        inputActions.Player.Attack.performed += OnPlace;
    }

    void OnDisable()
    {
        inputActions.Player.Attack.performed -= OnPlace;
        inputActions.Disable();
        DestroyGhost();
        ClearPlacementTarget();
    }

    void Update()
    {
        HandleBuildModeToggle();

        BuildingData selected = inventory != null ? inventory.GetSelectedBuilding() : null;
        if (selected != currentBuildingData)
        {
            currentBuildingData = selected;
            RecreateGhost();
        }

        if (IsBuildModeActive)
        {
            if (buildMenuUI != null && buildMenuUI.IsOpen)
            {
                if (currentGhost != null)
                    currentGhost.SetActive(false);
                ClearPlacementTarget();
            }
            else
            {
                UpdateGhost();
            }
        }
        else
        {
            DestroyGhost();
            ClearPlacementTarget();
        }

        if (isBuildMode
            && (buildMenuUI == null || !buildMenuUI.IsOpen)
            && Keyboard.current != null
            && Keyboard.current[rotateKey].wasPressedThisFrame)
        {
            HandleRotateKey();
        }

        if (isBuildMode
            && (buildMenuUI == null || !buildMenuUI.IsOpen)
            && Mouse.current != null
            && Mouse.current.rightButton.wasPressedThisFrame)
        {
            TryDemolish();
        }
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

    void HandleBuildModeToggle()
    {
        if (Keyboard.current == null || !Keyboard.current[buildMenuKey].wasPressedThisFrame)
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
    }

    public void ExitBuildMode()
    {
        isBuildMode = false;
        DestroyGhost();
        ClearPlacementTarget();

        if (buildMenuUI != null && buildMenuUI.IsOpen)
            buildMenuUI.CloseMenu(restorePlayerControl: false);

        ApplyGameplayCursorAndControl(buildMenuOpen: false);
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

        GameObject ghostSource = currentBuildingData.prefab;
        if (!currentBuildingData.IsConveyor && currentBuildingData.ghostPrefab != null)
            ghostSource = currentBuildingData.ghostPrefab;
        if (ghostSource == null)
            ghostSource = currentBuildingData.prefab;
        if (ghostSource == null)
            return;

        currentGhost = Instantiate(ghostSource);

        foreach (var col in currentGhost.GetComponentsInChildren<Collider>())
            col.enabled = false;

        Conveyor belt = currentGhost.GetComponent<Conveyor>();
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

    void OnPlace(InputAction.CallbackContext ctx)
    {
        if (!IsBuildModeActive || currentGhost == null || !canPlace)
            return;

        if (buildMenuUI != null && buildMenuUI.IsOpen)
            return;

        if (ResearchSystem.Instance != null
            && !ResearchSystem.Instance.IsBuildingUnlocked(currentBuildingData))
        {
            Debug.LogWarning($"[Builder] Locked: {currentBuildingData.displayName}");
            return;
        }

        if (IsResearchLabData(currentBuildingData)
            && ResearchSystem.Instance != null
            && !ResearchSystem.Instance.CanPlaceAnotherLab())
        {
            Debug.LogWarning(
                $"[Builder] Research Lab limit: {ResearchSystem.Instance.CountPlacedLabs()}/" +
                $"{ResearchSystem.Instance.GetMaxResearchLabs()}");
            return;
        }

        if (!IsPlacementValid(currentGhost.transform.position, currentGhost.transform.rotation))
            return;

        Vector3 placePos = currentGhost.transform.position;
        Quaternion placeRot = currentGhost.transform.rotation;
        GameObject prefabToSpawn = currentBuildingData.prefab;
        if (prefabToSpawn == null)
            return;

        GameObject go = Instantiate(prefabToSpawn, placePos, placeRot);
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
