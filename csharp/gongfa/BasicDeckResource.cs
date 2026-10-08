using Godot;
using Godot.Collections;

/// <summary>流派不可卸下的基础战斗技能库。</summary>
public partial class BasicDeckResource : Resource
{
    [Export] public string deck_name = "";
    [Export] public string deck_description = "";
    /// <summary>限定当前战斗侧重；不限制其他修行养成线。</summary>
    [Export] public int required_dao_bearing;
    [Export] public string required_realm = "";
    [Export] public Dictionary required_attributes = [];
    /// <summary>满足其中任一组即可；与 required_attributes 同时生效。</summary>
    [Export] public Array<Dictionary> required_attribute_any = [];
    [Export] public Array<BasicDeckUpgradeConfig> upgrade_branches = [];
    [Export] public Array<Dictionary> skill_entries = [];

    public Array<string> get_deck_skills()
    {
        var result = new Array<string>();
        foreach (var entry in skill_entries)
        {
            var path = BasicDeckUpgradeConfig.get_string(entry, "path");
            var count = BasicDeckUpgradeConfig.get_int(entry, "count", 1);
            for (var i = 0; i < count; i++) result.Add(path);
        }
        return result;
    }

    public bool is_unlocked_for(GodotObject? character)
    {
        if (!GodotObject.IsInstanceValid(character)) return false;
        if (required_dao_bearing != 0 && character!.Get("dao_bearing").AsInt32() != required_dao_bearing) return false;
        if (!string.IsNullOrEmpty(required_realm) && !is_realm_allowed(character)) return false;
        if (!BasicDeckUpgradeConfig.meets_requirements(character, required_attributes)) return false;
        if (required_attribute_any.Count == 0) return true;
        foreach (var alternative in required_attribute_any)
            if (BasicDeckUpgradeConfig.meets_requirements(character, alternative)) return true;
        return false;
    }

    public BasicDeckUpgradeConfig? get_upgrade_by_id(string upgrade_id)
    {
        foreach (var upgrade in upgrade_branches)
            if (upgrade is not null && upgrade.upgrade_id == upgrade_id) return upgrade;
        return null;
    }

    private bool is_realm_allowed(GodotObject character)
    {
        var tree = Engine.GetMainLoop() as SceneTree;
        var realmManager = tree?.Root.GetNodeOrNull("RealmManager");
        return !GodotObject.IsInstanceValid(realmManager)
            || realmManager!.Call("is_realm_after_or_equal", character.Get("cultivation_realm").AsString(), required_realm).AsBool();
    }
}
