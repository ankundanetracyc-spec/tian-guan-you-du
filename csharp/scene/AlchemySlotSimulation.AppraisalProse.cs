using System;
using System.Collections.Generic;
using System.Linq;

public static partial class AlchemySlotSimulation
{
    // All player copy lives here. Exact evidence is deliberately never interpolated into prose.
    internal static string AppraisalConcept(string value)
    {
        string clean = value.Replace("「", "").Replace("」", "").Replace("\r", "").Replace("\n", "").Trim();
        if (clean.Length == 0 || clean.Contains('/') || clean.Contains('\\') || clean.Contains(':')) clean = "未知";
        return "「" + clean + "」";
    }
    private static string PillConcept(Result result)
    {
        string name = result.ProductName;
        if (name.Length > 2 && "一二三四五六七八九".Contains(name[0]) && name[1] == '品') name = name[2..];
        return AppraisalConcept(name);
    }
    private static string RoleConcept(SlotRole role) => AppraisalConcept(role switch
    {
        SlotRole.Jun => "君", SlotRole.Chen => "臣", SlotRole.Zuo => "佐", _ => "使"
    }) + "位";
    private static string Concepts(IEnumerable<string> values) => string.Join("", values.Distinct().Select(AppraisalConcept));
    private static string MaterialConcepts(IEnumerable<MaterialAppraisal> rows) => string.Join("、", rows.Select(m => AppraisalConcept(m.Name)).Distinct());
    private static bool IsPriority(AppraisalFact f) => f.Importance != DiagnosisImportance.Observation;
    private static string Problem(DiagnosisKind kind) => kind switch
    {
        DiagnosisKind.Core => "核心药性不足", DiagnosisKind.Polarity => "阴阳偏性不合",
        DiagnosisKind.Connection => "药性相合不足", DiagnosisKind.Solid => "丹体稳形不足",
        DiagnosisKind.Purity => "杂质夹带过多", _ => "材料之间的实际互补有限"
    };

    private static string FormatAppraisalConclusion(Result r, IReadOnlyList<AppraisalFact> facts)
    {
        if (!r.CanSimulate || r.Appraisal is null) return "尚未开炉，无法鉴定。";
        var lines = new List<string>();
        if (r.Exploded) lines.Add("本炉阴阳相激未能受控，发生炸炉，未能成丹。");
        else if (!r.Appraisal.TemplateMatched) lines.Add("本炉药性尚未对应已知丹种，未能成丹。");
        else if (r.OrdinaryFailure)
        {
            lines.Add($"本炉鉴定为{PillConcept(r)}，未能成丹。");
            foreach (var f in facts.Where(f => f.Importance == DiagnosisImportance.DirectFailure))
                lines.Add($"{Problem(f.Kind)}，未达到成丹要求。");
        }
        else if (r.Quality == 100) lines.Add($"本炉炼成{PillConcept(r)}，丹效达到无暇。");
        else
        {
            lines.Add($"本炉炼成{PillConcept(r)}。");
            foreach (var f in facts.Where(f => f.Importance == DiagnosisImportance.QualityLimit))
                lines.Add(f.Kind switch
                {
                    DiagnosisKind.Core => "「君药」核心药性的发挥限制了本炉丹效。",
                    DiagnosisKind.Polarity => "阴阳偏性与丹药所需仍有差异，限制了本炉丹效。",
                    DiagnosisKind.Connection => "各味药性的相合程度限制了本炉丹效。",
                    DiagnosisKind.Solid => "丹体的稳形支持限制了本炉丹效。",
                    DiagnosisKind.Purity => "残余夹带影响了纯度，限制了本炉丹效。",
                    _ => "有效材料之间的互补程度限制了本炉丹效。"
                });
        }
        if (facts.Any(f => f.Kind == DiagnosisKind.CoreAdequate))
            lines.Add("「君药」核心药性已达到成丹要求，并非本次失败主要原因。");
        if (r.ZInitial <= AppraisalEpsilon) lines.Add("炉内未出现阴阳相激，也未发生炸炉。");
        return string.Join("\n", lines);
    }

