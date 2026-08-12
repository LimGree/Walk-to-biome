using UnityEngine;
using System.Collections.Generic;

public class ResearchSystem : MonoBehaviour
{
    public static ResearchSystem Instance { get; private set; }

    [Header("All Research Nodes")]
    public List<ResearchNodeData> allResearchNodes = new List<ResearchNodeData>();

    [Header("Level 0 — доступно с начала")]
    public List<BuildingData> startingBuildings = new List<BuildingData>();
    public List<RecipeData> startingRecipes = new List<RecipeData>();

    private HashSet<ResearchNodeData> unlockedResearch = new HashSet<ResearchNodeData>();
    private HashSet<BuildingData> unlockedBuildings = new HashSet<BuildingData>();
    private HashSet<RecipeData> unlockedRecipes = new HashSet<RecipeData>();

    /// <summary> UI / hotbar подписываются на это </summary>
    public event System.Action OnUnlocksChanged;

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

        GrantStartingUnlocks();
    }

    void GrantStartingUnlocks()
    {
        unlockedBuildings.Clear();
        unlockedRecipes.Clear();

        if (startingBuildings != null)
        {
            foreach (var b in startingBuildings)
                if (b != null) unlockedBuildings.Add(b);
        }

        if (startingRecipes != null)
        {
            foreach (var r in startingRecipes)
                if (r != null) unlockedRecipes.Add(r);
        }

        Debug.Log($"[ResearchSystem] Стартовые здания: {unlockedBuildings.Count}, рецепты: {unlockedRecipes.Count}");
    }

    public bool IsResearchUnlocked(ResearchNodeData node)
    {
        return node != null && unlockedResearch.Contains(node);
    }

    public bool CanStartResearch(ResearchNodeData node)
    {
        if (node == null) return false;
        if (IsResearchUnlocked(node)) return false;

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

        if (node.unlockedBuildings != null)
        {
            foreach (var building in node.unlockedBuildings)
                if (building != null) unlockedBuildings.Add(building);
        }

        if (node.unlockedRecipes != null)
        {
            foreach (var recipe in node.unlockedRecipes)
                if (recipe != null) unlockedRecipes.Add(recipe);
        }

        Debug.Log($"[Research] Completed: {node.displayName}");
        OnUnlocksChanged?.Invoke();
    }

    public bool IsBuildingUnlocked(BuildingData building)
    {
        if (building == null) return false;
        return unlockedBuildings.Contains(building); // больше никаких "пустой = всё"
    }

    public bool IsRecipeUnlocked(RecipeData recipe)
    {
        if (recipe == null) return false;
        return unlockedRecipes.Contains(recipe);
    }

    public List<ResearchNodeData> GetAvailableResearch()
    {
        var list = new List<ResearchNodeData>();
        foreach (var node in allResearchNodes)
            if (CanStartResearch(node))
                list.Add(node);
        return list;
    }

    public List<ResearchNodeData> GetAllNodes() => allResearchNodes;
}