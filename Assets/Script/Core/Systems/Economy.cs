using UnityEngine;

public static class Economy
{
    public const int StartingCoins = 250;
    public const int StartingRubies = 0;
    public const int CoinsPerRuby = 50;
    public const int BeltBaseGears = 10;
    public const float BeltCostGrowth = 1.1f;
    public const float BeltSpeedPerLevel = 0.15f;

    public static int SellValue(ItemData item)
    {
        if (item == null)
            return 0;
        if (item.sellValue > 0)
            return item.sellValue;

        string id = item.id != null ? item.id.ToLowerInvariant() : "";
        if (item.isFluid)
            return 2;
        if (Contains(id, "ai_module", "quantum", "nano_wire"))
            return 16;
        if (Contains(id, "advanced", "computer_chip", "battery"))
            return 10;
        if (Contains(id, "circuit", "motor", "cable"))
            return 6;
        if (Contains(id, "gear", "wire", "glass", "silicon", "plastic", "rubber"))
            return 4;
        if (Contains(id, "ingot", "plate", "rod", "beam", "brick", "plank"))
            return 3;
        if (Contains(id, "ore", "log", "stone", "sand", "coal", "sulfur"))
            return 1;
        return 2;
    }

    public static int BuildCost(BuildingData data)
    {
        if (data == null)
            return 0;
        if (data.buildCost > 0)
            return data.buildCost;

        string id = data.id != null ? data.id.ToLowerInvariant() : "";
        if (data.IsConveyor || data.isPipe || Contains(id, "conveyor", "pipe"))
            return 2;
        if (Contains(id, "splitter"))
            return 12;
        if (Contains(id, "storage", "tank"))
            return 20;
        if (Contains(id, "extractor") && Contains(id, "water", "oil"))
            return 40;
        if (Contains(id, "extractor"))
            return 15;
        if (Contains(id, "smelter"))
            return 25;
        if (Contains(id, "constructor"))
            return 35;
        if (Contains(id, "assembler"))
            return 60;
        if (Contains(id, "chemical"))
            return 100;
        if (Contains(id, "refinery"))
            return 80;
        if (Contains(id, "lab", "research"))
            return 80;
        if (Contains(id, "arm", "robot"))
            return 30;
        int cells = Mathf.Max(1, data.size.x * data.size.y);
        return 10 * cells;
    }

    public static int UpgradeCost(BuildingBase building)
    {
        int baseCost = BuildCost(building != null ? building.data : null);
        return Mathf.Max(20, baseCost * 2);
    }

    public static int RubyReward(ResearchNodeData node)
    {
        if (node == null)
            return 0;
        if (node.rubyReward > 0)
            return node.rubyReward;

        int items = 0;
        if (node.requiredItems != null)
        {
            for (int i = 0; i < node.requiredItems.Count; i++)
                items += Mathf.Max(0, node.requiredItems[i].amount);
        }

        return Mathf.Max(5, 4 + items / 8);
    }

    public static int BeltUpgradeCost(int nextLevel)
    {
        float cost = BeltBaseGears;
        for (int i = 1; i < Mathf.Max(1, nextLevel); i++)
            cost *= BeltCostGrowth;
        return Mathf.Max(1, Mathf.RoundToInt(cost));
    }

    public static float BeltMultiplier(int level)
    {
        return 1f + Mathf.Max(0, level) * BeltSpeedPerLevel;
    }

    public static bool IsGear(ItemData item)
    {
        if (item == null || string.IsNullOrEmpty(item.id))
            return false;
        string id = item.id.Trim().ToLowerInvariant();
        return id == "gear" || id == "gears";
    }

    static bool Contains(string id, params string[] parts)
    {
        if (string.IsNullOrEmpty(id))
            return false;
        for (int i = 0; i < parts.Length; i++)
        {
            if (id.Contains(parts[i]))
                return true;
        }
        return false;
    }
}
