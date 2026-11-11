using Godot;

/// <summary>
/// 战斗中的角色显示。人形使用水墨关键姿势，兽类保留独立素材与骨骼支持。
/// </summary>
public partial class BattlePaperDoll : Node2D
{
    public const string DefaultPortraitPath = SideInkArtRoot + "male_idle.png";
    public const string ForestWolfPortraitPath = "res://textures/bestiary/static/forest_wolf.png";
    public const string ForestWolfAttackPortraitPath = "res://textures/bestiary/static/forest_wolf_pounce.png";
    public const string ForestWolfHurtPortraitPath = "res://textures/bestiary/static/forest_wolf_hurt.png";
    public const string ForestBeiPortraitPath = "res://textures/bestiary/static/forest_bei_v1.png";
    public const string ForestBeiAttackPortraitPath = "res://textures/bestiary/static/forest_bei_attack_v1.png";
    public const string ForestBeiHurtPortraitPath = "res://textures/bestiary/static/forest_bei_hurt_v1.png";
    public const string ForestWolfSpineResourcePath = "res://textures/characters/forest_wolf_spine_rig_v6/runtime/forest_wolf_spine_rig_v6.spinejson";
    public const string CharacterSpineResourcePath = "res://textures/characters/c1_male_puppet_v2/runtime/c1_male_puppet_v2.spinejson";
    public const string FemaleSpineResourcePath = "res://textures/characters/c1_female_puppet_v2/runtime/c1_female_puppet_v2.spinejson";
    public const string FemaleStaticPortraitPath = SideInkArtRoot + "female_idle.png";
    public const string CombatMaleStaticPath = DefaultPortraitPath;
    public const string CombatFemaleStaticPath = FemaleStaticPortraitPath;
    private static readonly System.Collections.Generic.Dictionary<string, Rect2I> PortraitBounds = new();
    private static readonly System.Collections.Generic.Dictionary<string, Rect2> ShieldEnvelopes = new();
    private Rect2 _portraitLocalBounds;
    private float _portraitCenterX;

    private Sprite2D? _portrait;
    private Node2D? _spineCharacter;
    private float _idlePhase;
    private Tween? _actionTween;
    private Tween? _heldAttackTween;
    private bool _attackContactHeld;
    private bool _contactPoseHeld;
    private bool _responseTimeStopped;
    private Tween? _responseStoppedTween;
    private float _responseSpineSpeed = 1f;
    private float _spineSpeedBeforeHold = 1f;
    private Vector2 _idleBasePosition = new(0, -75);
    private bool _usesSpineCharacter;
    private bool _portraitFacesLeft;
    private bool _isForestWolf;
    private bool _isForestBei;
    private bool UsesStaticBeastPoses => _isForestWolf || _isForestBei;
    private enum ForestWolfPose { Idle, Attack, Hurt }
    private ForestWolfPose _forestWolfPose = ForestWolfPose.Idle;
    private Texture2D? _forestWolfIdlePortrait;
    private Texture2D? _forestWolfAttackPortrait;
    private Texture2D? _forestWolfHurtPortrait;
    private Rect2 _forestWolfIdleLocalBounds;
    private Vector2 _forestWolfBasePortraitScale = Vector2.One;
    private float _forestWolfIdleDisplayHeightReference = 89f;
    private float _combatChestY = -236f;
    private float _combatHeadY = -348f;
    private float _displayHeightReference = 348f;
    private string _spineAnimation = "idle";
    private static bool _spineRuntimeWarningShown;

