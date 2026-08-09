using UnityEngine;

public class Extractor : BuildingBase, IInteractable
{
    [Header("Extractor Settings")]
    public ItemData resource;
    public float extractInterval = 1.2f;
    public int itemsPerCycle = 1;

    [Header("Debug")]
    public bool showDebug = true;

    private float timer;

    public override void OnPlaced()
    {
        base.OnPlaced();
        timer = 0f;

        if (showDebug)
            Debug.Log($"[Extractor] OnPlaced. Resource: {(resource != null ? resource.displayName : "NULL")}");
    }

    void Update()
    {
        if (resource == null)
        {
            if (showDebug) Debug.LogWarning("[Extractor] resource не назначен!");
            return;
        }

        timer += Time.deltaTime;

        if (timer >= extractInterval)
        {
            timer -= extractInterval;

            for (int i = 0; i < itemsPerCycle; i++)
            {
                bool success = TryOutputToAny(resource);

                if (showDebug)
                {
                    if (success)
                        Debug.Log($"[Extractor] Выдал {resource.displayName}");
                    else
                        Debug.LogWarning($"[Extractor] Не смог выдать {resource.displayName} — нет места / не подключена лента");
                }

                if (!success) break;
            }
        }
    }
    public void Interact(GameObject interactor)
    {
        Debug.Log($"[Extractor] Interact вызван! MachineUI.Instance = {MachineUI.Instance}");

        if (MachineUI.Instance != null)
            MachineUI.Instance.Open(this);
        else
            Debug.LogError("MachineUI.Instance == null!");
    }
}