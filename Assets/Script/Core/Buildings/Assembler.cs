using UnityEngine;
using System.Collections.Generic;

public class Assembler : BuildingBase, IInteractable
{
    [Header("Assembler Settings")]
    public RecipeData currentRecipe;
    public float craftProgress = 0f;

    [Header("Input buffer")]
    [Tooltip("Макс. множитель запасов относительно рецепта (2 = два крафта вперёд).")]
    public int inputBufferMultiplier = 2;

    [Header("Upgrade")]
    public int level = 1;
    public float upgradedCraftSpeed = 2f;

    [Header("Debug")]
    public bool showDebug = false;

    GameObject level1Visual;
    GameObject level2Visual;

    private readonly Dictionary<ItemData, int> inputBuffer = new Dictionary<ItemData, int>();

    public bool CanUpgrade => level < 2;
    public float CraftSpeed => level >= 2 ? Mathf.Max(1f, upgradedCraftSpeed) : 1f;

    void Awake()
    {
        EnsureCollider();
        BindVisuals();
        ApplyLevel();
    }

    public override void OnPlaced()
    {
        base.OnPlaced();
        craftProgress = 0f;
        inputBuffer.Clear();
        EnsureCollider();
        BindVisuals();
        ApplyLevel();
    }

    public bool TryUpgrade()
    {
        if (!CanUpgrade)
            return false;

        level = 2;
        ApplyLevel();
        return true;
    }

    public float GetEffectiveCraftTime()
    {
        if (currentRecipe == null)
            return 0f;
        return currentRecipe.craftTime / CraftSpeed;
    }

    void ApplyLevel()
    {
        if (level1Visual != null)
            level1Visual.SetActive(level < 2);
        if (level2Visual != null)
            level2Visual.SetActive(level >= 2);
    }

    void BindVisuals()
    {
        if (level1Visual == null)
            level1Visual = FindNamedChild("Assembler_Level_1");
        if (level2Visual == null)
            level2Visual = FindNamedChild("Assembler_level_2");

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
            0.9f / Mathf.Max(0.01f, lossy.y),
            cell / Mathf.Max(0.01f, lossy.z)
        );
        box.center = new Vector3(0f, 0.45f, 0f);
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

    void Update()
    {
        if (currentRecipe == null) return;

        if (!HasEnoughInputs())
            return;

        if (!HasSpaceForRecipeOutputs())
        {
            craftProgress = Mathf.Min(craftProgress, currentRecipe.craftTime);
            return;
        }

        craftProgress += Time.deltaTime * CraftSpeed;

        if (craftProgress >= currentRecipe.craftTime)
        {
            if (TryCraft())
                craftProgress = 0f;
            else
                craftProgress = currentRecipe.craftTime;
        }
    }

    public override bool TryReceiveItem(ItemData item, BuildingSocket fromSocket)
    {
        if (currentRecipe == null || item == null) return false;

        int required = 0;
        bool needed = false;
        for (int i = 0; i < currentRecipe.inputs.Count; i++)
        {
            ItemStack stack = currentRecipe.inputs[i];
            if (stack.item == item)
            {
                needed = true;
                required = stack.amount;
                break;
            }
        }

        if (!needed) return false;

        int maxKeep = Mathf.Max(1, required) * Mathf.Max(1, inputBufferMultiplier);
        inputBuffer.TryGetValue(item, out int have);
        if (have >= maxKeep)
            return false;

        inputBuffer[item] = have + 1;

        if (showDebug)
            Debug.Log($"Assembler получил: {item.displayName}. Теперь: {inputBuffer[item]}");

        return true;
    }

    bool HasEnoughInputs()
    {
        if (currentRecipe == null) return false;

        for (int i = 0; i < currentRecipe.inputs.Count; i++)
        {
            ItemStack required = currentRecipe.inputs[i];
            if (!inputBuffer.TryGetValue(required.item, out int have) || have < required.amount)
                return false;
        }
        return true;
    }

    bool HasSpaceForRecipeOutputs()
    {
        if (currentRecipe == null) return false;

        int total = 0;
        for (int i = 0; i < currentRecipe.outputs.Count; i++)
            total += Mathf.Max(0, currentRecipe.outputs[i].amount);

        return HasOutputSpace(total);
    }

    bool TryCraft()
    {
        if (!HasEnoughInputs() || !HasSpaceForRecipeOutputs())
            return false;

        for (int i = 0; i < currentRecipe.inputs.Count; i++)
        {
            ItemStack required = currentRecipe.inputs[i];
            inputBuffer[required.item] -= required.amount;
        }

        for (int i = 0; i < currentRecipe.outputs.Count; i++)
        {
            ItemStack output = currentRecipe.outputs[i];
            for (int n = 0; n < output.amount; n++)
            {
                if (!TryOutputToAny(output.item) && showDebug)
                    Debug.LogWarning($"Assembler: буфер переполнен при выдаче {output.item?.displayName}");
                else if (showDebug)
                    Debug.Log($"Assembler выдал: {output.item.displayName}");
            }
        }

        return true;
    }

    public void SetRecipe(RecipeData recipe)
    {
        currentRecipe = recipe;
        craftProgress = 0f;
        inputBuffer.Clear();
    }

    public void Interact(GameObject interactor)
    {
        if (MachineUI.Instance != null)
            MachineUI.Instance.Open(this);
        else
            Debug.LogError("MachineUI.Instance == null!");
    }
}
