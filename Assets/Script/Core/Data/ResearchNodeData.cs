using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "New Research", menuName = "Builderment/Research Node")]
public class ResearchNodeData : ScriptableObject
{
    [Header("Basic Info")]
    public string id;
    public string displayName;
    [TextArea] public string description;
    public Sprite icon;

    [Header("Requirements")]
    public List<ItemStack> requiredItems = new List<ItemStack>();   // что нужно сдать в Research Lab
    public List<ResearchNodeData> requiredResearches;               // какие исследовани€ должны быть открыты до этого

    [Header("Rewards")]
    public List<BuildingData> unlockedBuildings;    // какие здани€ открывает
    public List<RecipeData> unlockedRecipes;        // какие рецепты открывает
    // ћожно потом добавить и другие награды (скорость конвейеров и т.д.)
}