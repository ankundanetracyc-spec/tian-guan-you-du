using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using System.Text;

public partial class ProcessForgingScene
{
    private async void AuditProcess()
    {
        try
        {
            int checks = 0;
            void Check(bool ok, string name) { checks++; if (!ok) throw new InvalidOperationException("炼器验收失败：" + name); }
            string Data(object value) => JsonSerializer.Serialize(value);
            void Throws(Action action, string name) { bool threw = false; try { action(); } catch (ArgumentException) { threw = true; } catch (InvalidOperationException) { threw = true; } Check(threw, name); }
            var report = new StringBuilder("# 无燃料炼器试炼运行记录\n\n参数版本：" + _model.Parameters.Version + "。身/质/性/和四组投料，无燃料与供热门槛。全部参数只用于独立测试，不是正式平衡。\n\n");
            void Invariants(ProcessForgingTrial.Evaluation r)
            {
                if (!r.Success) return;
                var a = r.Product!;
                Check(a.Volume > 0 && Math.Abs(a.Length * a.Width * a.Thickness - a.Volume) < 1e-8, "连续体量守恒");
                Check(a.Polarity >= -3 && a.Polarity <= 3, "七档性相合法");
                Check(a.Tier == (a.Calculation.Assessment.IsArtifact ? "法器" : "凡器"), "等级来自实际评阶");
                Check(a.Tier != "凡器" || (a.Effects.All(x => !x.Special) && a.BodyMaximum == 0 && a.BondContributions.Count == 0), "凡器只有本体及基础收益");
                Check(a.BodyMaximum > 0 == a.Effects.Any(x => x.CarrierId == "summon"), "器体仅来自有效御器");
                foreach (var s in a.Sources)
                    Check(Math.Abs(s.BodySupply + s.CharacterSupply + s.ProcessSupply + s.BridgeSpent + s.Loss - s.Supply) < 1e-8, "来源分账守恒");
                Check(a.Sources.All(x => x.Input.Role != "燃料"), "成器来源不存在燃料");
                foreach (var b in a.Calculation.Resources)
                    Check(a.Calculation.Used.GetValueOrDefault(b.Key) <= b.Value + 1e-7 && Math.Abs(a.Calculation.Unused[b.Key] + a.Calculation.Used.GetValueOrDefault(b.Key) - b.Value) < 1e-7, "公共预算不透支：" + b.Key);
                Check(a.Effects.All(x => x.Value > 0 && x.Allocated >= _model.Parameters.Scalars["MinimumEffect"] - 1e-7 && Math.Abs(x.SourceAllocations.Values.Sum() - x.Allocated) < 1e-7), "无零值效果，分配可追溯");
                Check(Data(ProcessForgingTrial.Clone(a)) == Data(a), "器物序列化往返");
                var replay = ProcessForgingTrial.Replay(a).Product!;
                Check(Data(replay.Effects) == Data(a.Effects) && Data(replay.Calculation) == Data(a.Calculation) && replay.InputHash == a.InputHash && replay.Sharpness == a.Sharpness, "脱离目录快照复算");
            }
            for (int i = 0; i < _model.Parameters.Examples.Count; i++)
            {
                var draft = _model.ExampleDraft(i); var r = _model.Evaluate(draft);
                Check(r.CanSubmit && r.Success, "全部无燃料样方正常成器：" + i); Invariants(r);
                report.AppendLine("## " + _model.Parameters.Examples[i].Name + "\n");
                report.AppendLine(string.Join("；", draft.Inputs.Select(x => $"{RoleLabel(x.Role)} {x.Material}×{x.Quantity}")) + "\n");
                report.AppendLine($"造诣{draft.Proficiency}；{r.Units}份材料；{r.Hours}时辰/{r.Energy}试炼精力。\n");
                if (r.Product is { } a)
                {
                    var g = a.Calculation.Assessment;
                    report.AppendLine($"{a.Name} · {a.Tier}；锋锐{a.Sharpness}；{ProcessForgingTrial.PolarityLabel(a.Polarity)}。\n\n内部评阶（不在玩家界面展示）：C灵={g.Capacity:F4}，R灵={g.Maintenance:F4}，T灵={g.Transmission:F4}，S={g.Stability:F6}，H={g.SpiritLevel:F4}。\n");
                    foreach (var e in a.Effects) report.AppendLine($"- {e.Route}：{e.Description}；分配{e.Allocated:F4}；{e.Scope}；{e.Condition}");
                    report.AppendLine($"\n器体{a.BodyMaximum}，在场灵识{a.SpiritRequired}；全部是未装配的测试描述。\n");
                }
                else report.AppendLine("结果：" + string.Join("；", r.Failures) + "\n");
            }
            var basic = _model.ExampleDraft(0); var composite = _model.ExampleDraft(1);
            var normal = _model.Evaluate(basic).Product!; var full = _model.Evaluate(composite).Product!;
            var multi = _model.Evaluate(_model.ExampleDraft(3)).Product!;
            GD.Print("FORGING_MULTI " + string.Join(";", multi.Effects.Select(x => x.CarrierId + "/" + x.AtomId + ":" + x.Value)) + " H=" + multi.Calculation.Assessment.SpiritLevel);
            GD.Print($"FORGING_SAMPLE basic={normal.Tier} H={normal.Calculation.Assessment.SpiritLevel} composite={full.Tier} H={full.Calculation.Assessment.SpiritLevel} S={full.Calculation.Assessment.Stability}");
            Check(normal.Tier == "凡器" && full.Tier == "法器", "真实材料样例覆盖凡器与法器");
            Check(typeof(ProcessForgingTrial.Artifact).GetProperty("Utilities") is null, "成器没有功用字段");
            Check(typeof(ProcessForgingTrial.Preparation).GetProperty("Product") is null && !Data(_model.Inspect(composite)).Contains("Assessment"), "准备信息不含成品或评阶");
            Check(ProcessForgingTrial.Assess(12.5, 12.5, 12.5, .8).IsArtifact, "评阶等号边界");
            Check(!ProcessForgingTrial.Assess(12.49, 12.5, 12.5, .8).IsArtifact && !ProcessForgingTrial.Assess(100, 100, 100, .7999).IsArtifact, "短板及稳定性独立约束");
            Throws(() => ProcessForgingTrial.Assess(double.NaN, 10, 10, .8), "非法指标拒绝");
            var baseOnly = ProcessForgingTrial.Clone(_model.Parameters); baseOnly.Carriers.RemoveAll(x => x.Special);
            var noActive = ProcessForgingTrial.Isolated(baseOnly, _model.MaterialFacts()).Evaluate(composite).Product!;
            Check(noActive.Tier == "法器" && !noActive.Effects.Any(x => x.Special) && Data(noActive.Calculation.Assessment) == Data(full.Calculation.Assessment) && noActive.Sharpness == full.Sharpness, "没有主动能力也能是法器，评阶不放大锋锐");
            foreach (string carrier in new[] { "keyword", "binding", "trigger", "direct", "summon" })
            {
                var focused = ProcessForgingTrial.Clone(_model.Parameters); focused.Carriers.RemoveAll(x => x.Id != carrier);
                var result = ProcessForgingTrial.Isolated(focused, _model.MaterialFacts()).Evaluate(_model.ExampleDraft(3));
                Invariants(result);
                Check(result.Product!.Effects.Any(x => x.CarrierId == carrier), "通用表达独立生成：" + carrier);
                Check(result.Product.Effects.All(x => x.BattleTemplateStatus == "未装配"), "测试描述不冒充已生效技能");
            }
            var sharpConfig = ProcessForgingTrial.Clone(_model.Parameters); sharpConfig.Profiles["铜髓矿"].Edge *= 100;
            var sharp = ProcessForgingTrial.Isolated(sharpConfig, _model.MaterialFacts()).Evaluate(basic).Product!;
            Check(sharp.Sharpness > normal.Sharpness && sharp.Tier == normal.Tier && Data(sharp.Calculation.Assessment) == Data(normal.Calculation.Assessment), "高锋锐不替代通灵水准");
            Check(_model.Snapshot("玄铁").Polarity == 2 && _model.Snapshot("紫砂灵土").Polarity == -2, "旧阴阳正确解码");
            foreach (int value in Enumerable.Range(-3, 7)) Check(ProcessForgingTrial.PolarityLabel(value) == ProcessForgingTrial.PolarityNames[value + 3], "七档完整");
            foreach (int extreme in new[] { -3, 3 })
            {
                var facts = _model.MaterialFacts(); foreach (var f in facts) f.Polarity = extreme;
                var a = ProcessForgingTrial.Isolated(_model.Parameters, facts).Evaluate(composite).Product!;
                Check(Math.Abs(a.Polarity - extreme) < 1e-8 && a.Tier == "法器", "同向极阴阳可以成法器");
            }
            var conflictFacts = _model.MaterialFacts(); foreach (var f in conflictFacts) f.Polarity = f.Name == "铜髓矿" ? -3 : f.Name == "裂甲石" ? 3 : 0;
            var conflict = ProcessForgingTrial.Isolated(_model.Parameters, conflictFacts).Evaluate(basic).Product!;
            Check(conflict.Sources.Single(x => x.Input.Role == "性材").Delivery < normal.Sources.Single(x => x.Input.Role == "性材").Delivery, "局部阴阳冲突不被总平均抹除");
            var reversed = ProcessForgingTrial.Clone(composite); reversed.Inputs.Reverse();
            Check(Data(_model.Evaluate(reversed)) == Data(_model.Evaluate(composite)), "投料顺序不改结果");
            var split = ProcessForgingTrial.Clone(composite); split.Inputs.Single(x => x.Role == "质材").Quantity = 2; split.Inputs.Add(new() { Role = "质材", Material = "铜髓矿", Quantity = 2 });
            Check(Data(_model.Evaluate(split)) == Data(_model.Evaluate(composite)), "同岗同材先合并");
            var unknown = ProcessForgingTrial.Clone(basic); unknown.Inputs[0].Material = "未知材料";
            Check(!_model.Inspect(unknown).CanSubmit, "无加工数据不可用");
            var missingBody = ProcessForgingTrial.Clone(basic); missingBody.Inputs.RemoveAll(x => x.Role == "主体");
            Check(!_model.Inspect(missingBody).CanSubmit, "缺主体不能提交");
            var noRoot = ProcessForgingTrial.Clone(basic); noRoot.Inputs.RemoveAll(x => x.Role == "质材");
            Check(_model.Evaluate(noRoot).Success, "质材不作原料身份硬门槛");
            var low = ProcessForgingTrial.Clone(basic); low.Proficiency = 0;
            Check(_model.Evaluate(low).Success, "加工难度影响保留，不硬禁低造诣");
            var locked = ProcessForgingTrial.Clone(composite); locked.Proficiency = 12;
            Check(!_model.Inspect(locked).CanSubmit && locked.Inputs.Count == composite.Inputs.Count, "位置超限阻止提交但保留草稿");
            var invalid = ProcessForgingTrial.Clone(basic); invalid.Inputs[0].Quantity = -1; Check(!_model.Inspect(invalid).CanSubmit, "负份数拒绝");
            var over = ProcessForgingTrial.Clone(basic); over.Inputs[0].Quantity = 8; over.Inputs.Add(new() { Role = "主体", Material = "铜髓矿", Quantity = 1 }); Check(!_model.Inspect(over).CanSubmit, "同岗合计上限");
            var legacyFuel = ProcessForgingTrial.Clone(basic); legacyFuel.Inputs.Add(new() { Role = "燃料", Material = "松心炭", Quantity = 1 });
            Check(!_model.Inspect(legacyFuel).CanSubmit && !_model.Parameters.Profiles.ContainsKey("松心炭") && !_model.Parameters.Profiles.ContainsKey("地脉焰炭") && !ProcessForgingTrial.RoleOrder.Contains("燃料"), "旧燃料投料明确拒绝，不偷偷计入成本");
            Check(typeof(ProcessForgingTrial.Preparation).GetProperty("OfferedHeat") is null && typeof(ProcessForgingTrial.Artifact).GetProperty("RequiredHeat") is null && typeof(ProcessForgingTrial.Profile).GetProperty("FuelHeat") is null && !_model.Parameters.Scalars.ContainsKey("HeatPerUnit"), "模型不再保留供热参数");
            Check(normal.Sources.All(x => x.BodySupply > 0 && x.CharacterSupply > 0 && x.ProcessSupply > 0), "主要来源并非独占");
            Check(_model.Q(0) == 0 && Math.Abs(_model.Q(1) - 1) < 1e-10 && _model.Q(3) - _model.Q(2) < _model.Q(2) - _model.Q(1) && _model.W(3) > _model.W(2), "份数递减与品级成长");
            var externalParameters = _model.Parameters; externalParameters.SupplyScale = 999; externalParameters.Profiles["铜髓矿"].Retention = .01;
            Check(Data(_model.Evaluate(basic).Product!.Effects) == Data(normal.Effects), "外部参数副本不改变当前模型或快照");
            var promotedFacts = _model.MaterialFacts(); promotedFacts.Single(x => x.Name == "裂甲石").Utilities["破"]++;
            var promoted = ProcessForgingTrial.Isolated(_model.Parameters, promotedFacts).Evaluate(basic).Product!;
            Check(promoted.Calculation.CharacterPotential["破"] >= normal.Calculation.CharacterPotential["破"], "相关功用品级提高不降低该来源潜力");
            // 同数值新材料仅增加档案与材性，不改算法。
            var aliasConfig = ProcessForgingTrial.Clone(_model.Parameters); var aliasFacts = _model.MaterialFacts();
            aliasConfig.Profiles["测试新金属"] = aliasConfig.Profiles["铜髓矿"]; aliasConfig.Profiles.Remove("铜髓矿");
            aliasFacts.Single(x => x.Name == "铜髓矿").Name = "测试新金属";
            var aliasDraft = ProcessForgingTrial.Clone(basic); foreach (var i in aliasDraft.Inputs.Where(x => x.Material == "铜髓矿")) i.Material = "测试新金属";
            var alias = ProcessForgingTrial.Isolated(aliasConfig, aliasFacts).Evaluate(aliasDraft).Product!;
            Check(Data(alias.Effects) == Data(normal.Effects) && alias.Sharpness == normal.Sharpness && Data(alias.Calculation) == Data(normal.Calculation), "不依材料名匹配配方");
            var badConfig = ProcessForgingTrial.Clone(_model.Parameters); badConfig.Scalars.Remove("BodyStability"); Throws(() => ProcessForgingTrial.Isolated(badConfig, _model.MaterialFacts()), "缺公式参数拒绝");
            var badProfile = ProcessForgingTrial.Clone(_model.Parameters); badProfile.Profiles["铜髓矿"].Volume = 0; Throws(() => ProcessForgingTrial.Isolated(badProfile, _model.MaterialFacts()), "缺物理加工参数拒绝");
            var old = ProcessForgingTrial.Clone(normal); old.SchemaVersion = 2; Throws(() => ProcessForgingTrial.Replay(old), "旧含燃料快照不自动改用新规则");
            var bridge = ProcessForgingTrial.Clone(basic); bridge.Proficiency = 100; bridge.Inputs.Single(x => x.Role == "质材").Material = "青木芯";
            bridge.Inputs.Add(new() { Role = "和材", Material = "紫砂灵土", Quantity = 1 });
            var noBridge = _model.Evaluate(bridge).Product!;
            Check(noBridge.Sources.Single(x => x.Input.Role == "性材").Delivery == .6 && noBridge.Sources.All(x => x.BridgeSpent == 0), "缺真实火行和材不虚构通关");
            bridge.Inputs.Add(new() { Role = "和材", Material = "赤纹燧石", Quantity = 1 });
            var bridged = _model.Evaluate(bridge); Invariants(bridged);
            Check(bridged.Product!.Sources.Single(x => x.Input.Role == "性材").Delivery == .9 && bridged.Product.Sources.Where(x => x.Input.Role == "和材").All(x => x.BridgeSpent > 0 && x.ProcessSupply == 0), "真实通关材料独占且不二次供给");
            Check(ProcessForgingTrial.Relation("金", "木", out var path) == .6 && string.Join("", path) == "水", "五行有向通关");
            // 五行是加工关系，不按成品总五行重复赠送伤害或属性。
            var sameElementResults = new List<ProcessForgingTrial.Artifact>();
            foreach (string element in new[] { "金", "木", "水", "火", "土" })
            {
                var facts = _model.MaterialFacts(); foreach (var fact in facts) fact.Element = element;
                var result = ProcessForgingTrial.Isolated(_model.Parameters, facts).Evaluate(composite); Invariants(result);
                sameElementResults.Add(result.Product!);
            }
            var sameElementBaseline = sameElementResults[0];
            Check(sameElementResults.All(x => Data(x.Effects) == Data(sameElementBaseline.Effects) && Data(x.Calculation.Assessment) == Data(sameElementBaseline.Calculation.Assessment) && x.Sharpness == sameElementBaseline.Sharpness), "相同数值与关系换五行不会凭空发放成品五行加成");
            Check(typeof(ProcessForgingTrial.Artifact).GetProperty("Element") is null && typeof(ProcessForgingTrial.Artifact).GetProperty("ElementRatios") is null, "不设置器物总五行或伤害占比字段");
            var opposedFacts = _model.MaterialFacts(); opposedFacts.Single(x => x.Name == "裂甲石").Element = "木";
            var opposedDraft = ProcessForgingTrial.Clone(basic); opposedDraft.Proficiency = 100;
            var opposedModel = ProcessForgingTrial.Isolated(_model.Parameters, opposedFacts);
            var opposed = opposedModel.Evaluate(opposedDraft).Product!;
            var matched = _model.Evaluate(opposedDraft).Product!;
            var opposedSource = opposed.Sources.Single(x => x.Input.Role == "性材");
            var matchedSource = matched.Sources.Single(x => x.Input.Role == "性材");
            Check(opposedSource.Delivery == .6 && matchedSource.Delivery == 1 && Math.Abs(opposedSource.CharacterSupply - .6 * matchedSource.CharacterSupply) < 1e-8 && opposed.Calculation.Assessment.Stability < matched.Calculation.Assessment.Stability, "五行相克实际降低送达潜力与稳定性");
            opposedDraft.Inputs.Add(new() { Role = "和材", Material = "灵泉露", Quantity = 1 });
            var waterBridge = opposedModel.Evaluate(opposedDraft); Invariants(waterBridge);
            Check(waterBridge.Product!.Sources.Single(x => x.Input.Role == "性材").Delivery == .9 && waterBridge.Product.Sources.Single(x => x.Input.Role == "和材").BridgeSpent > 0, "金根木性用真实水行和材通关，不把水变成伤害属性");
            var demands = new List<ForgingBudgetSolver.Demand> { new("base", 1, 0, new() { ["source"] = 1 }), new("keyword", 1, 0, new() { ["source"] = 1, ["通"] = 1 }) };
            var allocated = ForgingBudgetSolver.Solve(demands, new Dictionary<string, double> { ["source"] = 40, ["通"] = 12 });
            Check(Math.Abs(allocated["base"] - 28) < 1e-5 && Math.Abs(allocated["keyword"] - 12) < 1e-5, "共同预算解析算例40=28+12");
            var shared = ForgingBudgetSolver.Solve(demands, new Dictionary<string, double> { ["source"] = 40, ["通"] = 40 });
            Check(Math.Abs(shared["base"] - 20) < 1e-5 && Math.Abs(shared["keyword"] - 20) < 1e-5, "表达增多分摊同一来源");
            var zero = ForgingBudgetSolver.Solve(demands, new Dictionary<string, double> { ["source"] = 40, ["通"] = 0 });
            Check(Math.Abs(zero["base"] - 40) < 1e-5 && zero["keyword"] == 0, "零导通不拖累基础属性");
            for (int z = 0; z <= 100; z += 20)
                for (int q = 1; q <= 8; q++)
                {
                    var d = ProcessForgingTrial.Clone(composite); d.Proficiency = z; d.Inputs.Single(x => x.Role == "性材" && x.Material == "裂甲石").Quantity = q;
                    var r = _model.Evaluate(d); Invariants(r); Check(Data(r) == Data(_model.Evaluate(d)), "参数网格确定性");
                }
            var session = new ProcessForgingTrial.Session(_model.Parameters.Profiles.Keys); int before = session.Stock.Values.Sum();
            Check(!session.Submit(_model, unknown, 1).CanSubmit && session.Stock.Values.Sum() == before && session.Hours == 0, "非法提交不扣料");
            Check(session.Submit(_model, composite, 2).Success && session.Artifacts.Count == 1 && session.Stock["玄铁"] == 24, "跨岗合计扣料");
            int after = session.Stock.Values.Sum(), hours = session.Hours;
            Check(!session.Submit(_model, composite, 2).CanSubmit && session.Stock.Values.Sum() == after && session.Hours == hours, "重复提交幂等");
            Check(session.Submit(_model, composite, 3).Success && session.Artifacts[0].InstanceId != session.Artifacts[1].InstanceId, "独立实例");
            var copy = session.Artifacts[0]; copy.Name = "修改"; copy.Sources[0].Input.Quantity = 999;
            Check(session.Artifacts[0].Name != copy.Name && session.Artifacts[0].Sources[0].Input.Quantity != 999, "归档快照防外部篡改");
            int thirdStock = session.Stock.Values.Sum(); var third = session.Submit(_model, _model.ExampleDraft(2), 4);
            Check(third.Success && session.Stock.Values.Sum() == thirdStock - third.Units && session.Attempts.Count == 3 && session.Artifacts.Count == 3, "原少燃料投料现在正常成器且只扣当前材料");
            session.Stock["铜髓矿"] = 0; after = session.Stock.Values.Sum(); Check(!session.Submit(_model, basic, 5).CanSubmit && session.Stock.Values.Sum() == after, "库存不足原子拒绝");
            // 实机操作验证：投料页不调用成器结算；按钮信号完成测试闭环。
            LoadExample(1); await Capture("01_投料.png");
            var labels = new List<string>(); void ReadText(Node n) { if (n is Label l) labels.Add(l.Text); foreach (var child in n.GetChildren()) ReadText(child); } ReadText(_page);
            Check(!labels.Any(x => x.Contains("已贯通") || x.Contains("留存不足") || x.Contains("H=") || x.Contains("锋锐") || x.Contains("预计成品")), "炼前没有成品或达标提示");
            Check(!labels.Any(x => x.Contains("燃料") || x.Contains("供热")), "投料页没有燃料和供热");
            Picker("性材", _draft.Inputs.First(x => x.Role == "性材")); await Capture("02_选材.png"); CloseOverlay();
            LoadExample(0); _buttons["craft"].EmitSignal(Button.SignalName.Pressed); Check(_session.Artifacts.Count == 1 && _overlay is not null, "凡器场景按钮闭环"); await Capture("03_凡器.png"); CloseOverlay();
            LoadExample(1); _buttons["craft"].EmitSignal(Button.SignalName.Pressed); Check(_session.Artifacts.Count == 2 && _overlay is not null, "法器场景按钮闭环"); await Capture("04_法器.png");
            var artifact = _session.Artifacts[^1]; ShowArtifact(artifact, 1); await Capture("05_能力详情.png"); ShowArtifact(artifact, 2); await Capture("06_炼制记录.png");
            labels.Clear(); ReadText(_overlay!);
            Check(labels.Any(x => x.StartsWith("材料五行来源：")) && !labels.Any(x => x.StartsWith("器性：") || x.Contains("供热") || x.Contains("燃料")), "五行只标为材料来源，记录无燃料");
            ShowArtifact(artifact, 0); labels.Clear(); ReadText(_overlay!);
            Check(!labels.Any(x => x.StartsWith("器性：") || x.Contains("95%") || x.Contains("5%")), "器物本体页不显示五行占比");
            int sceneStock = _session.Stock.Values.Sum(); CloseOverlay(); Gallery(); CloseOverlay(); ShowArtifact(artifact, 0); CloseOverlay();
            Check(_session.Stock.Values.Sum() == sceneStock && _session.Artifacts.Count == 2, "重看不二次结算");
            _buttons["craft"].EmitSignal(Button.SignalName.Pressed); Check(_session.Stock.Values.Sum() == sceneStock, "界面重复点击不扣料"); CloseOverlay();
            LoadExample(2); _draft.Inputs.RemoveAll(x => x.Role == "主体"); Edit(); sceneStock = _session.Stock.Values.Sum(); _buttons["craft"].EmitSignal(Button.SignalName.Pressed); await Capture("07_非法投料.png"); Check(_session.Artifacts.Count == 2 && _session.Stock.Values.Sum() == sceneStock && _overlay is not null, "缺主体拒绝开炼且不扣材料");
            CloseOverlay(); LoadExample(3); _buttons["craft"].EmitSignal(Button.SignalName.Pressed); Check(_session.Artifacts.Count == 3, "第三种成功投料进入器物册"); await Capture("08_多材成器.png");
            report.AppendLine($"## 验收\n\n{checks}项断言全部通过；含48组造诣/份数网格、预算守恒、评阶边界、新材料、快照、库存与实机场景。\n\n未改战斗卡，未运行无关战斗底座27项；未接入正式装备、存档、锋锐伤害、器蕴规则、耐久或法器小档。\n");
            using (var file = Godot.FileAccess.Open(Evidence + "运行记录.md", Godot.FileAccess.ModeFlags.Write)) file.StoreString(report.ToString());
            GD.Print($"PROCESS_FORGING_AUDIT_OK {checks}"); Input.SetCustomMouseCursor(null); GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); Input.SetCustomMouseCursor(null); GetTree().Quit(1); }
    }
    private async System.Threading.Tasks.Task Capture(string name)
    {
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(Evidence));
        Input.WarpMouse(new Vector2(1660, 940)); HoverInfoManager.HideAll();
        await ToSignal(GetTree().CreateTimer(.3), SceneTreeTimer.SignalName.Timeout); HoverInfoManager.HideAll();
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage();
        if (image.SavePng(Evidence + name) != Error.Ok) throw new InvalidOperationException("截图保存失败：" + name);
    }
}

