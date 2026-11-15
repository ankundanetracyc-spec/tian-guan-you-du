using Godot;

/// <summary>Persistent, body-fitted blue ward. Reads shield only; never changes combat state.</summary>
public partial class BattleShieldAura : Node2D
{
    private const string ShaderPath = "res://shaders/battle_mana_shield.gdshader";
    private static Shader? _shieldShader;
    private static ImageTexture? _whiteTexture;
    private Creature? _owner;
    private Node2D? _previewSubject;
    private Sprite2D? _surface;
    private ShaderMaterial? _material;
    private int _shield;
    private bool _preview, _active;
    private float _elapsed, _opacity, _impactElapsed = -1;
    private Vector2 _dimensions = new(250, 350);

    public static void AttachTo(Node owner)
    {
        if (owner is not Creature creature || owner.GetNodeOrNull<BattleShieldAura>("BattleShieldAura") is not null) return;
        owner.AddChild(new BattleShieldAura { Name = "BattleShieldAura", _owner = creature });
    }

    public static BattleShieldAura CreatePreview(int shield) => new()
    {
        Name = "ShieldPreview", _preview = true, _shield = Mathf.Max(0, shield), _active = shield > 0
    };

    public void SetPreviewSubject(Node2D subject)
    {
        _previewSubject = subject;
        // The preview has the same moving parent as a real Creature's shield.
        if (IsInsideTree() && GetParent() != subject) Reparent(subject, false);
        FitToSubject();
    }
    public void set_preview_shield(int amount)
    {
        if (!_preview) return;
        _shield = Mathf.Max(0, amount); _active = _shield > 0;
    }

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Pausable;
        ZIndex = BattlePresentationLayers.Effects;
        ZAsRelative = false;
        _shieldShader ??= GD.Load<Shader>(ShaderPath);
        if (_shieldShader is not null)
        {
            _material = new ShaderMaterial { Shader = _shieldShader };
            _material.SetShaderParameter("ink_noise", BattleInkRibbon.SharedNoise);
            _material.SetShaderParameter("impact_progress", -1f);
            _surface = new Sprite2D { Name = "ManaShieldSurface", Texture = WhiteTexture(), Material = _material,
                TextureFilter = TextureFilterEnum.Linear, Visible = false };
            AddChild(_surface);
        }
        FitToSubject();
    }

    public override void _EnterTree() => RenderingServer.FramePreDraw += SyncBeforeDraw;
    public override void _ExitTree() => RenderingServer.FramePreDraw -= SyncBeforeDraw;

    private void SyncBeforeDraw()
    {
        // Tweens can move the doll after _Process. Sample its final render transform too.
        FitToSubject();
        if (_surface is not null) _surface.Scale = _dimensions / (256f * .82f);
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        if (!_preview)
        {
            if (!IsInstanceValid(_owner)) { QueueFree(); return; }
            _shield = Mathf.Max(0, _owner!.Get("shield").AsInt32());
            var alive = _owner.Get("current_health").AsInt32() > 0;
            _active = _shield > 0 && alive;
            if (!alive) _opacity = 0;
        }
        FitToSubject();
        _elapsed += dt;
        _opacity = Mathf.MoveToward(_opacity, _active ? 1 : 0, dt / (_active ? .24f : .34f));
        if (_impactElapsed >= 0)
        {
            _impactElapsed += dt;
            if (_impactElapsed >= .58f) _impactElapsed = -1;
        }
        if (_surface is not null)
        {
            _surface.Visible = _opacity > .001f;
            _surface.Scale = _dimensions / (256f * .82f);
        }
        if (_material is not null)
        {
            _material.SetShaderParameter("effect_time", _elapsed);
            _material.SetShaderParameter("visibility", _opacity);
            // Amount changes pigment gently, never bubble size or a new numerical rule.
            _material.SetShaderParameter("shield_strength", .82f + Mathf.Min(_shield, 18) / 18f * .18f);
            _material.SetShaderParameter("impact_progress", _impactElapsed < 0 ? -1 : _impactElapsed / .58f);
        }
        else QueueRedraw();
    }

    private void FitToSubject()
    {
        var subject = _preview ? _previewSubject : _owner;
        if (!IsInstanceValid(subject) || GetParent() is not Node2D parent) return;
        var body = subject!.GetNodeOrNull<BattlePaperDoll>("BattlePaperDoll")?.shield_world_rect()
            ?? new Rect2(subject.GlobalPosition + new Vector2(-95, -260), new Vector2(190, 260));
        Position = parent.ToLocal(body.GetCenter());
        var scale = parent.GlobalScale.Abs();
        _dimensions = body.Size
            / new Vector2(Mathf.Max(scale.X, .001f), Mathf.Max(scale.Y, .001f));
    }

    /// <summary>Directional contact ripple; direction points from attacker to defender.</summary>
    public void PlayImpact(Vector2 direction)
    {
        if (!_active || _material is null) return;
        var from = direction.LengthSquared() > .001f ? -direction.Normalized() : Vector2.Left;
        // Elliptical UV coordinates put the contact on the visible front, not in its centre.
        from = (from / _dimensions).Normalized();
        _impactElapsed = 0;
        _material.SetShaderParameter("impact_origin", Vector2.One * .5f + from * .395f);
        _material.SetShaderParameter("impact_progress", 0f);
    }

    public override void _Draw()
    {
        if (_surface is not null || _opacity <= .001f) return;
        var points = new Vector2[81];
        for (var i = 0; i < points.Length; i++)
            points[i] = new Vector2(Mathf.Cos(i * Mathf.Tau / 80), Mathf.Sin(i * Mathf.Tau / 80)) * _dimensions * .5f;
        DrawPolyline(points, new Color(.39f, .62f, .80f, _opacity * .40f), 2, true);
    }

    private static ImageTexture WhiteTexture()
    {
        if (_whiteTexture is not null) return _whiteTexture;
        using var image = Image.CreateEmpty(256, 256, false, Image.Format.Rgba8);
        image.Fill(new Color(1, 1, 1, 0));
        return _whiteTexture = ImageTexture.CreateFromImage(image);
    }
}
