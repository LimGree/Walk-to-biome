using UnityEngine;
using System.Collections.Generic;

public class ConveyorBelt : MonoBehaviour
{
    [Header("Settings")]
    public float speed = 2.5f;
    public float length = 1f;
    public int maxItems = 4;

    [Header("Connections")]
    public ConveyorBelt nextBelt;
    public BuildingSocket connectedInputSocket;
    public BuildingSocket connectedOutputSocket;

    [Header("Visual Path")]
    public Transform startPoint;
    public Transform endPoint;

    [Header("Debug")]
    public bool showDebug = true;

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

            Vector3 dir = (endPoint.position - startPoint.position).normalized;
            if (dir != Vector3.zero)
                item.transform.rotation = Quaternion.LookRotation(dir);

            if (item.progress >= 1f)
            {
                TryPassToNext(item, i);
            }
        }
    }

    void TryPassToNext(ItemOnBelt item, int index)
    {
        // 1. Следующая лента
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

        // 2. Здание через сокет
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

        // Некуда девать
        item.progress = 1f;
        item.transform.position = GetPositionOnBelt(1f);

        if (showDebug)
            Debug.LogWarning($"[Belt {name}] Предмет {item.itemData.displayName} застрял в конце ленты");
    }

    Vector3 GetPositionOnBelt(float t)
    {
        t = Mathf.Clamp01(t);
        if (startPoint == null || endPoint == null)
            return transform.position;

        return Vector3.Lerp(startPoint.position, endPoint.position, t);
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
        if (startPoint != null && endPoint != null)
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
    }
}