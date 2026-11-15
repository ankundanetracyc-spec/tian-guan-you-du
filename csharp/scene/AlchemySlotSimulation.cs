using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>槽位式炼丹的确定性试演结算；只读当前材料，不写入行囊或角色资源。</summary>
public static partial class AlchemySlotSimulation
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

    public static double RequiredAttainment(int pillRank) => MasteryThreshold[Math.Clamp(10 - pillRank, 1, 9)];

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
        string YieldRange,
        string Outcome,
        IReadOnlyList<string> Details,
        string ProductName,
        string CoreUtility)
    {
        public string ProductId { get; init; } = "";
        public IReadOnlyList<PillResult> Pills { get; init; } = [];
        public double DeliveredMass { get; init; }
        public double CarryMass { get; init; }
        /// <summary>正式人物开炉后获得的成长；独立试演恒为0。</summary>
        public double ProficiencyGrowth { get; init; }
    }

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
        double[] QualityAxes, double CarryMass);

    /// <summary>
    /// 由投料推导成品。君药携带的核心功用用于识别可能丹种；样方只负责自动填入材料，
    /// 不会把用户锁定到某个结果。
    /// </summary>
    public static Result Simulate(IReadOnlyList<Input> inputs, double attainment = TrialAttainment,
        int totalSpirit = SpiritCapacity, int otherSpiritOccupation = 0, int requestedCount = 0)
    {
        if (!double.IsFinite(attainment) || attainment < 0 || requestedCount is < 0 or > 4)
            return Blocked(AlchemyRecipeCatalog.Pills[0], "造诣或本炉颗数不合法。", 0, 0, "炼制配置无效。");
        if (inputs.Any(input => input.Dose < 1))
            return Blocked(AlchemyRecipeCatalog.Pills[0], "每味药材至少投入1整份。", 0, 0, "投料份数不合法。");
        if (inputs.Select(input => input.Path).Distinct(StringComparer.Ordinal).Count() != inputs.Count)
            return Blocked(AlchemyRecipeCatalog.Pills[0], "同一种材料不能重复占药位。", 0, 0, "存在重复材料。");
        foreach (var group in inputs.GroupBy(input => input.Role))
            if (group.Count() > RoleCapacity(group.Key, attainment))
                return Blocked(AlchemyRecipeCatalog.Pills[0], "当前造诣尚未开放所需的扩展药位。", 0, 0, "药位数量超过当前造诣许可。");
        var main = inputs.FirstOrDefault(input => input.IsMain);
        if (main is null)
            return Blocked(AlchemyRecipeCatalog.Pills[0], "先放入一味君药。", SpiritOccupation(inputs), 0, "尚未选君药，无法开始炼制。");

        var selected = IdentifyPill(inputs);
        if (selected is null)
            return SimulateUnrecognized(inputs, attainment, AvailableSpirit(totalSpirit, otherSpiritOccupation));
        // 先识别再检查准入与评分，不以更高丹效或可开炼与否倒选丹种。
        var chosen = SimulateForPill(selected, inputs, attainment, AvailableSpirit(totalSpirit, otherSpiritOccupation), requestedCount);
        var details = new List<string>
        {
            $"成品由投料推导：君药核心「{chosen.CoreUtility}」对应{chosen.ProductName}；其余药材共同改变成丹、品质与药效。"
        };
        details.AddRange(chosen.Details);
        return chosen with { Details = details };
    }

    private static Result SimulateForPill(AlchemyRecipeCatalog.PillType pill, IReadOnlyList<Input> inputs, double attainment, int spiritCapacity, int requestedCount)
    {
        var main = inputs.FirstOrDefault(input => input.IsMain);
        var mainGrade = main is null ? 0 : Grade(main.Craft, pill.CoreUtility);
        var pillGrade = Math.Clamp(10 - pill.Rank, 1, 9);
        var requiredAttainment = RequiredAttainment(pill.Rank);

        if (main is null)
            return Blocked(pill, "先放入一味君药。", 0, 0, "尚未选君药，无法判断成丹。");
        if (mainGrade == 0)
            return Blocked(pill, $"君药必须带有丹方核心功用「{pill.CoreUtility}」。", 0, 0, "君药缺少丹方要求的核心功用。");
        if (attainment < requiredAttainment)
            return Blocked(pill, $"当前试演造诣不足；{pill.Rank}品丹要求造诣 {requiredAttainment}。", 0, 0, "造诣未达到丹方门槛。");
        if (requestedCount < 0 || requestedCount > 4)
            return Blocked(pill, "本炉颗数必须为1—4颗。", SpiritOccupation(inputs), 0, "颗数配置不合法。");
        var supported = CalculateMetrics(pill, inputs, attainment);
        if (requestedCount > 0 && supported.MaximumCount > 0 && requestedCount > supported.MaximumCount)
            return Blocked(pill, "当前君药和聚性不足以支持所选颗数，请减产或调整配伍。", supported.SpiritNeed, supported.EnergyNeed, "颗数超出实际支持能力。");
        var metrics = requestedCount == 0 ? supported : CalculateMetrics(pill, inputs, attainment, requestedCount);
        if (metrics.SpiritNeed > spiritCapacity)
            return Blocked(pill, $"本炉需灵识 {metrics.SpiritNeed}，可支配 {spiritCapacity}；请换药、减量或调整药位。", metrics.SpiritNeed, metrics.EnergyNeed,
                "灵识并行负担超限，尚未开炼。");
        if (metrics.EnergyNeed > EnergyCapacity)
            return Blocked(pill, $"精力需求 {metrics.EnergyNeed}，超过试演上限 {EnergyCapacity}。", metrics.SpiritNeed, metrics.EnergyNeed,
                "精力不足，尚未开炼。");

        var effectiveMaterials = CountEffectiveMaterials(pill, inputs, metrics, attainment);
        bool exploded = metrics.Risk >= 90;
        var distributed = Distribute(pill, inputs, metrics, attainment);
        var pills = exploded ? Array.Empty<PillResult>() : distributed.ToArray();
        var successful = pills.Where(result => result.Quality >= 20).ToArray();
        bool ordinaryFailure = !exploded && successful.Length == 0;
        int outputCount = successful.Length;
        int quality = exploded ? 0 : successful.Length == 0 ? distributed.Select(result => result.Quality).DefaultIfEmpty(0).Max()
            : successful.Max(result => result.Quality);
        string qualityName = QualityName(quality);
        string breakdown = string.Join("、", successful.GroupBy(result => result.QualityName)
            .OrderByDescending(group => group.Max(result => result.Quality)).Select(group => $"{group.Key}×{group.Count()}"));
        string outcome = exploded ? "炸炉，未能成丹。" : ordinaryFailure ? "普通失败，药性未能凝成丹体。"
            : $"成丹 {outputCount} 枚 · {breakdown}。";

        var details = new List<string>
        {
            metrics.MaximumCount > 0
                ? $"本炉配伍自动形成 {metrics.CandidateCount} 枚候选丹胚，再逐颗判定是否成丹。"
                : "君药核心药力不足以凝成一枚丹胚，本次仍按实际配伍结算为普通失败。",
            $"丹效短板：君药核心 {metrics.Core:0.0} · 阴阳偏性 {metrics.Polarity:0.0} · 通路 {metrics.Connection:0.0} · 稳形与纯度 {metrics.Stability:0.0}。",
            $"实际贡献材料 {effectiveMaterials} 味（仅供诊断，不按味数封顶）；各丹按关键短板连续判分。",
            $"五行发挥倍率 {metrics.FiveElementRate:P0}；阴阳偏差 {metrics.YinDeviation:P0}（当前偏性 {metrics.YinValue:+0.00;-0.00;0.00}）。",
            $"阴阳相激 {metrics.ZInitial:0.0} → 镇后 {metrics.ZAfter:0.0}；危险值 {metrics.Risk:0.0}，混合效率 {metrics.MixingRate:P0}。",
            $"通路效率 {metrics.ConnectionRate:P0}；药性送达率 {metrics.DeliveryRate:P0}；残余夹带率 {metrics.CarryRate:P1}，纯度分 {metrics.Purity:0.0}。",
            $"资源试算：灵识 {metrics.SpiritNeed}/{spiritCapacity}，精力 {metrics.EnergyNeed}/{EnergyCapacity}；正式扣除由人物事务完成，独立试演免扣。"
        };
        if (outputCount > 0)
        {
            details.Add("「藏」减少本炉已炼出药性散失，不改变同档成品药效或时长；「引」在丹胚间搬运主药药性，整炉守恒。");
            details.AddRange(pills.Select(result => $"第{result.Index}颗：{result.QualityName} · {result.Quality}分；君药质量{result.MainMass:0.00}，阴阳{result.YinValue:+0.00;-0.00;0.00}，夹带{result.Contamination:P1}。"));
            details.Add("本丹按九品及对应丹效档生成；同档效果固定。丹炉耐久与额外造诣奖励不在本轮结算中。");
        }
        else if (exploded)
        {
            details.Add("“护”只能降低炸炉危险，不改变已发生的混合损耗；本页不扣丹炉耐久。");
        }
        else
        {
            details.Add("普通失败不生成丹药或药渣；正式事务仍扣材料与精力，独立试演免扣。");
        }

        details.Add("丹种由君药核心、整炉五行与阴阳识别；臣、佐、使不指定固定药材或固定辅功，实际触发的功用按所在角色进入不同结算通道。");
        details.AddRange(inputs.Select(input => DescribeRoleFunctions(pill, input)));

        return new Result(true, "", exploded, ordinaryFailure, metrics.CandidateCount, outputCount, quality, qualityName,
            metrics.SpiritNeed, metrics.EnergyNeed, effectiveMaterials,
            metrics.Core, metrics.Polarity, metrics.Connection, metrics.Stability, metrics.Purity,
            metrics.ZInitial, metrics.ZAfter, metrics.Risk, metrics.ConnectionRate, metrics.MixingRate, metrics.DeliveryRate,
            metrics.FiveElementRate, metrics.YinValue, metrics.YinDeviation, metrics.CarryRate, metrics.StorageMultiplier,
            YieldText(metrics.MinimumCount, metrics.MaximumCount, metrics.CandidateCount), outcome, details,
            pill.Name, pill.CoreUtility) { ProductId = pill.Id, Pills = pills,
                DeliveredMass = metrics.TotalDelivered, CarryMass = metrics.CarryMass };
    }

    private static Result SimulateUnrecognized(IReadOnlyList<Input> inputs, double attainment, int spiritCapacity)
    {
        int count = inputs.Select(input => input.Path).Distinct(StringComparer.Ordinal).Count();
        double totalDose = inputs.Sum(input => (double)input.Dose);
        int functionPairs = inputs.Sum(input => input.Craft.utilities
            .Where(entry => entry is not null && !string.IsNullOrWhiteSpace(entry.utility_id))
            .Select(entry => entry.utility_id)
            .Distinct(StringComparer.Ordinal)
            .Count(utility => input.IsMain || CanFunctionAct(input.Role, utility)));
        int spirit = SpiritOccupation(inputs);
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

        if (spirit > spiritCapacity)
            return Blocked(AlchemyRecipeCatalog.Pills[0], $"本炉需灵识 {spirit}，可支配 {spiritCapacity}；请换药、减量或调整药位。", spirit, energy,
                "灵识并行负担超限，尚未开炼。") with
            {
                ProductName = "未识别丹种",

                CoreUtility = "未匹配"
            };
        if (energy > EnergyCapacity)
            return Blocked(AlchemyRecipeCatalog.Pills[0], $"精力需求 {energy}，超过试演上限 {EnergyCapacity}。", spirit, energy,
                "精力不足，尚未开炼。") with
            {
                ProductName = "未识别丹种",

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
            $"资源试算：灵识 {spirit}/{spiritCapacity}，精力 {energy}/{EnergyCapacity}；正式扣除由人物事务完成，独立试演免扣。",
            "可以更换君药，或研习并载入已知丹方后调整药材；丹方样例不会限制自由配伍。"
        };
        return new Result(true, "", exploded, !exploded, 0, 0, 0, "未成丹", spirit, energy, count,
            0, 0, 100 * connectionRate, 0, 0, zInitial, zAfter, risk, connectionRate, mixRate, deliveryRate,
            1, 0, 0, 0, 1, "颗数范围　未识别", outcome, details,
            "未识别丹种", "未匹配");
    }

    // 首轮可调占用参数；独立于药力 Strength，避免把高阶材料的药力指数再用作费用指数。
    private static readonly double[] SpiritFunctionCosts = [0, .75, 1, 1.4, 2, 3, 4.5, 7, 11, 17];
    public sealed record SpiritLoad(Input Material, double BaseLoad, double FunctionLoad,
        double RoleMultiplier, double DoseMultiplier, double Occupation, string Functions);

    public static IReadOnlyList<SpiritLoad> SpiritLoads(IReadOnlyList<Input> inputs, string? selectedCore = null)
    {
        selectedCore ??= IdentifyPill(inputs)?.CoreUtility ?? "";
        return inputs.Select(input =>
        {
            var functions = input.Craft.utilities
                .Where(entry => entry is not null && !string.IsNullOrWhiteSpace(entry.utility_id))
                .Select(entry => entry.utility_id).Distinct(StringComparer.Ordinal).ToArray();
            var active = functions.Where(utility => input.IsMain
                ? utility == selectedCore
                : CanFunctionAct(input.Role, utility)).ToArray();
            int materialGrade = Math.Clamp(input.Craft.material_grade_value(), 0, 27);
            double baseLoad = 1.0 + .13 * materialGrade + .0065 * materialGrade * materialGrade;
            double functionLoad = active.Sum(utility => SpiritFunctionCosts[Grade(input.Craft, utility)]);
            double roleMultiplier = input.Role switch { SlotRole.Jun => 1.4, SlotRole.Chen => 1.2, SlotRole.Zuo => 1, _ => .9 };
            double doseMultiplier = 1 + .40 * Math.Max(0d, input.Dose - 1) + .04 * Math.Pow(Math.Max(0d, input.Dose - 1), 2);
            return new SpiritLoad(input, baseLoad, functionLoad, roleMultiplier, doseMultiplier,
                (baseLoad + functionLoad) * roleMultiplier * doseMultiplier,
                string.Join("、", active.Select(utility => $"{utility}{MaterialCraftData.utility_grade_name(Grade(input.Craft, utility))}")));
        }).ToArray();
    }

    // 各味保留小数，只在整炉合计后向上取整，防止拆味逐项取整形成额外负担。
    public static int SpiritOccupation(IReadOnlyList<Input> inputs, string? selectedCore = null) =>
        (int)Math.Min(int.MaxValue, Math.Ceiling(Math.Round(SpiritLoads(inputs, selectedCore).Sum(line => line.Occupation), 8)));

    public static int AvailableSpirit(int total, int otherOccupation) => Math.Max(0, total - Math.Max(0, otherOccupation));

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
            YieldRange: "颗数范围　—",
            Outcome: outcome,
            Details: [reason],
            ProductName: pill.Name,
            CoreUtility: pill.CoreUtility);

    private static Metrics CalculateMetrics(AlchemyRecipeCatalog.PillType pill, IReadOnlyList<Input> inputs, double attainment, int requestedCount = 0)
    {
        int pillGrade = Math.Clamp(10 - pill.Rank, 1, 9);
        double pillPower = Strength(pillGrade);
        var main = inputs.FirstOrDefault(input => input.IsMain);
        var usedUtilities = new HashSet<string>(CommonUtilities, StringComparer.Ordinal) { pill.CoreUtility };
        var functionPairs = inputs.Sum(input =>
            (input.IsMain && Grade(input.Craft, pill.CoreUtility) > 0 ? 1 : 0)
            + CommonUtilities.Count(utility => CanFunctionAct(input.Role, utility) && Grade(input.Craft, utility) > 0));
        var count = inputs.Select(input => input.Path).Distinct(StringComparer.Ordinal).Count();
        var totalDose = inputs.Sum(input => (double)input.Dose);
        int spirit = SpiritOccupation(inputs, pill.CoreUtility);

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
            Math.Min(Math.Max(0, .20 * rawTotal - breakAmount), Math.Min(.15 * rawTotal, .05 * Power(utilityPower, "化") * rawTotal)));
        TransferResidual(portions, convertAmount);

        double availableTotal = portions.Sum(portion => portion.Available);
        double residualTotal = portions.Sum(portion => portion.Residual);
        double mainAvailable = main is null ? 0 : portions.FirstOrDefault(portion => portion.Input.Path == main.Path)?.Available ?? 0;
        double mainGrade = main is null ? 0 : Grade(main.Craft, pill.CoreUtility);
        double mainSupport = main is null || mainGrade <= 0 ? 0 : mainAvailable * Strength((int)mainGrade) * Mastery((int)mainGrade, attainment) / (25 * pillPower);
        int minCount = mainSupport < 1 ? 0 : 1;
        int maxCount = mainSupport < 1 ? 0 : Math.Min(4, (int)Math.Floor(mainSupport));
        int candidateCount = mainSupport < 1 ? 0 : Math.Clamp((int)Math.Floor(mainSupport * Math.Min(1, .75 + .05 * Power(utilityPower, "聚"))), 1, 4);
        maxCount = candidateCount;
        if (requestedCount > 0 && mainSupport >= 1) candidateCount = requestedCount;

        double connectionRate = count >= 2 ? Math.Min(1, .70 + .10 * Power(utilityPower, "通")) : 1;
        double yinMass = portions.Where(portion => portion.Input.Craft.polarity < 0).Sum(portion => portion.Available);
        double yangMass = portions.Where(portion => portion.Input.Craft.polarity > 0).Sum(portion => portion.Available);
        double neutralMass = portions.Where(portion => portion.Input.Craft.polarity == 0).Sum(portion => portion.Available);
        double polarityBase = availableTotal <= 0 ? 0 : Math.Clamp(200 * Math.Min(yinMass, yangMass) / (yinMass + yangMass + neutralMass), 0, 100);
        double zAfter = Math.Max(0, polarityBase - 10 * Power(utilityPower, "镇"));
        double polarityMixRate = 1 - .20 * zAfter / 100;
        double mixRate = connectionRate * polarityMixRate;
        double deliveryRate = Math.Min(1, .60 + .10 * Power(utilityPower, "行"));
        double preservation = 1 - .03 * Math.Max(0, 1 - .20 * Power(utilityPower, "藏"));
        double totalDelivered = availableTotal * mixRate * deliveryRate * preservation;
        double carryRate = .15 / (1 + Power(utilityPower, "明"));
        double carryMass = residualTotal * carryRate;
        double contamination = totalDelivered + carryMass <= 0 ? 1 : carryMass / (totalDelivered + carryMass);
        double purity = AlchemyQualityRules.Purity(contamination);

        var sourceDelivered = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var portion in portions)
        {
            portion.Delivered = candidateCount == 0 ? 0 : portion.Available * mixRate * deliveryRate * preservation / candidateCount;
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
        string anchor = AlchemyRecipeCatalog.Anchor(pill.Id);
        string producing = ElementThatGenerates(anchor);
        string overcoming = ElementThatOvercomes(anchor);
        double generatingShare = elementVector.GetValueOrDefault(producing);
        double overcomingShare = elementVector.GetValueOrDefault(overcoming);
        double fiveElementRate = Math.Clamp(1 + .25 * (.50 - distance) + .125 * (generatingShare - overcomingShare), .75, 1.25);

        double yinValue = deliveredPerPill <= 0 ? 0 : portions.Sum(portion => Math.Sign(portion.Input.Craft.polarity) * portion.Delivered) / deliveredPerPill;
        double yinDeviation = Math.Abs(yinValue - pill.PolarityTarget);
        double polarityScore = AlchemyQualityRules.Polarity(yinDeviation, pill.PolarityToleranceAt20, pill.PolarityToleranceAt100);

        double mainPillMass = main is null ? 0 : sourceDelivered.GetValueOrDefault(main.Path);
        double leadRate = 1; // 引在逐丹分配阶段转移药性，不复制全炉药力。
        double corePower = mainGrade <= 0 ? 0 : mainPillMass * Strength((int)mainGrade) * Mastery((int)mainGrade, attainment) * fiveElementRate * leadRate;
        double core = CoreQuality(mainPillMass, corePower, pillPower);

        double UtilityDelivered(string utility) => portions.Sum(portion =>
        {
            int grade = CanFunctionAct(portion.Input.Role, utility) ? Grade(portion.Input.Craft, utility) : 0;
            return grade == 0 ? 0 : portion.Delivered * Strength(grade) * Mastery(grade, attainment) * fiveElementRate;
        });
        double structurePower = UtilityDelivered("固") + portions.Sum(portion => portion.Delivered * TextureBinding(portion.Input.Craft.texture));
        double solidQuality = Math.Max(45, AlchemyQualityRules.RawForDemand(structurePower / (8 * pillPower)));
        double stability = Math.Min(solidQuality, purity);
        double connectionQuality = count < 2 ? 100 : 65 + 35 * Math.Min(1, Power(utilityPower, "通") / 6);
        double danger = Math.Max(0, zAfter - 5 * Power(utilityPower, "护"));
        double storageMultiplier = 1; // 兼容旧结果字段，成品不附加独立时长。
        int energy = (int)Math.Ceiling(20 + RankBonus[Math.Clamp(9 - pill.Rank, 0, RankBonus.Length - 1)]
            + 1.5 * Math.Max(0, (requestedCount > 0 ? requestedCount : candidateCount) - 1) + Math.Max(0, count - 1)
            + .5 * Math.Max(0, functionPairs - 1) + .25 * totalDose);

        return new Metrics(candidateCount, minCount, maxCount, spirit, energy, mainSupport,
            core, polarityScore, connectionQuality, stability, purity,
            polarityBase, zAfter, danger, connectionRate, mixRate, deliveryRate, fiveElementRate,
            yinValue, yinDeviation, carryRate, storageMultiplier, totalDelivered,
            utilityPower, sourceDelivered, [core, polarityScore, connectionQuality, stability], carryMass);
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
            bool lowersAnAxis = full.QualityAxes.Zip(reduced.QualityAxes)
                .Any(pair => pair.First - pair.Second >= 1 - 1e-8);
            if (lowersAnAxis) count++;
        }
        return count;
    }

    private static double CoreQuality(double mass, double power, double pillPower) =>
        Math.Min(AlchemyQualityRules.RawForDemand(mass / 12),
            AlchemyQualityRules.RawForDemand(power / (8 * pillPower)));

    internal static bool CanFunctionAct(SlotRole role, string utility) => role switch
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
        foreach (var portion in eligible.OrderBy(portion => portion.Yield).ThenBy(portion => portion.Input.IsMain ? 0 : 1)
            .ThenBy(portion => (int)portion.Input.Role).ThenBy(portion => portion.Input.Path, StringComparer.Ordinal))
        {
            if (amount <= 0) break;
            double transferred = Math.Min(portion.Residual, amount);
            portion.Residual -= transferred;
            portion.Available += transferred;
            amount -= transferred;
        }
    }

    private static Dictionary<string, double> ElementTarget(string pillId) => AlchemyRecipeCatalog.ElementTarget(pillId);

    /// <summary>以基础得率参考质量识别，不读取丹效、存档丹方名或实际逐颗分配。</summary>
    public static AlchemyRecipeCatalog.PillType? IdentifyPill(IReadOnlyList<Input> inputs)
    {
        var main = inputs.FirstOrDefault(i => i.IsMain);
        if (main is null || inputs.Any(i => i.Dose < 1)) return null;
        double mass = inputs.Sum(i => 100d * i.Dose * BaseYield(i.Craft));
        if (mass <= 0) return null;
        var vector = Elements.ToDictionary(e => e, e => inputs.Where(i => i.Craft.element.Trim() == e)
            .Sum(i => 100d * i.Dose * BaseYield(i.Craft)) / mass, StringComparer.Ordinal);
        double yin = inputs.Sum(i => 100d * i.Dose * BaseYield(i.Craft) * Math.Sign(i.Craft.polarity)) / mass;
        var candidates = AlchemyRecipeCatalog.Pills.Select(p => new { Pill = p, Grade = Grade(main.Craft, p.CoreUtility),
            Distance = .5 * Elements.Sum(e => Math.Abs(vector[e] - ElementTarget(p.Id)[e])) + .5 * Math.Abs(yin - p.PolarityTarget) })
            .Where(p => p.Grade > 0 && p.Distance <= .85 + 1e-9).ToArray();
        if (candidates.Length == 0) return null;
        int grade = candidates.Max(p => p.Grade);
        double distance = candidates.Where(p => p.Grade == grade).Min(p => p.Distance);
        return candidates.Where(p => p.Grade == grade && p.Distance <= distance + 1e-6)
            .OrderBy(p => p.Pill.Id, StringComparer.Ordinal).First().Pill;
    }

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

    private static string QualityName(int score) => AlchemyQualityRules.Name(score);
}
