using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerBuilder : MonoBehaviour
{
    [Header("References")]
    public PlayerInventory inventory;
    public PlayerMovement playerMovement;
    public BuildMenuUI buildMenuUI;
    public Camera playerCamera;
    public LayerMask buildLayer = ~0;        // Для рейкаста (поверхность)
    public LayerMask collisionLayer = ~0;    // Для проверки коллизий (препятствия)
    public LayerMask demolishLayer = ~0;

    [Header("Settings")]
    public float maxBuildDistance = 12f;
    public Material ghostValidMaterial;
    public Material ghostInvalidMaterial;
    public Key rotateKey = Key.R;
    public Key buildMenuKey = Key.B;

    /// <summary>Build Mode включён и выбран building из hotbar — показываем ghost.</summary>
    public bool IsBuildModeActive => isBuildMode && currentBuildingData != null;
    public bool HasPlacementTarget { get; private set; }
    public Vector3 CurrentPlacementPosition { get; private set; }
    public bool CanPlaceCurrent { get; private set; }

    /// <summary>true = можно ставить из hotbar; false = строительство полностью выключено.</summary>
    public bool isBuildMode = false;

    private InputSystem_Actions inputActions;
    private GameObject currentGhost;
    private BuildingData currentBuildingData;
    private float currentRotationY = 0f;
    private bool canPlace = false;
    private int indexBuilding = 0;

    // Ghost preview: last resolved conveyor shape
    private bool ghostIsCorner;
    private bool ghostMirrorX;
    private GameObject ghostPrefabSource;

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

        // Hotbar / выбор здания — всегда (меню и режим не блокируют колёсико)
        BuildingData selected = inventory != null ? inventory.GetSelectedBuilding() : null;

        if (selected != currentBuildingData)
        {
            currentBuildingData = selected;
            RecreateGhost();
        }

        if (IsBuildModeActive)
        {
            // Ghost только в build mode; при открытом меню не мешаем UI
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

    /// <summary>
    /// R: ghost → +90°; луч на стоящий объект → rotate in place (§4.2 промт).
    /// </summary>
    void HandleRotateKey()
    {
        // Сначала пробуем повернуть уже стоящий объект под прицелом
        if (TryRotatePlacedObject())
            return;

        // Иначе крутим ghost
        currentRotationY += 90f;
        if (currentRotationY >= 360f)
            currentRotationY = 0f;

        if (currentGhost != null)
            currentGhost.transform.rotation = Quaternion.Euler(0f, currentRotationY, 0f);
    }

    bool TryRotatePlacedObject()
    {
        if (playerCamera == null || GridSystem.Instance == null)
            return false;

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, maxBuildDistance, demolishLayer))
            return false;

        GameObject target = FindDemolishTarget(hit.collider);
        if (target == null)
            return false;

        ConveyorBelt belt = target.GetComponent<ConveyorBelt>();
        if (belt != null)
        {
            RotatePlacedBelt(belt);
            return true;
        }

        BuildingBase building = target.GetComponent<BuildingBase>();
        if (building != null)
        {
            RotatePlacedBuilding(building);
            return true;
        }

        return false;
    }

    void RotatePlacedBelt(ConveyorBelt belt)
    {
        if (belt == null) return;

        // Предметы сбрасываем (допустимо по §4.2)
        belt.ClearItems();
        AutoConnector.ClearBeltLinks(belt);

        float yaw = ConveyorPlacement.NormalizeYaw(belt.transform.eulerAngles.y + 90f);
        belt.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        // Сбросить mirror после поворота (иначе направления плывут)
        Vector3 scale = belt.transform.localScale;
        scale.x = Mathf.Abs(scale.x);
        belt.transform.localScale = scale;

        // Straight: оставить форму. Corner: остаётся corner, yaw+90.
        // Occupancy 1x1 — re-register на всякий
        belt.OnRemoved();
        belt.OnPlaced();

        ConveyorReshape.TryReshapeNeighborsAfterPlace(belt.gameObject);
        AutoConnector.ReconnectObject(belt.gameObject);
    }

    void RotatePlacedBuilding(BuildingBase building)
    {
        if (building == null) return;

        // Буферы НЕ трогаем (§4.2)
        AutoConnector.ClearBuildingLinks(building);

        float prevYaw = building.transform.eulerAngles.y;
        float newYaw = prevYaw + 90f;
        building.transform.rotation = Quaternion.Euler(0f, newYaw, 0f);

        // size может смениться на 90° — перерегистрация
        if (GridSystem.Instance != null && building.data != null)
        {
            Vector2Int origin = GridSystem.Instance.WorldToCell(building.transform.position);
            Vector2Int newSize = GridOccupancy.GetRotatedSize(building.data.size, newYaw);

            GridOccupancy.Unregister(building.gameObject);
            if (!GridOccupancy.IsAreaFree(origin, newSize))
            {
                building.transform.rotation = Quaternion.Euler(0f, prevYaw, 0f);
                building.ReRegisterOnGrid();
                AutoConnector.ReconnectObject(building.gameObject);
                Debug.LogWarning($"[Builder] Rotate blocked: footprint occupied for {building.name}");
                return;
            }

            GridOccupancy.Register(building.gameObject, origin, newSize);
        }
        else
        {
            building.ReRegisterOnGrid();
        }

        ConveyorReshape.TryReshapeNeighborsAfterPlace(building.gameObject);
        AutoConnector.ReconnectObject(building.gameObject);
    }

    void HandleBuildModeToggle()
    {
        if (Keyboard.current == null || !Keyboard.current[buildMenuKey].wasPressedThisFrame)
            return;

        // Цикл: выкл → меню (build on) → build без меню → выкл
        if (!isBuildMode)
        {
            EnterBuildMode(openMenu: true);
        }
        else if (buildMenuUI != null && buildMenuUI.IsOpen)
        {
            // Меню открыто → закрыть меню, остаться в build mode (hotbar placement)
            buildMenuUI.CloseMenu(restorePlayerControl: true);
        }
        else
        {
            ExitBuildMode();
        }
    }

    public void EnterBuildMode(bool openMenu)
    {
        isBuildMode = true;

        if (openMenu && buildMenuUI != null)
            buildMenuUI.OpenMenu();
        else
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

    /// <summary>
    /// Вызывается из BuildMenuUI при выборе здания: меню закрыто, build mode остаётся.
    /// </summary>
    public void OnBuildMenuClosedAfterSelection()
    {
        // isBuildMode остаётся true — можно ставить из hotbar
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

        GameObject ghostSource = currentBuildingData.ghostPrefab;
        if (ghostSource == null && currentBuildingData.IsConveyor)
            ghostSource = currentBuildingData.GetDefaultPlacePrefab();
        if (ghostSource == null)
            ghostSource = currentBuildingData.prefab;
        if (ghostSource == null)
            return;

        SpawnGhostFromPrefab(ghostSource, isCorner: false, mirrorX: false);
    }

    void SpawnGhostFromPrefab(GameObject ghostSource, bool isCorner, bool mirrorX)
    {
        if (ghostSource == null)
            return;

        if (currentGhost != null)
            Destroy(currentGhost);

        ghostPrefabSource = ghostSource;
        ghostIsCorner = isCorner;
        ghostMirrorX = mirrorX;

        currentGhost = Instantiate(ghostSource);

        foreach (var col in currentGhost.GetComponentsInChildren<Collider>())
            col.enabled = false;

        var belt = currentGhost.GetComponent<ConveyorBelt>();
        if (belt != null)
            belt.enabled = false;

        ApplyGhostMirror(mirrorX);
    }

    void ApplyGhostMirror(bool mirrorX)
    {
        if (currentGhost == null) return;
        Vector3 scale = currentGhost.transform.localScale;
        float ax = Mathf.Abs(scale.x);
        if (ax < 0.0001f) ax = 1f;
        scale.x = mirrorX ? -ax : ax;
        currentGhost.transform.localScale = scale;
    }

    void DestroyGhost()
    {
        if (currentGhost != null)
        {
            Destroy(currentGhost);
            currentGhost = null;
        }
        ghostPrefabSource = null;
        ghostIsCorner = false;
        ghostMirrorX = false;
    }

    void ClearPlacementTarget()
    {
        HasPlacementTarget = false;
        CanPlaceCurrent = false;
    }

    void UpdateGhost()
    {
        if (playerCamera == null)
        {
            if (currentGhost != null)
                currentGhost.SetActive(false);
            ClearPlacementTarget();
            return;
        }

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, maxBuildDistance, buildLayer))
        {
            Vector3 placePos = SnapToGrid(hit.point);
            float yaw = currentRotationY;
            bool mirrorX = false;

            // Conveyor: preview straight/corner + mirror
            if (currentBuildingData != null && currentBuildingData.IsConveyor && GridSystem.Instance != null)
            {
                Vector2Int cell = GridSystem.Instance.WorldToCell(placePos);
                var resolved = ConveyorPlacement.Resolve(currentBuildingData, cell, currentRotationY);

                GameObject desiredPrefab = resolved.prefab;
                if (desiredPrefab == null)
                    desiredPrefab = currentBuildingData.GetDefaultPlacePrefab();

                // Corner ghost prefab optional
                if (resolved.isCorner && currentBuildingData.cornerGhostPrefab != null)
                    desiredPrefab = currentBuildingData.cornerGhostPrefab;
                else if (!resolved.isCorner && currentBuildingData.ghostPrefab != null)
                    desiredPrefab = currentBuildingData.ghostPrefab;

                if (desiredPrefab != null
                    && (currentGhost == null
                        || ghostPrefabSource != desiredPrefab
                        || ghostIsCorner != resolved.isCorner
                        || ghostMirrorX != resolved.mirrorX))
                {
                    SpawnGhostFromPrefab(desiredPrefab, resolved.isCorner, resolved.mirrorX);
                }
                else if (currentGhost != null && ghostMirrorX != resolved.mirrorX)
                {
                    ghostMirrorX = resolved.mirrorX;
                    ApplyGhostMirror(resolved.mirrorX);
                }

                yaw = resolved.rotationY;
                mirrorX = resolved.mirrorX;
                ghostIsCorner = resolved.isCorner;
                ghostMirrorX = mirrorX;
            }
            else if (currentGhost == null)
            {
                RecreateGhost();
            }

            if (currentGhost == null)
            {
                ClearPlacementTarget();
                return;
            }

            currentGhost.transform.position = placePos;
            currentGhost.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (currentBuildingData != null && currentBuildingData.IsConveyor)
                ApplyGhostMirror(mirrorX);

            canPlace = IsPlacementValid(placePos, currentGhost.transform.rotation);

            var renderers = currentGhost.GetComponentsInChildren<Renderer>();
            Material mat = canPlace ? ghostValidMaterial : ghostInvalidMaterial;
            foreach (var r in renderers)
            {
                if (mat != null)
                    r.material = mat;
            }

            CurrentPlacementPosition = placePos;
            HasPlacementTarget = true;
            CanPlaceCurrent = canPlace;
            currentGhost.SetActive(true);
        }
        else
        {
            if (currentGhost != null)
                currentGhost.SetActive(false);
            canPlace = false;
            ClearPlacementTarget();
        }
    }

    Vector3 SnapToGrid(Vector3 position)
    {
        if (GridSystem.Instance != null)
            return GridSystem.Instance.SnapToGrid(position);

        float cellSize = 1f;
        float x = Mathf.Round(position.x / cellSize) * cellSize;
        float z = Mathf.Round(position.z / cellSize) * cellSize;
        return new Vector3(x, position.y, z);
    }

    bool IsPlacementValid(Vector3 position, Quaternion rotation)
    {
        // 1) Проверка через словарь клеток (основная)
        if (GridSystem.Instance != null)
        {
            Vector2Int origin = GridSystem.Instance.WorldToCell(position);
            Vector2Int size_ = GetPlacementSize(rotation);

            if (!GridOccupancy.IsAreaFree(origin, size_))
                return false;
        }

        // 2) Доп. OverlapBox (AABB по сетке): твёрдые препятствия, не пол / ResourceNode
        Vector2Int size = GetPlacementSize(rotation);
        float cell = GridSystem.Instance != null ? GridSystem.Instance.cellSize : 1f;
        Vector3 halfExtents = new Vector3(
            Mathf.Max(0.1f, size.x * cell * 0.5f - 0.01f),
            0.9f,
            Mathf.Max(0.1f, size.y * cell * 0.5f - 0.01f)
        );
        // Центр площади от origin-клетки вдоль +X/+Z (как в GridOccupancy)
        Vector3 areaCenter = position
            + new Vector3((size.x - 1) * cell * 0.5f, halfExtents.y, (size.y - 1) * cell * 0.5f);

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
            if (overlap == null)
                continue;

            if (currentGhost != null && overlap.transform.IsChildOf(currentGhost.transform))
                continue;

            // Пол и ResourceNode не блокируют
            if (IsNonBlockingCollider(overlap))
                continue;

            return false;
        }

        return true;
    }

    Vector2Int GetPlacementSize(Quaternion rotation)
    {
        Vector2Int size = currentBuildingData != null ? currentBuildingData.size : Vector2Int.one;
        return GridOccupancy.GetRotatedSize(size, rotation.eulerAngles.y);
    }

    static bool IsNonBlockingCollider(Collider col)
    {
        if (col == null)
            return true;

        // ResourceNode не занимает клетку для строительства (экстрактор ставится поверх)
        if (col.GetComponentInParent<ResourceNode>() != null)
            return true;

        // Floor layer
        int floorLayer = LayerMask.NameToLayer("Floor");
        if (floorLayer != -1 && col.gameObject.layer == floorLayer)
            return true;

        // Частые имена поверхности
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

        // Повторная проверка на момент клика
        if (!IsPlacementValid(currentGhost.transform.position, currentGhost.transform.rotation))
            return;

        Vector3 placePos = currentGhost.transform.position;
        Quaternion placeRot = currentGhost.transform.rotation;
        GameObject prefabToSpawn = currentBuildingData.prefab;
        bool conveyorCorner = false;
        bool conveyorMirrorX = false;

        // --- Conveyor: auto straight / corner ---
        if (currentBuildingData.IsConveyor && GridSystem.Instance != null)
        {
            Vector2Int cell = GridSystem.Instance.WorldToCell(placePos);
            var resolved = ConveyorPlacement.Resolve(currentBuildingData, cell, currentRotationY);

            if (resolved.prefab != null)
                prefabToSpawn = resolved.prefab;

            placeRot = Quaternion.Euler(0f, resolved.rotationY, 0f);
            conveyorCorner = resolved.isCorner;
            conveyorMirrorX = resolved.mirrorX;
        }

        if (prefabToSpawn == null)
            return;

        GameObject go = Instantiate(prefabToSpawn, placePos, placeRot);
        go.name = go.name + $"{indexBuilding}";
        indexBuilding += 1;

        if (conveyorMirrorX)
        {
            Vector3 scale = go.transform.localScale;
            scale.x = -Mathf.Abs(scale.x);
            go.transform.localScale = scale;
        }

        // 1) Register only
        var buildingBase = go.GetComponent<BuildingBase>();
        var belt = go.GetComponent<ConveyorBelt>();

        if (buildingBase != null)
        {
            buildingBase.data = currentBuildingData;
            buildingBase.OnPlaced();
        }
        else if (belt != null)
        {
            belt.buildingData = currentBuildingData;
            ConveyorReshape.DefaultConveyorData = currentBuildingData;
            belt.isCorner = conveyorCorner;
            belt.OnPlaced();
        }
        else
        {
            RegisterGenericOnGrid(go);
        }

        // 2) Reshape neighbors (root сам сделает ReconnectObject)
        ConveyorReshape.TryReshapeNeighborsAfterPlace(go);
        // 3) Повторный reconnect идемпотентен (на случай early-return reshape)
        AutoConnector.ReconnectObject(go);
    }

    void RegisterGenericOnGrid(GameObject obj)
    {
        if (obj == null || GridSystem.Instance == null)
            return;

        Vector2Int origin = GridSystem.Instance.WorldToCell(obj.transform.position);
        Vector2Int size = GetPlacementSize(obj.transform.rotation);
        GridOccupancy.Register(obj, origin, size);
    }

    void TryDemolish()
    {
        if (playerCamera == null)
            return;

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, maxBuildDistance, demolishLayer))
            return;

        GameObject target = FindDemolishTarget(hit.collider);
        if (target == null)
            return;

        DemolishObject(target);
    }

    void DemolishObject(GameObject target)
    {
        if (target == null) return;

        // Снимок клеток ДО unregister
        var freedCells = ConveyorReshape.GetOccupiedCells(target);

        ConveyorBelt belt = target.GetComponent<ConveyorBelt>();
        if (belt != null)
        {
            belt.ClearItems();
            AutoConnector.ClearBeltLinks(belt);
            belt.OnRemoved();
            belt.suppressDestroyCleanup = true; // не трогать соседей в OnDestroy
            Destroy(target);

            AutoConnector.ReconnectCells(freedCells);
            return;
        }

        BuildingBase building = target.GetComponent<BuildingBase>();
        if (building != null)
        {
            building.OnRemoved(); // clear links + unregister
            Destroy(target);

            AutoConnector.ReconnectCells(freedCells);
        }
    }

    static GameObject FindDemolishTarget(Collider collider)
    {
        if (collider == null)
            return null;

        ConveyorBelt belt = collider.GetComponentInParent<ConveyorBelt>();
        if (belt != null)
            return belt.gameObject;

        BuildingBase building = collider.GetComponentInParent<BuildingBase>();
        if (building != null)
            return building.gameObject;

        return null;
    }
}
