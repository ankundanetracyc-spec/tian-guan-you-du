using Godot;

/// <summary>
/// 战斗常驻法力护盾。正常路径使用单张 Sprite + CanvasItem Shader 生成伪球面，
/// Shader 不可用时回退到少量程序化线条；只观察 Creature.shield，不参与数值结算。
/// </summary>
public partial class BattleShieldAura : Node2D
{
    private const string ShaderPath = "res://shaders/battle_mana_shield.gdshader";
    private const float ShieldSpriteScale = 0.62f;
    private static Shader? _shieldShader;
    private static ImageTexture? _whiteTexture;

    private Creature? _owner;
    private Sprite2D? _shieldVisual;
    private ShaderMaterial? _material;
    private int _shield;
    private bool _active;
    private bool _preview;
    private float _elapsed;
    private float _impactElapsed = -1f;

    public static void AttachTo(Node owner)
    {
        if (owner is not Creature creature || owner.GetNodeOrNull<BattleShieldAura>("BattleShieldAura") is not null) return;
        var aura = new BattleShieldAura
        {
            Name = "BattleShieldAura",
            Position = new Vector2(0f, -112f)
        };
        aura.Bind(creature);
        owner.AddChild(aura);
    }

    private void Bind(Creature owner) => _owner = owner;

