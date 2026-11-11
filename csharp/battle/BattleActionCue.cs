using Godot;

/// <summary>人物附近的倾斜招式提示。只负责显示，不参与行动判定或结算。</summary>
public partial class BattleActionCue : Node2D
{
    private const float CueWidth = 276f;
    private const float CueHeight = 70f;
    private const float TiltRadians = .44f;
    private const float EnterSeconds = .14f;
    private const float DefaultHoldSeconds = .62f;
    private const float DefaultFadeSeconds = .22f;

    private static readonly SystemFont ActionFont = new() { FontWeight = 700 };
    private Control? _screenVisual;
    private PanelContainer? _panel;
    private TextureRect? _icon;
    private Label? _name;
    private BattleManager? _manager;
    private Node2D? _owner;
    private bool _active;
    private bool _manualFade;
    private float _age;
    private float _holdSeconds = DefaultHoldSeconds;
    private float _fadeSeconds = DefaultFadeSeconds;
    private float _manualFadeStarted;
    private float _side = -1f;
    private float _baseRotation;

    /// <summary>在角色节点下获取或创建唯一的提示组件。</summary>
    public static BattleActionCue AttachTo(Node2D owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (owner.GetNodeOrNull<BattleActionCue>(nameof(BattleActionCue)) is { } existing)
            return existing;

        var cue = new BattleActionCue { Name = nameof(BattleActionCue) };
        owner.AddChild(cue);
        return cue;
    }

    public override void _Ready()
    {
        _owner = GetParent() as Node2D;
        _manager = GetTree().GetFirstNodeInGroup("battle_mgr") as BattleManager;
        EnsureVisual();
        SetProcess(false);
    }

    /// <summary>按招式现有名称与图标显示提示；holdSeconds 后自动淡出并隐藏。</summary>
    public void ShowSkill(ActiveSkillData skill, float holdSeconds = DefaultHoldSeconds)
    {
        if (skill is null) { Clear(); return; }
        ShowAction(skill.skill_name, skill.icon_path, holdSeconds);
    }

    /// <summary>显示指定名称和现有资源图标，可由非招式动作复用。</summary>
    public void ShowAction(string actionName, string iconPath, float holdSeconds = DefaultHoldSeconds)
    {
        if (string.IsNullOrWhiteSpace(actionName) || !GodotObject.IsInstanceValid(_owner))
        { Clear(); return; }

        EnsureVisual();
        if (_screenVisual is null || _panel is null || _name is null || _icon is null) return;

        _name.Text = actionName.Trim();
        _name.AddThemeFontSizeOverride("font_size", _name.Text.Length > 9 ? 18 : _name.Text.Length > 7 ? 20 : 23);
        _icon.Texture = string.IsNullOrWhiteSpace(iconPath) ? null : BattleBoardView.Icon(iconPath);
        _icon.Visible = _icon.Texture is not null;

        _side = IsPlayerSide() ? -1f : 1f;
        _baseRotation = _side < 0 ? TiltRadians : -TiltRadians;
        _panel.Rotation = _baseRotation;
        _active = true;
        _manualFade = false;
        _age = 0;
        _holdSeconds = Mathf.Max(.12f, holdSeconds);
        _fadeSeconds = DefaultFadeSeconds;
        _screenVisual.Modulate = Colors.White;
        _screenVisual.Visible = true;
        SetProcess(true);
        UpdateScreenPlacement(0, .94f, new Vector2(-_side * 18f, 10f));
    }

    /// <summary>提前淡出；不影响正在执行的行动或招式。</summary>
    public void FadeOut(float durationSeconds = DefaultFadeSeconds)
    {
        if (!_active) return;
        _manualFade = true;
        _manualFadeStarted = _age;
        _fadeSeconds = Mathf.Max(.01f, durationSeconds);
    }

