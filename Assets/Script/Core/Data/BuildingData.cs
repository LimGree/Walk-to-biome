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
    [Tooltip("Гост угла для превью линии. Если пусто — берётся cornerPrefab.")]
    public GameObject cornerGhostPrefab;
    [Tooltip("Труба: ставится линией, как конвейер, но возит только жидкости.")]
    public bool isPipe;

    [Header("Visuals")]
    public Sprite icon;

    [Header("Grid & Placement")]
    [Tooltip("Клетки footprint: X = по оси X, Y = по оси Z. Pivot префаба = ЦЕНТР footprint.")]
    public Vector2Int size = Vector2Int.one;
    public bool canRotate = true;

    [Header("Economy")]
    [Tooltip("Цена установки в монетах.")]
    public int buildCost = 0;

    [Header("Placement")]
    [Tooltip("Ставить только на жилу. Для экстрактора и будущих шахтёров.")]
    public bool requiresResourceNode;
    [Tooltip("Можно ставить на воду. Для водокачки.")]
    public bool allowOnWater;
    [Tooltip("Только на воду (озеро / океан). Для водокачки.")]
    public bool requiresWater;

    public bool IsConveyor => cornerPrefab != null || isPipe;

    public GameObject GetDefaultPlacePrefab()
    {
        return prefab;
    }
}
