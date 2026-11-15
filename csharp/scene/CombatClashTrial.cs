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
    private readonly Dictionary<BoardCardKey, int> _targets = [];
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
    private Label _status = null!;
    private Label _playerQueues = null!;
    private Label _selection = null!;
    private Label _phase = null!;
    private HBoxContainer _handBox = null!;
    private RichTextLabel _log = null!;
    private Button _commit = null!;
    private Button _advance = null!;
    private readonly List<Button> _planningButtons = [];

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
        BuildUi();
        Write("第一回合：选择手牌，再选择我方队列及敌方队列。敌方招式不预先显示。");
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

    private void BuildUi()
    {
        var back = new ColorRect { Color = new Color("171c1c"), MouseFilter = MouseFilterEnum.Ignore };
        back.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(back);
        var margin = new MarginContainer(); margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 28); margin.AddThemeConstantOverride("margin_right", 28);
        margin.AddThemeConstantOverride("margin_top", 18); margin.AddThemeConstantOverride("margin_bottom", 18); AddChild(margin);
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 8); margin.AddChild(column);
        var title = new Label { Text = "灵机试剑  ·  独立试玩", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 28); title.AddThemeColorOverride("font_color", new Color("e9d5a2")); column.AddChild(title);
        var note = new Label { Text = "固定样例：双方云息五层，5×2演示盘  ·  速度仅供演示  ·  仅基础伤害；暂无定力、失神、击退、守御、心法、法宝与暴击", HorizontalAlignment = HorizontalAlignment.Center };
        note.AddThemeFontSizeOverride("font_size", 14); note.AddThemeColorOverride("font_color", new Color("b2b7aa")); column.AddChild(note);
        _status = new Label { HorizontalAlignment = HorizontalAlignment.Center }; _status.AddThemeFontSizeOverride("font_size", 19); column.AddChild(_status);
        var columns = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill }; columns.AddThemeConstantOverride("separation", 18); column.AddChild(columns);
        var left = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsStretchRatio = 1.05f }; columns.AddChild(left);
        _phase = new Label(); _phase.AddThemeFontSizeOverride("font_size", 20); left.AddChild(_phase);
        _playerQueues = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(0, 104) }; left.AddChild(_playerQueues);
        var hide = new Label { Text = "对手一队  速度3：招式隐藏\n对手二队  速度1：招式隐藏", CustomMinimumSize = new Vector2(0, 62) };
        hide.AddThemeColorOverride("font_color", new Color("a3aaa2")); left.AddChild(hide);
        _selection = new Label { Text = "先点击下方手牌，再指定队列和目标。", AutowrapMode = TextServer.AutowrapMode.WordSmart }; left.AddChild(_selection);
        var assign = new GridContainer { Columns = 2 }; left.AddChild(assign);
        for (var queue = 0; queue < 2; queue++) for (var target = 0; target < 2; target++)
        {
            var q = queue; var t = target;
            var button = MakeButton($"我方{q + 1}队  →  对手{t + 1}队");
            button.Pressed += () => { if (_selected is { } key) QueuePlayerCard(key, q, t); };
            assign.AddChild(button); _planningButtons.Add(button);
        }
        var remove = new HBoxContainer(); left.AddChild(remove);
        for (var queue = 0; queue < 2; queue++)
        {
            var q = queue; var button = MakeButton($"撤回{q + 1}队末招");
            button.Pressed += () => WithdrawLast(q); remove.AddChild(button); _planningButtons.Add(button);
        }
        var actions = new HBoxContainer(); left.AddChild(actions);
        _commit = MakeButton(""); AddButtonText(_commit, "确定排招"); _commit.Name = "CommitPlan"; _commit.Pressed += CommitPlan; actions.AddChild(_commit);
        _advance = MakeButton("推进一小轮"); _advance.Name = "AdvanceSubround"; _advance.Pressed += AdvanceSubround; actions.AddChild(_advance);
        var right = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsStretchRatio = 1f }; columns.AddChild(right);
        var logTitle = new Label { Text = "交锋记录" }; logTitle.AddThemeFontSizeOverride("font_size", 20); right.AddChild(logTitle);
        _log = new RichTextLabel { SizeFlagsVertical = SizeFlags.ExpandFill, ScrollFollowing = true,
            FitContent = false, SelectionEnabled = true, CustomMinimumSize = new Vector2(0, 240) }; right.AddChild(_log);
        var handTitle = new Label { Text = "手牌  ·  点击选择" }; handTitle.AddThemeFontSizeOverride("font_size", 19); column.AddChild(handTitle);
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 130), HorizontalScrollMode = ScrollContainer.ScrollMode.Auto,
            VerticalScrollMode = ScrollContainer.ScrollMode.Disabled }; column.AddChild(scroll);
        _handBox = new HBoxContainer(); _handBox.AddThemeConstantOverride("separation", 8); scroll.AddChild(_handBox);
        var rules = new Label { Text = "每队容量5；应敌、聚炁占1，痛击、惊蛰占2。首拼双方各付一次，同一招后续不重复。聚炁得炁2，满2炁回满1法力。",
            AutowrapMode = TextServer.AutowrapMode.WordSmart }; rules.AddThemeFontSizeOverride("font_size", 13); rules.AddThemeColorOverride("font_color", new Color("aeb7ad")); column.AddChild(rules);
    }

    private static Button MakeButton(string text)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(150, 40) };
        BattleBoardSkin.Skin(button);
        button.AddThemeFontSizeOverride("font_size", 17); return button;
    }

    private static void AddButtonText(Button button, string text)
    {
        // 独立文本不受按钮禁用态的主题字体影响，未用手牌仍可阅读。
        var label = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", 17);
        label.AddThemeColorOverride("font_color", new Color("38342e"));
        button.AddChild(label); label.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    }

    private void Write(string text) { _log.AppendText(text + "\n"); }

    private void Render()
    {
        _payment.TryGetBalance(_player.Id, out var p); _payment.TryGetBalance(_enemy.Id, out var e);
        _status.Text = $"我方  气血 {_player.Stats.current_health}/303  体力 {p.Stamina}/6  法力 {p.Mana}/1  炁 {_player.Qi}     "
            + $"对手  气血 {_enemy.Stats.current_health}/303  体力 {e.Stamina}/6  法力 {e.Mana}/1  炁 {_enemy.Qi}";
        _phase.Text = _finished ? "试剑结束" : _planning ? $"第 {_round} 回合 · 排招" : $"第 {_round} 回合 · 第 {_subround + 1} 小轮";
        var lines = new List<string>();
        for (var q = 0; q < 2; q++)
        {
            var queued = _player.Hand.GetQueue(q);
            lines.Add($"我方{q + 1}队  速度{(q == 0 ? 4 : 2)}  {_player.Hand.GetQueueUsed(q)}/5： " + (queued.Count == 0 ? "空" : string.Join(" → ", queued.Select(item =>
                item.Card.Skill.skill_name + (_targets.TryGetValue(item.Card.Key, out var target) && item.Card.SourceKey != Qi ? $"〔敌{target + 1}〕" : "")))));
        }
        _playerQueues.Text = string.Join("\n\n", lines);
        _selection.Text = _selected is { } id ? "已选：" + (_player.Hand.Hand.FirstOrDefault(card => card.Key == id)?.Skill.skill_name ?? "") : "先点击手牌，再指定队列和目标。";
        foreach (var child in _handBox.GetChildren()) { _handBox.RemoveChild(child); child.QueueFree(); }
        foreach (var card in _player.Hand.Hand)
        {
            var key = card.Key; var skill = card.Skill;
            var text = $"{skill.skill_name}\n占 {Capacity(card)}  体 {skill.energy_cost}  法 {skill.mana_cost}\n" +
                (card.SourceKey == Qi ? "运转 · 聚炁2" : $"灵机 {LingjiRange(card, _player)}\n基础伤害 {Damage(card, _player)}");
            var button = MakeButton(""); AddButtonText(button, text); button.Name = "Hand_" + key.PlacementId;
            button.CustomMinimumSize = new Vector2(150, 110); button.Disabled = !_planning || _finished;
            button.ToggleMode = true; button.ButtonPressed = _selected == key;
            button.Pressed += () => { _selected = key; Render(); }; _handBox.AddChild(button);
        }
        foreach (var button in _planningButtons) button.Disabled = !_planning || _finished;
        _commit.Disabled = !_planning || _finished; _advance.Disabled = _planning || _finished;
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

    public bool QueuePlayerCard(BoardCardKey key, int queue, int target)
    {
        if (!_planning || _finished || target is < 0 or > 1) return false;
        var card = _player.Hand.Hand.FirstOrDefault(item => item.Key == key);
        if (card is null) return false;
        if (queue is < 0 or > 1) return false;
        var existingTarget = _player.Hand.GetQueue(queue)
            .Where(item => item.Card.SourceKey != Qi && _targets.ContainsKey(item.Card.Key))
            .Select(item => (int?)_targets[item.Card.Key]).FirstOrDefault();
        if (card.SourceKey != Qi && existingTarget is { } locked && locked != target)
        {
            Write($"我方{queue + 1}队已指定对手{locked + 1}队，请为同队招式选择相同目标。");
            Render(); return false;
        }
        if (!_player.Hand.TryEnqueue(key, queue, Capacity(card), out var reason)) { Write(reason); Render(); return false; }
        _targets[key] = target; _selected = null; Render(); return true;
    }

    public void WithdrawLast(int queue)
    {
        if (!_planning || _finished || queue is < 0 or > 1) return;
        var last = _player.Hand.GetQueue(queue).LastOrDefault();
        if (last is not null && _player.Hand.TryWithdraw(last.Card.Key)) _targets.Remove(last.Card.Key);
        Render();
    }

    public void CommitPlan()
    {
        if (!_planning || _finished) return;
        _selected = null;
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
        for (var q = 0; q < 2; q++)
        {
            var list = new List<QueueCombatAction>();
            foreach (var item in fighter.Hand.GetQueue(q))
            {
                var target = fighter == _player ? _targets.GetValueOrDefault(item.Card.Key) : q;
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
        _scheduler.BeginSubround([new("p0", 4, 0), new("e0", 3, 0), new("p1", 2, 0), new("e1", 1, 0)]);
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
            Discard(actor); Write($"{actor.Owner.Id}的{actor.Card.Skill.skill_name}资源不足，招式失效。"); return;
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
            ApplyQi(actor.Owner); Discard(actor); return;
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
        if (clash.FirstHits) Hit(actor, defender.Owner);
        else if (clash.SecondHits) Hit(defender, actor.Owner);
        else if (clash.FirstRetained || clash.SecondRetained) Write("近招破术，移到自身队尾；这次没有命中。");
        else Write("灵机相同，双方招式结束。");
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

    private void Hit(Planned action, Fighter target)
    {
        var amount = Damage(action.Card, action.Owner);
        target.Stats.current_health = Math.Max(0, target.Stats.current_health - amount);
        Write($"{action.Owner.Id}的{action.Card.Skill.skill_name}命中，{target.Id}气血 −{amount}。");
    }

    private void ApplyQi(Fighter owner)
    {
        var skill = Load(Qi);
        var gain = skill.special_effects.Where(effect => effect.GetValueOrDefault("effect", "").AsString() == "gain_qi")
            .Sum(effect => effect.GetValueOrDefault("count", 0).AsInt32());
        owner.Qi += gain;
        _payment.TryGetBalance(owner.Id, out var before);
        if (owner.Stats.max_mana > 0 && owner.Qi >= 2 * owner.Stats.max_mana)
        {
            owner.Qi = 0; _payment.SetBalance(owner.Id, before with { Mana = owner.Stats.max_mana });
            Write($"{owner.Id}施展聚炁，完成周天，法力回满。");
        }
        else Write($"{owner.Id}施展聚炁，获得{gain}炁。");
    }

    private void Discard(Planned action)
    {
        if (!action.Owner.Hand.TryDiscardQueued(action.Card.Key)) throw new InvalidOperationException("实体牌不在队列。");
        _targets.Remove(action.Card.Key);
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
        Write(_player.Stats.current_health <= 0 && _enemy.Stats.current_health <= 0 ? "双方同时倒下。"
            : _enemy.Stats.current_health <= 0 ? "试剑结束：我方获胜。" : "试剑结束：对手获胜。");
        return true;
    }

    private void EndRound()
    {
        _round++; _planning = true; _scheduler = null;
        foreach (var fighter in new[] { _player, _enemy })
        {
            _payment.TryGetBalance(fighter.Id, out var balance);
            _payment.SetBalance(fighter.Id, balance with { Stamina = Math.Min(6, balance.Stamina + 2) });
            fighter.Hand.DrawForRound(_round);
        }
        Write($"进入第 {_round} 回合：各自恢复2体力（上限6），补2张牌，未用手牌保留。");
    }
}
