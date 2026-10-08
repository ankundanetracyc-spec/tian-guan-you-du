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
        var sorted=new List<RealmConfig>();foreach(var value in realm_configs.Values){var config=value.AsGodotObject() as RealmConfig;if(config is not null)sorted.Add(config);} sorted.Sort((a,b)=>attribute(a.required_attributes,"spirit_strands").CompareTo(attribute(b.required_attributes,"spirit_strands")));foreach(var config in sorted)realm_order.Add(config.realm_name);
    }
    public RealmConfig? get_realm_config(string realm_name)=>realm_configs.TryGetValue(realm_name,out var value)?value.AsGodotObject() as RealmConfig:null;
    public string get_next_realm_name(string current){var index=realm_order.IndexOf(current);return index<0||index>=realm_order.Count-1?"":realm_order[index+1];}
    public string get_previous_realm_name(string current){var index=realm_order.IndexOf(current);return index<=0?"":realm_order[index-1];}
    public bool can_breakthrough(GodotObject? entity){if(!GodotObject.IsInstanceValid(entity))return false;var next=get_next_realm_name(entity!.Get("cultivation_realm").AsString());var config=get_realm_config(next);if(config is null)return false;if(config.requires_main_gongfa&&entity.Get("main_gongfa").VariantType==Variant.Type.Nil)return false;foreach(var key in config.required_attributes.Keys){var need=attribute(config.required_attributes,key.AsString());if(need<=0)continue;var current=entity.Get(key.AsString());if(current.VariantType==Variant.Type.Nil||current.AsDouble()<need)return false;}return true;}
    public Dictionary get_effective_required_attributes(GodotObject? entity,RealmConfig config)=>config.required_attributes.Duplicate();
    public Dictionary get_effective_requirements_for_next_realm(GodotObject? entity){if(!GodotObject.IsInstanceValid(entity))return [];var config=get_realm_config(get_next_realm_name(entity!.Get("cultivation_realm").AsString()));return config?.required_attributes.Duplicate()??[];}
    public bool is_realm_before_or_equal(string a,string b)=>string.IsNullOrEmpty(a)||string.IsNullOrEmpty(b)||realm_order.IndexOf(a)<0||realm_order.IndexOf(b)<0||realm_order.IndexOf(a)<=realm_order.IndexOf(b);
    public bool is_realm_after_or_equal(string a,string b)=>string.IsNullOrEmpty(a)||string.IsNullOrEmpty(b)||realm_order.IndexOf(a)<0||realm_order.IndexOf(b)<0||realm_order.IndexOf(a)>=realm_order.IndexOf(b);
    public bool can_be_main_gongfa(GodotObject? gongfa,string realm)=>check_gongfa(gongfa,realm,"can_be_main","main_realm_start","main_realm_end");
    public bool can_be_sub_gongfa(GodotObject? gongfa,string realm)=>check_gongfa(gongfa,realm,"can_be_sub","sub_realm_start","sub_realm_end");
    private bool check_gongfa(GodotObject? gongfa,string realm,string enabled,string start,string end)=>GodotObject.IsInstanceValid(gongfa)&&gongfa!.Get(enabled).AsBool()&&(string.IsNullOrEmpty(gongfa.Get(start).AsString())||is_realm_after_or_equal(realm,gongfa.Get(start).AsString()))&&(string.IsNullOrEmpty(gongfa.Get(end).AsString())||is_realm_before_or_equal(realm,gongfa.Get(end).AsString()));
    private static double attribute(Dictionary data,string key)=>data.TryGetValue(key,out var value)?value.AsDouble():0;
}
