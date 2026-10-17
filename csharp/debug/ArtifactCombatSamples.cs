using Godot;

/// <summary>仅由开发测试场景调用；不注册物品、奖励、配方或正式菜单。</summary>
public static class ArtifactCombatSamples
{
    public static ArtifactInstanceData Create(bool guard, bool inner, string id)
    {
        return new ArtifactInstanceData
        {
            instance_id = id, artifact_name = guard ? "守锋剑" : "鸣锋剑",
            form = new ArtifactFormData { form_id = guard ? "shoufeng" : "mingfeng", form_name = guard ? "守锋剑" : "鸣锋剑",
                icon_path = guard ? "res://textures/ui/combat/skill_icons/凡俗_架招_v1.png" : "res://textures/ui/combat/skill_icons/凡俗_痛击_v1.png" },
            ritual_mode = inner ? "内炼" : "外祭", utility_id = guard ? "固" : "破", utility_grade = 1,
            bound_basic_card_path = inner ? guard ? "res://resources/activeskills/架招.tres" : "res://resources/activeskills/应敌.tres" : "",
            spirit_cost = inner ? 0 : guard ? 3 : 2, outer_body = guard ? 18 : 12,
            max_durability = guard ? 100 : 80, current_durability = guard ? 100 : 80,
            deploy_energy_cost = guard ? 2 : 1, deploy_mana_cost = 0,
            combat_profile = new ArtifactCombatProfile
            {
                physical_defense_ignore_ratio = guard ? 0 : .2f, inner_block_bonus = guard ? 2 : 0,
                use_energy_cost = 1, use_mana_cost = 1, use_base_damage = guard ? 0 : 10, use_shield = guard ? 6 : 0
            }
        };
    }
}
