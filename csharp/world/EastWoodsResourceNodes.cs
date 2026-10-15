using System;
using System.Collections.Generic;

public enum EastWoodsResourceCategory
{
    None = 0,
    Plant = 1,
    Mineral = 2,
}

/// <summary>Data catalog for visible, category-gated East Woods resource nodes.</summary>
public static class EastWoodsResourceNodes
{
    public sealed record Definition(
        string Id,
        EastWoodsResourceCategory Category,
        string CategoryLabel,
        string DisplayName,
        EastWoodsActions.ActionDef Action);

    private static readonly Dictionary<string, Definition> Definitions = new()
    {
        ["common_herb"] = new("common_herb", EastWoodsResourceCategory.Plant, "植物", "苦苔根",
            new("gather_root", "采集", "蕨叶下生着几丛苦苔根。每次可采两份，此处现有余量有限；资源会随时间缓慢恢复。", 1, 2,
                "你收好苦苔根 ×2。", "res://resources/entities/items/苦苔根.tres", 2, 4,
                Recovery: EastWoodsActions.ResourceRecoveryGroup.Common)),
        ["common_moss"] = new("common_moss", EastWoodsResourceCategory.Plant, "植物", "月露苔",
            new("gather_moss", "采集", "浅洼背阴处附着月露苔。每次可采一份，此处现有余量有限；资源会随时间缓慢恢复。", 1, 2,
                "你收好月露苔 ×1。", "res://resources/entities/items/月露苔.tres", 1, 2,
                Recovery: EastWoodsActions.ResourceRecoveryGroup.Common)),
        ["mistbell_slow"] = new("mistbell_slow", EastWoodsResourceCategory.Plant, "植物", "雾铃花",
            new("gather_mistbell", "采集", "湿润林缘生着少量雾铃花。每次可采一份，余量有限；此点按30日慢生周期恢复。", 1, 2,
                "你采下雾铃花 ×1。", "res://resources/entities/items/雾铃花.tres", 1, 2,
                Recovery: EastWoodsActions.ResourceRecoveryGroup.SlowGrowth)),
        ["spirit_stone_vein"] = new("spirit_stone_vein", EastWoodsResourceCategory.Mineral, "矿产", "灵石",
            new("mine_spirit_stone", "采矿", "岩层裂隙中有一处专门灵石脉。此处独立限量采掘，不表示普通岩格都产灵石。每次可采一份；按90日矿脉周期恢复。", 1, 2,
                "你采得灵石 ×1。", "res://resources/entities/items/灵石.tres", 1, 2,
                Recovery: EastWoodsActions.ResourceRecoveryGroup.Mineral)),
    };

    public static Definition? Find(string id)
        => Definitions.TryGetValue(id, out var definition) ? definition : null;

    public static Definition? ForNode(string nodeId)
        => ForNode(EastWoodsDungeonRules.get_node(nodeId));

    public static Definition? ForNode(EastWoodsDungeonRules.NodeDefinition? node)
    {
        if (node is null || node.ResourceCategory == EastWoodsResourceCategory.None || string.IsNullOrWhiteSpace(node.ResourceNodeId))
            return null;
        var definition = Find(node.ResourceNodeId);
        return definition?.Category == node.ResourceCategory ? definition : null;
    }

    public static bool HasResourceTag(EastWoodsDungeonRules.NodeDefinition node)
        => node.ResourceCategory != EastWoodsResourceCategory.None || !string.IsNullOrWhiteSpace(node.ResourceNodeId);
}
