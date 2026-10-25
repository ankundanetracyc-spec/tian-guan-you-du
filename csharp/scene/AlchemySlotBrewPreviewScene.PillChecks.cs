using Godot;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

public partial class AlchemySlotBrewPreviewScene
{
    private async Task RunPillDescriptionChecks()
    {
        string path = ProjectSettings.GlobalizePath("user://alchemy/pill-check-" + Guid.NewGuid().ToString("N") + ".json");
        _notebookPathOverride = path;
        try
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            _tutorialOverlay.Hide();
            int checks = 0;
            void Verify(bool condition, string label)
            {
                if (!condition) throw new InvalidOperationException(label);
                GD.Print("PILL_OK " + label); checks++;
            }
            async Task Press(Button button)
            {
                if (!OS.GetCmdlineUserArgs().Contains("--alchemy-pill-pointer-check"))
                {
                    button.EmitSignal(BaseButton.SignalName.Pressed);
                    return;
                }
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
            _attainmentInput.Value = 68;
            ApplySampleRoute(AlchemyRecipeCatalog.Pills[0].SampleRoutes[0]);
            OpenRecipeBook();
            _recipeName.Text = "丹药说明隔离检查";
            SaveCurrentRecipe(false);
            var inputs = GetSimulationInputs().ToArray();
            double attainment = Attainment;
            string result = JsonSerializer.Serialize(AlchemySlotSimulation.Simulate(inputs, attainment));
            byte[] bytes = File.ReadAllBytes(path);
            Verify(AlchemyRecipeCatalog.Pills.Select(p => p.Id).ToHashSet().SetEquals(AlchemyPillDescriptions.Entries.Keys), "介绍与模板逐项对应");
            Verify(_savedRecipes.Count == 1, "已建立隔离丹方");
            DeleteNotebookSelection();
            Verify(_pendingDelete.Length > 0, "建立待确认删除状态");
            await Press(_pillTab);
            Verify(_showPills && !_showSaved && _selectedPillId == AlchemyRecipeCatalog.Pills[0].Id, "真实页签信号进入且默认首丹");
            Verify(_notebookList.GetChildCount() == AlchemyRecipeCatalog.Pills.Length, "目录遍历现有模板");
            Verify(!_notebookActions.Visible && !_notebookSaveRow.Visible && !_notebookStatus.Visible && _pillRule.Visible, "隐藏全部配方操作并显示通用说明");
            Verify(_pendingDelete.Length == 0 && _selectedSaved is null && _selectedRoute is null, "清除配方选择与删除确认");
            foreach (var pill in AlchemyRecipeCatalog.Pills)
            {
                await Press(_notebookList.GetNode<Button>("PillDescription_" + pill.Id));
                Verify(_selectedPillId == pill.Id, pill.Id + "目录按钮切换");
                var entry = AlchemyPillDescriptions.Entries[pill.Id];
                Verify(entry.Guidance.Contains("「" + pill.CoreUtility + "」") && entry.Guidance.Contains("「君」"), pill.Id + "指引包含真实君位核心");
                var portrait = _notebookDetail.GetNode<TextureRect>("PillPortrait");
                Verify(portrait.Texture is not null && portrait.Texture.ResourcePath == entry.TexturePath, pill.Id + "专属图已载入");
                using var pixels = portrait.Texture!.GetImage();
                Verify(pixels.GetPixel(0, 0).A == 0
                    && pixels.GetPixel(pixels.GetWidth() / 2, pixels.GetHeight() / 2).A > .9f, pill.Id + "真实透明背景与不透明丹体");
                Verify(portrait.StretchMode == TextureRect.StretchModeEnum.KeepAspectCentered, pill.Id + "图片等比完整显示");
                LoadNotebookSelection(); SaveCurrentRecipe(false); SaveCurrentRecipe(true); DeleteNotebookSelection();
                Verify(File.ReadAllBytes(path).SequenceEqual(bytes), pill.Id + "浏览和误调用不写丹方");
                Verify(GetSimulationInputs().SequenceEqual(inputs) && Attainment == attainment, pill.Id + "浏览不改投料造诣");
                Verify(JsonSerializer.Serialize(AlchemySlotSimulation.Simulate(GetSimulationInputs(), Attainment)) == result, pill.Id + "同输入结算保持一致");
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                Verify(portrait.Size.X > 0 && portrait.Size.Y == 170, pill.Id + "贴图布局不被源图撑大");
                if (OS.GetCmdlineUserArgs().Contains("--alchemy-pill-capture"))
                {
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    GetViewport().GetTexture().GetImage().SavePng("res://outputs/alchemy_pill_" + pill.Id + "_20261025.png");
                }
            }
            // 将长文测试内容限制在测试节点中，验证未来扩写仍可滚动，不改正式介绍。
            AddPillText(string.Join("\n", Enumerable.Repeat("长文滚动检查", 36)), 18);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            _notebookDetailScroll.ScrollVertical = 500;
            Verify(_notebookDetailScroll.ScrollVertical > 0, "长详情可纵向滚动");
            ShowPillDescription(AlchemyRecipeCatalog.Pills[0]);
            Verify(_notebookDetailScroll.ScrollVertical == 0, "切丹后详情回到顶部");
            ShowPillDescription(AlchemyRecipeCatalog.Pills.Last());
            _recipeOverlay.Hide(); OpenRecipeBook();
            Verify(_showPills && _selectedPillId == AlchemyRecipeCatalog.Pills.Last().Id, "关闭重开保持有效丹药选择");
            await Press(_savedTab);
            Verify(!_showPills && _showSaved && _notebookActions.Visible && _notebookSaveRow.Visible, "切回我的丹方恢复操作");
            SelectSavedRecipe(_savedRecipes.Single());
            ClearSelections(); LoadNotebookSelection();
            Verify(GetSimulationInputs().SequenceEqual(inputs), "切回后载入正常");
            OpenRecipeBook(); SelectSavedRecipe(_savedRecipes.Single());
            SaveCurrentRecipe(true);
            Verify(AlchemyRecipeStore.Load(path).Count == 1, "切回后覆盖正常");
            DeleteNotebookSelection(); DeleteNotebookSelection();
            Verify(AlchemyRecipeStore.Load(path).Count == 0, "切回后确认删除正常");
            await Press(_builtinTab);
            Verify(!_showPills && !_showSaved && _notebookActions.Visible, "切回内置样方正常");
            SelectNotebookRoute(AlchemyRecipeCatalog.Pills[0].SampleRoutes[0]);
            Verify(!_recipeLoad.Disabled, "内置样方恢复载入资格");
            GD.Print($"ALCHEMY_PILL_PASS {checks}");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            _notebookPathOverride = null;
        }
    }
}
