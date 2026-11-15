using Godot;

// Legacy pricing experiment: its repeated-clash payment expectations were
// superseded by the user's rule that an action pays only once.
// It is not validation of the current proposed combat flow.

/// <summary>独立串联真实盘面牌与候选队列/账本/交锋，不触碰正式战斗入口。</summary>
public partial class BoardCombatIntegrationSmoke : Node
{
    private int _checks;
    private void Check(bool ok, string message)
    { if (!ok) throw new InvalidOperationException(message); _checks++; }

    public override void _Ready() => CallDeferred(nameof(Run));

    private void Run()
    {
        try
        {
            const string yingPath = "res://resources/activeskills/应敌.tres";
            const string painPath = "res://resources/activeskills/痛击.tres";
            const string jingPath = "res://resources/activeskills/玄枢含章经/惊蛰.tres";
            var ying = GD.Load<ActiveSkillData>(yingPath);
            var pain = GD.Load<ActiveSkillData>(painPath);
            var jing = GD.Load<ActiveSkillData>(jingPath);
            Check(ying is not null && pain is not null && jing is not null, "正式应敌、痛击及惊蛰资源可加载");
            var catalog = new CombatBoardCatalog { skills = [ying!, pain!, jing!] };
            var playerLayout = new CombatBoardLayout { size = new(3, 1), confirmed = true, placements =
            [
                new() { placement_id = "pain", source_key = painPath, cell = new(0, 0) },
                new() { placement_id = "ying", source_key = yingPath, cell = new(2, 0) }
            ] };
            var enemyLayout = new CombatBoardLayout { size = new(6, 1), confirmed = true, placements =
            [
                new() { placement_id = "jing-1", source_key = jingPath, cell = new(0, 0) },
                new() { placement_id = "jing-2", source_key = jingPath, cell = new(3, 0) }
            ] };
            Check(CombatBoardGeometry.Validate(playerLayout, catalog.FindSkill).Count == 0
                && CombatBoardGeometry.Validate(enemyLayout, catalog.FindSkill).Count == 0,
                "正式招式份数与占格校验通过");
            var playerDeck = BoardDeckFactory.Build(101, playerLayout, catalog);
            var enemyDeck = BoardDeckFactory.Build(202, enemyLayout, catalog);
            Check(playerDeck.Success && enemyDeck.Success && playerDeck.Cards.Count == 2 && enemyDeck.Cards.Count == 2,
                "两张摆盘各自产生独立实体牌");
            var player = new BoardHandState(playerDeck.Cards, new Random(11));
            var enemy = new BoardHandState(enemyDeck.Cards, new Random(22));
            Check(player.DrawOpeningHand() == 2 && enemy.DrawOpeningHand() == 2, "牌不足四张时各自抽完牌组");
            var painCard = player.Hand.Single(card => card.SourceKey == painPath);
            var yingCard = player.Hand.Single(card => card.SourceKey == yingPath);
            var enemyFirst = enemy.Hand.Single(card => card.Key.PlacementId == "jing-1");
            var enemySecond = enemy.Hand.Single(card => card.Key.PlacementId == "jing-2");
            // 仅用于此场景联调：容量成本不是现行招式数据或已批准的平衡值。
            const int sampleQueueCost = 1;
            Check(player.TryEnqueue(painCard.Key, 0, sampleQueueCost, out _)
                && player.TryEnqueue(yingCard.Key, 0, sampleQueueCost, out _)
                && enemy.TryEnqueue(enemyFirst.Key, 0, sampleQueueCost, out _)
                && enemy.TryEnqueue(enemySecond.Key, 0, sampleQueueCost, out _), "实体牌进入两方指定队列");
            Check(painCard.Skill.energy_cost == 2 && painCard.Skill.mana_cost == 0
                && yingCard.Skill.energy_cost == 1 && yingCard.Skill.mana_cost == 0
                && enemyFirst.Skill.energy_cost == 0 && enemyFirst.Skill.mana_cost == 1,
                "费用来自实际痛击、应敌和惊蛰资源");

            var ledger = new CombatClashPaymentLedger();
            Check(ledger.Register("player", new(6, 4, 4)) && ledger.Register("enemy", new(6, 4, 4)), "联调资金仅用于样例");
            var scheduler = new CombatQueueScheduler();
            var firstAction = "enemy:" + enemyFirst.Key.PlacementId;
            var secondAction = "enemy:" + enemySecond.Key.PlacementId;
            var painAction = "player:" + painCard.Key.PlacementId;
            var yingAction = "player:" + yingCard.Key.PlacementId;
            scheduler.AddQueue("player-q", "player", [
                new(painAction, QueueActionKind.Melee, "enemy-q"),
                new(yingAction, QueueActionKind.Melee, "enemy-q")]);
            scheduler.AddQueue("enemy-q", "enemy", [
                new(firstAction, QueueActionKind.Ranged, "player-q"),
                new(secondAction, QueueActionKind.Ranged, "player-q")]);
            var painCost = new CombatClashPaymentLedger.Cost(painCard.Skill.energy_cost, painCard.Skill.mana_cost, 0);
            var jingCost = new CombatClashPaymentLedger.Cost(enemyFirst.Skill.energy_cost, enemyFirst.Skill.mana_cost, 0);
            var painPay = new CombatClashPaymentLedger.Payment("player", painAction, painCost);
            var spell1Pay = new CombatClashPaymentLedger.Payment("enemy", firstAction, jingCost);
            var spell2Pay = new CombatClashPaymentLedger.Payment("enemy", secondAction, jingCost);

            void ResolveBreak(string eventId, CombatClashPaymentLedger.Payment spellPay, BoardCard spell,
                int playerRoll, int enemyRoll)
            {
                scheduler.BeginSubround([new("player-q", 2, 0), new("enemy-q", 1, 0)]);
                var step = scheduler.Next()!;
                Check(step.Kind == QueueStepKind.AttackClash && step.Actor.Id == painAction
                    && step.Defender?.Id == spellPay.ActionId, "同一痛击与对应的惊蛰行动交锋");
                var pay = ledger.TryPayClash(eventId, painPay, spellPay, out var receipt);
                Check(pay == CombatClashPaymentLedger.Outcome.Paid && receipt is { Second: not null }, "本次交锋双方按真实招式费用各付款一次");
                Check(ledger.TryPayClash(eventId, painPay, spellPay, out _) == CombatClashPaymentLedger.Outcome.AlreadyPaid,
                    "重复事件幂等不再扣费");
                var clash = CombatClashOutcome.Attack(CombatClashAttackStyle.Melee, playerRoll,
                    CombatClashAttackStyle.Ranged, enemyRoll);
                Check(clash.FirstWon && clash.FirstRetained && !clash.FirstHits && !clash.SecondHits,
                    "固定掷点下近招破术，未命中且保留");
                scheduler.Complete(step, new(true, QueueClashWinner.Actor, clash.FirstHits, clash.SecondHits,
                    !clash.FirstRetained, !clash.SecondRetained, true, true));
                Check(enemy.TryDiscardQueued(spell.Key) && ledger.CloseAction("enemy", spellPay.ActionId)
                    && scheduler.Next() is null, "被破术的惊蛰弃入原实体牌且队列本小轮不再行动");
                Check(scheduler.GetActions("player-q")[0].Id == painAction
                    && player.GetQueue(0)[0].Card.Key == painCard.Key, "同一份痛击保持在两个模型的队首");
            }

            // 掷点仅是测试输入，不代表招式骰区间或正式公式。
            ResolveBreak("clash-1", spell1Pay, enemyFirst, 6, 2);
            Check(ledger.TryGetBalance("player", out var playerAfterFirst) && playerAfterFirst.Stamina == 4
                && ledger.TryGetBalance("enemy", out var enemyAfterFirst) && enemyAfterFirst.Mana == 3,
                "第一拼按各自费用扣减");
            ResolveBreak("clash-2", spell2Pay, enemySecond, 5, 3);
            Check(ledger.TryGetBalance("player", out var playerAfterSecond) && playerAfterSecond.Stamina == 2
                && ledger.TryGetBalance("enemy", out var enemyAfterSecond) && enemyAfterSecond.Mana == 2,
                "第二拼作为新的交锋双方再次扣费");

            scheduler.BeginSubround([new("player-q", 2, 0), new("enemy-q", 1, 0)]);
            var final = scheduler.Next()!;
            Check(final.Kind == QueueStepKind.UnopposedAttack && final.Actor.Id == painAction, "破术后原痛击继续落击");
            Check(ledger.TryPayUnopposed("landing", painPay, out var landing) == CombatClashPaymentLedger.Outcome.Paid
                && landing is { PreviouslyPaidAction: true } && ledger.TryGetBalance("player", out var afterLanding)
                && afterLanding == playerAfterSecond, "落击复用已支付行动，不再扣费");
            scheduler.Complete(final, new(true, QueueClashWinner.None, true, false, true, false, true, true));
            Check(player.TryDiscardQueued(painCard.Key) && scheduler.Next() is null, "落击结束后原痛击牌进入弃牌堆");
            Check(player.DiscardPile.Single().Key == painCard.Key && player.GetQueue(0).Single().Card.Key == yingCard.Key
                && enemy.DiscardPile.Select(card => card.Key).ToHashSet().SetEquals(enemyDeck.Cards.Select(card => card.Key)),
                "痛击、应敌和两份惊蛰在队列与弃牌中身份守恒");
            Check(player.DiscardPile.Concat(player.GetQueue(0).Select(item => item.Card)).Select(card => card.Key)
                .ToHashSet().SetEquals(playerDeck.Cards.Select(card => card.Key)), "玩家实体牌未丢失、复制或重置身份");

            GD.Print($"BOARD_COMBAT_INTEGRATION_SMOKE_OK checks={_checks}");
            GetTree().Quit();
        }
        catch (Exception error)
        { GD.PushError("BOARD_COMBAT_INTEGRATION_SMOKE_FAILED " + error); GetTree().Quit(1); }
    }
}
