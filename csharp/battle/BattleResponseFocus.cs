using Godot;
using System.Threading.Tasks;

/// <summary>仅表现：应对命中前的定格、局部特写和淡出。绝不推进时序或修改战斗数值。</summary>
public partial class BattleResponseFocus : CanvasLayer
{
    private const float IntroSeconds = .30f, ReleaseHoldSeconds = .16f, ReturnSeconds = .18f;
    private readonly TaskCompletionSource<bool> _intro = new();
    private readonly TaskCompletionSource<bool> _outro = new();
    private readonly List<BattlePaperDoll> _frozenActors = [];
    private readonly List<(Node node, ProcessModeEnum mode)> _frozenVisuals = [];
    private BattlePaperDoll? _doll;
    private Vector2 _originalScale, _originalPosition, _anchorOffset;
    private int _originalZ;
    private ShaderMaterial? _shade;
    private Control? _caption;
    private Node2D? _brackets;
    private float _elapsed;
    private bool _returning, _released, _restored;

    public Task IntroFinished => _intro.Task;

    public static BattleResponseFocus Begin(BattleManager manager, Node2D responder, ActiveSkillData skill)
    {
        var focus = new BattleResponseFocus { Name = "ResponseFocus", Layer = 0, ProcessMode = ProcessModeEnum.Pausable };
        foreach (var actor in manager.all_units.OfType<Node>().Where(GodotObject.IsInstanceValid))
        {
            if (actor.GetNodeOrNull<BattlePaperDoll>("BattlePaperDoll") is not { } doll) continue;
            doll.set_response_time_stop(true);
            focus._frozenActors.Add(doll);
        }
        // 暂停已有的墨痕与飘字，避免人物静止时来袭特效仍穿过身体。
        foreach (var visual in manager.GetTree().GetNodesInGroup("battle_action_visuals"))
        {
            focus._frozenVisuals.Add((visual, visual.ProcessMode));
            visual.ProcessMode = ProcessModeEnum.Disabled;
        }
        manager.GetTree().CurrentScene.AddChild(focus);
        focus.AddToGroup("battle_action_visuals");
        focus.Build(responder, skill);
        return focus;
    }

