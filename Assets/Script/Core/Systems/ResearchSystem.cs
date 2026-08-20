using UnityEngine;
using System.Collections.Generic;

public class ResearchSystem : MonoBehaviour
{
    public static ResearchSystem Instance { get; private set; }

    [Header("All Research Nodes")]
    public List<ResearchNodeData> allResearchNodes = new List<ResearchNodeData>();

    [Header("Level 0 — старт")]
    public List<BuildingData> startingBuildings = new List<BuildingData>();
    public List<RecipeData> startingRecipes = new List<RecipeData>();

    [Header("Лимит лабораторий")]
    [Tooltip("Сейчас на карте можно поставить столько лабораторий.")]
    public int baseLabLimit = 1;
    [Tooltip("Потолок после апгрейдов 4–5 уровня.")]
    public int maxLabLimit = 3;
    [Tooltip("id исследований, каждое из которых даёт +1 слот лаборатории (уровни 4 и 5).")]
    public string[] extraLabSlotResearchIds =
    {
        "research_advanced_automation",
        "research_petrochemistry"
    };

    private readonly HashSet<ResearchNodeData> unlockedResearch = new HashSet<ResearchNodeData>();
    private readonly HashSet<BuildingData> unlockedBuildings = new HashSet<BuildingData>();
    private readonly HashSet<RecipeData> unlockedRecipes = new HashSet<RecipeData>();
    private readonly Dictionary<ItemData, int> submittedItems = new Dictionary<ItemData, int>();

    public ResearchNodeData CurrentResearch { get; private set; }

    public event System.Action OnUnlocksChanged;
    public event System.Action OnResearchProgressChanged;

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

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void GrantStartingUnlocks()
    {
        unlockedBuildings.Clear();
        unlockedRecipes.Clear();

        if (startingBuildings != null)
        {
            for (int i = 0; i < startingBuildings.Count; i++)
            {
                if (startingBuildings[i] != null)
                    unlockedBuildings.Add(startingBuildings[i]);
            }
        }

        if (startingRecipes != null)
        {
            for (int i = 0; i < startingRecipes.Count; i++)
            {
                if (startingRecipes[i] != null)
                    unlockedRecipes.Add(startingRecipes[i]);
            }
        }

    }

    public bool IsResearchUnlocked(ResearchNodeData node)
    {
        return node != null && unlockedResearch.Contains(node);
    }

    public bool IsResearchIdUnlocked(string id)
    {
        if (string.IsNullOrEmpty(id))
            return false;

        foreach (var node in unlockedResearch)
        {
            if (node != null && IdsEqual(node.id, id))
                return true;
        }

        return false;
    }

    public bool CanStartResearch(ResearchNodeData node)
    {
        if (node == null) return false;
        if (IsResearchUnlocked(node)) return false;

        if (node.requiredResearches != null)
        {
            for (int i = 0; i < node.requiredResearches.Count; i++)
            {
                ResearchNodeData req = node.requiredResearches[i];
                if (req != null && !IsResearchUnlocked(req))
                    return false;
            }
        }

        return true;
    }

    public bool SetCurrentResearch(ResearchNodeData node)
    {
        if (node == null)
            return false;
        if (!CanStartResearch(node))
            return false;
        if (CurrentResearch != null && CurrentResearch != node)
            return false;

        if (CurrentResearch == node)
            return true;

        CurrentResearch = node;
        submittedItems.Clear();
        OnResearchProgressChanged?.Invoke();
        return true;
    }

    public bool TrySubmitItem(ItemData item)
    {
        if (item == null)
            return false;

        bool countedForResearch = false;
        if (CurrentResearch != null && CurrentResearch.requiredItems != null)
        {
            int requiredAmount = 0;
            bool needed = false;
            for (int i = 0; i < CurrentResearch.requiredItems.Count; i++)
            {
                ItemStack req = CurrentResearch.requiredItems[i];
                if (req.item == item)
                {
                    needed = true;
                    requiredAmount = req.amount;
                    break;
                }
            }

            if (needed)
            {
                submittedItems.TryGetValue(item, out int have);
                if (have < requiredAmount)
                {
                    submittedItems[item] = have + 1;
                    countedForResearch = true;
                    OnResearchProgressChanged?.Invoke();
                    TryCompleteCurrentResearch();
                }
            }
        }

        if (!countedForResearch && Economy.IsGear(item) && BeltSpeedSystem.Instance != null)
            BeltSpeedSystem.Instance.SubmitGear();

        if (!countedForResearch)
        {
            int coins = Economy.SellValue(item);
            if (coins > 0 && PlayerWallet.Instance != null)
                PlayerWallet.Instance.AddCoins(coins);
        }

        return true;
    }

