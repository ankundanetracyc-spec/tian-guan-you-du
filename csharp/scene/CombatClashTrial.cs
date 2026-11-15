using Godot;

/// <summary>独立试玩：真实牌面数据与固定人物样例，不接正式战斗或存档。</summary>
public partial class CombatClashTrial : Control
{
    private sealed class Fighter(string id, HumanData stats, BoardHandState hand)
    {
        public string Id { get; } = id;
        public HumanData Stats { get; } = stats;
        public BoardHandState Hand { get; } = hand;
        public int Qi;
    }
    private sealed record Planned(Fighter Owner, BoardCard Card, int Queue, int TargetQueue, string Id);
    private const string Ying = "res://resources/activeskills/应敌.tres";
    private const string Pain = "res://resources/activeskills/痛击.tres";
    private const string Jing = "res://resources/activeskills/玄枢含章经/惊蛰.tres";
    private const string Qi = "res://resources/activeskills/聚炁.tres";
    [Export] public int TrialSeed = -1;
    private Random _random = new();
    private readonly Dictionary<string, Planned> _actions = new(StringComparer.Ordinal);
    private readonly int?[] _queueTargets = new int?[BoardHandState.QueueCount];
    private Fighter _player = null!;
    private Fighter _enemy = null!;
    private CombatEffectiveActionTransaction _payment = null!;
    private CombatQueueScheduler? _scheduler;
    private BoardCardKey? _selected;
    private int _round = 1;
    private int _event;
    private int _subround;
    private bool _planning = true;
    private bool _finished;
    private Label _selection = null!;
    private Label _phase = null!;
    private RichTextLabel _log = null!;
    private Button _commit = null!;
    private Button _advance = null!;

    public int TrialRound => _round;
    public bool IsPlanning => _planning;
    public bool IsFinished => _finished;
    public BoardHandState PlayerHand => _player.Hand;
    public BoardHandState EnemyHand => _enemy.Hand;
    public CombatEffectiveActionTransaction Payment => _payment;
    public HumanData PlayerStats => _player.Stats;
    public HumanData EnemyStats => _enemy.Stats;
    public int PlayerQi => _player.Qi;
    public string TrialLog => _log.GetParsedText();
    public int PreviewPlayerDamage(BoardCard card) => Damage(card, _player);

    public override void _Ready()
    {
        // 独立试玩使用 1280×720 逻辑画布，避免按主工程大画布缩小字号。
        GetWindow().ContentScaleSize = new Vector2I(1280, 720);
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        SetupBattle();
        BuildStageUi();
        Write("第 1 回合。将手牌排进我方队列，再为攻击队列选择交互目标。");
        Render();
    }

    private void SetupBattle()
    {
        _random = TrialSeed < 0 ? new Random() : new Random(TrialSeed);
        var catalog = new CombatBoardCatalog { skills = [Load(Ying), Load(Pain), Load(Jing), Load(Qi)] };
        CombatBoardLayout Layout() => new() { size = new(5, 2), confirmed = true, placements =
        [
            new() { placement_id = "pain", source_key = Pain, cell = new(0, 0) },
            new() { placement_id = "ying-1", source_key = Ying, cell = new(2, 0) },
            new() { placement_id = "ying-2", source_key = Ying, cell = new(3, 0) },
            new() { placement_id = "ying-3", source_key = Ying, cell = new(4, 0) },
            new() { placement_id = "jing", source_key = Jing, cell = new(0, 1) },
            new() { placement_id = "qi", source_key = Qi, cell = new(3, 1) }
        ] };
        var layout = Layout();
        if (CombatBoardGeometry.Validate(layout, catalog.FindSkill).Count != 0) throw new InvalidOperationException("试玩摆盘不合法。");
        var playerDeck = BoardDeckFactory.Build(1, layout, catalog);
        var enemyDeck = BoardDeckFactory.Build(2, layout.CopyConfiguration(), catalog);
        if (!playerDeck.Success || !enemyDeck.Success) throw new InvalidOperationException("无法从摆盘生成试玩牌组。");
        _player = new("我方", SampleStats(), new(playerDeck.Cards, _random));
        _enemy = new("对手", SampleStats(), new(enemyDeck.Cards, _random));
        _payment = new();
        if (!_payment.Register(_player.Id, new(6, 1)) || !_payment.Register(_enemy.Id, new(6, 1)))
            throw new InvalidOperationException("试玩资源初始化失败。");
        _player.Hand.DrawOpeningHand();
        _enemy.Hand.DrawOpeningHand();
    }