    private static string UtilityObservation(Result r, string utility)
    {
        var status = AppraisalUtilityStatus(r, utility);
        string label = AppraisalConcept(utility);
        string purpose = utility == "明" ? "减少杂质夹带" : "进一步提取残余中的药性";
        return status switch
        {
            UtilityStatus.Absent => $"本炉没有{label}来{purpose}。",
            // The material-specific misplaced fact below explains this once, with the actual material and slot.
            UtilityStatus.Misplaced => "",
            UtilityStatus.Effective => utility == "明" ? $"本炉{label}已减少杂质夹带。" : $"本炉{label}已从残余中追加提取药性。",
            UtilityStatus.Participated => $"本炉{label}已在对应药位参与炼制，但未{purpose}。",
            _ => ""
        };
    }

    private static IEnumerable<string> FormatAnalysis(Result r, IReadOnlyList<AppraisalFact> facts)
    {
        var a = r.Appraisal!;
        bool Focus(DiagnosisKind kind) => facts.Any(f => f.Kind == kind && IsPriority(f));
        foreach (var f in facts)
        {
            switch (f.Kind)
            {
                case DiagnosisKind.UnknownPill:
                    yield return $"{MaterialConcepts(f.Subjects)}作为「君药」，尚不能据其药性鉴定丹种。";
                    break;
                case DiagnosisKind.CoreSource:
                    yield return $"{MaterialConcepts(f.Subjects)}置于「君」位，以{Concepts(f.Utilities)}承担炼制{PillConcept(r)}所需的核心药性。";
                    break;
                case DiagnosisKind.Residual:
                    var row = f.Subjects[0];
                    // A ratio of the material's own raw amount, never its rank within this batch.
                    string degree = f.Evidence["share"] + AppraisalEpsilon >= AppraisalResidualManyShare ? "较多" : "部分";
                    yield return $"{AppraisalConcept(row.Name)}质地{AppraisalConcept(row.Texture)}，提取后仍留下{degree}残余。";
                    break;
                case DiagnosisKind.Extraction:
                    if (Focus(DiagnosisKind.Purity) || f.Evidence["break"] + f.Evidence["convert"] > AppraisalEpsilon)
                    {
                        foreach (string utility in f.Utilities)
                        {
                            if (!Focus(DiagnosisKind.Purity) && AppraisalUtilityStatus(r, utility) != UtilityStatus.Effective) continue;
                            // Break has no target when no dense residual was ever present.
                            if (utility == "破" && !a.Materials.Any(m => m.Texture == "致密" && m.Residual + m.BreakExtracted > AppraisalEpsilon)) continue;
                            string observation = UtilityObservation(r, utility);
                            if (observation.Length > 0) yield return observation;
                        }
                    }
                    break;
                case DiagnosisKind.Clarifying:
                    if (Focus(DiagnosisKind.Purity) || f.Evidence["reduction"] > AppraisalEpsilon)
                    {
                        yield return UtilityObservation(r, "明");
                        if (Focus(DiagnosisKind.Purity)) yield return r.PurityQuality < 20
                            ? "纯度仍未达到成丹要求。" : "剩余的杂质夹带仍影响本炉丹效。";
                    }
                    break;
                case DiagnosisKind.Misplaced:
                    yield return $"{MaterialConcepts(f.Subjects)}携带的{Concepts(f.Utilities)}功用在{RoleConcept(f.Subjects[0].Role)}无法发挥。";
                    break;
                case DiagnosisKind.PolarityObservation:
                    string polarity = f.Evidence["balance"] > AppraisalEpsilon ? "偏阳" : f.Evidence["balance"] < -AppraisalEpsilon ? "偏阴" : "平和";
                    yield return f.Evidence["score"] < 20
                        ? $"本炉整体{polarity}，不符合{PillConcept(r)}所需的阴阳偏性。"
                        : $"本炉整体{polarity}，仍在{PillConcept(r)}可成丹的偏性范围内。";
                    if (f.Evidence["score"] is >= 20 and < 100 && !Focus(DiagnosisKind.Polarity))
                        yield return r.OrdinaryFailure ? "阴阳偏性影响了配伍表现，但并非本次失败的直接原因。" : "阴阳偏性仍有改善空间，但不是本炉丹效的主要限制。";
                    break;
                case DiagnosisKind.Conflict:
                    if (f.Evidence["initial"] > AppraisalEpsilon && !r.Exploded)
                        yield return "炉内存在阴阳相激，但未发生炸炉。";
                    break;
                case DiagnosisKind.Calming:
                    yield return $"{MaterialConcepts(f.Subjects)}在「佐」位发挥的「镇」已减轻阴阳相激。";
                    break;
                case DiagnosisKind.Guarding:
                    yield return $"{MaterialConcepts(f.Subjects)}在「佐」位发挥的「护」已降低炸炉危险，但不改善药性相合，也不追回已经损失的药性。";
                    break;
                case DiagnosisKind.Delivery:
                    if (r.Quality == 100) break;
                    if (f.Evidence["mixingLoss"] > AppraisalEpsilon) yield return "提取出的药性仍有部分未能相合，这部分不是未提取的残余。";
                    if (f.Evidence["deliveryLoss"] > AppraisalEpsilon) yield return "已经相合的药性仍有部分未能送入丹胚，这部分也不属于杂质夹带。";
                    if (r.OrdinaryFailure && r.CoreQuality >= 20 && r.ConnectionQuality >= 20)
                        yield return "药性相合与送达仍有改善空间，但未直接阻止本炉成丹。";
                    break;
                case DiagnosisKind.SolidSupport:
                    yield return f.Evidence["solidPower"] + f.Evidence["sealPower"] <= AppraisalEpsilon
                        ? "丹体未得到「固」或「佐」位「藏」的稳形支持。"
                        : "「固」或「佐」位「藏」已提供稳形支持，但丹体稳定仍有改善空间。";
                    if (r.OrdinaryFailure && a.SolidQuality >= 20) yield return "稳形表现并非本次失败的直接原因。";
                    break;
            }
        }
        if (r.Exploded && facts.Any(f => f.Kind is DiagnosisKind.Calming or DiagnosisKind.Guarding))
            yield return "这些处理已起作用，但仍未控制住本炉冲突。";
        if (r.OutputCount == 0 && a.TemplateMatched)
            yield return "上述提取、相合与夹带情况仅用于解释炼制过程，本炉没有获得成品丹药。";
    }