    void TryCompleteCurrentResearch()
    {
        if (CurrentResearch == null || CurrentResearch.requiredItems == null)
            return;

        for (int i = 0; i < CurrentResearch.requiredItems.Count; i++)
        {
            ItemStack req = CurrentResearch.requiredItems[i];
            if (req.item == null)
                continue;
            submittedItems.TryGetValue(req.item, out int have);
            if (have < req.amount)
                return;
        }

        CompleteResearch(CurrentResearch, grantReward: true);
    }

    public void CompleteResearch(ResearchNodeData node)
    {
        CompleteResearch(node, grantReward: true);
    }

    public void CompleteResearch(ResearchNodeData node, bool grantReward)
    {
        if (node == null || IsResearchUnlocked(node))
            return;

        unlockedResearch.Add(node);

        if (node.unlockedBuildings != null)
        {
            for (int i = 0; i < node.unlockedBuildings.Count; i++)
            {
                if (node.unlockedBuildings[i] != null)
                    unlockedBuildings.Add(node.unlockedBuildings[i]);
            }
        }

        if (node.unlockedRecipes != null)
        {
            for (int i = 0; i < node.unlockedRecipes.Count; i++)
            {
                if (node.unlockedRecipes[i] != null)
                    unlockedRecipes.Add(node.unlockedRecipes[i]);
            }
        }

        if (CurrentResearch == node)
        {
            CurrentResearch = null;
            submittedItems.Clear();
        }

        if (grantReward)
        {
            int rubies = Economy.RubyReward(node);
            if (rubies > 0 && PlayerWallet.Instance != null)
                PlayerWallet.Instance.AddRubies(rubies);
            Debug.Log($"[Research] Completed: {node.displayName}  +{rubies} ruby");
        }
        else
        {
            Debug.Log($"[Research] Completed: {node.displayName}");
        }
        OnUnlocksChanged?.Invoke();
        OnResearchProgressChanged?.Invoke();
    }

    public bool IsBuildingUnlocked(BuildingData building)
    {
        return building != null && unlockedBuildings.Contains(building);
    }

    public bool IsRecipeUnlocked(RecipeData recipe)
    {
        return recipe != null && unlockedRecipes.Contains(recipe);
    }

    public float GetCurrentProgress01()
    {
        if (CurrentResearch == null || CurrentResearch.requiredItems == null || CurrentResearch.requiredItems.Count == 0)
            return 0f;

        int totalRequired = 0;
        int totalSubmitted = 0;
        for (int i = 0; i < CurrentResearch.requiredItems.Count; i++)
        {
            ItemStack req = CurrentResearch.requiredItems[i];
            if (req.item == null)
                continue;
            totalRequired += Mathf.Max(0, req.amount);
            submittedItems.TryGetValue(req.item, out int have);
            totalSubmitted += Mathf.Min(have, Mathf.Max(0, req.amount));
        }

        return totalRequired > 0 ? (float)totalSubmitted / totalRequired : 0f;
    }

    public int GetSubmitted(ItemData item)
    {
        if (item == null)
            return 0;
        submittedItems.TryGetValue(item, out int have);
        return have;
    }

    public int GetMaxResearchLabs()
    {
        int extra = 0;
        if (extraLabSlotResearchIds != null)
        {
            for (int i = 0; i < extraLabSlotResearchIds.Length; i++)
            {
                if (IsResearchIdUnlocked(extraLabSlotResearchIds[i]))
                    extra++;
            }
        }

        return Mathf.Clamp(baseLabLimit + extra, 1, Mathf.Max(1, maxLabLimit));
    }

    public int CountPlacedLabs()
    {
        ResearchLab[] labs = FindObjectsByType<ResearchLab>(FindObjectsSortMode.None);
        int count = 0;
        for (int i = 0; i < labs.Length; i++)
        {
            if (labs[i] != null && labs[i].IsWorldLab)
                count++;
        }

        return count;
    }

    public bool CanPlaceAnotherLab()
    {
        return CountPlacedLabs() < GetMaxResearchLabs();
    }

