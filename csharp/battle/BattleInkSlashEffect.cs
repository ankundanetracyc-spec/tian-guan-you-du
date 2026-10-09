using Godot;

/// <summary>
/// 战斗水墨表现：正常路径使用单张 CanvasItem Shader 生成斩痕、拳锋、命中和墨散，
/// 渲染资源异常时回退到少量程序化绘制；按实际帧时间推进，不读取或修改战斗数值。
/// </summary>
public partial class BattleInkSlashEffect : Node2D
{
    private const string CombatShaderPath = "res://shaders/battle_ink_combat.gdshader";
    private const string BrushShaderPath = "res://shaders/battle_ink_brush.gdshader";
    private const float WhiteTextureSize = 256f;
    private const int MaxConcurrentEffects = 12;
    private const float SlashDuration = 0.42f;
    private const float ImpactDuration = 0.38f;
    private const float PunchDuration = 0.28f;
    private const float PunchImpactDuration = 0.34f;
    private const float SwordCleaveDuration = 0.60f;
    private const float SwordImpactDuration = 0.56f;
    private const float DeathDuration = 0.90f;
    private static int _activeEffects;
    private static uint _punchSequence;
    private static uint _swordSequence;
    private static Shader? _combatShader;
    private static Shader? _brushShader;
    private static ImageTexture? _whiteTexture;

    // Values match battle_ink_combat.gdshader, including fallback modes.
    private enum EffectMode { Slash = 0, Punch = 1, Impact = 2, PunchImpact = 3, Death = 4, SwordCleave = 5, SwordImpact = 6 }

