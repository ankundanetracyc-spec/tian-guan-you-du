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
            int checks = 0;
            void Verify(bool condition, string label)
            {
                if (!condition) throw new InvalidOperationException(label);
                checks++;
                GD.Print("ALCHEMY_SLOT_UI_OK " + label);
            }
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
            Verify(cleanResult.Quality == 99 && cleanResult.OutputCount == 4, "清理药效曲线不改变成丹评分");
            Verify(AlchemySlotSimulation.QualityName(90) == "优效" &&
                AlchemySlotSimulation.QualityName(91) == "极效" &&
                cleanResult.QualityName == "极效" &&
                AlchemySlotSimulation.QualityName(100) == "无暇", "丹效91至99极效、100无暇边界");
            Verify(_ingredientLedgerRows.GetChildCount() == 5 && _ingredientLedgerCount.Text == "5味 · 8份", "投料信息栏列出五味");
            SetDose(-1, 1);
            Verify(_ingredientLedgerRows.GetChild<Control>(0).GetChild<Label>(0).Text.EndsWith("×1"), "调量同步信息栏");
            ClearAssistant(1);
            Verify(_ingredientLedgerRows.GetChildCount() == 4, "移除药材同步信息栏");
            ApplySampleRoute(optimalRoute);
            Verify(_ingredientLedgerRows.GetChild<Control>(0).GetChild<Label>(2).Text.Contains("镇九品"), "本位生效功用与品级");
            ShowBrewSimulation();
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
            var leftTab = _canvas.GetNode<Button>("ToggleIngredientLedger");
            var rightTab = _canvas.GetNode<Button>("ToggleBatchRequirements");
            leftTab.EmitSignal(Button.SignalName.Pressed);
            rightTab.EmitSignal(Button.SignalName.Pressed);
            Verify(!_canvas.GetNode<Control>("IngredientLedger").Visible &&
                !_canvas.GetNode<Control>("BatchRequirements").Visible, "双侧栏独立收起");
            Verify(_compactBrewButton!.Visible && _compactBrewButton.Disabled == _brewButton.Disabled,
                "收起后炼丹入口保留");
            leftTab.EmitSignal(Button.SignalName.Pressed);
            Verify(_canvas.GetNode<Control>("IngredientLedger").Visible &&
                !_canvas.GetNode<Control>("BatchRequirements").Visible, "单侧展开不影响另一侧");
            rightTab.EmitSignal(Button.SignalName.Pressed);
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
