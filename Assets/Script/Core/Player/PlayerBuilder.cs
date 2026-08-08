using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerBuilder : MonoBehaviour
{
    [Header("References")]
    public PlayerInventory inventory;
    public Camera playerCamera;
    public LayerMask buildLayer = ~0;          // на что можно ставить (земля + здания)
    public LayerMask obstacleLayer = ~0;       // что мешает ставить

    [Header("Settings")]
    public float maxBuildDistance = 12f;
    public float gridSize = 1f;
    public Material ghostValidMaterial;
    public Material ghostInvalidMaterial;
    public Key rotateKey = Key.R;              // на будущее, если захочешь через Input System

    private InputSystem_Actions inputActions;
    private GameObject currentGhost;
    private BuildingData currentBuildingData;
    private bool isBuildMode = true;           // сразу включен для удобства
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
        // Можно добавить отдельную кнопку для удаления (например Right Click)
    }

    void OnDisable()
    {
        inputActions.Player.Attack.performed -= OnPlace;
        inputActions.Disable();
        DestroyGhost();
    }

    void Update()
    {
        // Переключение слота hotbar'а уже обрабатывается в Inventory
        BuildingData selected = inventory.GetSelectedBuilding();

        if (selected != currentBuildingData)
        {
            currentBuildingData = selected;
            RecreateGhost();
        }

        if (isBuildMode && currentBuildingData != null)
        {
            UpdateGhost();
        }
        else
        {
            DestroyGhost();
        }

        // Поворот здания (пока через старый Input, потом можно перевести)
        if (Keyboard.current != null && Keyboard.current[rotateKey].wasPressedThisFrame)
        {
            currentRotationY += 90f;
            if (currentRotationY >= 360f) currentRotationY = 0f;
        }
    }

    void RecreateGhost()
    {
        DestroyGhost();

        if (currentBuildingData == null || currentBuildingData.ghostPrefab == null)
            return;

        currentGhost = Instantiate(currentBuildingData.ghostPrefab);
        // Отключаем коллайдеры у ghost
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

    void UpdateGhost()
    {
        if (currentGhost == null) return;

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, maxBuildDistance, buildLayer))
        {
            Vector3 placePos = SnapToGrid(hit.point);

            currentGhost.transform.position = placePos;
            currentGhost.transform.rotation = Quaternion.Euler(0f, currentRotationY, 0f);

            canPlace = IsPlacementValid(placePos, currentGhost.transform.rotation);

            // Меняем материал ghost'а
            var renderers = currentGhost.GetComponentsInChildren<Renderer>();
            Material mat = canPlace ? ghostValidMaterial : ghostInvalidMaterial;
            foreach (var r in renderers)
            {
                if (mat != null)
                    r.material = mat;
            }
        }
        else
        {
            // Прячем ghost, если смотрим в небо
            currentGhost.SetActive(false);
            canPlace = false;
            return;
        }

        currentGhost.SetActive(true);
    }

    Vector3 SnapToGrid(Vector3 position)
    {
        float x = Mathf.Round(position.x / gridSize) * gridSize;
        float z = Mathf.Round(position.z / gridSize) * gridSize;
        // Y оставляем как есть (или можно тоже снапить, если нужно)
        return new Vector3(x, position.y, z);
    }

    bool IsPlacementValid(Vector3 position, Quaternion rotation)
    {
        // Простая проверка: нет ли коллайдеров в объёме здания
        // Для начала используем небольшой бокс. Потом можно брать размер из BuildingData.

        Vector3 halfExtents = new Vector3(0.45f, 0.9f, 0.45f); // подгони под свои модели
        Collider[] overlaps = Physics.OverlapBox(position + Vector3.up * halfExtents.y, halfExtents, rotation, obstacleLayer);

        return overlaps.Length == 0;
    }

    void OnPlace(InputAction.CallbackContext ctx)
    {
        if (!isBuildMode || currentBuildingData == null || currentGhost == null || !canPlace)
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

    // Можно вызвать из UI или по кнопке
    public void SetBuildMode(bool enabled)
    {
        isBuildMode = enabled;
        if (!enabled) DestroyGhost();
    }
}