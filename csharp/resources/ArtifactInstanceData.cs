using Godot;
using Godot.Collections;

/// <summary>
/// 一次炼制产出的唯一法宝实例。法宝不与普通材料按数量合并，但和普通物品一样归属于角色行囊。
/// </summary>
public partial class ArtifactInstanceData : Resource
{
    [Export] public string instance_id = "";
    [Export] public string artifact_name = "";
    [Export] public ArtifactFormData? form;
    [Export(PropertyHint.Enum, "内炼,外祭")] public string ritual_mode = "内炼";
    [Export] public string bound_basic_card_path = "";
    [Export] public string utility_id = "";
    [Export(PropertyHint.Range, "1,9,1")] public int utility_grade;
    [Export(PropertyHint.Enum, "凡器,灵器,玄器,宝器,道器")] public string artifact_tier = "凡器";
    [Export] public string element = "";
    [Export(PropertyHint.Range, "-3,3,1")] public int polarity;
    [Export] public int spirit_cost;
    [Export] public int max_durability;
    [Export] public int current_durability;
    [Export] public int outer_body;
    [Export] public int deploy_energy_cost;
    [Export] public int deploy_mana_cost;
    [Export] public Array<ArtifactInscriptionData> inscriptions = [];
    [Export] public ArtifactRecipeRecord recipe = new();
    [Export] public ArtifactCombatProfile? combat_profile;
    /// <summary>新炼器的固定快照。为空时沿用旧版实例；不将新效果塞进内炼/外祭等旧字段。</summary>
    [Export] public string forged_snapshot_json = "";

    public string ensure_instance_id()
    {
        if (string.IsNullOrWhiteSpace(instance_id))
            instance_id = $"artifact-{Time.GetTicksMsec()}-{GD.Randi():X8}";
        return instance_id;
    }
}
