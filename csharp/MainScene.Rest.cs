using Godot;

public partial class MainScene
{
    private RestPanel? _restPanel;
    private void open_rest_panel(string place, Action<int> confirm, int? fixedHours = null, string note = "")
    {
        if (player is null || GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager") is not { } time) return;
        if (_restPanel is null) { _restPanel = new RestPanel(); GetNode<CanvasLayer>("CanvasLayer").AddChild(_restPanel); }
        _restPanel.Open(player, time, place, confirm, fixedHours, note);
    }
}
