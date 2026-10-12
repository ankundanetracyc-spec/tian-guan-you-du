using Godot;
using Godot.Collections;

/// <summary>角色共用的基础技能池；技能按已学会的能力记录，不按卡牌数量复制。</summary>
public partial class BasicDeckResource : Resource
{
    [Export] public string deck_name = "";
    [Export] public string deck_description = "";
    [Export] public Array<string> skill_paths = [];
    /// <summary>兽类旧牌堆兼容字段；角色公共技能池使用 skill_paths，不读取数量。</summary>
    [Export] public Array<Dictionary> skill_entries = [];

    public Array<string> get_deck_skills()
    {
        var result = new Array<string>();
        if (skill_paths.Count > 0)
        {
            foreach (var path in skill_paths)
                if (!string.IsNullOrWhiteSpace(path) && !result.Contains(path)) result.Add(path);
            return result;
        }
        foreach (var entry in skill_entries)
        {
            var path = entry.TryGetValue("path", out var value) ? value.AsString() : "";
            var count = entry.TryGetValue("count", out var amount) ? Math.Max(0, amount.AsInt32()) : 1;
            for (var i = 0; i < count && !string.IsNullOrWhiteSpace(path); i++) result.Add(path);
        }
        return result;
    }
}
