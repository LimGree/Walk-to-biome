using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "New Recipe", menuName = "Builderment/Recipe Data")]
public class RecipeData : ScriptableObject
{
    [Header("Basic Info")]
    public string id;
    public string displayName;

    [Header("Recipe")]
    public List<ItemStack> inputs = new List<ItemStack>();
    public List<ItemStack> outputs = new List<ItemStack>();

    [Header("Settings")]
    public float craftTime = 2f;                 // время крафта в секундах
    public BuildingData requiredBuilding;        // какой ассемблер может это крафтить (можно оставить пустым)
}