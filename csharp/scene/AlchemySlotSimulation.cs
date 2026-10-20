using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>槽位式炼丹的确定性试演结算；只读当前材料，不写入行囊或角色资源。</summary>
public static class AlchemySlotSimulation
{
    public const double TrialAttainment = 36;
    public const int SpiritCapacity = 24;
    public const int EnergyCapacity = 100;

    private static readonly double[] MasteryThreshold = [0, 8, 32, 68, 122, 203, 325, 508, 783, 1196];
    private static readonly int[] RankBonus = [0, 1, 2, 4, 6, 8, 11, 14, 18];
    private static readonly string[] CommonUtilities = ["破", "化", "通", "行", "引", "聚", "固", "镇", "护", "明", "藏"];
    private static readonly string[] Elements = ["木", "火", "土", "金", "水"];
    private static readonly string[] ChenUtilities = ["聚", "固"];
    private static readonly string[] ZuoUtilities = ["破", "化", "固", "镇", "护", "明", "藏"];
    private static readonly string[] ShiUtilities = ["通", "行", "引", "藏"];

    public enum SlotRole { Jun, Chen, Zuo, Shi }

    public static int RoleCapacity(SlotRole role, double attainment) => role switch
    {
        SlotRole.Jun or SlotRole.Chen => 1,
        SlotRole.Zuo => attainment >= 508 ? 3 : attainment >= 68 ? 2 : 1,
        SlotRole.Shi => attainment >= 203 ? 3 : attainment >= 32 ? 2 : 1,
        _ => 0
    };

    /// <summary>以本炉最高材料大品阶定基础时长；九品一时辰，每升一品翻倍，超过四味每味另加25%。</summary>
    public static double DurationHours(IReadOnlyList<Input> inputs)
    {
        if (inputs.Count == 0) return 0;
        int highestGrade = inputs.Max(input => Math.Clamp((input.Craft.material_grade_value() + 2) / 3, 1, 9));
        int kinds = inputs.Select(input => input.Path).Distinct(StringComparer.Ordinal).Count();
        return Math.Pow(2, highestGrade - 1) * (1 + .25 * Math.Max(0, kinds - 4));
    }

    public sealed record Input(string Name, string Path, MaterialCraftData Craft, int Dose, SlotRole Role)
    {
        public bool IsMain => Role == SlotRole.Jun;
    }

    public sealed record Result(
        bool CanSimulate,
        string BlockingReason,
        bool Exploded,
        bool OrdinaryFailure,
        int CandidateCount,
        int OutputCount,
        int Quality,
        string QualityName,
        int SpiritNeed,
        int EnergyNeed,
        int EffectiveMaterialCount,
        double CoreQuality,
        double PolarityQuality,
        double ConnectionQuality,
        double StabilityQuality,
        double PurityQuality,
        double ZInitial,
        double ZAfterCalming,
        double ExplosionRisk,
        double ConnectionRate,
        double MixingRate,
        double DeliveryRate,
        double FiveElementRate,
        double YinValue,
        double YinDeviation,
        double CarryRate,
        double StorageMultiplier,
        double Effect,
        string YieldRange,
        string Outcome,
        IReadOnlyList<string> Details,
        string ProductName,
        string EffectName,
        string EffectUnit,
        string CoreUtility);

    private sealed class Portion(Input input, double available, double residual)
    {
        public Input Input { get; } = input;
        public double Available { get; set; } = available;
        public double Residual { get; set; } = residual;
        public double Delivered { get; set; }
        public double Yield => BaseYield(Input.Craft);
    }

    private sealed record Metrics(
        int CandidateCount,
        int MinimumCount,
        int MaximumCount,
        int SpiritNeed,
        int EnergyNeed,
        double MainSupport,
        double Core,
        double Polarity,
        double Connection,
        double Stability,
        double Purity,
        double ZInitial,
        double ZAfter,
        double Risk,
        double ConnectionRate,
        double MixingRate,
        double DeliveryRate,
        double FiveElementRate,
        double YinValue,
        double YinDeviation,
        double CarryRate,
        double StorageMultiplier,
        double TotalDelivered,
        IReadOnlyDictionary<string, double> UtilityPower,
        IReadOnlyDictionary<string, double> SourceDelivered,
        double[] QualityAxes);

