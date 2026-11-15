using Godot;

public partial class CombatQueueSchedulerSmoke : Node
{
    private int _checks;
    private void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); _checks++; }

    private static QueueStepResolution Result(QueueClashWinner winner = QueueClashWinner.None,
        bool actorHit = false, bool defenderHit = false, bool actorFinished = true, bool defenderFinished = false,
        QueueCombatAction? actorFollowUp = null, QueueCombatAction? defenderFollowUp = null) =>
        new(true, winner, actorHit, defenderHit, actorFinished, defenderFinished, true, true, actorFollowUp, defenderFollowUp);

    private static QueueCombatAction A(string id, QueueActionKind kind, string target = "") => new(id, kind, target);
    private static QueueScheduleInput O(string queue, int speed) => new(queue, speed, 0);

    public override void _Ready() => CallDeferred(nameof(Run));
    private void Run()
    {
        try
        {
            CheckContention(); CheckOperation(); CheckBreakAndContinuation(); CheckEvasion();
            CheckReverseBreak(); CheckBreakRotatesBothDirections(); CheckWaitingGuardAndInvalid(); CheckIneligibleDefender();
            GD.Print($"COMBAT_QUEUE_SCHEDULER_SMOKE_OK checks={_checks}");
            GetTree().Quit();
        }
        catch (Exception error)
        { GD.PushError("COMBAT_QUEUE_SCHEDULER_SMOKE_FAILED " + error); GetTree().Quit(1); }
    }

    private void CheckContention()
    {
        var s = new CombatQueueScheduler();
        s.AddQueue("a1", "a", [A("a1-1", QueueActionKind.Melee, "b")]);
        s.AddQueue("a2", "a", [A("a2-1", QueueActionKind.Melee, "b")]);
        s.AddQueue("b", "b", [A("b-1", QueueActionKind.Melee, "a2"), A("b-2", QueueActionKind.Melee, "a1")]);
        s.BeginSubround([O("b", 8), O("a2", 9), O("a1", 10)]);
        var first = s.Next()!;
        Check(first.QueueId == "a1" && first.Kind == QueueStepKind.AttackClash && first.Defender?.Id == "b-1",
            "高速先手把未行动的敌方队首拉入交锋");
        foreach (var actorCanPay in new[] { false, true })
        foreach (var defenderCanPay in new[] { false, true })
        {
            var report = s.InspectPaymentAvailability(first, actorCanPay, defenderCanPay);
            Check(report.Actor.ActionId == "a1-1" && report.Actor.CanPay == actorCanPay
                && report.Defender is { ActionId: "b-1" } && report.Defender.CanPay == defenderCanPay,
                "双方支付资格只透传不假设另一方结果");
        }
        var missing = false;
        try { _ = s.InspectPaymentAvailability(first, true, null); }
        catch (ArgumentException) { missing = true; }
        Check(missing && s.GetActions("a1").Count == 1 && s.GetActions("b").Count == 2,
            "缺少对方资格时拒绝隐含结算，不改变任何队列");
        s.Complete(first, Result(QueueClashWinner.Actor, actorHit: true, defenderFinished: true));
        var second = s.Next()!;
        Check(second.QueueId == "a2" && second.Kind == QueueStepKind.UnopposedAttack && second.Defender is null,
            "同小轮另一个敌方攻击不能再次拉走已经参与的队列");
        s.Complete(second, Result(actorHit: true));
        Check(s.Next() is null && s.GetActions("b").Single().Id == "b-2", "被提前配对的队列不再自行执行下一招");
    }

    private void CheckOperation()
    {
        var s = new CombatQueueScheduler();
        s.AddQueue("a", "a", [A("attack", QueueActionKind.Ranged, "b")]);
        s.AddQueue("b", "b", [A("buff", QueueActionKind.Operation)]);
        s.BeginSubround([O("a", 2), O("b", 1)]);
        var attack = s.Next()!;
        Check(attack.Kind == QueueStepKind.UnopposedAttack && attack.Defender is null, "攻击运转时不对运转拼点");
        var single = s.InspectPaymentAvailability(attack, false, null);
        Check(!single.Actor.CanPay && single.Defender is null, "单方行动资格不虚构防守方");
        s.Complete(attack, Result(actorHit: true));
        var operation = s.Next()!;
        Check(operation.Kind == QueueStepKind.Operation && operation.Actor.Id == "buff", "受到攻击不消费运转，运转仍能执行");
        s.Complete(operation, Result());
        Check(s.Next() is null, "运转小轮完成");
    }

    private void CheckBreakAndContinuation()
    {
        var s = new CombatQueueScheduler();
        s.AddQueue("a", "a", [A("start", QueueActionKind.Melee, "b")]);
        s.AddQueue("b", "b", [A("spell1", QueueActionKind.Ranged, "a"), A("spell2", QueueActionKind.Ranged, "a")]);
        s.BeginSubround([O("a", 2), O("b", 1)]);
        var first = s.Next()!;
        var forbidden = false;
        try { s.Complete(first, Result(QueueClashWinner.Actor, actorHit: true, defenderFinished: true,
            actorFollowUp: A("bad-follow", QueueActionKind.Melee, "b"))); }
        catch (ArgumentException) { forbidden = true; }
        Check(forbidden, "破术尚未命中不能生成追加段");
        s.Complete(first, Result(QueueClashWinner.Actor, actorFinished: false, defenderFinished: true));
        Check(s.Next() is null && s.GetActions("a").Single().Id == "start", "近招破术后原式留在队尾且本小轮不再行动");
        s.BeginSubround([O("a", 2), O("b", 1)]);
        var next = s.Next()!;
        Check(next.Defender?.Id == "spell2", "下一小轮同一近招可再次破术");
        s.Complete(next, Result(QueueClashWinner.Actor, actorFinished: false, defenderFinished: true));
        Check(s.Next() is null, "第二次破术仍未命中");
        s.BeginSubround([O("a", 2), O("b", 1)]);
        var hit = s.Next()!;
        Check(hit.Kind == QueueStepKind.UnopposedAttack, "远术全部解除后近招才尝试攻击本体");
        s.Complete(hit, Result(actorHit: true, actorFollowUp: A("follow1", QueueActionKind.Melee, "b")));
        Check(s.Next() is null && s.GetActions("a").Single().Id == "follow1", "起式命中只插入一段追加");
        s.BeginSubround([O("a", 2), O("b", 1)]);
        var follow = s.Next()!;
        s.Complete(follow, Result(actorHit: true, actorFollowUp: A("follow2", QueueActionKind.Melee, "b")));
        Check(s.Next() is null && s.GetActions("a").Single().Id == "follow2", "追加段各自命中后才产生下一段");
        s.BeginSubround([O("a", 2), O("b", 1)]);
        var missed = s.Next()!;
        forbidden = false;
        try { s.Complete(missed, Result(actorHit: false, actorFollowUp: A("follow3", QueueActionKind.Melee, "b"))); }
        catch (ArgumentException) { forbidden = true; }
        Check(forbidden, "未命中不能追加下一式");
        s.Complete(missed, Result(actorHit: false));
        Check(s.Next() is null && s.GetActions("a").Count == 0, "追加中断后队列不再生成新式");
    }

    private void CheckEvasion()
    {
        var s = new CombatQueueScheduler();
        s.AddQueue("a1", "a", [A("one", QueueActionKind.Melee, "b"), A("two", QueueActionKind.Melee, "b")]);
        s.AddQueue("a2", "a", [A("other", QueueActionKind.Melee, "b")]);
        s.AddQueue("b", "b", [A("dodge", QueueActionKind.Dodge, "a1")]);
        s.BeginSubround([O("b", 3), O("a2", 2), O("a1", 1)]);
        var prepare = s.Next()!;
        Check(prepare.Kind == QueueStepKind.PrepareDefense, "闪避可以先待命");
        s.Complete(prepare, Result());
        var other = s.Next()!;
        Check(other.Kind == QueueStepKind.UnopposedAttack, "闪避只等待指定来路");
        s.Complete(other, Result(actorHit: true));
        var dodge = s.Next()!;
        Check(dodge.Kind == QueueStepKind.DefenseClash && dodge.Defender?.Id == "dodge" && dodge.DefenderWasWaiting,
            "指定来路触发待命闪避");
        s.Complete(dodge, Result(QueueClashWinner.Defender, actorHit: false, defenderFinished: false));
        Check(s.Next() is null && s.GetWaitingDefenses("b").Single().Id == "dodge", "闪避胜利后仍待命");
        s.BeginSubround([O("a1", 2), O("a2", 1), O("b", 0)]);
        var dodgeAgain = s.Next()!;
        Check(dodgeAgain.Defender?.Id == "dodge" && dodgeAgain.Actor.Id == "two", "下一小轮同一闪避继续判定");
        s.Complete(dodgeAgain, Result(QueueClashWinner.Actor, actorHit: true, defenderFinished: true));
        Check(s.Next() is null && s.GetWaitingDefenses("b").Count == 0, "闪避失败后结束待命");
    }

    private void CheckReverseBreak()
    {
        var s = new CombatQueueScheduler();
        s.AddQueue("a", "a", [A("spell", QueueActionKind.Ranged, "b")]);
        s.AddQueue("b", "b", [A("sword", QueueActionKind.Melee, "a")]);
        s.BeginSubround([O("a", 2), O("b", 1)]);
        var step = s.Next()!;
        s.Complete(step, Result(QueueClashWinner.Defender, actorFinished: true, defenderFinished: false));
        Check(s.Next() is null && s.GetActions("b").Single().Id == "sword" && s.GetActions("a").Count == 0,
            "被提前拉入的近招胜远术也保留原式，远术结束");
        s.BeginSubround([O("b", 2), O("a", 1)]);
        step = s.Next()!;
        Check(step.Actor.Id == "sword" && step.Kind == QueueStepKind.UnopposedAttack, "被动破术近招下一小轮继续执行");
        s.Complete(step, Result(actorHit: true));
        Check(s.Next() is null, "反向破术后结算结束");
    }

    private void CheckWaitingGuardAndInvalid()
    {
        var s = new CombatQueueScheduler();
        s.AddQueue("a", "a", [A("meditate", QueueActionKind.Operation), A("attack", QueueActionKind.Melee, "b")]);
        s.AddQueue("b", "b", [A("guard", QueueActionKind.Guard, "a"), A("strike", QueueActionKind.Melee, "a")]);
        s.BeginSubround([O("b", 2), O("a", 1)]);
        var step = s.Next()!; s.Complete(step, Result());
        step = s.Next()!; s.Complete(step, Result());
        Check(s.Next() is null && s.GetWaitingDefenses("b").Single().Id == "guard", "守招进入待命并保留后续攻击");
        s.BeginSubround([O("a", 2), O("b", 1)]);
        step = s.Next()!;
        Check(step.Kind == QueueStepKind.DefenseClash && step.Defender?.Id == "guard", "待命守招优先于未行动队首攻击");
        s.Complete(step, new(false, QueueClashWinner.None, false, false, true, false, true, true));
        Check(s.GetWaitingDefenses("b").Count == 1, "外部判定攻击非法时不消费等待守招");
        var pending = s.Next()!;
        Check(pending.Actor.Id == "strike" && pending.Kind == QueueStepKind.UnopposedAttack,
            "非法攻击没有拉入对方，敌方队首仍可行动");
        s.Complete(pending, Result(actorHit: true));
        Check(s.Next() is null, "非法结果只推进调用方明确结束的行动");
    }

    private void CheckBreakRotatesBothDirections()
    {
        var s = new CombatQueueScheduler();
        var firstMelee = A("near-one", QueueActionKind.Melee, "b");
        var nextMelee = A("near-two", QueueActionKind.Melee, "b");
        s.AddQueue("a", "a", [firstMelee, nextMelee]);
        s.AddQueue("b", "b", [A("spell-one", QueueActionKind.Ranged, "a"), A("spell-two", QueueActionKind.Ranged, "a")]);
        s.BeginSubround([O("a", 2), O("b", 1)]);
        var first = s.Next()!;
        s.Complete(first, Result(QueueClashWinner.Actor, actorFinished: false, defenderFinished: true));
        var moved = s.GetActions("a");
        Check(moved.Select(action => action.Id).SequenceEqual(new[] { "near-two", "near-one" })
            && ReferenceEquals(moved[1], firstMelee), "主动近招破术移至自身队尾，保留同一行动实体");
        Check(s.Next() is null, "近招移尾后本小轮不再次出手");
        s.BeginSubround([O("a", 2), O("b", 1)]);
        var later = s.Next()!;
        Check(later.Actor.Id == "near-two" && later.Defender?.Id == "spell-two", "下一小轮同队原后续招式先出手");
        s.Complete(later, Result(QueueClashWinner.Actor, actorFinished: false, defenderFinished: true));
        Check(s.Next() is null && s.GetActions("a").Select(action => action.Id).SequenceEqual(new[] { "near-one", "near-two" }),
            "后续近招破术同样移尾，不复制或跳过原行动");

        var reverse = new CombatQueueScheduler();
        var defendingMelee = A("defending-near", QueueActionKind.Melee, "a");
        reverse.AddQueue("a", "a", [A("incoming-spell", QueueActionKind.Ranged, "b")]);
        reverse.AddQueue("b", "b", [defendingMelee, A("later-operation", QueueActionKind.Operation)]);
        reverse.BeginSubround([O("a", 2), O("b", 1)]);
        var incoming = reverse.Next()!;
        reverse.Complete(incoming, Result(QueueClashWinner.Defender, actorFinished: true, defenderFinished: false));
        var defendingQueue = reverse.GetActions("b");
        Check(defendingQueue.Select(action => action.Id).SequenceEqual(new[] { "later-operation", "defending-near" })
            && ReferenceEquals(defendingQueue[1], defendingMelee), "被拉入的近招破术也移至自己的队尾且保留实体");
        Check(reverse.Next() is null, "被拉入的近招本小轮不再出手");
        reverse.BeginSubround([O("b", 2), O("a", 1)]);
        var following = reverse.Next()!;
        Check(following.Actor.Id == "later-operation" && following.Kind == QueueStepKind.Operation,
            "被动破术移尾后本队后续运转先于原近招执行");
        reverse.Complete(following, Result());
        Check(reverse.Next() is null && reverse.GetActions("b").Single().Id == "defending-near",
            "后续行动执行后同一近招仍在队列中");
    }

    private void CheckIneligibleDefender()
    {
        var s = new CombatQueueScheduler();
        s.AddQueue("a", "a", [A("strike", QueueActionKind.Melee, "b")]);
        s.AddQueue("b", "b", [A("unaffordable", QueueActionKind.Ranged, "a"), A("later", QueueActionKind.Melee, "a")]);
        s.BeginSubround([O("a", 2), O("b", 1)]);
        var clash = s.Next()!;
        Check(clash.Kind == QueueStepKind.AttackClash, "双方排首招时先申请交锋");
        var unopposed = s.RemoveIneligibleDefender(clash);
        Check(unopposed.Kind == QueueStepKind.UnopposedAttack && unopposed.Actor.Id == "strike"
            && s.GetActions("b").Single().Id == "later", "明确移除无资格对手后转为单方行动");
        s.Complete(unopposed, Result(actorHit: true));
        Check(s.Next() is null && s.GetActions("b").Single().Id == "later", "对手失效后同小轮不重复推进下一式");
    }
}
