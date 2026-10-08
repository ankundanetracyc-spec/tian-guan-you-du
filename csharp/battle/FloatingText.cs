using Godot;

/// <summary>战斗中的飘字动画。</summary>
public partial class FloatingText : Node2D
{
    private Label? _label;

    public override void _Ready()
    {
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
        var font = new SystemFont { FontWeight = 700 };
        _label.AddThemeFontOverride("font", font);
        ZIndex = 1000;
    }

    public void show_text(string text, Color color)
    {
        if (_label is null) return;
        _label.Text = text;
        _label.AddThemeColorOverride("font_color", color);
        // 倍速只推进技能队列；伤害飘字仍按同一可读时长演出。
        float duration(float value) => value;
        var tween = CreateTween();
        tween.TweenProperty(this, "position", Position + new Vector2(0, -112), duration(1.55f)).SetTrans(Tween.TransitionType.Sine);
        // 前半段保持完全不透明，避免读到一半就消失。
        tween.Parallel().TweenProperty(_label, "modulate:a", 0.0f, duration(.48f)).SetDelay(duration(1.07f));
        tween.TweenCallback(Callable.From(QueueFree));
    }
}
