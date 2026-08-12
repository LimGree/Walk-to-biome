using UnityEngine;
using System.Collections.Generic;

public enum ConveyorShape
{
    Straight = 0,
    Corner = 1
}

public class ConveyorBelt : MonoBehaviour
{
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

    [Header("Connections")]
    public ConveyorBelt prevBelt;
    public ConveyorBelt nextBelt;
    public BuildingSocket connectedInputSocket;
    public BuildingSocket connectedOutputSocket;

    [Header("Visual Path")]
    public Transform startPoint;
    public Transform endPoint;
    [Tooltip("Центр поворота для Corner. Если null — вычисляется из start/end.")]
    public Transform midPoint;

    [Header("Debug")]
    public bool showDebug = false;

    private List<ItemOnBelt> items = new List<ItemOnBelt>();

    public bool CanAccept()
    {
        return items.Count < maxItems;
    }

    public bool TryAccept(ItemData data, float startProgress = 0f)
    {
        if (!CanAccept() || data == null)
        {
            if (showDebug)
                Debug.LogWarning($"[Belt {name}] Отказ принять {data?.displayName}. Причина: {(data == null ? "data null" : "нет места")}");
            return false;
        }

        GameObject prefab = data.worldPrefab != null ? data.worldPrefab : CreateFallbackItem();
        GameObject go = Instantiate(prefab, GetPositionOnBelt(startProgress), Quaternion.identity);

        ItemOnBelt item = go.GetComponent<ItemOnBelt>();
        if (item == null) item = go.AddComponent<ItemOnBelt>();

        item.Init(data, this, startProgress);
        items.Add(item);

        if (showDebug)
            Debug.Log($"[Belt {name}] Принял {data.displayName}. Предметов на ленте: {items.Count}");

        return true;
    }

    void OnEnable()
    {
        ConveyorNetwork.Instance?.RegisterBelt(this);
    }

    void OnDisable()
    {
        ConveyorNetwork.Instance?.UnregisterBelt(this);
    }

    /// <summary>
    /// Вызывать после установки игроком (регистрация в GridOccupancy).
    /// </summary>
    public void OnPlaced()
    {
        if (GridSystem.Instance == null)
            return;

        Vector2Int cell = GridSystem.Instance.WorldToCell(transform.position);
        GridOccupancy.Register(gameObject, cell);
    }

    public void OnRemoved()
    {
        GridOccupancy.Unregister(gameObject);
    }

    void Update()
    {
        if (items.Count == 0) return;

        float moveDelta = (speed / length) * Time.deltaTime;

        for (int i = items.Count - 1; i >= 0; i--)
        {
            ItemOnBelt item = items[i];
            if (item == null)
            {
                items.RemoveAt(i);
                continue;
            }

            item.progress += moveDelta;
            item.transform.position = GetPositionOnBelt(item.progress);

            Vector3 dir = GetDirectionAtProgress(item.progress);
            if (dir.sqrMagnitude > 0.0001f)
                item.transform.rotation = Quaternion.LookRotation(dir);

            if (item.progress >= 1f)
            {
                TryPassToNext(item, i);
            }
        }
    }

    void TryPassToNext(ItemOnBelt item, int index)
    {
        if (nextBelt != null)
        {
            if (nextBelt.TryAccept(item.itemData, item.progress - 1f))
            {
                if (showDebug)
                    Debug.Log($"[Belt {name}] Передал {item.itemData.displayName} → {nextBelt.name}");

                Destroy(item.gameObject);
                items.RemoveAt(index);
                return;
            }
            else if (showDebug)
            {
                Debug.LogWarning($"[Belt {name}] Не смог передать на следующую ленту {nextBelt.name}");
            }
        }

        if (connectedInputSocket != null)
        {
            BuildingBase building = connectedInputSocket.GetComponentInParent<BuildingBase>();
            if (building != null)
            {
                bool received = building.TryReceiveItem(item.itemData, connectedInputSocket);

                if (received)
                {
                    if (showDebug)
                        Debug.Log($"[Belt {name}] Передал {item.itemData.displayName} в здание {building.name}");

                    Destroy(item.gameObject);
                    items.RemoveAt(index);
                    return;
                }
                else if (showDebug)
                {
                    Debug.LogWarning($"[Belt {name}] Здание {building.name} отказалось принять {item.itemData.displayName}");
                }
            }
        }

        item.progress = 1f;
        item.transform.position = GetPositionOnBelt(1f);

        if (showDebug)
            Debug.LogWarning($"[Belt {name}] Предмет {item.itemData.displayName} застрял в конце ленты");
    }

    /// <summary>
    /// Направление ВЫХОДА ленты (куда уходит поток). Для AutoConnector.
    /// Straight: end - start. Corner: mid → end (или fallback).
    /// </summary>
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

        Vector3 f = transform.forward;
        f.y = 0f;
        return f.sqrMagnitude > 0.0001f ? f.normalized : Vector3.forward;
    }

    /// <summary>
    /// Направление ВХОДА (как движется предмет в начале ленты).
    /// Straight: end - start. Corner: start → mid.
    /// </summary>
    public Vector3 GetEntryDirection()
    {
        if (isCorner)
        {
            if (startPoint != null)
            {
                Vector3 mid = GetMidWorldPosition();
                Vector3 d = mid - startPoint.position;
                d.y = 0f;
                if (d.sqrMagnitude > 0.0001f)
                    return d.normalized;
            }
        }

        return GetExitDirection();
    }

    /// <summary>Алиас для совместимости: направление потока на выходе.</summary>
    public Vector3 GetBeltDirection() => GetExitDirection();

    public Vector3 GetPositionOnBelt(float t)
    {
        t = Mathf.Clamp01(t);

        if (startPoint == null || endPoint == null)
            return transform.position;

        if (!isCorner)
            return Vector3.Lerp(startPoint.position, endPoint.position, t);

        // Corner: start → mid → end (равные по параметру сегменты)
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

        // Fallback: ломаная L в плоскости XZ через центр объекта
        if (startPoint != null && endPoint != null)
        {
            Vector3 s = startPoint.position;
            Vector3 e = endPoint.position;
            float y = (s.y + e.y) * 0.5f;
            // Точка излома: (end.x, start.z) в мире относительно ориентации —
            // ближе к transform.position
            return new Vector3(transform.position.x, y, transform.position.z);
        }

        return transform.position + Vector3.up * 0.3f;
    }

    GameObject CreateFallbackItem()
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.transform.localScale = Vector3.one * 0.3f;
        Destroy(go.GetComponent<Collider>());
        return go;
    }

    void OnDrawGizmos()
    {
        if (startPoint == null || endPoint == null)
            return;

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
    }

    public void ClearItems()
    {
        foreach (var item in items)
            if (item != null) Destroy(item.gameObject);
        items.Clear();
    }

    void OnDestroy()
    {
        ClearItems();
        GridOccupancy.Unregister(gameObject);
    }
}
