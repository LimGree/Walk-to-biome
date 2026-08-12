using UnityEngine;

[CreateAssetMenu(fileName = "New Building", menuName = "Builderment/Building Data")]
public class BuildingData : ScriptableObject
{
    [Header("Basic Info")]
    public string id;
    public string displayName;
    [TextArea] public string description;

    [Header("Prefabs")]
    public GameObject prefab;            // основной префаб (здания / fallback)
    public GameObject ghostPrefab;       // ghost для предпросмотра

    [Header("Conveyor variants (optional)")]
    [Tooltip("Прямая лента. Если задан — hotbar «Conveyor» может выбирать форму.")]
    public GameObject straightPrefab;
    [Tooltip("Угловая лента 90°.")]
    public GameObject cornerPrefab;
    [Tooltip("Опциональный ghost для угла. Если null — используется ghostPrefab.")]
    public GameObject cornerGhostPrefab;

    [Header("Visuals")]
    public Sprite icon;

    [Header("Grid & Placement")]
    public Vector2Int size = Vector2Int.one;
    public bool canRotate = true;

    [Header("Optional")]
    public int buildCost = 0;

    /// <summary>Есть ли отдельные префабы ленты (straight/corner).</summary>
    public bool HasConveyorVariants =>
        straightPrefab != null || cornerPrefab != null;

    /// <summary>Считать ли этот BuildingData конвейером.</summary>
    public bool IsConveyor
    {
        get
        {
            if (HasConveyorVariants)
                return true;
            if (prefab == null)
                return false;
            return prefab.GetComponent<ConveyorBelt>() != null;
        }
    }

    public GameObject GetConveyorPrefab(bool isCorner)
    {
        if (isCorner && cornerPrefab != null)
            return cornerPrefab;
        if (straightPrefab != null)
            return straightPrefab;
        return prefab;
    }

    public GameObject GetDefaultPlacePrefab()
    {
        if (straightPrefab != null)
            return straightPrefab;
        return prefab;
    }
}