    private EffectMode _mode;
    private Vector2 _direction = Vector2.Right;
    private Vector2 _normal = Vector2.Up;
    private float _length;
    private float _elapsed;
    private float _duration;
    private float _seed;
    private Color _inkColor;
    private bool _critical;
    private bool _evaded;
    private bool _registered;
    private Sprite2D? _shaderVisual;
    private BattleInkRibbon? _inkRibbon;
    private ShaderMaterial? _shaderMaterial;
    private readonly Vector2[] _wetRibbon = new Vector2[36];
    private readonly Vector2[] _bodyRibbon = new Vector2[36];
    private readonly Vector2[] _dryRibbon = new Vector2[36];
    private readonly Vector2[] _dryStroke = new Vector2[12];

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Pausable;
        ZIndex = 80;
        SetupShaderVisual();
    }

    public override void _Process(double delta)
    {
        _elapsed += (float)delta;
        _inkRibbon?.SetProgress(Mathf.Clamp(_elapsed / Mathf.Max(_duration, 0.001f), 0f, 1f), _elapsed);
        if (_shaderMaterial is not null)
        {
            var progress = Mathf.Clamp(_elapsed / Mathf.Max(_duration, 0.001f), 0f, 1f);
            _shaderMaterial.SetShaderParameter("progress", progress);
        }
        QueueRedraw();
        if (_elapsed >= _duration) QueueFree();
    }

    public override void _Draw()
    {
        // 正常路径由单张 CanvasItem Shader 绘制；程序化版本保留作渲染资源异常时的回退。
        if (_shaderVisual is not null || _inkRibbon is not null) return;

        switch (_mode)
        {
            case EffectMode.Slash:
                DrawSlash();
                break;
            case EffectMode.Punch:
                DrawPunch();
                break;
            case EffectMode.PunchImpact:
                DrawPunchImpact();
                break;
            case EffectMode.Death:
                DrawDeath();
                break;
            default:
                DrawImpact();
                break;
        }
    }

    public override void _ExitTree()
    {
        if (!_registered) return;
        _registered = false;
        _activeEffects = Mathf.Max(0, _activeEffects - 1);
    }

    public static void PlaySlash(GodotObject attacker, GodotObject target, ActiveSkillData skill)
    {
        BattleInkCombatVfx.PlayAttack(attacker, target, skill);
    }

    public static void PlayAttack(GodotObject attacker, GodotObject target, ActiveSkillData skill)
    {
        BattleInkCombatVfx.PlayAttack(attacker, target, skill);
    }

    public static bool IsPunchSkill(ActiveSkillData skill)
    {
        return skill.skill_name is "发劲" or "痛击" or "蓄势重击";
    }

    private static void PlayPunch(GodotObject attacker, GodotObject target, ActiveSkillData skill)
    {
        if (attacker is not Node2D attackerNode || target is not Node2D targetNode) return;
        var scene = CurrentScene();
        if (scene is null) return;

        var start = CombatAnchor(attackerNode);
        var end = CombatAnchor(targetNode);
        var offset = end - start;
        var length = Mathf.Max(offset.Length(), 140f);
        var direction = offset.LengthSquared() < 0.01f ? Vector2.Right : offset.Normalized();
        var effect = CreateEffect(scene);
        if (effect is null) return;

        effect.GlobalPosition = start;
        effect._mode = EffectMode.Punch;
        effect._direction = direction;
        effect._normal = new Vector2(-direction.Y, direction.X);
        effect._length = length;
        effect._duration = PunchDuration;
        _punchSequence++;
        effect._seed = (attacker.GetInstanceId() ^ target.GetInstanceId() ^ 311UL ^ ((ulong)_punchSequence * 197UL)) % 997UL;
        effect._inkColor = InkColor(skill);
        effect._elapsed = 0f;
        effect.ConfigureShaderVisual();
        effect.QueueRedraw();
    }

    public static void PlayImpact(GodotObject target, bool critical, bool evaded = false, GodotObject? attacker = null)
    {
        BattleInkCombatVfx.PlayImpact(target, attacker: attacker, critical: critical, evaded: evaded);
    }

    public static void PlayImpactForSkill(GodotObject target, ActiveSkillData skill, bool critical, GodotObject? attacker = null, int segment = 0)
    {
        BattleInkCombatVfx.PlayImpact(target, skill, attacker, critical, segment: segment);
    }

    private static void PlayPunchImpact(GodotObject target, bool critical, Color inkColor)
    {
        if (target is not Node2D targetNode) return;
        var scene = CurrentScene();
        if (scene is null) return;

        var effect = CreateEffect(scene);
        if (effect is null) return;

        effect.GlobalPosition = CombatAnchor(targetNode);
        effect._mode = EffectMode.PunchImpact;
        effect._duration = PunchImpactDuration;
        effect._seed = (target.GetInstanceId() * 17UL + 53UL) % 997UL;
        effect._inkColor = inkColor;
        effect._critical = critical;
        effect._elapsed = 0f;
        effect.ConfigureShaderVisual();
        effect.QueueRedraw();
    }

    public static void PlayDeath(GodotObject target)
    {
        BattleInkCombatVfx.PlayDeath(target);
    }

    private static BattleInkSlashEffect? CreateEffect(Node scene)
    {
        if (_activeEffects >= MaxConcurrentEffects) return null;
        var effect = new BattleInkSlashEffect();
        scene.AddChild(effect);
        _activeEffects++;
        effect._registered = true;
        return effect;
    }

    private void SetupShaderVisual()
    {
        _combatShader ??= GD.Load<Shader>(CombatShaderPath);
        if (_combatShader is null) return;

        _shaderMaterial = new ShaderMaterial { Shader = _combatShader };
        _shaderVisual = new Sprite2D
        {
            Name = "CombatInkShader",
            Texture = WhiteTexture(),
            Centered = true,
            TextureFilter = CanvasItem.TextureFilterEnum.Linear,
            Material = _shaderMaterial
        };
        AddChild(_shaderVisual);
    }

    private void ConfigureShaderVisual()
    {
        if (_shaderVisual is null || _shaderMaterial is null) return;

        var isRibbon = _mode is EffectMode.Slash or EffectMode.Punch;
        var width = _mode switch
        {
            EffectMode.Slash => _length + 220f,
            EffectMode.Punch => _length + 150f,
            EffectMode.Death => 286f,
            _ => 238f
        };
        var height = _mode switch
        {
            EffectMode.Slash => 184f,
            EffectMode.Punch => 164f,
            EffectMode.Death => 286f,
            _ => 238f
        };

        _shaderVisual.Position = isRibbon ? _direction * (_length * 0.5f) : Vector2.Zero;
        _shaderVisual.Rotation = isRibbon ? _direction.Angle() : 0f;
        _shaderVisual.Scale = new Vector2(width / WhiteTextureSize, height / WhiteTextureSize);
        _shaderVisual.Visible = true;

        _shaderMaterial.SetShaderParameter("effect_mode", (float)_mode);
        _shaderMaterial.SetShaderParameter("progress", 0f);
        _shaderMaterial.SetShaderParameter("ink_color", _inkColor);
        _shaderMaterial.SetShaderParameter("accent_color", _critical
            ? new Color(0.74f, 0.20f, 0.10f, 1f)
            : _inkColor.Lightened(0.30f));
        _shaderMaterial.SetShaderParameter("seed_value", _seed);
        _shaderMaterial.SetShaderParameter("critical", _critical ? 1f : 0f);
        _shaderMaterial.SetShaderParameter("evaded", _evaded ? 1f : 0f);

        if (_mode is EffectMode.SwordCleave or EffectMode.SwordImpact)
        {
            _shaderVisual.Visible = false;
            _inkRibbon = new BattleInkRibbon { Name = "LivingInkRibbon" };
            AddChild(_inkRibbon);
            _inkRibbon.Configure(_mode == EffectMode.SwordCleave ? Mathf.Clamp(_length * 1.12f, 300f, 440f) : (_critical ? 226f : 190f),
                _seed, new Color(0.035f, 0.047f, 0.043f, 0.98f), _mode == EffectMode.SwordImpact, _critical, _direction);
            _inkRibbon.SetProgress(0f, 0f);
            return;
        }

        if (_mode is EffectMode.SwordCleave or EffectMode.SwordImpact or EffectMode.Impact or EffectMode.PunchImpact)
        {
            _brushShader ??= GD.Load<Shader>(BrushShaderPath);
            if (_brushShader is null) return;
            _shaderMaterial.Shader = _brushShader;
            _shaderMaterial.SetShaderParameter("effect_mode", _mode switch
            {
                EffectMode.SwordCleave => 0f,
                EffectMode.SwordImpact => 1f,
                EffectMode.Impact => 2f,
                _ => 3f
            });
            if (_mode == EffectMode.SwordCleave)
                _shaderVisual.Scale = new Vector2(Mathf.Clamp(_length * 1.20f, 300f, 490f), 200f) / WhiteTextureSize;
            else if (_mode == EffectMode.SwordImpact)
                _shaderVisual.Scale = new Vector2(_critical ? 282f : 246f, 164f) / WhiteTextureSize;
            else
                _shaderVisual.Scale = Vector2.One * (_critical ? 266f : 220f) / WhiteTextureSize;
            _shaderVisual.Rotation = _direction.Angle();
            if (_mode == EffectMode.SwordCleave)
            {
                _shaderVisual.Rotation = 0f;
                _shaderVisual.FlipH = _direction.X < 0f;
            }
        }
    }

    private static ImageTexture WhiteTexture()
    {
        if (_whiteTexture is not null) return _whiteTexture;
        var image = Image.CreateEmpty(256, 256, false, Image.Format.Rgba8);
        // 仅作为 Shader 承载层；透明底可避免 Shader 编译异常时露出白方块遮挡战场。
        image.Fill(new Color(1f, 1f, 1f, 0f));
        _whiteTexture = ImageTexture.CreateFromImage(image);
        return _whiteTexture;
    }

    private void DrawSlash()
    {
        var t = Mathf.Clamp(_elapsed / Mathf.Max(_duration, 0.001f), 0f, 1f);
        var travel = SmoothStep(0f, 0.55f, t);
        var head = -76f + travel * (_length + 150f);
        var tail = head - (_length * 0.72f + 116f);
        var fade = 1f - SmoothStep(0.70f, 1f, t);
        var wetColor = WithAlpha(_inkColor, 0.20f * fade);
        var bodyColor = WithAlpha(_inkColor, 0.68f * fade);
        var dryColor = WithAlpha(_inkColor.Lightened(0.18f), 0.82f * fade);

        DrawRibbon(tail - 7f, head + 11f, 50f, 0.07f, wetColor, 0f, _wetRibbon);
        DrawRibbon(tail, head, 34f, 0.10f, bodyColor, 0.25f, _bodyRibbon);
        DrawRibbon(tail + 8f, head - 8f, 15f, 0.02f, dryColor, 0.52f, _dryRibbon);
        DrawDryStroke(tail + 12f, head - 15f, 4.5f, WithAlpha(_inkColor.Lightened(0.34f), 0.72f * fade));

        // 少量断墨跟随斩痕，不创建粒子节点；每帧只是几次轻量圆形绘制。
        for (var i = 0; i < 7; i++)
        {
            var u = Hash(i + 21f);
            var distance = Mathf.Lerp(tail + 20f, head - 12f, u);
            var drift = _normal * Mathf.Sin(_seed * 0.013f + i * 2.1f) * (12f + u * 18f);
            var radius = 2.2f + Hash(i + 47f) * 4.8f;
            var alpha = (0.18f + 0.24f * (1f - u)) * fade;
            DrawCircle(_direction * distance + drift, radius, WithAlpha(_inkColor, alpha));
        }
    }

    private void DrawPunch()
    {
        var t = Mathf.Clamp(_elapsed / Mathf.Max(_duration, 0.001f), 0f, 1f);
        var windup = 1f - SmoothStep(0f, 0.12f, t);
        var launch = SmoothStep(0.08f, 0.56f, t);
        var strike = 1f - Mathf.Pow(1f - launch, 3f);
        var snap = SmoothStep(0.14f, 0.26f, t) * (1f - SmoothStep(0.34f, 0.58f, t));
        var recoil = SmoothStep(0.62f, 0.90f, t);
        var knuckle = -20f - windup * 9f + strike * (_length * 0.68f + 42f) - recoil * 10f;
        var tail = knuckle - Mathf.Lerp(82f, 66f, strike);
        var fade = 1f - SmoothStep(0.78f, 1f, t);
        var bodyColor = WithAlpha(_inkColor, 0.74f * fade);
        var dryColor = WithAlpha(_inkColor.Lightened(0.30f), 0.82f * fade);

        DrawRibbon(tail - 10f, knuckle + 16f, 31f, -0.13f, WithAlpha(_inkColor, 0.26f * fade), 0.02f, _wetRibbon);
        DrawRibbon(tail - 5f, knuckle + 18f, 25f + snap * 5f, 0.02f, bodyColor, 0.18f, _bodyRibbon);
        DrawRibbon(tail + 8f, knuckle + 8f, 11f, 0.12f, dryColor, 0.52f, _dryRibbon);

        var fistCenter = _direction * (knuckle + 12f) + _normal * (Mathf.Sin(t * Mathf.Pi) * 2f);
        var fistScale = 1f + snap * 0.14f;
        DrawLine(_direction * (knuckle - 42f), fistCenter - _direction * 8f, WithAlpha(_inkColor, 0.58f * fade), 18f * fistScale, true);
        DrawCircle(fistCenter, 16f * fistScale, WithAlpha(_inkColor, 0.86f * fade));
        for (var i = 0; i < 4; i++)
        {
            var finger = fistCenter + _direction * 7f + _normal * ((i - 1.5f) * 6f);
            DrawCircle(finger, 5.8f * fistScale, WithAlpha(dryColor, 0.86f * fade));
        }
        DrawCircle(fistCenter - _normal * 9f - _direction * 2f, 6.5f * fistScale, WithAlpha(_inkColor.Lightened(0.12f), 0.78f * fade));
        DrawCircle(fistCenter - _normal * 1f - _direction * 8f, 5f * fistScale, WithAlpha(_inkColor.Darkened(0.16f), 0.42f * fade));

        for (var i = 0; i < 3; i++)
        {
            var offset = (i - 1) * (8f + snap * 3f);
            var start = _direction * (knuckle - 5f) + _normal * offset;
            var end = start + _direction * (25f + i * 5f + snap * 12f);
            DrawLine(start, end, WithAlpha(dryColor, 0.82f * fade), 2.6f, true);
        }

        for (var i = 0; i < 4; i++)
        {
            var u = Hash(i + 173f);
            var distance = tail + 18f + u * Mathf.Max(30f, knuckle - tail - 24f);
            var drift = _normal * Mathf.Sin(_seed * 0.017f + i * 2.3f) * (4f + u * 12f);
            DrawCircle(_direction * distance + drift, 1.8f + Hash(i + 191f) * 3.8f, WithAlpha(_inkColor, (0.22f + u * 0.22f) * fade));
        }
    }

    private void DrawRibbon(float tail, float head, float width, float offset, Color color, float phase, Vector2[] points)
    {
        const int sampleCount = 18;
        for (var i = 0; i < sampleCount; i++)
        {
            var u = i / (float)(sampleCount - 1);
            var distance = Mathf.Lerp(tail, head, u);
            var grain = Hash(i * 1.71f + phase * 17f) - 0.5f;
            var curve = Mathf.Sin(distance * 0.022f + _seed * 0.017f) * 9f + grain * 7f;
            var envelope = 0.22f + Mathf.Sin(u * Mathf.Pi) * 0.78f;
            var localWidth = width * envelope * (0.92f + grain * 0.12f);
            var center = _direction * distance + _normal * (curve + offset * 48f);
            points[i] = center + _normal * localWidth;
            points[points.Length - 1 - i] = center - _normal * localWidth;
        }
        DrawColoredPolygon(points, color);
    }

    private void DrawDryStroke(float tail, float head, float width, Color color)
    {
        const int sampleCount = 12;
        var points = _dryStroke;
        for (var i = 0; i < sampleCount; i++)
        {
            var u = i / (float)(sampleCount - 1);
            var distance = Mathf.Lerp(tail, head, u);
            var curve = Mathf.Sin(distance * 0.025f + _seed * 0.011f) * 5f;
            points[i] = _direction * distance + _normal * curve;
        }
        DrawPolyline(points, color, width, true);
    }

    private void DrawImpact()
    {
        var t = Mathf.Clamp(_elapsed / Mathf.Max(_duration, 0.001f), 0f, 1f);
        var fade = 1f - SmoothStep(0.62f, 1f, t);
        var strength = _evaded ? 0.45f : 1f;
        var color = _critical ? new Color(0.34f, 0.075f, 0.045f, 1f) : _inkColor;
        var center = Vector2.Zero;
        var radius = 8f + t * 78f;

        DrawArc(center, radius, 0f, Mathf.Pi * 2f, 28, WithAlpha(color, 0.82f * fade * strength), 5f, true);
        DrawCircle(center, Mathf.Max(2f, 18f * (1f - t)), WithAlpha(color, 0.54f * fade * strength));

        for (var i = 0; i < 8; i++)
        {
            var angle = i * Mathf.Pi * 0.25f + (Hash(i + 71f) - 0.5f) * 0.30f;
            var direction = Vector2.FromAngle(angle);
            var inner = 18f + Hash(i + 83f) * 12f;
            var outer = inner + 22f + t * 48f * (0.58f + Hash(i + 97f) * 0.42f);
            DrawLine(direction * inner, direction * outer, WithAlpha(color, 0.58f * fade * strength), 3.2f, true);

            var splatCenter = direction * (outer + 5f);
            var splatRadius = 3f + Hash(i + 109f) * 5f;
            DrawCircle(splatCenter, splatRadius, WithAlpha(color, 0.70f * fade * strength));
            if (i % 2 == 0)
                DrawCircle(splatCenter + _normal * 8f, splatRadius * 0.42f, WithAlpha(color, 0.48f * fade * strength));
        }
    }

    private void DrawPunchImpact()
    {
        var t = Mathf.Clamp(_elapsed / Mathf.Max(_duration, 0.001f), 0f, 1f);
        var fade = 1f - SmoothStep(0.62f, 1f, t);
        var color = _critical ? new Color(0.34f, 0.075f, 0.045f, 1f) : _inkColor;
        var ring = 10f + SmoothStep(0f, 0.72f, t) * 50f;
        var core = Mathf.Max(1.5f, 17f * (1f - t));

        DrawCircle(Vector2.Zero, core, WithAlpha(color, 0.62f * fade));
        DrawArc(Vector2.Zero, ring, -0.85f, Mathf.Pi * 1.55f, 20, WithAlpha(color, 0.90f * fade), 5f, true);
        DrawArc(Vector2.Zero, ring * 0.64f, 1.2f, Mathf.Pi * 1.45f, 16, WithAlpha(color.Lightened(0.20f), 0.70f * fade), 3f, true);

        for (var i = 0; i < 5; i++)
        {
            var angle = (i - 2) * 0.34f + (Hash(i + 223f) - 0.5f) * 0.16f;
            var direction = Vector2.FromAngle(angle);
            var inner = 8f + Hash(i + 239f) * 8f;
            var outer = inner + 20f + t * 36f * (0.72f + Hash(i + 251f) * 0.28f);
            DrawLine(direction * inner, direction * outer, WithAlpha(color, 0.74f * fade), 3.4f, true);
            DrawCircle(direction * (outer + 4f), 2.5f + Hash(i + 269f) * 3.5f, WithAlpha(color, 0.66f * fade));
        }
    }

    private void DrawDeath()
    {
        var t = Mathf.Clamp(_elapsed / Mathf.Max(_duration, 0.001f), 0f, 1f);
        var spread = SmoothStep(0f, 0.68f, t);
        var fade = 1f - SmoothStep(0.48f, 1f, t);
        var coreFade = 1f - SmoothStep(0.16f, 0.72f, t);
        var color = _inkColor;

        DrawCircle(new Vector2(0f, 7f - spread * 16f), 25f - spread * 8f, WithAlpha(color, 0.34f * coreFade));
        DrawArc(Vector2.Zero, 28f + spread * 40f, -Mathf.Pi * 0.82f, Mathf.Pi * 1.56f, 24, WithAlpha(color, 0.56f * fade), 5f, true);
        DrawArc(new Vector2(0f, -10f), 17f + spread * 27f, 0.2f, Mathf.Pi * 1.66f, 18, WithAlpha(color.Lightened(0.18f), 0.42f * fade), 3f, true);

        for (var i = 0; i < 12; i++)
        {
            var angle = i * Mathf.Pi * 2f / 12f + (Hash(i + 281f) - 0.5f) * 0.28f;
            var direction = Vector2.FromAngle(angle);
            var start = 12f + Hash(i + 293f) * 18f;
            var distance = start + spread * (40f + Hash(i + 307f) * 76f);
            var drift = _normal * Mathf.Sin(_seed * 0.021f + i * 1.7f) * (4f + spread * 12f);
            var point = direction * distance + drift;
            var radius = (2.2f + Hash(i + 317f) * 4.8f) * (0.9f + spread * 0.25f);
            DrawCircle(point, radius, WithAlpha(color, (0.34f + Hash(i + 331f) * 0.30f) * fade));
            if (i < 8)
                DrawLine(direction * start, point, WithAlpha(color, 0.26f * fade), 2f + Hash(i + 347f) * 1.8f, true);
        }
    }

    private float Hash(float value)
        => Mathf.PosMod(Mathf.Sin(value * 12.9898f + _seed * 78.233f) * 43758.5453f, 1f);

    private static float SmoothStep(float from, float to, float value)
    {
        var t = Mathf.Clamp((value - from) / Mathf.Max(to - from, 0.0001f), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private static Color WithAlpha(Color color, float alpha) => new(color.R, color.G, color.B, Mathf.Clamp(alpha, 0f, 1f));

    private static Node? CurrentScene()
        => Engine.GetMainLoop() is SceneTree tree ? tree.CurrentScene : null;

    private static Vector2 CombatAnchor(Node2D unit) => BattleInkCombatVfx.Anchor(unit);

    private static Color InkColor(ActiveSkillData skill)
        => IsPunchSkill(skill)
            ? new Color(0.68f, 0.34f, 0.24f, 0.94f)
            : skill.damage_type switch
        {
            "magic" => new Color(0.07f, 0.16f, 0.24f, 0.90f),
            "soul" or "spiritual" => new Color(0.16f, 0.10f, 0.25f, 0.90f),
            "dot" => new Color(0.16f, 0.18f, 0.16f, 0.86f),
            _ => new Color(0.055f, 0.075f, 0.07f, 0.94f)
        };
}
