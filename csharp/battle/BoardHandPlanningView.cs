using Godot;

/// <summary>Only information intentionally visible about an enemy queue.</summary>
public sealed record BoardPublicEnemyQueue(string Id, string Name, int Speed);

/// <summary>A submitted queue preserves its card order, capacity costs, and selected target.</summary>
public sealed record BoardHandQueuePlan(int QueueIndex, string TargetQueueId, IReadOnlyList<BoardQueuedCard> Cards);
public sealed record BoardHandPlan(IReadOnlyList<BoardHandQueuePlan> Queues);

/// <summary>Independent planning UI. It does not charge resources or start a battle.</summary>
public partial class BoardHandPlanningView : Control
{
    private BoardHandState? _state;
    private Func<BoardCard, int?>? _capacityCost;
    private BoardPublicEnemyQueue[] _enemies = [];
    private readonly string?[] _targets = new string?[BoardHandState.QueueCount];
    private readonly Button[] _queueButtons = new Button[BoardHandState.QueueCount];
    private readonly Label[] _queueUsage = new Label[BoardHandState.QueueCount];
    private readonly OptionButton[] _targetOptions = new OptionButton[BoardHandState.QueueCount];
    private readonly VBoxContainer[] _queueRows = new VBoxContainer[BoardHandState.QueueCount];
    private VBoxContainer _handRows = null!;
    private Label _resources = null!, _notice = null!;
    private Button _confirm = null!;
    private int _selectedQueue;
    private int _stamina, _maxStamina, _mana, _maxMana;
    private bool _confirmed;

    public event Action<BoardHandPlan>? PlanConfirmed;
    public bool IsConfirmed => _confirmed;

