using Godot;
using Godot.Collections;

public partial class BattleManager : Node2D
{
    private float _battleVisualElapsedSeconds;

    public override void _Process(double delta)
    {
        FitBattleFormation();
        if(!battle_running||_battlePaused||FlowTransitionActive)return;
        if (_realtimeMode) { ProcessRealtimeBattle((float)delta * SimulationFps * battleSpeed); return; }
        // 倍速只改变实时队列推进；技能演出与目标提示仍由统一结算入口控制。
        foreach(var creature in all_units.OfType<Creature>().Where(IsInstanceValid))creature.tick_status_frames((float)delta*60);
        if (turn_state != TurnState.IDLE || CheckBattleOver()) return;
        _statusPanel?.refresh_all();
        foreach (var unit in all_units)
        {
            if (!Alive(unit)) continue;
            var gauge = action_gauges[unit].AsSingle() + Initiative(unit) * (float)delta * battleSpeed;
            action_gauges[unit] = Mathf.Min(gauge, ActionThreshold);
        }
        var ready = all_units.Where(unit => Alive(unit) && action_gauges[unit].AsSingle() >= ActionThreshold).OrderByDescending(unit => action_gauges[unit].AsSingle()).FirstOrDefault();
        if (GodotObject.IsInstanceValid(ready)) BeginTurn(ready!);
    }

    private void InitializeRealtimeBattle()
    {
        _pendingZhouTianAutoCasts.Clear();
        _runningZhouTianAutoCasts.Clear();
        foreach (var unit in all_units)
        {
            gongfa_combat.Begin(unit);
            ApplyEquippedCombatPassives(unit);
            var loadout = LoadCombatSkills(unit);
            _combatSkills[unit] = loadout;
            _skillCooldowns[unit] = new System.Collections.Generic.Dictionary<ActiveSkillData, float>();
            _skillCharges[unit] = new System.Collections.Generic.Dictionary<ActiveSkillData, int>();
            foreach (var skill in loadout) if (skill.max_charges > 0) _skillCharges[unit][skill] = skill.max_charges;
            _energyRegen[unit] = 0;
            _enemyPlanTimers[unit] = 0;
        }
        CallDeferred(nameof(BeginPlanningSegment));
    }

    private void ApplyEquippedCombatPassives(GodotObject unit)
    {
        if (!unit.HasMeta("combat_passive_paths")) return;
        var value = unit.GetMeta("combat_passive_paths");
        if (value.VariantType != Variant.Type.Array) return;
        foreach (var item in value.AsGodotArray())
        {
            var skill = ResourceLoader.Load<ActiveSkillData>(item.AsString());
            if (skill is null || !GongfaAvailability.IsSkillEnabled(skill) || skill.skill_type != ActiveSkillData.SkillType.POWER || string.IsNullOrWhiteSpace(skill.source_gongfa_name)) continue;
            // 主修/辅修秘籍快照自动提供心法被动；心法不经过技能队列，也不支付资源。
            gongfa_combat.Utility(unit, skill);
        }
    }

    private Array<ActiveSkillData> LoadCombatSkills(GodotObject unit)
    {
        var result = new Array<ActiveSkillData>();
        if (unit.HasMeta("combat_resolved_skills") && unit.GetMeta("combat_resolved_skills").AsGodotArray().Count > 0)
            return new Array<ActiveSkillData>(unit.GetMeta("combat_resolved_skills").AsGodotArray().Select(value =>
                value.AsGodotObject() is ActiveSkillData skill && Character.is_combat_skill_slot_eligible(skill) ? skill : ArtifactSkillFactory.EmptySlot()));
        var paths = new List<string>();
        void AddMetaPaths(string key)
        {
            if (!unit.HasMeta(key)) return;
            var value = unit.GetMeta(key);
            if (value.VariantType != Variant.Type.Array) return;
            foreach (var item in value.AsGodotArray()) { var path = item.AsString(); if (!string.IsNullOrEmpty(path) && !paths.Contains(path)) paths.Add(path); }
        }
        AddMetaPaths("combat_skill_slot_paths");
        var slotConfigExplicit = unit.HasMeta("combat_skill_slots_configured") && unit.GetMeta("combat_skill_slots_configured").AsBool();
        if (paths.Count == 0 && !slotConfigExplicit && unit is Character character)
            foreach (var path in character.get_combat_skill_slot_paths()) if (!string.IsNullOrEmpty(path) && !paths.Contains(path)) paths.Add(path);
        if (paths.Count == 0 && !slotConfigExplicit) AddMetaPaths("loaded_skill_paths");
        if (paths.Count == 0 && !slotConfigExplicit) AddMetaPaths("active_skill_paths");
        if (unit is Character owner)
        {
            var resolvedPaths = new List<string>();
            foreach (var path in paths)
            {
                var resolved = owner.resolve_combat_skill_slot_path(path);
                if (!string.IsNullOrWhiteSpace(resolved) && !resolvedPaths.Contains(resolved)) resolvedPaths.Add(resolved);
            }
            paths = resolvedPaths;
        }
        if (unit is Character artifactOwner)
        {
            var keys = artifactOwner.get_combat_skill_slot_paths();
            foreach (var key in keys)
                result.Add(artifactOwner.resolve_combat_skill(key) ?? ArtifactSkillFactory.EmptySlot());
            return result;
        }
        foreach (var path in paths)
        {
            var skill = ResourceLoader.Load<ActiveSkillData>(path)?.Duplicate() as ActiveSkillData;
            if (skill is not null && Character.is_combat_skill_slot_eligible(skill)) result.Add(skill);
            if (result.Count >= CombatSlotCapacity) break;
        }
        if (result.Count == 0 && !slotConfigExplicit)
        {
            foreach (var item in CultivationDeckBuilder.collect_cards(unit))
            {
                if (item is not ActiveSkillData skill || !Character.is_combat_skill_slot_eligible(skill)) continue;
                if (result.Any(existing => existing.skill_name == skill.skill_name)) continue;
                result.Add(skill);
                if (result.Count >= CombatSlotCapacity) break;
            }
        }
        // 槽位顺序严格沿用角色面板的装配顺序；心法/固有能力不进入技能槽。
        return new Array<ActiveSkillData>(result.Take(CombatSlotCapacity));
    }

