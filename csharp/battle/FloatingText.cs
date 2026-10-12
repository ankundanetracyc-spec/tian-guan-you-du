using Godot;

/// <summary>战斗中的飘字动画。</summary>
public partial class FloatingText : Node2D
{
    private Label? _label;
    private static readonly SystemFont TextFont = new() { FontWeight = 700 };
    private Node2D? _actionOwner;
    private BattleManager? _actionManager;
    private float _actionRise;

    public override void _Ready()
    {
        AddToGroup("battle_action_visuals");
        _label = GetNodeOrNull<Label>("Label") ?? new Label();
        if (_label.GetParent() is null) AddChild(_label);
        // 战斗数字需要在角色和场景背景之上仍能一眼读清。
        _label.AddThemeFontSizeOverride("font_size", 52);
        _label.AddThemeColorOverride("font_outline_color", Colors.Black);
        _label.AddThemeConstantOverride("outline_size", 10);
        _label.AddThemeColorOverride("font_shadow_color",new Color(0,0,0,.9f));
        _label.AddThemeConstantOverride("shadow_offset_x",2);
        _label.AddThemeConstantOverride("shadow_offset_y",3);
        _label.MouseFilter=Control.MouseFilterEnum.Ignore;
        _label.AddThemeFontOverride("font", TextFont);
        ZIndex = 1000;
    }

    public void show_text(string text, Color color, int fontSize = 52)
    {
        if (_label is null) return;
        ConfigureLabel(text, color, fontSize);
        // 倍速只推进技能队列；伤害飘字仍按同一可读时长演出。
        float duration(float value) => value;
        var tween = CreateTween().SetPauseMode(Tween.TweenPauseMode.Stop);
        tween.TweenProperty(this, "position", Position + new Vector2(0, -112) * Scale, duration(1.55f)).SetTrans(Tween.TransitionType.Sine);
        // 前半段保持完全不透明，避免读到一半就消失。
        tween.Parallel().TweenProperty(_label, "modulate:a", 0.0f, duration(.48f)).SetDelay(duration(1.07f));
        tween.TweenCallback(Callable.From(QueueFree));
    }

    public void show_action_name(Node2D owner, string text)
    {
        if (_label is null) return;
        _actionOwner = owner;
        _actionManager = GetTree().GetFirstNodeInGroup("battle_mgr") as BattleManager;
        ConfigureLabel(text, BattleUiArt.Text, 32);
        // 长招式名适当缩字，保持在本人的头顶区域内。
        if (_label.Size.X > 240) ConfigureLabel(text, BattleUiArt.Text, Math.Max(20, Mathf.FloorToInt(32 * 240 / _label.Size.X)));
        FollowActionOwner();
        var tween = CreateTween().SetPauseMode(Tween.TweenPauseMode.Stop);
        tween.TweenMethod(Callable.From<float>(rise => _actionRise = rise), 0f, 10f, 1.2f);
        tween.Parallel().TweenProperty(_label, "modulate:a", 0f, .3f).SetDelay(.9f);
        tween.TweenCallback(Callable.From(QueueFree));
    }

    public override void _Process(double delta)
    {
        if (_actionOwner is null) return;
        if (!IsInstanceValid(_actionOwner) || !_actionOwner.IsInsideTree()) { QueueFree(); return; }
        FollowActionOwner();
    }

    private void FollowActionOwner()
    {
        if (_actionOwner is null) return;
        var viewport = GetViewport();
        var scale = BattleUiArt.Scale(viewport);
        var height = IsInstanceValid(_actionManager) ? _actionManager!.get_unit_display_height() : 240 * scale;
        // 头顶姓名生命条及状态图标上方独立一行；出招移动时同步跟随。
        var screen = _actionOwner.GetGlobalTransformWithCanvas().Origin - new Vector2(0, height + (128 + _actionRise) * scale);
        var half = (_label?.Size ?? Vector2.Zero) * scale * .5f;
        var size = viewport.GetVisibleRect().Size;
        screen.X = Mathf.Clamp(screen.X, half.X + 8, Mathf.Max(half.X + 8, size.X - half.X - 8));
        screen.Y = Mathf.Max(screen.Y, half.Y + 8);
        GlobalPosition = viewport.GetCanvasTransform().AffineInverse() * screen;
    }

    private void ConfigureLabel(string text, Color color, int fontSize)
    {
        if (_label is null) return;
        _label.Text = text;
        _label.AddThemeFontSizeOverride("font_size", fontSize);
        _label.AddThemeConstantOverride("outline_size", fontSize < 52 ? 6 : 10);
        _label.AddThemeColorOverride("font_color", color);
        _label.ResetSize();
        _label.Position = -_label.Size * .5f;
    }
}