    public void Configure(BattleEntityData data)
    {
        EndContactPoseHold();
        ResetSideInk();
        _isForestWolf = false;
        _isForestBei = false;
        _forestWolfPose = ForestWolfPose.Idle;
        _forestWolfIdlePortrait = null;
        _forestWolfAttackPortrait = null;
        _forestWolfHurtPortrait = null;
        // 真实人类肖像快照也确认显示类型，避免独立入口漏填类型后退回旧C1纸偶。
        var isHuman=data.entity_type==(int)WorldEntity.EntityType.CHARACTER||data.portrait_data is HumanData;
        if (isHuman && ConfigureSideInk(data)) return;
        var isForestWolf = data.entity_name.Contains("凶狼") || data.entity_name.Contains("林地狼") || data.entity_name.Contains("森林狼");
        var isForestBei = data.entity_name.Contains("妖狈") || data.battle_portrait_path == ForestBeiPortraitPath;
        var path = string.IsNullOrWhiteSpace(data.battle_portrait_path)
            ? (isForestBei ? ForestBeiPortraitPath : isForestWolf ? ForestWolfPortraitPath : DefaultPortraitPath)
            : data.battle_portrait_path;
        var hasStaticHumanPortrait = isHuman;
        if (hasStaticHumanPortrait) path = data.gender == 1 ? CombatFemaleStaticPath : CombatMaleStaticPath;
        if (!ResourceLoader.Exists(path)) path = DefaultPortraitPath;

        _spineCharacter?.QueueFree();
        _spineCharacter = null;
        _usesSpineCharacter = false;
        _spineAnimation = "idle";
        if (_portrait is not null) _portrait.Visible = false;

        // 局内人形统一使用新静态图；兽类仍使用自身的资源。
        // Both canids share pose registration and ground anchors, but keep distinct VFX identities.
        var isWolf = isForestWolf || isForestBei;
        _isForestWolf = isForestWolf;
        _isForestBei = isForestBei;
        var hasStaticBeastPortrait = data.entity_type == (int)WorldEntity.EntityType.BEAST && !string.IsNullOrWhiteSpace(path) && path != DefaultPortraitPath;
        _portraitFacesLeft = isWolf || hasStaticBeastPortrait;
        var isFemale = !isWolf && data.gender == 1;
        _combatChestY = isWolf ? -50f : isFemale ? -204f : -236f;
        _combatHeadY = isWolf ? -89f : isFemale ? -304f : -348f;
        _displayHeightReference = isWolf ? 89f : isFemale ? 304f : 348f;
        _idleBasePosition = isWolf ? Vector2.Zero : new Vector2(0, isFemale ? -153 : -180);
        var facesEnemy = data.faction == (int)WorldEntity.Faction.ENEMY;
        var spineFailure = "未尝试加载 Spine";
        var shouldFlipSpine = isWolf ? !facesEnemy : facesEnemy;
        if (!hasStaticHumanPortrait && !hasStaticBeastPortrait && TryCreateSpineCharacter(shouldFlipSpine, isFemale, isWolf, out spineFailure))
        {
            _usesSpineCharacter = true;
            Name = "BattlePaperDoll";
            return;
        }

        if (!hasStaticHumanPortrait && !hasStaticBeastPortrait && !_spineRuntimeWarningShown)
        {
            _spineRuntimeWarningShown = true;
            GD.PushError($"战斗角色未使用 Spine：{spineFailure}");
        }

        _portrait ??= CreatePortrait();
        _portrait.Visible = true;
        var fallbackPath = hasStaticHumanPortrait ? path : isWolf
            ? (isForestBei ? ForestBeiPortraitPath : ForestWolfPortraitPath)
            : isFemale && ResourceLoader.Exists(FemaleStaticPortraitPath) ? FemaleStaticPortraitPath : path;
        _portrait.Texture = ResourceLoader.Load<Texture2D>(fallbackPath);
        _portrait.Hframes = 1;
        _portrait.Vframes = 1;
        _portrait.Frame = 0;
        _portrait.Modulate = Colors.White;
        _portrait.Scale = isWolf ? new Vector2(0.09f, 0.09f) : hasStaticBeastPortrait ? new Vector2(0.30f, 0.30f) : isFemale ? new Vector2(0.174f, 0.174f) : new Vector2(0.192f, 0.192f);
        if (isWolf && _portrait.Texture is not null)
        {
            _forestWolfIdlePortrait = _portrait.Texture;
            var attackPath = isForestBei ? ForestBeiAttackPortraitPath : ForestWolfAttackPortraitPath;
            var hurtPath = isForestBei ? ForestBeiHurtPortraitPath : ForestWolfHurtPortraitPath;
            _forestWolfAttackPortrait = ResourceLoader.Exists(attackPath) ? ResourceLoader.Load<Texture2D>(attackPath) : null;
            _forestWolfHurtPortrait = ResourceLoader.Exists(hurtPath) ? ResourceLoader.Load<Texture2D>(hurtPath) : null;
        }
        // Spine's wolf root is on the paw line; the centered static fallback needs its image center lifted to that line.
        _idleBasePosition = isWolf ? new Vector2(0, -43.6f) : isFemale ? new Vector2(0, -105) : new Vector2(0, -120);
        _portraitCenterX = 0f;
        if (_portrait.Texture is not null && (hasStaticBeastPortrait || hasStaticHumanPortrait))
        {
            var bounds = VisiblePortraitBounds(_portrait.Texture);
            _portraitLocalBounds = new Rect2(bounds.Position, bounds.Size);
            _portraitLocalBounds.Position -= _portrait.Texture.GetSize() * .5f;
            _portraitCenterX = (bounds.Position.X + bounds.Size.X * .5f - _portrait.Texture.GetWidth() * .5f) * _portrait.Scale.X;
            _idleBasePosition.Y = -(bounds.End.Y - _portrait.Texture.GetHeight() * .5f) * _portrait.Scale.Y;
            if (_isForestBei) _idleBasePosition.Y = -_portrait.Texture.GetHeight() * .36f * _portrait.Scale.Y;
            _combatHeadY = -bounds.Size.Y * _portrait.Scale.Y;
            _combatChestY = _combatHeadY * .62f;
            _displayHeightReference = Mathf.Abs(_combatHeadY);
        }
        if (isWolf && _portrait.Texture is not null)
        {
            _forestWolfIdleLocalBounds = _portraitLocalBounds;
            _forestWolfBasePortraitScale = _portrait.Scale;
            _forestWolfIdleDisplayHeightReference = _displayHeightReference;
        }

        // 人形素材面朝右、狼素材面朝左；交战双方始终朝向对手。
        _portrait.FlipH = isWolf || hasStaticBeastPortrait ? !facesEnemy : facesEnemy;
        AlignPortraitCenter();
        Name = "BattlePaperDoll";
    }