    /// <summary>
    /// 由投料推导成品。君药携带的核心功用用于识别可能丹种；样方只负责自动填入材料，
    /// 不会把用户锁定到某个结果。
    /// </summary>
    public static Result Simulate(IReadOnlyList<Input> inputs, double attainment = TrialAttainment)
    {
        if (inputs.Any(input => input.Dose < 1 || input.Dose > 4))
            return Blocked(AlchemyRecipeCatalog.Pills[0], "每味药材只能投入1—4整份。", 0, 0, "投料份数不合法。");
        if (inputs.Select(input => input.Path).Distinct(StringComparer.Ordinal).Count() != inputs.Count)
            return Blocked(AlchemyRecipeCatalog.Pills[0], "同一种材料不能重复占药位。", 0, 0, "存在重复材料。");
        foreach (var group in inputs.GroupBy(input => input.Role))
            if (group.Count() > RoleCapacity(group.Key, attainment))
                return Blocked(AlchemyRecipeCatalog.Pills[0], "当前造诣尚未开放所需的扩展药位。", 0, 0, "药位数量超过当前造诣许可。");
        var main = inputs.FirstOrDefault(input => input.IsMain);
        if (main is null)
            return Blocked(AlchemyRecipeCatalog.Pills[0], "先放入一味君药。", 0, 0, "尚未选君药，无法开始炼制。");

        double totalDose = inputs.Sum(input => input.Dose);
        if (totalDose > 8.0001)
            return Blocked(AlchemyRecipeCatalog.Pills[0], "总投料超过 8 份上限；请减少剂量。", 0, 0, "总投料超过试演上限。");

        var candidates = AlchemyRecipeCatalog.Pills
            .Where(pill => Grade(main.Craft, pill.CoreUtility) > 0)
            .Select(pill => SimulateForPill(pill, inputs, attainment))
            .ToArray();

        if (candidates.Length == 0)
            return SimulateUnrecognized(inputs, attainment);

        // 若同一核心功用对应多个已知丹种，取本次实际成形质量最高者。
        // 目前三种试演丹的核心功用互不重复，因此常规情况只有一个候选。
        var chosen = candidates
            .OrderByDescending(result => result.CanSimulate)
            .ThenByDescending(result => result.OutputCount > 0)
            .ThenByDescending(result => result.Quality)
            .ThenByDescending(result => result.Effect)
            .First();
        var details = new List<string>
        {
            $"成品由投料推导：君药核心「{chosen.CoreUtility}」对应{chosen.ProductName}；其余药材共同改变成丹、品质与药效。"
        };
        details.AddRange(chosen.Details);
        return chosen with { Details = details };
    }

