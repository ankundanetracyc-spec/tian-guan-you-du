using Godot;
using Godot.Collections;

/// <summary>基础技能库的一条互斥成长分支。</summary>
public partial class BasicDeckUpgradeConfig : Resource
{
    [Export] public string upgrade_id = "";
    [Export] public string branch_group = "";
    [Export] public string upgrade_name = "";
    [Export(PropertyHint.MultilineText)] public string description = "";
    [Export] public Dictionary required_attributes = [];
    [Export] public Array<Dictionary> additions = [];
    [Export] public Array<Dictionary> replacements = [];

    public bool is_unlocked_for(GodotObject? character) => meets_requirements(character, required_attributes);

    public Array<string> apply_to_paths(Array<string> paths)
    {
        var result = new Array<string>(paths);
        foreach (var replacement in replacements)
        {
            var sourcePath = get_string(replacement, "source_path");
            var resultPath = get_string(replacement, "result_path");
            var remaining = get_int(replacement, "count", 1);
            for (var i = 0; i < result.Count && remaining > 0; i++)
            {
                if (result[i] != sourcePath || string.IsNullOrEmpty(resultPath)) continue;
                result[i] = resultPath;
                remaining--;
            }
        }
        foreach (var addition in additions)
        {
            var path = get_string(addition, "path");
            var count = Math.Max(0, get_int(addition, "count", 1));
            for (var i = 0; i < count && !string.IsNullOrEmpty(path); i++) result.Add(path);
        }
        return result;
    }

    internal static bool meets_requirements(GodotObject? character, Dictionary requirements)
    {
        if (!GodotObject.IsInstanceValid(character)) return false;
        foreach (var key in requirements.Keys)
        {
            // 角色的各项技艺造诣保存在 entity_data；统一走卡牌属性读取，
            // 让基础技能库解锁、升级分支和战斗公式读取同一份数值。
            if (CardRules.Number(character, key.AsString()) < requirements[key].AsDouble()) return false;
        }
        return true;
    }

    internal static string get_string(Dictionary values, string key) =>
        values.TryGetValue(key, out var value) ? value.AsString() : "";

    internal static int get_int(Dictionary values, string key, int fallback) =>
        values.TryGetValue(key, out var value) ? value.AsInt32() : fallback;
}