    private static ActiveSkillData Load(string path) => GD.Load<ActiveSkillData>(path)
        ?? throw new InvalidOperationException("找不到招式：" + path);

    private static HumanData SampleStats() => new()
    {
        cultivation_realm = "云息五层", physique = 20, intelligence = 11.4f, soul = 12,
        body_proficiency = 5, sword_proficiency = 5, magic_proficiency = 6,
        max_health = 303, current_health = 303, max_stamina = 6, current_stamina = 6,
        max_mana = 1, current_mana = 1
    };

    private void Write(string text) { _log.AppendText(text + "\n"); }

    private void Render()
    {
        RefreshStage();
        RefreshHand();
        RefreshQueues();
    }

    private static int Capacity(BoardCard card) => card.SourceKey is Pain or Jing ? 2 : 1;
    private static int Damage(BoardCard card, Fighter owner) => Math.Max(0, Mathf.RoundToInt(card.Skill.calculate_damage(owner.Stats)));
    private static string LingjiRange(BoardCard card, Fighter owner)
    {
        var ranged = card.SourceKey == Jing;
        return CombatLingjiCalculator.TryCalculate(ranged ? owner.Stats.intelligence : owner.Stats.physique,
            ranged ? owner.Stats.magic_proficiency : owner.Stats.body_proficiency, 1, out var value)
            ? $"{value.Minimum}–{value.Maximum}" : "未知";
    }
    private static QueueActionKind Kind(BoardCard card) => card.SourceKey == Qi ? QueueActionKind.Operation
        : card.SourceKey == Jing ? QueueActionKind.Ranged : QueueActionKind.Melee;
    private static CombatClashAttackStyle Style(BoardCard card) => Kind(card) == QueueActionKind.Ranged
        ? CombatClashAttackStyle.Ranged : CombatClashAttackStyle.Melee;

    public bool QueuePlayerCard(BoardCardKey key, int queue, int target) =>
        target >= 0 && target < BoardHandState.QueueCount && TryPlacePlayerCard(key, queue, target);

    private bool TryPlacePlayerCard(BoardCardKey key, int queue, int? target)
    {
        if (!_planning || _finished || queue < 0 || queue >= BoardHandState.QueueCount) return false;
        var card = _player.Hand.Hand.FirstOrDefault(item => item.Key == key);
        if (card is null) return false;
        var attacking = Kind(card) != QueueActionKind.Operation;
        if (attacking && target.HasValue && QueueNeedsTarget(queue) && _queueTargets[queue] is { } locked && locked != target)
        {
            _planningNotice = $"我方{QueueNames[queue]}已锁定对手{QueueNames[locked]}。可点右侧队列修改整队目标。";
            RefreshQueues();
            return false;
        }
        if (!_player.Hand.TryEnqueue(key, queue, Capacity(card), out var reason))
        {
            _planningNotice = reason;
            RefreshQueues();
            return false;
        }
        if (attacking && target.HasValue) _queueTargets[queue] = target;
        _editingQueue = queue;
        _planningNotice = "";
        _selected = null;
        Render();
        return true;
    }

    public void WithdrawLast(int queue)
    {
        if (!_planning || _finished || queue < 0 || queue >= BoardHandState.QueueCount) return;
        var last = _player.Hand.GetQueue(queue).LastOrDefault();
        if (last is not null) WithdrawCard(last.Card.Key, queue);
    }

    private void WithdrawCard(BoardCardKey key, int queue)
    {
        if (!_planning || _finished || !_player.Hand.TryWithdraw(key)) return;
        _editingQueue = queue;
        _planningNotice = "";
        Render();
    }

