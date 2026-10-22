using Godot;

/// <summary>Presentation-only routing for the real skill catalogue. No combat state is written.</summary>
public partial class BattleInkCombatVfx : Node2D
{
    public enum Kind { Strike, Release, Heavy, Flow, Spirit, SpiritBurst, SoulNeedle, SoulDraw, Thunder, Fire, Bite, Pounce, Charge, Thrust, Guard, Recovery }
    public const float ContactSeconds = 0.18f;
    private const int Capacity = 32;
    private static int _active;
    private static uint _sequence;
    private static Shader? _shader;
    private static ImageTexture? _carrier;
    private ShaderMaterial? _material;
    private float _elapsed;
    private float _duration;
    private float _delay;
    private float _hold;
    private float _holdAt;
    private bool _registered;

    public static bool DealsDamage(ActiveSkillData skill) => !Mathf.IsZeroApprox(skill.base_damage)
        || skill.damage_bonuses.Count > 0 || !string.IsNullOrWhiteSpace(skill.damage_formula);

    // Presentation only: self-directed buffs/utilities use the approved quiet recovery pose.
    // Response cards retain their contact guard, even though they also target themselves.
    private static bool IsSelfBuffPresentation(ActiveSkillData skill) => skill.target_type == 0
        && !CardRules.IsResponse(skill)
        && skill.skill_type is ActiveSkillData.SkillType.BUFF or ActiveSkillData.SkillType.SPECIAL or ActiveSkillData.SkillType.POWER;

    public static Kind Profile(ActiveSkillData skill)
    {
        var name = skill.skill_name;
        if (!DealsDamage(skill)) return !CardRules.IsResponse(skill)
            && (IsSelfBuffPresentation(skill) || name is "回气" or "养息" or "调息" or "平息" or "去故" or "还阳" or "偃月" or "吐纳归元" or "聚息" or "回春丹")
            ? Kind.Recovery : Kind.Guard;
        return name switch
        {
            "雷击符" or "惊蛰" => Kind.Thunder,
            "爆炎符" or "烈焰突刺" => Kind.Fire,
            "魂刺" => Kind.SoulNeedle,
            "摄魂" => Kind.SoulDraw,
            "灵爆" => Kind.SpiritBurst,
            "灵击" => Kind.Spirit,
            "撕咬" or "裂喉" => Kind.Bite,
            "掠影扑杀" => Kind.Pounce,
            "拱撞" or "疾拱" => Kind.Charge,
            "痛击" or "蓄势重击" or "血臂撼岳" or "撼山" or "合劲" or "引筋" => Kind.Heavy,
            "发劲" or "挟势" => Kind.Release,
            "解形" or "冯虚" or "百骸" => Kind.Flow,
            "突刺" => Kind.Thrust,
            _ when skill.damage_type == "soul" => Kind.SoulNeedle,
            _ when skill.damage_type is "magic" or "spiritual" => Kind.Spirit,
            _ => Kind.Strike
        };
    }

    public static string Describe(ActiveSkillData skill) => IsJingzhe(skill)
        ? "原地提指引雷，冰白雷芯裹着灰蓝雷柱自上向下贯落，短停后分叉余电收散。"
        : UsesHandChop(skill)
        ? "侧向下劈姿势配合淡彩水墨序列帧，接触处短停、小范围墨花与自然漂散。"
        : Profile(skill) switch
    {
        Kind.Strike => "短促顿笔沿出手方向推进，接触处积墨压缩，再向受力方向散开。",
        Kind.Release => "前窄后厚的劲墨推出，落点凝成浓墨，余劲拖出枯笔。",
        Kind.Heavy => "厚重笔势先收后放，命中顿住，再压出宽阔墨晕与短促受力痕。",
        Kind.Flow => "曲折的淡墨与枯笔沿交手方向穿行，每段落点各有短促顿挫。",
        Kind.Spirit => "青灰灵息凝成一滴长墨，沿施法者到目标的方向飞入，落点洇开。",
        Kind.SpiritBurst => "浓墨灵息裹着淡染拖尾推进，命中后沉积为层叠墨团。",
        Kind.SoulNeedle => "一缕细长枯墨直指目标识海，落点留下收束的深墨刺痕。",
        Kind.SoulDraw => "两道墨流向识海收拢，接触时内陷凝墨，再缓缓散淡。",
        Kind.Thunder => "灰黑雷墨从目标上方折落，落点短促压黑，断纹迅速收淡。",
        Kind.Fire => "赭墨卷流推进，落点展开浓淡烟墨，上卷消散，保留纸本色阶。",
        Kind.Bite => "上下两道牙痕向落点合拢，留下短促撕裂墨痕。",
        Kind.Pounce => "扑势顺行进方向掠入，三道枯墨撕痕错开收锋。",
        Kind.Charge => "低伏宽墨沿冲撞方向压入，接触处形成厚重顿笔，余墨沉落。",
        Kind.Thrust => "细长直锋沿攻击方向刺入，接触处留下尖锐枯笔。",
        Kind.Recovery => "轻收手臂调息，局部淡青气息回拢与细小叶屑衔接回待机。",
        _ => "架挡姿势与淡彩弧形笔势，接触处短促积墨，余劲自然收淡。"
    };

