using Godot;

public partial class BattleManager
{
    private sealed class AiCandidate
    {
        public ActiveSkillData Skill = null!;
        public GodotObject? Target;
        public int Priority;
        public float Score;
        public int EnergyCost;
        public int ManaCost;
        public int Damage;
        public int Healing;
        public string AppliedStatus = "";
        public GodotObject? StatusTarget;
    }

    private void PlanEnemyTeams()
    {
        var forecast = CreateHealthForecast();
        foreach (var enemy in enemy_units.Where(Alive))
        {
            BuildAiQueue(enemy, forecast, false);
            ApplyQueueForecast(enemy, forecast);
        }
    }

    private void PlanAllyTeam()
    {
        if (_segmentPhase != SegmentPhase.Planning || !GodotObject.IsInstanceValid(_mainPlayerUnit)) return;
        var forecast = CreateHealthForecast();
        if (battle_units.ContainsKey(_mainPlayerUnit!))
            ApplyQueueForecast(_mainPlayerUnit!, forecast);
        foreach (var ally in player_units.Where(unit => Alive(unit) && unit != _mainPlayerUnit))
        {
            BuildAiQueue(ally, forecast, true);
            ApplyQueueForecast(ally, forecast);
        }
    }

    private Dictionary<GodotObject, float> CreateHealthForecast() => all_units
        .Where(Alive)
        .ToDictionary(unit => unit, unit => (float)unit.Get("current_health").AsInt32());

    private void BuildAiQueue(GodotObject unit, Dictionary<GodotObject, float> forecast, bool teammate)
    {
        if (!Alive(unit) || !_combatSkills.TryGetValue(unit, out var loadout)) return;
        var queue = get_battle_unit(unit).skill_queue;
        queue.Clear();
        var stamina = unit.Get("current_stamina").AsInt32();
        var mana = unit.Get("current_mana").AsInt32();
        var staminaMax = unit.Get("max_stamina").AsInt32();
        var manaMax = get_effective_mana_capacity(unit);
        var repeatCounts = new Dictionary<ActiveSkillData, int>();
        var plannedLimitedSkills = new HashSet<string>();
        var plannedStatuses = new HashSet<string>();
        var capacity = get_action_slot_capacity(unit);

        while (queue.Count < capacity)
        {
            var best = FindBestAiCandidate(unit, loadout, forecast, stamina, mana, repeatCounts, plannedLimitedSkills, plannedStatuses, teammate, true);
            if (best is not null && best.Priority > 0)
            {
                queue.Add(new BattleQueuedSkill(best.Skill, best.Target));
                stamina = Math.Max(0, stamina - best.EnergyCost);
                mana = Math.Max(0, mana - best.ManaCost);
                repeatCounts[best.Skill] = repeatCounts.GetValueOrDefault(best.Skill, 0) + 1;
                if (IsOncePerSegmentBodySkill(best.Skill)) plannedLimitedSkills.Add(best.Skill.skill_name);
                if (!string.IsNullOrEmpty(best.AppliedStatus) && GodotObject.IsInstanceValid(best.StatusTarget))
                    plannedStatuses.Add(StatusForecastKey(best.StatusTarget!, best.AppliedStatus));
                ApplyAiCandidateForecast(best, forecast, unit);
                continue;
            }

            var recovery = FindUsefulRecovery(unit, loadout, forecast, stamina, mana, staminaMax, manaMax,
                capacity - queue.Count, repeatCounts, plannedLimitedSkills, plannedStatuses, teammate);
            if (recovery == BattleQueuedSkill.ActionKind.Rest)
            {
                queue.Add(BattleQueuedSkill.Rest());
                stamina = Math.Min(staminaMax, stamina + 2);
                continue;
            }
            if (recovery == BattleQueuedSkill.ActionKind.Breathe)
            {
                queue.Add(BattleQueuedSkill.Breathe());
                mana = Math.Min(manaMax, mana + 1);
                continue;
            }
            break;
        }
    }

    private AiCandidate? FindBestAiCandidate(
        GodotObject unit,
        Godot.Collections.Array<ActiveSkillData> loadout,
        Dictionary<GodotObject, float> forecast,
        int stamina,
        int mana,
        Dictionary<ActiveSkillData, int> repeatCounts,
        HashSet<string> plannedLimitedSkills,
        HashSet<string> plannedStatuses,
        bool teammate,
        bool requireAffordable)
    {
        AiCandidate? best = null;
        foreach (var skill in loadout)
        {
            if (skill.skill_type == ActiveSkillData.SkillType.CURSE || !body_cards.CanPlay(unit, skill)) continue;
            if (IsOncePerSegmentBodySkill(skill) && plannedLimitedSkills.Contains(skill.skill_name)) continue;
            if (!is_skill_ready(unit, skill)) continue;
            var energyCost = get_skill_energy_cost(unit, skill);
            var manaCost = get_skill_mana_cost(unit, skill);
            if (requireAffordable && (energyCost > stamina || manaCost > mana)) continue;

            foreach (var target in AiTargets(unit, skill))
            {
                var candidate = ScoreAiSkill(unit, skill, target, forecast, energyCost, manaCost,
                    repeatCounts.GetValueOrDefault(skill), plannedStatuses, teammate);
                if (candidate.Priority <= 0) continue;
                if (best is null || candidate.Priority > best.Priority ||
                    candidate.Priority == best.Priority && candidate.Score > best.Score)
                    best = candidate;
            }
        }
        return best;
    }

