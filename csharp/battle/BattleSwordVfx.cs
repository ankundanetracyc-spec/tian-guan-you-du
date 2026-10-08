using Godot;

/// <summary>Routes sword attacks to the cleave and on-target wound VFX.</summary>
public static class BattleSwordVfx
{
    public static void PlayAttack(GodotObject attacker, GodotObject target, ActiveSkillData skill)
    {
        if (IsSwordSkill(skill)) BattleInkSlashEffect.PlaySwordCleave(attacker, target);
        else BattleInkSlashEffect.PlayAttack(attacker, target, skill);
    }

    public static void PlayImpact(GodotObject target, ActiveSkillData skill, bool critical, GodotObject? attacker = null)
    {
        if (IsSwordSkill(skill)) BattleInkSlashEffect.PlaySwordImpact(target, attacker, critical);
        else BattleInkSlashEffect.PlayImpactForSkill(target, skill, critical);
    }

    public static bool IsSwordSkill(ActiveSkillData skill)
    {
        if (skill.required_weapon_type == (int)Equipment.WeaponType.SWORD) return true;
        return skill.source_gongfa_name.Contains("剑")
            || skill.skill_name.Contains("剑")
            || skill.skill_name.Contains("斩");
    }
}

public partial class BattleInkSlashEffect
{
    private const string SwordCleaveTexturePath = "res://textures/battle/sword_cleave_ink_v4.png";
    private const string SwordImpactTexturePath = "res://textures/battle/sword_impact_ink_v3.png";
    private static readonly Rect2[] SwordCleaveBrushRegions =
    [
        new(0f, 0.045f, 1f, 0.46f),
        new(0f, 0.475f, 1f, 0.30f),
        new(0f, 0.81f, 1f, 0.15f)
    ];
    private static readonly Rect2[] SwordImpactBrushRegions =
    [
        new(0f, 0.065f, 1f, 0.395f),
        new(0f, 0.49f, 1f, 0.245f),
        new(0f, 0.75f, 1f, 0.19f)
    ];
    private static Shader? _swordBrushShader;
    private static Texture2D? _swordCleaveBrush;
    private static Texture2D? _swordImpactBrush;

    private void UseSwordBrushShader()
    {
        _swordBrushShader ??= GD.Load<Shader>(SwordShaderPath);
        var brush = _mode == EffectMode.SwordCleave
            ? _swordCleaveBrush ??= GD.Load<Texture2D>(SwordCleaveTexturePath)
            : _swordImpactBrush ??= GD.Load<Texture2D>(SwordImpactTexturePath);
        if (_swordBrushShader is null || _shaderMaterial is null || brush is null) return;

        var cleave = _mode == EffectMode.SwordCleave;
        _shaderMaterial.Shader = _swordBrushShader;
        _shaderMaterial.SetShaderParameter("progress", 0f);
        _shaderMaterial.SetShaderParameter("brush_texture", brush);
        _shaderMaterial.SetShaderParameter("brush_rect", BrushRegion(cleave, 0));
        _shaderMaterial.SetShaderParameter("vertical_flip", 1f);
        _shaderMaterial.SetShaderParameter("reveal_direction", 1f);
        _shaderMaterial.SetShaderParameter("ink_color", _inkColor);
        _shaderMaterial.SetShaderParameter("seed_value", _seed);
    }

