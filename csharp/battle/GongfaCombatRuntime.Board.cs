using Godot;

public sealed partial class GongfaCombatRuntime
{
    /// <summary>复命的两次付款连续预览：代付只能消费一次，不提前修改真实战斗资源。</summary>
    public (int Energy, int Mana, int Capacity) PreviewBoardSequentialCosts(GodotObject owner, params ActiveSkillData[] skills)
    {
        var payments = PreviewBoardPayments(owner, skills);
        return (payments.Sum(payment => payment.Energy), payments.Sum(payment => payment.Mana), payments.Sum(payment => payment.Capacity));
    }

    public IReadOnlyList<(int Energy, int Mana, int Capacity)> PreviewBoardPayments(GodotObject owner, params ActiveSkillData[] skills)
    {
        var state = For(owner); var discounts = state.Discounts;
        var capacityBurnReady = Chemical(owner).ForceManaCapacityBurnReady;
        var payments = new List<(int Energy, int Mana, int Capacity)>();
        foreach (var skill in skills)
        {
            var stamina = SequentialEnergyCost(skill, ref discounts, state.Boost);
            var wounds = manager.get_realtime_wound_counts(owner);
            var requiredMana = manager.get_raw_skill_mana_cost(owner, skill);
            var capacity = 0;
            var forcedCapacity = requiredMana > 0 && (capacityBurnReady
                || Chemical(owner).ForcedManaCapacitySkillKey == ManaSkillKey(skill));
            var alreadyConverted = Main(owner, "斫基损契玄经") && state.ManaConvertedSkillKeys.Contains(ManaSkillKey(skill));
            if (forcedCapacity)
            {
                capacity = requiredMana;
                requiredMana = 0;
                capacityBurnReady = false;
            }
            else if (requiredMana > 0 && alreadyConverted)
            {
                stamina += requiredMana; requiredMana = 0;
            }
            if (skill.skill_type == ActiveSkillData.SkillType.MOVEMENT)
                stamina = Mathf.CeilToInt(stamina * BattleWoundRules.get_energy_cost_multiplier(wounds, skill));
            payments.Add((stamina, requiredMana, capacity));
            if (HasEffect(skill, "zhuo_ji_c2_jianzhu")) capacityBurnReady = true;
        }
        return payments;
    }

    /// <summary>攻击方的自身应对事件只处理对应被动，不伪造受击或占用防守方机会。</summary>
    private void NotifyTaiheSelfResponse(GodotObject owner) => GrantTaiheResponseBlood(owner);

    private void GrantTaiheResponseBlood(GodotObject owner)
    {
        if (!HasPower(owner, TaiheHeart)) return;
        var state = For(owner);
        var gain = HasHeartUpgrade(owner, TaiheBook, "taihe_c4") && !state.TaiheFirstResponseThisRound ? 2 : 1;
        state.TaiheFirstResponseThisRound = true;
        if (owner is Creature creature && creature.gain_qi_blood(gain) > 0)
        {
            creature.update_ui();
            manager.SyncBloodArmVisual(owner);
        }
    }

    public ActiveSkillData CreateTaiheFollowUpSegment(GodotObject owner, ActiveSkillData skill, int index)
    {
        return CreateTaiheFollowUpSegment(skill);
    }
}
