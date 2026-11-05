using Godot;

public partial class BattlePaperDoll
{
    private QueueSmallAvatarBridge? _queueAvatar;
    private Vector2 _queueAvatarLastActorPosition;
    private string _queueOutfitId = "male_clothes_05";
    private string _queueArtDirectory = "res://tools/action_battle_test/art/yuxu_fullbody_v15";
    public bool uses_queue_avatar() => _queueAvatar is not null;
    public void set_queue_travel_mode(string mode) => _queueAvatar?.SetTravelMode(mode);
    public void set_queue_travel_progress(float progress) => _queueAvatar?.SetTravelProgress(progress);

    // Resolve the same catalog identities as the portrait UI. An unsupported outfit/hair
    // combination keeps the old renderer, rather than silently wearing the sample costume.
    private bool MatchesQueueAvatar(BattleEntityData data)
    {
        if (data.gender != 0 || data.portrait_data is not HumanData source || source.gender != 0) return false;
        var catalog = GetNodeOrNull<PortraitCatalog>("/root/PortraitCatalog");
        if (catalog is null) return false;
        var requested = catalog.get_requested_appearance(source);
        if (source.appearance_hair_back_style < 0) requested["hair_back"] = "";
        var context = catalog.get_context(source, "male", data.current_age, source.faction_name, data.cultivation_realm);
        var selected = catalog.select_portrait(context, requested);
        foreach (var (part, id) in new[] { ("hair_front", "male_front_hair_05"), ("hair_back", "male_back_hair_06") })
        {
            if (!selected.ContainsKey(part)) return false;
            var component = selected[part].AsGodotDictionary();
            if (component.GetValueOrDefault("id", "").AsString() != id
                || component.GetValueOrDefault("package_id", "").AsString() != "ink_three_variants_20261004") return false;
        }
        if (!selected.ContainsKey("clothes")) return false;
        var clothes = selected["clothes"].AsGodotDictionary();
        if (clothes.GetValueOrDefault("package_id", "").AsString() != "ink_three_variants_20261004") return false;
        _queueOutfitId = clothes.GetValueOrDefault("id", "").AsString();
        var folder = _queueOutfitId switch {
            "male_clothes_03" => "queue_yuxu_servant_v1",
            "male_clothes_04" => "queue_yuxu_outer_v1",
            "male_clothes_05" => "yuxu_fullbody_v15",
            "male_clothes_06" => "queue_yuxu_true_v1", _ => "" };
        if (folder.Length == 0) return false;
        _queueArtDirectory = "res://tools/action_battle_test/art/" + folder;
        if (!Godot.FileAccess.FileExists(_queueArtDirectory + "/body.png") || !Godot.FileAccess.FileExists(_queueArtDirectory + "/arm_relaxed_v19.png")) return false;
        return true;
    }

    private void ResetQueueAvatar()
    {
        if (_queueAvatar is null) return;
        _queueAvatar.Hide();
        _queueAvatar.QueueFree();
        _queueAvatar = null;
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