    private void Build(Node2D responder, ActiveSkillData skill)
    {
        _doll = responder.GetNodeOrNull<BattlePaperDoll>("BattlePaperDoll");
        if (_doll is not null)
        {
            _originalScale = _doll.Scale; _originalPosition = _doll.Position; _originalZ = _doll.ZIndex;
            _anchorOffset = _doll.combat_anchor() - _doll.GlobalPosition;
            // 转成父节点坐标，放大时固定胸口，角色脚点不会突然上窜。
            _anchorOffset = responder.GlobalTransform.BasisXformInv(_anchorOffset);
            _doll.ZIndex = 1500;
        }
        var viewport = GetViewport().GetVisibleRect().Size;
        var point = _doll is not null ? _doll.GetGlobalTransformWithCanvas() * _doll.ToLocal(_doll.combat_anchor())
            : responder.GetGlobalTransformWithCanvas().Origin + new Vector2(0, -100);
        var scale = BattleUiArt.Scale(GetViewport());
        _shade = new ShaderMaterial { Shader = new Shader { Code = """
            shader_type canvas_item;
            uniform vec2 focus_point;
            uniform vec2 view_size;
            uniform float radius;
            uniform float strength = 0.0;
            void fragment() {
                float d = length((SCREEN_UV - focus_point) * view_size);
                float outside = smoothstep(radius * 0.55, radius * 1.7, d);
                COLOR = vec4(0.025, 0.045, 0.044, outside * strength * 0.38);
            }
            """ } };
        _shade.SetShaderParameter("focus_point", point / viewport);
        _shade.SetShaderParameter("view_size", viewport);
        _shade.SetShaderParameter("radius", Mathf.Max(80, BattleInkCombatVfx.Anchor(responder).DistanceTo(responder.GlobalPosition) * GetViewport().GetCanvasTransform().Scale.Y * .8f));
        AddChild(new ColorRect { Size = viewport, Material = _shade, MouseFilter = Control.MouseFilterEnum.Ignore });
        _brackets = new Node2D { Position = point };
        AddChild(_brackets);
        foreach (var side in new[] { -1, 1 })
        {
            var line = new Line2D { Width = 2.4f * scale, DefaultColor = new Color("b9cec1"), Antialiased = true };
            for (var i = 0; i <= 24; i++)
            {
                var a = -.76f + i / 24f * 1.52f;
                line.AddPoint(new Vector2(side * Mathf.Cos(a) * 106, Mathf.Sin(a) * 124) * scale);
            }
            _brackets.AddChild(line);
        }
        _caption = new VBoxContainer { Position = new(Mathf.Clamp(point.X + 92 * scale, 12, Mathf.Max(12, viewport.X - 232 * scale)), Mathf.Clamp(point.Y - 48 * scale, 90 * scale, viewport.Y - 130 * scale)), Size = new(220 * scale, 90 * scale), MouseFilter = Control.MouseFilterEnum.Ignore };
        var title = new Label { Text = "应 对", MouseFilter = Control.MouseFilterEnum.Ignore };
        title.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(32 * scale));
        title.AddThemeColorOverride("font_color", new Color("e4e5d4"));
        title.AddThemeColorOverride("font_outline_color", new Color("183330"));
        title.AddThemeConstantOverride("outline_size", 7);
        _caption.AddChild(title);
        var name = new Label { Text = skill.skill_name, MouseFilter = Control.MouseFilterEnum.Ignore };
        name.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(20 * scale));
        name.AddThemeColorOverride("font_outline_color", new Color("183330"));
        name.AddThemeConstantOverride("outline_size", 5);
        _caption.AddChild(name); AddChild(_caption);
        SetFocus(1f, 0f);
    }

    public void ReleaseTimeStop()
    {
        if (_released) return;
        _released = true;
        foreach (var actor in _frozenActors.Where(GodotObject.IsInstanceValid)) actor.set_response_time_stop(false);
        foreach (var (node, mode) in _frozenVisuals)
            if (GodotObject.IsInstanceValid(node)) node.ProcessMode = mode;
    }

    public Task FinishPresentation(bool triggered)
    {
        ReleaseTimeStop();
        _returning = true; _elapsed = 0;
        if (!triggered && _caption is not null) _caption.Hide();
        return _outro.Task;
    }

    public override void _Process(double delta)
    {
        _elapsed += (float)delta;
        if (!_returning)
        {
            var t = Mathf.Clamp(_elapsed / .13f, 0, 1);
            var eased = 1 - Mathf.Pow(1 - t, 3);
            SetFocus(1 + .10f * eased, eased);
            if (_elapsed >= IntroSeconds) _intro.TrySetResult(true);
            return;
        }
        var fade = Mathf.Clamp((_elapsed - ReleaseHoldSeconds) / ReturnSeconds, 0, 1);
        var remaining = 1 - fade * fade * (3 - 2 * fade);
        SetFocus(1 + .10f * remaining, remaining);
        if (fade < 1) return;
        Restore();
        _outro.TrySetResult(true);
        QueueFree();
    }

    private void SetFocus(float zoom, float strength)
    {
        if (GodotObject.IsInstanceValid(_doll))
        {
            _doll!.Scale = _originalScale * zoom;
            _doll.Position = _originalPosition + _anchorOffset * (1 - zoom);
        }
        _shade?.SetShaderParameter("strength", strength);
        if (_caption is not null) _caption.Modulate = new Color(1, 1, 1, strength);
        if (_brackets is not null) { _brackets.Modulate = new Color(1, 1, 1, strength * .8f); _brackets.Scale = Vector2.One * zoom; }
    }

    private void Restore()
    {
        if (_restored) return;
        _restored = true; ReleaseTimeStop();
        if (!GodotObject.IsInstanceValid(_doll)) return;
        _doll!.Scale = _originalScale; _doll.Position = _originalPosition; _doll.ZIndex = _originalZ;
    }

    public void CancelPresentation()
    {
        Restore();
        if (IsInsideTree() && !IsQueuedForDeletion()) QueueFree();
    }

    public override void _ExitTree()
    {
        Restore(); _intro.TrySetResult(false); _outro.TrySetResult(false);
    }
}
