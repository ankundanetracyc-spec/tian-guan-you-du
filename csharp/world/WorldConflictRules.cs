using Godot;

/// <summary>显式地点禁斗；子场所优先于节点，友好训练不属于私斗。</summary>
public static class WorldConflictRules
{
    public enum Policy { Inherit, Allowed, Forbidden }
    public static bool Forbidden(WorldNodeData? node, string subsceneId)
    {
        foreach (var space in node?.subscenes ?? [])
            if (space.subscene_id == subsceneId && space.hostile_combat_policy != Policy.Inherit)
                return space.hostile_combat_policy == Policy.Forbidden;
        if (node is not null && node.hostile_combat_policy != Policy.Inherit)
            return node.hostile_combat_policy == Policy.Forbidden;
        // 这是本次明确配置的地点表，不能以City/Sect类型或背景图片猜测。
        return node?.node_id is "white_stone_town" or "yuxu_xianzong_core";
    }
}
