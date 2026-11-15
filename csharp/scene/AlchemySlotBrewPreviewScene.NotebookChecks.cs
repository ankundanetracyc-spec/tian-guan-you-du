using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.IO;

public partial class AlchemySlotBrewPreviewScene
{
    private void RunNotebookRestartCheck()
    {
        var path = ProjectSettings.GlobalizePath("res://outputs/alchemy_notebook_restart_fixture.json");
        try
        {
            if (OS.GetCmdlineUserArgs().Contains("--alchemy-notebook-restart-write"))
            {
                if (System.IO.File.Exists(path)) throw new InvalidOperationException("测试文件已存在。");
                AlchemyRecipeStore.Save(path, [new("restart", "跨进程丹方", [new("mat_spirit_stone", 2, -1)])]);
                GD.Print("NOTEBOOK_RESTART_WRITE_PASS");
            }
            else
            {
                var recipe = AlchemyRecipeStore.Load(path).Single();
                if (recipe.Name != "跨进程丹方" || recipe.Ingredients.Single() != new AlchemyRecipeStore.Entry("mat_spirit_stone", 2, -1))
                    throw new InvalidOperationException("跨进程读回不一致。");
                System.IO.File.Delete(path);
                GD.Print("NOTEBOOK_RESTART_READ_PASS");
            }
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private async Task RunNotebookChecks()
    {
        var testPath = ProjectSettings.GlobalizePath("user://alchemy/notebook-check-" + Guid.NewGuid().ToString("N") + ".json");
        _notebookPathOverride = testPath;
        try
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            _tutorialOverlay.Hide();
            int checks = 0;
            void Verify(bool condition, string label)
            {
                if (!condition) throw new InvalidOperationException(label);
                checks++; GD.Print("NOTEBOOK_OK " + label);
            }
            OpenRecipeBook();
            _recipeName.Text = "空方";
            SaveCurrentRecipe(false);
            Verify(!System.IO.File.Exists(testPath), "无君药不保存");
            _attainmentInput.Value = 68;
            var route = AlchemyRecipeCatalog.Pills[0].SampleRoutes[0];
            ApplySampleRoute(route);
            var original = GetSimulationInputs();
            _recipeName.Text = "本地丹方测试";
            SaveCurrentRecipe(false);
            Verify(AlchemyRecipeStore.Load(testPath).Count == 1, "创建并从磁盘读回");
            ClearSelections();
            _savedRecipes.Clear();
            OpenRecipeBook();
            SelectSavedRecipe(_savedRecipes.Single());
            LoadNotebookSelection();
            Verify(GetSimulationInputs().SequenceEqual(original), "重新打开后载入同药位份数");
            var bytes = System.IO.File.ReadAllText(testPath);
            SaveCurrentRecipe(false);
            Verify(System.IO.File.ReadAllText(testPath) == bytes, "同名普通保存不覆盖");
            _assistantDoses[3] = 2;
            SaveCurrentRecipe(true);
            Verify(AlchemyRecipeStore.Load(testPath).Single().Ingredients.Single(i => i.Slot == 3).Dose == 2,
                "显式同名覆盖");
            OpenRecipeBook();
            SelectSavedRecipe(_savedRecipes.Single());
            _attainmentInput.Value = 8;
            // Open again refreshes eligibility; source data remains unchanged.
            OpenRecipeBook(); SelectSavedRecipe(_savedRecipes.Single());
            Verify(_recipeLoad.Disabled, "造诣不足禁止载入");
            DeleteNotebookSelection();
            Verify(AlchemyRecipeStore.Load(testPath).Count == 1, "删除先确认");
            DeleteNotebookSelection();
            Verify(AlchemyRecipeStore.Load(testPath).Count == 0, "确认后删除写盘");
            System.IO.File.WriteAllText(testPath, "{bad-json");
            OpenRecipeBook(); SaveCurrentRecipe(true);
            Verify(System.IO.File.ReadAllText(testPath) == "{bad-json" && _notebookError.Length > 0, "损坏文件保留不覆写");
            System.IO.File.Delete(testPath);
            var invalid = new AlchemyRecipeStore.Recipe("bad", "坏方",
                [new("same", 4, -1), new("same", 4, 0), new("extra", 1, 1)]);
            bool rejected = false;
            try { AlchemyRecipeStore.Save(testPath, [invalid]); } catch (InvalidDataException) { rejected = true; }
            Verify(rejected && !System.IO.File.Exists(testPath), "非法重复或超量配方不写盘");
            OpenRecipeBook();
            _attainmentInput.Value = 36;
            ApplySampleRoute(AlchemyRecipeCatalog.Pills[0].SampleRoutes.Last());
            _recipeName.Text = "青芽试录";
            SaveCurrentRecipe(false);
            if (OS.GetCmdlineUserArgs().Contains("--alchemy-notebook-capture"))
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                var image = GetViewport().GetTexture().GetImage();
                image.SavePng("res://outputs/alchemy_notebook_20261022.png");
                _showSaved = false;
                RefreshNotebookList();
                SelectNotebookRoute(AlchemyRecipeCatalog.Pills[0].SampleRoutes.Last());
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                GetViewport().GetTexture().GetImage().SavePng("res://outputs/alchemy_notebook_builtin_20261022.png");
                _recipeOverlay.Hide();
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                GetViewport().GetTexture().GetImage().SavePng("res://outputs/alchemy_notebook_entry_20261022.png");
            }
            GD.Print($"ALCHEMY_NOTEBOOK_PASS {checks}");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
        finally
        {
            if (System.IO.File.Exists(testPath)) System.IO.File.Delete(testPath);
            _notebookPathOverride = null;
        }
    }
}