    // 微弱透明噪点不参与锚点计算；仅首次加载读取像素，复用时直接查缓存。
    private static Rect2I VisiblePortraitBounds(Texture2D texture)
    {
        if (PortraitBounds.TryGetValue(texture.ResourcePath, out var cached)) return cached;
        using var image = texture.GetImage();
        image.Convert(Image.Format.Rgba8);
        var bytes = image.GetData();
        int width = image.GetWidth(), height = image.GetHeight(), left = width, top = height, right = -1, bottom = -1;
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                if (bytes[(y * width + x) * 4 + 3] > 25)
                { left = Math.Min(left, x); top = Math.Min(top, y); right = Math.Max(right, x); bottom = Math.Max(bottom, y); }
        var bounds = right < left ? new Rect2I(0, 0, width, height) : new Rect2I(left, top, right - left + 1, bottom - top + 1);
        PortraitBounds[texture.ResourcePath] = bounds;
        return bounds;
    }

    private void AlignPortraitCenter()
    {
        if (_portrait is null) return;
        if (_usesSideInk)
        {
            _idleBasePosition = Vector2.Zero;
            _portrait.Position = Vector2.Zero;
            if (_sideIdle is not null) _sideIdle.FlipH = _portrait.FlipH;
            return;
        }
        _idleBasePosition.X = _portrait.FlipH ? _portraitCenterX : -_portraitCenterX;
        _portrait.Position = _idleBasePosition;
    }

    private void SetForestWolfPortraitPose(ForestWolfPose pose)
    {
        if (!UsesStaticBeastPoses || _portrait is null || _forestWolfIdlePortrait is null) return;
        var texture = pose switch
        {
            ForestWolfPose.Attack => _forestWolfAttackPortrait ?? _forestWolfIdlePortrait,
            ForestWolfPose.Hurt => _forestWolfHurtPortrait ?? _forestWolfIdlePortrait,
            _ => _forestWolfIdlePortrait
        };
        _forestWolfPose = pose;
        _portrait.Texture = texture;
        _portrait.Scale = _forestWolfBasePortraitScale;
        var bounds = VisiblePortraitBounds(texture);
        _portraitLocalBounds = new Rect2(bounds.Position, bounds.Size);
        _portraitLocalBounds.Position -= texture.GetSize() * .5f;
        _portraitCenterX = (bounds.Position.X + bounds.Size.X * .5f - texture.GetWidth() * .5f) * _portrait.Scale.X;
        _idleBasePosition.Y = -(bounds.End.Y - texture.GetHeight() * .5f) * _portrait.Scale.Y;
        if (_isForestBei) _idleBasePosition.Y = -texture.GetHeight() * .36f * _portrait.Scale.Y;
        _combatHeadY = -bounds.Size.Y * _portrait.Scale.Y;
        _combatChestY = _combatHeadY * .62f;
        _displayHeightReference = _forestWolfIdleDisplayHeightReference;
        AlignPortraitCenter();
    }

