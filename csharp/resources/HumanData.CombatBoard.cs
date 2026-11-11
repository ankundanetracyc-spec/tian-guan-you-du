using Godot;

public partial class HumanData
{
    public const string CloudVeilSkillPath = "res://resources/activeskills/玄枢含章经/云帔.tres";
    public const string XuanShuBookPath = "res://resources/gongfa/玄枢含章经.tres";

    /// <summary>仅对旧版本保存的明确习得事实迁移，不给新角色推导节点或技能。</summary>
    public void preserve_legacy_cloud_veil_learning()
    {
        var oldChapters = learned_gongfa_chapters.GetValueOrDefault(XuanShuBookPath, new Godot.Collections.Array<string>()).AsGodotArray();
        var learned = oldChapters.Any(value => value.AsString() == "yun_pei_2")
            || combat_skill_slot_paths.Contains(CloudVeilSkillPath)
            || combat_skill_references.Any(reference => reference?.Key == CloudVeilSkillPath);
        if (learned && !legacy_learned_combat_skill_paths.Contains(CloudVeilSkillPath))
            legacy_learned_combat_skill_paths.Add(CloudVeilSkillPath);
    }
}
