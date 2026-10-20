using Godot;

/// <summary>影足变种保留群猎本能，并以轻捷步法增加身法。</summary>
[GlobalClass]
public partial class ForestWolfShadowfootTalent : BeastRacialTalent
{
    public override void apply_derived_attributes(Beast beast) => beast.dodge+=Mathf.RoundToInt(Mathf.Min(beast.physique*.25f,beast.soul*.30f));
}