    private void ResetForestWolfPortraitPose() => SetForestWolfPortraitPose(ForestWolfPose.Idle);

    public Rect2? static_screen_rect()
    {
        if (_queueAvatar is not null) return QueueAvatarRect(true);
        if (_usesSpineCharacter || _portrait?.Texture is null || _portraitLocalBounds.Size == Vector2.Zero) return null;
        var local = _portraitLocalBounds;
        if (_portrait.FlipH) local.Position = new Vector2(-local.End.X, local.Position.Y);
        var transform = _portrait.GetGlobalTransformWithCanvas();
        var first = transform * local.Position;
        var rect = new Rect2(first, Vector2.Zero);
        rect = rect.Expand(transform * new Vector2(local.End.X, local.Position.Y));
        rect = rect.Expand(transform * local.End);
        return rect.Expand(transform * new Vector2(local.Position.X, local.End.Y));
    }

    // Keep the approved upright oval around the standing body, independent of action width.
    public Rect2 shield_world_rect()
    {
        if (_queueAvatar is not null) return QueueAvatarRect(false, true);
        if (_portraitLocalBounds.Size != Vector2.Zero && !_usesSpineCharacter)
        {
            var portraitBounds = UsesStaticBeastPoses ? _forestWolfIdleLocalBounds : _portraitLocalBounds;
            var local = portraitBounds;
            var texture = UsesStaticBeastPoses ? _forestWolfIdlePortrait : _usesSideInk ? _sideTextures.GetValueOrDefault("idle") : _portrait?.Texture;
            if (texture is not null)
            {
                if (!ShieldEnvelopes.TryGetValue(texture.ResourcePath, out var envelope))
                {
                    var visible = VisiblePortraitBounds(texture);
                    var centre = (Vector2)visible.GetCenter() + Vector2.Down * visible.Size.Y * .035f;
                    var ry = visible.Size.Y * .65f;
                    var rx = visible.Size.X * .52f;
                    using var image = texture.GetImage();
                    image.Convert(Image.Format.Rgba8);
                    var pixels = image.GetData();
                    var imageWidth = image.GetWidth();
                    for (var y = visible.Position.Y; y < visible.End.Y; y += 4)
                        for (var x = visible.Position.X; x < visible.End.X; x += 4)
                            if (pixels[(y * imageWidth + x) * 4 + 3] > 25)
                            {
                                var dy = (y - centre.Y) / ry;
                                rx = Mathf.Max(rx, Mathf.Abs(x - centre.X) / Mathf.Sqrt(Mathf.Max(.01f, 1 - dy * dy)));
                            }
                    var radius = new Vector2(rx * 1.055f, ry);
                    envelope = new Rect2(centre - radius - (Vector2)visible.Position + portraitBounds.Position, radius * 2);
                    ShieldEnvelopes[texture.ResourcePath] = envelope;
                }
                local = envelope;
            }
            if (_portrait?.FlipH == true) local.Position = new Vector2(-local.End.X, local.Position.Y);
            var transform = _usesSideInk ? side_ink_visual_transform() : GlobalTransform;
            var rect = new Rect2(transform * local.Position, Vector2.Zero);
            rect = rect.Expand(transform * new Vector2(local.End.X, local.Position.Y));
            rect = rect.Expand(transform * local.End);
            return rect.Expand(transform * new Vector2(local.Position.X, local.End.Y));
        }
        var height = _displayHeightReference * Mathf.Abs(GlobalScale.Y);
        var width = height * (_isForestWolf ? 1.4f : .75f);
        return new Rect2(GlobalPosition - new Vector2(width * .75f, height * 1.08f), new Vector2(width * 1.5f, height * 1.22f));
    }

    public override void _Ready()
    {
        _portrait ??= CreatePortrait();
    }

    public void SetDisplayHeight(float height)
    {
        var targetHeight = UsesStaticBeastPoses ? height * 0.65f : height;
        Scale = Vector2.One * targetHeight / Mathf.Max(1, _displayHeightReference);
    }

