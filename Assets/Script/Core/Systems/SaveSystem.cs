using UnityEngine;
using System.IO;
using System.Collections.Generic;

[System.Serializable]
public class SaveData
{
    public List<BuildingSaveData> buildings = new List<BuildingSaveData>();
    public List<ConveyorSaveData> conveyors = new List<ConveyorSaveData>();
}

[System.Serializable]
public class BuildingSaveData
{
    public string buildingId;
    public Vector3 position;
    public float rotationY;
}

[System.Serializable]
public class ConveyorSaveData
{
    public string buildingId;
    public Vector3 position;
    public float rotationY;
    public bool isCorner;
    public bool mirrorX;
}

public class SaveSystem : MonoBehaviour
{
    public static SaveSystem Instance { get; private set; }

    private string SavePath => Path.Combine(Application.persistentDataPath, "save.json");

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void SaveGame()
    {
        SaveData data = new SaveData();

        BuildingBase[] buildings = FindObjectsByType<BuildingBase>(FindObjectsSortMode.None);
        for (int i = 0; i < buildings.Length; i++)
        {
            BuildingBase building = buildings[i];
            if (building == null || building.data == null) continue;
            if (string.IsNullOrEmpty(building.data.id)) continue;

            data.buildings.Add(new BuildingSaveData
            {
                buildingId = building.data.id,
                position = building.transform.position,
                rotationY = building.transform.eulerAngles.y
            });
        }

        ConveyorBelt[] belts = FindObjectsByType<ConveyorBelt>(FindObjectsSortMode.None);
        for (int i = 0; i < belts.Length; i++)
        {
            ConveyorBelt belt = belts[i];
            if (belt == null) continue;
            // Ghost / disabled previews
            if (!belt.enabled || !belt.gameObject.activeInHierarchy) continue;

            string id = belt.buildingData != null ? belt.buildingData.id : null;
            if (string.IsNullOrEmpty(id))
                continue;

            data.conveyors.Add(new ConveyorSaveData
            {
                buildingId = id,
                position = belt.transform.position,
                rotationY = belt.transform.eulerAngles.y,
                isCorner = belt.isCorner,
                mirrorX = belt.transform.localScale.x < 0f
            });
        }

        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(SavePath, json);

        Debug.Log($"Game saved: {data.buildings.Count} buildings, {data.conveyors.Count} conveyors → {SavePath}");
    }

    public void LoadGame()
    {
        if (!File.Exists(SavePath))
        {
            Debug.Log("No save file found");
            return;
        }

        string json = File.ReadAllText(SavePath);
        SaveData data = JsonUtility.FromJson<SaveData>(json);
        if (data == null)
        {
            Debug.LogError("[SaveSystem] Failed to parse save file");
            return;
        }

        BuildingData[] catalog = ResolveBuildingCatalog();
        if (catalog == null || catalog.Length == 0)
        {
            Debug.LogError("[SaveSystem] No BuildingData catalog (PlayerInventory.allBuildings)");
            return;
        }

        ClearWorldPlaceables();

        int spawnedBuildings = 0;
        if (data.buildings != null)
        {
            for (int i = 0; i < data.buildings.Count; i++)
            {
                BuildingSaveData bsd = data.buildings[i];
                if (SpawnBuilding(bsd, catalog))
                    spawnedBuildings++;
            }
        }

        int spawnedBelts = 0;
        if (data.conveyors != null)
        {
            for (int i = 0; i < data.conveyors.Count; i++)
            {
                ConveyorSaveData csd = data.conveyors[i];
                if (SpawnConveyor(csd, catalog))
                    spawnedBelts++;
            }
        }

        // Единый reconnect после полной загрузки сети
        AutoConnector.ReconnectAll();

        Debug.Log($"Game loaded: {spawnedBuildings} buildings, {spawnedBelts} conveyors + ReconnectAll");
    }

    public void DeleteSave()
    {
        if (File.Exists(SavePath))
            File.Delete(SavePath);
    }

    /// <summary>Только пересобрать связи (после ручного спавна / отладка).</summary>
    public void ReconnectLogistics()
    {
        AutoConnector.ReconnectAll();
    }

    static BuildingData[] ResolveBuildingCatalog()
    {
        PlayerInventory inv = Object.FindFirstObjectByType<PlayerInventory>();
        if (inv != null && inv.allBuildings != null && inv.allBuildings.Length > 0)
            return inv.allBuildings;
        return null;
    }

