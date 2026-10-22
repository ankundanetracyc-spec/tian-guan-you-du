using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>最大化Σw ln(1+x)，共同来源/结构/工艺约束。与材料名称和配方无关。</summary>
public static class ForgingBudgetSolver
{
    public sealed record Demand(string Id, double Weight, double Minimum, Dictionary<string, double> Costs);
    public static Dictionary<string, double> Solve(IReadOnlyList<Demand> demands, IReadOnlyDictionary<string, double> budget)
    {
        if (budget.Values.Any(x => !double.IsFinite(x) || x < 0) || demands.Any(x => !double.IsFinite(x.Weight) || x.Weight <= 0 || x.Minimum < 0 || !double.IsFinite(x.Minimum) || x.Costs.Count == 0 || x.Costs.Values.Any(v => !double.IsFinite(v) || v <= 0)) || demands.Select(x => x.Id).Distinct().Count() != demands.Count) throw new ArgumentException("预算/需求非法");
        var active = demands.Where(d => d.Costs.All(c => budget.TryGetValue(c.Key, out double b) && b / c.Value >= d.Minimum)).OrderBy(x => x.Id, StringComparer.Ordinal).ToList();
        while (active.Count > 0)
        {
            var answer = Continuous(active, budget);
            var remove = active.Where((x, i) => answer[i] + 1e-8 < x.Minimum).OrderBy(x => answer[active.IndexOf(x)] / Math.Max(x.Minimum, 1e-12)).ThenBy(x => x.Id, StringComparer.Ordinal).FirstOrDefault();
            if (remove is null) return active.Select((d, i) => (d.Id, Value: answer[i])).ToDictionary(x => x.Id, x => x.Value, StringComparer.Ordinal);
            active.Remove(remove);
        }
        return [];
    }
    private static double[] Continuous(List<Demand> demands, IReadOnlyDictionary<string, double> budget)
    {
        string[] keys = demands.SelectMany(x => x.Costs.Keys).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray();
        double[,] costs = new double[keys.Length, demands.Count];
        for (int k = 0; k < keys.Length; k++) for (int j = 0; j < demands.Count; j++) costs[k, j] = demands[j].Costs.GetValueOrDefault(keys[k]);
        double[] prices = Enumerable.Repeat(1.0, keys.Length).ToArray(), allocation = new double[demands.Count], previous = new double[demands.Count];
        double Value(int j)
        {
            double sum = 0; for (int k = 0; k < keys.Length; k++) sum += prices[k] * costs[k, j];
            return sum <= 1e-15 ? 1e18 : Math.Max(0, demands[j].Weight / sum - 1);
        }
        double Usage(int k)
        {
            double sum = 0; for (int j = 0; j < demands.Count; j++) if (costs[k, j] > 0) sum += costs[k, j] * Value(j); return sum;
        }
        for (int iteration = 0; iteration < 1200; iteration++)
        {
            for (int k = 0; k < keys.Length; k++)
            {
                prices[k] = 0; if (Usage(k) <= budget[keys[k]]) continue;
                double low = 0, high = 1; prices[k] = high;
                while (Usage(k) > budget[keys[k]]) { high *= 2; prices[k] = high; if (high > 1e20) break; }
                for (int n = 0; n < 44; n++) { double middle = (low + high) / 2; prices[k] = middle; if (Usage(k) > budget[keys[k]]) low = middle; else high = middle; }
                prices[k] = high;
            }
            double change = 0, violation = 0;
            for (int j = 0; j < demands.Count; j++) { allocation[j] = Value(j); change = Math.Max(change, Math.Abs(allocation[j] - previous[j])); previous[j] = allocation[j]; }
            for (int k = 0; k < keys.Length; k++) violation = Math.Max(violation, Usage(k) - budget[keys[k]]);
            if (change < 1e-9 && violation < 1e-9) break;
        }
        // 浮点修正只收缩使用超额资源的变量，不拖累无导通需求的基础属性。
        for (int k = 0; k < keys.Length; k++)
        {
            double used = 0; for (int j = 0; j < demands.Count; j++) used += costs[k, j] * allocation[j];
            if (used <= budget[keys[k]]) continue;
            double factor = budget[keys[k]] / used;
            for (int j = 0; j < demands.Count; j++) if (costs[k, j] > 0) allocation[j] *= factor;
        }
        return allocation;
    }
}
