using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

public static partial class AlchemySlotSimulation
{
    public enum DiagnosisKind
    {
        Explosion, UnknownPill, Core, Polarity, Connection, Solid, Purity, Complement,
        CoreSource, CoreAdequate, PolarityObservation, Conflict, Calming, Guarding,
        Residual, Extraction, Clarifying, Misplaced, Delivery, SolidSupport
    }
    public enum DiagnosisImportance { Observation, QualityLimit, DirectFailure }
    public enum AdviceKind { None, Conflict, Identify, Core, Polarity, Connection, Solid, Purity, Complement, Placement }
    public enum UtilityStatus { Absent, Misplaced, NotEvaluated, Participated, Effective }

    // Evidence is internal-only. Subjects reference immutable material snapshots, never live resources.
    public sealed record AppraisalFact(DiagnosisKind Kind, DiagnosisImportance Importance,
        IReadOnlyList<MaterialAppraisal> Subjects, IReadOnlyList<string> Utilities,
        IReadOnlyDictionary<string, double> Evidence, AdviceKind Advice);

    // Text-only bands calibrated against current yields: dense .75, granular .80, fibre .85.
    // No ranking-based severity and no new success conditions.
    internal const double AppraisalResidualManyShare = .20;
    internal const double AppraisalEpsilon = 1e-9;

    public static UtilityStatus AppraisalUtilityStatus(Result result, string utility)
    {
        var a = result.Appraisal;
        if (a is null) return UtilityStatus.Absent;
        var readings = a.Materials.SelectMany(m => m.Functions.Where(f => f.Utility == utility)
            .Select(f => f with { RoleAllowed = m.Role != SlotRole.Jun && f.RoleAllowed, Participated = m.Role != SlotRole.Jun && f.Participated })).ToArray();
        if (readings.Length == 0) return UtilityStatus.Absent;
        if (!readings.Any(f => f.RoleAllowed)) return UtilityStatus.Misplaced;
        if (!readings.Any(f => f.Participated)) return UtilityStatus.NotEvaluated;
        bool effective = utility switch
        {
            "破" => a.Materials.Sum(m => m.BreakExtracted) > AppraisalEpsilon,
            "化" => a.Materials.Sum(m => m.ConvertExtracted) > AppraisalEpsilon,
            "明" => a.ClarifiedMass > AppraisalEpsilon,
            "镇" => result.ZInitial - result.ZAfterCalming > AppraisalEpsilon,
            "护" => result.ZAfterCalming - result.ExplosionRisk > AppraisalEpsilon,
            "通" => a.Materials.Count > 1 && result.ConnectionRate > .70 + AppraisalEpsilon,
            "行" => result.DeliveryRate > .60 + AppraisalEpsilon,
            "固" => a.SolidPower > AppraisalEpsilon,
            "藏" => a.SealPower > AppraisalEpsilon || result.StorageMultiplier > 1 + AppraisalEpsilon,
            // No counterfactual yield claim for 聚, and no attribution from mere role eligibility.
            _ => false
        };
        return effective ? UtilityStatus.Effective : UtilityStatus.Participated;
    }

    private static DiagnosisKind AxisKind(string name) => name switch
    {
        "君药核心" => DiagnosisKind.Core, "阴阳偏性" => DiagnosisKind.Polarity,
        "通路" => DiagnosisKind.Connection, "稳形" => DiagnosisKind.Solid,
        "纯度" => DiagnosisKind.Purity, _ => DiagnosisKind.Complement
    };

