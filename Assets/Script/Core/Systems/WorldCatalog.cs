using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public class WorldInfo
{
    public string id;
    public string name;
    public int seed;
    public string created;
    public string lastPlayed;
}

[Serializable]
public class WorldIndex
{
    public List<WorldInfo> worlds = new List<WorldInfo>();
}

public static class WorldCatalog
{
    public static WorldInfo Active { get; private set; }
    public static bool HasActive => Active != null && !string.IsNullOrEmpty(Active.id);

    static string Root => Path.Combine(Application.persistentDataPath, "worlds");
    static string IndexPath => Path.Combine(Root, "index.json");

    public static string SavePath(WorldInfo world)
    {
        if (world == null || string.IsNullOrEmpty(world.id))
            return null;
        return Path.Combine(Root, world.id, "save.json");
    }

    public static string ActiveSavePath => SavePath(Active);

    public static List<WorldInfo> ListWorlds()
    {
        WorldIndex index = ReadIndex();
        index.worlds.Sort((a, b) => string.CompareOrdinal(b.lastPlayed, a.lastPlayed));
        return index.worlds;
    }

    public static WorldInfo CreateWorld(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            name = "Новый мир";

        Directory.CreateDirectory(Root);
        var world = new WorldInfo
        {
            id = "world_" + DateTime.UtcNow.Ticks,
            name = name.Trim(),
            seed = UnityEngine.Random.Range(1, 999999),
            created = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
            lastPlayed = DateTime.Now.ToString("yyyy-MM-dd HH:mm")
        };

        WorldIndex index = ReadIndex();
        index.worlds.Add(world);
        WriteIndex(index);

        Directory.CreateDirectory(Path.Combine(Root, world.id));
        var save = new SaveData
        {
            version = SaveData.CurrentVersion,
            worldName = world.name,
            seed = world.seed
        };
        File.WriteAllText(SavePath(world), JsonUtility.ToJson(save, true));
        return world;
    }

    public static void DeleteWorld(string id)
    {
        if (string.IsNullOrEmpty(id))
            return;

        WorldIndex index = ReadIndex();
        index.worlds.RemoveAll(w => w != null && w.id == id);
        WriteIndex(index);

        string dir = Path.Combine(Root, id);
        if (Directory.Exists(dir))
            Directory.Delete(dir, true);

        if (Active != null && Active.id == id)
            Active = null;
    }

    public static void SetActive(WorldInfo world)
    {
        Active = world;
        if (world == null)
            return;
        world.lastPlayed = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        WorldIndex index = ReadIndex();
        for (int i = 0; i < index.worlds.Count; i++)
        {
            if (index.worlds[i] != null && index.worlds[i].id == world.id)
                index.worlds[i].lastPlayed = world.lastPlayed;
        }
        WriteIndex(index);
    }

    public static void ClearActive()
    {
        Active = null;
    }

    static WorldIndex ReadIndex()
    {
        if (!File.Exists(IndexPath))
            return new WorldIndex();
        try
        {
            WorldIndex index = JsonUtility.FromJson<WorldIndex>(File.ReadAllText(IndexPath));
            return index ?? new WorldIndex();
        }
        catch
        {
            return new WorldIndex();
        }
    }

    static void WriteIndex(WorldIndex index)
    {
        Directory.CreateDirectory(Root);
        File.WriteAllText(IndexPath, JsonUtility.ToJson(index ?? new WorldIndex(), true));
    }
}