    private BattleQueuedSkill.ActionKind? FindUsefulRecovery(
        GodotObject unit,
        Godot.Collections.Array<ActiveSkillData> loadout,
        Dictionary<GodotObject, float> forecast,
        int stamina,
        int mana,
        int staminaMax,
        int manaMax,
        int remainingSlots,
        Dictionary<ActiveSkillData, int> repeatCounts,
        HashSet<string> plannedLimitedSkills,
        HashSet<string> plannedStatuses,
        bool teammate)
    {
        if (remainingSlots <= 1) return null;
        var useful = new List<(int priority, float score, int energyGap, int manaGap)>();
        foreach (var skill in loadout)
        {
            if (skill.skill_type == ActiveSkillData.SkillType.CURSE || !body_cards.CanPlay(unit, skill)) continue;
            if (IsOncePerSegmentBodySkill(skill) && plannedLimitedSkills.Contains(skill.skill_name)) continue;
            if (!is_skill_ready(unit, skill)) continue;
            var energyCost = get_skill_energy_cost(unit, skill);
            var manaCost = get_skill_mana_cost(unit, skill);
            var energyGap = Math.Max(0, energyCost - stamina);
            var manaGap = Math.Max(0, manaCost - mana);
            if (energyGap == 0 && manaGap == 0) continue;
            var restCount = energyGap == 0 ? 0 : (energyGap + 1) / 2;
            var breatheCount = manaGap;
            if (restCount + breatheCount >= remainingSlots) continue;
            if (staminaMax - stamina < Math.Min(energyGap, restCount * 2) || manaMax - mana < breatheCount) continue;
            var bestTarget = AiTargets(unit, skill)
                .Select(target => ScoreAiSkill(unit, skill, target, forecast, energyCost, manaCost,
                    repeatCounts.GetValueOrDefault(skill), plannedStatuses, teammate))
                .OrderByDescending(candidate => candidate.Priority)
                .ThenByDescending(candidate => candidate.Score)
                .FirstOrDefault();
            if (bestTarget is null || bestTarget.Priority <= 0) continue;
            useful.Add((bestTarget.Priority, bestTarget.Score - (restCount + breatheCount) * 100, energyGap, manaGap));
        }
        if (useful.Count == 0) return null;

        var targetPlan = useful.OrderByDescending(item => item.priority).ThenByDescending(item => item.score).First();
        var canRest = targetPlan.energyGap > 0 && stamina < staminaMax;
        var canBreathe = targetPlan.manaGap > 0 && mana < manaMax;
        if (!canRest) return canBreathe ? BattleQueuedSkill.ActionKind.Breathe : null;
        if (!canBreathe) return BattleQueuedSkill.ActionKind.Rest;
        var energyShare = targetPlan.energyGap / (float)Math.Max(1, staminaMax - stamina);
        var manaShare = targetPlan.manaGap / (float)Math.Max(1, manaMax - mana);
        return energyShare >= manaShare ? BattleQueuedSkill.ActionKind.Rest : BattleQueuedSkill.ActionKind.Breathe;
    }