    public void CommitPlan()
    {
        if (!_planning || _finished) return;
        for (var queue = 0; queue < BoardHandState.QueueCount; queue++)
        {
            if (!QueueNeedsTarget(queue) || _queueTargets[queue].HasValue) continue;
            _editingQueue = queue;
            _planningNotice = $"请为我方{QueueNames[queue]}选择交互目标。";
            RefreshQueues();
            return;
        }
        _selected = null;
        _editingQueue = null;
        _planningNotice = "";
        _exchangePanel.Hide();
        // 试玩对手按手牌顺序交替入两队；不提前展示牌面。
        var turn = 0;
        foreach (var card in _enemy.Hand.Hand.ToArray())
        {
            var first = turn++ % 2;
            if (!_enemy.Hand.TryEnqueue(card.Key, first, Capacity(card), out _))
                _enemy.Hand.TryEnqueue(card.Key, 1 - first, Capacity(card), out _);
        }
        _scheduler = new(); _actions.Clear();
        AddPlanned(_player, "p", "e"); AddPlanned(_enemy, "e", "p");
        _planning = false; _subround = 0;
        Write($"第 {_round} 回合排招完成。点击推进一小轮。"); Render();
    }

    private void AddPlanned(Fighter fighter, string prefix, string opponent)
    {
        for (var q = 0; q < BoardHandState.QueueCount; q++)
        {
            var list = new List<QueueCombatAction>();
            foreach (var item in fighter.Hand.GetQueue(q))
            {
                var target = fighter == _player ? _queueTargets[q].GetValueOrDefault() : q;
                var id = $"{fighter.Id}:{_round}:{item.Card.Key.PlacementId}";
                _actions.Add(id, new(fighter, item.Card, q, target, id));
                list.Add(new(id, Kind(item.Card), opponent + target));
            }
            _scheduler!.AddQueue(prefix + q, fighter.Id, list);
        }
    }

    public void AdvanceSubround()
    {
        if (_planning || _finished || _scheduler is null) return;
        _subround++;
        // 同速及各队速度仍待正式设计；试玩固定交错，保证顺序清楚。
        _scheduler.BeginSubround([new("p0", QueueSpeed(true, 0), 0), new("e0", QueueSpeed(false, 0), 0),
            new("p1", QueueSpeed(true, 1), 0), new("e1", QueueSpeed(false, 1), 0)]);
        Write($"—— 第 {_round} 回合 · 小轮 {_subround} ——");
        while (_scheduler.Next() is { } step)
        {
            ResolveStep(step);
            if (CheckEnd()) { Render(); return; }
        }
        if (new[] { "p0", "p1", "e0", "e1" }.All(id => _scheduler.GetActions(id).Count == 0)) EndRound();
        Render();
    }

    private CombatEffectiveActionTransaction.ActionRequest Request(Planned action) => new(action.Owner.Id, action.Id,
        new(action.Card.Skill.energy_cost, action.Card.Skill.mana_cost));

