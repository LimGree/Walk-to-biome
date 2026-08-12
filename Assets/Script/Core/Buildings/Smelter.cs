using UnityEngine;
using System.Collections.Generic;

public class Smelter : BuildingBase, IInteractable
{
    [Header("Smelter Settings")]
    public RecipeData currentRecipe;
    public float craftProgress = 0f;

    [Header("Input buffer")]
    [Tooltip("Макс. множитель запасов относительно рецепта (2 = два крафта вперёд).")]
    public int inputBufferMultiplier = 2;

    [Header("Debug")]
    public bool showDebug = false;

    private readonly Dictionary<ItemData, int> inputBuffer = new Dictionary<ItemData, int>();

    public override void OnPlaced()
    {
        base.OnPlaced();
        craftProgress = 0f;
        inputBuffer.Clear();
    }

    void Update()
    {
        if (currentRecipe == null) return;

        if (!HasEnoughInputs())
            return;

        // Не крутим craft, если некуда положить выход — без потери
        if (!HasSpaceForRecipeOutputs())
        {
            craftProgress = Mathf.Min(craftProgress, currentRecipe.craftTime);
            return;
        }

        craftProgress += Time.deltaTime;

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
            Debug.Log($"[Smelter] Получил: {item.displayName}. Теперь: {inputBuffer[item]}");

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
                if (!TryOutputToAny(output.item))
                {
                    // Не должны сюда попасть — место резервировали
                    if (showDebug)
                        Debug.LogWarning($"[Smelter] Буфер переполнен при выдаче {output.item?.displayName}");
                }
                else if (showDebug)
                {
                    Debug.Log($"[Smelter] Выдал: {output.item.displayName}");
                }
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
