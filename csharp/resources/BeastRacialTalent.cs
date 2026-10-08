using Godot;

/// <summary>
/// 妖兽兽种固有的被动天赋，不属于功法、不占用卡牌。
/// 子类可直接覆写派生属性：例如厚皮按体魄提供物理防御，或改变某一兽种的生命计算。
/// </summary>
[GlobalClass]
public partial class BeastRacialTalent : Resource
{
    [Export] public string talent_id = "";
    [Export] public string talent_name = "";
    [Export(PropertyHint.MultilineText)] public string description = "";

    /// <summary>在妖兽通用派生属性计算完成后调用；仅修改本兽种需要改变的项目。</summary>
    public virtual void apply_derived_attributes(Beast beast) { }
}
