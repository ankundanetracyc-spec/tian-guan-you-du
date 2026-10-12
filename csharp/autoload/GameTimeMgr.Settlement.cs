using Godot;
using System;

public partial class GameTimeMgr
{
    [Signal] public delegate void smooth_time_advancedEventHandler();
    private bool _smoothAdvanceBusy;
    public bool IsSmoothAdvanceBusy => _smoothAdvanceBusy;

    // Signal-based bridge for GDScript movement: subscribe immediately after invoking this method.
    public async void advance_time_smooth(float hours, string reason = "")
    {
        if (_smoothAdvanceBusy) throw new InvalidOperationException("时间推进尚未结算");
        _smoothAdvanceBusy = true;
        try
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var manager = GetNodeOrNull<NpcWorldManager>("/root/NpcWorldManager");
            var crossingMonth = (get_total_game_hours() + (int)(_pendingHours + Math.Max(0, hours))) / 720 > get_total_game_hours() / 720;
            if (crossingMonth && manager?.simulation_enabled == true && GetTree().GetFirstNodeInGroup("action_transition") is ActionTransitionPanel panel)
            {
                await panel.run_work_async("时光流转", async progress =>
                {
                    manager.defer_time_settlement();
                    try { advance_time(hours, reason); }
                    finally { await manager.flush_time_settlement_async(progress); }
                });
            }
            else advance_time(hours, reason);
        }
        catch (Exception ex) { GD.PushError($"行旅时间结算失败：{ex}"); }
        finally { _smoothAdvanceBusy = false; EmitSignal(SignalName.smooth_time_advanced); }
    }
}
