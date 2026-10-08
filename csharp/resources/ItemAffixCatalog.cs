using Godot;
using Godot.Collections;

public partial class ItemAffixCatalog : RefCounted
{
    private static readonly System.Collections.Generic.Dictionary<string, string[]> Values = new()
    {
        ["五行"]=["金","木","水","火","土"], ["质地"]=["石","木","金","玉","晶","砂","液","气","甲","骨","布"], ["相性"]=["阴","阳","和"],
        ["功用"]=["锋锐","破甲","焚灼","冰封","蚀骨","震雷","碎魂","汲血","坚韧","护体","卸力","辟邪","净秽","不坏","反震","生发","回春","续骨","固本","造血","延年","培元","聚灵","散灵","蕴灵","通脉","纳气","化灵","锁灵","明心","凝神","养魂","破妄","悟道","通幽","镇魂","轻盈","疾行","隐踪","化形","御风","遁地","辟水","辟火","缠绕","固","聚","通","化","引","破","护","镇","生","明","藏","行"]
    };
    public static Dictionary parse_tag(string tag){var open=tag.IndexOf('[');if(open<=0||!tag.EndsWith(']')||tag.IndexOf('[',open+1)>=0)return [];var value=tag[(open+1)..^1];return string.IsNullOrEmpty(value)||value.Contains(']')?[]:new Dictionary{{"dimension",tag[..open]},{"value",value}};}
    public static Array<string> validate_tags(Array<string> tags,bool require_affinity=false){var errors=new Array<string>();var seen=new HashSet<string>();var counts=new System.Collections.Generic.Dictionary<string,int>();foreach(var tag in tags){if(!seen.Add(tag)){errors.Add($"重复词缀：{tag}");continue;}var parsed=parse_tag(tag);if(parsed.Count==0){errors.Add($"词缀格式错误：{tag}");continue;}var dimension=parsed["dimension"].AsString();var value=parsed["value"].AsString();if(!Values.TryGetValue(dimension,out var legal)){errors.Add($"未知词缀维度：{dimension}");continue;}if(!legal.Contains(value)){errors.Add($"未知词缀：{tag}");continue;}counts[dimension]=counts.TryGetValue(dimension,out var count)?count+1:1;}if(counts.TryGetValue("相性",out var affinity)&&affinity>1)errors.Add("相性词缀只能配置一个");if(require_affinity&&(!counts.TryGetValue("相性",out affinity)||affinity!=1))errors.Add("材料必须且只能配置一个相性词缀");return errors;}
    public static Array<string> get_tag_values(Array<string> tags,string dimension){var result=new Array<string>();foreach(var tag in tags){var parsed=parse_tag(tag);if(parsed.Count>0&&parsed["dimension"].AsString()==dimension)result.Add(parsed["value"].AsString());}return result;}
    public static string grade_to_string(int grade_value)
    {
        if (grade_value <= 0)
            return "无品";

        var rank = 9 - ((grade_value - 1) / 3);
        var subrank = new[] { "下", "中", "上" }[(grade_value - 1) % 3];
        return $"{rank}品{subrank}";
    }
}
