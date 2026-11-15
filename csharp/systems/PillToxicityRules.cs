using System;
using System.Collections.Generic;
using Godot;

/// <summary>丹种各自的积毒基值；同丹种同档固定，不制造额外物品实例。</summary>
public static class PillToxicityRules
{
    public const double NaturalDecayPerHour = 1d / 6d;
    public const double RestBonusPerHour = 1d / 3d;
    public static readonly IReadOnlyDictionary<string, double> BaseLoads = new Dictionary<string, double>
    {
        ["huichun"] = 8, ["tongmai"] = 10, ["jingxin"] = 4,
        ["ninggang"] = 9, ["jinji"] = 7, ["qingshen"] = 6, ["xusheng"] = 7.5,
        ["huiqi"] = 14, ["jiegu"] = 6.5, ["hezhong"] = 5, ["chisui"] = 11,
        ["peiyuan"] = 16, ["yinqi"] = 9.5, ["xudeng"] = 12.5, ["baopu"] = 10.5,
        ["lifeng"] = 13, ["cangfeng"] = 15, ["ningnian"] = 13.5, ["naling"] = 17,
        ["peiling"] = 11.5, ["yisi"] = 5.5, ["fanyuan"] = 8.5, ["yangshang"] = 14.5,
        ["dingmai"] = 12, ["guiyuan_dingxi"] = 20
    };
    private static readonly double[] TierFactors = [1.5, 1.2, .9, .6, .3];
    public static double Normalize(double value) => double.IsFinite(value) ? Math.Max(0, value) : 0;
    public static int Load(string pillId, int tier) => BaseLoads.TryGetValue(pillId, out var basis)
        ? (int)Math.Ceiling(basis * TierFactors[Math.Clamp(tier, 0, 4)]) : 0;
    public static double Efficiency(double toxicity) => Math.Max(.5, 1 - .005 * Math.Max(0, Normalize(toxicity) - 20));
    public static double Decayed(double toxicity, double hours) => Math.Max(0, Normalize(toxicity) - Math.Max(0, hours) * NaturalDecayPerHour);
    public static int Absorb(int nominal, double toxicity) => nominal <= 0 ? 0 : Math.Max(1, (int)Math.Floor(nominal * Efficiency(toxicity) + 1e-9));

    /// <summary>积分计算自然消退过程中的有效修炼小时，跨120/20断点分段，避免长闭关按终值重算。</summary>
    public static double EffectiveHours(double toxicity, double elapsed, double hours)
    {
        if (hours <= 0) return 0;
        toxicity = Normalize(toxicity);
        var start = Math.Max(0, elapsed);
        var end = start + hours;
        var points = new List<double> { start, end };
        foreach (var threshold in new[] { 120d, 20d })
        {
            var crossing = (toxicity - threshold) / NaturalDecayPerHour;
            if (crossing > start && crossing < end) points.Add(crossing);
        }
        points.Sort();
        double sum = 0;
        for (int i = 1; i < points.Count; i++)
            sum += (points[i] - points[i - 1]) *
                (Efficiency(Decayed(toxicity, points[i - 1])) + Efficiency(Decayed(toxicity, points[i]))) * .5;
        return sum;
    }

    public static string Description(double toxicity) =>
        $"服丹吸收与吐纳效率：{Efficiency(toxicity):0.#%}。正常每日排解4点，静养每日共排解12点。";

    /// <summary>为既有状态展示生成临时视图，不把派生说明反复写入持久状态数组。</summary>
    public static Godot.Collections.Array<WorldStatusRecord> WithStatus(HumanData? data, double? now = null)
    {
        var records = new Godot.Collections.Array<WorldStatusRecord>();
        if (data is null) return records;
        foreach (var record in data.world_status_records) records.Add(record);
        var toxicity = now.HasValue && double.IsFinite(data.pill_toxicity_last_game_hour) && data.pill_toxicity_last_game_hour >= 0
            ? Decayed(data.pill_toxicity, Math.Max(0, now.Value - data.pill_toxicity_last_game_hour))
            : Normalize(data.pill_toxicity);
        if (toxicity > 1e-8)
            records.Add(new WorldStatusRecord
            {
                status_id = "pill_toxicity", display_name = $"丹毒 {toxicity:0.#}",
                description = Description(toxicity), expires_at_total_game_hours = WorldStatusRules.PermanentExpiry,
                source_name = "服丹积累", stack_count = 1
            });
        return records;
    }
}
