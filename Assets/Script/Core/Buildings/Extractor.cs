using UnityEngine;

public class Extractor : BuildingBase, IInteractable
{
    [Header("Extractor Settings")]
    public ItemData resource;
    public float extractInterval = 1.2f;
    public int itemsPerCycle = 1;

    [Header("Node detect")]
    public float nodeSearchRadius = 0.25f;
    public LayerMask resourceNodeLayer = ~0;

    [Header("Debug")]
    public bool showDebug = false;

    private float timer;
    private ResourceNode boundNode;
    private float nextFailLogTime;

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
            Debug.LogWarning("[Extractor] Нет ResourceNode рядом");
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