    public static IReadOnlyList<AppraisalFact> DiagnoseAppraisal(Result result)
    {
        var a = result.Appraisal;
        if (!result.CanSimulate || a is null) return Array.Empty<AppraisalFact>();
        var facts = new List<AppraisalFact>();
        void Add(DiagnosisKind kind, DiagnosisImportance importance, IEnumerable<MaterialAppraisal> subjects,
            string[] utilities, AdviceKind advice, params (string Key, double Value)[] evidence)
        {
            facts.Add(new(kind, importance, Array.AsReadOnly(subjects.ToArray()), Array.AsReadOnly(utilities),
                new ReadOnlyDictionary<string, double>(evidence.ToDictionary(x => x.Key, x => x.Value)), advice));
        }
        var main = a.Materials.Where(m => m.Role == SlotRole.Jun).ToArray();
        IEnumerable<MaterialAppraisal> Providers(params string[] utilities) => a.Materials.Where(m => m.Role != SlotRole.Jun &&
            m.Functions.Any(f => f.Participated && utilities.Contains(f.Utility, StringComparer.Ordinal)));

        if (result.Exploded)
            Add(DiagnosisKind.Explosion, DiagnosisImportance.DirectFailure, a.Materials, ["镇", "护"], AdviceKind.Conflict,
                ("initial", result.ZInitial), ("calmed", result.ZAfterCalming), ("risk", result.ExplosionRisk));
        if (!a.TemplateMatched)
            Add(DiagnosisKind.UnknownPill, result.Exploded ? DiagnosisImportance.Observation : DiagnosisImportance.DirectFailure,
                main, [], result.Exploded ? AdviceKind.None : AdviceKind.Identify, ("matched", 0));
        else
        {
            var axes = AppraisalAxes(result);
            double minimum = axes.Min(x => x.Score);
            foreach (var axis in axes)
            {
                var kind = AxisKind(axis.Name);
                var importance = !result.Exploded && result.OrdinaryFailure && axis.Score < 20
                    ? DiagnosisImportance.DirectFailure
                    : result.OutputCount > 0 && minimum < 100 - AppraisalEpsilon && Math.Abs(axis.Score - minimum) < AppraisalEpsilon
                        ? DiagnosisImportance.QualityLimit : DiagnosisImportance.Observation;
                if (importance == DiagnosisImportance.Observation) continue;
                string[] utilities = kind switch
                {
                    DiagnosisKind.Core => [result.CoreUtility], DiagnosisKind.Connection => ["通"],
                    DiagnosisKind.Solid => ["固", "藏"], DiagnosisKind.Purity => ["破", "化", "明"], _ => []
                };
                var subjects = kind == DiagnosisKind.Core ? main.AsEnumerable()
                    : kind == DiagnosisKind.Purity ? a.Materials.Where(m => m.Residual > AppraisalEpsilon)
                    : a.Materials;
                var advice = kind switch
                {
                    DiagnosisKind.Core => AdviceKind.Core, DiagnosisKind.Polarity => AdviceKind.Polarity,
                    DiagnosisKind.Connection => AdviceKind.Connection, DiagnosisKind.Solid => AdviceKind.Solid,
                    DiagnosisKind.Purity => AdviceKind.Purity, _ => AdviceKind.Complement
                };
                Add(kind, importance, subjects, utilities, advice, ("score", axis.Score), ("minimum", minimum));
            }
            Add(DiagnosisKind.CoreSource, DiagnosisImportance.Observation, main, [result.CoreUtility], AdviceKind.None,
                ("core", result.CoreQuality), ("candidate", result.CandidateCount));
            if (result.OrdinaryFailure && result.CoreQuality >= 20)
                Add(DiagnosisKind.CoreAdequate, DiagnosisImportance.Observation, main, [result.CoreUtility], AdviceKind.None, ("core", result.CoreQuality));
            Add(DiagnosisKind.PolarityObservation, DiagnosisImportance.Observation, a.Materials, [], AdviceKind.None,
                ("balance", a.OverallPolarity), ("score", result.PolarityQuality));
        }

        Add(DiagnosisKind.Conflict, DiagnosisImportance.Observation, a.Materials.Where(m => m.Polarity != 0), [], AdviceKind.None,
            ("initial", result.ZInitial), ("after", result.ZAfterCalming));
        if (result.ZInitial - result.ZAfterCalming > AppraisalEpsilon)
            Add(DiagnosisKind.Calming, DiagnosisImportance.Observation, Providers("镇"), ["镇"], AdviceKind.None,
                ("reduction", result.ZInitial - result.ZAfterCalming));
        if (result.ZAfterCalming - result.ExplosionRisk > AppraisalEpsilon)
            Add(DiagnosisKind.Guarding, DiagnosisImportance.Observation, Providers("护"), ["护"], AdviceKind.None,
                ("reduction", result.ZAfterCalming - result.ExplosionRisk));

        foreach (var row in a.Materials.Where(m => m.Residual > AppraisalEpsilon))
            Add(DiagnosisKind.Residual, DiagnosisImportance.Observation, [row], [], AdviceKind.None,
                ("raw", row.Raw), ("residual", row.Residual), ("share", row.Residual / row.Raw));
        if (a.TemplateMatched && a.Materials.Any(m => m.Residual + m.BreakExtracted + m.ConvertExtracted > AppraisalEpsilon))
        {
            Add(DiagnosisKind.Extraction, DiagnosisImportance.Observation, Providers("破", "化"), ["破", "化"], AdviceKind.None,
                ("break", a.Materials.Sum(m => m.BreakExtracted)), ("convert", a.Materials.Sum(m => m.ConvertExtracted)));
            if (a.Materials.Sum(m => m.Residual) > AppraisalEpsilon)
                Add(DiagnosisKind.Clarifying, DiagnosisImportance.Observation, Providers("明"), ["明"], AdviceKind.None,
                    ("reduction", a.ClarifiedMass), ("purity", result.PurityQuality));
        }
        foreach (var row in a.Materials)
        {
            var misplaced = row.Functions.Where(f => !f.RoleAllowed && (a.TemplateMatched || row.Role != SlotRole.Jun))
                .Select(f => f.Utility).ToArray();
            if (misplaced.Length > 0)
                Add(DiagnosisKind.Misplaced, DiagnosisImportance.Observation, [row], misplaced, AdviceKind.Placement, ("role", (int)row.Role));
        }
        if (a.TemplateMatched)
        {
            double mixLoss = a.Materials.Sum(m => m.MixingLoss), deliveryLoss = a.Materials.Sum(m => m.DeliveryLoss);
            if (mixLoss > AppraisalEpsilon || deliveryLoss > AppraisalEpsilon)
                Add(DiagnosisKind.Delivery, DiagnosisImportance.Observation, a.Materials, ["通", "行"], AdviceKind.None,
                    ("mixingLoss", mixLoss), ("deliveryLoss", deliveryLoss));
            if (a.SolidQuality < 100 - AppraisalEpsilon)
                Add(DiagnosisKind.SolidSupport, DiagnosisImportance.Observation,
                    Providers("固", "藏").Where(m => m.Role == SlotRole.Zuo || m.Role == SlotRole.Chen), ["固", "藏"], AdviceKind.None,
                    ("solidPower", a.SolidPower), ("sealPower", a.SealPower));
        }
        return Array.AsReadOnly(facts.ToArray());
    }
}