    public bool uses_spine_character() => _usesSpineCharacter;
    public bool uses_forest_wolf_portrait() => _isForestWolf && !_usesSpineCharacter;
    public bool uses_forest_bei_portrait() => _isForestBei && !_usesSpineCharacter;
    public bool uses_static_beast_poses() => UsesStaticBeastPoses && !_usesSpineCharacter;
    public void play_beast_command(Vector2 direction)
    {
        if (!uses_static_beast_poses()) return;
        play_attack(direction);
        _actionTween?.TweenCallback(Callable.From(ResetForestWolfPortraitPose));
    }
    public Vector2 forest_wolf_mouth_anchor()
    {
        if (_portrait?.Texture is null) return combat_anchor();
        var point = _portrait.Texture.GetSize() * new Vector2(-.42f, 0);
        if (_portrait.FlipH) point.X = -point.X;
        return _portrait.ToGlobal(point);
    }
    public Vector2 beast_voice_anchor()
    {
        if (_portrait?.Texture is null) return combat_anchor(true);
        var point = _portrait.Texture.GetSize() * new Vector2(_portrait.FlipH ? .40f : -.40f, -.15f);
        return _portrait.ToGlobal(point);
    }
    public float body_display_height_local() => _displayHeightReference * Mathf.Abs(Scale.Y);

    // Include the sleeves and the forward attack pose, rather than just the torso.
    public float get_contact_radius() => _displayHeightReference * (_portraitFacesLeft ? 1.05f : .32f) * Mathf.Abs(GlobalScale.X);

    public void play_retreat(Vector2 facing)
    {
        hold_attack_contact(false);
        ResetForestWolfPortraitPose();
        EndContactPoseHold();
        // Finish the authored recovery pose during the existing return movement.
        // Do not replace the held contact pose with idle at the first retreat frame.
        if (_queueAvatar is null || _sidePose is not ("chop" or "punch")) ResetSideInkPose();
        face_direction(facing);
        _actionTween?.Kill();
        Position = Vector2.Zero;
        // Face the opponent while the root flies backwards along its return arc.
        Rotation = UsesStaticBeastPoses || _queueAvatar is not null ? 0 : -Mathf.Sign(facing.X) * .075f;
        _actionTween = CreateTween();
        _actionTween.TweenProperty(this, "rotation", 0f, BattleAttackMotion.ReturnSeconds)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
    }

    // 应对特写独立于命中停顿；恢复时保留原先已暂停的接触姿势。
    public void set_response_time_stop(bool stopped)
    {
        if (_responseTimeStopped == stopped) return;
        _responseTimeStopped = stopped;
        if (stopped)
        {
            _responseStoppedTween = _actionTween?.IsValid() == true && _actionTween.IsRunning() ? _actionTween : null;
            _responseStoppedTween?.Pause();
            if (_usesSpineCharacter && IsInstanceValid(_spineCharacter))
            {
                _responseSpineSpeed = _spineCharacter!.Call("get_speed").AsSingle();
                _spineCharacter.Call("set_speed", 0f);
            }
        }
        else
        {
            if (_responseStoppedTween == _actionTween && _responseStoppedTween?.IsValid() == true) _responseStoppedTween.Play();
            _responseStoppedTween = null;
            if (_usesSpineCharacter && IsInstanceValid(_spineCharacter)) _spineCharacter!.Call("set_speed", _responseSpineSpeed);
        }
    }

    // 只等待非循环动作；待机与常驻护盾不会阻塞行动条。
    public bool is_action_animation_playing() => _contactPoseHeld
        || (_usesSideInk && _sidePose != "idle" && !_sideDead)
        || (_actionTween?.IsValid()==true && _actionTween.IsRunning())
        || (_usesSpineCharacter && IsInstanceValid(_spineCharacter) && _spineAnimation!="idle"
            && _spineCharacter!.Call("is_playing",0).AsBool());