    public static float HitWeight(ActiveSkillData skill) => Profile(skill) switch
    {
        Kind.Heavy or Kind.Charge or Kind.SpiritBurst => 1.45f,
        Kind.SoulNeedle or Kind.Spirit or Kind.Flow => 0.72f,
        _ => 1f
    };

    public static float ContactHoldSeconds(float weight, bool critical = false) =>
        Mathf.Clamp(0.12f * weight, 0.10f, 0.18f) + (critical ? 0.035f : 0f);

    public static float AttackContactSeconds(GodotObject attacker) => attacker is Node2D node && SideDoll(node) is not null ? .30f : ContactSeconds;

    public static Vector2 Anchor(Node2D unit, bool head = false) => unit.GetNodeOrNull<BattlePaperDoll>("BattlePaperDoll") is { } doll
        ? doll.combat_anchor(head) : unit.GlobalPosition + new Vector2(0f, head ? -202f : -112f);

    public static BattleInkCombatVfx? PlayAttack(GodotObject attacker, GodotObject target, ActiveSkillData skill)
    {
        if (attacker is not Node2D from || target is not Node2D to || !DealsDamage(skill)) return null;
        if (IsJingzhe(skill) && to.GetNodeOrNull<BattlePaperDoll>("BattlePaperDoll") is { } lightningTarget) return PlayJingzhe(from, to, lightningTarget);
        if (SideDoll(from) is { } side && UsesHandChop(skill)) return PlayHandChop(side, skill);
        var kind = Profile(skill);
        var start = UsesThrowCast(skill) && SideDoll(from) is { } thrower ? thrower.throw_cast_origin() : Anchor(from);
        var end = Anchor(to, kind is Kind.SoulNeedle or Kind.SoulDraw);
        if (kind == Kind.Thunder) start = end + Vector2.Up * 250f;
        var offset = end - start;
        var distance = offset.Length();
        var direction = distance > 0.1f ? offset / distance : Facing(from);
        var height = kind is Kind.Heavy or Kind.Charge or Kind.SpiritBurst ? 230f : 174f;
        var effect = Spawn((start + end) * 0.5f, new Vector2(Mathf.Max(distance, 1f) + 100f, height), direction.Angle(), kind, 0, 0.44f, skill);
        if (effect is null) return null;
        effect._material!.SetShaderParameter("travel_span", distance / (distance + 100f));
        var contact = AttackContactSeconds(attacker);
        effect._material.SetShaderParameter("contact_progress", contact / effect._duration);
        effect._holdAt = contact;
        effect._hold = 0.035f + ContactHoldSeconds(HitWeight(skill));
        return effect;
    }

    public static void PlayImpact(GodotObject target, ActiveSkillData? skill = null, GodotObject? attacker = null, bool critical = false, bool evaded = false, int segment = 0)
    {
        if (target is not Node2D to) return;
        if (evaded && SideDoll(to) is { } dodger) { PlayDodge(dodger); EmphasizeContact(attacker, to, .7f); return; }
        if (SideDoll(to)?.is_defense_presenting() == true) { EmphasizeContact(attacker, to, .9f); return; }
        EmphasizeContact(attacker, to, skill is null ? 1 : HitWeight(skill));
        if (skill is not null && IsJingzhe(skill) && to.GetNodeOrNull<BattlePaperDoll>("BattlePaperDoll") is { } lightningTarget)
        {
            PlayJingzheContact(to, lightningTarget);
            return;
        }
        var kind = skill is null ? Kind.Strike : Profile(skill);
        if (!evaded && SideDoll(to) is { } side
            && (skill is null || UsesHandChop(skill)))
        {
            PlayChopContact(side, skill, critical, segment);
            return;
        }
        var end = Anchor(to, kind is Kind.SoulNeedle or Kind.SoulDraw);
        var offset = attacker is Node2D from ? end - Anchor(from) : Facing(to);
        var direction = offset.LengthSquared() > 0.01f ? offset.Normalized() : Vector2.Right;
        var weight = skill is null ? 1f : HitWeight(skill);
        var size = new Vector2(248f, 226f) * (critical ? weight * 1.10f : weight);
        if (kind == Kind.Thunder) direction = Vector2.Down;
        var effect = Spawn(end, size, direction.Angle(), kind, evaded ? 4 : 1, 0.44f, skill, critical);
        if (effect is not null)
        {
            effect._delay = Mathf.Clamp(segment, 0, 6) * 0.055f;
            if (!evaded) { effect._holdAt = 0.035f; effect._hold = ContactHoldSeconds(weight, critical); }
        }
    }

