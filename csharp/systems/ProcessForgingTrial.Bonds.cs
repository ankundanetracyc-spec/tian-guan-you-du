public sealed partial class ProcessForgingTrial
{
    private void BuildEffectsAndBonds(Artifact a, Dictionary<string, double> budgets)
    {
        var p = _parameters.BondParameters;
        if (a.Tier != "法器" || p.Count == 0) { BuildEffects(a, budgets); return; }
        double sourceCost = p.GetValueOrDefault("SourcePerPoint", 1), rootCost = p.GetValueOrDefault("RootPerPoint", .25), connectionCost = p.GetValueOrDefault("ConnectionPerPoint", .25);
        double share = p.GetValueOrDefault("SourceShare", .2);
        if (!Positive(sourceCost) || !Positive(rootCost) || !Positive(connectionCost) || !Positive(share) || share > 1)
            throw new InvalidOperationException("器蕴预算参数非法");
        var remaining = new Dictionary<string, double>(budgets);
        var reserved = new Dictionary<string, double>();
        foreach (var (name, source) in new[] { ("惊鸿", "source/破"), ("抱朴", "source/固") })
        {
            int points = (int)Math.Floor(Math.Min(3, Math.Min(remaining.GetValueOrDefault(source) * share / sourceCost,
                Math.Min(remaining.GetValueOrDefault("root") / rootCost, remaining.GetValueOrDefault("通") / connectionCost))));
            if (points < 1) continue;
            foreach (var (key, value) in new[] { (source, points * sourceCost), ("root", points * rootCost), ("通", points * connectionCost) })
            { remaining[key] -= value; reserved[key] = reserved.GetValueOrDefault(key) + value; }
            a.BondContributions.Add(name + " +" + points);
            a.Calculation.Ledger.Add($"器蕴{name}贡献{points}：支出{source}={points * sourceCost:0.##}、root={points * rootCost:0.##}、通={points * connectionCost:0.##}；不重复用于词条。");
        }
        BuildEffects(a, remaining);
        foreach (var entry in reserved) a.Calculation.Used[entry.Key] = a.Calculation.Used.GetValueOrDefault(entry.Key) + entry.Value;
        foreach (var entry in budgets) a.Calculation.Unused[entry.Key] = Math.Max(0, entry.Value - a.Calculation.Used.GetValueOrDefault(entry.Key));
    }
}
