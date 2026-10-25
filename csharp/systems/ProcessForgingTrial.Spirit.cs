public sealed partial class ProcessForgingTrial
{
    public sealed record SpiritLoad(Input Input, double BaseLoad, double FunctionLoad, double RoleMultiplier, double DoseMultiplier, double Total);
    public IReadOnlyList<SpiritLoad> SpiritLoads(Draft draft)
    {
        double[] costs = [0, .75, 1, 1.4, 2, 3, 4.5, 7, 11, 17];
        return Normalize(draft.Inputs).Where(input => _facts.ContainsKey(input.Material)).Select(input =>
        {
            var m = _facts[input.Material];
            var grades = m.Utilities.Values.OrderByDescending(value => value).ToArray();
            var v = grades.Length == 0 ? 0 : Math.Clamp(Math.Ceiling(grades[0] * 2 + (grades.Length > 1 ? grades[1] * .5 : 0) + grades.Skip(2).Sum() * .25), 1, 27);
            double b = 1 + .13 * v + .0065 * v * v;
            double f = grades.Sum(value => costs[Math.Clamp(value, 0, 9)]);
            double role = Group(input.Role) switch { "质材" => 1.4, "和材" => .9, _ => 1.2 };
            double q = Math.Max(0, input.Quantity - 1), dose = 1 + .4 * q + .04 * q * q;
            return new SpiritLoad(Clone(input), b, f, role, dose, (b + f) * role * dose);
        }).ToArray();
    }
    public int SpiritDemand(Draft draft) => (int)Math.Min(int.MaxValue, Math.Ceiling(Math.Round(SpiritLoads(draft).Sum(load => load.Total), 8)));
}
