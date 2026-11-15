/// <summary>新战斗交锋的攻击方式；与物理、神魂等伤害类型分开判断。</summary>
public enum CombatClashAttackStyle
{
    Melee,
    Ranged
}

/// <summary>攻击互拼的结果。保留的近招尚未命中，须由后续行动继续处理。</summary>
public readonly record struct CombatAttackClashOutcome(
    bool FirstHits,
    bool SecondHits,
    bool FirstRetained,
    bool SecondRetained,
    bool FirstWon,
    bool SecondWon);

/// <summary>化解值属于本次来袭，不表示剩余量可以跨行动继承。</summary>
public readonly record struct CombatGuardClashOutcome(
    long Block,
    bool AttackWon,
    bool GuardWon);

/// <summary>闪避成功时保留同一份守招，后续交锋仍须单独验证和付款。</summary>
public readonly record struct CombatEvasionClashOutcome(
    bool AttackHits,
    bool AttackWon,
    bool EvasionWon,
    bool EvasionRetained);

/// <summary>
/// 只计算双方已支付且已掷点后的交锋结果。
/// 目标资格、费用、伤害、队列推进和定力结算由调用者处理。
/// </summary>
public static class CombatClashOutcome
{
    public static CombatAttackClashOutcome Attack(
        CombatClashAttackStyle firstStyle, int firstRoll,
        CombatClashAttackStyle secondStyle, int secondRoll)
    {
        ValidateStyle(firstStyle);
        ValidateStyle(secondStyle);
        if (firstRoll == secondRoll) return default;

        bool firstWon = firstRoll > secondRoll;
        var winnerStyle = firstWon ? firstStyle : secondStyle;
        var loserStyle = firstWon ? secondStyle : firstStyle;
        bool breaksRanged = winnerStyle == CombatClashAttackStyle.Melee
            && loserStyle == CombatClashAttackStyle.Ranged;

        return new(
            FirstHits: firstWon && !breaksRanged,
            SecondHits: !firstWon && !breaksRanged,
            FirstRetained: firstWon && breaksRanged,
            SecondRetained: !firstWon && breaksRanged,
            FirstWon: firstWon,
            SecondWon: !firstWon);
    }

    public static CombatGuardClashOutcome Guard(
        int attackRoll, int guardRoll, long blockOnGuardWin, long blockOnTie, long blockOnAttackWin)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(blockOnGuardWin);
        ArgumentOutOfRangeException.ThrowIfNegative(blockOnTie);
        ArgumentOutOfRangeException.ThrowIfNegative(blockOnAttackWin);
        bool guardWon = guardRoll > attackRoll;
        bool attackWon = attackRoll > guardRoll;
        long block = guardWon ? blockOnGuardWin : attackWon ? blockOnAttackWin : blockOnTie;
        return new(block, attackWon, guardWon);
    }

    public static CombatEvasionClashOutcome Evade(int attackRoll, int evasionRoll)
    {
        bool evasionWon = evasionRoll > attackRoll;
        return new(
            AttackHits: !evasionWon,
            AttackWon: attackRoll > evasionRoll,
            EvasionWon: evasionWon,
            EvasionRetained: evasionWon);
    }

    /// <summary>无对抗时只需命中；交锋中则必须本次拼赢并命中。</summary>
    public static bool GeneratesContinuation(
        bool hadClash, bool attackWon, bool hit, bool actorStillValid, bool targetStillValid)
        => hit && actorStillValid && targetStillValid && (!hadClash || attackWon);

    private static void ValidateStyle(CombatClashAttackStyle style)
    {
        if (!Enum.IsDefined(style)) throw new ArgumentOutOfRangeException(nameof(style));
    }
}
