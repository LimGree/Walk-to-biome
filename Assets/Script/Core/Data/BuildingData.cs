using UnityEngine;

[CreateAssetMenu(fileName = "New Building", menuName = "Builderment/Building Data")]
public class BuildingData : ScriptableObject
{
    [Header("Basic Info")]
    public string id;
    public string displayName;
    [TextArea] public string description;

    [Header("Prefabs")]
    public GameObject prefab;            // настоящий префаб здания
    public GameObject ghostPrefab;       // полупрозрачный ghost для строительства

    [Header("Visuals")]
    public Sprite icon;                  // иконка в меню строительства / hotbar

    [Header("Grid & Placement")]
    public Vector2Int size = Vector2Int.one;     // размер в клетках (1x1, 2x2 и т.д.)
    public bool canRotate = true;

    [Header("Optional")]
    public int buildCost = 0;            // если потом появится валюта/ресурсы на строительство
}