    /// <param name="capacityCost">Explicit per-card queue cost; null disables planning.</param>
    public void Configure(BoardHandState state, IReadOnlyList<BoardPublicEnemyQueue> enemies,
        int stamina, int maxStamina, int mana, int maxMana, Func<BoardCard, int?>? capacityCost)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(enemies);
        if (enemies.Any(enemy => enemy is null || string.IsNullOrWhiteSpace(enemy.Id)
                || string.IsNullOrWhiteSpace(enemy.Name))
            || enemies.Select(enemy => enemy.Id).Distinct(StringComparer.Ordinal).Count() != enemies.Count)
            throw new ArgumentException("敌方队列信息无效或编号重复。", nameof(enemies));
        _state = state;
        _enemies = enemies.ToArray();
        _capacityCost = capacityCost;
        Array.Clear(_targets);
        _selectedQueue = 0;
        _confirmed = false;
        _stamina = stamina;
        _maxStamina = maxStamina;
        _mana = mana;
        _maxMana = maxMana;
        if (IsNodeReady()) Refresh();
    }

    public void UpdateResources(int stamina, int maxStamina, int mana, int maxMana)
    {
        _stamina = stamina; _maxStamina = maxStamina;
        _mana = mana; _maxMana = maxMana;
        if (IsNodeReady()) Refresh();
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var background = new ColorRect { Name = "Backdrop", Color = new Color("29312e"), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(background); background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var margin = new MarginContainer { Name = "Content" };
        margin.AddThemeConstantOverride("margin_left", 34); margin.AddThemeConstantOverride("margin_right", 34);
        margin.AddThemeConstantOverride("margin_top", 24); margin.AddThemeConstantOverride("margin_bottom", 24);
        AddChild(margin); margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var root = new VBoxContainer { Name = "Layout" };
        root.AddThemeConstantOverride("separation", 14); margin.AddChild(root);
        var title = Text("筹谋出手", 28, "Title"); root.AddChild(title);
        _resources = Text("", 20, "Resources"); root.AddChild(_resources);
        var hint = Text("选择队列后点击手牌，点击队列中的招式可撤回。", 17, "Hint"); root.AddChild(hint);

        var columns = new HBoxContainer { Name = "Columns", SizeFlagsVertical = SizeFlags.ExpandFill };
        columns.AddThemeConstantOverride("separation", 16); root.AddChild(columns);
        var handPanel = Panel("HandPanel"); handPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        columns.AddChild(handPanel);
        var handBody = new VBoxContainer { Name = "Body" }; handPanel.AddChild(handBody);
        handBody.AddChild(Text("手牌", 22, "HandTitle"));
        var handScroll = new ScrollContainer { Name = "HandScroll", SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        handBody.AddChild(handScroll);
        _handRows = new VBoxContainer { Name = "HandCards", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _handRows.AddThemeConstantOverride("separation", 7); handScroll.AddChild(_handRows);

        for (var i = 0; i < BoardHandState.QueueCount; i++)
        {
            var index = i;
            var panel = Panel("QueuePanel" + i); panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            columns.AddChild(panel);
            var body = new VBoxContainer { Name = "Body" }; panel.AddChild(body);
            _queueButtons[i] = MakeButton("出手队列 " + (i + 1), "SelectQueue" + i);
            _queueButtons[i].ToggleMode = true; _queueButtons[i].Pressed += () => SelectQueue(index);
            body.AddChild(_queueButtons[i]);
            _queueUsage[i] = Text("", 18, "Usage" + i); body.AddChild(_queueUsage[i]);
            body.AddChild(Text("交互目标", 16, "TargetLabel" + i));
            var target = new OptionButton { Name = "Target" + i, CustomMinimumSize = new(0, 42) };
            target.AddThemeFontSizeOverride("font_size", 18);
            target.ItemSelected += choice => SelectTarget(index, (int)choice);
            body.AddChild(target); _targetOptions[i] = target;
            var queueScroll = new ScrollContainer { Name = "QueueScroll", SizeFlagsVertical = SizeFlags.ExpandFill,
                HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
            body.AddChild(queueScroll);
            _queueRows[i] = new VBoxContainer { Name = "QueueCards", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            _queueRows[i].AddThemeConstantOverride("separation", 7); queueScroll.AddChild(_queueRows[i]);
        }

        _notice = Text("", 17, "Notice"); root.AddChild(_notice);
        _confirm = MakeButton("确认出手", "Confirm"); _confirm.CustomMinimumSize = new(180, 50);
        _confirm.SizeFlagsHorizontal = SizeFlags.ShrinkEnd; _confirm.Pressed += Confirm; root.AddChild(_confirm);
        Refresh();
    }

    private void SelectQueue(int index)
    {
        if (_confirmed || _state is null) return;
        _selectedQueue = index;
        _notice.Text = "";
        Refresh();
    }

    private void SelectTarget(int queueIndex, int choice)
    {
        if (_confirmed || _state is null || choice < 0 || choice > _enemies.Length) return;
        _targets[queueIndex] = choice == 0 ? null : _enemies[choice - 1].Id;
        _notice.Text = "";
        Refresh();
    }

    private void AddCard(BoardCard card)
    {
        if (_confirmed || _state is null) return;
        var cost = _capacityCost?.Invoke(card);
        if (cost is null || cost < 0) { _notice.Text = "该招式尚未配置有效队列点数。"; return; }
        if (!_state.TryEnqueue(card.Key, _selectedQueue, cost.Value, out var reason))
            _notice.Text = reason;
        else _notice.Text = "";
        Refresh();
    }

    private void Withdraw(BoardCardKey key)
    {
        if (_confirmed || _state is null) return;
        var queueIndex = Enumerable.Range(0, BoardHandState.QueueCount)
            .FirstOrDefault(index => _state.GetQueue(index).Any(item => item.Card.Key == key), -1);
        if (!_state.TryWithdraw(key)) _notice.Text = "该招式已不在队列中。";
        else
        {
            if (queueIndex >= 0 && _state.GetQueue(queueIndex).Count == 0) _targets[queueIndex] = null;
            _notice.Text = "";
        }
        Refresh();
    }

    private void Confirm()
    {
        if (_confirmed || _state is null) return;
        var plans = new List<BoardHandQueuePlan>();
        for (var i = 0; i < BoardHandState.QueueCount; i++)
        {
            var cards = _state.GetQueue(i);
            if (cards.Count == 0) continue;
            if (_targets[i] is not { } target || !_enemies.Any(enemy => enemy.Id == target))
            { _notice.Text = $"请为出手队列 {i + 1} 选择交互目标。"; return; }
            if (_capacityCost is null || cards.Any(item => _capacityCost(item.Card) is not int cost
                    || cost < 0 || cost != item.CapacityCost))
            { _notice.Text = "招式的队列点数未配置或已变化，请重新安排。"; return; }
            plans.Add(new(i, target, Array.AsReadOnly(cards.ToArray())));
        }
        if (plans.Count == 0) { _notice.Text = "请先安排招式。"; return; }
        _confirmed = true;
        _notice.Text = "出手安排已确认。";
        Refresh();
        PlanConfirmed?.Invoke(new BoardHandPlan(Array.AsReadOnly(plans.ToArray())));
    }

    private void Refresh()
    {
        _resources.Text = $"体力  {_stamina}/{_maxStamina}     法力  {_mana}/{_maxMana}";
        Clear(_handRows);
        if (_state is null)
        {
            _notice.Text = "尚未准备手牌。"; _confirm.Disabled = true;
            foreach (var button in _queueButtons) button.Disabled = true;
            foreach (var target in _targetOptions) target.Disabled = true;
            return;
        }
        if (_capacityCost is null) _notice.Text = "尚未配置队列点数，暂时无法安排招式。";
        else if (!_confirmed && _state.Hand.Any(card => _capacityCost(card) is not int cost || cost < 0))
            _notice.Text = "部分招式尚未配置有效队列点数。";
        else if (_notice.Text is "尚未准备手牌。" or "尚未配置队列点数，暂时无法安排招式。" or "部分招式尚未配置有效队列点数。")
            _notice.Text = "";

        var handIndex = 0;
        foreach (var card in _state.Hand)
        {
            var cost = _capacityCost?.Invoke(card);
            var configured = cost is >= 0;
            var button = MakeButton(configured ? $"{card.Skill.skill_name}    点数 {cost}" : $"{card.Skill.skill_name}    点数未配置", "HandCard" + handIndex++);
            button.Alignment = HorizontalAlignment.Left;
            button.Disabled = _confirmed || !configured || (long)_state.GetQueueUsed(_selectedQueue) + cost.GetValueOrDefault() > _state.QueueCapacity;
            button.TooltipText = !configured ? "该招式尚未配置有效队列点数。" : button.Disabled && !_confirmed ? "当前队列剩余点数不足。" : "加入当前队列";
            button.Pressed += () => AddCard(card); _handRows.AddChild(button);
        }
        if (_state.Hand.Count == 0) _handRows.AddChild(Text("暂无手牌", 17, "EmptyHand"));

        for (var i = 0; i < BoardHandState.QueueCount; i++)
        {
            _queueButtons[i].Disabled = _confirmed;
            _queueButtons[i].ButtonPressed = _selectedQueue == i;
            _queueUsage[i].Text = $"点数  {_state.GetQueueUsed(i)}/{_state.QueueCapacity}";
            var option = _targetOptions[i];
            option.Clear(); option.AddItem("选择敌方队列");
            foreach (var enemy in _enemies) option.AddItem($"{enemy.Name}  ·  行速 {enemy.Speed}");
            var selected = Array.FindIndex(_enemies, enemy => enemy.Id == _targets[i]);
            option.Select(selected + 1);
            option.Disabled = _confirmed || _state.GetQueue(i).Count == 0 || _enemies.Length == 0;
            Clear(_queueRows[i]);
            var index = 0;
            foreach (var queued in _state.GetQueue(i))
            {
                var button = MakeButton($"{index + 1}.  {queued.Card.Skill.skill_name}    点数 {queued.CapacityCost}", "QueuedCard" + index++);
                button.Alignment = HorizontalAlignment.Left; button.Disabled = _confirmed;
                button.TooltipText = "撤回手牌";
                var key = queued.Card.Key; button.Pressed += () => Withdraw(key); _queueRows[i].AddChild(button);
            }
            if (_state.GetQueue(i).Count == 0) _queueRows[i].AddChild(Text("尚未安排招式", 17, "EmptyQueue"));
        }
        _confirm.Disabled = _confirmed || _capacityCost is null;
    }

    private static Label Text(string value, int size, string name)
    {
        var label = new Label { Name = name, Text = value, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", BattleBoardSkin.PaperText);
        return label;
    }

    private static Button MakeButton(string text, string name)
    {
        var button = new Button { Name = name, Text = text, CustomMinimumSize = new(0, 46), FocusMode = FocusModeEnum.All };
        BattleBoardSkin.Skin(button);
        return button;
    }

    private static PanelContainer Panel(string name)
    {
        var panel = new PanelContainer { Name = name, SizeFlagsVertical = SizeFlags.ExpandFill };
        var style = new StyleBoxFlat { BgColor = new Color("3b433b"), BorderColor = new Color("92866e"),
            BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
            ContentMarginLeft = 14, ContentMarginRight = 14, ContentMarginTop = 14, ContentMarginBottom = 14 };
        panel.AddThemeStyleboxOverride("panel", style);
        return panel;
    }

    private static void Clear(Node node)
    {
        foreach (var child in node.GetChildren()) { node.RemoveChild(child); child.QueueFree(); }
    }
}
