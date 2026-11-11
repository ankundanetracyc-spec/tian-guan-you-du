using Godot;
using Cell = FormationGridModel.Cell;
using Direction = FormationGridModel.Direction;
using PortKind = FormationGridModel.PortKind;

/// <summary>可直接运行的设计台及隔离验收，不读写人物存档。</summary>
public partial class FormationWorkbenchPreview : Control
{
    private FormationWorkbench _workbench = null!;
    private int _checks;
    private const string Evidence = "res://docs/美术规范/阵盘俯视电路重绘_20261108";

    public override async void _Ready()
    {
        GetWindow().Mode = Window.ModeEnum.Windowed;
        GetWindow().Size = new Vector2I(1280, 900);
        GetWindow().ContentScaleSize = new Vector2I(1280, 900);
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var background = new ColorRect { Color = new Color("242a2e"), MouseFilter = MouseFilterEnum.Ignore };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(background);
        var margin = new MarginContainer(); margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 34); margin.AddThemeConstantOverride("margin_right", 34);
        margin.AddThemeConstantOverride("margin_top", 24); margin.AddThemeConstantOverride("margin_bottom", 24); AddChild(margin);
        _workbench = new FormationWorkbench(); margin.AddChild(_workbench);
        if (!OS.GetCmdlineUserArgs().Contains("--formation-smoke")) return;
        GetWindow().Position = new Vector2I(-30000, -30000);
        try
        {
            _workbench.DraftPath = "res://output/formation_grid_smoke/isolated_draft.json";
            await Frames(5);
            Check(_workbench.Canvas.Model.IsCircuitConnected, "基础示例两网接通");
            Check(_workbench.Canvas.Model.Wires.Count == 5, "4×4示例只有五格线路");
            Check(_workbench.Canvas.Model.Complexity == 2, "真实内置护持复杂度2");
            Check(!_workbench.Canvas.GuidesVisible, "空闲盘面隐藏格位和端口辅助");
            await Capture("01_基础护持连通.png");
            SelectTool("ToolSignal");
            Check(!_workbench.Canvas.GuidesVisible, "只选工具不常驻格线");
            _workbench.Canvas._GuiInput(new InputEventMouseMotion { Position = _workbench.Canvas.CellPosition(new Cell(2,1)) });
            Check(_workbench.Canvas.GuidesVisible, "指向盘面布线时显示辅助格位");
            await Capture("05_布线辅助格位.png");
            _workbench.Canvas._GuiInput(new InputEventMouseMotion { Position = new Vector2(-20,-20) });
            Check(!_workbench.Canvas.GuidesVisible, "退出盘面后收起格位");
            SelectTool("ToolSelect");
            var heldPart = _workbench.Canvas.CellPosition(new Cell(0,0));
            var beforeGrab = _workbench.Canvas.Model.Serialize();
            _workbench.Canvas._GuiInput(new InputEventMouseButton { Position = heldPart, ButtonIndex = MouseButton.Left, Pressed = true });
            Check(_workbench.Canvas.GuidesVisible, "抓取部件显示临时占格");
            _workbench.Canvas._GuiInput(new InputEventMouseButton { Position = heldPart, ButtonIndex = MouseButton.Left, Pressed = false });
            Check(!_workbench.Canvas.GuidesVisible && beforeGrab == _workbench.Canvas.Model.Serialize(), "放回原处收起辅助且不改线路");
            SelectTool("ToolEnergy");
            Click(new Cell(2, 3), MouseButton.Right);
            Check(!_workbench.Canvas.Model.IsCircuitConnected, "实际右键删除供能后断路");
            Click(new Cell(2, 3));
            Check(_workbench.Canvas.Model.IsCircuitConnected, "实际铺线恢复供能");
            var serialized = _workbench.Canvas.Model.Serialize();
            Click(new Cell(1, 0));
            Check(serialized == _workbench.Canvas.Model.Serialize(), "异类线交叉原子拒绝");
            Check(_workbench.SaveDraft(), "隔离保存草图");
            _workbench.Canvas.SetModel(new FormationGridModel());
            Check(_workbench.LoadDraft() && _workbench.Canvas.Model.IsCircuitConnected, "草图重载保留实际连接");
            System.IO.File.WriteAllText(ProjectSettings.GlobalizePath(_workbench.DraftPath), "{broken");
            var beforeBadLoad = _workbench.Canvas.Model.Serialize();
            Check(!_workbench.LoadDraft() && _workbench.Canvas.Model.Serialize() == beforeBadLoad, "坏草图不覆盖当前盘面");
            _workbench.Canvas.SetModel(new FormationGridModel()); _workbench.Refresh("线路形态示意：相邻连接自动变形。");
            SelectTool("ToolSignal");
            foreach (var cell in new[] { new Cell(0,0), new Cell(1,0), new Cell(2,0), new Cell(2,1), new Cell(2,2), new Cell(3,2) }) Click(cell);
            Check(_workbench.Canvas.Model.GetConnectionMask(new Cell(1,0)) == (Direction.West | Direction.East), "直线显示双向连接");
            Check(_workbench.Canvas.Model.GetConnectionMask(new Cell(2,0)) == (Direction.West | Direction.South), "转角显示实际连接");
            SelectTool("ToolEnergy");
            foreach (var cell in new[] { new Cell(0,3), new Cell(1,3), new Cell(2,3), new Cell(1,2) }) Click(cell);
            Check(_workbench.Canvas.Model.GetConnectionMask(new Cell(1,3)) == (Direction.West | Direction.East | Direction.North), "供能同网T形连接");
            _workbench.Refresh("连接形态：细信号线与粗供能线各自成路；此图尚未装齐部件。");
            await Capture("02_直线转角与分支.png");
            _workbench.Canvas.SetModel(new FormationGridModel());
            SelectTool("ToolSignal");
            foreach (var cell in new[] { new Cell(0,2), new Cell(1,2), new Cell(2,2) }) Click(cell);
            var beforeBranch = _workbench.Canvas.Model.Serialize(); Click(new Cell(1,3));
            Check(beforeBranch == _workbench.Canvas.Model.Serialize(), "实际铺线拒绝隐性信号分流");
            _workbench.Canvas.SetModel(FormationGridModel.CreateStarter());
            SelectTool("ToolEnergy"); Click(new Cell(1,0));
            await Capture("03_交叉被拒绝.png");
            var archive = new BaigongPanel(); AddChild(archive);
            var owner = new Character(); archive.show_for(owner);
            await Frames(5);
            var embedded = archive.FindChild("FormationWorkbench", true, false) as FormationWorkbench;
            Check(embedded is not null && embedded.Canvas.Model.IsCircuitConnected, "百工阵法入口接入同一设计台");
            Check(embedded is not null && GetViewport().GetVisibleRect().Encloses(embedded.GetGlobalRect()), "百工内嵌设计台在窗口内完整显示");
            var title = embedded?.FindChild("WorkbenchTitle", true, false) as Label;
            Check(title is not null && title.Size.X >= title.GetMinimumSize().X && title.Size.Y < 50, "标题横向显示且不挤压画布");
            await Capture("04_百工阵法入口.png");
            archive.QueueFree(); owner.Free();
            GD.Print($"FORMATION_WORKBENCH_SMOKE_PASS checks={_checks}"); GetTree().Quit();
        }
        catch (Exception exception) { GD.PushError("FORMATION_WORKBENCH_SMOKE_FAIL " + exception); GetTree().Quit(1); }
    }

    private void SelectTool(string name)
    {
        var button = _workbench.FindChild(name, true, false) as Button;
        if (button is null) throw new Exception("缺少按钮 " + name);
        button.EmitSignal(BaseButton.SignalName.Pressed);
    }
    private void Click(Cell cell, MouseButton button = MouseButton.Left)
    {
        var position = _workbench.Canvas.CellPosition(cell);
        _workbench.Canvas._GuiInput(new InputEventMouseButton { Position = position, ButtonIndex = button, Pressed = true });
        _workbench.Canvas._GuiInput(new InputEventMouseButton { Position = position, ButtonIndex = button, Pressed = false });
    }
    private void Check(bool condition, string message)
    {
        _checks++; if (!condition) throw new Exception(message); GD.Print("FORMATION_CHECK " + message);
    }
    private async System.Threading.Tasks.Task Frames(int count)
    {
        for (var i=0;i<count;i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
    private async System.Threading.Tasks.Task Capture(string file)
    {
        await Frames(3);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(Evidence));
        using var image = GetViewport().GetTexture().GetImage();
        Check(image is not null && !image.IsEmpty() && image.SavePng(Evidence + "/" + file) == Error.Ok, "实际画面截图 " + file);
    }
}
