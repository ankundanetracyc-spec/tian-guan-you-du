using Godot;
using Godot.Collections;
using System;
using System.Linq;

/// <summary>Persistent five-slot action loadout; queue references and rewrite bindings remain independent.</summary>
public static class ActionCombatLoadout
{
    public const int SlotCount = 5;
    public const string BasicAttack = "res://resources/activeskills/应敌.tres";
    public const string Dodge = "action:错步";
    public const string Heavy = "res://resources/activeskills/痛击.tres";
    public const string Guard = "res://resources/activeskills/架招.tres";
    public const string Blood = "res://resources/activeskills/催血.tres";
    public const string Qi = "res://resources/activeskills/聚炁.tres";
    public static readonly string[] Controls = ["右键", "Q", "E", "R", "F"];

    public static bool IsMigrated(string key) => key is BasicAttack or Dodge or Heavy or Guard or Blood or Qi;
    public static bool IsFixed(string key) => key is BasicAttack or Dodge;
    public static string Name(string key) => key switch
    { BasicAttack => "拳掌", Dodge => "错步", Heavy => "痛击", Guard => "架招", Blood => "催血", Qi => "聚炁", _ => "" };

    public static Array<string> Available(Character character)
    {
        ArgumentNullException.ThrowIfNull(character);
        var result = new Array<string> { Heavy, Guard };
        // Only learned base nodes are migrated; book rewrites are not implicitly enabled.
        foreach (var skill in character.get_available_battle_skills())
            if (skill.ResourcePath is Blood or Qi && !result.Contains(skill.ResourcePath))
                result.Add(skill.ResourcePath);
        return result;
    }

    public static Array<string> Read(Character character)
    {
        ArgumentNullException.ThrowIfNull(character);
        var data = character.entity_data as HumanData;
        var saved = data?.action_combat_skill_slots_configured == true
            ? data.action_combat_skill_slot_paths
            : new Array<string> { Heavy, Guard };
        var available = Available(character);
        var result = new Array<string>();
        for (var i = 0; i < SlotCount; i++)
        {
            var key = i < saved.Count ? saved[i] : "";
            result.Add(available.Contains(key) && !result.Contains(key) ? key : "");
        }
        return result;
    }

    public static bool Write(Character character, Array<string> actionSlots)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(actionSlots);
        if (character.HasMeta("artifact_battle_clone") || character.entity_data is not HumanData data ||
            actionSlots.Count != SlotCount) return false;
        var available = Available(character);
        var keys = actionSlots.Select(key => key ?? "").ToArray();
        if (keys.Any(key => key.Length > 0 && !available.Contains(key)) ||
            keys.Where(key => key.Length > 0).Distinct(StringComparer.Ordinal).Count() != keys.Count(key => key.Length > 0))
            return false;
        data.action_combat_skill_slot_paths = new Array<string>(keys);
        data.action_combat_skill_slots_configured = true;
        return true;
    }

    public static bool Assign(Character character, int target, string key)
    {
        if (target < 0 || target >= SlotCount || key.Length > 0 && !Available(character).Contains(key)) return false;
        var slots = Read(character);
        var source = key.Length > 0 ? slots.IndexOf(key) : -1;
        if (source >= 0) (slots[source], slots[target]) = (slots[target], slots[source]);
        else slots[target] = key;
        return Write(character, slots);
    }

    public static Array<string> ReadNames(Character character) => new(Read(character).Select(Name));
}
