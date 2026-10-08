using Godot;

/// <summary>
/// 战斗中的角色显示。人形角色优先使用 3.8 Spine 骨骼；非 Spine 编辑器环境保留静态立绘回退。
/// </summary>
public partial class BattlePaperDoll : Node2D
{
    public const string DefaultPortraitPath = "res://textures/characters/c1_male_puppet_v2/preview/static.png";
    public const string ForestWolfPortraitPath = "res://textures/battle/forest_wolf_static_v1.png";
    public const string CharacterSpineResourcePath = "res://textures/characters/c1_male_puppet_v2/runtime/c1_male_puppet_v2.spinejson";
    public const string FemaleSpineResourcePath = "res://textures/characters/c1_female_puppet_v2/runtime/c1_female_puppet_v2.spinejson";
    public const string FemaleStaticPortraitPath = "res://textures/characters/c1_female_puppet_v2/preview/static.png";

    private Sprite2D? _portrait;
    private Node2D? _spineCharacter;
    private float _idlePhase;
    private Tween? _actionTween;
    private Vector2 _idleBasePosition = new(0, -75);
    private bool _usesSpineCharacter;
    private string _spineAnimation = "idle";
    private static bool _spineRuntimeWarningShown;

    public void Configure(BattleEntityData data)
    {
        var isForestWolf = data.entity_name.Contains("凶狼") || data.entity_name.Contains("林地狼") || data.entity_name.Contains("森林狼");
        var path = string.IsNullOrWhiteSpace(data.battle_portrait_path)
            ? (isForestWolf ? ForestWolfPortraitPath : DefaultPortraitPath)
            : data.battle_portrait_path;
        if (!ResourceLoader.Exists(path)) path = DefaultPortraitPath;

        _spineCharacter?.QueueFree();
        _spineCharacter = null;
        _usesSpineCharacter = false;
        _spineAnimation = "idle";
        if (_portrait is not null) _portrait.Visible = false;

        // 局内男女使用相同的骨骼与动作，分别加载自己的彩色无五官皮影。
        var isWolf = path == ForestWolfPortraitPath;
        var isFemale = !isWolf && data.gender == 1;
        if (!isWolf) _idleBasePosition = new Vector2(0, isFemale ? -153 : -180);
        var facesEnemy = data.faction == (int)WorldEntity.Faction.ENEMY;
        var spineFailure = "未尝试加载 Spine";
        if (!isWolf && TryCreateSpineCharacter(facesEnemy, isFemale, out spineFailure))
        {
            _usesSpineCharacter = true;
            Name = "BattlePaperDoll";
            return;
        }

        if (!isWolf && !_spineRuntimeWarningShown)
        {
            _spineRuntimeWarningShown = true;
            GD.PushError($"战斗角色未使用 Spine：{spineFailure}");
        }

        _portrait ??= CreatePortrait();
        _portrait.Visible = true;
        var fallbackPath = isFemale && ResourceLoader.Exists(FemaleStaticPortraitPath) ? FemaleStaticPortraitPath : path;
        _portrait.Texture = ResourceLoader.Load<Texture2D>(fallbackPath);
        _portrait.Hframes = 1;
        _portrait.Vframes = 1;
        _portrait.Frame = 0;
        _portrait.Modulate = Colors.White;
        _portrait.Scale = isWolf ? new Vector2(0.09f, 0.09f) : isFemale ? new Vector2(0.174f, 0.174f) : new Vector2(0.192f, 0.192f);
        _idleBasePosition = isWolf ? new Vector2(0, -42) : isFemale ? new Vector2(0, -105) : new Vector2(0, -120);

        // 人形素材面朝右、狼素材面朝左；交战双方始终朝向对手。
        _portrait.FlipH = isWolf ? !facesEnemy : facesEnemy;
        Name = "BattlePaperDoll";
    }

    public override void _Ready()
    {
        _portrait ??= CreatePortrait();
    }

    public override void _Process(double delta)
    {
        _idlePhase += (float)delta;
        var idleOffset = new Vector2(0, Mathf.Sin(_idlePhase * 1.8f) * 2.2f);

        if (_usesSpineCharacter && IsInstanceValid(_spineCharacter))
        {
            if (_spineAnimation is not "idle" and not "death_kneel" && !_spineCharacter!.Call("is_playing", 0).AsBool())
                PlaySpineAnimation("idle", true);
            _spineCharacter!.Position = _idleBasePosition + idleOffset;
            return;
        }

        if (_portrait is not null)
            _portrait.Position = _idleBasePosition + idleOffset;
    }

