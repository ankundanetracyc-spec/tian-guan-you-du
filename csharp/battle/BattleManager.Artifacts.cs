using Godot;
using GArray = Godot.Collections.Array;

public partial class BattleManager
{
    private readonly Dictionary<GodotObject, ArtifactBattleRuntime> _artifactRuntime = [];
    private readonly Dictionary<GodotObject, List<ArtifactInstanceData>> _artifactItems = [];
    private readonly List<ArtifactBody> _artifactBodies = [];

    private void InitializeArtifacts(GodotObject owner, BattleEntityData snapshot)
    {
        var items = snapshot.equipped_artifacts.Where(item => item is not null && ArtifactSkillFactory.IsConfigured(item))
            .DistinctBy(item => item.instance_id).ToList();
        _artifactItems[owner] = items;
        owner.SetMeta("artifact_bond_sword_bonus", ArtifactBondRuntime.Aggregate(items).SwordDamageBonus);
        if (!snapshot.artifact_bond_defense_applied && ArtifactBondRuntime.Aggregate(items).DefenseBonus > 0)
            owner.Set("physical_defense", Mathf.FloorToInt(snapshot.physical_defense * (1 + ArtifactBondRuntime.Aggregate(items).DefenseBonus)));
        if (items.FirstOrDefault(ForgedArtifactInventory.IsForged) is { } forged) owner.SetMeta("forged_sword_json", forged.forged_snapshot_json);
        else owner.RemoveMeta("forged_sword_json");
        var baseDamage = items.Where(ForgedArtifactInventory.IsForged).Select(item => { ForgedArtifactInventory.TryRead(item, out var sword); return sword; })
            .SelectMany(sword => sword.Effects).Where(effect => effect.CarrierId == "base" && effect.AtomId == "physical_damage_percent").Sum(effect => effect.Value) / 100;
        owner.SetMeta("forged_physical_bonus", baseDamage);
        _artifactRuntime[owner] = new(items.Where(item => item.ritual_mode == "外祭").Select(item => (item.instance_id, item.spirit_cost)));
        owner.Set("used_spirit_perception", Math.Max(0, snapshot.used_spirit_perception));
    }

    public IEnumerable<GodotObject> get_target_candidates() => all_units.Concat(_artifactBodies
        .Where(body => GodotObject.IsInstanceValid(body) && body.current_health > 0 && body.DepartureReason.Length == 0)
        .Cast<GodotObject>());

    public bool IsXuanShuSkillSlot10(GodotObject owner,ActiveSkillData skill)=>gongfa_combat.HasPower(owner,"玄枢含章")&&_combatSkills.TryGetValue(owner,out var skills)&&skills.Count>9&&ReferenceEquals(skills[9],skill);

    public GodotObject? get_random_legal_skill_target(GodotObject owner,ActiveSkillData skill)
    {
        GodotObject? chosen=null;
        uint legalCount=0;
        foreach(var candidate in get_target_candidates())
        {
            if(!IsValidTarget(skill,owner,candidate))continue;
            legalCount++;
            if(GD.Randi()%legalCount==0)chosen=candidate;
        }
        return chosen;
    }

    private bool HasLegalSkillTarget(GodotObject owner,ActiveSkillData skill) =>
        get_target_candidates().Any(candidate=>IsValidTarget(skill,owner,candidate));

    private bool CanBeginFumingReplay(GodotObject owner,ActiveSkillData fuming)
    {
        var copied=gongfa_combat.GetLastNonResponseSkill(owner);
        if(copied is null||CardRules.IsResponse(copied)||copied.skill_name=="复命"||!is_skill_ready(owner,copied)||!HasLegalSkillTarget(owner,copied))return false;
        var (firstEnergy,secondEnergy)=gongfa_combat.PreviewSequentialEnergyCosts(owner,fuming,copied);
        if(copied.skill_type==ActiveSkillData.SkillType.MOVEMENT)
            secondEnergy=Mathf.CeilToInt(secondEnergy*BattleWoundRules.get_energy_cost_multiplier(get_realtime_wound_counts(owner),copied));
        var mana=get_skill_mana_cost(owner,fuming)+get_skill_mana_cost(owner,copied);
        return owner.Get("current_stamina").AsInt32()>=firstEnergy+secondEnergy&&can_pay_mana_amount(owner,mana);
    }

    private bool CanPayFumingCopyAfterItsOwnCast(GodotObject owner,ActiveSkillData copied)
    {
        return !CardRules.IsResponse(copied)&&copied.skill_name!="复命"&&is_skill_ready(owner,copied)
            &&owner.Get("current_stamina").AsInt32()>=get_skill_energy_cost(owner,copied)
            &&can_pay_skill_mana_cost(owner,copied);
    }

