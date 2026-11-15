using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

public partial class NpcWorldManager
{
    public bool IsSettling { get; private set; }
    public bool HasPendingSettlement => _deferSettlement || IsSettling;
    public double LastSettlementMaxSliceMilliseconds { get; private set; }
    private bool _deferSettlement, _settlementBatch, _settlementChanged;
    private bool _eastWoodsRouteSignalBatch, _eastWoodsRouteSignalPending;
    private Dictionary<string, WorldNpcData[]>? _monthlyResidents;
    private readonly Dictionary<(Vector2I From, Vector2I To), Vector2I[]> _settlementPaths = new();
    private readonly Dictionary<(Vector2I Cell, float Speed), double> _settlementTravelHours = new();
    private readonly Dictionary<(Vector2I From, Vector2I To, float Speed), double> _settlementEdgeTravelHours = new();

    public void begin_east_woods_route_signal_batch()
    {
        if (_eastWoodsRouteSignalBatch) return;
        _eastWoodsRouteSignalBatch = true;
        _eastWoodsRouteSignalPending = false;
    }

    public void end_east_woods_route_signal_batch()
    {
        if (!_eastWoodsRouteSignalBatch) return;
        var changed = _eastWoodsRouteSignalPending;
        _eastWoodsRouteSignalBatch = _eastWoodsRouteSignalPending = false;
        if (changed) EmitSignal(SignalName.world_npcs_changed);
    }

    private void notify_east_woods_route_changed()
    {
        if (_eastWoodsRouteSignalBatch)
        {
            _eastWoodsRouteSignalPending = true;
            return;
        }
        EmitSignal(SignalName.world_npcs_changed);
    }

    private void begin_settlement_batch()
    {
        _settlementBatch = true;
        _settlementChanged = false;
        _settlementPaths.Clear(); _settlementTravelHours.Clear(); _settlementEdgeTravelHours.Clear();
    }

    private void notify_settlement_changed()
    {
        if (_settlementBatch) _settlementChanged = true;
        else EmitSignal(SignalName.world_npcs_changed);
    }

    private void end_settlement_batch()
    {
        var changed = _settlementChanged;
        _settlementBatch = _settlementChanged = false;
        _monthlyResidents = null;
        _settlementPaths.Clear(); _settlementTravelHours.Clear(); _settlementEdgeTravelHours.Clear();
        if (changed) EmitSignal(SignalName.world_npcs_changed);
    }

    // Used only under the existing blocking long-action transition. Small movements remain synchronous.
    public void defer_time_settlement()
    {
        if (HasPendingSettlement) throw new InvalidOperationException("NPC settlement already active");
        _deferSettlement = true;
    }

    public async Task flush_time_settlement_async(Action<double, string>? progress = null)
    {
        if (!_deferSettlement) return;
        IsSettling = true;
        begin_settlement_batch();
        try
        {
            var time = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
            if (time is null) return;
            var target = month_index(time);
            var initialMonth = _last_processed_month;
            var budget = Stopwatch.StartNew();
            LastSettlementMaxSliceMilliseconds = 0;
            foreach (var _ in settle_time_steps(time.get_total_game_hours(), target))
            {
                if (budget.Elapsed.TotalMilliseconds < 4) continue;
                LastSettlementMaxSliceMilliseconds = Math.Max(LastSettlementMaxSliceMilliseconds, budget.Elapsed.TotalMilliseconds);
                progress?.Invoke(Math.Clamp((_last_processed_month - initialMonth) / (double)Math.Max(1, target - initialMonth), 0, .99), "正在推演人物与行程…");
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                budget.Restart();
            }
            LastSettlementMaxSliceMilliseconds = Math.Max(LastSettlementMaxSliceMilliseconds, budget.Elapsed.TotalMilliseconds);
            progress?.Invoke(1, "人物与行程已结算");
        }
        finally
        {
            _deferSettlement = false; IsSettling = false;
            end_settlement_batch();
        }
    }
}