    private bool CreateSwordBrushLayers(bool cleave)
    {
        _swordBrushShader ??= GD.Load<Shader>(SwordShaderPath);
        var brush = cleave
            ? _swordCleaveBrush ??= GD.Load<Texture2D>(SwordCleaveTexturePath)
            : _swordImpactBrush ??= GD.Load<Texture2D>(SwordImpactTexturePath);
        if (_swordBrushShader is null || brush is null || brush.GetWidth() <= 0 || brush.GetHeight() <= 0) return false;

        if (_shaderVisual is not null) _shaderVisual.Visible = false;

        var direction = _direction;
        var normal = _normal;
        var angle = direction.Angle();
        if (cleave)
        {
            // The brush sheet contains three variable-height marks, not equal-height rows.
            // Keep the main brush on a clean horizontal axis; direction selects left-to-right or right-to-left.
            var span = Mathf.Clamp(_length * 1.05f, 260f, 420f);
            var drift = Mathf.Clamp(_length * 0.055f, 12f, 24f);
            var sweepAngle = angle;
            AddSwordStroke(brush, true, 0, span, span * 0.28f,
                direction * -drift + normal * 1f, direction * drift + normal * 0f,
                sweepAngle, sweepAngle, 0.76f, 1.10f, 0.98f, 0.98f,
                delay: 0f, draw: 0.17f, settle: 0.07f, fade: 0.09f, revealDirection: 1f, seedOffset: 1f);
            AddSwordStroke(brush, true, 1, span * 0.98f, span * 0.17f,
                direction * -drift * 0.82f - normal * 3f, direction * drift * 0.92f - normal * 2f,
                sweepAngle + 0.015f, sweepAngle + 0.075f, 0.82f, 1.06f, 1.0f, 0.40f,
                delay: 0.045f, draw: 0.15f, settle: 0.065f, fade: 0.075f, revealDirection: 1f, seedOffset: 2f);
            AddSwordStroke(brush, true, 2, span * 0.90f, span * 0.085f,
                direction * -drift * 0.74f + normal * 4f, direction * drift + normal * 3f,
                sweepAngle - 0.02f, sweepAngle + 0.10f, 0.80f, 1.08f, 1.0f, 0.46f,
                delay: 0.075f, draw: 0.13f, settle: 0.06f, fade: 0.07f, revealDirection: 1f, seedOffset: 3f);
        }
        else
        {
            // Keep all three wound marks on the target; only the fine cuts cross at different angles.
            var hitAngle = angle - 0.38f;
            AddSwordStroke(brush, false, 0, 184f, 52f,
                -direction * 5f + normal * 2f, direction * 3f - normal * 1f,
                hitAngle, hitAngle + 0.04f, 0.70f, 1.12f, 1.0f, 0.98f,
                delay: 0f, draw: 0.11f, settle: 0.075f, fade: 0.10f, revealDirection: 1f, seedOffset: 11f);
            AddSwordStroke(brush, false, 1, 156f, 32f,
                -direction * 3f - normal * 3f, direction * 2f - normal * 4f,
                angle + 0.27f, angle + 0.31f, 0.68f, 1.10f, 1.0f, 0.62f,
                delay: 0.035f, draw: 0.105f, settle: 0.07f, fade: 0.09f, revealDirection: -1f, seedOffset: 12f);
            AddSwordStroke(brush, false, 2, 136f, 21f,
                -direction * 6f + normal * 7f, direction * 2f + normal * 5f,
                hitAngle - 0.04f, hitAngle + 0.025f, 0.66f, 1.12f, 1.0f, 0.48f,
                delay: 0.065f, draw: 0.095f, settle: 0.065f, fade: 0.085f, revealDirection: 1f, seedOffset: 13f);
        }

        return true;
    }

