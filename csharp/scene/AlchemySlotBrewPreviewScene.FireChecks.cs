using Godot;
using System;
using System.Threading.Tasks;
using System.Linq;

public partial class AlchemySlotBrewPreviewScene
{
    private async Task RunFireChecks()
    {
        try
        {
            _tutorialOverlay.Hide();
            bool layoutCapture = OS.GetCmdlineUserArgs().Contains("--alchemy-layout-capture");
            if (layoutCapture)
            {
                _attainmentInput.Value = 100;
                ApplySampleRoute(AlchemyRecipeCatalog.Pills[0].SampleRoutes.Last());
            }
            var furnace = _canvas.GetNode<AlchemyFurnaceView>("ApprovedTwoLayerFurnace");
            if (!furnace.UseTexturedFire || furnace.MouseFilter != MouseFilterEnum.Ignore)
                throw new InvalidOperationException("贴图火焰未启用或拦截界面输入。");
            await ToSignal(GetTree().CreateTimer(.8), SceneTreeTimer.SignalName.Timeout);
            float firstTime = furnace.FireAnimationTime;
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            var first = GetViewport().GetTexture().GetImage();
            first.SavePng("res://outputs/alchemy_fire_static_a_20261025.png");
            await ToSignal(GetTree().CreateTimer(.55), SceneTreeTimer.SignalName.Timeout);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            var second = GetViewport().GetTexture().GetImage();
            second.SavePng("res://outputs/alchemy_fire_static_b_20261025.png");
            if (furnace.FireAnimationTime != firstTime || furnace.IsProcessing())
                throw new InvalidOperationException("静态火焰仍在推进动画。");
            if (!first.GetData().AsSpan().SequenceEqual(second.GetData()))
                throw new InvalidOperationException("静态火焰两帧画面不一致。");
            if (layoutCapture)
            {
                first.SavePng("res://outputs/alchemy_layout_four_20261025.png");
                _attainmentInput.Value = 508;
                var choices = _materials.Where(m => m.Path != _main!.Path).Take(7).ToArray();
                _mainDose = 1;
                for (int i = 0; i < 7; i++) { _assistants[i] = choices[i]; _assistantDoses[i] = 1; }
                RefreshPage();
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                GetViewport().GetTexture().GetImage().SavePng("res://outputs/alchemy_layout_eight_20261025.png");
            }
            if (OS.GetCmdlineUserArgs().Contains("--alchemy-sidebar-capture"))
            {
                _canvas.GetNode<Button>("ToggleIngredientLedger").EmitSignal(Button.SignalName.Pressed);
                _canvas.GetNode<Button>("ToggleBatchRequirements").EmitSignal(Button.SignalName.Pressed);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                GetViewport().GetTexture().GetImage().SavePng("res://outputs/alchemy_sidebars_collapsed_20261025.png");
            }
            GD.Print("ALCHEMY_STATIC_FIRE_PASS");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