    public static BattleShieldAura CreatePreview(int shield)
    {
        return new BattleShieldAura
        {
            Name = "ShieldPreview",
            _preview = true,
            _shield = Mathf.Max(1, shield),
            _active = true
        };
    }

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Pausable;
        ZIndex = 72;
        SetupVisual();
        SetVisualActive(_active);
    }

    public override void _Process(double delta)
    {
        _material?.SetShaderParameter("effect_time", _elapsed);
        if (_preview)
        {
            _elapsed += (float)delta;
            UpdateImpact(delta);
            return;
        }

        if (_owner is null || !GodotObject.IsInstanceValid(_owner))
        {
            QueueFree();
            return;
        }

        var nextShield = Mathf.Max(0, _owner.Get("shield").AsInt32());
        Position = _owner.ToLocal(BattleInkCombatVfx.Anchor(_owner));
        var nextActive = nextShield > 0 && _owner.Get("current_health").AsInt32() > 0;
        _elapsed += (float)delta;
        if (nextShield != _shield || nextActive != _active)
        {
            _shield = nextShield;
            _active = nextActive;
            SetVisualActive(_active);
            UpdateShaderStrength();
        }

        UpdateImpact(delta);
        if (_shieldVisual is null && _active) QueueRedraw();
    }

    /// <summary>向护盾表面注入一次受击波纹；direction 是从攻击者指向受击者的方向。</summary>
    public void PlayImpact(Vector2 direction)
    {
        if (!_active || _material is null) return;
        var hitDirection = direction.LengthSquared() < 0.001f ? Vector2.Left : direction.Normalized();
        var fromAttacker = -hitDirection;
        var origin = new Vector2(
            Mathf.Clamp(0.5f + fromAttacker.X * 0.34f, 0.10f, 0.90f),
            Mathf.Clamp(0.5f + fromAttacker.Y * 0.24f, 0.10f, 0.90f));
        _impactElapsed = 0f;
        _material.SetShaderParameter("impact_origin", origin);
        _material.SetShaderParameter("impact_progress", 0f);
    }

    public override void _Draw()
    {
        // 正常路径由 Shader 负责完整护盾；这里仅作为渲染器/资源异常时的保险层。
        if (_shieldVisual is not null || !_active) return;

        var pulse = 1f + Mathf.Sin(_elapsed * 3.2f) * 0.035f;
        var intensity = Mathf.Clamp(0.58f + Mathf.Min(_shield, 18) / 18f * 0.30f, 0.58f, 0.88f);
        var ink = new Color(0.055f, 0.13f, 0.30f, 1f);
        var outer = 64f * pulse;
        var inner = 55f * pulse;

        DrawArc(Vector2.Zero, outer, 0f, Mathf.Pi * 2f, 32, WithAlpha(ink, intensity * 0.62f), 4f, true);
        DrawArc(Vector2.Zero, inner, 0f, Mathf.Pi * 2f, 28, WithAlpha(ink.Lightened(0.22f), intensity * 0.34f), 2.5f, true);
        for (var i = 0; i < 6; i++)
        {
            var angle = i * Mathf.Pi / 3f + _elapsed * 0.12f;
            var radial = Vector2.FromAngle(angle);
            var start = radial * (outer - 3f);
            var end = radial * (outer + 10f + Mathf.Sin(_elapsed * 2f + i) * 2f);
            DrawLine(start, end, WithAlpha(ink, intensity * 0.45f), 2.3f, true);
            if (i % 2 == 0) DrawCircle(end, 2.8f, WithAlpha(ink, intensity * 0.52f));
        }
    }

    private void SetupVisual()
    {
        _shieldShader ??= GD.Load<Shader>(ShaderPath);
        if (_shieldShader is null) return;

        _material = new ShaderMaterial { Shader = _shieldShader };
        _material.SetShaderParameter("shield_tint", new Color(0.105f, 0.21f, 0.19f, 1f));
        _material.SetShaderParameter("edge_tint", new Color(0.07f, 0.14f, 0.12f, 1f));
        _material.SetShaderParameter("impact_tint", new Color(0.025f, 0.038f, 0.031f, 1f));
        _material.SetShaderParameter("ink_noise", BattleInkRibbon.SharedNoise);
        _material.SetShaderParameter("impact_origin", new Vector2(0.18f, 0.50f));
        _material.SetShaderParameter("impact_progress", -1f);

        _shieldVisual = new Sprite2D
        {
            Name = "ManaShieldSurface",
            Texture = WhiteTexture(),
            Centered = true,
            Scale = Vector2.One * ShieldSpriteScale,
            TextureFilter = CanvasItem.TextureFilterEnum.Linear,
            Material = _material
        };
        AddChild(_shieldVisual);
        UpdateShaderStrength();
    }

    private void UpdateShaderStrength()
    {
        if (_material is null) return;
        var strength = Mathf.Clamp(0.70f + Mathf.Min(_shield, 18) / 18f * 0.30f, 0.70f, 1f);
        _material.SetShaderParameter("shield_strength", strength);
    }

    private void SetVisualActive(bool active)
    {
        if (_shieldVisual is not null) _shieldVisual.Visible = active;
        if (!active)
        {
            _impactElapsed = -1f;
            _material?.SetShaderParameter("impact_progress", -1f);
        }
        else if (_shieldVisual is null)
        {
            QueueRedraw();
        }
    }

    private void UpdateImpact(double delta)
    {
        if (_impactElapsed < 0f || _material is null) return;
        _impactElapsed += (float)delta;
        var progress = Mathf.Clamp(_impactElapsed / 0.48f, 0f, 1f);
        _material.SetShaderParameter("impact_progress", progress);
        if (progress >= 1f)
        {
            _impactElapsed = -1f;
            _material.SetShaderParameter("impact_progress", -1f);
        }
    }

    private static ImageTexture WhiteTexture()
    {
        if (_whiteTexture is not null) return _whiteTexture;
        var image = Image.CreateEmpty(256, 256, false, Image.Format.Rgba8);
        // 仅作为 Shader 承载层；透明底可避免资源异常时露出白方块。
        image.Fill(new Color(1f, 1f, 1f, 0f));
        _whiteTexture = ImageTexture.CreateFromImage(image);
        return _whiteTexture;
    }

    private static Color WithAlpha(Color color, float alpha) => new(color.R, color.G, color.B, Mathf.Clamp(alpha, 0f, 1f));
}