    private static Result SimulateForPill(AlchemyRecipeCatalog.PillType pill, IReadOnlyList<Input> inputs, double attainment)
    {
        var main = inputs.FirstOrDefault(input => input.IsMain);
        var mainGrade = main is null ? 0 : Grade(main.Craft, pill.CoreUtility);
        var pillGrade = Math.Clamp(10 - pill.Rank, 1, 9);
        var requiredAttainment = MasteryThreshold[pillGrade];

        if (main is null)
            return Blocked(pill, "先放入一味君药。", 0, 0, "尚未选君药，无法判断成丹。");
        if (mainGrade == 0)
            return Blocked(pill, $"君药必须带有丹方核心功用「{pill.CoreUtility}」。", 0, 0, "君药缺少丹方要求的核心功用。");
        if (attainment < requiredAttainment)
            return Blocked(pill, $"当前试演造诣不足；{pill.Rank}品丹要求造诣 {requiredAttainment}。", 0, 0, "造诣未达到丹方门槛。");
        var metrics = CalculateMetrics(pill, inputs, attainment);
        if (metrics.SpiritNeed > SpiritCapacity)
            return Blocked(pill, $"灵识需求 {metrics.SpiritNeed}，超过试演上限 {SpiritCapacity}。", metrics.SpiritNeed, metrics.EnergyNeed,
                "灵识并行负担超限，尚未开炼。");
        if (metrics.EnergyNeed > EnergyCapacity)
            return Blocked(pill, $"精力需求 {metrics.EnergyNeed}，超过试演上限 {EnergyCapacity}。", metrics.SpiritNeed, metrics.EnergyNeed,
                "精力不足，尚未开炼。");

        var effectiveMaterials = CountEffectiveMaterials(pill, inputs, metrics, attainment);
        int materialCap = effectiveMaterials switch { 1 => 50, 2 => 75, 3 => 90, 4 => 99, _ => 100 };
        int quality = (int)Math.Floor(Math.Min(materialCap, metrics.QualityAxes.Min()));
        bool exploded = metrics.Risk >= 90;
        bool ordinaryFailure = !exploded && quality < 20;
        int outputCount = exploded || ordinaryFailure ? 0 : metrics.CandidateCount;
        double effect = outputCount > 0 ? pill.Effect(quality) : 0;
        string qualityName = QualityName(quality);
        string outcome = exploded
            ? "炸炉，未能成丹。"
            : ordinaryFailure
                ? "普通失败，药性未能凝成丹体。"
                : $"成丹 {outputCount} 枚 · {qualityName} · 丹效 {quality}。";

        var details = new List<string>
        {
            metrics.MaximumCount > 0
                ? $"君药药力支持 {metrics.MinimumCount}—{metrics.MaximumCount} 枚；本方由「聚」计算为 {metrics.CandidateCount} 枚。"
                : "君药核心药力不足以凝成一枚丹胚，本次仍按实际配伍结算为普通失败。",
            $"丹效短板：君药核心 {metrics.Core:0.0} · 阴阳偏性 {metrics.Polarity:0.0} · 通路 {metrics.Connection:0.0} · 稳形与纯度 {metrics.Stability:0.0}。",
            $"有效材料 {effectiveMaterials} 味，材料互补上限 {materialCap} 分；最终取各项最低值并向下取整。",
            $"五行发挥倍率 {metrics.FiveElementRate:P0}；阴阳偏差 {metrics.YinDeviation:P0}（当前偏性 {metrics.YinValue:+0.00;-0.00;0.00}）。",
            $"阴阳相激 {metrics.ZInitial:0.0} → 镇后 {metrics.ZAfter:0.0}；危险值 {metrics.Risk:0.0}，混合效率 {metrics.MixingRate:P0}。",
            $"通路效率 {metrics.ConnectionRate:P0}；药性送达率 {metrics.DeliveryRate:P0}；残余夹带率 {metrics.CarryRate:P1}，纯度分 {metrics.Purity:0.0}。",
            $"资源试算：灵识 {metrics.SpiritNeed}/{SpiritCapacity}，精力 {metrics.EnergyNeed}/{EnergyCapacity}；本页模拟不实际扣除。"
        };
        if (outputCount > 0)
        {
            details.Add($"每枚预估{pill.EffectName} {Math.Floor(effect + .5):0}{pill.EffectUnit}；「藏」留存倍率 {metrics.StorageMultiplier:0.00} 倍（基础时长未配置，暂不换算实际时长）。");
            details.Add("本丹品阶为九品试演，尚未接入正式行囊、丹药效果、首炼造诣奖励或丹炉耐久。");
        }
        else if (exploded)
        {
            details.Add("“护”只能降低炸炉危险，不改变已发生的混合损耗；本页不扣丹炉耐久。");
        }
        else
        {
            details.Add("普通失败不会生成丹药或药渣；试演只展示结果，不消耗材料或精力。");
        }

        details.Add("成品模板只读取君药核心用于识别丹种；臣、佐、使不指定固定药材或固定辅功，实际触发的功用按所在角色进入不同结算通道。");
        details.AddRange(inputs.Select(input => DescribeRoleFunctions(pill, input)));

        return new Result(true, "", exploded, ordinaryFailure, metrics.CandidateCount, outputCount, quality, qualityName,
            metrics.SpiritNeed, metrics.EnergyNeed, effectiveMaterials,
            metrics.Core, metrics.Polarity, metrics.Connection, metrics.Stability, metrics.Purity,
            metrics.ZInitial, metrics.ZAfter, metrics.Risk, metrics.ConnectionRate, metrics.MixingRate, metrics.DeliveryRate,
            metrics.FiveElementRate, metrics.YinValue, metrics.YinDeviation, metrics.CarryRate, metrics.StorageMultiplier,
            effect, YieldText(metrics.MinimumCount, metrics.MaximumCount, metrics.CandidateCount), outcome, details,
            pill.Name, pill.EffectName, pill.EffectUnit, pill.CoreUtility);
    }

