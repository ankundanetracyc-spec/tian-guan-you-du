using Godot;
using System;
using System.Collections.Generic;

/// <summary>东林地路线与 NPC 遭遇的首轮短测规则；概率和节点基础耗时都不是最终平衡值。</summary>
public static class EastWoodsDungeonRules
{
    public const string DungeonId = "east_woods_route";
    public const float BaseDunSpeed = 100.0f;
    public const float BaseHoursPerNode = 1.0f;
    public const float BaseAttackChance = 0.08f;
    public const float MaximumAttackChance = 0.55f;
    public const int PrototypeLootCount = 1;

    private static readonly Dictionary<string, string[]> Neighbors = new()
    {
        ["forest_edge"] = ["leaf_creek"],
        ["leaf_creek"] = ["creek_path", "root_path", "fern_lowland"],
        ["creek_path"] = ["stream_canopy"],
        ["stream_canopy"] = ["deep_meet"],
        ["root_path"] = ["root_hollow"],
        ["root_hollow"] = ["deep_meet"],
        ["fern_lowland"] = ["fallen_canopy"],
        ["fallen_canopy"] = ["deep_meet"],
        ["deep_meet"] = ["quiet_grove", "upper_ridge", "shallow_gully"],
        ["upper_ridge"] = ["moss_bridge"],
        ["moss_bridge"] = ["quiet_grove"],
        ["shallow_gully"] = ["dry_pool"],
        ["dry_pool"] = ["quiet_grove"],
        ["quiet_grove"] = ["deep_meet"],
    };

    public static IReadOnlyList<string> get_neighbors(string nodeId)
        => Neighbors.TryGetValue(nodeId, out var values) ? values : Array.Empty<string>();

    /// <summary>东林地副本跨越一个相连节点的耗时；统一按遁速缩放，不为单条边写固定时长。</summary>
    public static float hours_per_node(float dunSpeed)
        => BaseHoursPerNode * BaseDunSpeed / Mathf.Max(0.01f, dunSpeed);

    public static float player_edge_hours(float dunSpeed)
        => hours_per_node(dunSpeed);

    public static float npc_edge_cost(float dunSpeed)
        => hours_per_node(dunSpeed);

    /// <summary>将性格、行为、目标声望、势力态度、私人关系与境界差独立计入试运行概率。</summary>
    public static float attack_chance(
        WorldNpcData attacker,
        string targetFactionId,
        long targetRegionalRenown,
        int personalRelationship,
        int targetRealmScore)
    {
        if (attacker is null) return 0f;

        var chance = BaseAttackChance;
        if (!string.IsNullOrWhiteSpace(targetFactionId) && attacker.faction_id == targetFactionId)
            chance -= 0.06f;
        chance += attacker.personality switch
        {
            "多疑" => 0.12f,
            "果决" => 0.08f,
            "机敏" => 0.04f,
            "温厚" or "圆融" or "随和" => -0.04f,
            _ => 0f
        };
        if (attacker.behavior_tags.Contains("劫掠")) chance += 0.24f;
        if (attacker.behavior_tags.Contains("好斗")) chance += 0.16f;

        var attitude = get_attitude(attacker, targetFactionId);
        if (attitude >= 0)
            chance += attitude switch { 0 => 0.18f, 1 => 0.13f, 2 => 0.07f, 3 => 0f, 4 => -0.04f, 5 => -0.08f, _ => -0.12f };

        chance += personalRelationship switch
        {
            <= -30 => 0.18f,
            <= -10 => 0.10f,
            >= 30 => -0.16f,
            >= 10 => -0.08f,
            _ => 0f
        };

        if (targetRegionalRenown >= 300) chance -= 0.12f;
        else if (targetRegionalRenown >= 100) chance -= 0.07f;
        else if (targetRegionalRenown < 0) chance += 0.04f;

        var realmGap = realm_score(attacker.cultivation_realm) - targetRealmScore;
        if (realmGap >= 8) chance += 0.08f;
        else if (realmGap <= -8) chance -= 0.08f;
        return Mathf.Clamp(chance, 0.01f, MaximumAttackChance);
    }

    public static int realm_score(string realm)
    {
        if (string.IsNullOrWhiteSpace(realm) || realm.Contains("凡人")) return 0;
        if (realm.StartsWith("云息", StringComparison.Ordinal)) return 10 + parse_tier(realm);
        if (realm.StartsWith("玄基", StringComparison.Ordinal)) return 30 + parse_tier(realm) * 3;
        if (realm.StartsWith("金丹", StringComparison.Ordinal)) return 75 + parse_tier(realm) * 6;
        if (realm.StartsWith("神胎", StringComparison.Ordinal)) return 160 + parse_tier(realm) * 12;
        if (realm.StartsWith("天人", StringComparison.Ordinal)) return 400 + parse_tier(realm) * 20;
        return 0;
    }

    public static ulong stable_seed(string value)
    {
        unchecked
        {
            ulong hash = 1469598103934665603;
            foreach (var character in value) { hash ^= character; hash *= 1099511628211; }
            return hash == 0 ? 1UL : hash;
        }
    }

    public static bool roll_attack(string seed, float chance)
    {
        var rng = new RandomNumberGenerator { Seed = stable_seed(seed) };
        return rng.Randf() < Mathf.Clamp(chance, 0f, 1f);
    }

    private static int get_attitude(WorldNpcData npc, string factionId)
    {
        if (string.IsNullOrWhiteSpace(factionId) || !npc.faction_reputations.TryGetValue(factionId, out var value)) return -1;
        return Mathf.Clamp(value.AsInt32(), 0, 6);
    }

    private static int parse_tier(string realm)
    {
        var start = realm.Length - 1;
        while (start >= 0 && char.IsDigit(realm[start])) start--;
        return int.TryParse(realm[(start + 1)..], out var tier) ? Mathf.Clamp(tier, 0, 99) : 0;
    }
}
