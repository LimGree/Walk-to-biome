using UnityEngine;
using System.IO;
using System.Collections.Generic;

[System.Serializable]
public class SaveData
{
    public List<BuildingSaveData> buildings = new List<BuildingSaveData>();
    public ResearchSaveData research = new ResearchSaveData();
}

[System.Serializable]
public class BuildingSaveData
{
    public string buildingId;
    public Vector3 position;
    public float rotationY;
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

        if (ResearchSystem.Instance != null)
            data.research = ResearchSystem.Instance.CaptureSave();

        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(SavePath, json);
        Debug.Log($"Game saved: {data.buildings.Count} buildings → {SavePath}");
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

        ClearWorldBuildings();

        int spawned = 0;
        if (data.buildings != null)
        {
            for (int i = 0; i < data.buildings.Count; i++)
            {
                if (SpawnBuilding(data.buildings[i], catalog))
                    spawned++;
            }
        }

        if (ResearchSystem.Instance != null)
            ResearchSystem.Instance.ApplySave(data.research);

        Debug.Log($"Game loaded: {spawned} buildings");
    }

    public void DeleteSave()
    {
        if (File.Exists(SavePath))
            File.Delete(SavePath);
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

    static void ClearWorldBuildings()
    {
        BuildingBase[] buildings = Object.FindObjectsByType<BuildingBase>(FindObjectsSortMode.None);
        for (int i = 0; i < buildings.Length; i++)
        {
            BuildingBase b = buildings[i];
            if (b == null) continue;
            b.OnRemoved();
            Object.Destroy(b.gameObject);
        }
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
            building.OnPlaced();
        }
        else if (GridSystem.Instance != null)
        {
            Vector2Int size = GridFootprint.GetRotatedSize(data.size, bsd.rotationY);
            GridFootprint.Register(go, bsd.position, size);
        }

        return true;
    }
}
