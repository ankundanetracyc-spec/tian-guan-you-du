using System;
using Godot;

/// <summary>探查的两次独立判定与按已有性情作出的确定反应。</summary>
public static class NpcProbeRules
{
    public enum Reaction { Tolerate, Reprimand, Attack }
    public readonly record struct Result(bool Success, bool Detected, double Probability);
    public static double Probability(double ownSoul, double targetSoul)
    {
        var own = double.IsFinite(ownSoul) ? Math.Max(1, ownSoul) : 1;
        var target = double.IsFinite(targetSoul) ? Math.Max(1, targetSoul) : 1;
        return own / (own + target);
    }
    public static Result Resolve(double ownSoul, double targetSoul, double successRoll, double concealRoll)
    {
        var p = Probability(ownSoul, targetSoul);
        var success = successRoll < p;
        return new(success, !success || concealRoll >= p, p);
    }
    public static Reaction Respond(WorldNpcData npc, double observedSoulRatio, bool repeated, bool forbidden,
        bool protectedRole, bool defiant = false)
    {
        bool Is(string axis, string value) => NpcPersonalityRules.Is(npc, axis, value);
        var hostile = CharacterRelationshipRules.IsHostile(npc, "player");
        var close = CharacterRelationshipRules.Value(npc, "player", CharacterRelationshipRules.Closeness) >= 30;
        var cautious = Is("risk", "谨慎");
        var forgiving = Is("forgiveness", "宽和");
        var grudging = Is("forgiveness", "记怨");
        // 只用记忆中的修为，或本次实际察觉的神魂来势，不偷读玩家完整属性。
        var knownRealm = npc.player_social_memory.GetValueOrDefault("player_realm", "").AsString();
        var knownStronger = observedSoulRatio >= 2 || knownRealm.Length > 0 &&
            NpcSocialRules.RealmAddress(npc.cultivation_realm, knownRealm, npc.current_age, npc.current_age) == "前辈";
        if (!hostile && !repeated && !defiant && (forgiving || close && !grudging)) return Reaction.Tolerate;
        if (knownStronger && cautious) return hostile || repeated || defiant ? Reaction.Reprimand : Reaction.Tolerate;
        var aggressive = hostile || repeated && grudging || defiant && !cautious ||
            Is("risk", "冒进") && (Is("benevolence", "冷酷") || Is("order", "不羁") || NpcPersonalityRules.Values(npc, "声名"));
        if (aggressive && !knownStronger && !forbidden && !protectedRole) return Reaction.Attack;
        if (aggressive || repeated || defiant || grudging || NpcPersonalityRules.Values(npc, "声名")) return Reaction.Reprimand;
        return Reaction.Tolerate;
    }
}