    public bool BeginFumingReplay(GodotObject owner,ActiveSkillData fuming,bool finishAsAutomatic,bool isSlot10Automatic=false)
    {
        var copied=gongfa_combat.GetLastNonResponseSkill(owner);
        if(copied is null||!CanPayFumingCopyAfterItsOwnCast(owner,copied))
        {
            if(finishAsAutomatic)on_automatic_skill_resolved(owner);else on_queued_skill_resolved(owner);
            return false;
        }
        var target=get_random_legal_skill_target(owner,copied);
        if(!GodotObject.IsInstanceValid(target))
        {
            if(finishAsAutomatic)on_automatic_skill_resolved(owner);else on_queued_skill_resolved(owner);
            return false;
        }
        var replay=copied.Duplicate(true) as ActiveSkillData??copied;
        if(fuming.gongfa_rewrite_effect_ids.Contains("xuan_shu_c4_fuming"))
        {
            if(!replay.gongfa_rewrite_effect_ids.Contains("xuan_shu_c4_fuming"))replay.gongfa_rewrite_effect_ids.Add("xuan_shu_c4_fuming");
            if(!replay.tags.Contains("护盾"))replay.tags.Add("护盾");
        }
        Pay(owner,replay);
        gongfa_combat.Before(owner,replay,recordAsLastCast:false);
        StartSkillCooldown(owner,copied);
        if(replay.aoe_range>0||replay.description.Contains("全体"))_executor?.execute_aoe_skill(owner,replay,Vector2I.Zero,Map??this,false,false,isSlot10Automatic,finishAsAutomatic);
        else _executor?.execute_skill(owner,replay,target!,false,false,isSlot10Automatic,finishAsAutomatic);
        return true;
    }

    private bool CanStartAutomaticSlotSkill(GodotObject owner,ActiveSkillData skill)
    {
        if(skill.combat_empty_slot||CardRules.IsResponse(skill)||!is_skill_ready(owner,skill)||!gongfa_combat.CanPlay(owner,skill))return false;
        if(skill.skill_name=="复命")return CanBeginFumingReplay(owner,skill);
        return owner.Get("current_stamina").AsInt32()>=get_skill_energy_cost(owner,skill)&&can_pay_skill_mana_cost(owner,skill);
    }

    private bool TryStartPendingZhouTianAutoCast(GodotObject owner)
    {
        if(_runningZhouTianAutoCasts.Contains(owner)||!_pendingZhouTianAutoCasts.TryGetValue(owner,out var pending)||pending<=0)return false;
        while(pending>0)
        {
            pending--;
            if(_combatSkills.TryGetValue(owner,out var skills)&&skills.Count>9&&skills[9] is { } skill&&CanStartAutomaticSlotSkill(owner,skill))
            {
                _pendingZhouTianAutoCasts[owner]=pending;
                _runningZhouTianAutoCasts.Add(owner);
                if(skill.skill_name=="复命"){Pay(owner,skill);gongfa_combat.Before(owner,skill);StartSkillCooldown(owner,skill);BeginFumingReplay(owner,skill,finishAsAutomatic:true,isSlot10Automatic:true);return true;}
                var target=skill.target_type==0?owner:get_random_legal_skill_target(owner,skill);
                if(GodotObject.IsInstanceValid(target))
                {
                    Pay(owner,skill);gongfa_combat.Before(owner,skill);StartSkillCooldown(owner,skill);
                    if(skill.aoe_range>0||skill.description.Contains("全体"))_executor?.execute_aoe_skill(owner,skill,Vector2I.Zero,Map??this,false,false,true,true);
                    else _executor?.execute_skill(owner,skill,target!,false,false,true,true);
                    return true;
                }
                _runningZhouTianAutoCasts.Remove(owner);
            }
        }
        _pendingZhouTianAutoCasts.Remove(owner);
        return false;
    }

    public void on_automatic_skill_resolved(GodotObject owner)
    {
        ReleaseForgedSpirit(owner);
        if(!_runningZhouTianAutoCasts.Remove(owner))return;
        if(TryStartPendingZhouTianAutoCast(owner))return;
        if(_segmentPhase==SegmentPhase.Executing&&_resolvingUnit==owner)CompleteSegmentAction(owner);
    }

    private GodotObject? ResolveActionTarget(GodotObject owner, BattleQueuedSkill action, ActiveSkillData skill)
    {
        if (skill.target_type == 0) return owner;
        if (IsValidTarget(skill, owner, action.target)) return action.target;
        var candidates = get_target_candidates().Where(target => IsValidTarget(skill, owner, target)).ToArray();
        return candidates.Length == 0 ? null : candidates[(int)(GD.Randi() % candidates.Length)];
    }