    private static Result SimulateUnrecognized(IReadOnlyList<Input> inputs, double attainment)
    {
        int count = inputs.Select(input => input.Path).Distinct(StringComparer.Ordinal).Count();
        double totalDose = inputs.Sum(input => input.Dose);
        int functionPairs = inputs.Sum(input => input.Craft.utilities
            .Where(entry => entry is not null && !string.IsNullOrWhiteSpace(entry.utility_id))
            .Select(entry => entry.utility_id)
            .Distinct(StringComparer.Ordinal)
            .Count(utility => input.IsMain || CanFunctionAct(input.Role, utility)));
        int spirit = count + 2 * functionPairs + (int)Math.Floor(Math.Max(0, totalDose - count) / 3d);
        int energy = (int)Math.Ceiling(20 + Math.Max(0, count - 1) + .5 * Math.Max(0, functionPairs - 1) + .25 * totalDose);

        var portions = inputs.Select(input =>
        {
            double raw = 100 * input.Dose;
            double available = raw * BaseYield(input.Craft);
            return (Input: input, Available: available, Residual: raw - available);
        }).ToArray();
        double yin = portions.Where(part => part.Input.Craft.polarity < 0).Sum(part => part.Available);
        double yang = portions.Where(part => part.Input.Craft.polarity > 0).Sum(part => part.Available);
        double availableTotal = portions.Sum(part => part.Available);
        double zInitial = availableTotal <= 0 ? 0 : Math.Clamp(200 * Math.Min(yin, yang) / availableTotal, 0, 100);
        double calming = portions.Where(part => part.Input.Role == SlotRole.Zuo).Sum(part =>
        {
            int grade = Grade(part.Input.Craft, "镇");
            return grade == 0 ? 0 : part.Input.Dose * Strength(grade) / Strength(1) * Mastery(grade, attainment);
        });
        double guarding = portions.Where(part => part.Input.Role == SlotRole.Zuo).Sum(part =>
        {
            int grade = Grade(part.Input.Craft, "护");
            return grade == 0 ? 0 : part.Input.Dose * Strength(grade) / Strength(1) * Mastery(grade, attainment);
        });
        double zAfter = Math.Max(0, zInitial - 10 * calming);
        double risk = Math.Max(0, zAfter - 5 * guarding);
        double connectionRate = count >= 2 ? Math.Min(1, .70 + .10 * portions.Where(part => part.Input.Role == SlotRole.Shi).Sum(part =>
        {
            int grade = Grade(part.Input.Craft, "通");
            return grade == 0 ? 0 : part.Input.Dose * Strength(grade) / Strength(1) * Mastery(grade, attainment);
        })) : 1;
        double mixRate = connectionRate * (1 - .20 * zAfter / 100);
        double deliveryRate = Math.Min(1, .60 + .10 * portions.Where(part => part.Input.Role == SlotRole.Shi).Sum(part =>
        {
            int grade = Grade(part.Input.Craft, "行");
            return grade == 0 ? 0 : part.Input.Dose * Strength(grade) / Strength(1) * Mastery(grade, attainment);
        }));

        if (spirit > SpiritCapacity)
            return Blocked(AlchemyRecipeCatalog.Pills[0], $"灵识需求 {spirit}，超过试演上限 {SpiritCapacity}。", spirit, energy,
                "灵识并行负担超限，尚未开炼。") with
            {
                ProductName = "未识别丹种",
                EffectName = "药效",
                EffectUnit = "点",
                CoreUtility = "未匹配"
            };
        if (energy > EnergyCapacity)
            return Blocked(AlchemyRecipeCatalog.Pills[0], $"精力需求 {energy}，超过试演上限 {EnergyCapacity}。", spirit, energy,
                "精力不足，尚未开炼。") with
            {
                ProductName = "未识别丹种",
                EffectName = "药效",
                EffectUnit = "点",
                CoreUtility = "未匹配"
            };

        bool exploded = risk >= 90;
        string outcome = exploded
            ? "炸炉：阴阳相激过烈，未能成丹。"
            : "普通失败：君药核心未对应当前已知丹种，药性未凝成可识别丹药。";
        var details = new List<string>
        {
            "成品由投料推导：君药药性尚未对应已知丹种。当前试演允许开炼并结算资源与冲突；失败时不生成丹药。",
            $"阴阳相激 {zInitial:0.0} → 镇后 {zAfter:0.0}；危险值 {risk:0.0}，混合效率 {mixRate:P0}。",
            $"通路效率 {connectionRate:P0}；药性送达率 {deliveryRate:P0}。",
            $"资源试算：灵识 {spirit}/{SpiritCapacity}，精力 {energy}/{EnergyCapacity}；本页模拟不实际扣除。",
            "可以更换君药，或研习并载入已知丹方后调整药材；丹方样例不会限制自由配伍。"
        };
        return new Result(true, "", exploded, !exploded, 0, 0, 0, "未成丹", spirit, energy, count,
            0, 0, 100 * connectionRate, 0, 0, zInitial, zAfter, risk, connectionRate, mixRate, deliveryRate,
            1, 0, 0, 0, 1, 0, "颗数范围　未识别", outcome, details,
            "未识别丹种", "药效", "点", "未匹配");
    }

