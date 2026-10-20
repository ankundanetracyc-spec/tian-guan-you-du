using Godot;

/// <summary>Approved PNG sequences on the existing battle visual clock and ownership group.</summary>
public partial class BattleInkCombatVfx
{
    public const string ChopSequencePath = "res://textures/vfx/ink_chop_sequence_20261016_v2/chop_effects.tres";
    public const string ResponseSequencePath = "res://textures/vfx/ink_response_sequences_20261016_v4/response_effects.tres";
    private static SpriteFrames? _chopFrames, _responseFrames;
    private AnimatedSprite2D? _sequenceSprite;
    private BattlePaperDoll? _sequenceOwner;
    private float _sequenceFps = 30, _followUntil = float.PositiveInfinity;
    private bool _cancelOnDeath;
    private Node2D? _worldSequenceOwner;
    private Vector2 _worldSequenceScale;

    private static BattlePaperDoll? SideDoll(Node2D unit) => unit.GetNodeOrNull<BattlePaperDoll>("BattlePaperDoll") is { } doll && doll.uses_side_ink() ? doll : null;

    // Presentation categories only. Explicit punch/weapon choreography keeps its dedicated effect.
    public static bool UsesHandChop(ActiveSkillData skill) =>
        Profile(skill) is Kind.Strike or Kind.Release or Kind.Heavy or Kind.Flow
        && !BattleInkSlashEffect.IsPunchSkill(skill)
        && !new[] { "拳", "踢", "腿", "剑", "刀", "枪", "刺" }.Any(skill.skill_name.Contains);

    private static BattleInkCombatVfx? SpawnSequence(BattlePaperDoll doll, bool chop, string animation, Vector2 at, Vector2 dimensions, float fps = 30, SpriteFrames? alternateFrames = null, Vector2? nativeSize = null)
    {
        if (_active >= Capacity || !IsInstanceValid(doll) || !doll.IsInsideTree() || doll.side_ink_dead()
            || Engine.GetMainLoop() is not SceneTree tree || tree.CurrentScene is null) return null;
        var frames = alternateFrames ?? (chop ? _chopFrames ??= GD.Load<SpriteFrames>(ChopSequencePath) : _responseFrames ??= GD.Load<SpriteFrames>(ResponseSequencePath));
        if (frames is null || !frames.HasAnimation(animation) || frames.GetFrameCount(animation) == 0) return null;
        var effect = new BattleInkCombatVfx
        {
            Name = "InkSequence_" + animation, _sequenceOwner = doll, _sequenceFps = fps,
            _duration = frames.GetFrameCount(animation) / fps,
            ProcessMode = ProcessModeEnum.Pausable, ZIndex = BattlePresentationLayers.Effects, ZAsRelative = false
        };
        tree.CurrentScene.AddChild(effect);
        effect.AddToGroup("battle_action_visuals");
        effect._sequenceSprite = new AnimatedSprite2D
        {
            Name = "SequenceFrames", SpriteFrames = frames, Animation = animation,
            Position = at, Scale = dimensions / (nativeSize ?? Vector2.One * 768f), TextureFilter = CanvasItem.TextureFilterEnum.Linear
        };
        effect.AddChild(effect._sequenceSprite);
        effect.UpdateSequenceTransform();
        _active++; effect._registered = true;
        return effect;
    }

    private static BattleInkCombatVfx? PlayHandChop(BattlePaperDoll doll, ActiveSkillData skill)
    {
        var shift = doll.side_ink_hand() - new Vector2(650, -355);
        const float contact = .30f;
        var effect = SpawnSequence(doll, true, "slash", new Vector2(480, -780) + shift, new Vector2(940, 1080), 5f / contact);
        if (effect is not null)
        {
            effect._holdAt = contact;
            effect._hold = .035f + ContactHoldSeconds(HitWeight(skill));
            effect._followUntil = contact + effect._hold;
        }
        return effect;
    }

