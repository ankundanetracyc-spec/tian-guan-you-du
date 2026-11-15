using Godot;

public partial class FormationWorkbench
{
    private string ActiveToolName => Canvas.Tool switch
    {
        FormationBoardCanvas.EditTool.Connect => "接线",
        FormationBoardCanvas.EditTool.Signal => "信号线",
        FormationBoardCanvas.EditTool.Energy => "供能线",
        FormationBoardCanvas.EditTool.Erase => "擦除",
        FormationBoardCanvas.EditTool.Component => "放置部件",
        _ => "移动"
    };

    private void SelectWireTool(FormationBoardCanvas.EditTool tool)
    {
        ChooseTool(tool);
        Canvas.GrabFocus();
        var message = tool == FormationBoardCanvas.EditTool.Signal
            ? "信号线布置中：从亮起的信号接口或信号线开始拖动，松开完成；E切换供能线，Esc退出。"
            : "供能线布置中：从亮起的供能接口或供能线开始拖动，松开完成；Q切换信号线，Esc退出。";
        Refresh(message);
        _status.Text = message;
    }

    public override void _UnhandledKeyInput(InputEvent input)
    {
        if (!IsVisibleInTree() || input is not InputEventKey { Pressed: true, Echo: false } key ||
            key.CtrlPressed || key.AltPressed || key.MetaPressed || Canvas.InteractionInProgress) return;
        if (GodotObject.IsInstanceValid(_formationHelp) && _formationHelp!.IsVisibleInTree() ||
            GodotObject.IsInstanceValid(_draftOverlay) && _draftOverlay!.IsVisibleInTree()) return;
        var focus = GetViewport().GuiGetFocusOwner();
        if (focus is LineEdit or TextEdit || focus is not null && focus != this && !IsAncestorOf(focus)) return;
        var code = key.Keycode == Key.None ? key.PhysicalKeycode : key.Keycode;
        if (code == Key.R)
        {
            Canvas.RotateSelection(); Canvas.GrabFocus(); GetViewport().SetInputAsHandled(); return;
        }
        if (code == Key.Q)
        {
            SelectWireTool(FormationBoardCanvas.EditTool.Signal);
            GetViewport().SetInputAsHandled(); return;
        }
        if (code == Key.E)
        {
            SelectWireTool(FormationBoardCanvas.EditTool.Energy);
            GetViewport().SetInputAsHandled(); return;
        }
        if (code == Key.Escape)
        {
            ChooseTool(FormationBoardCanvas.EditTool.Select);
            _status.Text = "已退出布线。";
            GetViewport().SetInputAsHandled();
        }
    }
}