    private void ResolveStep(QueueCombatStep step)
    {
        var actor = _actions[step.Actor.Id];
        var defender = step.Defender is null ? null : _actions[step.Defender.Id];
        var enemy = actor.Owner == _player ? _enemy : _player;
        var status = _payment.Prepare("trial-event-" + ++_event, Request(actor), defender is null ? null : Request(defender), out var preview);
        if (status != CombatEffectiveActionTransaction.Status.Ready || preview is null) throw new InvalidOperationException("费用检查失败：" + status);
        var actorReady = preview.FirstEligibility == CombatEffectiveActionTransaction.Eligibility.Ready;
        var defenderReady = preview.SecondEligibility != CombatEffectiveActionTransaction.Eligibility.InsufficientResources;
        if (!actorReady)
        {
            Commit(preview, CombatEffectiveActionTransaction.Participation.None,
                defender is null ? null : CombatEffectiveActionTransaction.Participation.None);
            _scheduler!.Complete(step, new(false, QueueClashWinner.None, false, false, true, false, true, true));
            Discard(actor);
            Write($"{actor.Owner.Id}的{actor.Card.Skill.skill_name}资源不足，招式失效。");
            PresentExchange($"{actor.Owner.Id} · {actor.Card.Skill.skill_name}", "资源不足，招式失效");
            return;
        }
        if (defender is not null && !defenderReady)
        {
            Commit(preview, CombatEffectiveActionTransaction.Participation.SingleAction, CombatEffectiveActionTransaction.Participation.None);
            var unopposed = _scheduler!.RemoveIneligibleDefender(step); Discard(defender);
            Write($"{defender.Owner.Id}的{defender.Card.Skill.skill_name}资源不足，招式失效。");
            Hit(actor, enemy); _scheduler.Complete(unopposed, new(true, QueueClashWinner.None, true, false, true, false, true, true));
            Discard(actor); return;
        }
        if (step.Kind == QueueStepKind.Operation)
        {
            Commit(preview, CombatEffectiveActionTransaction.Participation.SingleAction, null);
            _scheduler!.Complete(step, new(true, QueueClashWinner.None, false, false, true, false, true, true));
            ApplyQi(actor); Discard(actor); return;
        }
        if (defender is null)
        {
            Commit(preview, CombatEffectiveActionTransaction.Participation.SingleAction, null);
            Hit(actor, enemy); _scheduler!.Complete(step, new(true, QueueClashWinner.None, true, false, true, false, true, true));
            Discard(actor); return;
        }
        Commit(preview, CombatEffectiveActionTransaction.Participation.Clash, CombatEffectiveActionTransaction.Participation.Clash);
        var firstRoll = Roll(actor); var secondRoll = Roll(defender);
        var clash = CombatClashOutcome.Attack(Style(actor.Card), firstRoll, Style(defender.Card), secondRoll);
        Write($"{actor.Owner.Id} {actor.Card.Skill.skill_name} {firstRoll}  对  {defender.Owner.Id} {defender.Card.Skill.skill_name} {secondRoll}");
        var winner = clash.FirstWon ? QueueClashWinner.Actor : clash.SecondWon ? QueueClashWinner.Defender : QueueClashWinner.Tie;
        string result;
        if (clash.FirstHits) result = $"{defender.Owner.Id}气血 −{Hit(actor, defender.Owner)}";
        else if (clash.SecondHits) result = $"{actor.Owner.Id}气血 −{Hit(defender, actor.Owner)}";
        else if (clash.FirstRetained || clash.SecondRetained)
        {
            Write("近招破术，移到自身队尾；这次没有命中。");
            result = $"{(clash.FirstRetained ? actor.Owner.Id : defender.Owner.Id)}破术 · 近招移至队尾";
        }
        else
        {
            Write("灵机相同，双方招式结束。");
            result = "灵机相同，双方招式结束";
        }
        var left = actor.Owner == _player
            ? $"{actor.Card.Skill.skill_name} {firstRoll}" : $"{defender.Card.Skill.skill_name} {secondRoll}";
        var right = actor.Owner == _player
            ? $"{defender.Card.Skill.skill_name} {secondRoll}" : $"{actor.Card.Skill.skill_name} {firstRoll}";
        PresentExchange($"{left}　对　{right}", result);
        _scheduler!.Complete(step, new(true, winner, clash.FirstHits, clash.SecondHits,
            !clash.FirstRetained, !clash.SecondRetained, true, true));
        if (!clash.FirstRetained) Discard(actor);
        if (!clash.SecondRetained) Discard(defender);
        SyncQueue(actor.Owner, actor.Queue, step.QueueId);
        SyncQueue(defender.Owner, defender.Queue, step.TargetQueueId!);
    }

    private void Commit(CombatEffectiveActionTransaction.Preview preview,
        CombatEffectiveActionTransaction.Participation first, CombatEffectiveActionTransaction.Participation? second)
    {
        var status = _payment.Commit(preview, first, second, out var receipt);
        if (status != CombatEffectiveActionTransaction.Status.Committed) throw new InvalidOperationException("费用结算失败：" + status);
        if (receipt is null) throw new InvalidOperationException("缺少费用收据。");
        ShowCharge(receipt.First);
        if (receipt.Second is { } other) ShowCharge(other);
    }