    private static Result Blocked(AlchemyRecipeCatalog.PillType pill, string reason, int spirit, int energy, string outcome) =>
        new(
            CanSimulate: false,
            BlockingReason: reason,
            Exploded: false,
            OrdinaryFailure: false,
            CandidateCount: 0,
            OutputCount: 0,
            Quality: 0,
            QualityName: "未开炼",
            SpiritNeed: spirit,
            EnergyNeed: energy,
            EffectiveMaterialCount: 0,
            CoreQuality: 0,
            PolarityQuality: 0,
            ConnectionQuality: 0,
            StabilityQuality: 0,
            PurityQuality: 0,
            ZInitial: 0,
            ZAfterCalming: 0,
            ExplosionRisk: 0,
            ConnectionRate: 0,
            MixingRate: 0,
            DeliveryRate: 0,
            FiveElementRate: 0,
            YinValue: 0,
            YinDeviation: 0,
            CarryRate: 0,
            StorageMultiplier: 1,
            Effect: 0,
            YieldRange: "颗数范围　—",
            Outcome: outcome,
            Details: [reason],
            ProductName: pill.Name,
            EffectName: pill.EffectName,
            EffectUnit: pill.EffectUnit,
            CoreUtility: pill.CoreUtility);

    private static Metrics CalculateMetrics(AlchemyRecipeCatalog.PillType pill, IReadOnlyList<Input> inputs, double attainment)
    {
        int pillGrade = Math.Clamp(10 - pill.Rank, 1, 9);
        double pillPower = Strength(pillGrade);
        var main = inputs.FirstOrDefault(input => input.IsMain);
        var usedUtilities = new HashSet<string>(CommonUtilities, StringComparer.Ordinal) { pill.CoreUtility };
        var functionPairs = inputs.Sum(input =>
            (input.IsMain && Grade(input.Craft, pill.CoreUtility) > 0 ? 1 : 0)
            + CommonUtilities.Count(utility => CanFunctionAct(input.Role, utility) && Grade(input.Craft, utility) > 0));
        var count = inputs.Select(input => input.Path).Distinct(StringComparer.Ordinal).Count();
        var totalDose = inputs.Sum(input => input.Dose);
        int spirit = count == 0 ? 0 : count + 2 * functionPairs + (int)Math.Floor(Math.Max(0, totalDose - count) / 3d);

        var utilityPower = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var utility in usedUtilities)
        {
            utilityPower[utility] = inputs.Sum(input =>
            {
                int grade = CanFunctionAct(input.Role, utility) ? Grade(input.Craft, utility) : 0;
                return grade == 0 ? 0 : input.Dose * Strength(grade) / pillPower * Mastery(grade, attainment);
            });
        }

        var portions = inputs.Select(input =>
        {
            double raw = 100 * input.Dose;
            double available = raw * BaseYield(input.Craft);
            return new Portion(input, available, raw - available);
        }).ToList();
        double rawTotal = portions.Sum(portion => portion.Available + portion.Residual);
        var dense = portions.Where(portion => portion.Input.Craft.texture == "致密").ToArray();
        double denseRaw = dense.Sum(portion => portion.Available + portion.Residual);
        double denseResidual = dense.Sum(portion => portion.Residual);
        double breakAmount = Math.Min(denseResidual, Math.Min(.10 * denseRaw, .05 * Power(utilityPower, "破") * denseRaw));
        TransferResidual(dense, breakAmount);
        double residualAfterBreak = portions.Sum(portion => portion.Residual);
        double convertAmount = Math.Min(residualAfterBreak,
            Math.Min(.15 * rawTotal, .05 * Power(utilityPower, "化") * rawTotal));
        TransferResidual(portions, convertAmount);

