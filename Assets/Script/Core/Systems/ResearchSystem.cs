using UnityEngine;
using System.Collections.Generic;

public class ResearchSystem : MonoBehaviour
{
    public static ResearchSystem Instance { get; private set; }

    [Header("All Research Nodes")]
    public List<ResearchNodeData> allResearchNodes = new List<ResearchNodeData>();

    // Что уже исследовано
    private HashSet<ResearchNodeData> unlockedResearch = new HashSet<ResearchNodeData>();

    // Что разблокировано
    private HashSet<BuildingData> unlockedBuildings = new HashSet<BuildingData>();
    private HashSet<RecipeData> unlockedRecipes = new HashSet<RecipeData>();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Стартовые здания (уровень 0)
        // Их можно добавить вручную в unlockedBuildings в инспекторе
        // или прописать здесь
    }

    public bool IsResearchUnlocked(ResearchNodeData node)
    {
        return unlockedResearch.Contains(node);
    }

    public bool CanStartResearch(ResearchNodeData node)
    {
        if (node == null) return false;
        if (IsResearchUnlocked(node)) return false;

        // Проверяем зависимости
        if (node.requiredResearches != null)
        {
            foreach (var req in node.requiredResearches)
            {
                if (req != null && !IsResearchUnlocked(req))
                    return false;
            }
        }

        return true;
    }

    public void CompleteResearch(ResearchNodeData node)
    {
        if (node == null || IsResearchUnlocked(node)) return;

        unlockedResearch.Add(node);

        // Разблокируем здания
        if (node.unlockedBuildings != null)
        {
            foreach (var building in node.unlockedBuildings)
            {
                if (building != null)
                    unlockedBuildings.Add(building);
            }
        }

        // Разблокируем рецепты
        if (node.unlockedRecipes != null)
        {
            foreach (var recipe in node.unlockedRecipes)
            {
                if (recipe != null)
                    unlockedRecipes.Add(recipe);
            }
        }

        Debug.Log($"[Research] Исследование завершено: {node.displayName}");
    }

    public bool IsBuildingUnlocked(BuildingData building)
    {
        if (building == null) return false;

        // Если здание в стартовом наборе — всегда доступно
        if (unlockedBuildings.Count == 0) return true;

        return unlockedBuildings.Contains(building);
    }

    public bool IsRecipeUnlocked(RecipeData recipe)
    {
        if (recipe == null) return false;
        if (unlockedRecipes.Count == 0) return true;

        return unlockedRecipes.Contains(recipe);
    }

    public List<ResearchNodeData> GetAvailableResearch()
    {
        List<ResearchNodeData> available = new List<ResearchNodeData>();

        foreach (var node in allResearchNodes)
        {
            if (CanStartResearch(node))
                available.Add(node);
        }

        return available;
    }

    public List<ResearchNodeData> GetAllNodes() => allResearchNodes;
}