    public void play_attack(Vector2 direction)
    {
        if (_usesSpineCharacter) PlaySpineAnimation("attack", false);
        _actionTween?.Kill();
        Position = Vector2.Zero;
        Rotation = 0;
        _actionTween = CreateTween();
        _actionTween.SetParallel();
        _actionTween.TweenProperty(this, "position", direction * 16f, .07f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        _actionTween.TweenProperty(this, "rotation", direction.X * .06f, .07f);
        _actionTween.Chain().SetParallel();
        _actionTween.TweenProperty(this, "position", Vector2.Zero, .16f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        _actionTween.TweenProperty(this, "rotation", 0f, .16f);
    }

    public void play_hit(Vector2 direction)
    {
        if (_usesSpineCharacter) PlaySpineAnimation("hurt", false);
        _actionTween?.Kill();
        Position = Vector2.Zero;
        Modulate = Colors.White;
        _actionTween = CreateTween();
        _actionTween.SetParallel();
        _actionTween.TweenProperty(this, "position", direction * 9f, .045f);
        _actionTween.TweenProperty(this, "modulate", new Color(1f, .36f, .3f), .045f);
        _actionTween.Chain().SetParallel();
        _actionTween.TweenProperty(this, "position", Vector2.Zero, .14f).SetTrans(Tween.TransitionType.Bounce).SetEase(Tween.EaseType.Out);
        _actionTween.TweenProperty(this, "modulate", Colors.White, .14f);
    }

    public void play_death()
    {
        if (_usesSpineCharacter) PlaySpineAnimation("death_kneel", false);
    }

    private bool TryCreateSpineCharacter(bool flipX, bool isFemale, out string failureReason)
    {
        failureReason = "未知原因";
        if (!ClassDB.CanInstantiate("Spine"))
        {
            failureReason = "当前 Godot 不是 Spine 3.8 运行时";
            return false;
        }

        var resourcePath = isFemale ? FemaleSpineResourcePath : CharacterSpineResourcePath;
        var resource = ResourceLoader.Load<Resource>(resourcePath);
        if (resource is null)
        {
            failureReason = $"Spine 资源加载失败：{resourcePath}";
            return false;
        }

        if (ClassDB.Instantiate("Spine").AsGodotObject() is not Node2D spine)
        {
            failureReason = "Spine 节点创建失败";
            return false;
        }

        spine.Name = isFemale ? "FemaleShadowSpine" : "C1SpineCharacter";
        spine.Position = _idleBasePosition;
        // Spine 资源的运行时单位比旧序列帧小；完整画布不参与显示尺寸判断。
        // 按现有男女比例整体放大约 20%，让战斗角色更醒目。
        spine.Scale = isFemale ? new Vector2(0.252f, 0.252f) : new Vector2(0.288f, 0.288f);
        spine.ZIndex = 1;
        AddChild(spine);

        spine.Call("set_resource", resource);
        spine.Call("set_flip_x", flipX);
        spine.Call("set_active", true);
        // 保留皮影素材的染色皮面与刻纹；无五官由头部素材保证。
        spine.Modulate = Colors.White;
        foreach (var animation in new[] { "idle", "attack", "hurt", "death_kneel" })
        {
            if (spine.Call("get_animation_length", animation).AsSingle() <= 0f)
            {
                spine.QueueFree();
                failureReason = $"{resourcePath} 缺少 {animation} 动画";
                return false;
            }
        }
        _spineCharacter = spine;
        spine.Call("play", "idle", 1.0f, true, 0, 0.0f);
        return true;
    }

    private void PlaySpineAnimation(string animation, bool loop)
    {
        if (_spineCharacter is null || !IsInstanceValid(_spineCharacter)) return;
        _spineAnimation = animation;
        _spineCharacter.Call("play", animation, 1.0f, loop, 0, 0.0f);
    }

    private Sprite2D CreatePortrait()
    {
        var shadow = new Polygon2D
        {
            Name = "InkShadow",
            Polygon = [new Vector2(-48, 0), new Vector2(-28, -7), new Vector2(28, -7), new Vector2(48, 0), new Vector2(28, 7), new Vector2(-28, 7)],
            Color = new Color(0.05f, 0.06f, 0.06f, 0.35f),
            ZIndex = -1
        };
        AddChild(shadow);

        var portrait = new Sprite2D
        {
            Name = "Portrait",
            Centered = true,
            Scale = new Vector2(0.115f, 0.115f),
            ZIndex = 1
        };
        AddChild(portrait);
        return portrait;
    }
}
