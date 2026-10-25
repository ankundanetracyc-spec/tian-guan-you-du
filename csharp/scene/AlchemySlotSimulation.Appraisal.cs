using System;
using System.Collections.Generic;
using System.Linq;

public static partial class AlchemySlotSimulation
{
    // Only scalar values, strings and read-only collections; no live Craft/Resource references.
    public sealed record MaterialAppraisal(string Name, string Path, SlotRole Role, int Dose,
        string Texture, string Element, int Polarity, double BaseYield, double Raw,
        double BaseExtracted, double BreakExtracted, double ConvertExtracted, double Available,
        double MixingLoss, double DeliveryLoss, double Delivered, double Residual, double? Carried,
        IReadOnlyList<string> ActiveFunctions, IReadOnlyList<string> InactiveFunctions)
    {
        public IReadOnlyList<FunctionAppraisal> Functions { get; init; } = Array.Empty<FunctionAppraisal>();
    }

    public sealed record FunctionAppraisal(string Utility, int Grade, bool RoleAllowed, bool Participated);

    public sealed record AppraisalSnapshot(double? SolidQuality, int? MaterialCap, double? Contamination,
        double? CalmingPower, double? GuardingPower, bool TemplateMatched, IReadOnlyList<MaterialAppraisal> Materials)
    {
        public double OverallPolarity { get; init; }
        public double ClarifiedMass { get; init; }
        public double SolidPower { get; init; }
        public double SealPower { get; init; }
    }

    private static MaterialAppraisal SnapshotMaterial(Input input, string? core, double baseExtracted,
        double broken, double converted, double available, double residual, double mixing, double delivery, double? carry)
    {
        var functions = input.Craft.utilities.Where(u => u is not null && !string.IsNullOrWhiteSpace(u.utility_id))
            .GroupBy(u => u.utility_id).Select(g => (Id: g.Key, Grade: g.Max(u => Math.Clamp(u.grade, 1, 9)))).ToArray();
        bool Active(string id) => input.IsMain ? core == id : CanFunctionAct(input.Role, id);
        return new(input.Name, input.Path, input.Role, input.Dose, input.Craft.texture, input.Craft.element,
            input.Craft.polarity, BaseYield(input.Craft), input.Dose * 100, baseExtracted, broken, converted, available,
            available * (1 - mixing), available * mixing * (1 - delivery), available * mixing * delivery,
            residual, carry.HasValue ? residual * carry.Value : null,
            Array.AsReadOnly(functions.Where(f => Active(f.Id)).Select(f => $"{f.Id}{MaterialCraftData.utility_grade_name(f.Grade)}").ToArray()),
            Array.AsReadOnly(functions.Where(f => !Active(f.Id)).Select(f => $"{f.Id}{MaterialCraftData.utility_grade_name(f.Grade)}").ToArray()))
        {
            Functions = Array.AsReadOnly(functions.Select(f => new FunctionAppraisal(f.Id, f.Grade, Active(f.Id),
                Active(f.Id) && (core is not null || f.Id is "镇" or "护" or "通" or "行"))).ToArray())
        };
    }

    public static IReadOnlyList<(string Name, double Score)> AppraisalAxes(Result result)
    {
        var a = result.Appraisal;
        if (a?.TemplateMatched != true) return Array.Empty<(string, double)>();
        return Array.AsReadOnly(new[] { ("君药核心", result.CoreQuality), ("阴阳偏性", result.PolarityQuality),
            ("通路", result.ConnectionQuality), ("稳形", a.SolidQuality!.Value), ("纯度", result.PurityQuality),
            ("有效材料数上限", (double)a.MaterialCap!.Value) });
    }

    public static string AppraisalConclusion(Result result) => FormatAppraisalConclusion(result, DiagnoseAppraisal(result));

    public static string BuildAppraisalReport(Result result) => FormatIntuitiveAppraisal(result, DiagnoseAppraisal(result));
}
