using UnityEngine;
using System.Collections.Generic;

public class Assembler : BuildingBase, IInteractable
{
    [Header("Assembler Settings")]
    public RecipeData currentRecipe;
    public float craftProgress = 0f;

    [Header("Debug")]
    public bool showDebug = true;

    private Dictionary<ItemData, int> inputBuffer = new Dictionary<ItemData, int>();

    public override void OnPlaced()
    {
        base.OnPlaced();
        craftProgress = 0f;
        inputBuffer.Clear();
    }

    void Update()
    {
        if (currentRecipe == null) return;

        if (HasEnoughInputs())
        {
            craftProgress += Time.deltaTime;

            if (craftProgress >= currentRecipe.craftTime)
            {
                Craft();
                craftProgress = 0f;
            }
        }
    }

    public override bool TryReceiveItem(ItemData item, BuildingSocket fromSocket)
    {
        if (currentRecipe == null) return false;

        // Проверяем, нужен ли этот предмет по рецепту
        bool needed = false;
        foreach (var stack in currentRecipe.inputs)
        {
            if (stack.item == item)
            {
                needed = true;
                break;
            }
        }

        if (!needed) return false;

        if (!inputBuffer.ContainsKey(item))
            inputBuffer[item] = 0;

        inputBuffer[item]++;

        if (showDebug)
            Debug.Log($"Assembler получил: {item.displayName}. Теперь: {inputBuffer[item]}");

        return true;
    }

    bool HasEnoughInputs()
    {
        if (currentRecipe == null) return false;

        foreach (var required in currentRecipe.inputs)
        {
            if (!inputBuffer.ContainsKey(required.item) || inputBuffer[required.item] < required.amount)
                return false;
        }
        return true;
    }

    void Craft()
    {
        // Забираем ресурсы
        foreach (var required in currentRecipe.inputs)
        {
            inputBuffer[required.item] -= required.amount;
        }

        // Выдаём результат
        bool allOutputted = true;

        foreach (var output in currentRecipe.outputs)
        {
            for (int i = 0; i < output.amount; i++)
            {
                bool success = TryOutputToAny(output.item);

                if (!success)
                {
                    allOutputted = false;

                    if (showDebug)
                        Debug.LogWarning($"Assembler не смог выдать {output.item.displayName} — нет места на выходе");
                }
                else
                {
                    if (showDebug)
                        Debug.Log($"Assembler выдал: {output.item.displayName}");
                }
            }
        }

        // Если не смогли выдать — можно вернуть ресурсы (опционально)
        // if (!allOutputted) { ... вернуть ресурсы ... }
    }

    public void SetRecipe(RecipeData recipe)
    {
        currentRecipe = recipe;
        craftProgress = 0f;
        inputBuffer.Clear();
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