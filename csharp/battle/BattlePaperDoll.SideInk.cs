using Godot;

public partial class BattlePaperDoll
{
    public const string SideInkArtRoot = "res://textures/characters/side_ink_portrait_outfits_20261015_v3/";
    public const string SideInkResponseArtRoot = "res://textures/characters/side_ink_responses_20261015_v4/";
    public const string SideInkRestArtRoot = "res://textures/characters/side_ink_rest_20261016_v1/";
    public const string SideInkDodgeArtRoot = "res://textures/characters/side_ink_dodge_20261016_v1/";
    public const string SideInkCastArtRoot = "res://textures/characters/side_ink_cast_20261020_v1/";
    public const string SideInkThrowArtRoot = "res://textures/characters/side_ink_throw_20261020_v1/";
    public static readonly Vector2 SideInkGround = new(1024, 1900);
    public const float SideInkReferenceHeight = 1375f;
    private readonly Dictionary<string, Texture2D?> _sideTextures = [];
    private Sprite2D? _sideIdle;
    private Node2D? _sidePresentation;
    private float _contactAccent, _contactShake;
    private bool _guardAwaitingContact;
    private float _guardWaitClock;
    private bool _usesSideInk, _sideFemale, _sideDead;
    private string _sidePose = "idle";
    private float _sideElapsed, _sideDuration;

    public bool uses_side_ink() => _usesSideInk;
    public bool side_ink_dead() => _sideDead;
    public bool side_ink_faces_left() => _portrait?.FlipH == true;
    public Vector2 side_ink_hand() => _sideFemale ? new(751, -397) : new(685, -376);
    public Vector2 side_ink_body() => _sideFemale ? new(-50, -970) : new(-15, -925);
    public Vector2 side_ink_guard() => _sideFemale ? new(335, -1050) : new(320, -1025);
    public Vector2 side_ink_cast_finger() => _sideFemale ? new(366, -1423) : new(344, -1456);
    public bool is_defense_presenting() => _usesSideInk && !_sideDead && _sidePose == "guard";
    public float presentation_zoom() => _sidePresentation?.Scale.X ?? 1;
    public Transform2D side_ink_visual_transform() => _sidePresentation?.GlobalTransform ?? GlobalTransform;
    public void release_guard_contact() { _guardAwaitingContact = false; }
    public void emphasize_contact(float strength = 1)
    {
        _contactAccent = .22f;
        _contactShake = Mathf.Clamp(strength, .6f, 1.6f);
        release_guard_contact();
    }

    public static bool IsHumanPlaceholderPath(string path) => path is DefaultPortraitPath or FemaleStaticPortraitPath
        or "res://textures/characters/c1_male_puppet_v2/preview/static.png"
        or "res://textures/characters/c1_female_puppet_v2/preview/static.png"
        or "res://textures/characters/combat_static_20261011/male_v2.png"
        or "res://textures/characters/combat_static_20261011/female_v2.png";

    public void reset_presentation()
    {
        EndContactPoseHold();
        _actionTween?.Kill();
        _heldAttackTween = null;
        _attackContactHeld = false;
        _responseTimeStopped = false;
        _responseStoppedTween = null;
        _sideDead = false;
        _guardAwaitingContact = false;
        _contactAccent = 0;
        if (_sidePresentation is not null) { _sidePresentation.Scale = Vector2.One; _sidePresentation.Position = Vector2.Zero; }
        Position = Vector2.Zero; Rotation = 0; Modulate = Colors.White;
        ResetSideInkPose();
    }

    private void ResetSideInk()
    {
        _actionTween?.Kill();
        _heldAttackTween = null;
        _attackContactHeld = false;
        _responseTimeStopped = false;
        _responseStoppedTween = null;
        _usesSideInk = false;
        _sideDead = false;
        _sidePose = "idle";
        _sideTextures.Clear();
        _guardAwaitingContact = false;
        _contactAccent = 0;
        if (_sidePresentation is not null)
        {
            _sidePresentation.Visible = false;
            _sidePresentation.Scale = Vector2.One;
            _sidePresentation.Position = Vector2.Zero;
            if (_portrait?.GetParent() == _sidePresentation) _portrait.Reparent(this, false);
        }
        if (_sideIdle is not null) _sideIdle.Visible = false;
        Position = Vector2.Zero;
        Rotation = 0;
        Modulate = Colors.White;
        if (_portrait is not null) { _portrait.Centered = true; _portrait.Offset = Vector2.Zero; }
        if (GetNodeOrNull<Polygon2D>("InkShadow") is { } shadow) shadow.Scale = Vector2.One;
    }

