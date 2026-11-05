using System;

public partial class Character
{
    public double PillToxicity
    {
        get { SettlePillToxicity(); return PillToxicityRules.Normalize(human?.pill_toxicity ?? 0); }
    }
    public double PillAbsorption => PillToxicityRules.Efficiency(PillToxicity);

    public bool SettlePillToxicity()
    {
        if (human is null || !IsInsideTree()) return false;
        var time = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
        if (time is null) return false;
        var now = time.get_precise_game_hours();
        var old = PillToxicityRules.Normalize(human.pill_toxicity);
        var elapsed = !double.IsFinite(human.pill_toxicity_last_game_hour) || human.pill_toxicity_last_game_hour < 0
            ? 0 : Math.Max(0, now - human.pill_toxicity_last_game_hour);
        human.pill_toxicity = PillToxicityRules.Decayed(old, elapsed);
        human.pill_toxicity_last_game_hour = now;
        return Math.Abs(old - human.pill_toxicity) > 1e-9;
    }

    public void AddPillToxicity(string pillId, int tier = -1)
    {
        if (human is null) return;
        SettlePillToxicity();
        // 旧无档突破丹按丹种基值，不伪装成某个丹效档。
        var load = tier < 0 && PillToxicityRules.BaseLoads.TryGetValue(pillId, out var basis)
            ? basis : PillToxicityRules.Load(pillId, tier);
        human.pill_toxicity = PillToxicityRules.Normalize(human.pill_toxicity) + load;
        if (IsInsideTree()) GetTree().CallGroup("main_scene", "notify_pill_toxicity_changed", this);
    }

    public bool RestPillToxicity(double hours)
    {
        if (human is null || hours <= 0) return false;
        SettlePillToxicity();
        var old = PillToxicityRules.Normalize(human.pill_toxicity);
        // 正常排解由历法统一结算；这里只加静养奖励，避免同一段时间算两遍。
        human.pill_toxicity = Math.Max(0, old - hours * PillToxicityRules.RestBonusPerHour);
        return human.pill_toxicity != old;
    }

    public int AlchemyPillAmount(string pillId, int tier)
    {
        tier = Math.Clamp(tier, 0, 4);
        var toxicity = PillToxicity;
        return pillId switch
        {
            "huichun" => PillToxicityRules.Absorb(AlchemyProductCatalog.Healing[tier], toxicity),
            "tongmai" => (long)max_mana * AlchemyProductCatalog.ManaPercent[tier] / 100 <= 0 ? 0 :
                Math.Max(1, (int)Math.Floor((double)max_mana * AlchemyProductCatalog.ManaPercent[tier] / 100 * PillToxicityRules.Efficiency(toxicity) + 1e-9)),
            "jingxin" => PillToxicityRules.Absorb(AlchemyProductCatalog.Calm[tier], toxicity),
            _ => PillToxicityRules.Absorb(AlchemyProductCatalog.PrimaryAmount(pillId, tier), toxicity)
        };
    }
}
