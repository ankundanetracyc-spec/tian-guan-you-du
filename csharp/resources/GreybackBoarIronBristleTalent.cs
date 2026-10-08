using Godot;

/// <summary>铁鬃以硬化鬃毛替代普通厚皮，换取更高物理防御。</summary>
[GlobalClass]
public partial class GreybackBoarIronBristleTalent : BeastRacialTalent
{
    public override void apply_derived_attributes(Beast beast)
    {
        beast.physical_defense=Mathf.RoundToInt(beast.physique*.50f);
    }
}
