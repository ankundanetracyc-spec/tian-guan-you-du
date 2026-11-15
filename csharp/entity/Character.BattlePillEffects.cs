using Godot;

public partial class Character
{
    private bool _usingBattlePill;
    public const string DefensePillStatus = "金肌丹效", RegenerationPillStatus = "续生丹效";
    private StatusInstance? FindPillStatus(string id) => status_snapshot().FirstOrDefault(s => s.effect?.effect_id == id);
    private bool CanRefreshPillStatus(string id, int tier)
    {
        var state = FindPillStatus(id);
        return state is null || tier > state.pill_efficacy_tier || tier == state.pill_efficacy_tier &&
            (state.remaining_duration < 2 || state.applied_round_id != battle_round_id || state.remaining_pill_ticks is >= 0 and < 2);
    }
    private bool CanReceiveBattlePill(PillItemData pill) => pill.pill_id switch
    {
        "huichun" => current_health < max_health,
        "ninggang" => shield < int.MaxValue,
        "jinji" => CanRefreshPillStatus(DefensePillStatus, pill.efficacy_tier),
        "xusheng" => CanRefreshPillStatus(RegenerationPillStatus, pill.efficacy_tier),
        "qingshen" => get_status_stacks("身法") < 20 || status_snapshot().Any(s => s.effect?.effect_id == "身法" && s.applied_round_id != battle_round_id),
        "jiegu" => get_status_stacks("外伤") > 0,
        "hezhong" => get_status_stacks("内伤") > 0,
        _ => false
    };

    private int ApplyBattlePill(PillItemData pill, int amount, int healing)
    {
        switch (pill.pill_id)
        {
            case "huichun": return apply_healing(amount);
            case "ninggang":
                int gain = (int)Math.Min(amount, (long)int.MaxValue - Math.Max(0, shield)); shield += gain; return gain;
            case "qingshen":
                var before = get_status_stacks("身法");
                add_status(GD.Load<StatusEffect>("res://resources/battle/statuses/身法.tres"), stacks: amount);
                return get_status_stacks("身法") - before;
            case "jiegu": case "hezhong":
                string wound = pill.pill_id == "jiegu" ? "外伤" : "内伤";
                int removed = Math.Min(amount, get_status_stacks(wound)); consume_status_stacks(wound, removed);
                apply_healing(healing); return removed;
            case "jinji": case "xusheng":
                bool regeneration = pill.pill_id == "xusheng";
                string id = regeneration ? RegenerationPillStatus : DefensePillStatus;
                var existing = FindPillStatus(id);
                if (existing is not null) statuses.Remove(existing);
                var effect = new StatusEffect { effect_id = id, name = AlchemyProductCatalog.Name(pill.pill_id),
                    description = regeneration ? $"后续两个自身回合开始各恢复{amount}点生命值。" : $"物理防御提高{amount}点，持续2回合。",
                    duration_type = StatusEffect.DurationType.Turns, duration_default = 2, max_stacks = 1,
                    heal_per_tick = regeneration ? amount : 0 };
                if (!regeneration) effect.stat_modifiers["physical_defense"] = amount;
                add_status(effect, 2, sourceName: pill.entity_name);
                var applied = FindPillStatus(id)!;
                applied.pill_efficacy_tier = pill.efficacy_tier;
                applied.remaining_pill_ticks = regeneration ? 2 : -1;
                return amount;
            default: return 0;
        }
    }
}
