using UnityEngine;
using System.IO;
using System.Collections.Generic;

[System.Serializable]
public class SaveData
{
    public List<BuildingSaveData> buildings = new List<BuildingSaveData>();
    // Потом можно добавить исследования, инвентарь и т.д.
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

        // Находим все здания на сцене
        BuildingBase[] buildings = FindObjectsByType<BuildingBase>(FindObjectsSortMode.None);

        foreach (var building in buildings)
        {
            if (building.data == null) continue;

            BuildingSaveData bsd = new BuildingSaveData
            {
                buildingId = building.data.id,
                position = building.transform.position,
                rotationY = building.transform.eulerAngles.y
            };

            data.buildings.Add(bsd);
        }

        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(SavePath, json);

        Debug.Log("Game saved: " + SavePath);
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

        // Здесь нужна будет база всех BuildingData по id
        // Пока просто заготовка
        Debug.Log($"Loaded {data.buildings.Count} buildings (логика загрузки зданий пока не реализована)");
    }

    public void DeleteSave()
    {
        if (File.Exists(SavePath))
            File.Delete(SavePath);
    }
}