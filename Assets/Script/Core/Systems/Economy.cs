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
        return item != null ? Mathf.Max(0, item.sellValue) : 0;
    }

    public static int BuildCost(BuildingData data)
    {
        return data != null ? Mathf.Max(0, data.buildCost) : 0;
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
}
