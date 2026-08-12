using UnityEngine;
using System.Collections.Generic;

public enum ConveyorShape
{
    Straight = 0,
    Corner = 1
}

public class ConveyorBelt : MonoBehaviour
{
    public struct ItemSnapshot
    {
        public ItemData data;
        public float progress;
    }

    [Header("Settings")]
    public float speed = 2.5f;
    public float length = 1f;
    public int maxItems = 4;

    [Header("Shape")]
    public ConveyorShape shape = ConveyorShape.Straight;
    public bool isCorner
    {
        get => shape == ConveyorShape.Corner;
        set => shape = value ? ConveyorShape.Corner : ConveyorShape.Straight;
    }

    [Header("Data")]
    public BuildingData buildingData;

    [Header("Connections")]
    public ConveyorBelt prevBelt;
    public ConveyorBelt nextBelt;
    public BuildingSocket connectedInputSocket;
    public BuildingSocket connectedOutputSocket;

    [Header("Visual Path")]
    public Transform startPoint;
    public Transform endPoint;
    public Transform midPoint;

    [Header("Debug")]
    public bool showDebug = false;

    /// <summary>OnDestroy не чистит (reshape/demolish уже сделали).</summary>
    [System.NonSerialized] public bool suppressDestroyCleanup;

    public static bool SuppressReshapeOnPlaced { get; set; }

    private readonly List<ItemOnBelt> items = new List<ItemOnBelt>();

    public float ItemSpacing => 1f / Mathf.Max(1, maxItems);
    public int ItemCount => items.Count;

    // ------------------------------------------------------------------
    // Links (1 next — без merge/split в этой фазе)
    // ------------------------------------------------------------------

    public void SetNextBelt(ConveyorBelt other)
    {
        if (other == this)
            return;
        nextBelt = other;
    }

    public void ClearOutgoingBelts()
    {
        nextBelt = null;
    }

    // ------------------------------------------------------------------
    // Accept / spacing
    // ------------------------------------------------------------------

    public bool CanAccept() => CanAcceptAtProgress(0f);

    public bool CanAcceptAtProgress(float startProgress)
    {
        if (items.Count >= maxItems)
            return false;

        float spacing = ItemSpacing;
        float p = Mathf.Clamp(startProgress, 0f, 1f);

        for (int i = 0; i < items.Count; i++)
        {
            ItemOnBelt item = items[i];
            if (item == null) continue;
            if (Mathf.Abs(item.progress - p) < spacing * 0.99f)
                return false;
        }

        return true;
    }

    public bool TryAccept(ItemData data, float startProgress = 0f)
    {
        if (data == null || !CanAcceptAtProgress(startProgress))
            return false;

        GameObject prefab = data.worldPrefab != null ? data.worldPrefab : CreateFallbackItem();
        GameObject go = Instantiate(prefab, GetPositionOnBelt(startProgress), Quaternion.identity);

        ItemOnBelt item = go.GetComponent<ItemOnBelt>();
        if (item == null)
            item = go.AddComponent<ItemOnBelt>();

        item.Init(data, this, startProgress);
        items.Add(item);
        RefreshItemTransform(item);
        return true;
    }

    public bool TryAcceptItem(ItemOnBelt item, float startProgress = 0f)
    {
        if (item == null || item.itemData == null)
            return false;
        if (!CanAcceptAtProgress(startProgress))
            return false;

        ConveyorBelt oldBelt = item.currentBelt;
        if (oldBelt != null && oldBelt != this)
            oldBelt.RemoveItem(item, destroyGameObject: false);

        item.currentBelt = this;
        item.progress = Mathf.Clamp(startProgress, 0f, 1f);
        if (!items.Contains(item))
            items.Add(item);

        RefreshItemTransform(item);
        return true;
    }

    public void RemoveItem(ItemOnBelt item, bool destroyGameObject)
    {
        if (item == null) return;
        items.Remove(item);
        if (item.currentBelt == this)
            item.currentBelt = null;
        if (destroyGameObject && item != null)
            Destroy(item.gameObject);
    }

