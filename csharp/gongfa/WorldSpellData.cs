using Godot;

/// <summary>大世界术法设计与展示数据；不作为即时战斗技能使用。</summary>
public partial class WorldSpellData : Resource
{
    [Export] public string spell_id = "";
    [Export] public string spell_name = "";
    [Export] public Texture2D? icon_texture;
    [Export] public string use_type = "";
    [Export] public string cast_mode = "";
    [Export] public string scene = "";
    [Export] public string target_rule = "";
    [Export] public string resource_cost = "";
    [Export] public string world_time = "";
    [Export(PropertyHint.MultilineText)] public string effect = "";
    [Export] public string usage_limit = "";
    [Export(PropertyHint.MultilineText)] public string stop_and_failure = "";
    [Export] public string implementation_state = "已设计，尚未接入世界地图施放";

    public string get_icon_bbcode(int size = 56)
    {
        var iconPath = icon_texture?.ResourcePath;
        return string.IsNullOrWhiteSpace(iconPath) ? "" : $"[img={size}x{size}]{iconPath}[/img]";
    }

    public string get_manual_detail_text(bool chapter_learned)
    {
        var unlock = chapter_learned ? "本卷已研读，术法已解锁" : "学会本卷后永久解锁，不要求装配来源功法";
        var icon = get_icon_bbcode(58);
        return (icon.Length > 0 ? icon + "\n" : "") +
            $"[b]{spell_name}[/b]　{use_type} · {cast_mode}\n" +
            $"解锁：{unlock}\n" +
            $"场景与目标：{scene}；{target_rule}\n" +
            $"大世界体力：{resource_cost}\n" +
            $"时间：{world_time}\n" +
            $"效果：{effect}\n" +
            $"次数：{usage_limit}\n" +
            $"中止与失败：{stop_and_failure}\n" +
            $"接入状态：{implementation_state}";
    }
}
