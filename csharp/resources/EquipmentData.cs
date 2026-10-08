using Godot;
using Godot.Collections;

/// <summary>装备定义资源，包含武器、防具和饰品的专有加成。</summary>
public partial class EquipmentData : ItemData
{
    [Export] public int equip_type;
    [Export] public int quality = 1;
    [Export] public int weapon_type;
    [Export] public Dictionary fixed_bonuses = [];
    [Export] public Dictionary mult_bonuses = [];
    [Export] public Array<string> weapon_skill_paths = [];
}