    // ------------------------------------------------------------------
    // Lifecycle
    // ------------------------------------------------------------------

    public void OnPlaced()
    {
        if (GridSystem.Instance == null)
            return;

        Vector2Int cell = GridSystem.Instance.WorldToCell(transform.position);
        GridOccupancy.Register(gameObject, cell);

        if (buildingData != null)
            ConveyorReshape.DefaultConveyorData = buildingData;

        // Reshape соседей вызывается снаружи (PlayerBuilder) — один раз
    }

    public void OnRemoved()
    {
        GridOccupancy.Unregister(gameObject);
    }

    public List<ItemSnapshot> CaptureItemSnapshots()
    {
        var list = new List<ItemSnapshot>(items.Count);
        for (int i = 0; i < items.Count; i++)
        {
            ItemOnBelt item = items[i];
            if (item == null || item.itemData == null) continue;
            list.Add(new ItemSnapshot { data = item.itemData, progress = item.progress });
        }
        return list;
    }

    public void RestoreItemSnapshots(List<ItemSnapshot> snapshots)
    {
        if (snapshots == null) return;
        for (int i = 0; i < snapshots.Count; i++)
        {
            if (snapshots[i].data == null) continue;
            TryAccept(snapshots[i].data, snapshots[i].progress);
        }
    }

    // ------------------------------------------------------------------
    // Simulation
    // ------------------------------------------------------------------

    void Update()
    {
        if (items.Count == 0) return;

        for (int i = items.Count - 1; i >= 0; i--)
        {
            if (items[i] == null)
                items.RemoveAt(i);
        }

        if (items.Count == 0) return;

        items.Sort((a, b) => b.progress.CompareTo(a.progress));

        float moveDelta = (speed / Mathf.Max(0.01f, length)) * Time.deltaTime;
        float spacing = ItemSpacing;

        for (int i = 0; i < items.Count;)
        {
            ItemOnBelt item = items[i];
            if (item == null)
            {
                items.RemoveAt(i);
                continue;
            }

            float maxProgress = 1f;
            if (i > 0 && items[i - 1] != null)
                maxProgress = Mathf.Min(maxProgress, items[i - 1].progress - spacing);
            if (maxProgress < 0f)
                maxProgress = 0f;

            item.progress = Mathf.Min(item.progress + moveDelta, maxProgress);
            RefreshItemTransform(item);

            if (item.progress >= 1f - 0.0001f)
            {
                int before = items.Count;
                TryPassToNext(item);
                if (items.Count < before)
                    continue;
            }

            i++;
        }
    }

    void RefreshItemTransform(ItemOnBelt item)
    {
        if (item == null) return;
        item.transform.position = GetPositionOnBelt(item.progress);
        Vector3 dir = GetDirectionAtProgress(item.progress);
        if (dir.sqrMagnitude > 0.0001f)
            item.transform.rotation = Quaternion.LookRotation(dir);
    }

    void TryPassToNext(ItemOnBelt item)
    {
        if (item == null) return;

        if (nextBelt != null)
        {
            float handoff = Mathf.Max(0f, item.progress - 1f);
            if (nextBelt.TryAcceptItem(item, handoff))
                return;
        }

        if (connectedInputSocket != null)
        {
            BuildingBase building = connectedInputSocket.GetComponentInParent<BuildingBase>();
            if (building != null && building.TryReceiveItem(item.itemData, connectedInputSocket))
            {
                items.Remove(item);
                item.currentBelt = null;
                Destroy(item.gameObject);
                return;
            }
        }

        item.progress = 1f;
        RefreshItemTransform(item);
    }

    // ------------------------------------------------------------------
    // Directions
    // ------------------------------------------------------------------