    private AiCandidate ScoreAiSkill(
        GodotObject unit,
        ActiveSkillData skill,
        GodotObject? target,
        Dictionary<GodotObject, float> forecast,
        int energyCost,
        int manaCost,
        int repeatCount,
        HashSet<string> plannedStatuses,
        bool teammate)
    {
        var result = new AiCandidate { Skill = skill, Target = target, EnergyCost = energyCost, ManaCost = manaCost };
        var selfTarget = skill.target_type == 0;
        var damage = EstimateAiDamage(unit, skill, target, forecast);
        if (damage > 0)
        {
            var hit = skill.aoe_range > 0 ? 1f : GodotObject.IsInstanceValid(target) ? BattleActionResolver.preview_hit_chance(unit, target!) : 1f;
            result.Damage = damage;
            var targetHealth = GodotObject.IsInstanceValid(target) ? forecast.GetValueOrDefault(target!, target!.Get("current_health").AsSingle()) : 0;
            var canFinish = targetHealth > 0 && damage >= targetHealth && hit >= .78f;
            result.Priority = canFinish ? 4 : 2;
            result.Score = damage * hit * 10f + (canFinish ? 1800f : 0f);
            if (targetHealth > 0) result.Score += (1f - Mathf.Clamp(targetHealth / Mathf.Max(1, target!.Get("max_health").AsSingle()), 0, 1)) * 90f;
        }

        var health = forecast.GetValueOrDefault(unit, unit.Get("current_health").AsSingle());
        var maxHealth = Mathf.Max(1, unit.Get("max_health").AsSingle());
        foreach (var effect in skill.special_effects)
        {
            var kind = effect.GetValueOrDefault("effect", "").AsString();
            switch (kind)
            {
                case "restore_health_percent":
                {
                    var amount = Math.Max(1, Mathf.RoundToInt(maxHealth * effect.GetValueOrDefault("percent", .03f).AsSingle()));
                    var missing = Mathf.Max(0, Mathf.RoundToInt(maxHealth - health));
                    var healing = Math.Min(amount, missing);
                    result.Healing += healing;
                    if (healing > 0)
                    {
                        result.Priority = health / maxHealth < .30f ? 5 : Math.Max(result.Priority, 3);
                        result.Score += health / maxHealth < .30f ? 1800 + healing * 12 : 400 + healing * 8;
                    }
                    break;
                }
                case "shield":
                {
                    var value = Math.Max(0, Mathf.RoundToInt(CardRules.Evaluate(effect.GetValueOrDefault("formula", "").AsString(), unit)));
                    if (value > 0 && health / maxHealth < .68f)
                    {
                        result.Priority = Math.Max(result.Priority, health / maxHealth < .30f ? 5 : 3);
                        result.Score += health / maxHealth < .30f ? 1450 + value * 9 : 360 + value * 7;
                    }
                    break;
                }
                case "gain_energy":
                case "restore_stamina":
                case "restore_mana":
                    result.Priority = Math.Max(result.Priority, 2);
                    result.Score += 240;
                    break;
            }
        }

        if (skill.skill_type == ActiveSkillData.SkillType.POWER &&
            !HasStatus(unit, "心法:" + skill.skill_name) && !HasStatus(unit, skill.skill_name))
        {
            result.Priority = Math.Max(result.Priority, 2);
            result.Score += 620;
        }

        foreach (var effect in skill.status_effects)
        {
            var id = effect.GetValueOrDefault("status_id", "").AsString();
            var owner = selfTarget ? unit : target;
            if (string.IsNullOrWhiteSpace(id) || !GodotObject.IsInstanceValid(owner) || HasStatus(owner!, id) ||
                plannedStatuses.Contains(StatusForecastKey(owner!, id))) continue;
            var status = ResourceLoader.Load<StatusEffect>($"res://resources/battle/statuses/{id}.tres");
            result.AppliedStatus = id;
            result.StatusTarget = owner;
            if (status?.is_debuff == true && owner!.Get("faction").AsInt32() != unit.Get("faction").AsInt32())
            {
                result.Priority = Math.Max(result.Priority, 3);
                result.Score += 760;
            }
            else
            {
                result.Priority = Math.Max(result.Priority, 2);
                result.Score += 420;
            }
        }

        var profile = GetAiProfile(unit);
        if (profile == "aggressive" && result.Damage > 0) result.Score += 180;
        if (profile == "guard" && (result.Healing > 0 || skill.special_effects.Any(effect => effect.GetValueOrDefault("effect", "").AsString() == "shield"))) result.Score += 260;
        if (profile == "support" && (result.Healing > 0 || result.AppliedStatus != "")) result.Score += 220;
        if (profile == "control" && result.AppliedStatus != "" && result.StatusTarget is not null && result.StatusTarget.Get("faction").AsInt32() != unit.Get("faction").AsInt32()) result.Score += 250;

        // 同一牌可重复使用，但逐次降低倾向，避免 AI 把整段机械地灌给一招。
        result.Score -= repeatCount * 180f;
        result.Score -= energyCost * 24f + manaCost * 30f;
        if (teammate && GodotObject.IsInstanceValid(target) && target!.Get("faction").AsInt32() != unit.Get("faction").AsInt32())
            result.Score += 0; // 队友对敌人的偏好来自共享血量预测，不读取敌方隐藏队列。
        return result;
    }

    private IEnumerable<GodotObject?> AiTargets(GodotObject unit, ActiveSkillData skill)
    {
        if (skill.target_type == 0) { yield return unit; yield break; }
        var candidates = all_units.Where(Alive).Where(target =>
        {
            var sameFaction = target.Get("faction").AsInt32() == unit.Get("faction").AsInt32();
            return skill.target_type switch
            {
                1 => sameFaction,
                2 => !sameFaction,
                3 => true,
                _ => false
            };
        }).OrderBy(target => target == unit ? 0 : 1).ToList();
        if (candidates.Count == 0 && skill.target_type == 1) candidates.Add(unit);
        if (candidates.Count == 0) yield return null;
        else foreach (var candidate in candidates) yield return candidate;
    }

