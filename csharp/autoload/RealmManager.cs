using Godot;
using Godot.Collections;

public partial class RealmManager : Node
{
    public Dictionary realm_configs = [];
    public Array<string> realm_order = [];
    public override void _Ready() => load_all_realm_configs();
    public void load_all_realm_configs()
    {
        realm_configs.Clear(); realm_order.Clear(); var dir=DirAccess.Open("res://resources/realm/"); if(dir is null){GD.PushWarning("[RealmManager] 未找到境界配置目录");return;}
        foreach(var file in dir.GetFiles()) if(file.EndsWith(".tres")){var config=ResourceLoader.Load<RealmConfig>($"res://resources/realm/{file}");if(config is not null&&!string.IsNullOrEmpty(config.realm_name))realm_configs[config.realm_name]=config;}
        var sorted=new List<RealmConfig>();foreach(var value in realm_configs.Values){var config=value.AsGodotObject() as RealmConfig;if(config is not null)sorted.Add(config);} sorted.Sort((a,b)=>{var order=realm_rank(a.realm_name).CompareTo(realm_rank(b.realm_name));return order!=0?order:string.CompareOrdinal(a.realm_name,b.realm_name);});foreach(var config in sorted)realm_order.Add(config.realm_name);
    }
    public RealmConfig? get_realm_config(string realm_name)=>realm_configs.TryGetValue(realm_name,out var value)?value.AsGodotObject() as RealmConfig:null;
    public string get_next_realm_name(string current){if(current=="云息十层")return "";var index=realm_order.IndexOf(current);return index<0||index>=realm_order.Count-1?"":realm_order[index+1];}
    public string get_previous_realm_name(string current){var index=realm_order.IndexOf(current);return index<=0?"":realm_order[index-1];}
    public bool can_breakthrough(GodotObject? entity)
    {
        if (entity is not Character character || !GodotObject.IsInstanceValid(character)) return false;
        var next = get_next_realm_name(character.cultivation_realm);
        var config = get_realm_config(next);
        if (config is null || !can_be_main_gongfa(character.main_gongfa, next)) return false;
        var requirements = get_effective_required_attributes(character, config);
        foreach (var key in requirements.Keys)
        {
            var need = attribute(requirements, key.AsString());
            if (need <= 0) continue;
            var current = character.Get(key.AsString());
            if (current.VariantType == Variant.Type.Nil || current.AsDouble() < need) return false;
        }
        return true;
    }
    public string get_main_gongfa_breakthrough_hint(GodotObject? entity, string targetRealm)
    {
        if (entity is not Character character || !GodotObject.IsInstanceValid(character))
            return $"需装配可作主修、且能支撑至{targetRealm}的功法。";
        var book = character.main_gongfa;
        if (!GodotObject.IsInstanceValid(book))
            return $"尚未装配主修功法；需选择能支撑至{targetRealm}的功法。";
        if (!book!.can_be_main)
            return $"当前主修《{book.gongfa_name}》不能作为主修功法。";
        if (!string.IsNullOrWhiteSpace(book.main_realm_start) && !is_realm_after_or_equal(targetRealm, book.main_realm_start))
            return $"《{book.gongfa_name}》从{book.main_realm_start}起方可主修，不能支撑至{targetRealm}。";
        if (!string.IsNullOrWhiteSpace(book.main_realm_end) && !is_realm_before_or_equal(targetRealm, book.main_realm_end))
            return $"《{book.gongfa_name}》只能主修至{book.main_realm_end}，需更换可支撑至{targetRealm}的功法。";
        return can_be_main_gongfa(book, targetRealm) ? "" : $"当前主修《{book.gongfa_name}》不能支撑至{targetRealm}，需更换功法。";
    }
    // 灵力字段保留累计记账；凝核补正只作用于七层之后各次新增量，不重复乘此前144缕。
    public Dictionary get_effective_required_attributes(GodotObject? entity,RealmConfig config)
    {
        var result=config.required_attributes.Duplicate();
        var rank=realm_rank(config.realm_name);
        if(rank<8||rank>10)return result;
        var multiplier=entity is Character character?character.get_condensation_spirit_requirement_multiplier():1d;
        var seventh=get_realm_config("云息七层");
        if(seventh is null)return result;
        var total=attribute(seventh.required_attributes,"spirit_strands");
        for(var tier=8;tier<=rank;tier++)
        {
            var current=get_realm_config(cloud_realm_name(tier));
            var previous=get_realm_config(cloud_realm_name(tier-1));
            if(current is null||previous is null)return result;
            var increment=attribute(current.required_attributes,"spirit_strands")-attribute(previous.required_attributes,"spirit_strands");
            total+=Math.Ceiling(increment*multiplier);
        }
        result["spirit_strands"]=(int)total;
        return result;
    }
    public Dictionary get_effective_requirements_for_next_realm(GodotObject? entity){if(!GodotObject.IsInstanceValid(entity))return [];var config=get_realm_config(get_next_realm_name(entity!.Get("cultivation_realm").AsString()));return config is null?[]:get_effective_required_attributes(entity,config);}
    public int get_current_layer_spirit_origin(GodotObject? entity,string realm)
    {
        var rank=realm_rank(realm);var config=get_realm_config(realm);
        return rank>=7&&rank<=10&&config is not null?(int)attribute(get_effective_required_attributes(entity,config),"spirit_strands"):0;
    }
    private static string cloud_realm_name(int tier)=>"云息"+new[]{"","一","二","三","四","五","六","七","八","九","十"}[tier]+"层";
    private static int realm_rank(string realm)=>realm switch{"凡人"=>0,"云息一层"=>1,"云息二层"=>2,"云息三层"=>3,"云息四层"=>4,"云息五层"=>5,"云息六层"=>6,"云息七层"=>7,"云息八层"=>8,"云息九层"=>9,"云息十层"=>10,_=>int.MaxValue};
    /// <summary>识别正式中文云息境界，兼容旧阿拉伯数字显示名；其他境界不冒充云息。</summary>
    public static int get_cloud_realm_tier(string realm)
    {
        var rank = realm_rank(realm);
        if (rank != int.MaxValue) return rank;
        const string prefix = "云息";
        return realm.StartsWith(prefix, StringComparison.Ordinal) && realm.EndsWith("层", StringComparison.Ordinal) &&
            int.TryParse(realm.AsSpan(prefix.Length, realm.Length - prefix.Length - 1), out var tier) && tier is >= 1 and <= 10 ? tier : 0;
    }
    public bool is_realm_before_or_equal(string a,string b)=>string.IsNullOrEmpty(a)||string.IsNullOrEmpty(b)||realm_order.IndexOf(a)<0||realm_order.IndexOf(b)<0||realm_order.IndexOf(a)<=realm_order.IndexOf(b);
    public bool is_realm_after_or_equal(string a,string b)=>string.IsNullOrEmpty(a)||string.IsNullOrEmpty(b)||realm_order.IndexOf(a)<0||realm_order.IndexOf(b)<0||realm_order.IndexOf(a)>=realm_order.IndexOf(b);
    public bool can_be_main_gongfa(GodotObject? gongfa,string realm)=>check_gongfa(gongfa,realm,"can_be_main","main_realm_start","main_realm_end");
    public bool can_be_sub_gongfa(GodotObject? gongfa,string realm)=>check_gongfa(gongfa,realm,"can_be_sub","sub_realm_start","sub_realm_end");
    private bool check_gongfa(GodotObject? gongfa,string realm,string enabled,string start,string end)=>GodotObject.IsInstanceValid(gongfa)&&gongfa!.Get(enabled).AsBool()&&(string.IsNullOrEmpty(gongfa.Get(start).AsString())||is_realm_after_or_equal(realm,gongfa.Get(start).AsString()))&&(string.IsNullOrEmpty(gongfa.Get(end).AsString())||is_realm_before_or_equal(realm,gongfa.Get(end).AsString()));
    private static double attribute(Dictionary data,string key)=>data.TryGetValue(key,out var value)?value.AsDouble():0;
}
