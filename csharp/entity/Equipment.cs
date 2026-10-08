using Godot;
using Godot.Collections;

public partial class Equipment : Item
{
    public enum EquipType { WEAPON, ARMOR, ACCESSORY }
    public enum Quality { ROUGH, NORMAL, EXQUISITE, FLAWLESS, PERFECT }
    public enum WeaponType { NONE, SWORD, BLADE, SPEAR, STAFF, DAGGER, GLOVE, BOW, WHIP, POLEARM, AXE }
    private static readonly string[] QualityNames=["粗糙","普通","精致","无暇","完美"], TypeNames=["武器","防具","饰品"], WeaponNames=["空手","剑","刀","枪","棍","匕首","拳套","弓","鞭","戟","斧"];
    private static readonly float[] QualityMults=[.7f,1,1.3f,1.6f,2];
    public int equip_type,quality=1,weapon_type,equipped_slot=-1; public GodotObject? equipped_on; public bool consumed_this_battle; public Dictionary fixed_bonuses=[],mult_bonuses=[]; private Array<Resource> weapon_skills=[];
    public override void _Ready(){base._Ready();item_category="装备";item_subcategory=get_equip_type_name();can_stack=false;max_stack=1;update_equipment_visual();}
    public override void load_from_data(EntityData source){base.load_from_data(source);if(source is EquipmentData data){equip_type=data.equip_type;quality=data.quality;weapon_type=data.weapon_type;fixed_bonuses=data.fixed_bonuses.Duplicate();mult_bonuses=data.mult_bonuses.Duplicate();weapon_skills=[];foreach(var path in data.weapon_skill_paths)if(ResourceLoader.Load<Resource>(path) is { } skill)weapon_skills.Add(skill);}item_category="装备";item_subcategory=get_equip_type_name();can_stack=false;max_stack=1;update_equipment_visual();}
    public void update_equipment_visual(){if(label is null)return;label.Text=$"{entity_name} [{get_quality_name()}]";label.Modulate=quality switch{0=>new(.7f,.5f,.3f),2=>new(.4f,.7f,1),3=>new(.7f,.3f,.9f),4=>new(1,.7f,0),_=>Colors.White};}
    public Dictionary get_effective_fixed_bonuses()=>scale_bonuses(fixed_bonuses);
    public Dictionary get_effective_mult_bonuses()=>scale_bonuses(mult_bonuses);
    private Dictionary scale_bonuses(Dictionary values){var result=new Dictionary();var mult=QualityMults[Math.Clamp(quality,0,4)];foreach(var pair in values)result[pair.Key]=pair.Value.AsSingle()*mult;return result;}
    public string get_quality_name()=>QualityNames[Math.Clamp(quality,0,4)]; public string get_equip_type_name()=>TypeNames[Math.Clamp(equip_type,0,2)]; public string get_weapon_type_name()=>WeaponNames[Math.Clamp(weapon_type,0,10)]; public Array<Resource> get_weapon_skills()=>weapon_skills;
    public override string get_detail_text(){var text=$"{entity_name}\n{get_equip_type_name()} | {get_quality_name()}\n";foreach(var pair in get_effective_fixed_bonuses())if(!Mathf.IsZeroApprox(pair.Value.AsSingle()))text+=$"  {pair.Key}: +{pair.Value}\n";foreach(var pair in get_effective_mult_bonuses())if(!Mathf.IsZeroApprox(pair.Value.AsSingle()))text+=$"  {pair.Key}: +{pair.Value.AsSingle()*100:F0}%\n";return text;}
}
