using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 云息六层至七层凝核面板。
/// 这是一个独立的八回合牌局，不调用战斗牌组，也不消耗体力、法力或灵识。
/// </summary>
public partial class CondensationPanel : Control
{
    private const string ParticleScenePath = "res://addons/vfx_library/effects/magic_aura.tscn";
    private const string BattleCardScenePath = "res://tscn/panel/BattleCard.tscn";
    private const string BackgroundPath = "res://textures/cultivation/condensation_background_ink_v1.png";
    private const string TitlePlaqueScenePath = "res://tscn/panel/CondensationTitlePlaque.tscn";
    private const string TitlePlaqueTexturePath = "res://textures/ui/cultivation/condensation_title_plaque_transparent_v1.png";

    private sealed class CondensationCard
    {
        public string Name = "无名牌";
        public string Text = "";
        public CondensationElement Element;
        public CardTiming Timing;
        public CardEffect Effect = new();
        public int Seat;
        public bool Swapped;
        public bool Resolved;
        public bool Retain;
        public int RetainTurns;
        public bool RetainedFromPreviousRound;
    }

    private sealed class CardEffect
    {
        public float Volume;
        public float Density;
        public float Impurity;
        public float Compress;
        public float Expand;
        public float NeighborVolume;
        public string KeepIf = "";
        public bool KeepAlways;
        public bool KeepIfSixth;
        public bool BodyHold;
        public bool MetalOrder;
        public bool MetalWater;
        public bool EarthBalance;
        public bool Depend;
        public bool Bridge;
        public bool EchoLast;
        public bool SameEcho;
        public bool FirstPriority;
    }

    private sealed record Cloud(string Name, string Description, int Direction, CondensationRelationTuning Tuning, bool RetainedRelationDoubles = false);
    private enum CardTiming { Normal, Priority, Late }

    private static readonly Cloud[] Clouds =
    [
        new("凝云", "普通牌从左至右结算，邻位关系按通常方向判断。", 1, CondensationRelationTuning.Default),
        new("回风", "普通牌从右至左结算；牌面中的右侧紧邻牌视为左侧紧邻牌，灵根关系按右生左、右克左判断。", -1, CondensationRelationTuning.Default),
        new("连阴", "本轮同灵根关系的密度与体积增加各提高 0.01。", 1, new(.03f, .03f, .04f, 1f, .06f, 2f)),
        new("暖雨", "本轮相生关系的密度增加 0.05，杂质增加 2。", 1, new(.02f, .02f, .05f, 2f, .06f, 2f)),
        new("冷雨", "本轮相生关系的密度增加 0.03，且不增加杂质。", 1, new(.02f, .02f, .03f, 0f, .06f, 2f)),
        new("急雨", "本轮相克关系的体积增加 0.08，杂质增加 3。", 1, new(.02f, .02f, .04f, 1f, .08f, 3f)),
        new("细雨", "本轮相克关系的体积增加 0.04，且不增加杂质。", 1, new(.02f, .02f, .04f, 1f, .04f, 0f)),
        new("层云", "本轮由牌面保留的牌，下一回合第一次灵根关系的数值变化翻倍。", 1, CondensationRelationTuning.Default, true)
    ];

    private Character? _target;
    private bool _testMode;
    private CondensationCoreState _core = new();
    private readonly RandomNumberGenerator _random = new();
    private readonly List<CondensationCard> _deck = [];
    private readonly List<CondensationCard> _discard = [];
    private readonly List<CondensationCard> _retained = [];
    private readonly List<CondensationCard> _row = [];
    private readonly List<string> _log = [];
    private readonly List<Button> _seatButtons = [];
    private readonly List<GpuParticles2D> _particleLayers = [];
    private PackedScene? _battleCardScene;
    private Label? _title, _round, _stats, _deckInfo, _logLabel, _pileBrowserTitle;
    private SkillCard? _cloudCard;
    private Panel? _cloudCardFrame;
    private Control? _cloudCardHost;
    private Control? _particleHost, _particleAnchor;
    private Tween? _cloudFlipTween;
    // BattleCard 原尺寸为 240×360；云候框内线可容纳 240×360，使用 1.00 使四边对齐。
    private const float CloudCardScale = 1f;
    private HBoxContainer? _rowBox;
    private Button? _closeButton, _drawPileButton, _discardPileButton;
    private Panel? _pileBrowser;
    private GridContainer? _pileGrid;
    private ScrollContainer? _logScroll;
    private Cloud _currentCloud = Clouds[0];
    private int _roundNumber;
    private int _firstSwap = -1;
    private int _secondSwap = -1;
    private bool _awaitingSwap;
    private bool _settled;
    private bool _finished;
    private int _dragSeat = -1;
    private int _dragTargetSeat = -1;
    private Vector2 _dragStart = Vector2.Zero;
    private bool _dragMoved;
    private CondensationRelationResult? _lastRelation;
    private float _roundStartVolume = 1f;
    private float _roundStartDensity = 1f;
    private float _roundStartImpurity;

