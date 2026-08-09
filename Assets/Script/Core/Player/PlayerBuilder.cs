using UnityEngine;
using UnityEngine.InputSystem;
using System.Text;

public class PlayerBuilder : MonoBehaviour
{
    [Header("References")]
    public PlayerInventory inventory;
    public Camera playerCamera;
    public LayerMask buildLayer = ~0;        // Для рейкаста (поверхность)
    public LayerMask collisionLayer = ~0;    // Для проверки коллизий (препятствия)
    public LayerMask demolishLayer = ~0;

    [Header("Settings")]
    public float maxBuildDistance = 12f;
    public Material ghostValidMaterial;
    public Material ghostInvalidMaterial;
    public Key rotateKey = Key.R;

    public bool IsBuildModeActive => isBuildMode && currentBuildingData != null;
    public bool HasPlacementTarget { get; private set; }
    public Vector3 CurrentPlacementPosition { get; private set; }
    public bool CanPlaceCurrent { get; private set; }

    private InputSystem_Actions inputActions;
    private GameObject currentGhost;
    private BuildingData currentBuildingData;
    public bool isBuildMode = true;
    private float currentRotationY = 0f;
    private bool canPlace = false;

    private int indexBuilding = 0;

    void Awake()
    {
        inputActions = new InputSystem_Actions();

        if (inventory == null)
            inventory = GetComponent<PlayerInventory>();

        if (playerCamera == null)
            playerCamera = Camera.main;
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
        BuildingData selected = inventory.GetSelectedBuilding();

        if (selected != currentBuildingData)
        {
            currentBuildingData = selected;
            RecreateGhost();
        }

        if (IsBuildModeActive)
        {
            UpdateGhost();
        }
        else
        {
            DestroyGhost();
            ClearPlacementTarget();
        }

        if (Keyboard.current != null && Keyboard.current[rotateKey].wasPressedThisFrame)
        {
            currentRotationY += 90f;
            if (currentRotationY >= 360f)
                currentRotationY = 0f;

            // Если уже стоит ghost — просто поворачиваем его
            if (currentGhost != null)
                currentGhost.transform.rotation = Quaternion.Euler(0f, currentRotationY, 0f);

            // Если хочешь, чтобы уже установленные здания тоже можно было поворачивать —
            // это отдельная механика. Пока оставляем только при строительстве.
        }

        if (isBuildMode && Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
            TryDemolish();
    }

    void RecreateGhost()
    {
        DestroyGhost();

        if (currentBuildingData == null || currentBuildingData.ghostPrefab == null)
            return;

        currentGhost = Instantiate(currentBuildingData.ghostPrefab);

        foreach (var col in currentGhost.GetComponentsInChildren<Collider>())
            col.enabled = false;
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
    }

    void UpdateGhost()
    {
        if (currentGhost == null)
        {
            ClearPlacementTarget();
            return;
        }

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

        // Ищем поверхность для размещения
        if (Physics.Raycast(ray, out RaycastHit hit, maxBuildDistance, buildLayer))
        {
            Vector3 placePos = SnapToGrid(hit.point);

            currentGhost.transform.position = placePos;
            currentGhost.transform.rotation = Quaternion.Euler(0f, currentRotationY, 0f);

            // Проверяем коллизии отдельным LayerMask, который ИСКЛЮЧАЕТ пол
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
        Vector3 halfExtents = new Vector3(0.49f, 0.9f, 0.49f);

        // Включаем коллайдеры гостя
        Collider[] ghostColliders = null;
        if (currentGhost != null)
        {
            ghostColliders = currentGhost.GetComponentsInChildren<Collider>();
            foreach (var col in ghostColliders)
                col.enabled = true;
        }

        // ИСПОЛЬЗУЕМ collisionLayer, который НЕ содержит пол
        // Если collisionLayer не задан, используем buildLayer но исключаем пол
        LayerMask checkMask = collisionLayer;
        if (checkMask == 0) // Если не задан отдельный слой
        {
            // Исключаем слой пола из проверки
            int floorLayer = LayerMask.NameToLayer("Floor");
            if (floorLayer != -1)
            {
                checkMask = buildLayer & ~(1 << floorLayer);
            }
            else
            {
                checkMask = buildLayer;
            }
        }

        Collider[] overlaps = Physics.OverlapBox(
            position + Vector3.up * halfExtents.y,
            halfExtents,
            rotation,
            checkMask
        );

        // Выключаем коллайдеры гостя
        if (ghostColliders != null)
        {
            foreach (var col in ghostColliders)
                col.enabled = false;
        }

        // Проверяем, есть ли препятствия
        foreach (Collider overlap in overlaps)
        {
            if (currentGhost != null && overlap.transform.IsChildOf(currentGhost.transform))
                continue;

            return false;
        }

        return true;
    }

    void OnPlace(InputAction.CallbackContext ctx)
    {
        if (!IsBuildModeActive || currentGhost == null || !canPlace)
            return;

        GameObject building = Instantiate(
            currentBuildingData.prefab,
            currentGhost.transform.position,
            currentGhost.transform.rotation
        );
        building.name = building.name + $"{indexBuilding}";
        indexBuilding += 1;

        var buildingBase = building.GetComponent<BuildingBase>();
        if (buildingBase != null)
        {
            buildingBase.data = currentBuildingData;
            buildingBase.OnPlaced();
        }
        AutoConnector.TryAutoConnect(building);
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

        // Вызываем правильное удаление
        DemolishObject(target);
    }

    void DemolishObject(GameObject target)
    {
        if (target == null) return;

        // 1. Конвейер
        ConveyorBelt belt = target.GetComponent<ConveyorBelt>();
        if (belt != null)
        {
            // Очищаем предметы на ленте
            belt.ClearItems();

            // Отключаем все связи
            if (belt.connectedOutputSocket != null)
                belt.connectedOutputSocket.DisconnectBelt();

            if (belt.connectedInputSocket != null)
                belt.connectedInputSocket.DisconnectBelt();

            // Соседи, которые ссылались на эту ленту как nextBelt
            // (простая версия — потом можно сделать умнее)
            ConveyorBelt[] allBelts = FindObjectsByType<ConveyorBelt>(FindObjectsSortMode.None);
            foreach (var other in allBelts)
            {
                if (other != null && other.nextBelt == belt)
                    other.nextBelt = null;
            }

            Destroy(target);
            Debug.Log($"[Demolish] Удалён конвейер {target.name}");
            return;
        }

        // 2. Здание
        BuildingBase building = target.GetComponent<BuildingBase>();
        if (building != null)
        {
            // Вызываем OnRemoved (там уже есть отключение сокетов)
            building.OnRemoved();

            Destroy(target);
            Debug.Log($"[Demolish] Удалено здание {target.name}");
            return;
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

    public void SetBuildMode(bool enabled)
    {
        isBuildMode = enabled;
        if (!enabled)
        {
            DestroyGhost();
            ClearPlacementTarget();
        }
    }
}