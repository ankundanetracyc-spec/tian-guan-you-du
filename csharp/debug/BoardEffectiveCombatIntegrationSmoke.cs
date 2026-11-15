using Godot;

/// <summary>新政策的隔离联调：真实盘面牌、队列、破术及首次参与付费原型。</summary>
public partial class BoardEffectiveCombatIntegrationSmoke : Node
{
    private int _checks;
    private void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); _checks++; }

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
            Check(ying is not null && pain is not null && jing is not null, "加载正式应敌、痛击和惊蛰");
            var catalog = new CombatBoardCatalog { skills = [ying!, pain!, jing!] };
            var playerLayout = new CombatBoardLayout { size = new(3, 1), confirmed = true, placements =
            [
                new() { placement_id = "pain", source_key = painPath, cell = new(0, 0) },
                new() { placement_id = "ying", source_key = yingPath, cell = new(2, 0) }
            ] };
            var enemyLayout = new CombatBoardLayout { size = new(3, 1), confirmed = true, placements =
            [new() { placement_id = "jing", source_key = jingPath, cell = new(0, 0) }] };
            Check(CombatBoardGeometry.Validate(playerLayout, catalog.FindSkill).Count == 0
                && CombatBoardGeometry.Validate(enemyLayout, catalog.FindSkill).Count == 0, "真实盘面几何和摆放份数合法");
            var playerDeck = BoardDeckFactory.Build(101, playerLayout, catalog);
            var enemyDeck = BoardDeckFactory.Build(202, enemyLayout, catalog);
            Check(playerDeck.Success && enemyDeck.Success && playerDeck.Cards.Count == 2 && enemyDeck.Cards.Count == 1,
                "各摆放一份生成独立实体牌");
            var player = new BoardHandState(playerDeck.Cards, new Random(11));
            var enemy = new BoardHandState(enemyDeck.Cards, new Random(22));
            Check(player.DrawOpeningHand() == 2 && enemy.DrawOpeningHand() == 1, "牌组不足四张时抽完已有实体牌");
            var painCard = player.Hand.Single(card => card.SourceKey == painPath);
            var yingCard = player.Hand.Single(card => card.SourceKey == yingPath);
            var jingCard = enemy.Hand.Single();
            Check(painCard.Skill.energy_cost == 2 && painCard.Skill.mana_cost == 0
                && yingCard.Skill.energy_cost == 1 && yingCard.Skill.mana_cost == 0
                && jingCard.Skill.energy_cost == 0 && jingCard.Skill.mana_cost == 1, "招式费用来自现有资源");
            Check(player.TryEnqueue(painCard.Key, 0, 2, out _) && player.TryEnqueue(yingCard.Key, 0, 1, out _)
                && enemy.TryEnqueue(jingCard.Key, 0, 2, out _), "按用户确认的队列容量点数入队");

            var payer = new CombatEffectiveActionTransaction();
            var playerInitial = new CombatEffectiveActionTransaction.Balance(
                painCard.Skill.energy_cost + yingCard.Skill.energy_cost, 0);
            var enemyInitial = new CombatEffectiveActionTransaction.Balance(0, jingCard.Skill.mana_cost);
            Check(payer.Register("player", playerInitial) && payer.Register("enemy", enemyInitial), "建立隔离费用模型");
            var painId = "player:" + painCard.Key.PlacementId;
            var yingId = "player:" + yingCard.Key.PlacementId;
            var jingId = "enemy:" + jingCard.Key.PlacementId;
            var painAction = new QueueCombatAction(painId, QueueActionKind.Melee, "enemy-q");
            var yingAction = new QueueCombatAction(yingId, QueueActionKind.Melee, "enemy-q");
            var jingAction = new QueueCombatAction(jingId, QueueActionKind.Ranged, "player-q");
            var scheduler = new CombatQueueScheduler();
            scheduler.AddQueue("player-q", "player", [painAction, yingAction]);
            scheduler.AddQueue("enemy-q", "enemy", [jingAction]);
            var painRequest = new CombatEffectiveActionTransaction.ActionRequest("player", painId,
                new(painCard.Skill.energy_cost, painCard.Skill.mana_cost));
            var yingRequest = new CombatEffectiveActionTransaction.ActionRequest("player", yingId,
                new(yingCard.Skill.energy_cost, yingCard.Skill.mana_cost));
            var jingRequest = new CombatEffectiveActionTransaction.ActionRequest("enemy", jingId,
                new(jingCard.Skill.energy_cost, jingCard.Skill.mana_cost));

            scheduler.BeginSubround([new("player-q", 2, 0), new("enemy-q", 1, 0)]);
            var clashStep = scheduler.Next()!;
            Check(clashStep.Kind == QueueStepKind.AttackClash && ReferenceEquals(clashStep.Actor, painAction)
                && ReferenceEquals(clashStep.Defender, jingAction), "痛击与惊蛰指定交锋");
            Check(payer.Prepare("break", painRequest, jingRequest, out var preview) == CombatEffectiveActionTransaction.Status.Ready
                && preview is not null && preview.FirstEligibility == CombatEffectiveActionTransaction.Eligibility.Ready
                && preview.SecondEligibility == CombatEffectiveActionTransaction.Eligibility.Ready, "交锋前分别核验双方可用");
            // 掷点与速度仅为验证顺序的测试输入，不定义任何正式公式。
            var outcome = CombatClashOutcome.Attack(CombatClashAttackStyle.Melee, 6, CombatClashAttackStyle.Ranged, 2);
            Check(outcome.FirstWon && outcome.FirstRetained && !outcome.FirstHits && !outcome.SecondHits,
                "痛击胜出只破术、没有命中");
            Check(payer.Commit(preview!, CombatEffectiveActionTransaction.Participation.Clash,
                CombatEffectiveActionTransaction.Participation.Clash, out var breakReceipt) == CombatEffectiveActionTransaction.Status.Committed
                && breakReceipt is not null && breakReceipt.First.Charged && breakReceipt.Second is { Charged: true },
                "第一次交锋双方均按各自招式费用支付");
            Check(payer.TryGetBalance("player", out var pAfterBreak)
                && pAfterBreak.Stamina == playerInitial.Stamina - painCard.Skill.energy_cost
                && payer.TryGetBalance("enemy", out var eAfterBreak)
                && eAfterBreak.Mana == enemyInitial.Mana - jingCard.Skill.mana_cost,
                "痛击扣2体力、惊蛰扣1法力，与胜负无关");
            scheduler.Complete(clashStep, new(true, QueueClashWinner.Actor, false, false, false, true, true, true));
            Check(player.TryReorder(0, painCard.Key, 1) && enemy.TryDiscardQueued(jingCard.Key),
                "同步将同一痛击牌移到应敌之后、惊蛰进入弃牌");
            Check(scheduler.GetActions("player-q").Select(action => action.Id).SequenceEqual(new[] { yingId, painId })
                && player.GetQueue(0).Select(item => item.Card.Key).SequenceEqual(new[] { yingCard.Key, painCard.Key })
                && ReferenceEquals(scheduler.GetActions("player-q")[1], painAction), "调度和实体牌均按同一身份轮转");
            Check(scheduler.Next() is null, "破术后的痛击本小轮不再次行动");

            scheduler.BeginSubround([new("player-q", 2, 0), new("enemy-q", 1, 0)]);
            var yingStep = scheduler.Next()!;
            Check(yingStep.Actor.Id == yingId && yingStep.Kind == QueueStepKind.UnopposedAttack, "下一小轮应敌先行动");
            Check(payer.Prepare("ying-hit", yingRequest, null, out var yingPreview) == CombatEffectiveActionTransaction.Status.Ready
                && yingPreview is { FirstEligibility: CombatEffectiveActionTransaction.Eligibility.Ready }, "应敌单方行动检查费用");
            Check(payer.Commit(yingPreview!, CombatEffectiveActionTransaction.Participation.SingleAction, null, out var yingReceipt)
                == CombatEffectiveActionTransaction.Status.Committed && yingReceipt is not null && yingReceipt.First.Charged,
                "应敌实际命中后按原费扣体力");
            scheduler.Complete(yingStep, new(true, QueueClashWinner.None, true, false, true, false, true, true));
            Check(player.TryDiscardQueued(yingCard.Key) && scheduler.Next() is null, "应敌结算后进入弃牌，痛击不在同小轮出手");

            scheduler.BeginSubround([new("player-q", 2, 0), new("enemy-q", 1, 0)]);
            var painStep = scheduler.Next()!;
            Check(painStep.Actor.Id == painId && ReferenceEquals(painStep.Actor, painAction)
                && painStep.Kind == QueueStepKind.UnopposedAttack, "同一痛击后续对本体发动");
            Check(payer.Prepare("pain-hit", painRequest, null, out var painPreview) == CombatEffectiveActionTransaction.Status.Ready
                && painPreview is { FirstEligibility: CombatEffectiveActionTransaction.Eligibility.Ready }, "原痛击重新检查实际落击资格");
            Check(payer.Commit(painPreview!, CombatEffectiveActionTransaction.Participation.SingleAction, null, out var painReceipt)
                == CombatEffectiveActionTransaction.Status.Committed && painReceipt is not null && !painReceipt.First.Charged
                && painReceipt.First.PreviouslyPaid && painReceipt.First.Before == painReceipt.First.After,
                "原痛击真正落击时沿用首拼费用，不重复扣费");
            Check(payer.Commit(painPreview!, CombatEffectiveActionTransaction.Participation.SingleAction, null, out _)
                == CombatEffectiveActionTransaction.Status.AlreadyCommitted
                && payer.TryGetBalance("player", out var playerFinal) && playerFinal.Stamina == 0,
                "重复同一事件不重复收费");
            scheduler.Complete(painStep, new(true, QueueClashWinner.None, true, false, true, false, true, true));
            Check(player.TryDiscardQueued(painCard.Key) && scheduler.Next() is null, "痛击完成后同一实体牌进入弃牌");
            Check(player.DiscardPile.Select(card => card.Key).ToHashSet().SetEquals(playerDeck.Cards.Select(card => card.Key))
                && enemy.DiscardPile.Select(card => card.Key).ToHashSet().SetEquals(enemyDeck.Cards.Select(card => card.Key))
                && player.DrawPile.Count == 0 && player.Hand.Count == 0 && player.GetQueue(0).Count == 0
                && enemy.DrawPile.Count == 0 && enemy.Hand.Count == 0 && enemy.GetQueue(0).Count == 0,
                "从盘到手牌、队列和弃牌实体身份守恒");

            CheckRepeatedDodge(yingCard.Skill);
            CheckCloudStages();
            GD.Print($"BOARD_EFFECTIVE_COMBAT_INTEGRATION_SMOKE_OK checks={_checks}");
            GetTree().Quit();
        }
        catch (Exception error)
        { GD.PushError("BOARD_EFFECTIVE_COMBAT_INTEGRATION_SMOKE_FAILED " + error); GetTree().Quit(1); }
    }

    private void CheckRepeatedDodge(ActiveSkillData attackSkill)
    {
        // 避锋旧资源尚无正式盘面定义；此处仅借用其现有费用，不宣称它已改为新闪避招式。
        var oldDodge = GD.Load<ActiveSkillData>("res://resources/activeskills/避锋.tres")
            ?? throw new InvalidOperationException("缺少避锋费用来源");
        var payer = new CombatEffectiveActionTransaction();
        Check(payer.Register("attacker", new(attackSkill.energy_cost * 2, attackSkill.mana_cost * 2))
            && payer.Register("defender", new(oldDodge.energy_cost, oldDodge.mana_cost)), "建立连续闪避隔离费用模型");
        var scheduler = new CombatQueueScheduler();
        var dodge = new QueueCombatAction("sample-dodge", QueueActionKind.Dodge, "attack-q");
        scheduler.AddQueue("attack-q", "attacker", [
            new("sample-attack-1", QueueActionKind.Melee, "dodge-q"),
            new("sample-attack-2", QueueActionKind.Melee, "dodge-q")]);
        scheduler.AddQueue("dodge-q", "defender", [dodge]);
        var dodgeRequest = new CombatEffectiveActionTransaction.ActionRequest("defender", dodge.Id,
            new(oldDodge.energy_cost, oldDodge.mana_cost));

        for (var index = 1; index <= 2; index++)
        {
            scheduler.BeginSubround([new("dodge-q", 2, 0), new("attack-q", 1, 0)]);
            var step = scheduler.Next()!;
            if (index == 1)
            {
                Check(step.Kind == QueueStepKind.PrepareDefense, "闪避先待命");
                scheduler.Complete(step, new(true, QueueClashWinner.None, false, false, true, false, true, true));
                Check(payer.TryGetBalance("defender", out var beforeTrigger)
                    && beforeTrigger.Stamina == oldDodge.energy_cost && beforeTrigger.Mana == oldDodge.mana_cost,
                    "守招仅待命没有交锋，资源不变");
                step = scheduler.Next()!;
            }
            Check(step.Kind == QueueStepKind.DefenseClash && ReferenceEquals(step.Defender, dodge), "同一闪避接指定来路");
            var attackRequest = new CombatEffectiveActionTransaction.ActionRequest("attacker", step.Actor.Id,
                new(attackSkill.energy_cost, attackSkill.mana_cost));
            Check(payer.Prepare("dodge-clash-" + index, attackRequest, dodgeRequest, out var preview)
                == CombatEffectiveActionTransaction.Status.Ready && preview is not null
                && preview.FirstEligibility == CombatEffectiveActionTransaction.Eligibility.Ready
                && preview.SecondEligibility == CombatEffectiveActionTransaction.Eligibility.Ready,
                "双方首拼或已付行动均具备交锋资格");
            Check(payer.Commit(preview!, CombatEffectiveActionTransaction.Participation.Clash,
                CombatEffectiveActionTransaction.Participation.Clash, out var receipt)
                == CombatEffectiveActionTransaction.Status.Committed && receipt is not null && receipt.First.Charged
                && receipt.Second is { } defenseReceipt && defenseReceipt.Charged == (index == 1)
                && defenseReceipt.PreviouslyPaid == (index == 2), "同一闪避首拼支付，后续拼点不重复支付");
            var result = CombatClashOutcome.Evade(1, 2);
            Check(result.EvasionWon && result.EvasionRetained && !result.AttackHits, "闪避获胜仍保留");
            scheduler.Complete(step, new(true, QueueClashWinner.Defender, false, false, true, false, true, true));
            Check(scheduler.Next() is null && ReferenceEquals(scheduler.GetWaitingDefenses("dodge-q").Single(), dodge),
                "每轮结束仍是同一份待命闪避");
        }
        Check(payer.TryGetBalance("attacker", out var attacking) && attacking.Stamina == 0
            && payer.TryGetBalance("defender", out var defending) && defending.Stamina == 0
            && defending.Mana == 0, "两张攻击各付一次，连续闪避只付原费一次");
    }

    private void CheckCloudStages()
    {
        const string path = "res://resources/activeskills/舒卷天章/云岚三叠.tres";
        var skill = GD.Load<ActiveSkillData>(path) ?? throw new InvalidOperationException("缺少云岚三叠");
        Check(skill.energy_cost == 2 && skill.mana_cost == 0, "云岚读取现有2体力费用");
        var catalog = new CombatBoardCatalog { skills = [skill] };
        var layout = new CombatBoardLayout { size = new(2, 2), confirmed = true,
            placements = [new() { placement_id = "cloud", source_key = path, cell = new(0, 0) }] };
        Check(CombatBoardGeometry.Validate(layout, catalog.FindSkill).Count == 0, "云岚使用真实摆盘定义");
        var deck = BoardDeckFactory.Build(303, layout, catalog);
        var hand = new BoardHandState(deck.Cards, new Random(1));
        Check(deck.Success && hand.DrawOpeningHand() == 1, "一份云岚摆放只生成一张实体牌");
        var card = hand.Hand.Single();
        Check(hand.TryEnqueue(card.Key, 0, 3, out _), "云岚按确认容量3占队列一次");
        var scheduler = new CombatQueueScheduler();
        scheduler.AddQueue("cloud-q", "cloud", [new("cloud-start", QueueActionKind.Melee, "foe-q")]);
        scheduler.AddQueue("foe-q", "foe", []);
        var payer = new CombatEffectiveActionTransaction();
        Check(payer.Register("cloud", new(skill.energy_cost, skill.mana_cost)), "云岚使用独立测试账户");
        var sharedPaymentIdentity = "cloud-card-payment:" + card.Key.PlacementId;
        var cost = new CombatEffectiveActionTransaction.Cost(skill.energy_cost, skill.mana_cost);
        for (var index = 0; index <= 3; index++)
        {
            scheduler.BeginSubround([new("cloud-q", 2, 0), new("foe-q", 1, 0)]);
            var step = scheduler.Next()!;
            var expectedId = index == 0 ? "cloud-start" : "cloud-follow-" + index;
            Check(step.Actor.Id == expectedId && step.Kind == QueueStepKind.UnopposedAttack,
                "云岚依序执行不同的行动实例 " + index);
            var payment = new CombatEffectiveActionTransaction.ActionRequest("cloud", step.Actor.Id, cost, sharedPaymentIdentity);
            Check(payer.Prepare("cloud-event-" + index, payment, null, out var preview)
                == CombatEffectiveActionTransaction.Status.Ready
                && preview is { FirstEligibility: CombatEffectiveActionTransaction.Eligibility.Ready },
                "每段以独立行动ID检查同一张牌的付款凭证 " + index);
            Check(payer.Commit(preview!, CombatEffectiveActionTransaction.Participation.SingleAction, null, out var receipt)
                == CombatEffectiveActionTransaction.Status.Committed && receipt is not null
                && receipt.First.Charged == (index == 0) && receipt.First.PreviouslyPaid == (index > 0),
                "起式支付2体力，追加段使用同一已付凭证 " + index);
            var following = index < 3 ? new QueueCombatAction("cloud-follow-" + (index + 1), QueueActionKind.Melee, "foe-q") : null;
            // 此处每段命中和允许追加由测试显式输入，并非新的伤害或触发概率公式。
            scheduler.Complete(step, new(true, QueueClashWinner.None, true, false, true, false, true, true, following));
            Check(scheduler.Next() is null && scheduler.GetActions("cloud-q").Count == (index < 3 ? 1 : 0)
                && hand.GetQueue(0).Count == 1 && hand.GetQueue(0)[0].Card.Key == card.Key,
                "每次命中最多生成一段伪行动，实体牌仍只有原张 " + index);
        }
        Check(payer.TryGetBalance("cloud", out var after) && after.Stamina == 0
            && hand.TryDiscardQueued(card.Key) && hand.DiscardPile.Single().Key == card.Key,
            "四式共扣2体力并只弃置原云岚实体牌");
    }
}