        double availableTotal = portions.Sum(portion => portion.Available);
        double residualTotal = portions.Sum(portion => portion.Residual);
        double mainAvailable = main is null ? 0 : portions.FirstOrDefault(portion => portion.Input.Path == main.Path)?.Available ?? 0;
        double mainGrade = main is null ? 0 : Grade(main.Craft, pill.CoreUtility);
        double mainSupport = main is null || mainGrade <= 0 ? 0 : mainAvailable * Strength((int)mainGrade) * Mastery((int)mainGrade, attainment) / (25 * pillPower);
        int minCount = mainSupport < 1 ? 0 : Math.Max(1, Math.Min(4, (int)Math.Floor(.75 * mainSupport)));
        int maxCount = mainSupport < 1 ? 0 : Math.Min(4, (int)Math.Floor(mainSupport));
        int candidateCount = mainSupport < 1 ? 0 : Math.Clamp((int)Math.Floor(mainSupport * Math.Min(1, .75 + .05 * Power(utilityPower, "聚"))), 1, 4);

        double connectionRate = count >= 2 ? Math.Min(1, .70 + .10 * Power(utilityPower, "通")) : 1;
        double yinMass = portions.Where(portion => portion.Input.Craft.polarity < 0).Sum(portion => portion.Available);
        double yangMass = portions.Where(portion => portion.Input.Craft.polarity > 0).Sum(portion => portion.Available);
        double neutralMass = portions.Where(portion => portion.Input.Craft.polarity == 0).Sum(portion => portion.Available);
        double polarityBase = availableTotal <= 0 ? 0 : Math.Clamp(200 * Math.Min(yinMass, yangMass) / (yinMass + yangMass + neutralMass), 0, 100);
        double zAfter = Math.Max(0, polarityBase - 10 * Power(utilityPower, "镇"));
        double polarityMixRate = 1 - .20 * zAfter / 100;
        double mixRate = connectionRate * polarityMixRate;
        double deliveryRate = Math.Min(1, .60 + .10 * Power(utilityPower, "行"));
        double totalDelivered = availableTotal * mixRate * deliveryRate;
        double carryRate = Math.Max(0, .15 - .10 * Power(utilityPower, "明"));
        double carryMass = residualTotal * carryRate;
        double contamination = totalDelivered + carryMass <= 0 ? 1 : carryMass / (totalDelivered + carryMass);
        double purity = contamination > .06 ? 0 : 20 + 80 * Math.Clamp((.06 - contamination) / .057, 0, 1);