    private void ShowRealtimeSkills(GodotObject unit)
    {
        if (_skillBar is null || !Alive(unit)) return;
        active_unit = unit;
        _skillBar.set_battle_unit(unit);
        _skillBar.set_loadout(_combatSkills.GetValueOrDefault(unit, []));
        _skillBar.Show();
    }

    private void ProcessRealtimeBattle(float frames)
    {
        if (_segmentPhase == SegmentPhase.Planning)
        {
            AdvancePlanning(frames);
            return;
        }
        if (_segmentPhase == SegmentPhase.Executing)
        {
            ProcessSegmentExecution(frames);
            return;
        }
        if (_realtimeMode) return;
        if (_battlePaused || CheckBattleOver()) return;
        _battleVisualElapsedSeconds += frames / SimulationFps;
        UpdateShieldVisualClocks();
        foreach (var unit in all_units)
        {
            if (unit is Creature creature && GodotObject.IsInstanceValid(creature)) creature.tick_status_frames(frames);
        }

        _livingUnits.Clear();
        foreach (var unit in all_units)
            if (Alive(unit)) _livingUnits.Add(unit);

        for (var i = 0; i < _livingUnits.Count; i++)
        {
            var unit = _livingUnits[i];
            RegenerateResources(unit, frames);
            TickSkillCooldowns(unit, frames);
            AdvanceQueue(unit, frames);
        }
        // 所有角色先推进自身队列，再让敌方更新“已经看见了什么”，避免敌方读到
        // 玩家本模拟步之前的旧状态，也不允许规划器直接读取玩家隐藏队列。
        for (var i = 0; i < _livingUnits.Count; i++)
        {
            var unit = _livingUnits[i];
            if (unit.Get("faction").AsInt32() == (int)WorldEntity.Faction.PLAYER) continue;
            UpdateEnemyKnowledge(unit, frames);
            PlanEnemyQueue(unit, frames);
        }
        _statusPanel?.refresh_all();
    }

    private void TickSkillCooldowns(GodotObject unit, float frames)
    {
        if (!_skillCooldowns.TryGetValue(unit, out var cooldowns)) return;
        // Updating existing values does not invalidate the key collection enumerator.
        foreach (var skill in cooldowns.Keys) cooldowns[skill] = Mathf.Max(0, cooldowns[skill] - frames);
    }

    private void RegenerateResources(GodotObject unit, float frames)
    {
        var energyFactor = RegenFactor(CardRules.Number(unit, "physique") * 2f);
        _energyRegen[unit] = _energyRegen.GetValueOrDefault(unit, 0) + frames * energyFactor;
        while (_energyRegen[unit] >= EnergyBaseInterval) { _energyRegen[unit] -= EnergyBaseInterval; unit.Set("current_stamina", Mathf.Min(unit.Get("max_stamina").AsInt32(), unit.Get("current_stamina").AsInt32() + 1)); }
    }
    private static float RegenFactor(float attributes) => Creature.calculate_combat_regen_factor(attributes);

    /// <summary>当前体力距离下一点恢复的连续进度（0—1），供资源槽显示小数填充。</summary>
    public float get_energy_regen_progress(GodotObject unit) =>
        Mathf.Clamp(_energyRegen.GetValueOrDefault(unit, 0f) / EnergyBaseInterval, 0f, 1f);
    private void UpdateShieldVisualClocks()
    {
        foreach (var unit in all_units)
        {
            if (unit is not Node node) continue;
            var shieldSurface = node.GetNodeOrNull<Sprite2D>("BattleShieldAura/ManaShieldSurface");
            if (shieldSurface?.Material is ShaderMaterial material)
                material.SetShaderParameter("effect_time", _battleVisualElapsedSeconds);
        }
    }
}