    public void hold_attack_contact(bool held)
    {
        if(held)
        {
            // 受击／倒地已经替换攻击时，不暂停新动作。
            if(UsesStaticBeastPoses && _forestWolfPose!=ForestWolfPose.Attack)return;
            if(_usesSpineCharacter && _spineAnimation!="attack")return;
            if (_usesSideInk && _sidePose is not ("chop" or "punch" or "cast" or "throw" or "sword_cut" or "sword_finish")) return;
            _attackContactHeld=true;
            _heldAttackTween=_actionTween;
            if(_heldAttackTween?.IsValid()==true)_heldAttackTween.Pause();
            BeginContactPoseHold();
        }
        else
        {
            if(_attackContactHeld)
            {
                if(_heldAttackTween==_actionTween)
                {
                    if(_heldAttackTween?.IsValid()==true)_heldAttackTween.Play();
                    EndContactPoseHold();
                }
                _heldAttackTween=null;
                _attackContactHeld=false;
            }
            if(UsesStaticBeastPoses && _forestWolfPose==ForestWolfPose.Attack)ResetForestWolfPortraitPose();
        }
    }

    public override void _Process(double delta)
    {
        if (_responseTimeStopped) return;
        if (_usesSideInk) { ProcessSideInkPose((float)delta); return; }
        if (!_contactPoseHeld) _idlePhase += (float)delta;
        var idleOffset = new Vector2(0, Mathf.Sin(_idlePhase * 1.8f) * 2.2f);

        if (_usesSpineCharacter && IsInstanceValid(_spineCharacter))
        {
            if (!_contactPoseHeld && _spineAnimation is not "idle" and not "death_kneel" and not "death" && !_spineCharacter!.Call("is_playing", 0).AsBool())
                PlaySpineAnimation("idle", true);
            _spineCharacter!.Position = _idleBasePosition + idleOffset;
            return;
        }

        if (_portrait is not null)
            _portrait.Position = _idleBasePosition;
    }