    static BuildingData FindBuildingData(string id, BuildingData[] catalog)
    {
        if (string.IsNullOrEmpty(id) || catalog == null)
            return null;

        for (int i = 0; i < catalog.Length; i++)
        {
            BuildingData d = catalog[i];
            if (d != null && d.id == id)
                return d;
        }
        return null;
    }

    static void ClearWorldPlaceables()
    {
        // Ленты
        ConveyorBelt[] belts = Object.FindObjectsByType<ConveyorBelt>(FindObjectsSortMode.None);
        for (int i = 0; i < belts.Length; i++)
        {
            ConveyorBelt belt = belts[i];
            if (belt == null) continue;
            if (!belt.enabled) continue; // ghost

            belt.ClearItems();
            AutoConnector.ClearBeltLinks(belt);
            belt.OnRemoved();
            belt.suppressDestroyCleanup = true;
            Object.Destroy(belt.gameObject);
        }

        BuildingBase[] buildings = Object.FindObjectsByType<BuildingBase>(FindObjectsSortMode.None);
        for (int i = 0; i < buildings.Length; i++)
        {
            BuildingBase b = buildings[i];
            if (b == null) continue;
            b.OnRemoved();
            Object.Destroy(b.gameObject);
        }

        // На всякий случай — если остались мёртвые записи
        // (Destroy отложен, occupancy уже снят в OnRemoved)
    }

    static bool SpawnBuilding(BuildingSaveData bsd, BuildingData[] catalog)
    {
        if (bsd == null) return false;

        BuildingData data = FindBuildingData(bsd.buildingId, catalog);
        if (data == null || data.prefab == null)
        {
            Debug.LogWarning($"[SaveSystem] Missing building prefab for id={bsd.buildingId}");
            return false;
        }

        Quaternion rot = Quaternion.Euler(0f, bsd.rotationY, 0f);
        GameObject go = Object.Instantiate(data.prefab, bsd.position, rot);
        BuildingBase building = go.GetComponent<BuildingBase>();
        if (building != null)
        {
            building.data = data;
            // OnPlaced: register + reshape (сеть ещё не полная — reshape ок, connect в конце)
            bool prev = ConveyorBelt.SuppressReshapeOnPlaced;
            ConveyorBelt.SuppressReshapeOnPlaced = true;
            try
            {
                building.OnPlaced();
            }
            finally
            {
                ConveyorBelt.SuppressReshapeOnPlaced = prev;
            }
        }
        else
        {
            if (GridSystem.Instance != null)
            {
                Vector2Int origin = GridSystem.Instance.WorldToCell(bsd.position);
                Vector2Int size = GridOccupancy.GetRotatedSize(
                    data.size, bsd.rotationY);
                GridOccupancy.Register(go, origin, size);
            }
        }

        return true;
    }

    static bool SpawnConveyor(ConveyorSaveData csd, BuildingData[] catalog)
    {
        if (csd == null) return false;

        BuildingData data = FindBuildingData(csd.buildingId, catalog);
        if (data == null)
        {
            Debug.LogWarning($"[SaveSystem] Missing conveyor BuildingData id={csd.buildingId}");
            return false;
        }

        GameObject prefab = data.GetConveyorPrefab(csd.isCorner);
        if (prefab == null)
        {
            Debug.LogWarning($"[SaveSystem] Missing conveyor prefab id={csd.buildingId}");
            return false;
        }

        Quaternion rot = Quaternion.Euler(0f, csd.rotationY, 0f);
        GameObject go = Object.Instantiate(prefab, csd.position, rot);

        if (csd.mirrorX)
        {
            Vector3 scale = go.transform.localScale;
            scale.x = -Mathf.Abs(scale.x);
            go.transform.localScale = scale;
        }

        ConveyorBelt belt = go.GetComponent<ConveyorBelt>();
        if (belt != null)
        {
            belt.buildingData = data;
            belt.isCorner = csd.isCorner;
            ConveyorReshape.DefaultConveyorData = data;

            bool prev = ConveyorBelt.SuppressReshapeOnPlaced;
            ConveyorBelt.SuppressReshapeOnPlaced = true;
            try
            {
                belt.OnPlaced();
            }
            finally
            {
                ConveyorBelt.SuppressReshapeOnPlaced = prev;
            }
        }

        return true;
    }
}
