using UnityEngine;
using System.Collections.Generic;

public class ResearchSystem : MonoBehaviour
{
    public static ResearchSystem Instance { get; private set; }

    [Header("All Research")]
    public List<ResearchNodeData> allResearchNodes = new List<ResearchNodeData>();

    // Что уже исследовано
    private HashSet<ResearchNodeData> unlockedResearch = new HashSet<ResearchNodeData>();

    // Что уже разблокировано
    private HashSet<BuildingData> unlockedBuildings = new HashSet<BuildingData>();
    private HashSet<RecipeData> unlockedRecipes = new HashSet<RecipeData>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public bool IsResearchUnlocked(ResearchNodeData node)
    {
        return unlockedResearch.Contains(node);
    }

    public bool CanStartResearch(ResearchNodeData node)
    {
        if (IsResearchUnlocked(node)) return false;

        // Проверяем зависимости
        if (node.requiredResearches != null)
        {
            foreach (var req in node.requiredResearches)
            {
                if (!IsResearchUnlocked(req))
                    return false;
            }
        }
        return true;
    }

    public void CompleteResearch(ResearchNodeData node)
    {
        if (IsResearchUnlocked(node)) return;

        unlockedResearch.Add(node);

        // Разблокируем здания
        if (node.unlockedBuildings != null)
        {
            foreach (var building in node.unlockedBuildings)
            {
                unlockedBuildings.Add(building);
            }
        }

        // Разблокируем рецепты
        if (node.unlockedRecipes != null)
        {
            foreach (var recipe in node.unlockedRecipes)
            {
                unlockedRecipes.Add(recipe);
            }
        }

        Debug.Log($"Research unlocked: {node.displayName}");
    }

    public bool IsBuildingUnlocked(BuildingData building)
    {
        // Если список пустой — считаем, что всё открыто с начала
        if (unlockedBuildings.Count == 0) return true;
        return unlockedBuildings.Contains(building);
    }

    public bool IsRecipeUnlocked(RecipeData recipe)
    {
        if (unlockedRecipes.Count == 0) return true;
        return unlockedRecipes.Contains(recipe);
    }

    public List<ResearchNodeData> GetAllNodes() => allResearchNodes;
}