    private bool ConfigureSideInk(BattleEntityData data)
    {
        _sideFemale = data.gender == 1;
        var sex = _sideFemale ? "female" : "male";
        var idlePath = SideInkArtRoot + sex + "_idle.png";
        if (!ResourceLoader.Exists(idlePath)) return false;
        _spineCharacter?.QueueFree();
        _spineCharacter = null;
        _usesSpineCharacter = false;
        _isForestWolf = false;
        _portraitFacesLeft = false;
        _usesSideInk = true;
        _spineAnimation = "idle";
        foreach (var pose in new[] { "idle", "chop", "hurt", "guard", "rest", "dodge", "cast", "throw_release" })
        {
            var folder = pose.StartsWith("throw_") ? SideInkThrowArtRoot : pose == "cast" ? SideInkCastArtRoot : pose == "dodge" ? SideInkDodgeArtRoot : pose == "rest" ? SideInkRestArtRoot : pose is "hurt" or "guard" ? SideInkResponseArtRoot : SideInkArtRoot;
            var path = folder + sex + "_" + pose + ".png";
            _sideTextures[pose] = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
        }
        _portrait ??= CreatePortrait();
        _sidePresentation ??= new Node2D { Name = "InkPresentation" };
        if (_sidePresentation.GetParent() is null) AddChild(_sidePresentation);
        _sidePresentation.Visible = true;
        _portrait.Reparent(_sidePresentation, false);
        _portrait.Centered = false;
        _portrait.Offset = -SideInkGround;
        _portrait.Texture = _sideTextures["idle"];
        _portrait.Scale = Vector2.One;
        _portrait.Position = Vector2.Zero;
        _portrait.Hframes = _portrait.Vframes = 1;
        _portrait.Frame = 0;
        _portrait.Modulate = Colors.White;
        _portrait.FlipH = data.faction == (int)WorldEntity.Faction.ENEMY;
        _portrait.Visible = true;
        _portrait.TextureFilter = TextureFilterEnum.Linear;
        _sideIdle ??= new Sprite2D { Name = "SideInkIdleBlend", Centered = false, Offset = -SideInkGround, ZIndex = 1, TextureFilter = TextureFilterEnum.Linear };
        if (_sideIdle.GetParent() is null) AddChild(_sideIdle);
        _sideIdle.Reparent(_sidePresentation, false);
        _sideIdle.Texture = _sideTextures["idle"];
        _sideIdle.FlipH = _portrait.FlipH;
        _sideIdle.Visible = false;
        // Layout reads the idle silhouette once. Pose changes never crop or recenter a sprite.
        var bounds = VisiblePortraitBounds(_portrait.Texture!);
        _portraitLocalBounds = new Rect2((Vector2)bounds.Position - SideInkGround, bounds.Size);
        _portraitCenterX = 0;
        _idleBasePosition = Vector2.Zero;
        _displayHeightReference = SideInkReferenceHeight;
        _combatChestY = -860;
        _combatHeadY = -1280;
        Scale = Vector2.One * 320f / SideInkReferenceHeight;
        // The legacy shadow is in display units; register it to the new native art units.
        if (GetNodeOrNull<Polygon2D>("InkShadow") is { } shadow) shadow.Scale = Vector2.One * SideInkReferenceHeight / 348f;
        Name = "BattlePaperDoll";
        return true;
    }

    private float MotionUnits(float amount) => _usesSideInk ? amount * SideInkReferenceHeight / 348f : amount;

    private void StartSideInkPose(string pose, float duration)
    {
        if (!_usesSideInk || _sideDead) return;
        _sidePose = pose;
        _guardAwaitingContact = false;
        _sideElapsed = 0;
        _sideDuration = duration;
        ProcessSideInkPose(0);
    }

    private void ResetSideInkPose()
    {
        if (!_usesSideInk) return;
        _sidePose = "idle";
        _guardAwaitingContact = false;
        _sideElapsed = 0;
        if (_portrait is not null) { _portrait.Texture = _sideTextures.GetValueOrDefault("idle"); _portrait.Modulate = Colors.White; }
        if (_sideIdle is not null) _sideIdle.Visible = false;
    }

