using Godot;
using Godot.Collections;

/// <summary>单场战斗中的阵图解释器；复用设计台的基础信号逻辑，阵能另行结算。</summary>
public sealed class FormationCircuitRuntime
{
    private readonly FormationCircuitData _data;
    private readonly GodotObject _owner;
    private readonly BattleManager _battle;
    private readonly System.Collections.Generic.Dictionary<string, FormationNodeData> _nodes = [];
    private readonly HashSet<string> _firedThisTurn = [];
    private readonly FormationLogicCircuit? _logic;
    private int _sourceEnergy;
    private long _eventSequence;

    public FormationCircuitRuntime(FormationCircuitData data, GodotObject owner, BattleManager battle)
    {
        _data = data; _owner = owner; _battle = battle;
        _sourceEnergy = Mathf.Max(0, data.hardware?.effective_source_capacity() ?? 0);
        if (data.validate_circuit().Count > 0) return;
        foreach (var node in data.nodes) _nodes.Add(node.node_id.ToString(), node);
        _logic = new FormationLogicCircuit(data.logic_nodes(), data.logic_edges());
    }

    public Dictionary snapshot()
    {
        var counts = new Dictionary();
        if (_logic is not null)
            foreach (var node in _logic.Nodes.Values.Where(n => n.Kind == FormationLogicCircuit.NodeKind.Counter))
                counts[node.Id] = _logic.GetCount(node.Id);
        return new Dictionary
        {
            ["circuit_id"] = _data.circuit_id.ToString(), ["circuit_name"] = _data.circuit_name,
            ["source_energy"] = _sourceEnergy, ["source_capacity"] = _data.hardware?.effective_source_capacity() ?? 0,
            ["valid"] = _logic is not null && _data.validate_circuit().Count == 0, ["logic_counts"] = counts
        };
    }

    public void begin_owner_turn()
    {
        var hardware = _data.hardware;
        if (hardware is null || _logic is null || _data.validate_circuit().Count > 0) return;
        _firedThisTurn.Clear();
        _sourceEnergy = Mathf.Min(hardware.effective_source_capacity(), _sourceEnergy + hardware.effective_source_recharge());
        // 只沿用原有自身行动事件；计数与分频进度跨行动保留。
        emit("turn_start", "turn", 1);
    }

    /// <param name="occurrenceId">可选的真实发生身份；eventId仍是事件类型，不能用来去重。</param>
    public void emit(string eventId, string sourceTag, int amplitude = 1, string? occurrenceId = null)
    {
        if (_logic is null || _data.validate_circuit().Count > 0) return;
        FormationLogicCircuit.RunResult result;
        try
        {
            var identity = occurrenceId is null ? "generated:" + ++_eventSequence : "observed:" + occurrenceId;
            result = _logic.Emit(new FormationLogicCircuit.Signal(identity, eventId, sourceTag, amplitude));
        }
        catch (ArgumentException exception) { GD.PushWarning("阵法信号已拒绝：" + exception.Message); return; }
        var committedLoad = 0;
        foreach (var id in result.OutputNodeIds) try_activate(_nodes[id], ref committedLoad);
    }

    private void try_activate(FormationNodeData actuator, ref int committedLoad)
    {
        var hardware = _data.hardware!;
        var id = actuator.node_id.ToString();
        if (actuator.once_per_owner_turn && _firedThisTurn.Contains(id)) return;
        var demand = Mathf.Max(actuator.energy_cost, actuator.peak_load);
        var totalLoad = committedLoad + demand;
        if (_sourceEnergy < actuator.energy_cost || totalLoad > hardware.effective_source_peak() ||
            totalLoad > hardware.effective_link_peak() || totalLoad > hardware.effective_board_load()) return;

        // 先预留，防止效果入口重入时借用同一笔阵能；实际提交失败再撤销预留。
        _sourceEnergy -= actuator.energy_cost;
        committedLoad += demand;
        var marked = _firedThisTurn.Add(id);
        var committed = false;
        try { committed = _battle.try_resolve_formation_actuator(_owner, actuator); }
        finally
        {
            if (!committed)
            {
                _sourceEnergy += actuator.energy_cost; committedLoad -= demand;
                if (marked) _firedThisTurn.Remove(id);
            }
        }
    }
}
