using Godot;

public partial class CombatPointPaymentQuoteSmoke : Node
{
    private int _checks;
    private void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); _checks++; }
    private static PointPaymentRequest Request(int stamina, int mana, PointManaPaymentMode mode = PointManaPaymentMode.Standard,
        bool unsupported = false, bool inProgress = false) => new(stamina, mana, mode, unsupported, inProgress);
    private static PointPaymentSnapshot Snapshot(int stamina, int mana, int raw, int penalty, int qi,
        bool burn = true, bool automatic = true, bool running = false) =>
        new(stamina, mana, raw, penalty, qi, burn, automatic, running);

    public override void _Ready() => CallDeferred(nameof(Run));
    private void Run()
    {
        try
        {
            var before = Snapshot(5, 0, 3, 0, 4);
            var quote = CombatPointPaymentQuote.Preview(before, Request(2, 2));
            Check(quote.Status == PointPaymentQuoteStatus.Ready && quote.After.Stamina == 3
                && quote.After.RawManaCapacity == 2 && quote.After.EffectiveManaCapacity == 2
                && quote.After.Mana == 1 && quote.After.Qi == 0, "烧一上限立即周天回法，同笔第二点扣回复的法力");
            Check(quote.Events.Select(item => item.Kind).SequenceEqual(new[] { PointPaymentEventKind.ManaCapacityBurned,
                PointPaymentEventKind.ZhouTianCompleted, PointPaymentEventKind.ManaSpent })
                && quote.Events[1].QueuedZhouTianAutoCast, "逐点事件顺序与周天自动施展标记");
            Check(before == Snapshot(5, 0, 3, 0, 4) && quote.Before == before && quote.NeedsRuntimeCallbacks,
                "纯报价不修改输入且标明仍需运行时回调");

            quote = CombatPointPaymentQuote.Preview(Snapshot(5, 0, 4, 1, 4), Request(0, 2));
            Check(quote.Status == PointPaymentQuoteStatus.Ready && quote.After.RawManaCapacity == 3
                && quote.After.EffectiveManaCapacity == 2 && quote.After.Mana == 1,
                "内伤时保留原始上限并按有效上限计算周天");

            quote = CombatPointPaymentQuote.Preview(Snapshot(5, 0, 2, 0, 2), Request(0, 2));
            Check(quote.Status == PointPaymentQuoteStatus.InsufficientManaOrCapacity
                && quote.After == quote.Before && quote.Events.Count == 0, "整笔预检不足时不提前烧上限或触发周天");
            quote = CombatPointPaymentQuote.Preview(Snapshot(5, 0, 4, 2, 0), Request(0, 2));
            Check(quote.Status == PointPaymentQuoteStatus.InsufficientManaOrCapacity, "内伤压低有效上限后正确拒绝不足");
            quote = CombatPointPaymentQuote.Preview(Snapshot(5, 0, 3, 0, 4, burn: false), Request(0, 1));
            Check(quote.Status == PointPaymentQuoteStatus.InsufficientManaOrCapacity, "无烧上限资格时法力不足不能支付");

            quote = CombatPointPaymentQuote.Preview(Snapshot(5, 2, 3, 0, 4), Request(1, 2, PointManaPaymentMode.ForcedCapacity));
            Check(quote.Status == PointPaymentQuoteStatus.Ready && quote.After.Stamina == 4
                && quote.After.RawManaCapacity == 1 && quote.After.Mana == 1 && quote.After.Qi == 0
                && quote.Events.Count(item => item.Kind == PointPaymentEventKind.ManaCapacityBurned) == 2,
                "强制代付逐点烧上限，周天后仍继续烧剩余点数");
            Check(quote.Events.First().Kind == PointPaymentEventKind.ForcedCapacityStarted
                && quote.Events.Last().Kind == PointPaymentEventKind.ForcedCapacityCompleted,
                "强制代付的开始与结束被标记供后续动作上下文处理");
            quote = CombatPointPaymentQuote.Preview(Snapshot(5, 2, 3, 0, 4), Request(0, 3, PointManaPaymentMode.ForcedCapacity));
            Check(quote.Status == PointPaymentQuoteStatus.InsufficientManaOrCapacity && quote.Events.Count == 0,
                "强制代付整笔上限预检不使用当前法力代替");
            quote = CombatPointPaymentQuote.Preview(Snapshot(5, 0, 3, 0, 4, automatic: true, running: true), Request(0, 1));
            Check(quote.After.Mana == 2 && quote.Events.Any(item => item.Kind == PointPaymentEventKind.ZhouTianCompleted
                && !item.QueuedZhouTianAutoCast), "周天自动施展期间仍回法，但不递归排入自动施展");
            quote = CombatPointPaymentQuote.Preview(Snapshot(5, 2, 3, 0, 4), Request(0, 2));
            Check(quote.After.Mana == 0 && quote.After.RawManaCapacity == 3
                && quote.Events.All(item => item.Kind == PointPaymentEventKind.ManaSpent), "直接支付法力时不自行检测周天或烧上限");
            quote = CombatPointPaymentQuote.Preview(Snapshot(5, 2, 3, 0, 0), Request(3, 0, PointManaPaymentMode.TransferredToStamina));
            Check(quote.Status == PointPaymentQuoteStatus.Ready && quote.After.Stamina == 2 && quote.After.Mana == 2,
                "法力已转体力时只使用调用方提供的最终体力费用");
            quote = CombatPointPaymentQuote.Preview(before, Request(0, 1, PointManaPaymentMode.TransferredToStamina));
            Check(quote.Status == PointPaymentQuoteStatus.InvalidInput, "转体力分支不允许再收法力");
            quote = CombatPointPaymentQuote.Preview(before, Request(6, 0));
            Check(quote.Status == PointPaymentQuoteStatus.InsufficientStamina && quote.After == before, "体力不足不产生部分支付");
            quote = CombatPointPaymentQuote.Preview(before, Request(0, 1, unsupported: true));
            Check(quote.Status == PointPaymentQuoteStatus.Unsupported && quote.After == before, "未知支付副作用明确返回不支持");
            quote = CombatPointPaymentQuote.Preview(before, Request(0, 1, inProgress: true));
            Check(quote.Status == PointPaymentQuoteStatus.Unsupported && quote.After == before, "已开始的分段强制代付不冒充完整报价");
            quote = CombatPointPaymentQuote.Preview(Snapshot(5, 3, 3, 1, 0), Request(0, 0));
            Check(quote.Status == PointPaymentQuoteStatus.InvalidInput, "拒绝当前法力高于有效上限的无效快照");

            GD.Print($"COMBAT_POINT_PAYMENT_QUOTE_SMOKE_OK checks={_checks}");
            GetTree().Quit();
        }
        catch (Exception error)
        { GD.PushError("COMBAT_POINT_PAYMENT_QUOTE_SMOKE_FAILED " + error); GetTree().Quit(1); }
    }
}
