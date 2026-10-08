using Godot;
using Godot.Collections;

/// <summary>法宝器形定义的最小数据底座；具体器形与炼制服务可在此基础上继续扩展。</summary>
public partial class ArtifactFormData : Resource
{
    [Export] public string form_id = "";
    [Export] public string form_name = "";
    [Export] public string icon_path = "";
    [Export] public Array<string> allowed_textures = [];
    [Export] public Array<string> accepted_utilities = [];
    [Export] public int inner_spirit_base;
    [Export] public int outer_spirit_base;
    [Export] public int outer_energy_cost;
    [Export] public int outer_mana_cost;
    [Export] public int outer_body_base;
    [Export] public int durability_base;
    [Export] public string inner_trigger_id = "";
    [Export] public string outer_trigger_id = "";
    [Export] public int response_limit = 1;
}

/// <summary>法宝实例上的单条铭文；这里只保存数据，不在行囊层解释战斗效果。</summary>
public partial class ArtifactInscriptionData : Resource
{
    [Export] public string inscription_id = "";
    [Export] public string display_name = "";
    [Export] public Dictionary modifiers = [];
}

/// <summary>法宝炼制追溯记录；用于详情、修复和存档兼容。</summary>
public partial class ArtifactRecipeRecord : Resource
{
    [Export] public Array<string> material_definition_ids = [];
    [Export] public Dictionary material_slots = [];
    [Export] public string fuel_id = "";
    [Export] public Dictionary result_summary = [];
    [Export] public int created_hour;
}

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

    public string ensure_instance_id()
    {
        if (string.IsNullOrWhiteSpace(instance_id))
            instance_id = $"artifact-{Time.GetTicksMsec()}-{GD.Randi():X8}";
        return instance_id;
    }
}
