using Godot;

public partial class BattlePaperDoll
{
    private QueueSmallAvatarBridge? _queueAvatar;
    private Vector2 _queueAvatarLastActorPosition;
    private string _queueOutfitId = "male_clothes_05";
    private string _queueArtDirectory = "res://tools/action_battle_test/art/yuxu_fullbody_v15";
    public bool uses_queue_avatar() => _queueAvatar is not null;
    public bool uses_basic_pair_avatar() => _queueAvatar is not null && _queueOutfitId.StartsWith("turn_basic_");
    public void set_queue_travel_mode(string mode) => _queueAvatar?.SetTravelMode(mode);
    public void set_queue_travel_progress(float progress) => _queueAvatar?.SetTravelProgress(progress);

    // 2026-11-06: user selected one provisional battle appearance per gender.
    // This chooses a renderer only; it never rewrites the character's real wardrobe/hair.
    private bool MatchesQueueAvatar(BattleEntityData data)
    {
        if (data.gender is not (0 or 1)) return false;
        _queueOutfitId = data.gender == 1 ? "turn_basic_female_20261106" : "turn_basic_male_20261106";
        _queueArtDirectory = "res://textures/characters/turn_based_pair_20261106";
        return Godot.FileAccess.FileExists(_queueArtDirectory + "/female_native_v3/sprite_65806.png")
            && Godot.FileAccess.FileExists(_queueArtDirectory + "/female_native_v3/hair_front.png")
            && Godot.FileAccess.FileExists(_queueArtDirectory + "/female_native_v3/hair_back.png");
    }

    private void ResetQueueAvatar()
    {
        if (_queueAvatar is null) return;
        _queueAvatar.Hide();
        _queueAvatar.QueueFree();
        _queueAvatar = null;
        GetNodeOrNull<CanvasItem>("InkShadow")?.Show();
    }

    private bool ConfigureQueueAvatar(BattleEntityData data)
    {
        _queueAvatar = new QueueSmallAvatarBridge { Name = "QueueSmallAvatarBridge", ZIndex = 1 };
        _sidePresentation!.AddChild(_queueAvatar);
        if (!_queueAvatar.Configure(SideInkReferenceHeight, _queueArtDirectory, _queueOutfitId))
        {
            ResetQueueAvatar();
            return false;
        }
        // The doll owns the animation clock, so response freeze/contact hold/death and
        // normal scene pause all stop both the skeleton and the surrounding presentation.
        _queueAvatar.SetProcess(false);
        _queueAvatar.SetFacing(_portrait!.FlipH);
        _portraitLocalBounds = _queueAvatar.LocalVisibleBounds;
        _combatChestY = _queueAvatar.ChestAnchor.Y;
        _combatHeadY = _queueAvatar.HeadAnchor.Y;
        _portrait.Visible = false;
        _sideIdle!.Visible = false;
        GetNodeOrNull<CanvasItem>("InkShadow")?.Hide();
        _queueAvatarLastActorPosition = (GetParent() as Node2D)?.GlobalPosition ?? GlobalPosition;
        return true;
    }

    private void UpdateQueueAvatar(float delta)
    {
        if (_queueAvatar is null) return;
        var actorPosition = (GetParent() as Node2D)?.GlobalPosition ?? GlobalPosition;
        var velocity = delta > 0 ? (actorPosition - _queueAvatarLastActorPosition) / delta / Mathf.Max(.0001f, Mathf.Abs(GlobalScale.X)) : Vector2.Zero;
        _queueAvatarLastActorPosition = actorPosition;
        _queueAvatar.SetMotion(_sideDead || _contactPoseHeld || _responseTimeStopped ? Vector2.Zero : velocity);
        _queueAvatar.SetFacing(_portrait?.FlipH == true);
        _queueAvatar.SetPaused(_contactPoseHeld || _responseTimeStopped || _sideDead);
        var action = _sideDead ? "idle" : _sidePose switch
        {
            "chop" or "punch" => "attack",
            "hurt" or "sword_hurt" => "hurt",
            "guard" or "sword_parry" => "guard",
            "dodge" => "dodge",
            _ => "idle"
        };
        _queueAvatar.UpdateVisual(delta, action, _sideElapsed, _sideDuration);
        // Other actions retain this same outfit until their own poses are authored.
        if (_portrait is not null) _portrait.Visible = false;
        if (_sideIdle is not null) _sideIdle.Visible = false;
    }

    private Vector2 QueueAvatarPoint(Vector2 point) => new(_portrait?.FlipH == true ? -point.X : point.X, point.Y);

    private Vector2 QueueAvatarPoseOffset()
    {
        // The original small rig provides its own restrained torso/robe motion.
        return Vector2.Zero;
    }

    private void StartQueueAvatarAttack(Vector2 direction, string kind)
    {
        EndContactPoseHold();
        face_direction(direction);
        _actionTween?.Kill();
        _actionTween = null;
        Position = Vector2.Zero;
        Rotation = 0;
        StartSideInkPose(kind, QueueSmallAvatarBridge.AttackDuration);
    }

    public void play_punch(Vector2 direction)
    {
        if (_queueAvatar is null) { play_attack(direction, false); return; }
        StartQueueAvatarAttack(direction, "punch");
    }

    private Rect2 QueueAvatarRect(bool canvas, bool shield = false)
    {
        var local = _queueAvatar!.LocalVisibleBounds;
        if (_portrait?.FlipH == true) local.Position = new Vector2(-local.End.X, local.Position.Y);
        if (shield) local = local.GrowIndividual(local.Size.X * .12f, local.Size.Y * .08f, local.Size.X * .12f, local.Size.Y * .05f);
        var transform = canvas ? _sidePresentation!.GetGlobalTransformWithCanvas() : _sidePresentation!.GlobalTransform;
        var rect = new Rect2(transform * local.Position, Vector2.Zero);
        rect = rect.Expand(transform * new Vector2(local.End.X, local.Position.Y));
        rect = rect.Expand(transform * local.End);
        return rect.Expand(transform * new Vector2(local.Position.X, local.End.Y));
    }
}