    public List<ResearchNodeData> GetAvailableResearch()
    {
        var list = new List<ResearchNodeData>();
        List<ResearchNodeData> nodes = GetAllNodes();
        if (nodes == null)
            return list;

        for (int i = 0; i < nodes.Count; i++)
        {
            if (CanStartResearch(nodes[i]))
                list.Add(nodes[i]);
        }

        return list;
    }

    public List<ResearchNodeData> GetAllNodes()
    {
        ResearchNodeData[] fromDb = GameDatabase.AllResearches();
        if (fromDb != null && fromDb.Length > 0)
            return new List<ResearchNodeData>(fromDb);
        if (allResearchNodes != null && allResearchNodes.Count > 0)
            return allResearchNodes;
        return new List<ResearchNodeData>();
    }

    [ContextMenu("Debug/Complete Current Research")]
    void DebugCompleteCurrentResearch()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[Research] Skip research only works in Play Mode.");
            return;
        }

        ResearchNodeData node = CurrentResearch;
        if (node == null)
        {
            List<ResearchNodeData> available = GetAvailableResearch();
            if (available.Count == 0)
            {
                Debug.LogWarning("[Research] Nothing to complete.");
                return;
            }

            node = available[0];
            Debug.Log($"[Research] No active research, completing next available: {node.displayName}");
        }

        CompleteResearch(node);
    }

    [ContextMenu("Debug/Complete All Research")]
    void DebugCompleteAllResearch()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[Research] Skip research only works in Play Mode.");
            return;
        }

        if (allResearchNodes == null)
            return;

        int completed = 0;
        bool progressed = true;
        while (progressed)
        {
            progressed = false;
            for (int i = 0; i < allResearchNodes.Count; i++)
            {
                ResearchNodeData node = allResearchNodes[i];
                if (node == null || IsResearchUnlocked(node) || !CanStartResearch(node))
                    continue;

                CompleteResearch(node);
                completed++;
                progressed = true;
            }
        }

        Debug.Log($"[Research] Debug completed {completed} node(s).");
    }

    public ResearchSaveData CaptureSave()
    {
        var save = new ResearchSaveData();
        foreach (var node in unlockedResearch)
        {
            if (node != null && !string.IsNullOrEmpty(node.id))
                save.unlockedResearchIds.Add(node.id);
        }

        if (CurrentResearch != null)
            save.currentResearchId = CurrentResearch.id;

        foreach (var pair in submittedItems)
        {
            if (pair.Key == null || string.IsNullOrEmpty(pair.Key.id))
                continue;
            save.submittedItems.Add(new ItemAmountSave
            {
                itemId = pair.Key.id,
                amount = pair.Value
            });
        }

        return save;
    }

    public void ApplySave(ResearchSaveData save)
    {
        GrantStartingUnlocks();
        unlockedResearch.Clear();
        CurrentResearch = null;
        submittedItems.Clear();

        if (save == null)
            return;

        if (save.unlockedResearchIds != null)
        {
            for (int i = 0; i < save.unlockedResearchIds.Count; i++)
            {
                ResearchNodeData node = FindNode(save.unlockedResearchIds[i]);
                if (node != null)
                    CompleteResearch(node, grantReward: false);
            }
        }

        ResearchNodeData current = FindNode(save.currentResearchId);
        if (current != null && CanStartResearch(current))
        {
            CurrentResearch = current;
            if (save.submittedItems != null)
            {
                for (int i = 0; i < save.submittedItems.Count; i++)
                {
                    ItemAmountSave entry = save.submittedItems[i];
                    ItemData item = FindItem(entry.itemId);
                    if (item != null && entry.amount > 0)
                        submittedItems[item] = entry.amount;
                }
            }
        }

        OnUnlocksChanged?.Invoke();
        OnResearchProgressChanged?.Invoke();
    }

    ResearchNodeData FindNode(string id)
    {
        ResearchNodeData fromDb = GameDatabase.FindResearch(id);
        if (fromDb != null)
            return fromDb;

        if (string.IsNullOrEmpty(id) || allResearchNodes == null)
            return null;

        for (int i = 0; i < allResearchNodes.Count; i++)
        {
            ResearchNodeData node = allResearchNodes[i];
            if (node != null && IdsEqual(node.id, id))
                return node;
        }

        return null;
    }

    static bool IdsEqual(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
            return false;
        return a.Trim() == b.Trim();
    }

    static ItemData FindItem(string id)
    {
        return GameDatabase.FindItem(id);
    }
}
