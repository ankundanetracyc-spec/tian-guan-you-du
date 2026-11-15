using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class Character
{
    public const float ArtisanStudyEnergyPerPoint = 8f;
    public const double ArtisanStudyShichenPerPoint = 1d;
    public const double AlchemyEntryThreshold = 8d;
    public const float ForgingEntryGrowth = 3f;
    public const float AlchemyPracticeGrowth = 2f;
    public const float ForgingPracticeGrowth = 2f;
    private const float ForgingProficiencyCap = 100f;
    private const double AlchemyProficiencyCap = 1196d;
    private const string AlchemyEntryLessonId = "alchemy:introductory_formula:v1";
    private const string ForgingEntryLessonId = "forging:introductory_principles:v1";
    private bool _artisanGrowthCommitting;
    public bool HasCompletedAlchemyEntry => human?.alchemy_proficiency_growth_sources.Contains(AlchemyEntryLessonId) ?? false;
    public bool HasCompletedForgingEntry => human?.forging_proficiency_growth_sources.Contains(ForgingEntryLessonId) ?? false;

    private void AddArtisanTrainingBonus(string proficiency, float amount)
    {
        if (human is null || Mathf.IsZeroApprox(amount)) return;
        if (proficiency == "alchemy_proficiency") human.alchemy_proficiency_training_bonus += amount;
        else if (proficiency == "forging_proficiency") human.forging_proficiency_training_bonus += amount;
    }

    public bool TryStudyAlchemyEntry(out float growth, out float energyCost, out double shichen,
        out string reason)
    {
        growth = 0; energyCost = 0; shichen = 0; reason = "";
        if (human is null || current_health <= 0 || HasMeta("artifact_battle_clone") ||
            _artisanGrowthCommitting || _alchemyCommitting || _forgingCommitting)
        { reason = "人物当前不能研习。"; return false; }
        if (HasCompletedAlchemyEntry)
        { reason = "已经完成丹诀入门研习。"; return false; }
        double current = AlchemyAttainment;
        if (!double.IsFinite(current) || current >= AlchemyEntryThreshold)
        { reason = "当前丹道造诣已达到九品丹门槛。"; return false; }

        double needed = AlchemyEntryThreshold - current;
        double cost = needed * ArtisanStudyEnergyPerPoint;
        double duration = needed * ArtisanStudyShichenPerPoint;
        if (!double.IsFinite(cost) || !double.IsFinite(duration) || cost > float.MaxValue)
        { reason = "丹道造诣数据无效。"; return false; }
        if (!can_spend_world_stamina((float)cost))
        { reason = $"本次需精力{cost:0.#}，当前精力不足。"; return false; }

        _artisanGrowthCommitting = true;
        try
        {
            if (!can_spend_world_stamina((float)cost))
            { reason = "当前精力不足。"; return false; }
            human.current_world_stamina = Math.Max(0, human.current_world_stamina - (float)cost);
            human.alchemy_proficiency_growth_sources.Add(AlchemyEntryLessonId);
            AddArtisanTrainingBonus("alchemy_proficiency", (float)needed);
            refresh_gongfa_effects();
            human.alchemy_records.Add(crafting_record_date() +
                $"研习丹诀入门 · 丹道造诣+{needed:0.#} · {duration:0.#}时辰 · 精力{cost:0.#}");
            growth = (float)needed; energyCost = (float)cost; shichen = duration;
            CompleteArtisanStudy(duration, "alchemy_study");
            return true;
        }
        finally { _artisanGrowthCommitting = false; }
    }

    public bool TryStudyForgingEntry(out float growth, out float energyCost, out double shichen,
        out string reason)
    {
        growth = 0; energyCost = 0; shichen = 0; reason = "";
        if (human is null || current_health <= 0 || HasMeta("artifact_battle_clone") ||
            _artisanGrowthCommitting || _alchemyCommitting || _forgingCommitting)
        { reason = "人物当前不能研习。"; return false; }
        if (HasCompletedForgingEntry)
        { reason = "已经完成器理入门研习。"; return false; }
        double current = human.forging_proficiency;
        if (!double.IsFinite(current) || current >= ForgingProficiencyCap)
        { reason = "器道造诣已达当前炼器上限。"; return false; }

        float amount = (float)Math.Min(ForgingEntryGrowth, ForgingProficiencyCap - current);
        double cost = amount * ArtisanStudyEnergyPerPoint;
        double duration = amount * ArtisanStudyShichenPerPoint;
        if (!can_spend_world_stamina((float)cost))
        { reason = $"本次需精力{cost:0.#}，当前精力不足。"; return false; }

        _artisanGrowthCommitting = true;
        try
        {
            if (!can_spend_world_stamina((float)cost))
            { reason = "当前精力不足。"; return false; }
            human.current_world_stamina = Math.Max(0, human.current_world_stamina - (float)cost);
            human.forging_proficiency_growth_sources.Add(ForgingEntryLessonId);
            AddArtisanTrainingBonus("forging_proficiency", amount);
            refresh_gongfa_effects();
            human.forging_records.Add(crafting_record_date() +
                $"研习器理入门 · 器道造诣+{amount:0.#} · {duration:0.#}时辰 · 精力{cost:0.#}");
            growth = amount; energyCost = (float)cost; shichen = duration;
            CompleteArtisanStudy(duration, "forging_study");
            return true;
        }
        finally { _artisanGrowthCommitting = false; }
    }

    internal float RecordAlchemyPracticeGrowth(string productId, int efficacyTier)
    {
        if (human is null || string.IsNullOrWhiteSpace(productId) || efficacyTier is < 0 or > 4) return 0;
        int previous = human.alchemy_growth_highest_tier_by_pill.GetValueOrDefault(productId, -1);
        if (efficacyTier <= previous) return 0;
        // 一炉即使跳过多个档位，也只记当前最高丹效档的一次成长。
        human.alchemy_growth_highest_tier_by_pill[productId] = efficacyTier;
        double room = Math.Max(0, AlchemyProficiencyCap - human.alchemy_proficiency);
        float amount = (float)Math.Min(AlchemyPracticeGrowth, room);
        if (amount <= 0) return 0;
        AddArtisanTrainingBonus("alchemy_proficiency", amount);
        refresh_gongfa_effects();
        return amount;
    }

    internal float RecordForgingPracticeGrowth(ProcessForgingTrial.Artifact product)
    {
        if (human is null || product is null) return 0;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var source in product.Sources)
        {
            if (source.Retained <= 1e-8 || string.IsNullOrWhiteSpace(source.Material.DefinitionId)) continue;
            ids.Add($"material-role:{source.Material.DefinitionId}:{source.Input.Role}");
        }
        ids.Add("practice:first-success:v1");
        if (product.Tier == "法器") ids.Add("practice:first-artifact:v1");
        var fresh = ids.Where(id => !human.forging_proficiency_growth_sources.Contains(id)).ToArray();
        if (fresh.Length == 0) return 0;

        // 同一成器可以证明多项材料知识，但单次事务最多增加2点；所有实际用到的新来源同时记账，防止重复投料刷取。
        foreach (var id in fresh) human.forging_proficiency_growth_sources.Add(id);
        float amount = (float)Math.Min(ForgingPracticeGrowth,
            Math.Max(0, ForgingProficiencyCap - human.forging_proficiency));
        if (amount <= 0) return 0;
        AddArtisanTrainingBonus("forging_proficiency", amount);
        refresh_gongfa_effects();
        return amount;
    }

    private void CompleteArtisanStudy(double shichen, string activity)
    {
        WorldStaminaChanged?.Invoke(this);
        if (!IsInsideTree()) return;
        GetTree().CallGroup("main_scene", "notify_world_stamina_changed", this);
        GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.advance_time((float)(2 * shichen), activity);
    }
}
