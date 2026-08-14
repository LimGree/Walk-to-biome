using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Склад 1x1: один тип предмета, два стека. Вход сзади, выход вперёд. Рецепта нет.
/// </summary>
public class StorageContainer : BuildingBase, IInteractable
{
    [Header("Inventory")]
    public int slotCount = 2;

    [Header("Debug")]
    public bool showDebug;

    readonly List<ItemStack> slots = new List<ItemStack>(2);

    public ItemData StoredType
    {
        get
        {
            EnsureSlots();
            for (int i = 0; i < slots.Count; i++)
            {
                if (!slots[i].IsEmpty)
                    return slots[i].item;
            }

            return null;
        }
    }

    public int SlotCount => Mathf.Max(1, slotCount);
    public IReadOnlyList<ItemStack> Slots
    {
        get
        {
            EnsureSlots();
            return slots;
        }
    }

    public int UsedSlotCount
    {
        get
        {
            EnsureSlots();
            int used = 0;
            for (int i = 0; i < slots.Count; i++)
            {
                if (!slots[i].IsEmpty)
                    used++;
            }
            return used;
        }
    }

    void Awake()
    {
        EnsureSetup();
        EnsureSlots();
    }

    public override void OnPlaced()
    {
        EnsureSetup();
        EnsureSlots();
        base.OnPlaced();
    }

    void Update()
    {
        ItemData item = PeekFirstItem();
        if (item == null)
            return;

        if (TryPushToConnections(item) && TryRemoveOne(item) && showDebug)
            Debug.Log($"[Storage] out {item.displayName}");
    }

    public override bool TryReceiveItem(ItemData item, BuildingSocket fromSocket)
    {
        if (item == null)
            return false;

        if (!TryAddOne(item))
            return false;

        if (showDebug)
            Debug.Log($"[Storage] in {item.displayName}");
        return true;
    }

    public bool TryAddOne(ItemData item)
    {
        if (item == null)
            return false;

        EnsureSlots();

        ItemData locked = StoredType;
        if (locked != null && item != locked)
            return false;

        int max = MaxStack(item);

        for (int i = 0; i < slots.Count; i++)
        {
            ItemStack stack = slots[i];
            if (stack.item == item && stack.amount > 0 && stack.amount < max)
            {
                stack.amount++;
                return true;
            }
        }

        for (int i = 0; i < slots.Count; i++)
        {
            if (!slots[i].IsEmpty)
                continue;

            slots[i].item = item;
            slots[i].amount = 1;
            return true;
        }

        return false;
    }

    public ItemData PeekFirstItem()
    {
        EnsureSlots();
        for (int i = 0; i < slots.Count; i++)
        {
            if (!slots[i].IsEmpty)
                return slots[i].item;
        }

        return null;
    }

    public bool TryRemoveOne(ItemData item)
    {
        if (item == null)
            return false;

        EnsureSlots();
        for (int i = 0; i < slots.Count; i++)
        {
            ItemStack stack = slots[i];
            if (stack.item != item || stack.amount <= 0)
                continue;

            stack.amount--;
            if (stack.amount <= 0)
            {
                stack.item = null;
                stack.amount = 0;
            }

            return true;
        }

        return false;
    }

    public ItemStack GetSlot(int index)
    {
        EnsureSlots();
        if (index < 0 || index >= slots.Count)
            return null;
        return slots[index];
    }

    public void Interact(GameObject interactor)
    {
        if (MachineUI.Instance != null)
            MachineUI.Instance.Open(this);
        else
            Debug.LogError("MachineUI.Instance == null!");
    }

    void EnsureSlots()
    {
        int count = SlotCount;
        while (slots.Count < count)
            slots.Add(new ItemStack(null, 0));

        if (slots.Count > count)
            slots.RemoveRange(count, slots.Count - count);
    }

    static int MaxStack(ItemData item)
    {
        if (item == null || item.maxStack <= 0)
            return 100;
        return item.maxStack;
    }

    void EnsureSetup()
    {
        DisableChildColliders();
        EnsureCollider();
        EnsureSockets();
    }

    void DisableChildColliders()
    {
        Collider[] childCols = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < childCols.Length; i++)
        {
            if (childCols[i] != null && childCols[i].gameObject != gameObject)
                childCols[i].enabled = false;
        }
    }

    void EnsureCollider()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null)
            box = gameObject.AddComponent<BoxCollider>();

        Vector3 world = GridFootprint.GetWorldSize(FootprintSize);
        Vector3 lossy = transform.lossyScale;
        box.size = new Vector3(
            world.x * 0.92f / Mathf.Max(0.01f, lossy.x),
            0.9f / Mathf.Max(0.01f, lossy.y),
            world.z * 0.92f / Mathf.Max(0.01f, lossy.z)
        );
        box.center = new Vector3(0f, 0.45f, 0f);
        box.enabled = true;
    }

    void EnsureSockets()
    {
        if (NeedSocket(inputSockets))
        {
            inputSockets = new[]
            {
                FindOrCreateSocket("InputSocket", SocketType.Input, new Vector3(0f, 0.3f, -0.5f))
            };
        }

        if (NeedSocket(outputSockets))
        {
            outputSockets = new[]
            {
                FindOrCreateSocket("OutputSocket", SocketType.Output, new Vector3(0f, 0.3f, 0.5f))
            };
        }
    }

    static bool NeedSocket(BuildingSocket[] sockets)
    {
        return sockets == null || sockets.Length == 0 || sockets[0] == null;
    }

    BuildingSocket FindOrCreateSocket(string socketName, SocketType type, Vector3 localPos)
    {
        Transform existing = transform.Find(socketName);
        GameObject go;
        if (existing != null)
        {
            go = existing.gameObject;
        }
        else
        {
            go = new GameObject(socketName);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
        }

        BuildingSocket socket = go.GetComponent<BuildingSocket>();
        if (socket == null)
            socket = go.AddComponent<BuildingSocket>();
        socket.socketType = type;
        return socket;
    }
}
