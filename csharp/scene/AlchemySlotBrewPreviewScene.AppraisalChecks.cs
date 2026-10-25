using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

public partial class AlchemySlotBrewPreviewScene
{
    private IEnumerable<(string Name, AlchemySlotSimulation.Input[] Inputs, double Attainment)> AppraisalCases()
    {
        foreach (var pill in AlchemyRecipeCatalog.Pills)
        foreach (var route in pill.SampleRoutes)
        foreach (double attainment in new[] { 8d, 36d, 100d })
            yield return ($"{route.Name}/{attainment}", route.Ingredients.Select(i =>
            {
                var m = _materials.Single(m => m.Name == i.HerbName);
                return new AlchemySlotSimulation.Input(m.Name, m.Path, m.Craft, i.Dose, RoleFor(i.AssistantSlot));
            }).ToArray(), attainment);
        // Real catalogue materials: all ordered pairs, including unrecognized cores and explosive mixes.
        foreach (var main in _materials)
        foreach (var other in _materials.Where(m => m != main))
            yield return ($"pair/{main.Name}/{other.Name}", new[]
            {
                new AlchemySlotSimulation.Input(main.Name, main.Path, main.Craft, 1, AlchemySlotSimulation.SlotRole.Jun),
                new AlchemySlotSimulation.Input(other.Name, other.Path, other.Craft, 1, AlchemySlotSimulation.SlotRole.Zuo)
            }, 100d);
    }

    private static string SettlementSignature(AlchemySlotSimulation.Result r) => JsonSerializer.Serialize(new
    {
        r.CanSimulate, r.BlockingReason, r.Exploded, r.OrdinaryFailure, r.CandidateCount, r.OutputCount,
        r.Quality, r.QualityName, r.SpiritNeed, r.EnergyNeed, r.EffectiveMaterialCount, r.CoreQuality,
        r.PolarityQuality, r.ConnectionQuality, r.StabilityQuality, r.PurityQuality, r.ZInitial,
        r.ZAfterCalming, r.ExplosionRisk, r.ConnectionRate, r.MixingRate, r.DeliveryRate,
        r.FiveElementRate, r.YinValue, r.YinDeviation, r.CarryRate, r.StorageMultiplier,
        r.YieldRange, r.Outcome, r.ProductName, r.CoreUtility
    });

