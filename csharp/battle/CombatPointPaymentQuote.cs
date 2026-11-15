/// <summary>由调用方识别的现行法力付款分支；有效体力成本由调用方事先计算。</summary>
public enum PointManaPaymentMode { Standard, ForcedCapacity, TransferredToStamina }
public enum PointPaymentQuoteStatus { Ready, InvalidInput, Unsupported, InsufficientStamina, InsufficientManaOrCapacity }
public enum PointPaymentEventKind { ManaSpent, ManaCapacityBurned, ZhouTianCompleted, ForcedCapacityStarted, ForcedCapacityCompleted }

/// <summary>原始上限与伤势压低后的有效上限分开；此结构不会修改真实角色。</summary>
public readonly record struct PointPaymentSnapshot(int Stamina, int Mana, int RawManaCapacity,
    int InnerWoundCapacityPenalty, int Qi, bool CanBurnCapacityForShortfall,
    bool CanQueueZhouTianAutoCast, bool ZhouTianAutoCastRunning)
{
    public int EffectiveManaCapacity => Math.Max(0, RawManaCapacity - InnerWoundCapacityPenalty);
}

/// <param name="EffectiveStamina">已计入减费、伤势及法力转体力的最终体力费用。</param>
/// <param name="ManaPoints">普通模式下应支付的法力；强制模式下需烧的上限点数；转体力模式必须为0。</param>
/// <param name="HasUnmodelledPaymentSideEffects">存在本原型尚未建模的支付副作用时必须为 true。</param>
/// <param name="ForcedPaymentAlreadyInProgress">旧系统已开始分段强制代付时，此原型不承诺复用其剩余量。</param>
public readonly record struct PointPaymentRequest(int EffectiveStamina, int ManaPoints,
    PointManaPaymentMode ManaMode, bool HasUnmodelledPaymentSideEffects,
    bool ForcedPaymentAlreadyInProgress);

public readonly record struct PointPaymentEvent(PointPaymentEventKind Kind, int EffectiveCapacity,
    int Mana, int Qi, bool QueuedZhouTianAutoCast);

public sealed record PointPaymentQuote(PointPaymentQuoteStatus Status, PointPaymentSnapshot Before,
    PointPaymentSnapshot After, IReadOnlyList<PointPaymentEvent> Events, string Reason)
{
    /// <summary>即使报价成功，也仍需正式战斗事务处理心法、灵识和效果回调。</summary>
    public bool NeedsRuntimeCallbacks => true;
}

/// <summary>
/// 只模拟一人的体力、逐点法力、上限与炁；不扣真实资源、不注册次数或施展行动。
/// 调用方负责提供完整的现行条件、识别不支持的副作用，不能把结果当作双人事务。
/// </summary>
public static class CombatPointPaymentQuote
{
    public static PointPaymentQuote Preview(PointPaymentSnapshot before, PointPaymentRequest request)
    {
        PointPaymentQuote Failure(PointPaymentQuoteStatus status, string reason) => new(status, before, before, [], reason);
        if (before.Stamina < 0 || before.Mana < 0 || before.RawManaCapacity < 0
            || before.InnerWoundCapacityPenalty is < 0 or > 2 || before.Qi < 0
            || before.Mana > before.EffectiveManaCapacity || request.EffectiveStamina < 0 || request.ManaPoints < 0
            || !Enum.IsDefined(request.ManaMode)
            || request.ManaMode == PointManaPaymentMode.ForcedCapacity && request.ManaPoints == 0
            || request.ManaMode == PointManaPaymentMode.TransferredToStamina && request.ManaPoints != 0)
            return Failure(PointPaymentQuoteStatus.InvalidInput, "资源快照或付款分支无效。");
        if (request.HasUnmodelledPaymentSideEffects || request.ForcedPaymentAlreadyInProgress)
            return Failure(PointPaymentQuoteStatus.Unsupported, "存在未建模支付副作用或已开始的分段强制代付。");
        if (before.Stamina < request.EffectiveStamina)
            return Failure(PointPaymentQuoteStatus.InsufficientStamina, "体力不足。");

        // 对齐当前 CanPay：普通法力短缺先整笔核对可烧上限，强制代付先核对全部烧额。
        var burnable = Math.Max(0, before.EffectiveManaCapacity - 1);
        if (request.ManaMode == PointManaPaymentMode.Standard)
        {
            var missing = Math.Max(0, request.ManaPoints - before.Mana);
            if (missing > 0 && (!before.CanBurnCapacityForShortfall || missing > burnable))
                return Failure(PointPaymentQuoteStatus.InsufficientManaOrCapacity, "法力及可消耗的法力上限不足。");
        }
        if (request.ManaMode == PointManaPaymentMode.ForcedCapacity && request.ManaPoints > burnable)
            return Failure(PointPaymentQuoteStatus.InsufficientManaOrCapacity, "强制代付需要的法力上限不足。");

        var state = before with { Stamina = before.Stamina - request.EffectiveStamina };
        var events = new List<PointPaymentEvent>();
        if (request.ManaMode == PointManaPaymentMode.ForcedCapacity)
            events.Add(new(PointPaymentEventKind.ForcedCapacityStarted, state.EffectiveManaCapacity, state.Mana, state.Qi, false));
        for (var i = 0; i < request.ManaPoints; i++)
        {
            if (request.ManaMode == PointManaPaymentMode.TransferredToStamina) break;
            if (request.ManaMode == PointManaPaymentMode.Standard && state.Mana > 0)
            {
                state = state with { Mana = state.Mana - 1 };
                events.Add(new(PointPaymentEventKind.ManaSpent, state.EffectiveManaCapacity, state.Mana, state.Qi, false));
                continue;
            }
            if (state.EffectiveManaCapacity - 1 < 1
                || request.ManaMode == PointManaPaymentMode.Standard && !state.CanBurnCapacityForShortfall)
                return Failure(PointPaymentQuoteStatus.InsufficientManaOrCapacity, "逐点付款时法力上限不足。");

            state = state with { RawManaCapacity = Math.Max(1, state.RawManaCapacity - 1) };
            state = state with { Mana = Math.Min(state.Mana, state.EffectiveManaCapacity) };
            events.Add(new(PointPaymentEventKind.ManaCapacityBurned, state.EffectiveManaCapacity, state.Mana, state.Qi, false));
            // 现行 spend_mana_capacity 每烧1点立即按新上限检测一次炁及周天。
            var effective = state.EffectiveManaCapacity;
            if (effective > 0 && (long)state.Qi >= 2L * effective)
            {
                state = state with { Mana = effective, Qi = 0 };
                events.Add(new(PointPaymentEventKind.ZhouTianCompleted, effective, state.Mana, state.Qi,
                    state.CanQueueZhouTianAutoCast && !state.ZhouTianAutoCastRunning));
            }
        }
        if (request.ManaMode == PointManaPaymentMode.ForcedCapacity)
            events.Add(new(PointPaymentEventKind.ForcedCapacityCompleted, state.EffectiveManaCapacity, state.Mana, state.Qi, false));
        return new(PointPaymentQuoteStatus.Ready, before, state, events.ToArray(), "");
    }
}
