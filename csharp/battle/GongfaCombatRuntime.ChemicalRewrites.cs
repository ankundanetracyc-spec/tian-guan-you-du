using Godot;

/// <summary>机制化卷章化法的延后结算与条件判断。</summary>
public sealed partial class GongfaCombatRuntime
{
    private sealed class ChemicalRewriteState
    {
        public int PendingBaihaiQiBlood;
        public int PendingTaiheShield;
        public bool ForceManaCapacityBurnReady;
        public string ForcedManaCapacitySkillKey = "";
        public int ForcedManaCapacityRemaining;
    }

    private readonly Dictionary<ulong, ChemicalRewriteState> chemicalRewriteStates = [];

    private ChemicalRewriteState Chemical(GodotObject owner)
    {
        if (!chemicalRewriteStates.TryGetValue(owner.GetInstanceId(), out var state))
            chemicalRewriteStates[owner.GetInstanceId()] = state = new();
        return state;
    }

    private void ResetChemicalRewrites(GodotObject owner) => chemicalRewriteStates[owner.GetInstanceId()] = new();

    private void BeginChemicalRound(GodotObject owner)
    {
        var state = Chemical(owner);
        state.ForceManaCapacityBurnReady = false;
        state.ForcedManaCapacitySkillKey = "";
        state.ForcedManaCapacityRemaining = 0;
    }

    private void FinishChemicalIncomingAttack(GodotObject owner)
    {
        var state = Chemical(owner);
        if (state.PendingTaiheShield > 0)
        {
            GrantShield(owner, state.PendingTaiheShield);
            state.PendingTaiheShield = 0;
        }
    }

    public bool ShouldDelayTaiheResponseShield(GodotObject owner, ActiveSkillData skill)
    {
        var state = For(owner);
        return HasEffect(skill, "taihe_c2_hanxu") && state.ResponseTriggered
            && ReferenceEquals(state.PendingResponseCard, skill);
    }

    public void DeferTaiheResponseShield(GodotObject owner, int amount)
    {
        if (amount > 0) Chemical(owner).PendingTaiheShield = (int)Math.Min(int.MaxValue, (long)Chemical(owner).PendingTaiheShield + amount);
    }

    public void PrepareChemicalAttack(GodotObject owner, ActiveSkillData skill)
    {
        if (!IsMeleePhysicalAttack(skill)) return;
        var state = Chemical(owner);
        if (state.PendingBaihaiQiBlood <= 0) return;
        var amount = state.PendingBaihaiQiBlood;
        state.PendingBaihaiQiBlood = 0;
        Gain(owner, amount);
        manager.SyncBloodArmVisual(owner);
    }

    public void DeferBaihaiQiBlood(GodotObject owner, int amount)
    {
        if (amount > 0)
            Chemical(owner).PendingBaihaiQiBlood = (int)Math.Min(int.MaxValue, (long)Chemical(owner).PendingBaihaiQiBlood + amount);
    }

    public void CompleteForcedManaCapacityPayment(GodotObject owner, ActiveSkillData skill)
    {
        var state = Chemical(owner);
        if (state.ForcedManaCapacitySkillKey == ManaSkillKey(skill))
        {
            state.ForcedManaCapacitySkillKey = "";
            state.ForcedManaCapacityRemaining = 0;
        }
    }

    public bool ForceManaCapacityPayment(GodotObject owner, ActiveSkillData skill, int manaCost)
    {
        if (manaCost <= 0) return false;
        var state = Chemical(owner);
        var key = ManaSkillKey(skill);
        return state.ForcedManaCapacitySkillKey == key || state.ForceManaCapacityBurnReady;
    }

    public int ForcedManaCapacityCost(GodotObject owner, ActiveSkillData skill, int manaCost)
    {
        if (!ForceManaCapacityPayment(owner, skill, manaCost)) return 0;
        var state = Chemical(owner);
        return state.ForcedManaCapacitySkillKey == ManaSkillKey(skill)
            ? Math.Max(0, state.ForcedManaCapacityRemaining) : manaCost;
    }

    public bool CanPayForcedManaCapacity(GodotObject owner, ActiveSkillData skill, int amount)
    {
        var state = Chemical(owner);
        var remaining = state.ForcedManaCapacitySkillKey == ManaSkillKey(skill)
            ? state.ForcedManaCapacityRemaining : amount;
        return remaining <= Math.Max(0, manager.get_effective_mana_capacity(owner) - 1);
    }

    public void ArmForcedManaCapacityPayment(GodotObject owner) => Chemical(owner).ForceManaCapacityBurnReady = true;

    public void BeginForcedManaCapacityPayment(GodotObject owner, ActiveSkillData skill, int amount)
    {
        var state = Chemical(owner);
        if (!state.ForceManaCapacityBurnReady || amount <= 0) return;
        state.ForceManaCapacityBurnReady = false;
        state.ForcedManaCapacitySkillKey = ManaSkillKey(skill);
        state.ForcedManaCapacityRemaining = amount;
    }

    public void ConsumeForcedManaCapacityPoint(GodotObject owner, ActiveSkillData skill)
    {
        var state = Chemical(owner);
        if (state.ForcedManaCapacitySkillKey != ManaSkillKey(skill)) return;
        state.ForcedManaCapacityRemaining = Math.Max(0, state.ForcedManaCapacityRemaining - 1);
        if (state.ForcedManaCapacityRemaining == 0) CompleteForcedManaCapacityPayment(owner, skill);
    }

    public int ManaBeforeSkill(GodotObject owner, ActiveSkillData skill)
    {
        var state = For(owner);
        return state.PendingManaBeforeSkillKey == ManaSkillKey(skill)
            ? state.PendingManaBeforeSkill
            : state.CurrentManaBeforeSkill;
    }

    public bool BypassesShield(ActiveSkillData skill, bool zhouTianExtraCast) =>
        zhouTianExtraCast && skill.skill_name == "惊蛰" && HasEffect(skill, "xuan_shu_c1_jingzhe");

}
