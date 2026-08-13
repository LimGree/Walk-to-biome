using UnityEngine;

[CreateAssetMenu(fileName = "New Building", menuName = "Builderment/Building Data")]
public class BuildingData : ScriptableObject
{
    [Header("Basic Info")]
    public string id;
    public string displayName;
    [TextArea] public string description;

    [Header("Prefabs")]
    public GameObject prefab;
    public GameObject ghostPrefab;

    [Header("Conveyor")]
    [Tooltip("Если задан — это конвейер: в хотбаре один слот, прямой/угол выбирается сам.")]
    public GameObject cornerPrefab;

    [Header("Visuals")]
    public Sprite icon;

    [Header("Grid & Placement")]
    [Tooltip("Клетки footprint: X = по оси X, Y = по оси Z. Pivot префаба = ЦЕНТР footprint.")]
    public Vector2Int size = Vector2Int.one;
    public bool canRotate = true;

    [Header("Optional")]
    public int buildCost = 0;

    public bool IsConveyor => cornerPrefab != null;

    public GameObject GetDefaultPlacePrefab()
    {
        return prefab;
    }
}