    private void PreviewArtifactQueue(GodotObject owner)
    {
        if (!_artifactRuntime.TryGetValue(owner, out var runtime) || !battle_units.ContainsKey(owner)) return;
        var queue = get_battle_unit(owner).skill_queue;
        var intents = runtime.PreviewQueue(queue.Select(action => action.artifact_instance_id));
        for (var i = 0; i < queue.Count; i++)
        {
            if (queue[i].artifact_intent != intents[i]) queue[i].target = null;
            queue[i].artifact_intent = intents[i];
        }
    }

    public ActiveSkillData get_next_action_skill(GodotObject owner, ActiveSkillData skill)
    {
        if (skill.artifact_instance_id.Length == 0 || skill.artifact_is_summon || !_artifactRuntime.TryGetValue(owner, out var runtime)) return skill;
        var sources = battle_units.ContainsKey(owner) ? get_battle_unit(owner).skill_queue
            .Where(action => !action.completed).Select(action => action.artifact_instance_id).ToList() : [];
        sources.Add(skill.artifact_instance_id);
        return runtime.PreviewQueue(sources)[^1] == ArtifactBattleRuntime.Intent.Summon ? skill.artifact_summon_skill! : skill;
    }

    private int QueuedUseCount(GodotObject owner, ActiveSkillData skill) => get_battle_unit(owner).skill_queue.Count(action =>
        !action.completed && action.kind == BattleQueuedSkill.ActionKind.Skill && action.skill == skill
        && action.artifact_intent != ArtifactBattleRuntime.Intent.Summon);

    private bool CanResolveArtifactAction(GodotObject owner, BattleQueuedSkill action)
    {
        if (!_combatSkills.TryGetValue(owner, out var installed) || !installed.Contains(action.skill)
            || !_artifactRuntime.TryGetValue(owner, out var runtime) || !runtime.CanExecute(action.artifact_instance_id, action.artifact_intent)) return false;
        if (action.artifact_intent == ArtifactBattleRuntime.Intent.Summon
            && !runtime.CanSummon(action.artifact_instance_id, owner.Get("max_spirit_perception").AsInt32(), OtherSpirit(owner))) return false;
        var skill = action.execution_skill;
        if (action.artifact_intent == ArtifactBattleRuntime.Intent.Use && (!HasAvailableSegmentSkillUses(owner, action.skill)
            || action.skill.max_charges > 0 && _skillCharges.TryGetValue(owner, out var charges) && charges.GetValueOrDefault(action.skill, action.skill.max_charges) <= 0)) return false;
        if (!gongfa_combat.CanPlay(owner, skill)) return false;
        return owner.Get("current_stamina").AsInt32() >= get_skill_energy_cost(owner, skill)
            && can_pay_skill_mana_cost(owner,skill);
    }

    public bool can_plan_artifact_summon(GodotObject owner, ActiveSkillData skill)
    {
        if (!_artifactRuntime.TryGetValue(owner, out var runtime) || !runtime.CanExecute(skill.artifact_instance_id, ArtifactBattleRuntime.Intent.Summon)) return false;
        var item = _artifactItems[owner].FirstOrDefault(item => item.instance_id == skill.artifact_instance_id);
        if (item is null) return false;
        var pendingIds = get_battle_unit(owner).skill_queue.Where(action => !action.completed && action.artifact_intent == ArtifactBattleRuntime.Intent.Summon)
            .Select(action => action.artifact_instance_id).ToHashSet(StringComparer.Ordinal);
        var pending = _artifactItems[owner].Where(item => pendingIds.Contains(item.instance_id)).Sum(item => (long)item.spirit_cost);
        return OtherSpirit(owner) + runtime.UsedSpirit + pending + item.spirit_cost <= owner.Get("max_spirit_perception").AsInt32();
    }

    private long OtherSpirit(GodotObject owner) => Math.Max(0, (long)owner.Get("used_spirit_perception").AsInt32()
        - (_artifactRuntime.TryGetValue(owner, out var runtime) ? runtime.UsedSpirit : 0));

    private void SyncArtifactSpirit(GodotObject owner, long other)
    {
        owner.Set("used_spirit_perception", (int)Math.Min(int.MaxValue, other + _artifactRuntime[owner].UsedSpirit));
    }

    private void ResolveArtifactSummon(GodotObject owner, BattleQueuedSkill action)
    {
        var item = _artifactItems[owner].First(item => item.instance_id == action.artifact_instance_id);
        var other = OtherSpirit(owner);
        if (!_artifactRuntime[owner].TryCommitSummon(item.instance_id, owner.Get("max_spirit_perception").AsInt32(), other))
        { SkipSegmentAction(owner); _resolvingUnit = null; return; }
        Pay(owner, action.execution_skill);
        var index = _artifactBodies.Count(body => body.Caster == owner);
        var body = new ArtifactBody
        {
            Name = "ArtifactBody", Caster = owner, InstanceId = item.instance_id, WasSummoned = true,
            entity_name = item.artifact_name, faction = owner.Get("faction").AsInt32(),
            current_health = item.outer_body, max_health = item.outer_body,
            Position = (owner as Node2D)?.Position + new Vector2(105 + index * 150, -100) ?? Vector2.Zero,
            Visible = !_spectatorBattle
        };
        // 显示位置不参与目标合法性；观战隐藏表现时仍须可被选作逻辑目标。
        (DeployLayer ?? this).AddChild(body);
        _artifactBodies.Add(body);
        SyncArtifactSpirit(owner, other);
        _executor?.show_action_name(owner, action.get_display_name());
        _planHud?.PresentAction(owner);
        CompleteSystemActionPresentation(owner);
    }

