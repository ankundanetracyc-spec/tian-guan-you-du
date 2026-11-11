using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

public partial class HoverCategoryNativeSmoke
{
    private async Task FormationEntries()
    {
        _anchor.Hide();
        var canvas = new FormationBoardCanvas { Name = "ActualFormationCanvas", Position = new(36, 88), Size = new(500, 500) }; AddChild(canvas);
        try
        {
            foreach (var (id, cell) in new[] { ("sensor", new FormationGridModel.Cell(0, 0)), ("source", new FormationGridModel.Cell(0, 3)) })
            {
                foreach (var mode in new[] { PlayerInfoText.InformationMode.Simple, PlayerInfoText.InformationMode.Detailed })
                {
                    PlayerInfoText.SetMode(mode, persist: false);
                    var point = canvas.CellPosition(cell);
                    Move(canvas.GetGlobalTransformWithCanvas() * point);
                    canvas._GuiInput(new InputEventMouseMotion { Position = point });
                    await ToSignal(GetTree().CreateTimer(.18), SceneTreeTimer.SignalName.Timeout); await Frames();
                    var description = (string)Invoke(canvas, "Describe", canvas.Model.Components[id])!;
                    Check(Compact(SharedText()).Contains(Compact(description)), "formation actual canvas hover reads its actual component parameters", SharedText());
                    await Capture(_card, "阵法_" + id + "_" + mode, "阵法部件", "正式FormationBoardCanvas鼠标处理",
                        "FormationBoardCanvas.UpdateHoverText → Describe", canvas.GetPath(), SharedText());
                }
            }
        }
        finally { HoverInfoManager.HideAll(); canvas.Free(); _anchor.Show(); }
    }
    private async Task WorldEntries()
    {
        EastWoodsDungeonRules.use_legacy_layout();
        var panel = new EastWoodsDungeonPanel { ProcessMode = ProcessModeEnum.Disabled }; AddChild(panel); panel.Hide();
        try
        {
            var resource = EastWoodsActions.ResourcePoints().First();
            typeof(EastWoodsDungeonPanel).GetField("_current", Private)!.SetValue(panel, resource.Node);
            var node = EastWoodsDungeonRules.get_node(resource.Node)!;
            await ContentSample("林地资源", "林地当前资源", "EastWoodsDungeonPanel.build_node_hover_content", resource.Node,
                () => (HoverInfoManager.HoverInfoContent)Invoke(panel, "build_node_hover_content", node)!);
            typeof(EastWoodsDungeonPanel).GetField("_current", Private)!.SetValue(panel, resource.Node == "forest_edge" ? "root_hollow" : "forest_edge");
            var hidden = (HoverInfoManager.HoverInfoContent)Invoke(panel, "build_node_hover_content", node)!;
            Check(!hidden.Body.Contains("资源类别："), "remote forest content does not expose current-node-only resource details", hidden.Body);
        }
        finally { panel.Free(); }
        _anchor.Hide();
        var view = new ForestWolfEventView { Name = "ActualForestEvent" }; AddChild(view);
        try
        {
            var wolf = new BattleEntityData { entity_name = "隔离凶狼", physique = 12, soul = 10 };
            var player = new BattleEntityData { entity_name = "隔离观察者", physique = _owner.physique, soul = _owner.soul, intelligence = _owner.intelligence };
            var action = EastWoodsActions.Content(ForestWolfEvent.NodeId) ?? throw new Exception("missing authored wolf action");
            var record = ForestWolfEvent.Create(action, wolf);
            PlayerInfoText.SetMode(PlayerInfoText.InformationMode.Simple, persist: false);
            var page = ForestWolfEvent.Describe(record, player); view.Display(page); await Frames();
            var observe = Nodes<Button>(view).Single(button => button.Name == "Choice_observe");
            var option = page.Choices.Single(choice => choice.Id == "observe");
            foreach (var mode in new[] { PlayerInfoText.InformationMode.Simple, PlayerInfoText.InformationMode.Detailed })
            {
                PlayerInfoText.SetMode(mode, persist: false);
                await NativeSource(observe, "actual forest event choice"); observe.EmitSignal(Control.SignalName.MouseEntered); await Frames();
                var info = new HoverInfoManager.HoverInfoContent(option.Text, option.Hint);
                VerifyShared("林地事件" + mode, info);
                Check(mode == PlayerInfoText.InformationMode.Simple ? !SharedText().Contains("成功率") : SharedText().Contains("成功率") && SharedText().Contains("min("),
                    "real event choice retains complete detailed probability source while hiding it in simple mode");
                await Capture(_card, "林地事件_" + mode, "林地事件", "正式ForestWolfEventView选项按钮",
                    "ForestWolfEvent.Describe → ForestWolfEventView.Display", observe.GetPath(), SharedText());
            }
            PlayerInfoText.SetMode(PlayerInfoText.InformationMode.Simple, persist: false); await Frames();
            Check(_card.IsVisibleInTree() && !SharedText().Contains("计算来源"), "open authored event hover refreshes mode without reconstructing its page");
            var half = ForestWolfEvent.Create(action, new BattleEntityData { soul = 7 });
            var halfHint = ForestWolfEvent.Describe(half, new BattleEntityData { soul = 1, intelligence = 1 }).Choices.Single(choice => choice.Id == "observe").Hint;
            Check(Math.Abs(ForestWolfEvent.Chance(half, "observe", new BattleEntityData { soul = 1, intelligence = 1 }) - .125) < .00001
                && PlayerInfoText.Detailed(halfHint).Contains("13%"), "event 12.5-percent display preserves its original away-from-zero thirteen-percent result");
        }
        finally { HoverInfoManager.HideAll(); view.Free(); _anchor.Show(); }
    }
}
