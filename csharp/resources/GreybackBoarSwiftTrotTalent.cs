using Godot;

/// <summary>迅蹄保留厚皮，并把轻快四蹄转化为额外行动速度。</summary>
[GlobalClass]
public partial class GreybackBoarSwiftTrotTalent : BeastRacialTalent
{
    public override void apply_derived_attributes(Beast beast)
    {
        beast.physical_defense=Mathf.RoundToInt(beast.physique*.30f);
        beast.initiative_speed+=Mathf.Min(beast.physique*.5f,beast.soul*.5f);
    }
}
