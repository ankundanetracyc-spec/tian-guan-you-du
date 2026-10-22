using Godot;

public partial class BattleManager
{
    // 有限状态目录的定义保持强引用；搜索草案不能反复加载同一 C# Resource。
    private static readonly Dictionary<string,StatusEffect?> AiStatusDefinitions = [];
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
            EnsureSegmentSlots(enemy);
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
            EnsureSegmentSlots(ally);
        }
    }

    private Dictionary<GodotObject, float> CreateHealthForecast() => get_target_candidates()
        .Where(Alive)
        .ToDictionary(unit => unit, unit => (float)unit.Get("current_health").AsInt32());

    private void BuildAiQueue(GodotObject unit, Dictionary<GodotObject, float> forecast, bool teammate)
    {
        BuildConstructedAiQueue(unit, forecast, teammate);
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
            var canFinish = target is not ArtifactBody && targetHealth > 0 && damage >= targetHealth && hit >= .78f;
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

        foreach (var effect in skill.status_effects.Where(_ => target is not ArtifactBody))
        {
            var id = effect.GetValueOrDefault("status_id", "").AsString();
            var owner = selfTarget ? unit : target;
            if (string.IsNullOrWhiteSpace(id) || !GodotObject.IsInstanceValid(owner) || HasStatus(owner!, id) ||
                plannedStatuses.Contains(StatusForecastKey(owner!, id))) continue;
            if(!AiStatusDefinitions.TryGetValue(id,out var status))
                AiStatusDefinitions[id]=status=ResourceLoader.Load<StatusEffect>($"res://resources/battle/statuses/{id}.tres");
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
        if (CardRules.IsResponse(skill))
        {
            // 应对状态只覆盖匹配来袭，不当作已经获得的常驻防护或确定的回复／伤害。
            result.Damage = 0; result.Healing = 0; result.AppliedStatus = ""; result.StatusTarget = null;
            if (CardRules.NormalCast(skill) is { } normal)
            {
                var ordinary = ScoreAiSkill(unit, normal, target, forecast, energyCost, manaCost, repeatCount, plannedStatuses, teammate);
                if (ordinary.Priority > result.Priority || ordinary.Priority == result.Priority && ordinary.Score > result.Score)
                {
                    ordinary.Skill = skill;
                    return ordinary;
                }
            }
        }
        return result;
    }

    private IEnumerable<GodotObject?> AiTargets(GodotObject unit, ActiveSkillData skill)
    {
        skill = CardRules.NormalCast(skill) ?? skill;
        if (skill.target_type == 0) { yield return unit; yield break; }
        var candidates = get_target_candidates().Where(Alive).Where(target =>
        {
            if (target is ArtifactBody artifactBody) return ArtifactAcceptsSkill(artifactBody, unit, skill);
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
            foreach (var victim in get_target_candidates().Where(Alive).Where(other => other.Get("faction").AsInt32() != unit.Get("faction").AsInt32()))
                if (forecast.GetValueOrDefault(victim, victim.Get("current_health").AsSingle()) > 0)
                    total += Mathf.RoundToInt(EnemyExpectedDamage(skill, unit, victim) * BattleActionResolver.preview_hit_chance(unit, victim));
            return total;
        }
        if (!GodotObject.IsInstanceValid(target) || forecast.GetValueOrDefault(target!, target!.Get("current_health").AsSingle()) <= 0) return 0;
        return EnemyExpectedDamage(skill, unit, target!);
    }

    private void ApplyQueueForecast(GodotObject unit, Dictionary<GodotObject, float> forecast)
    {
        if (!battle_units.ContainsKey(unit)) return;
        foreach (var action in get_battle_unit(unit).skill_queue)
        {
            if (action.kind != BattleQueuedSkill.ActionKind.Skill) continue;
            var skill = CardRules.NormalCast(action.execution_skill);
            if (skill is null) continue;
            var target = GodotObject.IsInstanceValid(action.target) ? action.target : DefaultTarget(skill, unit);
            var damage = EstimateAiDamage(unit, skill, target, forecast);
            if (damage <= 0) continue;
            if (skill.aoe_range > 0)
            {
                foreach (var victim in get_target_candidates().Where(Alive).Where(other => other.Get("faction").AsInt32() != unit.Get("faction").AsInt32()))
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

    private static string StatusForecastKey(GodotObject unit, string id) => $"{unit.GetInstanceId()}:{id}";
    private static string GetAiProfile(GodotObject unit)
    {
        var profile = unit.HasMeta("battle_ai_profile") ? unit.GetMeta("battle_ai_profile").AsString() : "balanced";
        return profile is "aggressive" or "guard" or "support" or "control" ? profile : "balanced";
    }
}
