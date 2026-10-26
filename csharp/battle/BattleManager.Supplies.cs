using Godot;

public partial class BattleManager
{
    private BattleSupplyHud? _supplyHud;
    internal bool SupplyHudVisible => battle_running && can_player_plan() && _planHud?.IsVisibleInTree() == true && !FlowTransitionActive;

    public int get_planned_pill_count(GodotObject unit, int slot) => battle_units.ContainsKey(unit)
        ? get_battle_unit(unit).skill_queue.Count(action => !action.completed && action.kind == BattleQueuedSkill.ActionKind.Pill && action.supply_slot == slot) : 0;

    public int get_available_supply_count(GodotObject unit, int slotIndex)
    {
        if (unit is not Character owner || slotIndex < 0 || slotIndex >= owner.battle_supplies.Count) return 0;
        var slot = owner.battle_supplies[slotIndex];
        var plannedSameItem = battle_units.ContainsKey(unit) ? get_battle_unit(unit).skill_queue.Count(action =>
            !action.completed && action.kind == BattleQueuedSkill.ActionKind.Pill && action.supply_path == slot.item_path) : 0;
        return Math.Max(0, Math.Min(slot.count - get_planned_pill_count(unit, slotIndex),
            BattleSupplyRules.PackCount(owner.pack_items, owner.pack_counts, slot.item_path) - plannedSameItem));
    }

    public bool queue_pill_action(int slot)
    {
        if (!can_player_plan() || _mainPlayerUnit is not Character owner || get_available_supply_count(owner, slot) <= 0) return false;
        var path = owner.battle_supplies[slot].item_path;
        return BattleSupplyRules.Load(path) is not null && PlacePlayerAction(BattleQueuedSkill.Pill(slot, path, owner));
    }

    internal HoverInfoManager.HoverInfoContent SupplyHover(Character owner, int index, string? queuedPath = null)
    {
        var slot = index >= 0 && index < owner.battle_supplies.Count ? owner.battle_supplies[index] : null;
        var pill = BattleSupplyRules.Load(queuedPath ?? slot?.item_path ?? "");
        if (pill is null) return new("未编入", "请在战前确认页从本人的行囊配置战备。每人3位，每位最多5件同一物品。");
        var amount = BattleSupplyRules.Amount(pill, get_effective_mana_capacity(owner), owner.PillToxicity);
        if (pill.pill_id == "huichun" && amount > 0) amount = Mathf.Max(1, Mathf.RoundToInt(amount * (1 + Mathf.Max(0, owner.healing_received_bonus))));
        var tier = pill.efficacy_tier;
        var absorption = owner.PillAbsorption * 100;
        var effect = pill.pill_id == "huichun"
            ? $"恢复自身{amount}点生命值，基础{AlchemyProductCatalog.Healing[tier]}点 计算公式：四舍五入(取较大值(1,向下取整({AlchemyProductCatalog.Healing[tier]}×{absorption:0.##}%))×{1 + Mathf.Max(0, owner.healing_received_bonus):0.####})＝{amount}"
            : $"恢复自身{amount}点法力，基础为有效法力上限的{AlchemyProductCatalog.ManaPercent[tier]}% 计算公式：有效法力上限[{get_effective_mana_capacity(owner)}]×{AlchemyProductCatalog.ManaPercent[tier]}%×{absorption:0.##}%，向下取整且基础恢复大于0时至少1点＝{amount}";
        return AlchemyPillVisual.Hover(pill, user: owner, effectOverride: effect);
    }

    private void ResolvePillAction(GodotObject unit, BattleQueuedSkill action)
    {
        if (unit is not Character owner || action.supply_slot < 0 || action.supply_slot >= owner.battle_supplies.Count ||
            owner.battle_supplies[action.supply_slot].item_path != action.supply_path ||
            !owner.TryUseBattlePill(action.supply_slot, get_effective_mana_capacity(unit), out var restored))
        {
            _resolvingUnit = null;
            SkipSegmentAction(unit);
            return;
        }
        _executor?.show_action_name(unit, action.get_display_name());
        _executor?.present_recovery(unit);
        // 治疗与法力变化均通过 Creature 的公共回复入口提示，避免重复飘字。
        _planHud?.PresentAction(unit);
        CompleteSystemActionPresentation(unit);
    }

    private void AddAiSupplyBranches(GodotObject unit, AiDraft draft, List<AiDraft> next,
        Godot.Collections.Array<ActiveSkillData> loadout, bool teammate, int remaining)
    {
        if (unit is not Character owner) return;
        for (var i = 0; i < owner.battle_supplies.Count; i++)
        {
            var slot = owner.battle_supplies[i];
            var pill = BattleSupplyRules.Load(slot.item_path);
            if (pill is null || slot.count <= draft.Actions.Count(a => a.kind == BattleQueuedSkill.ActionKind.Pill && a.supply_slot == i) ||
                BattleSupplyRules.PackCount(owner.pack_items, owner.pack_counts, slot.item_path) <=
                    draft.Actions.Count(a => a.kind == BattleQueuedSkill.ActionKind.Pill && a.supply_path == slot.item_path)) continue;
            var amount = BattleSupplyRules.Amount(pill, draft.Capacity, draft.PillToxicity);
            if (pill.pill_id == "huichun" && amount > 0) amount = Mathf.Max(1, Mathf.RoundToInt(amount * (1 + Mathf.Max(0, owner.healing_received_bonus))));
            if (amount <= 0) continue;
            var branch = draft.Copy();
            if (pill.pill_id == "huichun")
            {
                var health = draft.Health.GetValueOrDefault(unit, owner.current_health);
                var healing = Math.Min(amount, Math.Max(0, Mathf.RoundToInt(owner.max_health - health)));
                if (health <= 0 || healing <= 0) continue;
                branch.Health[unit] = Math.Min(owner.max_health, health + healing);
                // 复用既有 AI 回复技能的生存优先级及收益权重。
                var urgent = health / Math.Max(1, owner.max_health) < .30f;
                branch.Value += (urgent ? 5 : 3) * 100 + (urgent ? 1800 + healing * 12 : 400 + healing * 8);
            }
            else
            {
                var restored = Math.Min(amount, Math.Max(0, draft.Capacity - draft.Mana));
                if (restored <= 0 || remaining < 2) continue;
                branch.Mana += restored;
                if (!AiDraftCandidates(unit, loadout, branch, teammate, remaining - 1).Any(candidate => candidate.ManaCost > draft.Mana)) continue;
                branch.Value -= 35; // 与已有休整铺垫采用同一行动机会成本。
            }
            branch.PillToxicity += PillToxicityRules.Load(pill.pill_id, pill.efficacy_tier);
            branch.Actions.Add(BattleQueuedSkill.Pill(i, slot.item_path, unit));
            next.Add(branch);
        }
    }
}
