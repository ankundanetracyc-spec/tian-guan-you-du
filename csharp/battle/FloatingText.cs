using Godot;

/// <summary>战斗中的飘字动画。</summary>
public partial class FloatingText : Node2D
{
    private Label? _label;
    private static readonly SystemFont TextFont = new() { FontWeight = 700 };
    private Node2D? _actionOwner;
    private BattleManager? _actionManager;
    private float _actionRise;
    private Node2D? _screenVisual;
    private Vector2 _avoidance;
    private BattleUnitOverlay? _unitOverlay;

    public override void _Ready()
    {
        AddToGroup("battle_action_visuals");
        _label = GetNodeOrNull<Label>("Label") ?? new Label();
        _screenVisual=new Node2D{Name="FloatingFeedback",ZIndex=BattlePresentationLayers.Numbers,Visible=false};
        BattlePresentationLayers.FeedbackLayer(this).AddChild(_screenVisual);
        if(_label.GetParent() is null)_screenVisual.AddChild(_label);
        else _label.Reparent(_screenVisual,false);
        _actionManager=GetTree().GetFirstNodeInGroup("battle_mgr") as BattleManager;
        _unitOverlay=GetTree().GetNodesInGroup("battle_unit_overlays").OfType<BattleUnitOverlay>().FirstOrDefault();
        // 战斗数字需要在角色和场景背景之上仍能一眼读清。
        _label.AddThemeFontSizeOverride("font_size", 52);
        _label.AddThemeColorOverride("font_outline_color", Colors.Black);
        _label.AddThemeConstantOverride("outline_size", 10);
        _label.AddThemeColorOverride("font_shadow_color",new Color(0,0,0,.9f));
        _label.AddThemeConstantOverride("shadow_offset_x",2);
        _label.AddThemeConstantOverride("shadow_offset_y",3);
        _label.MouseFilter=Control.MouseFilterEnum.Ignore;
        _label.AddThemeFontOverride("font", TextFont);
        ZIndex = BattlePresentationLayers.Numbers;
        ZAsRelative = false;
    }

    public void show_text(string text, Color color, int fontSize = 52)
    {
        if (_label is null) return;
        ConfigureLabel(text, color, fontSize);
        UpdateScreenVisual(0,true);
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
        UpdateScreenVisual(0,true);
        var tween = CreateTween().SetPauseMode(Tween.TweenPauseMode.Stop);
        tween.TweenMethod(Callable.From<float>(rise => _actionRise = rise), 0f, 10f, 1.2f);
        tween.Parallel().TweenProperty(_label, "modulate:a", 0f, .3f).SetDelay(.9f);
        tween.TweenCallback(Callable.From(QueueFree));
    }

    public override void _Process(double delta)
    {
        if(_actionOwner is not null)
        {
            if (!IsInstanceValid(_actionOwner) || !_actionOwner.IsInsideTree()) { QueueFree(); return; }
            FollowActionOwner();
        }
        UpdateScreenVisual((float)delta);
    }

    public override void _ExitTree()
    {
        if(IsInstanceValid(_screenVisual)&&!_screenVisual!.IsQueuedForDeletion())_screenVisual.QueueFree();
    }

    private void UpdateScreenVisual(float delta,bool immediate=false)
    {
        if(!IsInstanceValid(_screenVisual)||_label is null)return;
        var transform=GetGlobalTransformWithCanvas();
        var point=BattleLocalFocusView.FrameUnitRect(this,new Rect2(transform.Origin,Vector2.Zero)).Position;
        var scale=transform.Scale.Abs();
        var half=_label.Size*scale*.5f;
        var safe=point;
        if(IsInstanceValid(_unitOverlay))
            foreach(var rect in _unitOverlay!.FeedbackObstacles())
                if(rect.Grow(6).Intersects(new Rect2(safe-half,half*2)))safe.Y=Mathf.Min(safe.Y,rect.Position.Y-half.Y-10);
        var target=new Vector2(0,Mathf.Min(0,safe.Y-point.Y));
        _avoidance=immediate?target:_avoidance.Lerp(target,1-Mathf.Exp(-delta*14));
        point+=_avoidance;
        var viewport=GetViewport().GetVisibleRect().Size;
        var uiScale=BattleUiArt.Scale(GetViewport());var origin=BattleUiArt.Origin(GetViewport());
        point.X=Mathf.Clamp(point.X,half.X+8,Mathf.Max(half.X+8,viewport.X-half.X-8));
        var top=origin.Y+96*uiScale+half.Y;
        var bottom=Mathf.Max(top,origin.Y+828*uiScale-half.Y);
        point.Y=Mathf.Clamp(point.Y,top,bottom);
        _screenVisual!.Position=point;_screenVisual.Scale=scale;_screenVisual.Visible=true;
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