    public event Action? Closed;
    public event Action<Character, bool>? CondensationFinished;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddToGroup("condensation_panel");
        ZIndex = 600;
        MouseFilter = MouseFilterEnum.Stop;
        BuildUi();
        Hide();
    }

    public void ShowFor(Character target, bool testMode = false)
    {
        if (!GodotObject.IsInstanceValid(target)) return;
        _target = target; _testMode = testMode;
        _random.Randomize();
        _core = new CondensationCoreState();
        _deck.Clear(); _discard.Clear(); _retained.Clear(); _row.Clear(); _log.Clear();
        _roundNumber = 0; _firstSwap = -1; _secondSwap = -1; _settled = false; _finished = false;
        ResetDrag();
        _roundStartVolume = 1f; _roundStartDensity = 1f; _roundStartImpurity = 0f; _pileBrowser?.Hide();
        BuildDeck(target);
        AddLog("凝核开始：体积 1.00，密度 1.00，杂质 0。", false);
        BeginRound();
        Show(); MoveToFront();
    }

    public void ClosePanel()
    {
        ResetDrag();
        _pileBrowser?.Hide();
        Hide();
        Closed?.Invoke();
    }

    /// <summary>供外部页面栈优先关闭牌堆浏览层，不退出凝核牌局。</summary>
    public bool TryClosePileBrowser()
    {
        if (_pileBrowser?.Visible != true) return false;
        _pileBrowser.Hide();
        return true;
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape } && TryClosePileBrowser())
            GetViewport().SetInputAsHandled();
        if (!Visible || _pileBrowser?.Visible == true || !_awaitingSwap || _settled || _finished) return;
        var pointer = GetViewport().GetMousePosition();
        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left } mouse)
        {
            if (mouse.Pressed)
            {
                var seat = SeatAt(pointer);
                if (seat >= 0)
                {
                    _dragSeat = seat; _dragTargetSeat = seat; _dragStart = pointer; _dragMoved = false;
                    GetViewport().SetInputAsHandled();
                }
            }
            else if (_dragSeat >= 0)
            {
                var source = _dragSeat; var target = SeatAt(pointer);
                var shouldSwap = _dragMoved && target >= 0 && target != source;
                ResetDrag();
                if (shouldSwap) SwapAndSettle(source, target);
                GetViewport().SetInputAsHandled();
            }
            return;
        }
        if (@event is InputEventMouseMotion && _dragSeat >= 0)
        {
            if (!_dragMoved && pointer.DistanceTo(_dragStart) >= 12f) _dragMoved = true;
            _dragTargetSeat = SeatAt(pointer);
            UpdateDragVisuals();
            GetViewport().SetInputAsHandled();
        }
    }

    private void BuildUi()
    {
        _battleCardScene = ResourceLoader.Load<PackedScene>(BattleCardScenePath);

        // 背景只负责静室的暖色纸纹，不再叠一层深色遮罩；可读性由文字颜色和轻边框完成。
        var backdrop = new TextureRect
        {
            Texture = GD.Load<Texture2D>(BackgroundPath),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = MouseFilterEnum.Ignore,
            Modulate = Colors.White
        };
        backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(backdrop);

        var frame = new PanelContainer { Name = "CondensationFrame", MouseFilter = MouseFilterEnum.Stop };
        frame.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); frame.OffsetLeft = 22; frame.OffsetTop = 20; frame.OffsetRight = -22; frame.OffsetBottom = -20;
        frame.AddThemeStyleboxOverride("panel", FrameStyle()); AddChild(frame);

        // 顶部题签是独立场景：根节点锚定顶边，透明素材只保留牌面与边饰，便于在编辑器中直接调整。
        var titleBanner = ResourceLoader.Load<PackedScene>(TitlePlaqueScenePath)?.Instantiate<Control>();
        if (titleBanner is not null)
        {
            titleBanner.Name = "CondensationTitleBanner";
            AddChild(titleBanner);
            _title = titleBanner.GetNodeOrNull<Label>("Title");
            _round = titleBanner.GetNodeOrNull<Label>("Round");
        }
        if (_title is null || _round is null)
        {
            titleBanner?.QueueFree();
            titleBanner = BuildTitlePlaqueFallback();
            AddChild(titleBanner);
        }

        var root = new VBoxContainer(); root.AddThemeConstantOverride("separation", 8); frame.AddChild(root);
        var head = new HBoxContainer { CustomMinimumSize = new Vector2(0, 44) }; root.AddChild(head);
        head.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
        _closeButton = new Button { Text = "暂离", CustomMinimumSize = new Vector2(88, 40) }; StyleButton(_closeButton); _closeButton.Pressed += ClosePanel; head.AddChild(_closeButton);

        var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill }; body.AddThemeConstantOverride("separation", 14); root.AddChild(body);
        var left = new VBoxContainer { CustomMinimumSize = new Vector2(270, 0), SizeFlagsVertical = SizeFlags.ExpandFill }; left.AddThemeConstantOverride("separation", 6); body.AddChild(left);
        _cloudCardHost = BuildCloudCard(); left.AddChild(_cloudCardHost);

        var center = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill }; center.AddThemeConstantOverride("separation", 8); body.AddChild(center);
        var effectPanel = new Control { SizeFlagsVertical = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore, ClipContents = false }; BuildCoreParticles(effectPanel); center.AddChild(effectPanel);

        // 底部采用战斗界面的三段式结构：提示在手牌左侧，手牌居中，两个牌堆按钮在右侧。
        _stats = Label("体积 1.00　密度 1.00　总量 1.00　杂质 0.00", 19, Colors.White);
        _stats.SizeFlagsHorizontal = SizeFlags.ExpandFill; _stats.HorizontalAlignment = HorizontalAlignment.Center; _stats.VerticalAlignment = VerticalAlignment.Center; _stats.CustomMinimumSize = new Vector2(0, 34); _stats.MouseFilter = MouseFilterEnum.Ignore; root.AddChild(_stats);
        var handShell = new HBoxContainer { CustomMinimumSize = Vector2.Zero }; handShell.AddThemeConstantOverride("separation", 12); root.AddChild(handShell);

        var promptPanel = new PanelContainer { Name = "HandPrompt", CustomMinimumSize = new Vector2(232, 0), SizeFlagsVertical = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        promptPanel.AddThemeStyleboxOverride("panel", SideStyle());
        var promptContent = new VBoxContainer(); promptContent.AddThemeConstantOverride("separation", 8); promptPanel.AddChild(promptContent);
        var promptTitle = Label("凝核记录", 18, Colors.White); promptTitle.MouseFilter = MouseFilterEnum.Ignore; promptContent.AddChild(promptTitle);
        _logScroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _logLabel = Label("", 14, Colors.White); _logLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart; _logLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill; _logLabel.MouseFilter = MouseFilterEnum.Ignore; _logScroll.AddChild(_logLabel); promptContent.AddChild(_logScroll);
        handShell.AddChild(promptPanel);

        var handCenter = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill }; handCenter.AddThemeConstantOverride("separation", 4); handShell.AddChild(handCenter);
        var handHead = new HBoxContainer { CustomMinimumSize = new Vector2(0, 30) }; handCenter.AddChild(handHead);
        var handTitle = Label("六席手牌", 18, Colors.White); handTitle.MouseFilter = MouseFilterEnum.Ignore; handHead.AddChild(handTitle);
        handHead.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
        _rowBox = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, SizeFlagsVertical = SizeFlags.ExpandFill }; _rowBox.AddThemeConstantOverride("separation", 6); handCenter.AddChild(_rowBox);

        var pilePanel = new PanelContainer { Name = "PileButtons", CustomMinimumSize = new Vector2(236, 0), SizeFlagsVertical = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Stop };
        pilePanel.AddThemeStyleboxOverride("panel", SideStyle());
        var pileContent = new VBoxContainer(); pileContent.AddThemeConstantOverride("separation", 8); pilePanel.AddChild(pileContent);
        var pileTitle = Label("牌堆", 18, Colors.White); pileTitle.HorizontalAlignment = HorizontalAlignment.Center; pileTitle.MouseFilter = MouseFilterEnum.Ignore; pileContent.AddChild(pileTitle);
        var pileButtons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }; pileButtons.AddThemeConstantOverride("separation", 6); pileContent.AddChild(pileButtons);
        _drawPileButton = PileButton("抽牌堆"); _drawPileButton.CustomMinimumSize = new Vector2(108, 52); _drawPileButton.Pressed += () => OpenPile("抽牌堆（牌堆顶在前）", _deck); pileButtons.AddChild(_drawPileButton);
        _discardPileButton = PileButton("弃牌堆"); _discardPileButton.CustomMinimumSize = new Vector2(108, 52); _discardPileButton.Pressed += () => OpenPile("弃牌堆（按手牌顺序）", _discard); pileButtons.AddChild(_discardPileButton);
        pileContent.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
        handShell.AddChild(pilePanel);
        BuildPileBrowser();
    }

    private Control BuildTitlePlaqueFallback()
    {
        var titleBanner = new Control
        {
            Name = "CondensationTitleBanner",
            MouseFilter = MouseFilterEnum.Ignore,
            ZIndex = 20
        };
        titleBanner.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterTop);
        titleBanner.OffsetLeft = -230; titleBanner.OffsetTop = 0; titleBanner.OffsetRight = 230; titleBanner.OffsetBottom = 126;

        var plaque = new TextureRect
        {
            Texture = GD.Load<Texture2D>(TitlePlaqueTexturePath),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore
        };
        plaque.Position = new Vector2(0, -17); plaque.Size = new Vector2(460, 144); titleBanner.AddChild(plaque);

        _title = new Label { Text = "凝核", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
        _title.Position = new Vector2(36, 13); _title.Size = new Vector2(388, 48); _title.AddThemeFontSizeOverride("font_size", 28); _title.AddThemeColorOverride("font_color", new Color("f4f0df")); _title.AddThemeColorOverride("font_shadow_color", new Color(0.04f, 0.03f, 0.02f, .92f)); _title.AddThemeConstantOverride("shadow_offset_x", 1); _title.AddThemeConstantOverride("shadow_offset_y", 2); titleBanner.AddChild(_title);
        _round = new Label { Text = "当前回合1/总回合8", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
        _round.Position = new Vector2(36, 67); _round.Size = new Vector2(388, 45); _round.AddThemeFontSizeOverride("font_size", 15); _round.AddThemeColorOverride("font_color", new Color("f4f0df")); _round.AddThemeColorOverride("font_shadow_color", new Color(0.04f, 0.03f, 0.02f, .92f)); _round.AddThemeConstantOverride("shadow_offset_x", 1); _round.AddThemeConstantOverride("shadow_offset_y", 1); titleBanner.AddChild(_round);
        return titleBanner;
    }

    private Control BuildCloudCard()
    {
        var host = new Control { Name = "CloudCardHost", CustomMinimumSize = new Vector2(0, 410), SizeFlagsVertical = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore, ClipContents = false };
        // 云候框只用代码绘制两层淡墨线，外部与中部保持透明；战斗牌本身的黑框和画面不改。
        _cloudCardFrame = new Panel { Name = "CloudCardFrame", MouseFilter = MouseFilterEnum.Ignore };
        _cloudCardFrame.AddThemeStyleboxOverride("panel", CloudFrameStyle());
        host.AddChild(_cloudCardFrame);
        var innerFrame = new Panel { Name = "CloudCardInnerFrame", MouseFilter = MouseFilterEnum.Ignore };
        innerFrame.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        innerFrame.OffsetLeft = 6; innerFrame.OffsetTop = 6; innerFrame.OffsetRight = -6; innerFrame.OffsetBottom = -6;
        innerFrame.AddThemeStyleboxOverride("panel", CloudFrameInnerStyle());
        _cloudCardFrame.AddChild(innerFrame);
        if (_battleCardScene?.Instantiate<SkillCard>() is SkillCard visual)
        {
            _cloudCard = visual; visual.PivotOffset = new Vector2(120, 180); visual.Scale = Vector2.One * CloudCardScale; visual.setup_condensation("云候", "等待本回合云候。", CloudIcon(_currentCloud), -1, "云候"); host.AddChild(visual);
            host.Resized += PositionCloudCard; CallDeferred(nameof(PositionCloudCard));
        }
        else
        {
            var fallback = new PanelContainer { CustomMinimumSize = new Vector2(0, 260), MouseFilter = MouseFilterEnum.Ignore }; fallback.AddThemeStyleboxOverride("panel", EffectStyle()); var text = Label("云候\n\n等待本回合环境牌。", 16, new Color("3f2c1c")); text.HorizontalAlignment = HorizontalAlignment.Center; text.VerticalAlignment = VerticalAlignment.Center; text.AutowrapMode = TextServer.AutowrapMode.WordSmart; fallback.AddChild(text); host.AddChild(fallback);
        }
        return host;
    }

    private void PositionCloudCard()
    {
        if (_cloudCardHost is null || _cloudCard is null || !GodotObject.IsInstanceValid(_cloudCard)) return;
        var frameSize = new Vector2(252f, 378f);
        if (_cloudCardFrame is not null && GodotObject.IsInstanceValid(_cloudCardFrame))
        {
            _cloudCardFrame.Size = frameSize;
            _cloudCardFrame.Position = (_cloudCardHost.Size - frameSize) * .5f;
        }
        var scaled = new Vector2(240f, 360f) * CloudCardScale; _cloudCard.Position = (_cloudCardHost.Size - scaled) * .5f;
    }

    private void RefreshCloudCard(bool animate)
    {
        if (_cloudCard is null || !GodotObject.IsInstanceValid(_cloudCard)) return;
        var targetScale = Vector2.One * CloudCardScale; _cloudFlipTween?.Kill();
        if (!animate)
        {
            _cloudCard.Scale = targetScale; _cloudCard.setup_condensation(_currentCloud.Name, _currentCloud.Description, CloudIcon(_currentCloud), -1, "云候"); PositionCloudCard(); return;
        }
        _cloudFlipTween = CreateTween(); _cloudFlipTween.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
        _cloudFlipTween.TweenProperty(_cloudCard, "scale", new Vector2(.035f, CloudCardScale), .14);
        _cloudFlipTween.TweenCallback(Callable.From(() => { if (GodotObject.IsInstanceValid(_cloudCard)) _cloudCard.setup_condensation(_currentCloud.Name, _currentCloud.Description, CloudIcon(_currentCloud), -1, "云候"); }));
        _cloudFlipTween.TweenProperty(_cloudCard, "scale", targetScale, .18); _cloudFlipTween.Finished += PositionCloudCard;
    }

    private void BuildCoreParticles(Control host)
    {
        var anchor = new Control { Name = "CoreParticleAnchor", MouseFilter = MouseFilterEnum.Ignore };
        anchor.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); host.AddChild(anchor);
        _particleHost = host;
        _particleAnchor = anchor;
        var scene = ResourceLoader.Load<PackedScene>(ParticleScenePath);
        if (scene is null) return;
        var tints = new[] { new Color(.55f, .86f, .78f, .42f), new Color(.43f, .72f, .94f, .32f), new Color(.90f, .76f, .42f, .28f) };
        var scales = new[] { 1.15f, .78f, .48f };
        for (var i = 0; i < tints.Length; i++)
        {
            if (scene.Instantiate() is not GpuParticles2D layer) continue;
            layer.Name = $"CoreParticleLayer{i + 1}";
            layer.Scale = Vector2.One * scales[i]; layer.Modulate = tints[i]; layer.Emitting = true; layer.ZIndex = 1;
            anchor.AddChild(layer); _particleLayers.Add(layer);
        }
        host.Resized += PositionCoreParticles;
        CallDeferred(nameof(PositionCoreParticles));
    }

    private void PositionCoreParticles()
    {
        if (_particleHost is null || _particleAnchor is null || !GodotObject.IsInstanceValid(_particleHost) || !GodotObject.IsInstanceValid(_particleAnchor)) return;
        var viewportSize = GetViewport().GetVisibleRect().Size;
        var screenCenter = viewportSize.X > 0f && viewportSize.Y > 0f
            ? viewportSize * .5f
            : _particleHost.GlobalPosition + _particleHost.Size * .5f;
        var localCenter = screenCenter - _particleAnchor.GlobalPosition;
        foreach (var layer in _particleLayers.Where(GodotObject.IsInstanceValid)) layer.Position = localCenter;
    }

    private static Button PileButton(string label)
    {
        var button = new Button { Text = $"{label}\n0", CustomMinimumSize = new Vector2(112, 48), Alignment = HorizontalAlignment.Center, FocusMode = Control.FocusModeEnum.None };
        button.AddThemeFontSizeOverride("font_size", 13); button.AddThemeStyleboxOverride("normal", PileStyle()); button.AddThemeStyleboxOverride("hover", PileHoverStyle()); button.AddThemeStyleboxOverride("pressed", PileHoverStyle()); button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        button.AddThemeColorOverride("font_color", new Color("f4f0df")); button.AddThemeColorOverride("font_hover_color", Colors.White); button.AddThemeColorOverride("font_shadow_color", new Color(0.04f, 0.03f, 0.02f, .9f)); button.AddThemeConstantOverride("shadow_offset_x", 1); button.AddThemeConstantOverride("shadow_offset_y", 1); return button;
    }

    private void BuildPileBrowser()
    {
        _pileBrowser = new Panel { Name = "PileBrowser", Visible = false, ZIndex = 900, MouseFilter = MouseFilterEnum.Stop };
        _pileBrowser.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); _pileBrowser.OffsetLeft = 58; _pileBrowser.OffsetTop = 50; _pileBrowser.OffsetRight = -58; _pileBrowser.OffsetBottom = -50; _pileBrowser.AddThemeStyleboxOverride("panel", new StyleBoxEmpty()); AddChild(_pileBrowser);
        var ink = GD.Load<Texture2D>("res://textures/ui/battle_pile_panel_v1.png");
        if (ink is not null)
        {
            var inkLayer = new TextureRect { Texture = ink, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale, Modulate = new Color(1f, 1f, 1f, .08f), MouseFilter = MouseFilterEnum.Ignore };
            inkLayer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); _pileBrowser.AddChild(inkLayer);
        }
        var root = new VBoxContainer(); root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); root.OffsetLeft = 24; root.OffsetTop = 20; root.OffsetRight = -24; root.OffsetBottom = -20; root.AddThemeConstantOverride("separation", 12); _pileBrowser.AddChild(root);
        var head = new HBoxContainer { CustomMinimumSize = new Vector2(0, 44) }; root.AddChild(head);
        _pileBrowserTitle = Label("牌堆", 23, new Color("3f2c1c")); head.AddChild(_pileBrowserTitle);
        head.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
        var close = new Button { Text = "返回手牌", CustomMinimumSize = new Vector2(112, 40) }; StyleButton(close); close.Pressed += () => _pileBrowser?.Hide(); head.AddChild(close);
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _pileGrid = new GridContainer { Columns = 6 }; _pileGrid.AddThemeConstantOverride("h_separation", 12); _pileGrid.AddThemeConstantOverride("v_separation", 12); scroll.AddChild(_pileGrid); root.AddChild(scroll);
    }

    private void OpenPile(string title, IReadOnlyList<CondensationCard> pile)
    {
        if (_pileBrowser is null || _pileGrid is null) return;
        if (_pileBrowserTitle is not null) _pileBrowserTitle.Text = title;
        foreach (var child in _pileGrid.GetChildren()) child.QueueFree();
        for (var i = 0; i < pile.Count; i++) _pileGrid.AddChild(PileCard(pile[i], i));
        _pileBrowser.Show(); _pileBrowser.MoveToFront();
    }

    private Control PileCard(CondensationCard card, int index) => BuildCondensationCard(card, index, new Vector2(188, 282), false, false);

    private Control BuildCondensationCard(CondensationCard card, int index, Vector2 slotSize, bool interactive, bool selected)
    {
        var slot = new Control { CustomMinimumSize = slotSize, MouseFilter = interactive ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore, ClipContents = true };
        if (_battleCardScene?.Instantiate<SkillCard>() is SkillCard visual)
        {
            slot.AddChild(visual);
            var scale = Mathf.Min(slotSize.X / 240f, slotSize.Y / 360f);
            visual.Scale = Vector2.One * scale;
            visual.Position = (slotSize - new Vector2(240f, 360f) * scale) * .5f;
            visual.setup_condensation(card.Name, card.Text, CondensationIcon(card.Element), index);
            visual.set_selected(selected);
            visual.Modulate = card.Resolved ? new Color(.56f, .57f, .54f, 1f) : Colors.White;
        }
        else
        {
            var fallback = new Button { Text = $"第 {index + 1} 位\n{card.Name}\n{ElementName(card.Element)}\n\n{card.Text}", MouseFilter = MouseFilterEnum.Ignore, Disabled = true };
            fallback.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); fallback.AddThemeFontSizeOverride("font_size", 14); StyleCard(fallback, card.Element, selected, card.Resolved); slot.AddChild(fallback);
        }
        if (interactive)
        {
            var select = new Button { Text = "", Disabled = _settled || card.Resolved, FocusMode = Control.FocusModeEnum.None, MouseFilter = MouseFilterEnum.Stop };
            select.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            select.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
            select.AddThemeStyleboxOverride("hover", new StyleBoxEmpty());
            select.AddThemeStyleboxOverride("pressed", new StyleBoxEmpty());
            select.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
            select.TooltipText = "拖动到另一张牌上，对调牌位并开始结算。";
            slot.AddChild(select); _seatButtons.Add(select);
        }
        return slot;
    }

    private static Texture2D? CondensationIcon(CondensationElement element)
    {
        var path = element switch
        {
            CondensationElement.Water => "res://textures/cards/expressive_ink/yangxi_ink_v4.png",
            CondensationElement.Wood => "res://textures/cards/expressive_ink/zhuangzhuang_ink_v4.png",
            CondensationElement.Fire => "res://textures/cards/expressive_ink/fajin_ink_v4.png",
            CondensationElement.Earth => "res://textures/cards/expressive_ink/hanshan_ink_v4.png",
            CondensationElement.Metal => "res://textures/cards/expressive_ink/jiazhao_ink_v4.png",
            _ => "res://textures/cards/expressive_ink/jingguan_ink_v4.png"
        };
        return ResourceLoader.Load<Texture2D>(path);
    }

    private static Texture2D? CloudIcon(Cloud cloud)
    {
        var path = cloud.Name switch
        {
            "回风" => "res://textures/ui/cultivation/clouds/cloud_returning_wind_v1.png",
            "连阴" => "res://textures/ui/cultivation/clouds/cloud_continuous_overcast_v1.png",
            "暖雨" => "res://textures/ui/cultivation/clouds/cloud_warm_rain_v1.png",
            "冷雨" => "res://textures/ui/cultivation/clouds/cloud_cold_rain_v1.png",
            "急雨" => "res://textures/ui/cultivation/clouds/cloud_sudden_rain_v1.png",
            "细雨" => "res://textures/ui/cultivation/clouds/cloud_light_rain_v1.png",
            "层云" => "res://textures/ui/cultivation/clouds/cloud_layered_v1.png",
            _ => "res://textures/ui/cultivation/clouds/cloud_condensation_v1.png"
        };
        // 新素材未导入时仍保留旧牌面，避免测试入口因单张图缺失而显示空图。
        return ResourceLoader.Load<Texture2D>(path) ?? CondensationIcon(CondensationElement.Water);
    }

    private static string ElementName(CondensationElement element) => element switch
    {
        CondensationElement.Water => "水灵根", CondensationElement.Wood => "木灵根", CondensationElement.Fire => "火灵根", CondensationElement.Earth => "土灵根", CondensationElement.Metal => "金灵根", _ => "机制牌"
    };

    private void BuildDeck(Character target)
    {
        var roots = EffectiveRoots(target);
        var rootEntries = roots.SelectMany(root => RootCards(root).Select(card => (root, card))).ToList();
        for (var i = 0; i < 26; i++)
        {
            var weightedRoot = WeightedRoot(target, roots);
            var cards = RootCards(weightedRoot);
            _deck.Add(cards[_random.RandiRange(0, cards.Count - 1)]);
        }
        if (roots.Count == 1)
        {
            _deck.Add(StrongCard(roots[0], false)); _deck.Add(StrongCard(roots[0], true));
        }
        else if (roots.Count == 2)
        {
            foreach (var root in roots) _deck.Add(StrongCard(root, false));
        }
        else
        {
            for (var i = 0; i < 2; i++) _deck.Add(RootCards(roots[_random.RandiRange(0, roots.Count - 1)])[0]);
        }
        var statCards = StatCards(target);
        foreach (var stat in new[] { "physique", "intelligence", "agility", "soul" }) _deck.Add(statCards[stat][_random.RandiRange(0, statCards[stat].Count - 1)]);
        for (var i = 0; i < 8; i++)
        {
            var stat = WeightedStat(target); _deck.Add(statCards[stat][_random.RandiRange(0, statCards[stat].Count - 1)]);
        }
        var methods = MethodCards();
        for (var i = 0; i < 8; i++) _deck.Add(methods[_random.RandiRange(0, methods.Count - 1)]);
        Shuffle(_deck);
    }

    private void BeginRound()
    {
        if (_finished) return;
        _roundNumber++;
        _currentCloud = Clouds[_random.RandiRange(0, Clouds.Length - 1)];
        _roundStartVolume = _core.Volume; _roundStartDensity = _core.Density; _roundStartImpurity = _core.Impurity;
        _row.Clear();
        var previous = _retained.ToList(); _retained.Clear();
        while (_row.Count + previous.Count < 6 && _deck.Count > 0) { _row.Insert(0, _deck[0]); _deck.RemoveAt(0); }
        foreach (var card in previous) { card.RetainedFromPreviousRound = true; card.Resolved = false; card.Swapped = false; _row.Add(card); }
        foreach (var card in _row) { card.Seat = _row.IndexOf(card); card.Resolved = false; card.Swapped = false; }
        _firstSwap = -1; _secondSwap = -1; _awaitingSwap = true; _settled = false; _lastRelation = null;
        AddLog($"第 {_roundNumber} 回合：云候为“{_currentCloud.Name}”。", false);
        RefreshCloudCard(_roundNumber > 1);
        Render();
    }

    private void SwapAndSettle(int firstSwap, int secondSwap)
    {
        if (!_awaitingSwap || _settled || firstSwap < 0 || secondSwap < 0 || firstSwap >= _row.Count || secondSwap >= _row.Count || firstSwap == secondSwap) return;
        _firstSwap = firstSwap; _secondSwap = secondSwap;
        (_row[_firstSwap], _row[_secondSwap]) = (_row[_secondSwap], _row[_firstSwap]);
        foreach (var card in _row) { card.Seat = _row.IndexOf(card); card.Swapped = false; }
        _row[_firstSwap].Swapped = true; _row[_secondSwap].Swapped = true;
        _awaitingSwap = false; _settled = true;
        AddLog($"本轮对调第 {_firstSwap + 1} 位与第 {_secondSwap + 1} 位。", false);
        foreach (var button in _seatButtons) button.Disabled = true;
        ResolveRound();
    }

    private void ResolveRound()
    {
        // 先固定结算前的手牌顺序；自动插牌/结算方向只影响效果，不改变弃牌堆记录顺序。
        var handOrder = _row.ToList();
        var snapshot = _row.ToList();
        var priority = snapshot.Where(card => card.Timing == CardTiming.Priority).OrderBy(card => card.Seat).ToList();
        var late = snapshot.Where(card => card.Timing == CardTiming.Late).OrderBy(card => card.Seat).ToList();
        foreach (var card in priority) ResolveCard(card, _currentCloud.Direction);
        if (!_core.Failed)
        {
            var normal = _row.Where(card => card.Timing == CardTiming.Normal && !card.Resolved).ToList();
            if (_currentCloud.Direction < 0) normal.Reverse();
            foreach (var card in normal) ResolveCard(card, _currentCloud.Direction);
        }
        if (!_core.Failed) foreach (var card in late) ResolveCard(card, _currentCloud.Direction);
        AddRoundSummary();
        var discardOrder = handOrder.Concat(_row.Where(card => !handOrder.Contains(card))).ToList();
        foreach (var card in discardOrder)
        {
            if (card.Retain && card.RetainTurns > 0 && !_core.Failed) { card.RetainTurns--; _retained.Add(card); }
            else _discard.Add(card);
        }
        _row.Clear();
        Render();
        if (_core.Failed) { Finish(false, "杂质达到 100，凝核失败；牌局已中止。"); return; }
        if (_roundNumber >= 8) { Finish(true, "八回合结束，云息灵核已成。"); return; }
        BeginRound();
    }

    private void AddRoundSummary()
    {
        var dv = _core.Volume - _roundStartVolume; var dd = _core.Density - _roundStartDensity; var di = _core.Impurity - _roundStartImpurity;
        AddLog($"本回合结算：体积 {dv:+0.00;-0.00;0.00}，密度 {dd:+0.00;-0.00;0.00}，杂质 {di:+0.00;-0.00;0.00}；当前体积 {_core.Volume:0.00}，密度 {_core.Density:0.00}，总量 {_core.TotalMass:0.00}，杂质 {_core.Impurity:0.00}。", di > 0f);
    }

    private void ResolveCard(CondensationCard card, int direction)
    {
        if (card.Resolved || _core.Failed) return;
        card.Resolved = true;
        var beforeVolume = _core.Volume;
        var beforeDensity = _core.Density;
        var beforeImpurity = _core.Impurity;
        var relation = ApplyNeighbourRelation(card, direction);
        if (_core.Failed) return;
        var effect = card.Effect;
        var directVolume = effect.Volume;
        if (directVolume != 0f) ApplyChange(_core.AddVolume(directVolume), $"{card.Name}：牌面体积");
        if (effect.NeighborVolume != 0f && NeighborElement(card, direction) is CondensationElement.Wood or CondensationElement.Fire) ApplyChange(_core.AddVolume(effect.NeighborVolume), $"{card.Name}：邻位增量");
        if (_core.Failed) return;
        if (effect.EarthBalance)
        {
            if (_core.Volume < 1.20f) ApplyChange(_core.AddVolume(.07f), $"{card.Name}：承载体积");
            else ApplyChange(_core.AddDensity(.03f), $"{card.Name}：承载密度");
        }
        else if (effect.Density != 0f) ApplyChange(_core.AddDensity(effect.Density), $"{card.Name}：牌面密度");
        if (effect.Compress > 0f) ApplyChange(_core.Compress(effect.Compress), $"{card.Name}：标准压缩");
        if (effect.Expand > 0f) ApplyChange(_core.Expand(effect.Expand), $"{card.Name}：标准扩张");
        var impurity = effect.Impurity;
        if (impurity != 0f) ApplyChange(AddImpurity(impurity, $"{card.Name}：牌面杂质"), $"{card.Name}：牌面杂质");
        if (_core.Failed) return;
        card.Retain = effect.KeepAlways || (effect.KeepIfSixth && card.Seat == 5) || (!string.IsNullOrEmpty(effect.KeepIf) && NeighborElement(card, direction).ToString().Equals(effect.KeepIf, StringComparison.OrdinalIgnoreCase)) || effect.BodyHold;
        if (card.Retain) { card.RetainTurns = Math.Max(1, effect.KeepAlways || effect.BodyHold ? 1 : 1); AddLog($"{card.Name}保留至下一回合。", false); }
        if (effect.EchoLast && _lastRelation is not null) ApplyRelationValues(_lastRelation.Value, true, $"{card.Name}：余响");
        if (effect.SameEcho && _lastRelation is { Type: "同灵根" } same) ApplyRelationValues(same, true, $"{card.Name}：重帘");
        var deltaVolume = _core.Volume - beforeVolume;
        var deltaDensity = _core.Density - beforeDensity;
        var deltaImpurity = _core.Impurity - beforeImpurity;
        AddLog($"{card.Name}结算后：体积 {deltaVolume:+0.00;-0.00;0.00}，密度 {deltaDensity:+0.00;-0.00;0.00}，杂质 {deltaImpurity:+0.00;-0.00;0.00}。", deltaImpurity > 0f);
    }

    private CondensationRelationResult? ApplyNeighbourRelation(CondensationCard card, int direction)
    {
        if (card.Element == CondensationElement.None) return null;
        var seat = _row.IndexOf(card); if (seat < 0) return null;
        var front = seat - direction;
        if (front < 0 || front >= _row.Count) return null;
        var frontCard = _row[front];
        var frontElement = EffectiveElement(frontCard, direction);
        var backElement = EffectiveElement(card, direction);
        if (frontElement == CondensationElement.None || backElement == CondensationElement.None) return null;
        if (frontCard.Effect.Bridge)
        {
            var bridgeSeat = front - direction;
            while (bridgeSeat >= 0 && bridgeSeat < _row.Count && EffectiveElement(_row[bridgeSeat], direction) == CondensationElement.None) bridgeSeat -= direction;
            if (bridgeSeat >= 0 && bridgeSeat < _row.Count) frontElement = EffectiveElement(_row[bridgeSeat], direction);
        }
        var relationOption = Relation(frontElement, backElement, _currentCloud.Tuning);
        if (!relationOption.HasValue) return null;
        var relation = relationOption.Value;
        if (frontCard.Effect.MetalOrder && frontElement == CondensationElement.Metal)
        {
            if (frontCard.Swapped) relation = relation with { Density = relation.Density + .03f, Impurity = relation.Impurity + 1f };
            else relation = relation with { Impurity = MathF.Max(0f, relation.Impurity - 1f) };
        }
        if (frontCard.Effect.MetalWater && frontElement == CondensationElement.Metal && backElement == CondensationElement.Water)
            relation = relation with { Density = relation.Density + .01f, Impurity = MathF.Max(0f, relation.Impurity - 1f) };
        if (frontCard.Effect.Depend) relation = relation with { Type = relation.Type };
        ApplyRelationValues(relation, false, $"{relation.Type}（{frontElement}→{backElement}）");
        _lastRelation = relation;
        return relation;
    }

    private void ApplyRelationValues(CondensationRelationResult relation, bool doubled, string reason)
    {
        var multiplier = doubled ? 2f : 1f;
        var d = relation.Density * multiplier; var v = relation.Volume * multiplier; var i = relation.Impurity * multiplier;
        if (d != 0f) ApplyChange(_core.AddDensity(d), reason + "：密度");
        if (v != 0f) ApplyChange(_core.AddVolume(v), reason + "：体积");
        if (i != 0f) ApplyChange(AddImpurity(i, reason + "：杂质"), reason + "：杂质");
    }

    private CondensationChange AddImpurity(float amount, string reason)
    {
        return _core.AddImpurity(amount);
    }

    private CondensationElement EffectiveElement(CondensationCard card, int direction)
    {
        if (card.Element != CondensationElement.None) return card.Element;
        if (!card.Effect.Depend) return CondensationElement.None;
        var seat = _row.IndexOf(card) + direction;
        while (seat >= 0 && seat < _row.Count)
        {
            if (_row[seat].Element != CondensationElement.None) return _row[seat].Element;
            seat += direction;
        }
        return CondensationElement.None;
    }

    private CondensationElement NeighborElement(CondensationCard card, int direction)
    {
        var seat = _row.IndexOf(card) - direction; return seat >= 0 && seat < _row.Count ? EffectiveElement(_row[seat], direction) : CondensationElement.None;
    }

    private static CondensationRelationResult? Relation(CondensationElement front, CondensationElement back, CondensationRelationTuning tuning)
    {
        if (front == back) return new("同灵根", tuning.SameDensity, tuning.SameVolume, 0f);
        if (CondensationCoreState.Produces(front, back)) return new("相生", tuning.GenerateDensity, 0f, tuning.GenerateImpurity);
        if (CondensationCoreState.Controls(front, back)) return new("相克", 0f, tuning.ControlVolume, tuning.ControlImpurity);
        return null;
    }

    private void ApplyChange(CondensationChange change, string reason)
    {
        if (MathF.Abs(change.Volume) > .0001f || MathF.Abs(change.Density) > .0001f || MathF.Abs(change.Impurity) > .0001f) AddLog($"{reason}：体积 {change.Volume:+0.00;-0.00;0.00}，密度 {change.Density:+0.00;-0.00;0.00}，杂质 {change.Impurity:+0.00;-0.00;0.00}。", change.Impurity > 0f);
        if (change.CausedFailure) AddLog("杂质达到 100，牌局立即停止。", true);
    }

    private void Finish(bool success, string message)
    {
        _finished = true; _awaitingSwap = false; _settled = true; AddLog(message, !success);
        if (_target is not null && success && !_testMode) _target.complete_condensation(_core.Volume, _core.Density, _core.Impurity, QualityName());
        CondensationFinished?.Invoke(_target!, success);
        Render();
    }

    private string QualityName() => _core.Impurity <= 12f && _core.Density >= 1.45f ? "上品云息灵核" : _core.Impurity <= 28f ? "中品云息灵核" : "下品云息灵核";

    private void Render()
    {
        if (_target is null) return;
        _round!.Text = $"当前回合{Mathf.Clamp(_roundNumber, 0, 8)}/总回合8";
        if (_stats is not null) _stats.Text = $"体积 {_core.Volume:0.00}　密度 {_core.Density:0.00}　总量 {_core.TotalMass:0.00}　杂质 {_core.Impurity:0.00}";
        if (_deckInfo is not null) _deckInfo.Text = $"抽牌堆：{_deck.Count}　弃牌堆：{_discard.Count}";
        if (_drawPileButton is not null) _drawPileButton.Text = $"抽牌堆\n{_deck.Count}";
        if (_discardPileButton is not null) _discardPileButton.Text = $"弃牌堆\n{_discard.Count}";
        if (_logLabel is not null) _logLabel.Text = string.Join("\n", _log);
        RenderRowOnly();
    }

    private int SeatAt(Vector2 pointer)
    {
        for (var i = 0; i < _seatButtons.Count; i++)
        {
            var button = _seatButtons[i];
            if (GodotObject.IsInstanceValid(button) && !button.Disabled && button.GetGlobalRect().HasPoint(pointer)) return i;
        }
        return -1;
    }

    private void ResetDrag()
    {
        _dragSeat = -1; _dragTargetSeat = -1; _dragStart = Vector2.Zero; _dragMoved = false;
        UpdateDragVisuals();
    }

    private void UpdateDragVisuals()
    {
        if (_rowBox is null) return;
        for (var i = 0; i < _rowBox.GetChildCount(); i++)
        {
            if (_rowBox.GetChild(i) is not Control slot || slot.GetChildCount() == 0) continue;
            if (slot.GetChild(0) is SkillCard visual) visual.set_selected(_dragMoved && i == _dragTargetSeat && i != _dragSeat);
        }
    }

    private void RenderRowOnly()
    {
        if (_rowBox is null) return;
        foreach (var child in _rowBox.GetChildren()) child.QueueFree(); _seatButtons.Clear();
        var availableWidth = _rowBox.Size.X;
        if (availableWidth < 480f) availableWidth = Size.X - 500f;
        availableWidth = Mathf.Max(720f, availableWidth);
        var cardWidth = Mathf.Clamp((availableWidth - 30f) / 6f, 132f, 225f);
        var cardHeight = cardWidth * 1.5f;
        for (var i = 0; i < _row.Count; i++)
        {
            var card = _row[i];
            _rowBox.AddChild(BuildCondensationCard(card, i, new Vector2(cardWidth, cardHeight), true, i == _firstSwap || i == _secondSwap));
        }
    }

    private void AddLog(string text, bool bad)
    {
        _log.Add((bad ? "【险】" : "【记】") + text); if (_log.Count > 120) _log.RemoveAt(0); if (_logLabel is not null) _logLabel.Text = string.Join("\n", _log); if (_logScroll is not null) CallDeferred(nameof(ScrollLogToEnd));
    }

    private void ScrollLogToEnd()
    {
        if (_logScroll is not null && GodotObject.IsInstanceValid(_logScroll)) _logScroll.ScrollVertical = int.MaxValue;
    }

    private static List<CondensationElement> EffectiveRoots(Character target)
    {
        var result = new List<CondensationElement>(); var weights = target.entity_data is HumanData data ? data.root_weights : [];
        for (var i = 0; i < 5; i++) if (weights.Count > i && weights[i].AsInt32() > 0) result.Add((CondensationElement)(i + 1));
        return result.Count > 0 ? result : [CondensationElement.Water];
    }

    private CondensationElement WeightedRoot(Character target, List<CondensationElement> roots)
    {
        var values = roots.Select(root => Math.Max(1, target.entity_data is HumanData data && data.root_weights.Count > (int)root - 1 ? data.root_weights[(int)root - 1].AsInt32() : 1)).ToArray();
        var total = values.Sum(); var roll = _random.RandiRange(1, total); for (var i = 0; i < values.Length; i++) if ((roll -= values[i]) <= 0) return roots[i]; return roots[^1];
    }

    private string WeightedStat(Character target)
    {
        var stats = new[] { ("physique", target.physique), ("intelligence", target.intelligence), ("agility", target.agility), ("soul", target.soul) };
        var weights = stats.Select(item => Math.Max(1, Mathf.FloorToInt(item.Item2 / 2f))).ToArray(); var roll = _random.RandiRange(1, weights.Sum());
        for (var i = 0; i < weights.Length; i++) if ((roll -= weights[i]) <= 0) return stats[i].Item1; return stats[^1].Item1;
    }

    private static List<CondensationCard> RootCards(CondensationElement element) => element switch
    {
        CondensationElement.Water => [Card("静露", element, "结算时密度增加 0.03，杂质减少 1；若结算方向前方紧邻牌属水或木，保留至下一回合。", new() { Density = .03f, Impurity = -1f, KeepIf = "Water" }), Card("汇流", element, "结算时体积增加 0.04。", new() { Volume = .04f }), Card("回澜", element, "结算时执行一次标准扩张 0.16。", new() { Expand = .16f })],
        CondensationElement.Wood => [Card("抽芽", element, "结算时体积增加 0.06。", new() { Volume = .06f }), Card("承露", element, "结算时体积增加 0.03；若结算方向前方紧邻牌属木或火，额外体积增加 0.04。", new() { Volume = .03f, NeighborVolume = .04f }), Card("繁枝", element, "结算时体积增加 0.02，并保留至下一回合。", new() { Volume = .02f, KeepAlways = true })],
        CondensationElement.Fire => [Card("焚浊", element, "结算时密度增加 0.02，杂质减少 4。", new() { Density = .02f, Impurity = -4f }), Card("炽息", element, "结算时密度增加 0.05，杂质减少 1。", new() { Density = .05f, Impurity = -1f }), Card("淬火", element, "结算时执行一次标准压缩 0.20，杂质减少 2。", new() { Compress = .20f, Impurity = -2f })],
        CondensationElement.Earth => [Card("厚载", element, "结算时体积增加 0.08，杂质增加 1。", new() { Volume = .08f, Impurity = 1f }), Card("沉露", element, "若本牌位于第六位，结算时体积增加 0.12；否则体积增加 0.03。", new() { Volume = .03f, KeepIfSixth = true }), Card("定壤", element, "结算时若体积低于 1.20，体积增加 0.07；否则密度增加 0.03。", new() { EarthBalance = true })],
        CondensationElement.Metal => [Card("镇列", element, "未参与本轮对调时，本牌触发的关系杂质减少 1；参与对调时，本牌触发的关系密度额外增加 0.03、杂质额外增加 1。", new() { MetalOrder = true }), Card("鸣铎", element, "结算时若结算方向前方紧邻牌属水，金生水的杂质减少 1、密度额外增加 0.01；否则密度增加 0.03。", new() { MetalWater = true, Density = .03f }), Card("敛锋", element, "结算时执行一次标准压缩 0.16。", new() { Compress = .16f })],
        _ => []
    };

    private static CondensationCard StrongCard(CondensationElement element, bool dangerous) => Card(dangerous ? "怒潮" : $"凝{element}", element, dangerous ? "结算时执行一次标准扩张 0.50，杂质增加 3。" : "结算时密度增加 0.04，体积增加 0.04，杂质减少 4。", dangerous ? new() { Expand = .50f, Impurity = 3f } : new() { Density = .04f, Volume = .04f, Impurity = -4f });

    private static Dictionary<string, List<CondensationCard>> StatCards(Character target) => new()
    {
        ["physique"] = [Card("持势", CondensationElement.None, "结算时保留至下一回合；体魄达到 8 时，下回合第一次关系效果翻倍。", new() { BodyHold = true }), Card("承浊", CondensationElement.None, "结算时令本轮下一次关系增加的杂质减少 1。", new())],
        ["intelligence"] = [Card("观源", CondensationElement.None, "结算时整理抽牌堆顶部的牌，为后续回合调整牌序。", new())],
        ["agility"] = [Card("先引", CondensationElement.None, "本牌优先结算；将后续牌列的一张牌引入当前牌列第一位。", new() { FirstPriority = true }, CardTiming.Priority)],
        ["soul"] = [Card("定观", CondensationElement.None, "结算时令本轮下一次云候造成的杂质增加改为 0。", new()), Card("叠映", CondensationElement.None, "若本轮此前已触发灵根关系，下一次灵根关系结算两次。", new() { SameEcho = true })]
    };

    private static List<CondensationCard> MethodCards() =>
    [
        Card("依脉", CondensationElement.None, "本牌开始结算时，视为结算方向后方最近灵根牌所属灵根。", new() { Depend = true }),
        Card("引脉", CondensationElement.None, "本牌不参与邻位关系；使两张最近灵根牌之间额外判断一次关系。", new() { Bridge = true }),
        Card("余响", CondensationElement.None, "本牌滞后结算：重复本轮最后一次灵根关系。", new() { EchoLast = true }, CardTiming.Late),
        Card("重帘", CondensationElement.None, "若本轮已触发同灵根关系，下一次同灵根关系结算两次。", new() { SameEcho = true })
    ];

    private static CondensationCard Card(string name, CondensationElement element, string text, CardEffect effect, CardTiming timing = CardTiming.Normal) => new() { Name = name, Element = element, Text = text, Effect = effect, Timing = timing };
    private static void Shuffle<T>(IList<T> list) { for (var i = list.Count - 1; i > 0; i--) { var j = Random.Shared.Next(i + 1); (list[i], list[j]) = (list[j], list[i]); } }
    private static StyleBoxFlat FrameStyle() => new() { BgColor = new Color(1f, 1f, 1f, 0f), BorderColor = new Color(1f, 1f, 1f, 0f), ContentMarginLeft = 26, ContentMarginRight = 26, ContentMarginTop = 18, ContentMarginBottom = 18 };
    private static StyleBoxFlat SideStyle() => new() { BgColor = new Color(.96f, .84f, .63f, .08f), BorderColor = new Color(.42f, .28f, .14f, .24f), ContentMarginLeft = 12, ContentMarginRight = 12, ContentMarginTop = 10, ContentMarginBottom = 10 };
    private static StyleBoxFlat BrowserStyle() => new() { BgColor = new Color(.86f, .74f, .54f, .16f), BorderColor = new Color(.46f, .30f, .14f, .55f), BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1, CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10, CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10, ContentMarginLeft = 22, ContentMarginRight = 22, ContentMarginTop = 18, ContentMarginBottom = 18 };
    private static StyleBoxFlat PlaceholderStyle() => new() { BgColor = new Color(.96f, .84f, .63f, .04f), BorderColor = new Color(.46f, .30f, .14f, .30f), BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1, CornerRadiusTopLeft = 7, CornerRadiusTopRight = 7, CornerRadiusBottomLeft = 7, CornerRadiusBottomRight = 7 };
    private static StyleBoxFlat EffectStyle() => new() { BgColor = new Color(.96f, .84f, .63f, .06f), BorderColor = new Color(.46f, .30f, .14f, .28f), BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1, CornerRadiusTopLeft = 14, CornerRadiusTopRight = 14, CornerRadiusBottomLeft = 14, CornerRadiusBottomRight = 14 };
    private static StyleBoxFlat CloudFrameStyle() => new()
    {
        BgColor = new Color(1f, 1f, 1f, 0f),
        BorderColor = new Color(.39f, .63f, .56f, .78f),
        BorderWidthLeft = 2, BorderWidthRight = 2, BorderWidthTop = 2, BorderWidthBottom = 2,
        CornerRadiusTopLeft = 14, CornerRadiusTopRight = 14, CornerRadiusBottomLeft = 14, CornerRadiusBottomRight = 14
    };
    private static StyleBoxFlat CloudFrameInnerStyle() => new()
    {
        BgColor = new Color(1f, 1f, 1f, 0f),
        BorderColor = new Color(.76f, .61f, .34f, .62f),
        BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
        CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10, CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10
    };
    private static Label Label(string text, int size, Color color)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", new Color("f4f0df"));
        label.AddThemeColorOverride("font_shadow_color", new Color(0.04f, 0.03f, 0.02f, .9f));
        label.AddThemeConstantOverride("shadow_offset_x", 1);
        label.AddThemeConstantOverride("shadow_offset_y", 1);
        return label;
    }
    private static StyleBoxFlat PileStyle() => new() { BgColor = new Color(.82f, .68f, .44f, .24f), BorderColor = new Color(.46f, .30f, .14f, .64f), BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1, CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6 };
    private static StyleBoxFlat PileHoverStyle() => new() { BgColor = new Color(.88f, .74f, .50f, .40f), BorderColor = new Color(.46f, .30f, .14f, .84f), BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1, CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6 };
    private static void StyleButton(Button button, bool primary = false)
    {
        var normal = new StyleBoxFlat { BgColor = primary ? new Color(.72f, .50f, .23f, .82f) : new Color(.82f, .68f, .44f, .24f), BorderColor = new Color(.46f, .30f, .14f, .72f), BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1, CornerRadiusTopLeft = 7, CornerRadiusTopRight = 7, CornerRadiusBottomLeft = 7, CornerRadiusBottomRight = 7 };
        var hover = new StyleBoxFlat { BgColor = primary ? new Color(.80f, .60f, .30f, .92f) : new Color(.88f, .74f, .50f, .40f), BorderColor = new Color(.46f, .30f, .14f, .88f), BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1, CornerRadiusTopLeft = 7, CornerRadiusTopRight = 7, CornerRadiusBottomLeft = 7, CornerRadiusBottomRight = 7 };
        var disabled = new StyleBoxFlat { BgColor = new Color(.70f, .62f, .48f, .16f), BorderColor = new Color(.46f, .36f, .24f, .30f), BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1, CornerRadiusTopLeft = 7, CornerRadiusTopRight = 7, CornerRadiusBottomLeft = 7, CornerRadiusBottomRight = 7 };
        button.AddThemeStyleboxOverride("normal", normal); button.AddThemeStyleboxOverride("hover", hover); button.AddThemeStyleboxOverride("pressed", hover); button.AddThemeStyleboxOverride("disabled", disabled); button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty()); button.AddThemeColorOverride("font_color", new Color("f4f0df")); button.AddThemeColorOverride("font_hover_color", Colors.White); button.AddThemeColorOverride("font_disabled_color", new Color("d8d0b8")); button.AddThemeColorOverride("font_shadow_color", new Color(0.04f, 0.03f, 0.02f, .9f)); button.AddThemeConstantOverride("shadow_offset_x", 1); button.AddThemeConstantOverride("shadow_offset_y", 1);
    }
    private static void StyleCard(Button button, CondensationElement element, bool selected, bool resolved)
    {
        var color = element switch { CondensationElement.Water => new Color(.70f, .84f, .90f), CondensationElement.Wood => new Color(.73f, .86f, .73f), CondensationElement.Fire => new Color(.92f, .70f, .62f), CondensationElement.Earth => new Color(.84f, .76f, .58f), CondensationElement.Metal => new Color(.78f, .80f, .87f), _ => new Color(.84f, .80f, .70f) };
        if (selected) color = new Color(.96f, .78f, .34f); if (resolved) color = new Color(.48f, .50f, .47f);
        button.AddThemeStyleboxOverride("normal", new StyleBoxFlat { BgColor = color, BorderColor = selected ? new Color("f4c04e") : new Color("d1b27a"), BorderWidthLeft = selected ? 4 : 2, BorderWidthRight = selected ? 4 : 2, BorderWidthTop = selected ? 4 : 2, BorderWidthBottom = selected ? 4 : 2, CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10, CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10, ContentMarginLeft = 14, ContentMarginRight = 14, ContentMarginTop = 14, ContentMarginBottom = 14 });
        button.AddThemeStyleboxOverride("hover", new StyleBoxFlat { BgColor = color.Lightened(.08f), BorderColor = new Color("f5d274"), BorderWidthLeft = selected ? 4 : 3, BorderWidthRight = selected ? 4 : 3, BorderWidthTop = selected ? 4 : 3, BorderWidthBottom = selected ? 4 : 3, CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10, CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10, ContentMarginLeft = 14, ContentMarginRight = 14, ContentMarginTop = 14, ContentMarginBottom = 14 });
        button.AddThemeColorOverride("font_color", new Color("201b15")); button.AddThemeColorOverride("font_hover_color", new Color("14110d")); button.AddThemeColorOverride("font_disabled_color", new Color("4c473d"));
    }
}

public readonly record struct CondensationRelationResult(string Type, float Density, float Volume, float Impurity);