    public void play_attack(Vector2 direction, bool useHandChop = true)
    {
        EndContactPoseHold();
        if (uses_static_beast_poses())
        {
            _actionTween?.Kill();
            Position = Vector2.Zero;
            Rotation = 0;
            face_direction(direction);
            SetForestWolfPortraitPose(ForestWolfPose.Attack);
            _actionTween = CreateTween();
            // One attack image, held through contact; force comes from FX and hit stop.
            _actionTween.TweenInterval(.23f);
            return;
        }
        ResetForestWolfPortraitPose();
        face_direction(direction);
        if (UsesStaticBeastPoses) SetForestWolfPortraitPose(ForestWolfPose.Attack);
        if (_queueAvatar is not null && useHandChop)
        {
            StartQueueAvatarAttack(direction, "chop");
            return;
        }
        if (_usesSideInk) StartSideInkPose(useHandChop ? "chop" : "idle", .95f);
        if (_usesSpineCharacter) PlaySpineAnimation("attack", false);
        _actionTween?.Kill();
        Position = Vector2.Zero;
        Rotation = 0;
        _actionTween = CreateTween();
        _actionTween.SetParallel();
        _actionTween.TweenProperty(this, "position", direction * MotionUnits(16f), .07f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        _actionTween.TweenProperty(this, "rotation", direction.X * .06f, .07f);
        _actionTween.Chain().SetParallel();
        _actionTween.TweenProperty(this, "position", Vector2.Zero, .16f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        _actionTween.TweenProperty(this, "rotation", 0f, .16f);
    }

    public void face_direction(Vector2 direction)
    {
        if (Mathf.Abs(direction.X) < 0.001f) return;
        _queueAvatar?.SetFacing(direction.X < 0);
        if (_usesSpineCharacter && IsInstanceValid(_spineCharacter))
            _spineCharacter!.Call("set_flip_x", _portraitFacesLeft ? direction.X >= 0f : direction.X < 0f);
        else if (_portrait is not null)
        {
            _portrait.FlipH = _portraitFacesLeft ? direction.X >= 0f : direction.X < 0f;
            AlignPortraitCenter();
        }
    }

    public Vector2 combat_anchor(bool head = false) => _queueAvatar is not null
        ? _sidePresentation!.ToGlobal(QueueAvatarPoint(head ? _queueAvatar.HeadAnchor : _queueAvatar.ChestAnchor))
        : ToGlobal(new Vector2(0f, head ? _combatHeadY : _combatChestY));

    public void play_hit(Vector2 direction, float weight = 1f, bool critical = false, bool playHurtFx = true)
    {
        if (is_defense_presenting()) { emphasize_contact(weight); return; }
        EndContactPoseHold();
        if (UsesStaticBeastPoses) SetForestWolfPortraitPose(ForestWolfPose.Hurt);
        if (_usesSideInk)
        {
            if(!StartSwordHurt())StartSideInkPose("hurt", 1.12f);
            emphasize_contact(weight);
            if (playHurtFx) BattleInkCombatVfx.PlayHurt(this, weight, critical);
        }
        if (_usesSpineCharacter) PlaySpineAnimation("hurt", false);
        _actionTween?.Kill();
        Position = Vector2.Zero;
        Modulate = Colors.White;
        _actionTween = CreateTween();
        _actionTween.SetParallel();
        var force = Mathf.Clamp(weight, 0.6f, 1.6f);
        _actionTween.TweenProperty(this, "position", direction * (MotionUnits(10f) * force), .035f);
        _actionTween.TweenProperty(this, "rotation", direction.X * 0.035f * force, .035f);
        _actionTween.TweenProperty(this, "modulate", new Color(.70f, .73f, .69f), .035f);
        _actionTween.Chain().TweenCallback(Callable.From(BeginContactPoseHold));
        _actionTween.Chain().TweenInterval(BattleInkCombatVfx.ContactHoldSeconds(force, critical));
        _actionTween.Chain().TweenCallback(Callable.From(EndContactPoseHold));
        _actionTween.Chain().SetParallel();
        _actionTween.TweenProperty(this, "position", Vector2.Zero, .16f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        _actionTween.TweenProperty(this, "rotation", 0f, .16f);
        _actionTween.TweenProperty(this, "modulate", Colors.White, .14f);
        if (UsesStaticBeastPoses) _actionTween.Chain().TweenCallback(Callable.From(ResetForestWolfPortraitPose));
    }

    public void play_death()
    {
        EndContactPoseHold();
        ResetForestWolfPortraitPose();
        _actionTween?.Kill();
        if (_usesSideInk) { ResetSideInkPose(); _sideDead = true; }
        if (_usesSpineCharacter) PlaySpineAnimation(_portraitFacesLeft ? "death" : "death_kneel", false);
    }

    private void BeginContactPoseHold()
    {
        if (_contactPoseHeld) return;
        _contactPoseHeld = true;
        if (_usesSpineCharacter && IsInstanceValid(_spineCharacter))
        {
            _spineSpeedBeforeHold = _spineCharacter!.Call("get_speed").AsSingle();
            _spineCharacter.Call("set_speed", 0f);
        }
    }

    private void EndContactPoseHold()
    {
        if (!_contactPoseHeld) return;
        _contactPoseHeld = false;
        if (_usesSpineCharacter && IsInstanceValid(_spineCharacter))
            _spineCharacter!.Call("set_speed", _spineSpeedBeforeHold);
    }

    private bool TryCreateSpineCharacter(bool flipX, bool isFemale, bool isWolf, out string failureReason)
    {
        failureReason = "未知原因";
        if (!ClassDB.CanInstantiate("Spine"))
        {
            failureReason = "当前 Godot 不是 Spine 3.8 运行时";
            return false;
        }

        var resourcePath = isWolf
            ? ForestWolfSpineResourcePath
            : isFemale ? FemaleSpineResourcePath : CharacterSpineResourcePath;
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

        spine.Name = isWolf ? "ForestWolfSpine" : isFemale ? "FemaleShadowSpine" : "C1SpineCharacter";
        spine.Position = _idleBasePosition;
        // Spine 资源的运行时单位比旧序列帧小；完整画布不参与显示尺寸判断。
        // 按现有男女比例整体放大约 20%，让战斗角色更醒目。
        spine.Scale = isWolf ? new Vector2(0.09f, 0.09f) : isFemale ? new Vector2(0.252f, 0.252f) : new Vector2(0.288f, 0.288f);
        spine.ZIndex = 1;
        AddChild(spine);

        spine.Call("set_resource", resource);
        spine.Call("set_flip_x", flipX);
        spine.Call("set_active", true);
        // 保留皮影素材的染色皮面与刻纹；无五官由头部素材保证。
        spine.Modulate = Colors.White;
        var requiredAnimations = isWolf
            ? new[] { "idle", "attack", "hurt", "death" }
            : new[] { "idle", "attack", "hurt", "death_kneel" };
        foreach (var animation in requiredAnimations)
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