    public static void PlayUtility(GodotObject caster, GodotObject target, ActiveSkillData skill)
    {
        if (caster is not Node2D from || DealsDamage(skill)) return;
        if (SideDoll(from) is { } side)
        {
            if (Profile(skill) == Kind.Recovery) PlayRecovery(from);
            else PlayGuard(side, CardRules.IsResponse(skill));
            return;
        }
        Spawn(Anchor(from), new Vector2(210f, 280f), 0f, Profile(skill), 2, 0.62f, skill);
    }

    public static void PlayDeath(GodotObject target)
    {
        if (target is Node2D to) Spawn(Anchor(to), new Vector2(228f, 290f), 0f, Kind.Strike, 3, 0.82f);
    }

    private static Color Ink(Kind kind, ActiveSkillData? skill) => kind switch
    {
        Kind.Heavy => new Color(0.30f, 0.24f, 0.18f, 0.94f),
        Kind.Strike or Kind.Release => new Color(0.37f, 0.35f, 0.28f, 0.94f),
        Kind.Flow => new Color(0.32f, 0.38f, 0.34f, 0.90f),
        Kind.Fire => new Color(0.30f, 0.135f, 0.075f, 0.92f),
        Kind.Spirit or Kind.SpiritBurst => new Color(0.19f, 0.35f, 0.38f, 0.94f),
        Kind.SoulNeedle or Kind.SoulDraw => new Color(0.30f, 0.24f, 0.36f, 0.94f),
        Kind.Thunder => new Color(0.20f, 0.28f, 0.38f, 0.94f),
        Kind.Recovery => new Color(0.12f, 0.22f, 0.17f, 0.76f),
        _ when skill?.source_gongfa_name == "赤阳参同经" => new Color(0.23f, 0.10f, 0.072f, 0.95f),
        _ => new Color(0.038f, 0.052f, 0.046f, 0.94f)
    };

    private static Vector2 Facing(Node2D unit) => unit is Creature creature && creature.faction == (int)WorldEntity.Faction.ENEMY ? Vector2.Left : Vector2.Right;

    private static BattleInkCombatVfx? Spawn(Vector2 position, Vector2 size, float rotation, Kind kind, int phase, float duration, ActiveSkillData? skill = null, bool critical = false)
    {
        if (_active >= Capacity || Engine.GetMainLoop() is not SceneTree tree || tree.CurrentScene is null) return null;
        _shader ??= GD.Load<Shader>("res://shaders/battle_ink_action.gdshader");
        if (_shader is null) return null;
        if (_carrier is null)
        {
            var image = Image.CreateEmpty(8, 8, false, Image.Format.Rgba8);
            image.Fill(new Color(1f, 1f, 1f, 0f));
            _carrier = ImageTexture.CreateFromImage(image);
            image.Dispose();
        }
        // Formation actors use depth 565–720; strokes must remain above them and below damage labels.
        var effect = new BattleInkCombatVfx { Name = "CombatInk", _duration = duration, ProcessMode = ProcessModeEnum.Pausable, ZIndex = BattlePresentationLayers.Effects, ZAsRelative = false };
        tree.CurrentScene.AddChild(effect);
        effect.AddToGroup("battle_action_visuals");
        effect.GlobalPosition = position;
        effect.GlobalRotation = rotation;
        _active++; effect._registered = true;
        _sequence++;
        effect._material = new ShaderMaterial { Shader = _shader };
        effect._material.SetShaderParameter("ink_noise", BattleInkRibbon.SharedNoise);
        effect._material.SetShaderParameter("ink_color", phase == 3 ? new Color(0.08f, 0.09f, 0.08f, 0.92f) : Ink(kind, skill));
        effect._material.SetShaderParameter("kind", (float)kind);
        effect._material.SetShaderParameter("phase", (float)phase);
        effect._material.SetShaderParameter("critical", critical ? 1f : 0f);
        effect._material.SetShaderParameter("seed_value", (_sequence * 137u % 997u) * 0.007f);
        effect._material.SetShaderParameter("progress", 0f);
        effect.AddChild(new Sprite2D { Name = "InkSurface", Texture = _carrier, Scale = size / 8f, Material = effect._material });
        return effect;
    }

    public override void _Process(double delta)
    {
        if (_sequenceSprite is not null) { ProcessSequence((float)delta); return; }
        _elapsed += (float)delta;
        var local = Mathf.Max(0f, _elapsed - _delay);
        // 命中停顿只改变画面进度；管理器等待整个特效结束后再推进下一行动。
        var visual = local - Mathf.Clamp(local - _holdAt, 0f, _hold);
        _material?.SetShaderParameter("progress", Mathf.Clamp(visual / _duration, 0f, 1f));
        if (_elapsed >= _duration + _delay + _hold) QueueFree();
    }

    public override void _ExitTree()
    {
        if (_registered) { _active = Mathf.Max(_active - 1, 0); _registered = false; }
    }
}
