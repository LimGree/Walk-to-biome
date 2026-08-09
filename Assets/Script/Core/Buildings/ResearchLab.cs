using UnityEngine;
using System.Collections.Generic;
using System;

public class ResearchLab : BuildingBase, IInteractable
{
    [Header("Research Settings")]
    public ResearchNodeData currentResearch;
    public float researchProgress = 0f;

    // Сколько предметов уже сдано на текущее исследование
    private Dictionary<ItemData, int> submittedItems = new Dictionary<ItemData, int>();

    public override void OnPlaced()
    {
        base.OnPlaced();
        researchProgress = 0f;
        submittedItems.Clear();
    }

    public override bool TryReceiveItem(ItemData item, BuildingSocket fromSocket)
    {
        if (currentResearch == null) return false;

        // Проверяем, нужен ли этот предмет для текущего исследования
        bool isNeeded = false;
        int requiredAmount = 0;

        foreach (var req in currentResearch.requiredItems)
        {
            if (req.item == item)
            {
                isNeeded = true;
                requiredAmount = req.amount;
                break;
            }
        }

        if (!isNeeded) return false;

        if (!submittedItems.ContainsKey(item))
            submittedItems[item] = 0;

        // Принимаем, пока не набрали нужное количество
        if (submittedItems[item] < requiredAmount)
        {
            submittedItems[item]++;
            CheckResearchCompletion();
            return true;
        }

        return false;
    }

    void CheckResearchCompletion()
    {
        if (currentResearch == null) return;

        foreach (var req in currentResearch.requiredItems)
        {
            if (!submittedItems.ContainsKey(req.item) || submittedItems[req.item] < req.amount)
                return; // ещё не всё собрали
        }

        // Исследование завершено!
        CompleteResearch();
    }

    void CompleteResearch()
    {
        Debug.Log($"Research completed: {currentResearch.displayName}");

        // Здесь можно разблокировать новые здания / рецепты
        // ResearchSystem.Instance.Unlock(currentResearch);

        currentResearch = null;
        submittedItems.Clear();
        researchProgress = 0f;
    }

    // Для взаимодействия игрока (клавиша E)
    public void Interact(GameObject interactor)
    {
        Debug.Log($"[Extractor] Interact вызван! MachineUI.Instance = {MachineUI.Instance}");

        if (MachineUI.Instance != null)
            MachineUI.Instance.Open(this);
        else
            Debug.LogError("MachineUI.Instance == null!");
    }

    public void SetResearch(ResearchNodeData research)
    {
        currentResearch = research;
        submittedItems.Clear();
        researchProgress = 0f;
    }

    internal float GetProgress()
    {
        return researchProgress;
    }
}