    /// <summary>立即隐藏提示；组件可留在角色节点上供下一招复用。</summary>
    public void Clear()
    {
        _active = false;
        _manualFade = false;
        if (GodotObject.IsInstanceValid(_screenVisual))
        {
            _screenVisual!.Visible = false;
            _screenVisual.Modulate = Colors.White;
            _screenVisual.Scale = Vector2.One;
            _screenVisual.Rotation = 0;
        }
        SetProcess(false);
    }

    public override void _Process(double delta)
    {
        if (!_active || !GodotObject.IsInstanceValid(_owner) || !_owner!.IsInsideTree())
        { Clear(); return; }

        _age += (float)delta;
        var entrance = Mathf.Clamp(_age / EnterSeconds, 0, 1);
        var easedEntrance = 1f - Mathf.Pow(1f - entrance, 3f);
        var fadeProgress = _manualFade
            ? Mathf.Clamp((_age - _manualFadeStarted) / _fadeSeconds, 0, 1)
            : Mathf.Clamp((_age - _holdSeconds) / _fadeSeconds, 0, 1);
        var alpha = Mathf.Min(easedEntrance, 1f - fadeProgress);
        var entryScale = Mathf.Lerp(.94f, 1f, easedEntrance) + .05f * Mathf.Sin(easedEntrance * Mathf.Pi);
        var offset = new Vector2(-_side * 18f * (1f - easedEntrance), 10f * (1f - easedEntrance));
        UpdateScreenPlacement(alpha, entryScale, offset);

        if (fadeProgress >= 1f) Clear();
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(_screenVisual) && !_screenVisual!.IsQueuedForDeletion())
            _screenVisual.QueueFree();
    }

    private void EnsureVisual()
    {
        if (GodotObject.IsInstanceValid(_screenVisual)) return;
        if (!IsInsideTree()) return;

        var feedback = BattlePresentationLayers.FeedbackLayer(this);
        _screenVisual = new Control
        {
            Name = "ActionCueScreenVisual",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ProcessMode = ProcessModeEnum.Pausable,
            ZIndex = BattlePresentationLayers.Numbers - 100,
            ZAsRelative = false,
            Visible = false
        };
        _screenVisual.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
        feedback.AddChild(_screenVisual);

        var width = CueWidth;
        var height = CueHeight;
        var panel = new PanelContainer
        {
            Name = "CuePanel",
            Position = new Vector2(-width * .5f, -height * .5f),
            Size = new Vector2(width, height),
            PivotOffset = new Vector2(width * .5f, height * .5f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ProcessMode = ProcessModeEnum.Pausable
        };
        var plate = new StyleBoxFlat
        {
            BgColor = new Color(.105f, .137f, .125f, .97f),
            BorderColor = new Color("96886d"),
            ShadowColor = new Color(0, 0, 0, .34f),
            ShadowSize = 4,
            ShadowOffset = new Vector2(2, 3),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 5,
            CornerRadiusTopRight = 5,
            CornerRadiusBottomRight = 5,
            CornerRadiusBottomLeft = 5
        };
        plate.ContentMarginLeft = 13;
        plate.ContentMarginRight = 16;
        plate.ContentMarginTop = 8;
        plate.ContentMarginBottom = 8;
        panel.AddThemeStyleboxOverride("panel", plate);
        _screenVisual.AddChild(panel);
        _panel = panel;

        var row = new HBoxContainer
        {
            Name = "CueContents",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        row.AddThemeConstantOverride("separation", 10);
        panel.AddChild(row);

        var accent = new ColorRect
        {
            Name = "ActionAccent",
            Color = new Color("a18d68"),
            CustomMinimumSize = new Vector2(3, 42),
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        row.AddChild(accent);

        _icon = new TextureRect
        {
            Name = "ActionIcon",
            CustomMinimumSize = new Vector2(46, 46),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        row.AddChild(_icon);

        var divider = new ColorRect
        {
            Name = "ActionDivider",
            Color = new Color(.49f, .45f, .37f, .72f),
            CustomMinimumSize = new Vector2(1, 38),
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        row.AddChild(divider);

        _name = new Label
        {
            Name = "ActionName",
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _name.AddThemeFontOverride("font", ActionFont);
        _name.AddThemeColorOverride("font_color", BattleUiArt.Text);
        _name.AddThemeColorOverride("font_outline_color", new Color(.04f, .05f, .045f, .9f));
        _name.AddThemeConstantOverride("outline_size", 2);
        row.AddChild(_name);
    }

    private void UpdateScreenPlacement(float alpha, float entryScale, Vector2 entryOffset)
    {
        if (_screenVisual is null || _panel is null || _owner is null || !GodotObject.IsInstanceValid(_owner)) return;
        var viewport = GetViewport();
        var scale = BattleUiArt.Scale(viewport);
        var visibleSize = viewport.GetVisibleRect().Size;
        var rect = GodotObject.IsInstanceValid(_manager)
            ? _manager!.get_unit_screen_rect(_owner)
            : FallbackUnitRect(_owner, scale);
        _panel.Rotation = _baseRotation;

        var cosine = Mathf.Abs(Mathf.Cos(TiltRadians));
        var sine = Mathf.Abs(Mathf.Sin(TiltRadians));
        var halfWidth = (CueWidth * cosine + CueHeight * sine) * .5f * scale;
        var halfHeight = (CueWidth * sine + CueHeight * cosine) * .5f * scale;
        var gap = 12f * scale;
        var centerX = _side < 0
            ? rect.Position.X - halfWidth - gap
            : rect.End.X + halfWidth + gap;
        var centerY = rect.Position.Y + 48f * scale;

        var origin = BattleUiArt.Origin(viewport);
        var minX = halfWidth + 8f * scale;
        var maxX = Mathf.Max(minX, visibleSize.X - halfWidth - 8f * scale);
        var minY = origin.Y + 96f * scale + halfHeight;
        var maxY = Mathf.Max(minY, visibleSize.Y - 108f * scale - halfHeight);
        centerX = Mathf.Clamp(centerX, minX, maxX);
        centerY = Mathf.Clamp(centerY, minY, maxY);

        var ownBounds = rect.Grow(4f * scale);
        var cueBounds = new Rect2(new Vector2(centerX, centerY) - new Vector2(halfWidth, halfHeight),
            new Vector2(halfWidth * 2, halfHeight * 2));
        if (cueBounds.Intersects(ownBounds))
        {
            var alternateX = _side < 0
                ? rect.End.X + halfWidth + gap
                : rect.Position.X - halfWidth - gap;
            alternateX = Mathf.Clamp(alternateX, minX, maxX);
            var alternateBounds = new Rect2(new Vector2(alternateX, centerY) - new Vector2(halfWidth, halfHeight),
                new Vector2(halfWidth * 2, halfHeight * 2));
            if (!alternateBounds.Intersects(ownBounds))
            {
                centerX = alternateX;
                _panel.Rotation = -_baseRotation;
            }
            else
            {
                var above = rect.Position.Y - halfHeight - gap;
                var below = rect.End.Y + halfHeight + gap;
                centerY = above >= minY ? above : Mathf.Min(below, maxY);
                centerY = Mathf.Clamp(centerY, minY, maxY);
            }
        }

        _screenVisual.Position = new Vector2(centerX, centerY) + entryOffset * scale;
        _screenVisual.Scale = Vector2.One * scale * entryScale;
        _screenVisual.Modulate = new Color(1, 1, 1, alpha);
    }

    private bool IsPlayerSide() => _owner is not null
        && _owner.Get("faction").AsInt32() == (int)WorldEntity.Faction.PLAYER;

    private Rect2 FallbackUnitRect(Node2D owner, float scale)
    {
        var origin = owner.GetGlobalTransformWithCanvas().Origin;
        var height = 160f * scale;
        var width = height * .72f;
        return new Rect2(origin - new Vector2(width * .5f, height), new Vector2(width, height));
    }
}
