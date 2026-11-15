using System;
using System.Collections.Generic;
using System.Linq;

public static partial class AlchemySlotSimulation
{
    public sealed record PillResult(int Index, int Quality, string QualityName, double MainMass,
        double Core, double Polarity, double Connection, double Stability, double Purity,
        double YinValue, double Contamination, IReadOnlyDictionary<string, double> SourceMasses, double CarryMass = 0);

    private static IReadOnlyList<PillResult> Distribute(AlchemyRecipeCatalog.PillType pill,
        IReadOnlyList<Input> inputs, Metrics metrics, double attainment)
    {
        int count = metrics.CandidateCount;
        if (count == 0) return [];
        var main = inputs.Single(input => input.IsMain);
        var masses = Enumerable.Range(0, count).Select(_ => new Dictionary<string, double>(StringComparer.Ordinal)).ToArray();
        double spread = .12 + .10 * (1 - metrics.ConnectionRate);
        int sourceIndex = 0;
        foreach (var input in inputs.OrderBy(input => input.Path, StringComparer.Ordinal))
        {
            var weights = Enumerable.Range(0, count).Select(j => count == 1 ? 1d :
                1 + spread * (count - 1 - 2d * j) / (count - 1) +
                .025 * (count - 1 - 2d * ((j + sourceIndex) % count)) / (count - 1)).ToArray();
            double total = metrics.SourceDelivered.GetValueOrDefault(input.Path) * count;
            double used = 0, sum = weights.Sum();
            for (int j = 0; j < count; j++)
            {
                double amount = j == count - 1 ? total - used : total * weights[j] / sum;
                masses[j][input.Path] = amount;
                used += amount;
            }
            sourceIndex++;
        }
        // 引搬运真实的主药药性，不再给全炉核心凭空乘倍率；一颗时无可转移来源。
        double leadFraction = Math.Min(.25, .05 * Power(metrics.UtilityPower, "引"));
        for (int j = 1; j < count; j++)
        {
            double moved = masses[j][main.Path] * leadFraction;
            masses[j][main.Path] -= moved;
            masses[0][main.Path] += moved;
        }
        double pillPower = Strength(Math.Clamp(10 - pill.Rank, 1, 9));
        int mainGrade = Grade(main.Craft, pill.CoreUtility);
        // 各丹均分残余质量，不能均分比例后再乘各自药性量，避免复制/丢失残余。
        double totalCarry = metrics.CarryMass;
        var results = new List<PillResult>();
        for (int j = 0; j < count; j++)
        {
            var allocation = masses[j];
            double total = allocation.Values.Sum();
            var vector = Elements.ToDictionary(e => e, _ => 0d, StringComparer.Ordinal);
            foreach (var input in inputs)
                if (vector.ContainsKey(input.Craft.element.Trim())) vector[input.Craft.element.Trim()] += allocation[input.Path];
            if (total > 0) foreach (var element in Elements) vector[element] /= total;
            double elementRate = FiveElementRate(pill, vector);
            double yin = total <= 0 ? 0 : inputs.Sum(input => Math.Sign(input.Craft.polarity) * allocation[input.Path]) / total;
            double polarity = AlchemyQualityRules.Polarity(Math.Abs(yin - pill.PolarityTarget), pill.PolarityToleranceAt20, pill.PolarityToleranceAt100);
            double mainMass = allocation[main.Path];
            double core = CoreQuality(mainMass, mainMass * Strength(mainGrade) * Mastery(mainGrade, attainment) * elementRate, pillPower);
            double structure = inputs.Sum(input =>
            {
                int grade = CanFunctionAct(input.Role, "固") ? Grade(input.Craft, "固") : 0;
                return grade == 0 ? 0 : allocation[input.Path] * Strength(grade) * Mastery(grade, attainment) * elementRate;
            });
            structure += inputs.Sum(input => allocation[input.Path] * TextureBinding(input.Craft.texture));
            double solid = Math.Max(45, AlchemyQualityRules.RawForDemand(structure / (8 * pillPower)));
            double carry = totalCarry / count;
            double contamination = total + carry <= 0 ? 1 : carry / (total + carry);
            double purity = AlchemyQualityRules.Purity(contamination);
            double stability = Math.Min(solid, purity);
            double localConnectionPower = inputs.Sum(input =>
            {
                int grade = CanFunctionAct(input.Role, "通") ? Grade(input.Craft, "通") : 0;
                double averageMass = metrics.SourceDelivered.GetValueOrDefault(input.Path);
                return grade == 0 || averageMass <= 0 ? 0 : input.Dose * Strength(grade) / pillPower *
                    Mastery(grade, attainment) * allocation[input.Path] / averageMass;
            });
            double connection = inputs.Count < 2 ? 100 : 65 + 35 * Math.Min(1, localConnectionPower / 6);
            int quality = (int)Math.Floor(AlchemyQualityRules.FromRaw(Math.Min(Math.Min(core, polarity), Math.Min(connection, stability))) + 1e-8);
            results.Add(new(j + 1, quality, AlchemyQualityRules.Name(quality), mainMass, core, polarity,
                connection, stability, purity, yin, contamination, allocation, carry));
        }
        return results;
    }

    private static double TextureBinding(string texture) => texture switch
    {
        "纤维" => .10, "致密" => .04, "颗粒" => .06, "黏胶" => .22,
        "液体" => .04, "气体" => .01, _ => .04
    };

    private static double FiveElementRate(AlchemyRecipeCatalog.PillType pill, IReadOnlyDictionary<string, double> vector)
    {
        var target = ElementTarget(pill.Id);
        double distance = .5 * Elements.Sum(e => Math.Abs(vector.GetValueOrDefault(e) - target.GetValueOrDefault(e)));
        string anchor = AlchemyRecipeCatalog.Anchor(pill.Id);
        return Math.Clamp(1 + .25 * (.50 - distance) + .125 *
            (vector.GetValueOrDefault(ElementThatGenerates(anchor)) - vector.GetValueOrDefault(ElementThatOvercomes(anchor))), .75, 1.25);
    }
}
