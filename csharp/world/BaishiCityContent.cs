using Godot;
using Godot.Collections;

/// <summary>白石基础文案与场景配置的同一数据来源。</summary>
public static class BaishiCityContent
{
    private static Dictionary? _data;
    public static Dictionary Data
    {
        get
        {
            if (_data is not null) return _data;
            using var file = Godot.FileAccess.Open("res://resources/world/bai_chuan/baishi_city_content_v1.json", Godot.FileAccess.ModeFlags.Read);
            if (file is null) throw new System.InvalidOperationException("白石城内容配置无法读取。");
            _data = Json.ParseString(file.GetAsText()).AsGodotDictionary();
            return _data;
        }
    }
    public static Dictionary Section(string name) => Data.TryGetValue(name, out var value) ? value.AsGodotDictionary() : [];
    public static Dictionary Notice(string id) => Section("notices").TryGetValue(id, out var value) ? value.AsGodotDictionary() : [];
    public static Godot.Collections.Array Questions(string space) => Section("conversations").TryGetValue(space, out var value) ? value.AsGodotArray() : [];
}
