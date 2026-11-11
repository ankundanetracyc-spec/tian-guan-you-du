using Godot;
using Godot.Collections;

/// <summary>单人新建存档的主角模板；预设只保存建档方案，不承载正式游戏进度。</summary>
public partial class GameStartProfile : Node
{
    private const string PresetFile = "user://开局设定预设.cfg";
    public const float BaseAttributeTotal = 15f;
    public string player_name = "苏醒者";
    public int player_gender;
    public HumanData player_appearance_data = new();
    public string surname = "李", family_name = "本家";
    // Legacy previews may set this explicitly; new journeys select at the lecture.
    public string selected_initial_gongfa_path = "";
    public Array<string> generation_characters = ["守", "承", "远", "宁"];
    public int fated_youngest_root_type = 22, fated_youngest_element = 2, fated_remaining_points;
    public bool has_fated_roll;
    public string fated_youngest_root_name = "木水火灵根";
    public Godot.Collections.Array fated_youngest_root_weights = [0, 40, 35, 25, 0];
    public bool is_test_mode;
    private static readonly string[] PortraitFields =
    [
        "appearance_seed", "appearance_face_style", "appearance_eyebrow_style", "appearance_eye_style",
        "appearance_brow_style", "appearance_nose_style", "appearance_mouth_style", "appearance_makeup_style",
        "appearance_skin_tone", "appearance_hair_color", "appearance_hair_style", "appearance_hair_back_style",
        "appearance_ear_style", "appearance_clothes_style", "appearance_beard_style",
    ];
    public float fated_base_physique = 5, fated_base_intelligence = 5, fated_base_soul = 5, fated_base_fortune = 50, fated_base_comprehension = 50, fated_base_charisma = 50, fated_base_social = 50, fated_base_management = 50;
    private static readonly string[] RootKeys = ["METAL", "WOOD", "WATER", "FIRE", "EARTH"], RootNames = ["金", "木", "水", "火", "土"];
    public string get_generation_character(int generation) => generation > 0 && generation <= generation_characters.Count ? generation_characters[generation - 1][..1] : "";
    public void set_generation_poem(string source) { var chars = source.Where(ch => ch is >= '\u4e00' and <= '\u9fff').Select(ch => ch.ToString()).ToArray(); if (chars.Length >= 2) generation_characters = new Array<string>(chars); }
    private static HumanData generate_rooted_aptitude()
    {
        var generator = new CharacterGenerator();
        var draft = generator.generate("凡人", 1);
        while (draft.spirit_root_type == 0) draft = generator.generate("凡人", 1);
        return draft;
    }
    private void assign_fated_root(HumanData draft)
    {
        fated_youngest_root_type = draft.spirit_root_type;
        fated_youngest_root_name = draft.spirit_root_name;
        fated_youngest_element = draft.spirit_element;
        fated_youngest_root_weights = draft.root_weights.Duplicate();
    }
    public void reroll_fated_root() => assign_fated_root(generate_rooted_aptitude());
    public void roll_fated_aptitude()
    {
        var draft = generate_rooted_aptitude();
        assign_fated_root(draft);
        foreach (var name in new[] { "physique", "intelligence", "soul", "fortune", "comprehension", "charisma", "social", "management" }) Set("fated_base_" + name, draft.Get("base_" + name));
        fated_remaining_points = (int)GD.RandRange(6, 10); has_fated_roll = true;
    }
    public bool increase_base_attribute(string attribute)
    { var increment = attribute is "fortune" or "comprehension" or "charisma" or "social" or "management" ? 5f : 1f; var max = increment == 5 ? 100f : 10f; var key = "fated_base_" + attribute; if (fated_remaining_points < 1 || Get(key).AsSingle() >= max || !new[] { "physique", "intelligence", "soul", "fortune", "comprehension", "charisma", "social", "management" }.Contains(attribute)) return false; Set(key, Get(key).AsSingle() + increment); fated_remaining_points--; return true; }
    public string get_fated_base_attribute_description() => $"体魄 {fated_base_physique:F1}    灵慧 {fated_base_intelligence:F1}    神魂 {fated_base_soul:F1}\n机缘 {fated_base_fortune:F0}    悟性 {fated_base_comprehension:F0}    魅力 {fated_base_charisma:F0}    交际 {fated_base_social:F0}    治事 {fated_base_management:F0}";
    public string get_fated_aptitude_description() => $"灵根：{fated_youngest_root_name}";
    public string compose_family_name(string given, int generation) { var character = get_generation_character(generation); return surname + (!string.IsNullOrEmpty(character) && given.StartsWith(character) ? given : character + given); }
    public float get_remaining_base_points() => Mathf.Max(0, BaseAttributeTotal - (fated_base_physique + fated_base_intelligence + fated_base_soul));
    public void set_fated_attribute(string attribute, float value)
    {
        if (attribute is not ("physique" or "intelligence" or "soul")) return;
        var key="fated_base_"+attribute;
        var otherTotal=fated_base_physique+fated_base_intelligence+fated_base_soul-Get(key).AsSingle();
        Set(key,Mathf.Clamp(value,2,Mathf.Min(10,BaseAttributeTotal-otherTotal)));
    }
    public void apply_to_youngest(HumanData data)
    {
        data.entity_name=string.IsNullOrWhiteSpace(player_name)?"苏醒者":player_name.Trim();
        data.gender=player_gender;
        apply_starting_proficiencies(data);
        apply_player_appearance(data);
        data.base_physique=fated_base_physique; data.base_intelligence=fated_base_intelligence; data.base_soul=fated_base_soul;
        data.physique=data.base_physique; data.intelligence=data.base_intelligence; data.soul=data.base_soul;
        data.base_fortune=fated_base_fortune; data.base_comprehension=fated_base_comprehension; data.base_charisma=fated_base_charisma; data.base_social=fated_base_social; data.base_management=fated_base_management;
        data.fortune=data.base_fortune; data.comprehension=data.base_comprehension; data.charisma=data.base_charisma; data.social=data.base_social; data.management=data.base_management;
        data.spirit_root_type=fated_youngest_root_type; data.spirit_root_name=fated_youngest_root_name; data.spirit_element=fated_youngest_element; data.root_weights=fated_youngest_root_weights.Duplicate();
    }
    public HumanData create_player_appearance_preview()
    {
        var data = player_appearance_data.Duplicate(true) as HumanData ?? new HumanData();
        data.gender = player_gender;
        return data;
    }
    public void capture_player_appearance(EntityData source)
    {
        foreach (var field in PortraitFields) player_appearance_data.Set(field, source.Get(field));
    }
    public void capture_resolved_player_appearance(Dictionary resolved)
    {
        void store(string part, System.Action<int> set)
        {
            var index = resolved.GetValueOrDefault(part, -1).AsInt32();
            if (index >= 0) set(index);
        }
        store("face", index => player_appearance_data.appearance_face_style = index);
        store("ear", index => player_appearance_data.appearance_ear_style = index);
        store("eye", index => player_appearance_data.appearance_eye_style = index);
        store("brow", index => player_appearance_data.appearance_brow_style = index);
        store("nose", index => player_appearance_data.appearance_nose_style = index);
        store("mouth", index => player_appearance_data.appearance_mouth_style = index);
        store("makeup", index => player_appearance_data.appearance_makeup_style = index);
        store("beard", index => player_appearance_data.appearance_beard_style = index);
        store("hair_back", index => player_appearance_data.appearance_hair_back_style = index);
        store("hair_front", index => player_appearance_data.appearance_hair_style = index);
        store("clothes", index => player_appearance_data.appearance_clothes_style = index);
    }
    public void randomize_player_appearance()
    {
        var rng = new RandomNumberGenerator();
        rng.Randomize();
        player_appearance_data.appearance_seed = (int)rng.RandiRange(1, int.MaxValue);
        player_appearance_data.appearance_face_style = -1;
        player_appearance_data.appearance_eyebrow_style = -1;
        player_appearance_data.appearance_eye_style = -1;
        player_appearance_data.appearance_brow_style = -1;
        player_appearance_data.appearance_nose_style = -1;
        player_appearance_data.appearance_mouth_style = -1;
        player_appearance_data.appearance_makeup_style = -1;
        player_appearance_data.appearance_ear_style = -1;
        player_appearance_data.appearance_hair_style = -1;
        player_appearance_data.appearance_hair_back_style = -1;
        player_appearance_data.appearance_clothes_style = -1;
        player_appearance_data.appearance_beard_style = -1;
        player_appearance_data.appearance_skin_tone = -1;
        player_appearance_data.appearance_hair_color = -1;
    }
    public void apply_player_appearance(HumanData target)
    {
        target.appearance_seed = player_appearance_data.appearance_seed;
        target.appearance_face_style = player_appearance_data.appearance_face_style;
        target.appearance_eyebrow_style = player_appearance_data.appearance_eyebrow_style;
        target.appearance_eye_style = player_appearance_data.appearance_eye_style;
        target.appearance_brow_style = player_appearance_data.appearance_brow_style;
        target.appearance_nose_style = player_appearance_data.appearance_nose_style;
        target.appearance_mouth_style = player_appearance_data.appearance_mouth_style;
        target.appearance_makeup_style = player_appearance_data.appearance_makeup_style;
        target.appearance_skin_tone = player_appearance_data.appearance_skin_tone;
        target.appearance_hair_color = player_appearance_data.appearance_hair_color;
        target.appearance_hair_style = player_appearance_data.appearance_hair_style;
        target.appearance_hair_back_style = player_appearance_data.appearance_hair_back_style;
        target.appearance_ear_style = player_appearance_data.appearance_ear_style;
        target.appearance_clothes_style = player_appearance_data.appearance_clothes_style;
        target.appearance_beard_style = player_appearance_data.appearance_beard_style;
        if (target.appearance_hair_back_style < 0 && target.appearance_hair_style >= 0
            && GetNodeOrNull<PortraitCatalog>("/root/PortraitCatalog") is { } portraitCatalog)
        {
            var gender = target.gender == 1 ? "female" : "male";
            var context = portraitCatalog.get_context(target, gender, 18, target.faction_name, "凡人");
            var backHair = portraitCatalog.choose_part(context, "hair_back");
            target.appearance_hair_back_style = backHair.GetValueOrDefault("variant_index", -1).AsInt32();
        }
    }
    public void save_preset(string name)
    {
        name = string.IsNullOrWhiteSpace(name) ? "未命名设定" : name.Trim(); var cfg = new ConfigFile(); cfg.Load(PresetFile); var key = name.Replace("/", "_").Replace("\\", "_");
        cfg.SetValue(key,"attribute_schema",2); cfg.SetValue(key,"name",player_name); cfg.SetValue(key,"gender",player_gender);  cfg.SetValue(key,"root_type",fated_youngest_root_type); cfg.SetValue(key,"root_name",fated_youngest_root_name); cfg.SetValue(key,"root_element",fated_youngest_element); cfg.SetValue(key,"weights",fated_youngest_root_weights); cfg.SetValue(key,"remaining_points",fated_remaining_points); cfg.SetValue(key,"physique",fated_base_physique); cfg.SetValue(key,"intelligence",fated_base_intelligence); cfg.SetValue(key,"soul",fated_base_soul);
        cfg.SetValue(key,"fortune",fated_base_fortune); cfg.SetValue(key,"comprehension",fated_base_comprehension); cfg.SetValue(key,"charisma",fated_base_charisma); cfg.SetValue(key,"social",fated_base_social); cfg.SetValue(key,"management",fated_base_management);
        foreach (var field in PortraitFields) cfg.SetValue(key, "portrait_" + field, player_appearance_data.Get(field));
        save_starting_proficiencies(cfg, key);
        cfg.Save(PresetFile);
    }
    public Array<string> get_preset_names(){var cfg=new ConfigFile();if(cfg.Load(PresetFile)!=Error.Ok)return [];return new Array<string>(cfg.GetSections());}
    public bool load_preset(string name)
    {
        var cfg=new ConfigFile();if(cfg.Load(PresetFile)!=Error.Ok||!cfg.HasSection(name))return false;
        player_name=cfg.GetValue(name,"name",player_name).AsString(); player_gender=cfg.GetValue(name,"gender",player_gender).AsInt32(); selected_initial_gongfa_path=""; fated_youngest_root_weights=cfg.GetValue(name,"weights",fated_youngest_root_weights).AsGodotArray();
        if (cfg.HasSectionKey(name, "root_type"))
        {
            fated_youngest_root_type=cfg.GetValue(name,"root_type",fated_youngest_root_type).AsInt32();
            fated_youngest_root_name=cfg.GetValue(name,"root_name",fated_youngest_root_name).AsString();
            fated_youngest_element=cfg.GetValue(name,"root_element",fated_youngest_element).AsInt32();
        }
        else restore_legacy_root_identity_from_weights();
        fated_base_physique=cfg.GetValue(name,"physique",fated_base_physique).AsSingle(); fated_base_intelligence=cfg.GetValue(name,"intelligence",fated_base_intelligence).AsSingle(); fated_base_soul=cfg.GetValue(name,"soul",fated_base_soul).AsSingle();
        if (cfg.GetValue(name,"attribute_schema",1).AsInt32() < 2)
        {
            var removedAgility=Mathf.Max(0,cfg.GetValue(name,"agility",0).AsSingle())*.5f;
            fated_base_physique=Mathf.Min(10,fated_base_physique+removedAgility);
            fated_base_soul=Mathf.Min(10,fated_base_soul+removedAgility);
        }
        fated_base_fortune=cfg.GetValue(name,"fortune",fated_base_fortune).AsSingle(); fated_base_comprehension=cfg.GetValue(name,"comprehension",fated_base_comprehension).AsSingle(); fated_base_charisma=cfg.GetValue(name,"charisma",fated_base_charisma).AsSingle(); fated_base_social=cfg.GetValue(name,"social",fated_base_social).AsSingle(); fated_base_management=cfg.GetValue(name,"management",fated_base_management).AsSingle(); fated_remaining_points=cfg.GetValue(name,"remaining_points",fated_remaining_points).AsInt32(); has_fated_roll=true;
        foreach (var field in PortraitFields) player_appearance_data.Set(field, cfg.GetValue(name, "portrait_" + field, player_appearance_data.Get(field)));
        load_starting_proficiencies(cfg, name);
        return true;
    }
    private void restore_legacy_root_identity_from_weights()
    {
        var active=Enumerable.Range(0,5).Where(index=>index<fated_youngest_root_weights.Count&&fated_youngest_root_weights[index].AsInt32()>0).ToArray();
        if(active.Length==0){fated_youngest_root_type=0;fated_youngest_root_name="无灵根";fated_youngest_element=0;return;}
        var keys=active.Select(index=>RootKeys[index]).ToArray();
        if (!System.Enum.TryParse<GongfaResource.Wuxing>(string.Join('_',keys), out var rootType))
        {
            fated_youngest_root_type=0; fated_youngest_root_name="无灵根"; fated_youngest_element=0;
            return;
        }
        fated_youngest_root_type=(int)rootType; fated_youngest_root_name=string.Concat(active.Select(index=>RootNames[index]))+(active.Length==1?"灵根（天灵根）":"灵根"); fated_youngest_element=active[0]+1;
    }
}
