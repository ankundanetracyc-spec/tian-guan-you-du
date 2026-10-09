using Godot;

/// <summary>兽种的常驻数据：四维按截断正态分布抽取，变种和精英在抽取后修正。</summary>
[GlobalClass]
public partial class BeastTemplate : Resource
{
    [Export] public string species_id = "";
    /// <summary>未配置为0，不擅自用战斗速度代替遁速。</summary>
    [Export] public float dun_speed;
    [Export] public Godot.Collections.Array<Godot.Collections.Dictionary> battle_loot = [];
    [Export] public string display_name = "";
    [Export] public string cultivation_realm = "凡兽";
    [Export] public float physique_min = 5f, physique_mean = 5f, physique_max = 5f;
    [Export] public float intelligence_min = 5f, intelligence_mean = 5f, intelligence_max = 5f;
    [Export] public float agility_min = 5f, agility_mean = 5f, agility_max = 5f;
    [Export] public float soul_min = 5f, soul_mean = 5f, soul_max = 5f;
    [Export] public BeastRacialTalent? racial_talent;
    [Export] public BasicDeckResource? deck;
    [Export] public int initial_hand_size = 5;
    [Export] public int cards_per_turn = 3;
}
