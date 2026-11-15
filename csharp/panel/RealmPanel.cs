using Godot;

/// <summary>境界总览只读取既有境界配置，不在界面内结算新数值。</summary>
public partial class RealmPanel : Control
{
    private static readonly string[] Stages =
    [
        "云息一层", "云息二层", "云息三层", "云息四层",
        "云息五层", "云息六层", "云息七层", "云息八层",
        "云息九层", "云息十层"
    ];

    private Character? _player;
    private VBoxContainer? _stageRail;
    private HBoxContainer? _majorTabs;
    private Label? _bonuses;
    private Label? _bonusHeading;
    private Button? _bonusToggle;
    private bool _showCumulativeBonuses;
    private RealmQiVisual? _qiVisual;
    private Label? _currentRealm;
    private Label? _realmCaption;
    private Label? _nextRealm;
    private Label? _visualHint;
    private Button? _skipAnimation;
    private readonly List<Control> _presentationSurroundings = [];
    private VBoxContainer? _presentationLayer;
    private Label? _presentationPhase;
    private Tween? _presentationTween;
    private bool _isPresenting;
    private string _presentationResult = "";
    public bool IsPresenting => _isPresenting;
    private enum PendingPresentation { None, Breakthrough, Completion }
    private PendingPresentation _pendingPresentation;
    private Func<bool>? _presentationReadiness;
    public bool HasPendingPresentation => _pendingPresentation != PendingPresentation.None;
    public void SetPresentationReadiness(Func<bool> readiness) => _presentationReadiness = readiness;
    private string _observedRealm = "";
    private int _observedBreaths;
    private int _pendingBreathsFrom;
    private int _pendingBreathsTo;
    private string _pendingFrom = "";
    private string _pendingTo = "";
    private float _refreshElapsed;
    private int _previewStage = -1;
    public event Action? PresentationStarted;
    public event Action? Closed;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        ZIndex = 650;
        Build();
        VisibilityChanged += OnVisibilityChanged;
        Hide();
    }

    public override void _Process(double delta)
    {
        // 结算先于呈现；长行动和跨月结算结束后才播放，避免动画藏在过渡页下。
        if (HasPendingPresentation && !_isPresenting && (_presentationReadiness?.Invoke() ?? true))
        {
            var pending = _pendingPresentation;
            _pendingPresentation = PendingPresentation.None;
            if (pending == PendingPresentation.Breakthrough) PresentBreakthrough();
            else PresentBreaths();
        }
        if (!Visible || _isPresenting || _qiVisual?.IsTransitioning == true) return;
        _refreshElapsed += (float)delta;
        if (_refreshElapsed < .25f) return;
        _refreshElapsed = 0f;
        if (GodotObject.IsInstanceValid(_player)) RefreshVisual();
    }

    public override void _ExitTree()
    {
        ResetPresentation();
        UnbindPlayer();
    }

    private void UnbindPlayer()
    {
        if (!GodotObject.IsInstanceValid(_player)) return;
        _player!.BreakthroughCompleted -= OnBreakthrough;
        _player.YuanxiChanged -= OnYuanxiChanged;
    }

    public bool is_unlocked(Character? target) => GodotObject.IsInstanceValid(target) && Index(target!.cultivation_realm) >= 0;

    public void bind_player(Character target)
    {
        if (_player == target) return;
        UnbindPlayer();
        _pendingPresentation = PendingPresentation.None;
        _player = target;
        _observedRealm = target.cultivation_realm;
        _observedBreaths = target.yuanxi_count;
        target.BreakthroughCompleted += OnBreakthrough;
        target.YuanxiChanged += OnYuanxiChanged;
    }

    public void show_for(Character target)
    {
        if (!is_unlocked(target)) return;
        ResetPresentation();
        bind_player(target);
        _previewStage = -1;
        _qiVisual?.StopTransition();
        Rebuild();
        Refresh();
        Show();
        MoveToFront();
    }

    private void Build()
    {
        // 全屏暗宣纸；背景本身保持实体色，不在代码中再次调色或做透明遮罩。
        // 普通Panel不会像PanelContainer一样将背景/云眼/文字全部强制铺满重排。
        var paper = new Panel { MouseFilter = MouseFilterEnum.Stop, ClipContents = true };
        paper.SetAnchorsPreset(LayoutPreset.FullRect);
        paper.AddThemeStyleboxOverride("panel", Box(new Color(0, 0, 0, 0), new Color(0, 0, 0, 0), 0, 0));
        AddChild(paper);

        var background = Texture("res://textures/ui/realm_yunxi_paper_v6.png", TextureRect.StretchModeEnum.KeepAspectCovered);
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        paper.AddChild(background);

        // 同一片气由疏到密；六层以前不叠中心光球，成核后仍保留气的流动。
        var cloudEye = new Control { MouseFilter = MouseFilterEnum.Ignore };
        cloudEye.SetAnchorsPreset(LayoutPreset.FullRect);
        // 主视觉放在纸面正中，为左下加成和右上题字各留一角。
        cloudEye.AnchorLeft = .28f; cloudEye.AnchorTop = .14f; cloudEye.AnchorRight = .72f; cloudEye.AnchorBottom = .86f;
        cloudEye.OffsetLeft = 0; cloudEye.OffsetTop = 0; cloudEye.OffsetRight = 0; cloudEye.OffsetBottom = 0;
        paper.AddChild(cloudEye);

        _qiVisual = new RealmQiVisual();
        _qiVisual.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        cloudEye.AddChild(_qiVisual);
        _qiVisual.Resized += () => _qiVisual.PivotOffset = _qiVisual.Size * .5f;

        var root = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        root.OffsetLeft = 44; root.OffsetTop = 32; root.OffsetRight = -44; root.OffsetBottom = -38;
        root.AddThemeConstantOverride("separation", 16);
        paper.AddChild(root);

        var header = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        root.AddChild(header);
        header.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
        var close = new Button { Text = "关闭", CustomMinimumSize = new Vector2(78, 38), MouseFilter = MouseFilterEnum.Stop };
        ButtonStyle(close, false);
        close.Pressed += () => Hide();
        header.AddChild(close);
        var headerRule = Separator();
        root.AddChild(headerRule);
        _presentationSurroundings.Add(headerRule);

        // 右上题字、左下加成与中部云眼三者互不挤占。
        var content = new Control { SizeFlagsVertical = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        root.AddChild(content);
        var poem = new RealmPoemVisual();
        poem.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        poem.AnchorLeft = .73f; poem.AnchorTop = .015f; poem.AnchorRight = .985f; poem.AnchorBottom = .72f;
        poem.OffsetLeft = 0; poem.OffsetTop = 0; poem.OffsetRight = 0; poem.OffsetBottom = 0;
        content.AddChild(poem);

        var realmInfo = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore };
        realmInfo.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        realmInfo.AnchorLeft = .31f; realmInfo.AnchorTop = .76f; realmInfo.AnchorRight = .69f; realmInfo.AnchorBottom = .995f;
        realmInfo.OffsetLeft = 0; realmInfo.OffsetTop = 0; realmInfo.OffsetRight = 0; realmInfo.OffsetBottom = 0;
        realmInfo.AddThemeConstantOverride("separation", 8);
        content.AddChild(realmInfo);
        _realmCaption = Label("当前境界", 14, new Color("c1b797"));
        _currentRealm = Label("", 32, new Color("eee8d5"));
        _nextRealm = Label("", 16, new Color("c7c1ac"));
        _visualHint = Label("", 15, new Color("b9bba9"));
        foreach (var line in new[] { _realmCaption, _currentRealm, _nextRealm, _visualHint })
        {
            line.HorizontalAlignment = HorizontalAlignment.Center;
            realmInfo.AddChild(line);
        }
        _presentationLayer = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore, Visible = false };
        _presentationLayer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _presentationLayer.AnchorLeft = .28f; _presentationLayer.AnchorTop = .77f;
        _presentationLayer.AnchorRight = .72f; _presentationLayer.AnchorBottom = .98f;
        _presentationLayer.AddThemeConstantOverride("separation", 18);
        content.AddChild(_presentationLayer);
        _presentationPhase = Label("", 21, new Color("eee8d5"));
        _presentationPhase.HorizontalAlignment = HorizontalAlignment.Center;
        _presentationLayer.AddChild(_presentationPhase);
        _skipAnimation = new Button { Text = "跳过动画", Visible = false, MouseFilter = MouseFilterEnum.Stop };
        _skipAnimation.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
        _skipAnimation.AddThemeStyleboxOverride("hover", new StyleBoxEmpty());
        _skipAnimation.AddThemeColorOverride("font_color", new Color("c7c1ac"));
        _skipAnimation.Pressed += FinishPresentation;
        _presentationLayer.AddChild(_skipAnimation);

        var bonus = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        bonus.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        bonus.AnchorLeft = .04f; bonus.AnchorTop = .70f; bonus.AnchorRight = .28f; bonus.AnchorBottom = .96f;
        bonus.OffsetLeft = 0; bonus.OffsetTop = 0; bonus.OffsetRight = 0; bonus.OffsetBottom = 0;
        bonus.AddThemeConstantOverride("separation", 10);
        content.AddChild(bonus);
        bonus.AddChild(Separator());
        // 纸面底色复杂且偏暗，使用明亮的暖色文字保证加成清楚可读。
        var bonusHeader = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        bonusHeader.AddThemeConstantOverride("separation", 12);
        bonus.AddChild(bonusHeader);
        _bonusHeading = Label("本境界加成", 18, new Color(.95f, .84f, .48f));
        _bonusHeading.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        bonusHeader.AddChild(_bonusHeading);
        _bonusToggle = new Button { Text = "查看累计", MouseFilter = MouseFilterEnum.Stop };
        ButtonStyle(_bonusToggle, false);
        _bonusToggle.AddThemeFontSizeOverride("font_size", 14);
        _bonusToggle.Pressed += () => { if (_isPresenting) return; _showCumulativeBonuses = !_showCumulativeBonuses; Refresh(); };
        bonusHeader.AddChild(_bonusToggle);
        bonus.AddChild(Label("仅列境界贡献，成长倍率显示增加量", 14, new Color(.81f, .78f, .68f)));
        _bonuses = Label("", 17, new Color(.96f, .94f, .87f));
        _bonuses.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        bonus.AddChild(_bonuses);

        // 大境界与小境界都在纸面外；小境界标签水平书写，由下至上堆叠。
        // 容器横跨整个顶部，空白区域必须放行鼠标，否则会盖住右上角关闭按钮。
        _majorTabs = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore };
        _majorTabs.SetAnchorsPreset(LayoutPreset.TopWide);
        _majorTabs.OffsetTop = 12; _majorTabs.OffsetBottom = 64;
        _majorTabs.ZIndex = 20;
        AddChild(_majorTabs);

        _stageRail = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Stop };
        _stageRail.SetAnchorsPreset(LayoutPreset.FullRect);
        _stageRail.AnchorLeft = .12f; _stageRail.AnchorRight = .12f;
        // 避开左下角加成说明，并为云息十层境界签预留完整高度。
        _stageRail.AnchorTop = .10f; _stageRail.AnchorBottom = .68f;
        _stageRail.OffsetLeft = -205; _stageRail.OffsetTop = 0; _stageRail.OffsetRight = -15; _stageRail.OffsetBottom = 0;
        _stageRail.AddThemeConstantOverride("separation", 6);
        _stageRail.ZIndex = 20;
        AddChild(_stageRail);
        _presentationSurroundings.AddRange([poem, realmInfo, bonus, _majorTabs, _stageRail]);
    }

    private void Rebuild()
    {
        RebuildMajorTabs();
        if (_stageRail is null) return;
        foreach (var child in _stageRail.GetChildren()) child.QueueFree();
        var current = Index(_player?.cultivation_realm ?? "");
        if (current < 0 || current >= Stages.Length) return;

        // 显示已到达的全部层数，并在末尾保留下一层预览。
        for (var stage = 0; stage <= Math.Min(current + 1, Stages.Length - 1); stage++)
            AddStage(stage, stage == current);
    }

    private void RebuildMajorTabs()
    {
        if (_majorTabs is null) return;
        foreach (var child in _majorTabs.GetChildren()) child.QueueFree();
        // 当前只有云息；新大境界须待实际突破进入后才会追加，绝不预置灰色占位。
        if (_player?.cultivation_realm.StartsWith("云息") != true) return;
        var tab = new Button { Text = "云息", CustomMinimumSize = new Vector2(138, 48), MouseFilter = MouseFilterEnum.Ignore };
        ButtonStyle(tab, true);
        _majorTabs.AddChild(tab);
    }

    private void AddStage(int stage, bool current)
    {
        if (_stageRail is null) return;
        var button = new Button
        {
            Text = current ? $"· {Stages[stage]} ·" : Stages[stage],
            CustomMinimumSize = new Vector2(180, 40),
            Alignment = HorizontalAlignment.Center,
            MouseFilter = MouseFilterEnum.Stop
        };
        ButtonStyle(button, current);
        button.SelfModulate = new Color(1f, 1f, 1f, current ? 1f : .70f);
        button.Pressed += () => { if (_isPresenting) return; _previewStage = stage; Refresh(); };
        _stageRail.AddChild(button);
    }

    private void Refresh()
    {
        if (_player is null || _bonuses is null) return;
        var realm = _previewStage >= 0 ? Stages[_previewStage] : _player.cultivation_realm;
        var config = GetNodeOrNull<RealmManager>("/root/RealmManager")?.get_realm_config(realm);
        if (_bonusHeading is not null) _bonusHeading.Text = _showCumulativeBonuses ? "累计加成" : "本境界加成";
        if (_bonusToggle is not null) _bonusToggle.Text = _showCumulativeBonuses ? "查看本境界" : "查看累计";
        _bonuses.Text = Bonuses(config, realm);
        RefreshVisual();
    }

    private float CloudAccumulation()
    {
        if (!GodotObject.IsInstanceValid(_player)) return 0f;
        var config = GetNodeOrNull<RealmManager>("/root/RealmManager")?.get_realm_config("云息六层");
        if (config is null || !config.required_attributes.TryGetValue("spirit_strands", out var required) || required.AsSingle() <= 0f)
            return 0f;
        return Mathf.Clamp((_player!.spirit_strands + _player.get_cultivation_progress()) / required.AsSingle(), 0f, 1f);
    }

    private void RefreshVisual()
    {
        if (!GodotObject.IsInstanceValid(_player) || _qiVisual is null) return;
        var index = _previewStage >= 0 ? _previewStage : Index(_player!.cultivation_realm);
        if (index < 0) return;
        if (index >= Stages.Length) return;
        if (index >= 7) _qiVisual.ShowPath(Mathf.Clamp((index - 6) / 3f, 0, 1));
        else if (index >= 6) _qiVisual.ShowCore();
        else _qiVisual.ShowCloud(_previewStage >= 0 ? (index + 1) / 6f : CloudAccumulation());
        if (_realmCaption is not null) _realmCaption.Visible = true;
        if (_realmCaption is not null) _realmCaption.Text = _previewStage >= 0 ? "境界预览（不改变修为）" : "当前境界";
        if (_currentRealm is not null) _currentRealm.Text = Stages[index];
        if (_nextRealm is not null) _nextRealm.Text = index + 1 < Stages.Length ? $"下一层　{Stages[index + 1]}" : "";
        if (_visualHint is not null) _visualHint.Text = index >= 6 ? "灵核已成，内部气息缓缓流动" : index == 5 ? "灵云充盈，尚未凝核" : "灵气渐聚，随修炼逐渐充盈";
        if (_skipAnimation is not null) _skipAnimation.Visible = false;
        _qiVisual.ShowBreaths(index == 9 && _player!.is_yuanxi_realm() ? _player.yuanxi_count : 0);
        if (index == 9 && _player!.is_yuanxi_realm())
        {
            if (_currentRealm is not null) _currentRealm.Text = _player.is_yunxi_complete() ? "云息十层圆满" : "云息十层";
            if (_nextRealm is not null) _nextRealm.Text = _player.is_yunxi_complete() ? "后续　准备铸玄基" : $"归元息　{_player.yuanxi_count}／九息";
            if (_visualHint is not null) _visualHint.Text = _player.is_yuanxi_active() ? $"正在养成第{_player.yuanxi_count + 1}息 · {_player.get_yuanxi_phase_name()}" : _player.is_yunxi_complete() ? "归元九息已成，可准备铸玄基" : "三环相续，周行养息";
        }
    }

    private void OnBreakthrough(Character character, string newRealm)
    {
        var previous = _observedRealm;
        _observedRealm = newRealm;
        if (Index(newRealm) is < 0 or > 9) return;
        _pendingFrom = previous;
        _pendingTo = newRealm;
        _previewStage = -1;
        _pendingPresentation = PendingPresentation.Breakthrough;
    }

    private void PresentBreakthrough()
    {
        if (!GodotObject.IsInstanceValid(_player) || _qiVisual is null || _player!.cultivation_realm != _pendingTo) return;
        BeginPresentation($"已突破至{_pendingTo}");
        if (_pendingFrom == "云息六层" && _pendingTo == "云息七层")
            _qiVisual.PlayCondensation(SetPresentationPhase, CompletePresentation);
        else if (Index(_pendingTo) >= 7)
        {
            SetPresentationPhase("灵流向外续行，新环逐渐贯通");
            _qiVisual.PlayPathGrowth(Mathf.Clamp((Index(_pendingFrom) - 6) / 3f, 0, 1), (Index(_pendingTo) - 6) / 3f, CompletePresentation);
        }
        else
        {
            var amount = CloudAccumulation();
            SetPresentationPhase("气息渐稳，灵云舒展");
            _qiVisual.PlayAccumulation(Mathf.Max(0f, amount - .025f), amount, CompletePresentation);
        }
    }

    private void OnYuanxiChanged(Character character)
    {
        var previous = _observedBreaths;
        _observedBreaths = character.yuanxi_count;
        // 前八息在养息页演出，不打断连续闭关；九息圆满才自动弹出境界结果。
        if (!character.is_yuanxi_realm() || _observedBreaths <= previous || _observedBreaths != 9) return;
        _pendingBreathsFrom = previous;
        _pendingBreathsTo = _observedBreaths;
        _previewStage = -1;
        _pendingPresentation = PendingPresentation.Completion;
    }

    private void PresentBreaths()
    {
        if (!GodotObject.IsInstanceValid(_player) || !_player!.is_yuanxi_realm() || _qiVisual is null) return;
        BeginPresentation(_pendingBreathsTo == 9 ? "云息十层圆满" : "归元息已成");
        SetPresentationPhase(_pendingBreathsTo == 9 ? "九息相续，气机圆融" : "环流分出新息，渐与已有气息相和");
        _qiVisual.PlayBreathGain(_pendingBreathsFrom, _pendingBreathsTo, CompletePresentation);
    }

    private void BeginPresentation(string result)
    {
        var wasVisible = Visible;
        ResetPresentation();
        _isPresenting = true;
        _presentationResult = result;
        PresentationStarted?.Invoke();
        // 不提前刷新新境界的说明和收益；演出结束后再揭示结果。
        foreach (var control in _presentationSurroundings)
        {
            control.Visible = wasVisible;
            control.Modulate = new Color(1, 1, 1, wasVisible ? 1 : 0);
        }
        _presentationLayer!.Show();
        _presentationLayer.Modulate = new Color(1, 1, 1, 0);
        _presentationPhase!.AddThemeFontSizeOverride("font_size", 21);
        _presentationPhase.Modulate = Colors.White;
        _skipAnimation!.Show();
        Show(); MoveToFront();
        _presentationTween = CreateTween().SetParallel();
        foreach (var control in _presentationSurroundings)
            _presentationTween.TweenProperty(control, "modulate:a", 0f, .3);
        _presentationTween.TweenProperty(_presentationLayer, "modulate:a", 1f, .3);
        _presentationTween.TweenMethod(Callable.From<float>(SetPresentationFocus), 0f, 1f, .4)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        _presentationTween.Chain().TweenCallback(Callable.From(() =>
        {
            foreach (var control in _presentationSurroundings) control.Hide();
        }));
    }

    private void SetPresentationPhase(string text)
    {
        if (_presentationPhase is not null) _presentationPhase.Text = text;
    }

    private void SetPresentationFocus(float amount)
    {
        if (_qiVisual is null) return;
        _qiVisual.PivotOffset = _qiVisual.Size * .5f;
        _qiVisual.Scale = Vector2.One * (1f + .65f * amount);
        _qiVisual.SetPresentationFocus(amount);
    }

    private void CompletePresentation()
    {
        if (!_isPresenting) return;
        _qiVisual?.StopTransition();
        _presentationTween?.Kill();
        _presentationTween = CreateTween();
        // 先看清稳定后的形态，再出现新境界；其余说明最后恢复。
        _presentationTween.TweenInterval(.45);
        _presentationTween.TweenProperty(_presentationPhase!, "modulate:a", 0f, .15);
        _presentationTween.TweenCallback(Callable.From(() =>
        {
            _presentationPhase!.Text = _presentationResult;
            _presentationPhase.AddThemeFontSizeOverride("font_size", 32);
        }));
        _presentationTween.TweenProperty(_presentationPhase!, "modulate:a", 1f, .2);
        _presentationTween.TweenInterval(1.0);
        _presentationTween.TweenCallback(Callable.From(() =>
        {
            Rebuild(); Refresh();
            foreach (var control in _presentationSurroundings)
            {
                control.Show(); control.Modulate = new Color(1, 1, 1, 0);
            }
        }));
        _presentationTween.TweenProperty(_presentationLayer!, "modulate:a", 0f, .4);
        _presentationTween.Parallel().TweenMethod(Callable.From<float>(SetPresentationFocus), 1f, 0f, .4)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        foreach (var control in _presentationSurroundings)
            _presentationTween.Parallel().TweenProperty(control, "modulate:a", 1f, .4);
        _presentationTween.TweenCallback(Callable.From(FinishPresentation));
    }

    private void ResetPresentation()
    {
        _presentationTween?.Kill(); _presentationTween = null;
        _qiVisual?.StopTransition();
        _isPresenting = false;
        foreach (var control in _presentationSurroundings)
        {
            control.Show(); control.Modulate = Colors.White;
        }
        _presentationLayer?.Hide();
        if (_skipAnimation is not null) _skipAnimation.Visible = false;
        SetPresentationFocus(0f);
    }

    private void FinishPresentation()
    {
        ResetPresentation();
        Rebuild();
        Refresh();
    }

    private void OnVisibilityChanged()
    {
        if (Visible) return;
        _pendingPresentation = PendingPresentation.None;
        ResetPresentation();
        Closed?.Invoke();
    }

    private static int Index(string realm)
    {
        if (!realm.StartsWith("云息") || !realm.EndsWith("层")) return -1;
        return realm[2..^1] switch
        {
            "一" => 0, "二" => 1, "三" => 2, "四" => 3, "五" => 4, "六" => 5, "七" => 6,
            "八" => 7, "九" => 8, "十" => 9, "十一" => 10, "十二" => 11, "十三" => 12,
            _ => -1
        };
    }

    private string Bonuses(RealmConfig? config, string realm)
    {
        if (config is null) return "此层的正式配置尚未接入。";
        var manager = GetNodeOrNull<RealmManager>("/root/RealmManager");
        var realmIndex = manager?.realm_order.IndexOf(realm) ?? -1;
        if (manager is null || realmIndex < 0) return "此层的正式配置尚未接入。";

        var values = new List<string>();
        var keys = new HashSet<string>();
        for (var index = 0; index <= realmIndex; index++)
        {
            var layer = manager.get_realm_config(manager.realm_order[index]);
            if (layer is null) continue;
            foreach (Variant key in layer.realm_fixed_bonuses.Keys) keys.Add(key.AsString());
            foreach (Variant key in layer.realm_multiplier_bonuses.Keys) keys.Add(key.AsString());
        }

        var basicAttributes = new[] { "physique", "intelligence", "soul", "fortune", "comprehension" };
        (float growth, float flat) DisplayContribution(string key)
        {
            var current = _player?.get_realm_attribute_adjustment_for_realm(realm, key) ?? SumRealmBonuses(manager, realmIndex, key);
            if (_showCumulativeBonuses) return (current.Item1 - 1f, current.Item2);
            var previous = realmIndex > 0
                ? _player?.get_realm_attribute_adjustment_for_realm(manager.realm_order[realmIndex - 1], key) ?? SumRealmBonuses(manager, realmIndex - 1, key)
                : (1f, 0f);
            return (current.Item1 - previous.Item1, current.Item2 - previous.Item2);
        }
        foreach (var key in basicAttributes.Where(keys.Contains))
        {
            var (growth, flat) = DisplayContribution(key);
            values.Add($"{AttributeName(key)}　成长倍率 {growth:+0.00;-0.00;+0.00}　定额加成 {FormatFlat(flat)}");
        }

        // 寿元和辅修位是直接增加值，不套用基础属性公式，也不显示虚假的一点零零倍率。
        foreach (var key in new[] { "max_age", "sub_slot_increase" }.Where(keys.Contains).OrderBy(key => AttributeName(key)))
        {
            var (_, flat) = DisplayContribution(key);
            values.Add($"{AttributeName(key)}　定额加成 {FormatFlat(flat)}");
        }

        var previousEfficiency = _showCumulativeBonuses || realmIndex == 0 ? 1f : Character.get_realm_cultivation_multiplier_for_tier(realmIndex);
        values.Add($"修炼效率　成长倍率 {Character.get_realm_cultivation_multiplier_for_tier(realmIndex + 1) - previousEfficiency:+0.00;-0.00;+0.00}");

        return values.Count == 0 ? "本层没有额外属性加成。" : string.Join("\n", values);
    }

    private static (float growthMultiplier, float fixedBonus) SumRealmBonuses(RealmManager manager, int realmIndex, string key)
    {
        var growth = 1f;
        var fixedBonus = 0f;
        for (var index = 0; index <= realmIndex; index++)
        {
            var layer = manager.get_realm_config(manager.realm_order[index]);
            if (layer is null) continue;
            if (layer.realm_multiplier_bonuses.TryGetValue(key, out var multiplier)) growth += multiplier.AsSingle();
            if (layer.realm_fixed_bonuses.TryGetValue(key, out var flat)) fixedBonus += flat.AsSingle();
        }
        return (growth, fixedBonus);
    }

    private static string FormatFlat(float value) => $"{(value >= 0 ? "+" : "")}{value:0.##}";

    private static string AttributeName(string key) => key switch
    {
        "current_age" => "当前年龄", "gender" => "性别", "physique" => "体魄", "intelligence" => "灵慧",
        "soul" => "神魂", "fortune" => "福缘", "comprehension" => "悟性",
        "charisma" => "魅力", "spirit_strands" => "灵力", "core_count" => "云息灵核",
        "max_health" => "生命上限", "max_mana" => "法力上限", "max_stamina" => "体力上限",
        "max_mind" => "心境上限", "physical_defense" => "物理防御", "precision" => "精准",
        "dodge" => "身法", "crit_resist" => "护心", "initiative_speed" => "行动速度",
        "cards_per_turn" => "每回合出牌数", "initial_hand_size" => "初始手牌数", "sub_slot_increase" => "辅修槽位",
        "health_regen_rate" => "生命恢复", "stamina_regen_rate" => "体力恢复", "mind_regen_rate" => "心境恢复",
        "learning_rate" => "研读效率", "cultivation_rate" => "修炼效率", "critical" => "会心",
        "crit_damage_mult" => "会心伤害", "sword_proficiency" => "剑术造诣", "blade_proficiency" => "刀术造诣",
        "spear_proficiency" => "枪术造诣", "body_proficiency" => "体道造诣", "magic_proficiency" => "术法造诣",
        "forging_proficiency" => "炼器造诣", "talisman_proficiency" => "符箓造诣",
        "formation_proficiency" => "阵法造诣", "alchemy_proficiency" => "炼丹造诣",
        "mana_regen_rate" => "法力恢复", "max_spirit_perception" => "灵识上限", "max_age" => "寿元上限",
        _ => "其他加成"
    };

    private static TextureRect Texture(string path, TextureRect.StretchModeEnum stretch) => new()
    {
        Texture = ResourceLoader.Load<Texture2D>(path),
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = stretch,
        MouseFilter = MouseFilterEnum.Ignore
    };

    private static Label Label(string text, int size, Color color)
    {
        var result = new Label { Text = text };
        result.AddThemeFontSizeOverride("font_size", size);
        result.AddThemeColorOverride("font_color", color);
        return result;
    }

    private static HSeparator Separator()
    {
        var separator = new HSeparator();
        separator.Modulate = new Color(.18f, .16f, .12f, .48f);
        return separator;
    }

    private static StyleBoxFlat Box(Color background, Color border, int width, int radius = 8) => new()
    {
        BgColor = background, BorderColor = border,
        BorderWidthLeft = width, BorderWidthTop = width, BorderWidthRight = width, BorderWidthBottom = width,
        CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius, CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius
    };

    private static void ButtonStyle(Button button, bool current)
    {
        button.AddThemeFontSizeOverride("font_size", 15);
        button.AddThemeColorOverride("font_color", current ? new Color(.96f, .93f, .82f) : new Color(.82f, .78f, .65f));
        button.AddThemeColorOverride("font_hover_color", new Color(1f, .95f, .76f));
        button.AddThemeStyleboxOverride("normal", OpaqueRealmTag());
        button.AddThemeStyleboxOverride("hover", OpaqueRealmTag());
    }

    private static StyleBoxTexture OpaqueRealmTag() => new()
    {
        Texture = ResourceLoader.Load<Texture2D>("res://textures/ui/realm_tag_opaque_v1.png")
    };
}