    public int damage_artifact_body(GodotObject attacker, ActiveSkillData skill, ArtifactBody body)
    {
        if (!get_target_candidates().Contains(body) || !ArtifactAcceptsSkill(body, attacker, skill)) return 0;
        var damage = Math.Max(0, Mathf.RoundToInt(BattleActionResolver.preview_damage(attacker, skill)));
        var actual = Math.Min(body.current_health, damage);
        body.current_health -= actual;
        body.Refresh();
        _executor?.show_damage_number(body, actual, skill.damage_type, false);
        if (body.current_health == 0) depart_artifact(body.Caster, body.InstanceId, "collapsed");
        return actual;
    }

    public bool depart_artifact(GodotObject owner, string instanceId, string reason = "departed")
    {
        if (!_artifactRuntime.TryGetValue(owner, out var runtime)) return false;
        var other = OtherSpirit(owner);
        var collapsed = reason == "collapsed";
        if (!(collapsed ? runtime.TryCollapse(instanceId) : runtime.TryDepart(instanceId))) return false;
        var body = _artifactBodies.FirstOrDefault(body => body.Caster == owner && body.InstanceId == instanceId);
        if (body is not null) { body.Collapsed = collapsed; body.DepartureReason = reason; body.Hide(); }
        SyncArtifactSpirit(owner, other);
        _skillBar?.refresh_slots();
        return true;
    }

    private void LeaveDeadOwnerArtifacts()
    {
        foreach (var owner in _artifactRuntime.Keys.Where(owner => !Alive(owner)).ToArray())
        {
            ReleaseForgedSpirit(owner);
            foreach (var body in _artifactBodies.Where(body => body.Caster == owner && body.DepartureReason.Length == 0).ToArray())
                depart_artifact(owner, body.InstanceId, "owner_defeated");
        }
    }

    public static bool ArtifactAcceptsSkill(ArtifactBody body, GodotObject caster, ActiveSkillData skill) =>
        body.current_health > 0 && body.DepartureReason.Length == 0 && skill.target_type is 2 or 3 && body.faction != caster.Get("faction").AsInt32()
        && skill.damage_type is "physical" or "magic" or "magical"
        && (skill.base_damage > 0 || skill.damage_bonuses.Count > 0 || !string.IsNullOrWhiteSpace(skill.damage_formula));

    public Godot.Collections.Array<Godot.Collections.Dictionary> build_artifact_reports()
    {
        var reports = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        foreach (var (owner, items) in _artifactItems)
        foreach (var item in items.Where(item => item.ritual_mode == "外祭"))
        {
            var body = _artifactBodies.FirstOrDefault(body => body.Caster == owner && body.InstanceId == item.instance_id);
            reports.Add(new Godot.Collections.Dictionary
            {
                ["owner_id"] = owner.Get("unique_id").AsString(), ["instance_id"] = item.instance_id,
                ["was_summoned"] = body is not null, ["max_body"] = item.outer_body,
                ["remaining_body"] = body?.current_health ?? 0, ["collapsed"] = body?.Collapsed ?? false,
                ["departure_reason"] = body?.DepartureReason ?? ""
            });
        }
        return reports;
    }

    private void FinishArtifacts()
    {
        foreach (var owner in _forgedTemporarySpirit.Keys.ToArray()) ReleaseForgedSpirit(owner);
        foreach (var owner in _artifactRuntime.Keys)
        {
            foreach (var body in _artifactBodies.Where(body => body.Caster == owner && body.DepartureReason.Length == 0).ToArray())
                depart_artifact(owner, body.InstanceId, "battle_ended");
            var other = OtherSpirit(owner);
            _artifactRuntime[owner].FinishBattle();
            SyncArtifactSpirit(owner, other);
        }
    }

    private void ResetArtifacts()
    {
        foreach (var owner in _forgedTemporarySpirit.Keys.ToArray()) ReleaseForgedSpirit(owner);
        foreach (var body in _artifactBodies) if (GodotObject.IsInstanceValid(body)) body.QueueFree();
        _artifactBodies.Clear(); _artifactItems.Clear(); _artifactRuntime.Clear();
    }
}