    private void ProcessSideInkPose(float delta)
    {
        if (_portrait is null) return;
        if (_guardAwaitingContact)
        {
            _guardWaitClock += delta;
            if (_guardWaitClock >= 1.4f) _guardAwaitingContact = false;
        }
        if (!_contactPoseHeld) _sideElapsed += delta;
        if (_guardAwaitingContact) _sideElapsed = Mathf.Min(_sideElapsed, .33f);
        if (_sidePose != "idle" && _sideElapsed >= _sideDuration) ResetSideInkPose();
        var visiblePose = _sidePose == "chop" && _sideElapsed < .08f ? "idle" : _sidePose;
        if (_sidePose == "throw") visiblePose = "throw_release";
        var texture = _sideTextures.GetValueOrDefault(visiblePose) ?? _sideTextures.GetValueOrDefault("idle");
        _portrait.Texture = texture;
        _portrait.Position = Vector2.Zero;
        var blend = 1f;
        if (_sidePose == "rest")
            blend = Mathf.SmoothStep(0, 1, Mathf.Clamp((_sideElapsed - .12f) / .16f, 0, 1))
                * (1 - Mathf.SmoothStep(0, 1, Mathf.Clamp((_sideElapsed - 1.37f) / .27f, 0, 1)));
        else if (_sidePose == "throw")
            blend = Mathf.SmoothStep(0, 1, Mathf.Clamp((_sideElapsed - .015f) / .07f, 0, 1))
                * (1 - Mathf.SmoothStep(0, 1, Mathf.Clamp((_sideElapsed - (_sideDuration - .22f)) / .22f, 0, 1)));
        else if (_sidePose == "cast")
            blend = Mathf.SmoothStep(0, 1, Mathf.Clamp((_sideElapsed - .035f) / .12f, 0, 1))
                * (1 - Mathf.SmoothStep(0, 1, Mathf.Clamp((_sideElapsed - (_sideDuration - .18f)) / .18f, 0, 1)));
        else if (_sidePose != "idle") blend = 1 - Mathf.SmoothStep(0, 1, Mathf.Clamp((_sideElapsed - (_sideDuration - .14f)) / .14f, 0, 1));
        if (texture == _sideTextures.GetValueOrDefault("idle")) blend = 1;
        _portrait.Modulate = new Color(1, 1, 1, blend);
        if (_sideIdle is not null)
        {
            _sideIdle.FlipH = _portrait.FlipH;
            _sideIdle.Modulate = new Color(1, 1, 1, 1 - blend);
            _sideIdle.Visible = blend < 1;
        }
        if (_sidePresentation is not null)
        {
            _contactAccent = Mathf.Max(0, _contactAccent - delta);
            _sidePresentation.Scale = Vector2.One;
            var shakeAge = .22f - _contactAccent;
            _sidePresentation.Position = _contactAccent <= 0 ? Vector2.Zero
                : new Vector2(Mathf.Sin(shakeAge * 155), Mathf.Cos(shakeAge * 187) * .45f) * (16 * _contactShake * Mathf.Pow(_contactAccent / .22f, 2));
        }
    }

    public void play_cast(Vector2 direction)
    {
        if (!_usesSideInk) { play_attack(direction, false); return; }
        if (_sideDead) return;
        EndContactPoseHold();
        _actionTween?.Kill();
        face_direction(direction);
        Position = Vector2.Zero; Rotation = 0; Modulate = Colors.White;
        StartSideInkPose("cast", 1.25f);
    }

    public void play_guard(bool awaitingContact = false)
    {
        if (!_usesSideInk || _sideDead) return;
        EndContactPoseHold();
        _actionTween?.Kill();
        Position = Vector2.Zero; Rotation = 0; Modulate = Colors.White;
        StartSideInkPose("guard", 1.35f);
        _guardAwaitingContact = awaitingContact;
        _guardWaitClock = 0;
    }

    public void play_rest()
    {
        if (!_usesSideInk || _sideDead) return;
        EndContactPoseHold();
        _actionTween?.Kill();
        Position = Vector2.Zero; Rotation = 0; Modulate = Colors.White;
        StartSideInkPose("rest", 1.72f);
    }

    public void play_dodge()
    {
        if (!_usesSideInk || _sideDead) return;
        EndContactPoseHold();
        _actionTween?.Kill();
        Position = Vector2.Zero; Rotation = 0; Modulate = Colors.White;
        StartSideInkPose("dodge", 1.10f);
        var direction = side_ink_faces_left() ? 1f : -1f;
        _actionTween = CreateTween();
        _actionTween.TweenProperty(this, "position", new Vector2(direction * 130, 0), .10f).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
        _actionTween.TweenInterval(.12f);
        _actionTween.TweenProperty(this, "position", Vector2.Zero, .34f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
    }
}
