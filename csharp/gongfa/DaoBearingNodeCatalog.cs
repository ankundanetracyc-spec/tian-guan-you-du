using System.Collections.Generic;

/// <summary>
/// 主要修行侧重节点的数据目录。
/// 节点奖励与属性门槛放在这里；节点只描述对应侧重的成长，不封锁其他养成线。
/// </summary>
public sealed class DaoBearingNodeDefinition
{
    public string Id { get; }
    public int Bearing { get; }
    public string Name { get; }
    public string Subtitle { get; }
    public string Description { get; }
    public string RewardSummary { get; }
    public bool IsRoot { get; }
    public bool IsPlanned { get; }
    public IReadOnlyDictionary<string, float> RequiredAttributes { get; }
    public IReadOnlyList<string> PrerequisiteIds { get; }
    public IReadOnlyList<string> RewardSkillPaths { get; }

    public DaoBearingNodeDefinition(
        string id,
        int bearing,
        string name,
        string subtitle,
        string description,
        string rewardSummary,
        bool isRoot = false,
        bool isPlanned = false,
        IReadOnlyDictionary<string, float>? requiredAttributes = null,
        IReadOnlyList<string>? prerequisiteIds = null,
        IReadOnlyList<string>? rewardSkillPaths = null)
    {
        Id = id;
        Bearing = bearing;
        Name = name;
        Subtitle = subtitle;
        Description = description;
        RewardSummary = rewardSummary;
        IsRoot = isRoot;
        IsPlanned = isPlanned;
        RequiredAttributes = requiredAttributes ?? new Dictionary<string, float>();
        PrerequisiteIds = prerequisiteIds ?? [];
        RewardSkillPaths = rewardSkillPaths ?? [];
    }
}

public static class DaoBearingNodeCatalog
{
    public static IReadOnlyList<DaoBearingNodeDefinition> For(int bearing) => bearing switch
    {
        1 => BodyNodes(),
        2 => QiNodes(),
        3 => SoulNodes(),
        _ => [],
    };

    public static bool MeetsAttributes(Character character, DaoBearingNodeDefinition node)
    {
        foreach (var (property, required) in node.RequiredAttributes)
        {
            var value = character.Get(property);
            if (value.VariantType is not Godot.Variant.Type.Int and not Godot.Variant.Type.Float) return false;
            if (value.AsSingle() < required) return false;
        }
        return true;
    }

    private static IReadOnlyList<DaoBearingNodeDefinition> BodyNodes() =>
    [
        new DaoBearingNodeDefinition(
            "body_root", 1, "锻躯侧重", "基础技能",
            "当前主修为锻躯时，战斗使用体修基础技能库；更换主修即可切换侧重，三途养成进度保留。淬体对所有修士开放。",
            "当前侧重技能库：锻躯基础技能库\n可装配技能：发劲 · 催血 · 平息 · 卸力 · 撼山",
            isRoot: true),
        new DaoBearingNodeDefinition(
            "body_sword", 1, "体剑流", "节点预留",
            "以肉身承载剑术的锻躯分支。具体属性门槛、前置节点与技能奖励待专题方案确定。",
            "技能奖励待定",
            isPlanned: true,
            prerequisiteIds: ["body_root"]),
    ];

    private static IReadOnlyList<DaoBearingNodeDefinition> QiNodes() =>
    [
        new DaoBearingNodeDefinition(
            "qi_root", 2, "行炁侧重", "基础技能",
            "当前主修为行炁时，战斗使用行炁基础技能库；更换主修即可切换侧重，三途养成进度保留。行炁养成对所有修士开放。",
            "当前侧重技能库：行炁基础技能库",
            isRoot: true),
    ];

    private static IReadOnlyList<DaoBearingNodeDefinition> SoulNodes() =>
    [
        new DaoBearingNodeDefinition(
            "soul_root", 3, "蕴神侧重", "基础技能",
            "当前主修为蕴神时，战斗使用对应基础技能库；更换主修即可切换侧重，三途养成进度保留。观想对所有修士开放。",
            "当前侧重技能库：蕴神基础技能库\n蕴神秘籍化法后续追加",
            isRoot: true),
    ];
}