    public Vector3 GetExitDirection()
    {
        if (isCorner)
        {
            Vector3 mid = GetMidWorldPosition();
            if (endPoint != null)
            {
                Vector3 d = endPoint.position - mid;
                d.y = 0f;
                if (d.sqrMagnitude > 0.0001f)
                    return d.normalized;
            }
        }

        if (startPoint != null && endPoint != null)
        {
            Vector3 d = endPoint.position - startPoint.position;
            d.y = 0f;
            if (d.sqrMagnitude > 0.0001f)
                return d.normalized;
        }

        // Fallback: transform.forward (yaw; negative scale.x does not flip forward)
        Vector3 f = transform.forward;
        f.y = 0f;
        return f.sqrMagnitude > 0.0001f ? f.normalized : Vector3.forward;
    }

    public Vector3 GetEntryDirection()
    {
        if (isCorner && startPoint != null)
        {
            Vector3 mid = GetMidWorldPosition();
            Vector3 d = mid - startPoint.position;
            d.y = 0f;
            if (d.sqrMagnitude > 0.0001f)
                return d.normalized;
        }

        return GetExitDirection();
    }

    public Vector3 GetBeltDirection() => GetExitDirection();

    public Vector3 GetPositionOnBelt(float t)
    {
        t = Mathf.Clamp01(t);
        if (startPoint == null || endPoint == null)
            return transform.position;

        if (!isCorner)
            return Vector3.Lerp(startPoint.position, endPoint.position, t);

        Vector3 mid = GetMidWorldPosition();
        if (t <= 0.5f)
            return Vector3.Lerp(startPoint.position, mid, t * 2f);
        return Vector3.Lerp(mid, endPoint.position, (t - 0.5f) * 2f);
    }

    public Vector3 GetDirectionAtProgress(float t)
    {
        t = Mathf.Clamp01(t);
        if (!isCorner)
            return GetExitDirection();

        Vector3 mid = GetMidWorldPosition();
        if (t < 0.5f)
        {
            if (startPoint == null) return GetEntryDirection();
            Vector3 d = mid - startPoint.position;
            d.y = 0f;
            return d.sqrMagnitude > 0.0001f ? d.normalized : GetEntryDirection();
        }

        if (endPoint == null) return GetExitDirection();
        Vector3 d2 = endPoint.position - mid;
        d2.y = 0f;
        return d2.sqrMagnitude > 0.0001f ? d2.normalized : GetExitDirection();
    }

    Vector3 GetMidWorldPosition()
    {
        if (midPoint != null)
            return midPoint.position;

        if (startPoint != null && endPoint != null)
        {
            float y = (startPoint.position.y + endPoint.position.y) * 0.5f;
            return new Vector3(transform.position.x, y, transform.position.z);
        }

        return transform.position + Vector3.up * 0.3f;
    }

    GameObject CreateFallbackItem()
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.transform.localScale = Vector3.one * 0.3f;
        Collider col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);
        return go;
    }

    public void ClearItems()
    {
        for (int i = 0; i < items.Count; i++)
        {
            ItemOnBelt item = items[i];
            if (item == null) continue;
            item.currentBelt = null;
            Destroy(item.gameObject);
        }
        items.Clear();
    }

    void OnDrawGizmos()
    {
        if (startPoint == null || endPoint == null) return;

        if (isCorner)
        {
            Vector3 mid = GetMidWorldPosition();
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(startPoint.position, mid);
            Gizmos.DrawLine(mid, endPoint.position);
            Gizmos.DrawSphere(startPoint.position, 0.08f);
            Gizmos.DrawSphere(mid, 0.06f);
            Gizmos.DrawSphere(endPoint.position, 0.08f);
        }
        else
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(startPoint.position, endPoint.position);
            Gizmos.DrawSphere(startPoint.position, 0.08f);
            Gizmos.DrawSphere(endPoint.position, 0.08f);
        }

        if (nextBelt != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(transform.position + Vector3.up * 0.2f,
                nextBelt.transform.position + Vector3.up * 0.2f);
        }
    }

    void OnDestroy()
    {
        if (suppressDestroyCleanup)
            return;

        ClearItems();
        AutoConnector.ClearBeltLinks(this);
        GridOccupancy.Unregister(gameObject);
    }
}