        var sourceDelivered = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var portion in portions)
        {
            portion.Delivered = candidateCount == 0 ? 0 : portion.Available * mixRate * deliveryRate / candidateCount;
            sourceDelivered[portion.Input.Path] = portion.Delivered;
        }

        var elementVector = Elements.ToDictionary(element => element, _ => 0d, StringComparer.Ordinal);
        foreach (var portion in portions)
        {
            var element = portion.Input.Craft.element.Trim();
            if (elementVector.ContainsKey(element)) elementVector[element] += portion.Delivered;
        }
        double deliveredPerPill = portions.Sum(portion => portion.Delivered);
        if (deliveredPerPill > 0)
            foreach (var element in Elements) elementVector[element] /= deliveredPerPill;
        var target = ElementTarget(pill.Id);
        double distance = .5 * Elements.Sum(element => Math.Abs(elementVector[element] - target.GetValueOrDefault(element)));
        string anchor = pill.Id switch { "huichun" => "木", "tongmai" => "水", _ => "土" };
        string producing = ElementThatGenerates(anchor);
        string overcoming = ElementThatOvercomes(anchor);
        double generatingShare = elementVector.GetValueOrDefault(producing);
        double overcomingShare = elementVector.GetValueOrDefault(overcoming);
        double fiveElementRate = Math.Clamp(1 + .20 * (.50 - distance) + .10 * (generatingShare - overcomingShare), .80, 1.20);

        double yinValue = deliveredPerPill <= 0 ? 0 : portions.Sum(portion => Math.Sign(portion.Input.Craft.polarity) * portion.Delivered) / deliveredPerPill;
        double yinDeviation = Math.Abs(yinValue - pill.PolarityTarget);
        double polarityScore = yinDeviation <= pill.PolarityToleranceAt100
            ? 100
            : yinDeviation > pill.PolarityToleranceAt20
                ? 0
                : 20 + 80 * (pill.PolarityToleranceAt20 - yinDeviation) / (pill.PolarityToleranceAt20 - pill.PolarityToleranceAt100);

        double mainPillMass = main is null ? 0 : sourceDelivered.GetValueOrDefault(main.Path);
        double leadRate = Math.Min(1.25, 1 + .05 * Power(utilityPower, "引"));
        double corePower = mainGrade <= 0 ? 0 : mainPillMass * Strength((int)mainGrade) * Mastery((int)mainGrade, attainment) * fiveElementRate * leadRate;
        double core = CoreQuality(mainPillMass, corePower, pillPower);

        double UtilityDelivered(string utility) => portions.Sum(portion =>
        {
            int grade = CanFunctionAct(portion.Input.Role, utility) ? Grade(portion.Input.Craft, utility) : 0;
            return grade == 0 ? 0 : portion.Delivered * Strength(grade) * Mastery(grade, attainment) * fiveElementRate;
        });
        double sealPower = portions.Where(portion => portion.Input.Role == SlotRole.Zuo)
            .Sum(portion =>
            {
                int grade = Grade(portion.Input.Craft, "藏");
                return grade == 0 ? 0 : portion.Delivered * Strength(grade) * Mastery(grade, attainment) * fiveElementRate;
            });
        double structurePower = UtilityDelivered("固") + .65 * sealPower;
        double solidQuality = 45 + 55 * Math.Pow(Math.Min(1, structurePower / (3 * pillPower)), 1 / 1.15);
        double stability = Math.Min(solidQuality, purity);
        double connectionQuality = 100 * connectionRate;
        double danger = Math.Max(0, zAfter - 5 * Power(utilityPower, "护"));
        double shiStoragePower = inputs.Where(input => input.Role == SlotRole.Shi)
            .Sum(input =>
            {
                int grade = Grade(input.Craft, "藏");
                return grade == 0 ? 0 : input.Dose * Strength(grade) / pillPower * Mastery(grade, attainment);
            });
        double storageMultiplier = Math.Min(2, 1 + .10 * shiStoragePower);
        int energy = (int)Math.Ceiling(20 + RankBonus[Math.Clamp(9 - pill.Rank, 0, RankBonus.Length - 1)]
            + 1.5 * Math.Max(0, candidateCount - 1) + Math.Max(0, count - 1)
            + .5 * Math.Max(0, functionPairs - 1) + .25 * totalDose);

        return new Metrics(candidateCount, minCount, maxCount, spirit, energy, mainSupport,
            core, polarityScore, connectionQuality, stability, purity,
            polarityBase, zAfter, danger, connectionRate, mixRate, deliveryRate, fiveElementRate,
            yinValue, yinDeviation, carryRate, storageMultiplier, totalDelivered,
            utilityPower, sourceDelivered, [core, polarityScore, connectionQuality, stability]);
    }

    private static int CountEffectiveMaterials(AlchemyRecipeCatalog.PillType pill, IReadOnlyList<Input> inputs, Metrics full, double attainment)
    {
        int count = inputs.Any(input => input.IsMain) ? 1 : 0;
        foreach (var assistant in inputs.Where(input => !input.IsMain))
        {
            double sourceMass = full.SourceDelivered.GetValueOrDefault(assistant.Path);
            if (full.TotalDelivered <= 0 || sourceMass * full.CandidateCount / full.TotalDelivered < .05) continue;
            var without = inputs.Where(input => input.Path != assistant.Path).ToArray();
            var reduced = CalculateMetrics(pill, without, attainment);
            bool lowersAnUncappedAxis = full.QualityAxes.Zip(reduced.QualityAxes)
                .Any(pair => pair.First < 99.999 && pair.First - pair.Second >= 1);
            if (lowersAnUncappedAxis) count++;
        }
        return count;
    }

    private static double CoreQuality(double mass, double power, double pillPower)
    {
        if (mass < 12 || power < 8 * pillPower) return 0;
        double massScore = 20 + 80 * Math.Clamp(Math.Pow((mass - 12) / 10, 1 / 1.15), 0, 1);
        double powerScore = 20 + 80 * Math.Clamp(Math.Pow((power / pillPower - 8) / 10, 1 / 1.2), 0, 1);
        return Math.Min(massScore, powerScore);
    }

    private static bool CanFunctionAct(SlotRole role, string utility) => role switch
    {
        SlotRole.Chen => ChenUtilities.Contains(utility, StringComparer.Ordinal),
        SlotRole.Zuo => ZuoUtilities.Contains(utility, StringComparer.Ordinal),
        SlotRole.Shi => ShiUtilities.Contains(utility, StringComparer.Ordinal),
        _ => false
    };

    private static string DescribeRoleFunctions(AlchemyRecipeCatalog.PillType pill, Input input)
    {
        var functions = input.Craft.utilities
            .Where(entry => entry is not null && !string.IsNullOrWhiteSpace(entry.utility_id))
            .GroupBy(entry => entry.utility_id, StringComparer.Ordinal)
            .Select(group => (Utility: group.Key, Grade: group.Max(entry => Math.Clamp(entry.grade, 1, 9))))
            .ToArray();
        var active = functions.Where(function => input.IsMain
                ? function.Utility == pill.CoreUtility
                : CanFunctionAct(input.Role, function.Utility))
            .Select(function => $"{function.Utility}{function.Grade}品").ToArray();
        var inactive = functions.Where(function => input.IsMain
                ? function.Utility != pill.CoreUtility
                : !CanFunctionAct(input.Role, function.Utility))
            .Select(function => function.Utility).ToArray();
        var roleName = input.Role switch
        {
            SlotRole.Jun => "君",
            SlotRole.Chen => "臣",
            SlotRole.Zuo => "佐",
            _ => "使"
        };
        var activeText = active.Length == 0 ? "无额外功用起效" : $"起效：{string.Join("、", active)}";
        var inactiveText = inactive.Length == 0 ? "" : $"；其余功用只计材料组成：{string.Join("、", inactive)}";
        return $"{roleName}位·{input.Name}：{activeText}{inactiveText}";
    }

    private static void TransferResidual(IEnumerable<Portion> eligible, double amount)
    {
        foreach (var portion in eligible.OrderBy(portion => portion.Yield).ThenBy(portion => portion.Input.IsMain ? 0 : 1))
        {
            if (amount <= 0) break;
            double transferred = Math.Min(portion.Residual, amount);
            portion.Residual -= transferred;
            portion.Available += transferred;
            amount -= transferred;
        }
    }

    private static Dictionary<string, double> ElementTarget(string pillId) => pillId switch
    {
        "huichun" => new(StringComparer.Ordinal) { ["木"] = .50, ["火"] = 0, ["土"] = .10, ["金"] = .05, ["水"] = .35 },
        "tongmai" => new(StringComparer.Ordinal) { ["木"] = .20, ["火"] = .10, ["土"] = .20, ["金"] = 0, ["水"] = .50 },
        _ => new(StringComparer.Ordinal) { ["木"] = .30, ["火"] = .05, ["土"] = .25, ["金"] = .10, ["水"] = .30 }
    };

    private static string ElementThatGenerates(string element) => element switch
    {
        "木" => "水", "火" => "木", "土" => "火", "金" => "土", _ => "金"
    };

    private static string ElementThatOvercomes(string element) => element switch
    {
        "木" => "金", "火" => "水", "土" => "木", "金" => "火", _ => "土"
    };

    private static int Grade(MaterialCraftData craft, string utility) => craft.utilities
        .Where(entry => entry is not null && entry.utility_id == utility)
        .Select(entry => Math.Clamp(entry.grade, 1, 9)).DefaultIfEmpty(0).Max();

    private static double Strength(int grade) => AlchemyProcessConfig.Strength[Math.Clamp(grade, 0, 9)];
    private static double Mastery(int grade, double attainment) => Math.Min(1, attainment / MasteryThreshold[Math.Clamp(grade, 1, 9)]);
    private static double Power(IReadOnlyDictionary<string, double> values, string utility) => values.GetValueOrDefault(utility);

    private static double BaseYield(MaterialCraftData craft)
    {
        try { return AlchemyProcessConfig.Profile(craft).Yield; }
        catch { return AlchemyProcessConfig.TestUsableYield; }
    }

    private static string YieldText(int minimum, int maximum, int candidate) =>
        minimum < 1 ? "君药范围　不足一颗" : $"君药范围　{minimum}—{maximum} 颗\n本方拟成　约 {candidate} 颗";

    private static string QualityName(int score) => score switch
    {
        < 20 => "未成丹", <= 50 => "薄效", <= 75 => "良效", <= 90 => "优效", _ => "极效"
    };
}
