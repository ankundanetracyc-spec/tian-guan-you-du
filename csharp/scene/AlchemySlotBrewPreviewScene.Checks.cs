using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

public partial class AlchemySlotBrewPreviewScene
{
    private async Task RunSlotUiChecks()
    {
        try
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            _tutorialOverlay.Hide();
            if (OS.GetCmdlineUserArgs().Contains("--alchemy-spirit-capture") || OS.GetCmdlineUserArgs().Contains("--alchemy-complete-capture"))
            {
                bool completeCapture = OS.GetCmdlineUserArgs().Contains("--alchemy-complete-capture");
                _attainmentInput.Value = completeCapture ? 18 : 36;
                ApplySampleRoute(completeCapture ? AlchemyRecipeCatalog.Pills[0].SampleRoutes.Single(r => r.Name == "青木清露方")
                    : AlchemyRecipeCatalog.Pills[0].SampleRoutes.Last());
                if (!completeCapture) { _hoverTarget = _mainPickButton; _hoverDelay = 1; _Process(.4); }
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                GetViewport().GetTexture().GetImage().SavePng(completeCapture ? "res://outputs/alchemy_complete_page_20261023.png"
                    : "res://outputs/alchemy_spirit_detail_20261023.png");
                if (completeCapture)
                {
                    var distribution = AlchemySlotSimulation.Simulate(GetSimulationInputs(), Attainment, TotalSpirit, OtherSpiritOccupation);
                    GD.Print("ALCHEMY_PRESENTATION_SCORES " + string.Join(",", distribution.Pills.Select(p => p.Quality)));
                    if (OS.GetCmdlineUserArgs().Contains("--alchemy-simple-capture")) _simpleAlchemyPresentation = true;
                    ShowBrewSimulation();
                    if (OS.GetCmdlineUserArgs().Contains("--alchemy-skip-capture"))
                    {
                        bool late = OS.GetCmdlineUserArgs().Contains("--alchemy-skip-late");
                        await ToSignal(GetTree().CreateTimer(late ? 4.4 : .3), SceneTreeTimer.SignalName.Timeout);
                        _brewStage!.GetChildren().OfType<Button>().Single(b => b.Text.StartsWith("跳过演出")).EmitSignal(Button.SignalName.Pressed);
                        while (_brewing) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                        GetViewport().GetTexture().GetImage().SavePng(late ? "res://outputs/alchemy_skip_late_20261024.png" : "res://outputs/alchemy_skip_early_20261024.png");
                        var cards = _brewStage.GetChildren().OfType<Control>().Where(c => c.HasMeta("settled_position")).ToArray();
                        GD.Print($"ALCHEMY_SKIP_PRESENTATION late={late} count={cards.Length} normal={cards.All(c => c.Scale == Vector2.One && c.Position == c.GetMeta("settled_position").AsVector2())} residue={_brewStage.GetChildren().OfType<Control>().Count(c => c.HasMeta("alchemy_flying_ingredient"))}");
                        GetTree().Quit();
                        return;
                    }
                    await ToSignal(GetTree().CreateTimer(.7), SceneTreeTimer.SignalName.Timeout);
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    GetViewport().GetTexture().GetImage().SavePng("res://outputs/alchemy_absorption_20261023.png");
                    await ToSignal(GetTree().CreateTimer(1.8), SceneTreeTimer.SignalName.Timeout);
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    GetViewport().GetTexture().GetImage().SavePng("res://outputs/alchemy_mouth_fire_20261023.png");
                    while (_brewing) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    GetViewport().GetTexture().GetImage().SavePng("res://outputs/alchemy_complete_result_20261023.png");
                    if (OS.GetCmdlineUserArgs().Contains("--alchemy-receipt-capture"))
                    {
                        _brewStage!.GetChildren().OfType<Button>().Single(b => b.Text.StartsWith("确定 ·")).EmitSignal(Button.SignalName.Pressed);
                        if (!_simpleAlchemyPresentation)
                        {
                            await ToSignal(GetTree().CreateTimer(.62), SceneTreeTimer.SignalName.Timeout);
                            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                            GetViewport().GetTexture().GetImage().SavePng("res://outputs/alchemy_enter_bottle_20261024.png");
                            await ToSignal(GetTree().CreateTimer(.36), SceneTreeTimer.SignalName.Timeout);
                            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                            GetViewport().GetTexture().GetImage().SavePng("res://outputs/alchemy_stopper_seat_20261024.png");
                        }
                        while (_alchemyClaim is null)
                            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                        GetViewport().GetTexture().GetImage().SavePng("res://outputs/alchemy_receipt_20261024.png");
                        GD.Print("ALCHEMY_RECEIPT_CAPTURE_PASS shared=" + _alchemyClaim.GetType().Name); GetTree().Paused = false; GetTree().Quit(); return;
                    }
                    var revealed = _brewStage?.GetChildren().OfType<Control>().FirstOrDefault(c => c.HasMeta("pill_item_path"));
                    if (revealed is not null)
                    {
                        var item = GD.Load<PillItemData>(revealed.GetMeta("pill_item_path").AsString());
                        Input.ParseInputEvent(new InputEventMouseMotion { Position = revealed.GetGlobalRect().GetCenter() });
                        Input.ParseInputEvent(new InputEventMouseMotion { Position = revealed.GetGlobalRect().GetCenter() });
                        HoverInfoManager.SetInfo(revealed, AlchemyPillVisual.Hover(item, revealed.GetMeta("pill_score").AsInt32()) with
                        { Icon = GD.Load<Texture2D>(AnimationPillTexturePath) }, true);
                        await ToSignal(GetTree().CreateTimer(.15), SceneTreeTimer.SignalName.Timeout);
                        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                        GetViewport().GetTexture().GetImage().SavePng("res://outputs/alchemy_pill_hover_20261023.png");
                    }
                }
                GD.Print("ALCHEMY_SPIRIT_CAPTURE_PASS");
                GetTree().Quit();
                return;
            }
            int checks = 0;
            void Verify(bool condition, string label)
            {
                if (!condition) throw new InvalidOperationException(label);
                checks++;
                GD.Print("ALCHEMY_SLOT_UI_OK " + label);
            }
            MaterialCraftData Craft(params (string Utility, int Grade)[] functions) => new()
            {
                element = "木", texture = "液体",
                utilities = new Godot.Collections.Array<MaterialUtilityGrade>(functions.Select(f =>
                    new MaterialUtilityGrade { utility_id = f.Utility, grade = f.Grade }))
            };
            AlchemySlotSimulation.Input Herb(string id, AlchemySlotSimulation.SlotRole role,
                MaterialCraftData craft, int dose = 1) => new(id, id, craft, dose, role);
            double Load(AlchemySlotSimulation.Input input) => AlchemySlotSimulation.SpiritLoads([input]).Single().Occupation;
            var solid = Herb("固药", AlchemySlotSimulation.SlotRole.Zuo, Craft(("固", 1)));
            Verify(Load(solid with { Role = AlchemySlotSimulation.SlotRole.Chen }) > Load(solid), "相同固药臣位占用高于佐位");
            Verify(Load(solid with { Dose = 2 }) > Load(solid) && Load(solid with { Dose = 2 }) < 2 * Load(solid), "增量提高占用但不按两味翻倍");
            Verify(Load(solid with { Craft = Craft(("固", 3)) }) > Load(solid), "高品功用与材料占用上升");
            var moreFunctions = solid with { Craft = Craft(("固", 1), ("明", 1)) };
            Verify(AlchemySlotSimulation.SpiritLoads([moreFunctions]).Single().FunctionLoad >
                AlchemySlotSimulation.SpiritLoads([solid]).Single().FunctionLoad, "本位功用越多负担越高");
            var inactive = solid with { Craft = Craft(("固", 1), ("行", 4)) };
            var inactiveLoad = AlchemySlotSimulation.SpiritLoads([inactive]).Single();
            Verify(inactiveLoad.FunctionLoad == AlchemySlotSimulation.SpiritLoads([solid]).Single().FunctionLoad &&
                inactiveLoad.BaseLoad > AlchemySlotSimulation.SpiritLoads([solid]).Single().BaseLoad,
                "非本位功用不收功用负担但高材阶保留基础负担");
            Verify(AlchemySlotSimulation.SpiritOccupation([]) == 0 &&
                AlchemySlotSimulation.SpiritOccupation([solid]) > 0, "空炉零占用且无君药仍计辅药");
            Verify(AlchemySlotSimulation.AvailableSpirit(24, 7) == 17 &&
                AlchemySlotSimulation.AvailableSpirit(24, 30) == 0, "其他占用扣除且可支配上限不为负");
            var perfectInputs = new[]
            {
                Herb("满效君", AlchemySlotSimulation.SlotRole.Jun, Craft(("生", 2)), 3),
                Herb("满效臣", AlchemySlotSimulation.SlotRole.Chen, Craft(("固", 2))),
                Herb("满效佐", AlchemySlotSimulation.SlotRole.Zuo, Craft(("明", 3))),
                Herb("满效使通", AlchemySlotSimulation.SlotRole.Shi, Craft(("通", 5))),
                Herb("满效使行", AlchemySlotSimulation.SlotRole.Shi, Craft(("行", 4)))
            };
            var perfect = AlchemySlotSimulation.Simulate(perfectInputs, 203);
            Verify(perfect.CanSimulate && perfect.OutputCount == 4 && perfect.Quality == 100 &&
                perfect.QualityName == "满效" && perfect.EffectiveMaterialCount == 5,
                "五味各有贡献且评分全满时可达满效");
            int exactNeed = AlchemySlotSimulation.SpiritOccupation(perfectInputs);
            Verify(AlchemySlotSimulation.Simulate(perfectInputs, 203, exactNeed).CanSimulate &&
                !AlchemySlotSimulation.Simulate(perfectInputs, 203, exactNeed - 1).CanSimulate,
                "可支配灵识等于需求可炼少一点则阻塞");
            Verify(!AlchemySlotSimulation.Simulate(perfectInputs, 203, exactNeed, 1).CanSimulate,
                "其他持续占用影响本炉准入");
            Verify(perfect.SpiritNeed == exactNeed && AlchemySlotSimulation.Simulate([solid], 203).SpiritNeed ==
                AlchemySlotSimulation.SpiritOccupation([solid]), "开炼与缺君药的读数共用占用算法");
            var unknownInputs = new[] { Herb("未知君", AlchemySlotSimulation.SlotRole.Jun, Craft(("固", 1))) };
            Verify(AlchemySlotSimulation.Simulate(unknownInputs).SpiritNeed == AlchemySlotSimulation.SpiritOccupation(unknownInputs),
                "未知丹种共用占用算法");
            ClearSelections();
            Verify(_spiritHeaderValue.Text == "24/24" && _resourceSummary.Text.Contains("本炉灵识占用\n0"),
                "空炉顶部显示全部可支配右侧占用零");
            _attainmentInput.Value = 36;
            ApplySampleRoute(AlchemyRecipeCatalog.Pills[0].SampleRoutes.Last());
            int sampleOccupation = AlchemySlotSimulation.SpiritOccupation(GetSimulationInputs());
            Verify(_spiritHeaderValue.Text == "24/24" &&
                _resourceSummary.Text.Contains($"本炉灵识占用\n{sampleOccupation}"), "配伍只改变右侧需求不提前扣除顶部可支配");
            TotalSpirit = 100;
            OtherSpiritOccupation = 70;
            RefreshSummary();
            Verify(_spiritHeaderValue.Text == "30/100" && !_brewButton.Disabled,
                "总灵识100其他事务占70顶部显示30比100");
            OtherSpiritOccupation = 100 - sampleOccupation;
            RefreshSummary();
            Verify(!_brewButton.Disabled && _spiritHeaderValue.Text == $"{sampleOccupation}/100", "本炉需求等于剩余额度可炼");
            OtherSpiritOccupation++;
            RefreshSummary();
            Verify(_brewButton.Disabled && _spiritHeaderValue.Text == $"{sampleOccupation - 1}/100",
                "本炉需求大于剩余额度时禁止开炼而非比较人物总值");
            TotalSpirit = 24;
            OtherSpiritOccupation = 0;
            ClearSelections();
            _attainmentInput.Value = 1196;
            _main = new MaterialChoice("spirit-overflow-fixture", _materials[0].Item, Craft(("生", 9), ("固", 9)));
            RefreshPage();
            Verify(_spiritHeaderValue.Text == "24/24" && _brewButton.Disabled &&
                _resourceSummary.Text.Contains($"本炉灵识占用\n{AlchemySlotSimulation.SpiritOccupation(GetSimulationInputs())}"),
                "本炉需求超限不改变顶部余额右侧保留真实需求并禁止开炼");
            ClearSelections();
            foreach (var pill in AlchemyRecipeCatalog.Pills)
            foreach (var route in pill.SampleRoutes.TakeLast(2))
            foreach (double attainment in new[] { 8d, 36d })
            {
                var inputs = route.Ingredients.Select(i =>
                {
                    var material = _materials.Single(m => m.Name == i.HerbName);
                    return new AlchemySlotSimulation.Input(material.Name, material.Path, material.Craft, i.Dose, RoleFor(i.AssistantSlot));
                }).ToList();
                Verify(inputs.All(i => i.Dose is >= 1 and <= 4) && inputs.Sum(i => i.Dose) <= 8 &&
                    inputs.Select(i => i.Path).Distinct().Count() == inputs.Count, $"{route.Name}整数投料");
                var result = AlchemySlotSimulation.Simulate(inputs, attainment);
                GD.Print($"RECIPE_DIAG {route.Name} A={attainment} Q={result.Quality} count={result.OutputCount} {result.Outcome} {string.Join(" | ", result.Details.Take(6))}");
                Verify(result.CanSimulate && !result.Exploded && !result.OrdinaryFailure &&
                    result.OutputCount > 0 && result.Quality >= 20 && result.ProductName == pill.Name,
                    $"{route.Name}造诣{attainment}成丹");
                _attainmentInput.Value = attainment;
                ApplySampleRoute(route);
                Verify(GetSimulationInputs().SequenceEqual(inputs), $"{route.Name}造诣{attainment}载入");
                GD.Print($"RECIPE_RESULT {route.Name} A={attainment} {result.ProductName} Q={result.Quality} count={result.OutputCount} L={result.SpiritNeed} E={result.EnergyNeed} blocked={result.BlockingReason}");
            }
            ClearSelections();
            _attainmentInput.Value = 508;
            Verify(Attainment == 508 && Enumerable.Range(0, 7).All(SlotUnlocked), "造诣控件开放八味");
            for (int slot = -1; slot < 7; slot++)
            {
                ShowPicker(slot);
                _effectiveOnly.ButtonPressed = true;
                var expected = _materials.Count(m => IsAvailableInSlot(m, slot) &&
                    m.Craft.utilities.Any(u => u is not null && FunctionActsInSlot(slot, u.utility_id)));
                Verify(_pickerGrid.GetChildCount() == expected && expected > 0, $"药位{slot}有效功用筛选");
                foreach (var element in new[] { "金", "木", "水", "火", "土" })
                {
                    _elementFilter.Select(Array.IndexOf(new[] { "", "金", "木", "水", "火", "土" }, element));
                    _polarityFilter.Select(1);
                    _pickerSearch.Text = "固";
                    RefreshPickerGrid();
                    expected = _materials.Count(m => IsAvailableInSlot(m, slot) && m.Craft.element == element &&
                        m.Craft.polarity < 0 && PickerText(m).Contains("固") &&
                        m.Craft.utilities.Any(u => u is not null && FunctionActsInSlot(slot, u.utility_id)));
                    Verify(_pickerGrid.GetChildCount() == expected, $"药位{slot}组合筛选{element}阴固");
                }
                _elementFilter.Select(0);
                _polarityFilter.Select(0);
                _pickerSearch.Clear();
                _effectiveOnly.ButtonPressed = false;
            }
            ShowPicker(-1);
            _pickerSearch.Text = "不存在的药材";
            _pickerSearch.EmitSignal(LineEdit.SignalName.TextChanged, _pickerSearch.Text);
            Verify(_pickerGrid.GetChildCount() == 0 && _pickerCount.Text.Contains("没有"), "空结果反馈");
            _pickerSearch.Clear();
            _pickerSearch.EmitSignal(LineEdit.SignalName.TextChanged, "");
            Verify(_pickerGrid.GetChildCount() == _materials.Count, "重置后恢复全部材料");
            _pickerOverlay.Hide();
            _assistants[4] = _materials[0];
            _assistantDoses[4] = 2;
            _attainmentInput.Value = 8;
            Verify(_assistants[4] is null && _assistantDoses[4] == 1 && !_assistantRows[4].Visible,
                "降造诣清除锁定位且刷新布局");
            Verify(_messageOverlay.Visible, "降造诣清料提示");
            _messageOverlay.Hide();
            foreach (var value in new[] { 31d, 32, 67, 68, 202, 203, 507, 508 })
            {
                _attainmentInput.Value = value;
                Verify(Attainment == value && Enumerable.Range(0, 7).Count(SlotUnlocked) + 1 ==
                    2 + AlchemySlotSimulation.RoleCapacity(AlchemySlotSimulation.SlotRole.Zuo, value) +
                    AlchemySlotSimulation.RoleCapacity(AlchemySlotSimulation.SlotRole.Shi, value),
                    $"动态药位门槛{value}");
            }
            _attainmentInput.Value = 36.5;
            Verify(Attainment == 36.5, "造诣保留小数");
            ClearSelections();
            ShowPicker(-1);
            foreach (var button in _utilityFilterButtons)
            {
                button.ButtonPressed = true;
                var expected = _materials.Count(m => m.Craft.utilities.Any(u => u is not null && u.utility_id == button.Text));
                Verify(_pickerGrid.GetChildCount() == expected && expected > 0, $"单功用筛选{button.Text}");
                button.ButtonPressed = false;
            }
            _utilityFilterButtons.Single(b => b.Text == "生").ButtonPressed = true;
            _utilityFilterButtons.Single(b => b.Text == "固").ButtonPressed = true;
            var unionCount = _materials.Count(m => m.Craft.utilities.Any(u => u is not null && (u.utility_id == "生" || u.utility_id == "固")));
            Verify(_pickerGrid.GetChildCount() == unionCount && unionCount > 0, "多功用按或匹配");
            _elementFilter.Select(2); // 木
            _polarityFilter.Select(3); // 阳
            _effectiveOnly.ButtonPressed = true;
            RefreshPickerGrid();
            var combinedCount = _materials.Count(m => m.Craft.element == "木" && m.Craft.polarity > 0 &&
                m.Craft.utilities.Any(u => u is not null && (u.utility_id == "生" || u.utility_id == "固")) &&
                m.Craft.utilities.Any(u => u is not null && FunctionActsInSlot(-1, u.utility_id)));
            Verify(_pickerGrid.GetChildCount() == combinedCount, "功用与五行阴阳本位组合");
            _pickerOverlay.FindChild("ResetPickerFilters", true, false).EmitSignal(Button.SignalName.Pressed);
            Verify(_utilityFilters.Count == 0 && _utilityFilterButtons.All(b => !b.ButtonPressed) &&
                _pickerGrid.GetChildCount() == _materials.Count, "实际重置按钮清除全部功用");
            _pickerOverlay.Hide();
            _attainmentInput.Value = 100;
            var optimal = new[] { ("泥脉石团", 2, -1), ("山蚕长丝", 1, 0), ("月露菌汁", 1, 1), ("回泉珠", 2, 2), ("风纹石英", 2, 3) };
            var optimalRoute = new AlchemyRecipeCatalog.SampleRoute("双化检查", "", optimal.Select(i =>
                new AlchemyRecipeCatalog.SampleIngredient(i.Item1, i.Item2, i.Item3)).ToArray());
            ApplySampleRoute(optimalRoute);
            var cleanResult = AlchemySlotSimulation.Simulate(GetSimulationInputs(), Attainment);
            Verify(cleanResult.Quality >= 20 && cleanResult.OutputCount > 0 && cleanResult.OutputCount <= 4, "双化旧样方仍按新版实际指标成丹");
            ShowBrewSimulation();
            while (_brewing) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            CloseBrewPresentation();
            Verify(!_messageBody.Text.Contains("每枚约") && !cleanResult.Details.Any(d => d.Contains("每枚预估")),
                "结果和详情无废弃药效值");
            Verify(typeof(AlchemyRecipeCatalog.PillType).GetProperty("EffectCurve") is null &&
                typeof(AlchemySlotSimulation.Result).GetProperty("Effect") is null, "曲线及结果旧字段已删除");
            if (OS.GetCmdlineUserArgs().Contains("--alchemy-filter-capture"))
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                GetViewport().GetTexture().GetImage().SavePng("res://outputs/alchemy_result_clean_20261023.png");
                _messageOverlay.Hide();
                ShowPicker(1);
                _utilityFilterButtons.Single(b => b.Text == "化").ButtonPressed = true;
                _utilityFilterButtons.Single(b => b.Text == "明").ButtonPressed = true;
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                GetViewport().GetTexture().GetImage().SavePng("res://outputs/alchemy_utility_filter_20261023.png");
            }
            RunCompleteAlchemyChecks(Verify);
            GD.Print($"ALCHEMY_SLOT_UI_PASS {checks}");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }
}
