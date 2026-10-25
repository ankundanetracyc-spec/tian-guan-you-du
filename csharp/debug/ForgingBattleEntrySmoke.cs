using Godot;
using System.Reflection;

public partial class ForgingBattleEntrySmoke : Node
{
    public override async void _Ready()
    {
        try
        {
            var model = new ProcessForgingTrial();
            var session = new ProcessForgingTrial.Session(model.Parameters.Profiles.Keys);
            var artifact = session.Submit(model, model.ExampleDraft(1), 1).Product!;
            var forge = new ProcessForgingScene(); AddChild(forge);
            if (OS.GetCmdlineUserArgs().Contains("--capture-forging-ui"))
            {
                typeof(ProcessForgingScene).GetMethod("LoadExample", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(forge, [1]);
                for (var frame = 0; frame < 8; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                DirAccess.MakeDirRecursiveAbsolute("res://docs/美术规范/炼器界面改版_20261024");
                using var image = GetViewport().GetTexture().GetImage();
                if (image.SavePng("res://docs/美术规范/炼器界面改版_20261024/01_投料.png") != Error.Ok) throw new Exception("炼器截图保存失败");
                GD.Print("FORGING_UI_CAPTURE_OK"); Input.SetCustomMouseCursor(null); GetTree().Quit(); return;
            }
            typeof(ProcessForgingScene).GetMethod("ShowArtifact", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(forge, [artifact, 0]);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            forge.FindChild("EnterEquipmentTrial", true, false)!.EmitSignal(BaseButton.SignalName.Pressed);
            for (var i = 0; i < 20; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var preview = GetTree().Root.GetChildren().OfType<ForgedEquipmentPreview>().Single();
            var manager = preview.GetNode<BattleManager>("BattleMgr");
            if (forge.Visible || manager.player_units.Count != 1) throw new Exception("对练入口未建立");
            var owner = manager.player_units[0];
            var equippedDefense = CardRules.Number(owner, "physical_defense");
            preview.FindChild("EquipSingle", true, false)!.EmitSignal(BaseButton.SignalName.Toggled, false);
            preview.StartBattle();
            if (CardRules.Number(manager.player_units[0], "physical_defense") != 5) throw new Exception("卸下后物防未恢复基线");
            preview.FindChild("EquipSingle", true, false)!.EmitSignal(BaseButton.SignalName.Toggled, true);
            preview.StartBattle();
            if (CardRules.Number(manager.player_units[0], "physical_defense") != equippedDefense) throw new Exception("重装收益重复或丢失");
            DirAccess.MakeDirRecursiveAbsolute("res://docs/美术规范/炼器对练入口_20261023");
            for (var i = 0; i < 10; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var shot = GetViewport().GetTexture().GetImage();
            shot.SavePng("res://docs/美术规范/炼器对练入口_20261023/对练.png");
            preview.Close();
            if (!forge.Visible || forge.FindChild("EnterEquipmentTrial", true, false) is null) throw new Exception("返回后未恢复成器详情");
            GD.Print("FORGING_BATTLE_ENTRY_OK: 成器按钮、真实战斗、卸下恢复、重装不叠加、返回详情");
            forge.QueueFree();
            for (var i = 0; i < 10; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Input.SetCustomMouseCursor(null); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