    private void WriteAppraisalBaseline()
    {
        try
        {
            var rows = AppraisalCases().ToDictionary(c => c.Name,
                c => SettlementSignature(AlchemySlotSimulation.Simulate(c.Inputs, c.Attainment)));
            System.IO.File.WriteAllText(ProjectSettings.GlobalizePath("res://outputs/alchemy_appraisal_settlements_before.json"),
                JsonSerializer.Serialize(rows));
            GD.Print($"APPRAISAL_BASELINE_PASS {rows.Count}");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private void LoadAppraisalInputs(AlchemySlotSimulation.Input[] inputs, double attainment)
    {
        _messageOverlay.Hide();
        _attainmentInput.Value = attainment;
        ClearSelections();
        foreach (var input in inputs)
        {
            var material = _materials.Single(m => m.Path == input.Path);
            if (input.IsMain) { _main = material; _mainDose = input.Dose; continue; }
            int slot = Enumerable.Range(0, 7).First(i => RoleFor(i) == input.Role && _assistants[i] is null);
            _assistants[slot] = material;
            _assistantDoses[slot] = input.Dose;
        }
        RefreshPage();
    }

    private async Task ClickAppraisalControl(Button button)
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var point = button.GetGlobalRect().GetCenter();
        GetViewport().PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point }, true);
        GetViewport().PushInput(new InputEventMouseButton { Position = point, GlobalPosition = point,
            ButtonIndex = MouseButton.Left, Pressed = true }, true);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GetViewport().PushInput(new InputEventMouseButton { Position = point, GlobalPosition = point,
            ButtonIndex = MouseButton.Left, Pressed = false }, true);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private async Task RunAppraisalChecks()
    {
        try
        {
            _tutorialOverlay.Hide();
            int checks = 0;
            void Verify(bool ok, string label)
            {
                if (!ok) throw new InvalidOperationException(label);
                checks++;
                // Per-case checks are summarized to keep the output bounded.
            }
            bool Near(double a, double b) => Math.Abs(a - b) < 1e-8;
            var baseline = JsonSerializer.Deserialize<Dictionary<string, string>>(System.IO.File.ReadAllText(
                ProjectSettings.GlobalizePath("res://outputs/alchemy_appraisal_settlements_before.json")))!;
            var cases = AppraisalCases().ToArray();
            Verify(cases.Length == baseline.Count, "baseline case count");
            var evaluated = cases.Select(c => (Case: c, Result: AlchemySlotSimulation.Simulate(c.Inputs, c.Attainment))).ToArray();
            int matched = 0, unknown = 0, exploded = 0, failures = 0, successes = 0;
            foreach (var entry in evaluated)
            {
                var r = entry.Result;
                Verify(baseline[entry.Case.Name] == SettlementSignature(r), "settlement changed: " + entry.Case.Name);
                if (!r.CanSimulate) { Verify(r.Appraisal is null, "blocked report"); continue; }
                var a = r.Appraisal!;
                Verify(a is not null, "missing snapshot");
                foreach (var row in a!.Materials)
                {
                    Verify(Near(row.Raw, row.Available + row.Residual), "raw conservation");
                    Verify(Near(row.Available, row.BaseExtracted + row.BreakExtracted + row.ConvertExtracted), "extraction conservation");
                    Verify(Near(row.Available, row.Delivered + row.MixingLoss + row.DeliveryLoss), "delivery conservation");
                    Verify(row.BreakExtracted >= -1e-8 && row.ConvertExtracted >= -1e-8 && row.Residual >= -1e-8, "negative material ledger");
                }
                if (a.TemplateMatched)
                {
                    matched++;
                    Verify(Near(r.StabilityQuality, Math.Min(a.SolidQuality!.Value, r.PurityQuality)), "solid vs stability");
                    Verify(Near(r.ZAfterCalming, Math.Max(0, r.ZInitial - 10 * a.CalmingPower!.Value)), "actual calming");
                    Verify(Near(r.ExplosionRisk, Math.Max(0, r.ZAfterCalming - 5 * a.GuardingPower!.Value)), "actual guarding");
                    Verify(Near(r.MixingRate, r.ConnectionRate * (1 - .2 * r.ZAfterCalming / 100)), "guard must not alter mixing");
                    Verify(r.Quality == (int)Math.Floor(AlchemySlotSimulation.AppraisalAxes(r).Min(x => x.Score)), "floor and limits");
                    double delivered = a.Materials.Sum(x => x.Delivered), carried = a.Materials.Sum(x => x.Carried!.Value);
                    Verify(Near(a.Contamination!.Value, delivered + carried <= 0 ? 1 : carried / (delivered + carried)), "contamination");
                    Verify(Near(a.Materials.Sum(x => x.Raw), delivered + carried + a.Materials.Sum(x => x.MixingLoss + x.DeliveryLoss + x.Residual - x.Carried!.Value)), "whole batch conservation");
                }
                else
                {
                    unknown++;
                    Verify(a.SolidQuality is null && a.Contamination is null && a.MaterialCap is null &&
                        AlchemySlotSimulation.AppraisalAxes(r).Count == 0, "unknown fabricated quality");
                    Verify(AlchemySlotSimulation.DiagnoseAppraisal(r).Any(f => f.Kind == AlchemySlotSimulation.DiagnosisKind.UnknownPill) &&
                        !AlchemySlotSimulation.DiagnoseAppraisal(r).Any(f => f.Kind is AlchemySlotSimulation.DiagnosisKind.Core or
                            AlchemySlotSimulation.DiagnosisKind.Purity or AlchemySlotSimulation.DiagnosisKind.Polarity), "unknown scope");
                }
                if (r.Exploded) exploded++;
                else if (r.OrdinaryFailure) failures++;
                else successes++;
            }
            GD.Print($"APPRAISAL_SETTLEMENTS_PASS {evaluated.Length} matched={matched} unknown={unknown} success={successes} failure={failures} explosion={exploded}");

            // A deliberately small test material set exercises actual break -> conversion -> carry accounting.
            MaterialCraftData Craft(string texture, string element, params string[] utilities)
            {
                var craft = new MaterialCraftData { texture = texture, element = element };
                foreach (var utility in utilities) craft.utilities.Add(new MaterialUtilityGrade { utility_id = utility, grade = 1 });
                return craft;
            }
            var extractionInputs = new[] {
                new AlchemySlotSimulation.Input("致密核心", "check/jun", Craft("致密", "木", "生"), 2, AlchemySlotSimulation.SlotRole.Jun),
                new AlchemySlotSimulation.Input("臣", "check/chen", Craft("致密", "木", "固"), 1, AlchemySlotSimulation.SlotRole.Chen),
                new AlchemySlotSimulation.Input("佐", "check/zuo", Craft("致密", "土", "破", "化", "明"), 1, AlchemySlotSimulation.SlotRole.Zuo),
                new AlchemySlotSimulation.Input("使", "check/shi", Craft("致密", "水", "通", "行"), 1, AlchemySlotSimulation.SlotRole.Shi) };
            var extraction = AlchemySlotSimulation.Simulate(extractionInputs, 100);
            var ledger = extraction.Appraisal!;
            Verify(extraction.CanSimulate && Near(ledger.Materials.Sum(x => x.BaseExtracted), 375) &&
                Near(ledger.Materials.Sum(x => x.BreakExtracted), 25) && Near(ledger.Materials.Sum(x => x.ConvertExtracted), 25) &&
                Near(ledger.Materials.Sum(x => x.Residual), 75) && Near(ledger.Materials.Sum(x => x.Carried!.Value), 3.75), "actual break/convert/clarify amounts");
            var originalReport = AlchemySlotSimulation.BuildAppraisalReport(extraction);
            extractionInputs[0].Craft.texture = "流质";
            extractionInputs[0].Craft.utilities.Clear();
            Verify(originalReport == AlchemySlotSimulation.BuildAppraisalReport(extraction), "mutable resource leaked into snapshot");
            GD.Print("APPRAISAL_EXTRACTION_PASS raw=500 base=375 break=25 convert=25 available=425 residual=75 carried=3.75");

            var tied = extraction with { CoreQuality = 50, PolarityQuality = 50, ConnectionQuality = 50,
                PurityQuality = 50, StabilityQuality = 50, Quality = 50, OrdinaryFailure = false, OutputCount = 1,
                Appraisal = ledger with { SolidQuality = 50, MaterialCap = 50 } };
            Verify(AlchemySlotSimulation.DiagnoseAppraisal(tied).Count(f => f.Importance == AlchemySlotSimulation.DiagnosisImportance.QualityLimit) ==
                AlchemySlotSimulation.AppraisalAxes(tied).Count, "all tied bottlenecks");
            var actualTie = evaluated.First(e => e.Result.Appraisal?.TemplateMatched == true &&
                AlchemySlotSimulation.AppraisalAxes(e.Result).Count(x => Math.Abs(x.Score - AlchemySlotSimulation.AppraisalAxes(e.Result).Min(y => y.Score)) < 1e-9) >= 2);
            var tieMin = AlchemySlotSimulation.AppraisalAxes(actualTie.Result).Min(x => x.Score);
            Verify(AlchemySlotSimulation.DiagnoseAppraisal(actualTie.Result).Count(f => f.Importance == AlchemySlotSimulation.DiagnosisImportance.DirectFailure) ==
                AlchemySlotSimulation.AppraisalAxes(actualTie.Result).Count(x => x.Score < 20), "actual tied axes");
            var cappedInputs = new[] { ("泥脉石团", 2, -1), ("山蚕长丝", 1, 0), ("月露菌汁", 1, 1), ("回泉珠", 2, 2), ("风纹石英", 2, 3) }
                .Select(i => { var m = _materials.Single(m => m.Name == i.Item1); return new AlchemySlotSimulation.Input(m.Name, m.Path, m.Craft, i.Item2, RoleFor(i.Item3)); }).ToArray();
            var capped = AlchemySlotSimulation.Simulate(cappedInputs, 100);
            Verify(capped.Quality == 99 && capped.Appraisal!.MaterialCap == 99 &&
                AlchemySlotSimulation.DiagnoseAppraisal(capped).Any(f => f.Kind == AlchemySlotSimulation.DiagnosisKind.Complement &&
                    f.Importance == AlchemySlotSimulation.DiagnosisImportance.QualityLimit), "actual material cap bottleneck");
            GD.Print($"APPRAISAL_LIMITS_PASS capped=99 tie={actualTie.Case.Name}");
            checks += RunAppraisalProseChecks(evaluated.Select(e => (e.Case.Name, e.Result)), capped);
            var success = evaluated.First(e => e.Result.OutputCount > 0);
            var failure = evaluated.First(e => e.Result.OrdinaryFailure && e.Result.Appraisal?.TemplateMatched == true);
            var explosion = evaluated.First(e => e.Result.Exploded);
            var noTemplate = evaluated.First(e => e.Result.OrdinaryFailure && e.Result.Appraisal?.TemplateMatched == false);
            foreach (var fixture in new[] { ("success", success), ("failure", failure), ("explosion", explosion), ("unrecognized", noTemplate) })
            {
                LoadAppraisalInputs(fixture.Item2.Case.Inputs, fixture.Item2.Case.Attainment);
                await ClickAppraisalControl(_brewButton);
                Verify(_lastBrew is not null && _appraisalButton.Visible && _messageTitle.Text == "炼丹结果", "real brew button");
                var snapshot = _lastBrew!;
                await ClickAppraisalControl(_appraisalButton);
                Verify(_messageTitle.Text.StartsWith("炼后鉴定") && _appraisalBackButton.Visible && _messageBody.Text.Contains(snapshot.Report), "real appraisal button");
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                Verify(_messageBody.Size.Y <= _messageScroll.Size.Y || _messageScroll.GetVScrollBar().MaxValue > _messageScroll.Size.Y, "long report not scrollable");
                if (fixture.Item1 == "failure") Verify(_messageScroll.GetVScrollBar().MaxValue > _messageScroll.Size.Y, "long failure report must scroll");
                Verify(_messageTitle.Text == "炼后鉴定" && _messageBody.Text == snapshot.Report, "appraisal metadata leaked");
                Verify(_appraisalBackButton.GetGlobalRect().End.Y <= GetViewportRect().End.Y, "button clipped");
                if (OS.GetCmdlineUserArgs().Contains("--alchemy-appraisal-capture"))
                {
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    GetViewport().GetTexture().GetImage().SavePng($"res://outputs/alchemy_intuitive_{fixture.Item1}_20261025.png");
                    _messageScroll.ScrollVertical = (int)_messageScroll.GetVScrollBar().MaxValue;
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    GetViewport().GetTexture().GetImage().SavePng($"res://outputs/alchemy_intuitive_{fixture.Item1}_bottom_20261025.png");
                }
                System.IO.File.WriteAllText(ProjectSettings.GlobalizePath($"res://outputs/alchemy_intuitive_{fixture.Item1}_20261025.txt"), snapshot.Report);
                GD.Print($"APPRAISAL_FIXTURE {fixture.Item1} {fixture.Item2.Case.Name} {AlchemySlotSimulation.AppraisalConclusion(snapshot.Result)}");
                await ClickAppraisalControl(_appraisalBackButton);
                Verify(_messageTitle.Text == "炼丹结果" && _lastBrew == snapshot && _messageBody.Text == snapshot.Summary, "back mutated settlement");
                ClearSelections();
                _attainmentInput.Value = 508;
                Verify(_lastBrew == snapshot && _lastBrew.Report == snapshot.Report, "edit mutated snapshot");
                ShowBrewSnapshot(); // Existing attainment handler closes message overlays.
                await ClickAppraisalControl(_appraisalButton);
                Verify(_lastBrew == snapshot && _messageBody.Text.Contains(snapshot.Report), "edit changed old report");
                _messageOverlay.Hide();
                ShowBrewSnapshot();
                await ClickAppraisalControl(_appraisalButton);
                Verify(_lastBrew == snapshot, "close/reopen changed report");
            }
            int sequence = _brewSequence;
            var old = _lastBrew;
            LoadAppraisalInputs(success.Case.Inputs, success.Case.Attainment);
            await ClickAppraisalControl(_brewButton);
            Verify(_lastBrew != old && _brewSequence == sequence + 1, "new batch reused old report");
            _messageOverlay.Hide();
            ClearSelections();
            var emptyBefore = SettlementSignature(AlchemySlotSimulation.Simulate(GetSimulationInputs(), Attainment));
            ShowBrewSimulation(); // The UI brew button is disabled on preflight failure.
            Verify(_lastBrew is null && !_appraisalButton.Visible && _brewSequence == sequence + 1, "preflight generated report");
            Verify(emptyBefore == SettlementSignature(AlchemySlotSimulation.Simulate(GetSimulationInputs(), Attainment)), "preflight mutated state");
            GD.Print($"ALCHEMY_APPRAISAL_PASS {checks}");
            if (OS.GetCmdlineUserArgs().Contains("--alchemy-appraisal-interactive"))
            {
                LoadAppraisalInputs(success.Case.Inputs, success.Case.Attainment);
                ShowBrewSimulation();
                return; // Leave this isolated preview open for native mouse verification.
            }
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
