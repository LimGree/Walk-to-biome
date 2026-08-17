using UnityEngine;

public class Extractor : BuildingBase, IInteractable
{
    [Header("Extractor Settings")]
    public ItemData resource;
    public float extractInterval = 1.2f;
    public int itemsPerCycle = 1;

    [Header("Upgrade")]
    public int level = 1;
    public float upgradedExtractInterval = 0.6f;
    public int upgradedItemsPerCycle = 1;

    [Header("Node detect")]
    public float nodeSearchRadius = 0.25f;
    public LayerMask resourceNodeLayer = ~0;

    [Header("Debug")]
    public bool showDebug = false;

    GameObject level1Visual;
    GameObject level2Visual;

    private float timer;
    private ResourceNode boundNode;
    private float nextFailLogTime;

    public bool CanUpgrade => level < 2;
    public override bool CanUpgradeBuilding => CanUpgrade;
    public float CurrentInterval => Mathf.Max(0.05f, extractInterval);
    public int CurrentItemsPerCycle => Mathf.Max(1, itemsPerCycle);

    void Awake()
    {
        EnsureCollider();
        BindVisuals();
        ApplyLevel(false);
    }

    public override void OnPlaced()
    {
        base.OnPlaced();
        timer = 0f;
        EnsureCollider();
        BindVisuals();
        ApplyLevel(false);
        BindToNearbyNode();
    }

    public override void OnRotated()
    {
        base.OnRotated();
        BindToNearbyNode();
    }

    public bool TryUpgrade()
    {
        return TryUpgradeBuilding();
    }

    public override bool TryUpgradeBuilding()
    {
        if (!CanUpgrade)
            return false;

        level = 2;
        ApplyLevel(true);
        return true;
    }

    public override int ReadLevel()
    {
        return level;
    }

    public override void ApplyLevel(int savedLevel)
    {
        if (savedLevel < 2)
            return;
        level = 2;
        ApplyLevel(false);
    }

    void ApplyLevel(bool resetTimer)
    {
        if (level >= 2)
        {
            extractInterval = Mathf.Max(0.05f, upgradedExtractInterval);
            itemsPerCycle = Mathf.Max(1, upgradedItemsPerCycle);
        }

        if (level1Visual != null)
            level1Visual.SetActive(level < 2);
        if (level2Visual != null)
            level2Visual.SetActive(level >= 2);

        if (resetTimer)
            timer = 0f;
    }

    void BindVisuals()
    {
        if (level1Visual == null)
            level1Visual = FindNamedChild("extractor_level_1");
        if (level2Visual == null)
            level2Visual = FindNamedChild("extractor_level_2");

        DisableChildColliders(level1Visual);
        DisableChildColliders(level2Visual);
    }

    GameObject FindNamedChild(string childName)
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null && children[i] != transform && children[i].name == childName)
                return children[i].gameObject;
        }

        return null;
    }

    void EnsureCollider()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null)
            box = gameObject.AddComponent<BoxCollider>();

        float cell = GridFootprint.CellSize * 0.84f;
        Vector3 lossy = transform.lossyScale;
        box.size = new Vector3(
            cell / Mathf.Max(0.01f, lossy.x),
            0.8f / Mathf.Max(0.01f, lossy.y),
            cell / Mathf.Max(0.01f, lossy.z)
        );
        box.center = new Vector3(0f, 0.4f, 0f);
        box.enabled = true;
    }

    static void DisableChildColliders(GameObject root)
    {
        if (root == null)
            return;

        Collider[] cols = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] != null)
                cols[i].enabled = false;
        }
    }

    void BindToNearbyNode()
    {
        boundNode = null;
        resource = null;

        Vector2Int cell = BuildingLinker.WorldToCell(transform.position);
        ResourceNode node = ResourceNode.GetAt(cell);
        if (node != null && node.resource != null)
        {
            boundNode = node;
            resource = node.resource;
            if (showDebug)
                Debug.Log($"[Extractor] Bound to {resource.displayName} at {cell}");
            return;
        }

        if (showDebug)
            Debug.LogWarning($"[Extractor] Нет ResourceNode в клетке {cell}");
    }

    void Update()
    {
        if (resource == null) return;

        timer += Time.deltaTime;
        if (timer < extractInterval) return;
        timer -= extractInterval;

        if (!HasOutputSpace(itemsPerCycle) && !CanPushAnyNow())
        {
            if (showDebug && Time.time >= nextFailLogTime)
            {
                nextFailLogTime = Time.time + 2f;
                Debug.LogWarning($"[Extractor] Выход забит (buffer={OutputBufferCount}/{maxOutputBuffer})");
            }
            return;
        }

        for (int i = 0; i < itemsPerCycle; i++)
        {
            if (!CanPushAnyNow() && !HasOutputSpace(1))
                break;

            if (boundNode != null && !boundNode.TryConsume(1))
                break;

            if (!TryOutputToAny(resource))
                break;

            ProductionStats.Instance?.RecordProduced(resource, 1);

            if (showDebug)
                Debug.Log($"[Extractor] Выдал {resource.displayName}");
        }
    }

    bool CanPushAnyNow()
    {
        if (outputSockets == null) return false;

        for (int i = 0; i < outputSockets.Length; i++)
        {
            BuildingSocket socket = outputSockets[i];
            if (socket == null)
                continue;

            if (socket.connectedSocket != null)
            {
                BuildingBase target = socket.connectedSocket.GetComponentInParent<BuildingBase>();
                if (target != null)
                    return true;
            }

            BuildingBase front = BuildingLinker.GetBuildingAt(BuildingLinker.GetSocketFrontCell(socket));
            if (front != null && front != this)
                return true;
        }

        return false;
    }

    public void Interact(GameObject interactor)
    {
        if (MachineUI.Instance != null)
            MachineUI.Instance.Open(this);
    }
}