    private void ShowCharge(CombatEffectiveActionTransaction.ActionReceipt charge)
    {
        if (charge.Participation == CombatEffectiveActionTransaction.Participation.None) return;
        var name = _actions.TryGetValue(charge.Action.ActionId, out var action) ? action.Card.Skill.skill_name : "招式";
        if (charge.PreviouslyPaid) Write($"{charge.Action.ParticipantId}的{name}已付费，本次不重复扣除。");
        else Write($"{charge.Action.ParticipantId}的{name}支付：体力 {charge.Before.Stamina - charge.After.Stamina}，法力 {charge.Before.Mana - charge.After.Mana}。");
    }

    private int Roll(Planned action)
    {
        var stats = action.Owner.Stats; var ranged = action.Card.SourceKey == Jing;
        if (!CombatLingjiCalculator.TryRoll(ranged ? stats.intelligence : stats.physique,
            ranged ? stats.magic_proficiency : stats.body_proficiency, () => _random.Next(1, 7), out var result))
            throw new InvalidOperationException("灵机计算失败。");
        return result.Value;
    }

    private int Hit(Planned action, Fighter target)
    {
        var amount = Damage(action.Card, action.Owner);
        target.Stats.current_health = Math.Max(0, target.Stats.current_health - amount);
        Write($"{action.Owner.Id}的{action.Card.Skill.skill_name}命中，{target.Id}气血 −{amount}。");
        PresentExchange($"{action.Owner.Id} · {action.Card.Skill.skill_name}", $"{target.Id}气血 −{amount}");
        return amount;
    }

    private static int QiGain(ActiveSkillData skill) => skill.special_effects
        .Where(effect => effect.GetValueOrDefault("effect", "").AsString() == "gain_qi")
        .Sum(effect => effect.GetValueOrDefault("count", 0).AsInt32());

    private void ApplyQi(Planned action)
    {
        var owner = action.Owner;
        var gain = QiGain(action.Card.Skill);
        owner.Qi += gain;
        _payment.TryGetBalance(owner.Id, out var before);
        if (owner.Stats.max_mana > 0 && owner.Qi >= 2 * owner.Stats.max_mana)
        {
            owner.Qi = 0; _payment.SetBalance(owner.Id, before with { Mana = owner.Stats.max_mana });
            Write($"{owner.Id}施展聚炁，完成周天，法力回满。");
            PresentExchange($"{owner.Id} · 聚炁", "完成周天，法力回满");
        }
        else
        {
            Write($"{owner.Id}施展聚炁，获得{gain}炁。");
            PresentExchange($"{owner.Id} · 聚炁", $"获得炁 {gain}");
        }
    }

    private void Discard(Planned action)
    {
        if (!action.Owner.Hand.TryDiscardQueued(action.Card.Key)) throw new InvalidOperationException("实体牌不在队列。");
    }

    private void SyncQueue(Fighter owner, int index, string queueId)
    {
        var desired = _scheduler!.GetActions(queueId);
        for (var i = 0; i < desired.Count; i++)
            if (!owner.Hand.TryReorder(index, _actions[desired[i].Id].Card.Key, i))
                throw new InvalidOperationException("行动队列与实体牌顺序不一致。");
    }

    private bool CheckEnd()
    {
        if (_player.Stats.current_health > 0 && _enemy.Stats.current_health > 0) return false;
        _finished = true; _planning = false;
        var outcome = _player.Stats.current_health <= 0 && _enemy.Stats.current_health <= 0 ? "双方同时倒下"
            : _enemy.Stats.current_health <= 0 ? "我方获胜" : "对手获胜";
        Write($"试剑结束：{outcome}。");
        PresentExchange(outcome, $"第 {_round} 回合 · 试剑结束");
        return true;
    }

    private void EndRound()
    {
        _round++; _planning = true; _scheduler = null;
        Array.Clear(_queueTargets);
        foreach (var fighter in new[] { _player, _enemy })
        {
            _payment.TryGetBalance(fighter.Id, out var balance);
            _payment.SetBalance(fighter.Id, balance with { Stamina = Math.Min(6, balance.Stamina + 2) });
            fighter.Hand.DrawForRound(_round);
        }
        Write($"进入第 {_round} 回合：各自恢复2体力（上限6），补2张牌，未用手牌保留。");
    }
}
