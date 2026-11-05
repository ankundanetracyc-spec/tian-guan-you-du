using Godot;
using Godot.Collections;

/// <summary>Reads the action arena's live rules and the current character's opening attributes.</summary>
public static class ActionCombatSkillPresentation
{
    private static readonly GDScript Rules = GD.Load<GDScript>("res://tools/action_battle_test/action_skill_rules.gd");

    public static Dictionary Read(string key, BattleEntityData attributes, int bloodCapacity)
    {
        if (!ActionCombatLoadout.IsMigrated(key)) return new();
        using var adapter = Rules.New().AsGodotObject();
        return adapter.Call("preview", ActionCombatLoadout.Name(key), attributes.physique, attributes.soul,
            attributes.dodge, attributes.intelligence, bloodCapacity, attributes.max_mana).AsGodotDictionary();
    }

    public static string Detail(string key, BattleEntityData attributes, int bloodCapacity)
    {
        var data = Read(key, attributes, bloodCapacity);
        if (data.Count == 0) return "";
        return $"体力 {data["cost"].AsInt32()}　法力 0\n目标：{data["target"].AsString()}\n关键词：{data["tags"].AsString()}\n" +
            $"{data["phase"].AsString()}\n\n{data["body"].AsString()}\n\n" +
            $"[b]当前冷却 {data["cooldown"].AsSingle():0.##} 秒[/b] · 最低 {data["minimum"].AsSingle():0.##} 秒\n" +
            $"计算公式：{data["formula"].AsString()}\n从施放结束或中断后起算。";
    }
}
