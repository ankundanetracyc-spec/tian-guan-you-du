using Godot;

/// <summary>灰背野猪的固有兽种天赋：厚皮按当前体魄提供物理防御。</summary>
[GlobalClass]
public partial class GreybackBoarThickHideTalent : BeastRacialTalent
{
    public override void apply_derived_attributes(Beast beast)
    {
        beast.physical_defense = Mathf.RoundToInt(beast.physique * .30f);
    }
}