    private void AddSwordStroke(
        Texture2D brush,
        bool cleave,
        int row,
        float width,
        float height,
        Vector2 startPosition,
        Vector2 finishPosition,
        float startRotation,
        float finishRotation,
        float startScale,
        float peakScale,
        float finishScale,
        float opacity,
        float delay,
        float draw,
        float settle,
        float fade,
        float revealDirection,
        float seedOffset)
    {
        if (_swordBrushShader is null) return;

        var material = new ShaderMaterial { Shader = _swordBrushShader };
        material.SetShaderParameter("brush_texture", brush);
        material.SetShaderParameter("brush_rect", BrushRegion(cleave, row));
        material.SetShaderParameter("vertical_flip", 1f);
        material.SetShaderParameter("reveal_direction", revealDirection);
        material.SetShaderParameter("progress", 0f);
        material.SetShaderParameter("ink_color", _inkColor);
        material.SetShaderParameter("seed_value", _seed + seedOffset);

        var spriteRotation = cleave ? startRotation : startRotation - _direction.Angle();
        var sprite = new Sprite2D
        {
            Name = $"SwordInkStroke{row + 1}",
            Texture = WhiteTexture(),
            Centered = true,
            TextureFilter = CanvasItem.TextureFilterEnum.Linear,
            Material = material,
            Position = startPosition,
            Rotation = spriteRotation,
            Scale = new Vector2(width / WhiteTextureSize, height / WhiteTextureSize) * startScale,
            Modulate = new Color(1f, 1f, 1f, 0f),
            ZIndex = row
        };
        AddChild(sprite);

        var finalRotation = cleave ? finishRotation : finishRotation - _direction.Angle();
        var tween = CreateTween();
        tween.TweenMethod(
                Callable.From<float>(progress => material.SetShaderParameter("progress", progress)),
                0f,
                1f,
                draw)
            .SetDelay(delay)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
        tween.Parallel()
            .TweenProperty(sprite, "modulate", new Color(1f, 1f, 1f, opacity), 0.055f)
            .SetDelay(delay)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
        tween.Parallel()
            .TweenProperty(sprite, "scale", new Vector2(width / WhiteTextureSize, height / WhiteTextureSize) * peakScale, 0.055f)
            .SetDelay(delay)
            .SetTrans(Tween.TransitionType.Back)
            .SetEase(Tween.EaseType.Out);
        tween.TweenProperty(sprite, "position", finishPosition, settle)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.Out);
        tween.Parallel().TweenProperty(sprite, "rotation", finalRotation, settle)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.Out);
        tween.Parallel().TweenProperty(sprite, "scale", new Vector2(width / WhiteTextureSize, height / WhiteTextureSize) * finishScale, settle)
            .SetTrans(Tween.TransitionType.Back)
            .SetEase(Tween.EaseType.Out);
        tween.TweenProperty(sprite, "modulate:a", 0f, fade)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.In);
    }

    private static Vector4 BrushRegion(bool cleave, int row)
    {
        var regions = cleave ? SwordCleaveBrushRegions : SwordImpactBrushRegions;
        var region = regions[Mathf.Clamp(row, 0, regions.Length - 1)];
        return new Vector4(region.Position.X, region.Position.Y, region.Size.X, region.Size.Y);
    }

    public static void PlaySwordCleave(GodotObject attacker, GodotObject target)
    {
        if (attacker is not Node2D attackerNode || target is not Node2D targetNode) return;
        var scene = CurrentScene();
        if (scene is null) return;

        var attackerAnchor = CombatAnchor(attackerNode);
        var targetAnchor = CombatAnchor(targetNode);
        var offset = targetAnchor - attackerAnchor;
        // Sword cleaves are designed as screen-horizontal sweeps; only their facing changes.
        var direction = offset.X < 0f ? Vector2.Left : Vector2.Right;
        var effect = CreateEffect(scene);
        if (effect is null) return;

        var travel = offset.Length();
        effect.GlobalPosition = (attackerAnchor + targetAnchor) * 0.5f;
        effect._mode = EffectMode.SwordCleave;
        effect._direction = direction;
        effect._normal = new Vector2(-direction.Y, direction.X);
        effect._length = travel;
        effect._duration = SwordCleaveDuration;
        _swordSequence++;
        effect._seed = (attacker.GetInstanceId() ^ target.GetInstanceId() ^ ((ulong)_swordSequence * 131UL)) % 997UL;
        effect._inkColor = new Color(0.012f, 0.012f, 0.012f, 0.98f);
        effect._elapsed = 0f;
        effect.ConfigureShaderVisual();
        if (!effect.CreateSwordBrushLayers(cleave: true))
        {
            effect.UseSwordBrushShader();
            if (effect._shaderVisual is not null)
            {
                effect._shaderVisual.Scale = new Vector2(360f / WhiteTextureSize, 196f / WhiteTextureSize);
                effect._shaderVisual.Rotation = direction.Angle();
            }
        }
        effect.QueueRedraw();
        SpawnInkDroplets(effect, 3, 125f);
    }

    public static void PlaySwordImpact(GodotObject target, GodotObject? attacker = null, bool critical = false)
    {
        if (target is not Node2D targetNode) return;
        var scene = CurrentScene();
        if (scene is null) return;

        var direction = attacker is Node2D attackerNode
            ? (CombatAnchor(targetNode) - CombatAnchor(attackerNode)).Normalized()
            : Vector2.Right;
        if (direction.LengthSquared() < 0.01f) direction = Vector2.Right;
        var effect = CreateEffect(scene);
        if (effect is null) return;

        effect.GlobalPosition = CombatAnchor(targetNode);
        effect._mode = EffectMode.SwordImpact;
        effect._direction = direction;
        effect._normal = new Vector2(-direction.Y, direction.X);
        effect._duration = SwordImpactDuration;
        _swordSequence++;
        effect._seed = (target.GetInstanceId() ^ ((ulong)_swordSequence * 97UL)) % 997UL;
        effect._inkColor = new Color(0.012f, 0.012f, 0.012f, 0.98f);
        effect._critical = critical;
        effect._elapsed = 0f;
        effect.ConfigureShaderVisual();
        if (!effect.CreateSwordBrushLayers(cleave: false))
        {
            effect.UseSwordBrushShader();
            if (effect._shaderVisual is not null)
            {
                effect._shaderVisual.Scale = new Vector2(250f / WhiteTextureSize, 136f / WhiteTextureSize);
                effect._shaderVisual.Rotation = direction.Angle();
            }
        }
        effect.QueueRedraw();
        SpawnInkDroplets(effect, 3, 88f);
    }

    private static readonly Vector2[] InkDropletShape =
    [
        new(0f, -1f), new(0.42f, -0.52f), new(0.78f, -0.08f), new(0.74f, 0.42f),
        new(0.43f, 0.82f), new(0f, 1f), new(-0.43f, 0.82f), new(-0.74f, 0.42f),
        new(-0.78f, -0.08f), new(-0.42f, -0.52f)
    ];

    private static void SpawnInkDroplets(BattleInkSlashEffect effect, int count, float radius)
    {
        var random = new RandomNumberGenerator { Seed = (ulong)(effect._seed * 65537f) + 1UL };
        var direction = effect._direction;
        var normal = new Vector2(-direction.Y, direction.X);

        for (var i = 0; i < count; i++)
        {
            var along = effect._mode == EffectMode.SwordCleave
                ? random.RandfRange(radius * 0.24f, radius * 0.78f)
                : random.RandfRange(-radius * 0.52f, radius * 0.52f);
            var lateral = random.RandfRange(-radius * 0.48f, radius * 0.48f);
            var offset = direction * along + normal * lateral;
            var outward = offset.LengthSquared() > 0.01f ? offset.Normalized() : normal;
            var velocity = outward * random.RandfRange(58f, 118f)
                + direction * random.RandfRange(8f, 28f);
            var size = random.RandfRange(2.5f, 4.3f);
            var particle = new Polygon2D
            {
                Name = "InkDroplet",
                Polygon = InkDropletShape,
                Color = new Color(0.008f, 0.008f, 0.008f, random.RandfRange(0.78f, 0.96f)),
                Position = offset,
                Rotation = velocity.Angle() + Mathf.Pi * 0.5f,
                Scale = new Vector2(size * 0.82f, size * 1.18f),
                ZIndex = 1
            };
            effect.AddChild(particle);

            var lifetime = random.RandfRange(0.20f, 0.32f);
            var tween = particle.CreateTween();
            tween.TweenProperty(particle, "position", offset + velocity * lifetime, lifetime)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.Out);
            tween.Parallel().TweenProperty(particle, "modulate", new Color(1f, 1f, 1f, 0f), lifetime);
            tween.Parallel().TweenProperty(particle, "scale", new Vector2(0.15f, 0.15f), lifetime);
        }
    }
}
