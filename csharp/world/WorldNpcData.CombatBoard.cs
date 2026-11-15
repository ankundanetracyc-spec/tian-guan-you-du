using Godot;

public partial class WorldNpcData
{
    public void preserve_legacy_cloud_veil_learning()
    {
        var chapters = learned_gongfa_chapters.GetValueOrDefault(HumanData.XuanShuBookPath,
            learned_gongfa_chapters.GetValueOrDefault("玄枢含章经", new Godot.Collections.Array<string>())).AsGodotArray();
        if (chapters.Any(value => value.AsString() == "yun_pei_2")
            && !legacy_learned_combat_skill_paths.Contains(HumanData.CloudVeilSkillPath))
            legacy_learned_combat_skill_paths.Add(HumanData.CloudVeilSkillPath);
    }
}