    private static IEnumerable<string> FormatSuggestions(Result r, IReadOnlyList<AppraisalFact> facts)
    {
        if (r.Quality == 100 && r.OutputCount > 0) { yield return "可保留本次配伍作为参考，无需围绕丹效继续补强。"; yield break; }
        var a = r.Appraisal!;
        var priority = facts.Where(IsPriority).Select(f => f.Advice).Distinct().ToArray();
        foreach (var advice in priority)
        {
            switch (advice)
            {
                case AdviceKind.Conflict:
                    yield return AppraisalUtilityStatus(r, "镇") == UtilityStatus.Effective
                        ? "应优先控制阴阳相激。可尝试调整阴阳材料的组合，或重新安排现有「镇」的支持。"
                        : "应优先控制阴阳相激。可尝试调整阴阳材料的组合，或考虑在「佐」位发挥「镇」。";
                    yield return AppraisalUtilityStatus(r, "护") == UtilityStatus.Effective
                        ? "现有「护」已抵挡部分危险，但仍不足以避免炸炉；可尝试调整其配伍支持。"
                        : "也可考虑「佐」位「护」来降低危险，但它不能改善药性相合。";
                    break;
                case AdviceKind.Identify:
                    yield return "可先核对「君药」提供的核心药性是否对应已知丹种，再调整其余药材。";
                    break;
                case AdviceKind.Purity:
                    yield return "应优先处理杂质夹带问题。";
                    if (a.Materials.Sum(m => m.Residual) <= AppraisalEpsilon) break;
                    foreach (var utility in new[] { "明", "化", "破" })
                    {
                        var dense = a.Materials.Where(m => m.Texture == "致密" && m.Residual > AppraisalEpsilon).ToArray();
                        if (utility == "破" && dense.Length == 0) continue;
                        var status = AppraisalUtilityStatus(r, utility);
                        string q = AppraisalConcept(utility);
                        if (status == UtilityStatus.Effective)
                            yield return $"现有{q}已起作用，可尝试调整其配伍支持，或选择其他处理方向。";
                        else if (status == UtilityStatus.Participated)
                            yield return $"现有{q}虽已参与炼制，但未产生相应处理效果，可先核对其作用对象。";
                        else
                            yield return utility == "明" ? "可考虑在「佐」位发挥「明」，减少残余夹带。"
                                : utility == "化" ? "也可考虑在「佐」位发挥「化」，进一步提取残余中的药性。"
                                : $"「破」可针对{MaterialConcepts(dense)}这类仍有残余的「致密」材料发挥作用。";
                    }
                    break;
                case AdviceKind.Core:
                    yield return "可尝试调整「君药」的选择、用量或药性送达，让核心药性得到更合适的发挥。";
                    break;
                case AdviceKind.Polarity:
                    yield return "可尝试调整材料的阴阳配比，使整体偏性贴合丹药所需；「镇」不能代替这项调整。";
                    break;
                case AdviceKind.Solid:
                    yield return a.SolidPower + a.SealPower > AppraisalEpsilon
                        ? "可尝试调整现有「固」或「佐」位「藏」的稳形支持。"
                        : "可考虑「臣」位或「佐」位「固」，也可考虑「佐」位「藏」提供稳形支持。";
                    break;
                case AdviceKind.Connection:
                    yield return AppraisalUtilityStatus(r, "通") == UtilityStatus.Effective
                        ? "可尝试调整现有「使」位「通」的配伍支持，改善各味药性的相合。"
                        : "可考虑在「使」位发挥「通」，改善各味药性的相合。";
                    break;
                case AdviceKind.Complement:
                    yield return "可尝试让材料在药性与功用上形成实际互补；只增加没有有效贡献的药材，不能改善这项限制。";
                    break;
            }
        }
        // Placement advice is secondary, never a promise that rearranging will fix the primary issue.
        if (!r.Exploded && a.TemplateMatched && priority.Length > 0)
        {
            var misplaced = facts.FirstOrDefault(f => f.Kind == DiagnosisKind.Misplaced && f.Subjects[0].Role != SlotRole.Jun);
            if (misplaced is not null)
            {
                var choices = misplaced.Utilities.Select(u =>
                {
                    var roles = new[] { SlotRole.Chen, SlotRole.Zuo, SlotRole.Shi }.Where(role => CanFunctionAct(role, u)).ToArray();
                    if (roles.Length > 0) return $"置于{string.Join("或", roles.Select(RoleConcept))}可发挥{AppraisalConcept(u)}";
                    return AlchemyRecipeCatalog.Pills.Any(p => p.CoreUtility == u)
                        ? $"置于「君」位可提供{AppraisalConcept(u)}核心药性" : "";
                }).Where(s => s.Length > 0).ToArray();
                if (choices.Length > 0)
                {
                    yield return $"若保留{MaterialConcepts(misplaced.Subjects)}，可根据需要重新安排药位：{string.Join("；", choices)}。";
                    yield return "移位会改变功用的发挥，并不保证同时解决本炉的主要问题。";
                    if (choices.Any(s => s.StartsWith("置于「君」位"))) yield return "调整「君药」还可能改变丹种。";
                }
            }
        }
        yield return "建议围绕主要问题逐步调整配伍，再观察结果，避免同时改动过多而难以判断变化来源。";
    }

    private static string FormatIntuitiveAppraisal(Result r, IReadOnlyList<AppraisalFact> facts)
    {
        if (!r.CanSimulate || r.Appraisal is null) return "尚未开炉，无法鉴定。";
        return "【鉴定结论】\n" + FormatAppraisalConclusion(r, facts)
            + "\n\n【配伍分析】\n" + string.Join("\n\n", FormatAnalysis(r, facts).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct())
            + "\n\n【调整建议】\n" + string.Join("\n\n", FormatSuggestions(r, facts).Distinct());
    }
}