    private static void PlayChopContact(BattlePaperDoll target, ActiveSkillData? skill, bool critical, int segment)
    {
        var strength = Mathf.Clamp(skill is null ? 1 : HitWeight(skill), .8f, 1.25f) * (critical ? 1.08f : 1);
        var dimensions = Vector2.One * 560 * strength;
        var effect = SpawnSequence(target, true, "impact", target.side_ink_body() + dimensions * .05f, dimensions, 21);
        if (effect is null) return;
        effect._delay = Mathf.Clamp(segment, 0, 6) * .055f;
        effect._holdAt = 1f / 21;
        effect._hold = ContactHoldSeconds(strength, critical);
    }

    public static void PlayHurt(BattlePaperDoll target, float weight = 1, bool critical = false)
    {
        if (target.is_defense_presenting()) return;
        var effect = SpawnSequence(target, false, "hurt", target.side_ink_body() - new Vector2(315, 0), new Vector2(900, 800), 21);
        if (effect is null) return;
        effect._holdAt = 1f / 21;
        effect._hold = ContactHoldSeconds(weight, critical);
    }

    private static void PlayGuard(BattlePaperDoll doll, bool awaitingContact = false)
    {
        // Preserve the pose and cinematic focus, without a second response effect.
        if (doll.is_defense_presenting()) return;
        doll.play_guard(awaitingContact);
    }

    public static void PlayRecovery(GodotObject caster)
    {
        if (caster is not Node2D unit || SideDoll(unit) is not { } doll) return;
        doll.play_rest();
        var effect = SpawnSequence(doll, false, "restore", new Vector2(140, -860), new Vector2(600, 660));
        if (effect is not null) effect._cancelOnDeath = true;
    }

    public static void PlayDefenseContact(GodotObject target, GodotObject? attacker = null)
    {
        if (target is Node2D unit && SideDoll(unit) is { } doll)
        {
            PlayGuard(doll);
            EmphasizeContact(attacker, unit, .9f);
        }
    }

    public static void PlayDodge(BattlePaperDoll doll)
    {
        doll.play_dodge();
        var effect = SpawnSequence(doll, false, "dodge", new Vector2(160, -850), new Vector2(1000, 700), 24);
        if (effect is not null) effect._cancelOnDeath = true;
    }

    public static void EmphasizeContact(GodotObject? attacker, Node2D target, float strength)
    {
        if (attacker is Node2D source) SideDoll(source)?.emphasize_contact(strength);
        if (SideDoll(target) is { } doll)
        {
            doll.emphasize_contact(strength);
        }
        BattleContactFocus.Play(attacker as Node2D, target, strength);
    }

    private void UpdateSequenceTransform()
    {
        if (!IsInstanceValid(_sequenceOwner)) return;
        if (IsInstanceValid(_worldSequenceOwner))
        {
            GlobalTransform = _worldSequenceOwner!.GlobalTransform * new Transform2D(new Vector2(_worldSequenceScale.X, 0), new Vector2(0, _worldSequenceScale.Y), Vector2.Zero);
            ZIndex = BattlePresentationLayers.Effects;
            return;
        }
        var mirror = _sequenceOwner!.side_ink_faces_left() ? -1f : 1f;
        GlobalTransform = _sequenceOwner.side_ink_visual_transform() * new Transform2D(new Vector2(mirror, 0), Vector2.Down, Vector2.Zero);
        ZIndex = BattlePresentationLayers.Effects;
    }

    private void ProcessSequence(float delta)
    {
        if (!IsInstanceValid(_sequenceOwner) || !_sequenceOwner!.IsInsideTree() || _sequenceOwner.IsQueuedForDeletion() || (!_sequenceOwner.uses_side_ink() && !IsInstanceValid(_worldSequenceOwner))
            || (_cancelOnDeath && _sequenceOwner.side_ink_dead())) { QueueFree(); return; }
        _elapsed += delta;
        if (_elapsed <= _followUntil) UpdateSequenceTransform();
        var local = Mathf.Max(0, _elapsed - _delay);
        var visual = local - Mathf.Clamp(local - _holdAt, 0, _hold);
        _sequenceSprite!.Visible = _elapsed >= _delay;
        _sequenceSprite.Frame = Mathf.Clamp(Mathf.FloorToInt(visual * _sequenceFps + .0001f), 0, _sequenceSprite.SpriteFrames.GetFrameCount(_sequenceSprite.Animation) - 1);
        if (_elapsed >= _duration + _delay + _hold) QueueFree();
    }
}
