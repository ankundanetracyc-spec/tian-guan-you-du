using System.Text.RegularExpressions;

/// <summary>器蕴贡献来自固定快照；聚合只看已装备实例，不以技能引用或器体分体计数。</summary>
public static class ArtifactBondRuntime
{
    public sealed record Totals(int Jinghong, int Baopu)
    {
        public float SwordDamageBonus => Ratio(Jinghong);
        public float DefenseBonus => Ratio(Baopu);
        public string Description => Describe("惊鸿", Jinghong, "带有“剑”关键词的技能物理伤害") + "\n\n" + Describe("抱朴", Baopu, "角色物理防御");
    }
    private static readonly Regex Contribution = new(@"^\s*(惊鸿|抱朴)\s*\+\s*([0-9]+)\s*$");
    public static float Ratio(int count) => count >= 4 ? .20f : count >= 2 ? .10f : 0;
    public static string Describe(string name, int count, string effect) =>
        $"{name} {count}/{(count < 2 ? 2 : 4)} · " + (count < 2 ? "未激活" : $"已激活：{effect}+{Ratio(count):P0}") +
        (count < 4 ? $"\n下一档还需{(count < 2 ? 2 : 4) - count}点，总加成为{(count < 2 ? 10 : 20)}%。" : "\n已达最高档。" );
    public static Totals Aggregate(IEnumerable<ArtifactInstanceData> equipped)
    {
        var total = new Dictionary<string, int> { ["惊鸿"] = 0, ["抱朴"] = 0 };
        foreach (var item in equipped.Where(item => item is not null).DistinctBy(item => item.instance_id))
        {
            if (!ForgedArtifactInventory.TryRead(item, out var s) || s.Tier != "法器") continue;
            var local = new Dictionary<string, int>();
            foreach (var text in s.BondContributions)
            {
                var match = Contribution.Match(text);
                if (!match.Success || !int.TryParse(match.Groups[2].Value, out var points)) continue;
                var name = match.Groups[1].Value;
                local[name] = (int)Math.Min(3L, (long)local.GetValueOrDefault(name) + points);
            }
            foreach (var pair in local) total[pair.Key] = (int)Math.Min(int.MaxValue, (long)total[pair.Key] + pair.Value);
        }
        return new(total["惊鸿"], total["抱朴"]);
    }
}