    private int EstimateAiDamage(GodotObject unit, ActiveSkillData skill, GodotObject? target, Dictionary<GodotObject, float> forecast)
    {
        if (Mathf.IsZeroApprox(skill.base_damage) && skill.damage_bonuses.Count == 0 && string.IsNullOrWhiteSpace(skill.damage_formula)) return 0;
        if (skill.aoe_range > 0)
        {
            var total = 0;
            foreach (var victim in all_units.Where(Alive).Where(other => other.Get("faction").AsInt32() != unit.Get("faction").AsInt32()))
                if (forecast.GetValueOrDefault(victim, victim.Get("current_health").AsSingle()) > 0)
                    total += Mathf.RoundToInt(EnemyExpectedDamage(skill, unit, victim) * BattleActionResolver.preview_hit_chance(unit, victim));
            return total;
        }
        if (!GodotObject.IsInstanceValid(target) || forecast.GetValueOrDefault(target!, target!.Get("current_health").AsSingle()) <= 0) return 0;
        return EnemyExpectedDamage(skill, unit, target!);
    }

    private void ApplyAiCandidateForecast(AiCandidate candidate, Dictionary<GodotObject, float> forecast, GodotObject actor)
    {
        if (candidate.Damage > 0 && candidate.Skill.aoe_range > 0)
        {
            foreach (var victim in all_units.Where(Alive).Where(other => other.Get("faction").AsInt32() != actor.Get("faction").AsInt32()))
            {
                var health = forecast.GetValueOrDefault(victim, victim.Get("current_health").AsSingle());
                if (health <= 0) continue;
                var hit = BattleActionResolver.preview_hit_chance(actor, victim);
                forecast[victim] = Math.Max(0, health - EnemyExpectedDamage(candidate.Skill, actor, victim) * hit);
            }
        }
        else if (candidate.Damage > 0 && GodotObject.IsInstanceValid(candidate.Target))
        {
            var target = candidate.Target!;
            var health = forecast.GetValueOrDefault(target, target.Get("current_health").AsSingle());
            var hit = BattleActionResolver.preview_hit_chance(actor, target);
            forecast[target] = Math.Max(0, health - candidate.Damage * hit);
        }
        if (candidate.Healing > 0)
        {
            var current = forecast.GetValueOrDefault(actor, actor.Get("current_health").AsSingle());
            forecast[actor] = Math.Min(actor.Get("max_health").AsSingle(), current + candidate.Healing);
        }
    }

    private void ApplyQueueForecast(GodotObject unit, Dictionary<GodotObject, float> forecast)
    {
        if (!battle_units.ContainsKey(unit)) return;
        foreach (var action in get_battle_unit(unit).skill_queue)
        {
            if (action.kind != BattleQueuedSkill.ActionKind.Skill) continue;
            var skill = action.skill;
            var target = GodotObject.IsInstanceValid(action.target) ? action.target : DefaultTarget(skill, unit);
            var damage = EstimateAiDamage(unit, skill, target, forecast);
            if (damage <= 0) continue;
            if (skill.aoe_range > 0)
            {
                foreach (var victim in all_units.Where(Alive).Where(other => other.Get("faction").AsInt32() != unit.Get("faction").AsInt32()))
                {
                    var current = forecast.GetValueOrDefault(victim, victim.Get("current_health").AsSingle());
                    if (current > 0) forecast[victim] = Math.Max(0, current - EnemyExpectedDamage(skill, unit, victim) * BattleActionResolver.preview_hit_chance(unit, victim));
                }
            }
            else if (GodotObject.IsInstanceValid(target))
            {
                var current = forecast.GetValueOrDefault(target!, target!.Get("current_health").AsSingle());
                forecast[target!] = Math.Max(0, current - damage * BattleActionResolver.preview_hit_chance(unit, target!));
            }
        }
    }

    private static bool IsOncePerSegmentBodySkill(ActiveSkillData skill) => skill.skill_name is "抱一" or "伏火" or "归藏";
    private static string StatusForecastKey(GodotObject unit, string id) => $"{unit.GetInstanceId()}:{id}";
    private static string GetAiProfile(GodotObject unit)
    {
        var profile = unit.HasMeta("battle_ai_profile") ? unit.GetMeta("battle_ai_profile").AsString() : "balanced";
        return profile is "aggressive" or "guard" or "support" or "control" ? profile : "balanced";
    }
}
