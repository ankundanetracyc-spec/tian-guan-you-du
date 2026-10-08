using Godot;

/// <summary>
/// 锻躯六部位的稳定 ID、炼度门槛与首轮节点奖励。
/// 门槛使用显式累计表，便于后续平衡时只改配置，不改变结算逻辑。
/// </summary>
public static class BodyRefinementRules
{
    public const string Skin = "skin";
    public const string Sinew = "sinew";
    public const string Bone = "bone";
    public const string Blood = "blood";
    public const string Viscera = "viscera";
    public const string Heart = "heart";

    public static readonly string[] Parts = [Skin, Sinew, Bone, Blood, Viscera, Heart];

    private static readonly System.Collections.Generic.Dictionary<string, string> Names = new()
    {
        [Skin] = "皮", [Sinew] = "筋", [Bone] = "骨", [Blood] = "血", [Viscera] = "脏", [Heart] = "心"
    };

    // 每个值是达到对应“炼”的累计炼度，不使用运行时累计公式。
    private static readonly System.Collections.Generic.Dictionary<string, int[]> Thresholds = new()
    {
        [Skin] = [100, 210, 330, 460, 600, 760, 940, 1130, 1330, 1540, 1780, 2030, 2290, 2560, 2840],
        [Sinew] = [105, 220, 345, 480, 625, 790, 970, 1165, 1375, 1600, 1850, 2110, 2380, 2660, 2950],
        [Bone] = [115, 240, 375, 520, 675, 850, 1040, 1240, 1450, 1675, 1940, 2220, 2510, 2810, 3120],
        [Blood] = [100, 215, 335, 465, 605, 770, 950, 1145, 1350, 1570, 1810, 2060, 2320, 2590, 2870],
        [Viscera] = [110, 230, 360, 500, 650, 825, 1015, 1220, 1430, 1660, 1910, 2170, 2440, 2720, 3010],
        [Heart] = [120, 250, 390, 540, 700, 885, 1085, 1290, 1505, 1730, 1985, 2270, 2550, 2820, 3100]
    };

    public readonly record struct Reward(
        int PhysicalDefense,
        int Dodge,
        int MaxHealth,
        int QiBlood,
        int CritResist,
        float HealingBonus,
        float PhysicalDamageReduction);

    public static string GetName(string part) => Names.GetValueOrDefault(part, part);

    public static int GetLevel(string part, float progress)
    {
        if (!Thresholds.TryGetValue(part, out var values)) return 0;
        var level = 0;
        foreach (var threshold in values)
        {
            if (progress < threshold) break;
            level++;
        }
        return level;
    }

    public static int GetThreshold(string part, int level)
    {
        if (!Thresholds.TryGetValue(part, out var values) || level <= 0) return 0;
        return level <= values.Length ? values[level - 1] : values[^1];
    }

    public static int GetNextThreshold(string part, int level) =>
        level >= 15 ? GetThreshold(part, 15) : GetThreshold(part, level + 1);

    public static float GetLevelProgressRatio(string part, float progress)
    {
        var level = GetLevel(part, progress);
        if (level >= 15) return 1f;
        var lower = level == 0 ? 0 : GetThreshold(part, level);
        var upper = GetThreshold(part, level + 1);
        return upper <= lower ? 1f : Mathf.Clamp((progress - lower) / (upper - lower), 0f, 1f);
    }

    public static int GetNodeLevel(int level) => level >= 15 ? 15 : level >= 10 ? 10 : level >= 5 ? 5 : 0;

    public static Reward GetReward(string part, int level)
    {
        var reward = new Reward(0, 0, 0, 0, 0, 0f, 0f);
        if (level >= 5) reward = AddNode(reward, part, 5);
        if (level >= 10) reward = AddNode(reward, part, 10);
        if (level >= 15) reward = AddNode(reward, part, 15);
        return reward;
    }

    private static Reward AddNode(Reward reward, string part, int node) => part switch
    {
        Skin => reward with
        {
            PhysicalDefense = reward.PhysicalDefense + (node == 5 ? 1 : node == 10 ? 2 : 3),
            PhysicalDamageReduction = reward.PhysicalDamageReduction + (node == 5 ? .02f : node == 10 ? .03f : .05f)
        },
        Sinew => reward with { Dodge = reward.Dodge + (node == 5 ? 1 : node == 10 ? 2 : 3) },
        Bone => reward with { MaxHealth = reward.MaxHealth + (node == 5 ? 2 : node == 10 ? 4 : 6) },
        Blood => reward with { QiBlood = reward.QiBlood + (node == 5 ? 1 : node == 10 ? 2 : 3) },
        Viscera => reward with { CritResist = reward.CritResist + (node == 5 ? 1 : node == 10 ? 2 : 3) },
        Heart => reward with { HealingBonus = reward.HealingBonus + (node == 5 ? .01f : node == 10 ? .02f : .03f) },
        _ => reward
    };

    public static string GetRewardText(string part, int level)
    {
        var reward = GetReward(part, level);
        var values = new System.Collections.Generic.List<string>();
        if (reward.PhysicalDefense > 0) values.Add($"物防 +{reward.PhysicalDefense}");
        if (reward.PhysicalDamageReduction > 0) values.Add($"物理减伤 +{reward.PhysicalDamageReduction:P0}");
        if (reward.Dodge > 0) values.Add($"身法 +{reward.Dodge}");
        if (reward.MaxHealth > 0) values.Add($"生命上限 +{reward.MaxHealth}");
        if (reward.QiBlood > 0) values.Add($"气血上限 +{reward.QiBlood}");
        if (reward.CritResist > 0) values.Add($"护心 +{reward.CritResist}");
        if (reward.HealingBonus > 0) values.Add($"受疗加成 +{reward.HealingBonus:P0}");
        return values.Count == 0 ? "尚无五炼节点奖励" : string.Join("、", values);
    }

    /// <summary>兼容尚未迁移到新版十二功用词典的旧材料资源。</summary>
    public static bool UtilityMatches(string actual, string required)
    {
        if (actual == required) return true;
        var aliases = required switch
        {
            "固" => new[] { "固本", "坚韧", "不坏" },
            "护" => new[] { "护体", "坚韧", "卸力", "辟邪", "净秽" },
            "生" => new[] { "生发", "回春", "续骨", "造血", "延年", "培元" },
            "行" => new[] { "通脉", "纳气", "轻盈", "疾行", "御风" },
            "镇" => new[] { "镇魂", "凝神", "明心" },
            _ => System.Array.Empty<string>()
        };
        return aliases.Contains(actual);
    }
}
