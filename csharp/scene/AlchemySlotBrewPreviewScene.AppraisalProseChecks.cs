using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using K = AlchemySlotSimulation.DiagnosisKind;
using I = AlchemySlotSimulation.DiagnosisImportance;
using U = AlchemySlotSimulation.UtilityStatus;
using R = AlchemySlotSimulation.SlotRole;

public partial class AlchemySlotBrewPreviewScene
{
    private int RunAppraisalProseChecks(IEnumerable<(string Name, AlchemySlotSimulation.Result Result)> originals,
        AlchemySlotSimulation.Result capped)
    {
        int checks = 0;
        void Verify(bool ok, string label)
        {
            if (!ok) throw new InvalidOperationException("prose: " + label);
            checks++;
        }
        var corpus = originals.Where(c => c.Result.CanSimulate).ToList();
        // Fixed diagnostic coverage matrix, not an optimizer: repeat catalogue pairs at entry attainment.
        foreach (var c in AppraisalCases().Where(c => c.Name.StartsWith("pair/")))
        {
            var result = AlchemySlotSimulation.Simulate(c.Inputs, 8);
            if (result.CanSimulate) corpus.Add((c.Name + "/entry", result));
        }
        corpus.Add(("material-cap", capped));
        // Analytically chosen boundary, not a recipe search: core and default solid support both equal 45.
        var tiedMain = new MaterialCraftData { texture = "致密", element = "金" };
        tiedMain.utilities.Add(new MaterialUtilityGrade { utility_id = "生", grade = 3 });
        var tiedZuo = new MaterialCraftData { texture = "致密", element = "金" };
        tiedZuo.utilities.Add(new MaterialUtilityGrade { utility_id = "明", grade = 1 });
        double tiedAttainment = (8 + 10 * Math.Pow(25d / 80, 1.2)) * 68 / (31.5 * 2.9 * .7625);
        var tiedSimulation = AlchemySlotSimulation.Simulate(new[] {
            new AlchemySlotSimulation.Input("并列核心材", "prose/tie-jun", tiedMain, 1, R.Jun),
            new AlchemySlotSimulation.Input("并列提纯材", "prose/tie-zuo", tiedZuo, 1, R.Zuo) }, tiedAttainment);
        Verify(tiedSimulation.OutputCount > 0 && AlchemySlotSimulation.DiagnoseAppraisal(tiedSimulation).Count(f => f.Importance == I.QualityLimit) == 2,
            "simulated tied success");
        corpus.Add(("tied-success-simulation", tiedSimulation));
        foreach (string processing in new[] { "明", "化" })
        {
            var mainCraft = new MaterialCraftData { texture = "致密", element = "木" };
            mainCraft.utilities.Add(new MaterialUtilityGrade { utility_id = "生", grade = 1 });
            var processingCraft = new MaterialCraftData { texture = "致密", element = "木" };
            processingCraft.utilities.Add(new MaterialUtilityGrade { utility_id = processing, grade = 3 });
            var partiallyProcessed = AlchemySlotSimulation.Simulate(new[] {
                new AlchemySlotSimulation.Input("核心材", "prose/partial-jun", mainCraft, 1, R.Jun),
                new AlchemySlotSimulation.Input("处理材", "prose/partial-zuo", processingCraft, 1, R.Zuo) }, 8);
            Verify(partiallyProcessed.CanSimulate && partiallyProcessed.PurityQuality < 20 &&
                AlchemySlotSimulation.AppraisalUtilityStatus(partiallyProcessed, processing) == U.Effective, "simulated partial processing " + processing);
            corpus.Add(("partial-processing-simulation/" + processing, partiallyProcessed));
        }
        var forbidden = new Regex(@"[0-9０-９%％=×≥≤→]|评分|百分比|倍率|阈值|公式|造诣|炉次|模板|最低|质量守恒|字段|调试|未配置|库存|res://|user://|分支|向下取整|百分之|零点|第.+炉|「[君臣佐使]药」位");
        var coverage = new HashSet<string>();
        foreach (var c in corpus)
        {
            var r = c.Result;
            var a = r.Appraisal!;
            var facts = AlchemySlotSimulation.DiagnoseAppraisal(r);
            string report = AlchemySlotSimulation.BuildAppraisalReport(r);
            Verify(!forbidden.IsMatch(report), "internal or numerical leak " + c.Name);
            Verify(Regex.Matches(report, "【鉴定结论】|【配伍分析】|【调整建议】").Count == 3 && report.StartsWith("【鉴定结论】"), "three sections");
            Verify(report.Count(x => x == '「') == report.Count(x => x == '」') && !report.Contains("「「"), "concept quotes");
            Verify(!Regex.IsMatch(report, "必定|必然|一定成功|保证成丹|保证能|确保成丹"), "promised success");
            Verify(!report.Contains("置于可发挥"), "invalid placement suggestion");
            Verify(facts.All(f => f.Evidence.Count > 0 && f.Subjects.All(s => a.Materials.Contains(s))), "missing factual evidence");
            Verify(!report.Contains("「佐药」位") && !report.Contains("「君药」位"), "role vs responsibility");
            if (!a.TemplateMatched)
            {
                Verify(!facts.Any(f => f.Kind is K.Core or K.Polarity or K.Connection or K.Solid or K.Purity or K.Complement), "unknown quality attribution");
                coverage.Add("unrecognized");
            }
            else if (r.OrdinaryFailure)
            {
                int failedAxes = AlchemySlotSimulation.AppraisalAxes(r).Count(x => x.Score < 20);
                Verify(facts.Count(f => f.Importance == I.DirectFailure) == failedAxes, "all direct failures");
                coverage.Add(failedAxes == 1 ? "single-failure" : "multiple-failure");
            }
            else if (r.OutputCount > 0)
            {
                var axes = AlchemySlotSimulation.AppraisalAxes(r);
                double min = axes.Min(x => x.Score);
                int limits = min >= 100 - 1e-9 ? 0 : axes.Count(x => Math.Abs(x.Score - min) < 1e-9);
                Verify(facts.Count(f => f.Importance == I.QualityLimit) == limits, "all quality limits");
                coverage.Add(limits > 1 ? "tied-success" : "single-success");
            }
            if (a.TemplateMatched && r.PolarityQuality < 20 && r.ZInitial == 0) coverage.Add("polarity-without-conflict");
            if (r.ZInitial > 0 && !r.Exploded) coverage.Add("conflict-without-explosion");
            if (r.Exploded) coverage.Add("explosion");
            if (facts.Any(f => f.Kind == K.Calming))
            {
                Verify(facts.Where(f => f.Kind == K.Calming).All(f => f.Subjects.Count > 0 && f.Subjects.All(s => s.Role == R.Zuo) && f.Evidence["reduction"] > 0), "calming provider/effect");
                coverage.Add("calming-effective");
            }
            if (facts.Any(f => f.Kind == K.Guarding))
            {
                Verify(facts.Where(f => f.Kind == K.Guarding).All(f => f.Subjects.Count > 0 && f.Subjects.All(s => s.Role == R.Zuo) && f.Evidence["reduction"] > 0), "guarding provider/effect");
                Verify(report.Contains("不改善药性相合"), "guarding must not improve mixing");
                coverage.Add("guarding-effective");
            }
            foreach (var f in facts.Where(f => f.Kind == K.Misplaced))
                Verify(f.Subjects.All(s => f.Utilities.All(u => s.Functions.Any(x => x.Utility == u && !x.RoleAllowed))), "wrong placement evidence");
            if (facts.Any(f => f.Kind == K.Misplaced)) coverage.Add("misplaced");
            if (facts.Any(f => f.Kind == K.Complement && f.Importance == I.QualityLimit)) coverage.Add("complement-limit");
            if (AlchemySlotSimulation.AppraisalUtilityStatus(r, "明") == U.Effective)
            {
                Verify(a.ClarifiedMass > 0, "clarify effect evidence");
                if (r.PurityQuality < 20 && a.TemplateMatched)
                {
                    Verify(report.Contains("「明」已减少杂质夹带") && report.Contains("纯度仍未达到成丹要求"), "clarify helped but insufficient");
                    Verify(!report.Contains("本炉没有「明」"), "contradictory clarify state");
                    coverage.Add("clarify-insufficient");
                }
            }
            if (AlchemySlotSimulation.AppraisalUtilityStatus(r, "化") == U.Effective)
            {
                Verify(a.Materials.Sum(m => m.ConvertExtracted) > 0, "conversion actual extraction");
                if (r.PurityQuality < 20 && a.TemplateMatched) coverage.Add("extraction-insufficient");
            }
            if (a.TemplateMatched && AlchemySlotSimulation.AppraisalUtilityStatus(r, "明") == U.Absent && r.PurityQuality < 20)
                coverage.Add("untreated-purity");
            if (a.TemplateMatched && a.Materials.Sum(m => m.Residual) <= 1e-9)
            {
                Verify(!report.Contains("本炉没有「明」") && !report.Contains("本炉没有「化」") && !report.Contains("本炉没有「破」"), "nonexistent residual defect");
                string suggestions = report.Split("【调整建议】")[1];
                Verify(!suggestions.Contains("「明」") && !suggestions.Contains("「化」") && !suggestions.Contains("「破」"), "zero residual advice");
                coverage.Add("zero-residual");
            }
        }

        // Simulation-based edge inputs, not authored report strings. They are isolated from the catalogue.
        MaterialCraftData Craft(params string[] utilities)
        {
            var craft = new MaterialCraftData { texture = "致密", element = "木" };
            foreach (var u in utilities) craft.utilities.Add(new MaterialUtilityGrade { utility_id = u, grade = 4 });
            return craft;
        }
        var zeroInputs = new[] {
            new AlchemySlotSimulation.Input("核心材", "prose/jun", Craft("生"), 1, R.Jun),
            new AlchemySlotSimulation.Input("稳形材", "prose/chen", Craft("固"), 1, R.Chen),
            new AlchemySlotSimulation.Input("提取材", "prose/zuo", Craft("破", "化", "明"), 1, R.Zuo),
            new AlchemySlotSimulation.Input("送达材", "prose/shi", Craft("通", "行"), 1, R.Shi) };
        var zero = AlchemySlotSimulation.Simulate(zeroInputs, 1196);
        Verify(zero.CanSimulate && zero.Appraisal!.Materials.Sum(m => m.Residual) < 1e-9, "zero residual simulated");
        string zeroReport = AlchemySlotSimulation.BuildAppraisalReport(zero);
        Verify(!zeroReport.Contains("本炉没有「明」") && !zeroReport.Split("【调整建议】")[1].Contains("「明」"), "zero residual no invented deficiency");
        Verify(AlchemySlotSimulation.AppraisalUtilityStatus(zero, "明") == U.Participated, "participating clarify with no effect");
        Verify(zero.Quality == 50 && zero.EffectiveMaterialCount == 1 &&
            AlchemySlotSimulation.AppraisalAxes(zero).Where(x => x.Name != "有效材料数上限").All(x => x.Score == 100),
            "existing saturated-axis material-count behavior must remain unchanged");
        GD.Print("APPRAISAL_EXISTING_BOUNDARY all_uncapped_scores=100 effective_materials=1 final_quality=50; flawless display tested separately");
        coverage.Add("zero-residual"); coverage.Add("participated-no-effect");
        var states = corpus.SelectMany(c => new[] { "破", "化", "明" }.Select(u => AlchemySlotSimulation.AppraisalUtilityStatus(c.Result, u))).ToHashSet();
        states.Add(AlchemySlotSimulation.AppraisalUtilityStatus(zero, "明"));
        Verify(Enum.GetValues<U>().All(states.Contains), "all distinct utility states");
        // Full-quality display boundary only; do not present this modified result as a real obtainable recipe.
        var flawless = zero with { Quality = 100, OutputCount = 1, OrdinaryFailure = false, CoreQuality = 100,
            PolarityQuality = 100, ConnectionQuality = 100, PurityQuality = 100, StabilityQuality = 100,
            Appraisal = zero.Appraisal! with { SolidQuality = 100, MaterialCap = 100 } };
        string perfectReport = AlchemySlotSimulation.BuildAppraisalReport(flawless);
        Verify(!AlchemySlotSimulation.DiagnoseAppraisal(flawless).Any(f => f.Importance != I.Observation), "flawless no shortfalls");
        Verify(perfectReport.Contains("丹效达到无暇") && !perfectReport.Contains("限制了本炉丹效") &&
            !perfectReport.Split("【调整建议】")[1].Contains("可考虑"), "flawless no baseless strengthening");
        coverage.Add("flawless-boundary");
        Verify(AlchemySlotSimulation.AppraisalConcept("「木」") == "「木」" && AlchemySlotSimulation.AppraisalConcept("res://secret.tres") == "「未知」", "concept normalization");

        GD.Print("APPRAISAL_PROSE_COVERAGE " + string.Join(",", coverage.Order()));
        foreach (string expected in new[] { "single-failure", "multiple-failure", "single-success", "tied-success", "explosion", "unrecognized",
            "polarity-without-conflict", "conflict-without-explosion", "calming-effective", "guarding-effective", "untreated-purity",
            "clarify-insufficient", "extraction-insufficient", "misplaced", "zero-residual", "complement-limit", "participated-no-effect", "flawless-boundary" })
            Verify(coverage.Contains(expected), "missing real diagnostic coverage " + expected);
        GD.Print($"APPRAISAL_PROSE_PASS cases={corpus.Count} assertions={checks} coverage={string.Join(",", coverage.Order())}");
        return checks;
    }
}
