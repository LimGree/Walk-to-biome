using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerBuilder : MonoBehaviour
{
    [Header("References")]
    public PlayerInventory inventory;
    public Camera playerCamera;
    public LayerMask buildLayer = ~0;
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
    private bool isBuildMode = true;
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

        if (Physics.Raycast(ray, out RaycastHit hit, maxBuildDistance, buildLayer))
        {
            Vector3 placePos = SnapToGrid(hit.point);

            currentGhost.transform.position = placePos;
            currentGhost.transform.rotation = Quaternion.Euler(0f, currentRotationY, 0f);

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
        Vector3 halfExtents = new Vector3(0.45f, 0.9f, 0.45f);
        Collider[] overlaps = Physics.OverlapBox(
            position + Vector3.up * halfExtents.y,
            halfExtents,
            rotation,
            buildLayer
        );

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

        if (ConnectionManager.Instance != null)
            ConnectionManager.Instance.OnPlaced(building);
    }

    void TryDemolish()
    {
        if (playerCamera == null || ConnectionManager.Instance == null)
            return;

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, maxBuildDistance, demolishLayer))
            return;

        GameObject target = FindDemolishTarget(hit.collider);
        if (target == null)
            return;

        ConnectionManager.Instance.RemovePlacedObject(target);
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
