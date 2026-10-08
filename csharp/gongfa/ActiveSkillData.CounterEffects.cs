using Godot;
using Godot.Collections;

public partial class ActiveSkillData
{
    [ExportGroup("应对额外效果")]
    [Export] public Array<Dictionary> counter_special_effects = [];
    [Export] public Array<Dictionary> counter_status_effects = [];

    public string get_counter_effect_description(GodotObject? owner = null)
    {
        if (!keywords.Contains("应对")) return "";
        var lines = new System.Collections.Generic.List<string>();
        foreach (var effect in counter_special_effects)
        {
            var text = brief_effect(effect, owner);
            if (!string.IsNullOrWhiteSpace(text)) lines.Add(text);
        }
        foreach (var effect in counter_status_effects)
        {
            var id = effect.GetValueOrDefault("status_id", "").AsString();
            if (string.IsNullOrWhiteSpace(id)) continue;
            var count = effect.GetValueOrDefault("stacks", 1).AsInt32();
            lines.Add($"获得 {count} 层{id}。");
        }
        return lines.Count == 0 ? "" : $"\n应对触发后：{string.Join("；", lines)}";
    }
}
