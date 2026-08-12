using UnityEngine;

public class Extractor : BuildingBase, IInteractable
{
    [Header("Extractor Settings")]
    public ItemData resource;               // runtime — берётся с ноды
    public float extractInterval = 1.2f;
    public int itemsPerCycle = 1;

    [Header("Node detect")]
    public float nodeSearchRadius = 0.25f;
    public LayerMask resourceNodeLayer = ~0;

    [Header("Debug")]
    public bool showDebug = true;

    private float timer;
    private ResourceNode boundNode;

    public override void OnPlaced()
    {
        base.OnPlaced();
        timer = 0f;
        BindToNearbyNode();
    }

    void BindToNearbyNode()
    {
        boundNode = null;
        resource = null;

        Collider[] hits = Physics.OverlapSphere(
            transform.position,
            nodeSearchRadius,
            resourceNodeLayer
        );

        foreach (var hit in hits)
        {
            var node = hit.GetComponentInParent<ResourceNode>();
            if (node != null && node.resource != null)
            {
                boundNode = node;
                resource = node.resource;

                if (showDebug)
                    Debug.Log($"[Extractor] Bound to {resource.displayName}");
                return;
            }
        }

        if (showDebug)
            Debug.LogWarning("[Extractor] Нет ResourceNode под экстрактором — не будет добывать");
    }

    void Update()
    {
        if (resource == null) return;

        timer += Time.deltaTime;
        if (timer < extractInterval) return;
        timer -= extractInterval;

        // Конечные ноды
        if (boundNode != null && !boundNode.TryConsume(itemsPerCycle))
            return;

        for (int i = 0; i < itemsPerCycle; i++)
        {
            if (!TryOutputToAny(resource))
            {
                if (showDebug)
                    Debug.LogWarning($"[Extractor] Не смог выдать {resource.displayName}");
                break;
            }
            else if (showDebug)
            {
                Debug.Log($"[Extractor] Выдал {resource.displayName}");
            }
        }
    }

    public void Interact(GameObject interactor)
    {
        if (MachineUI.Instance != null)
            MachineUI.Instance.Open(this);
    }
}