using Godot;
using Godot.Collections;

/// <summary>单场战斗中的阵图解释器。阵脉在同一事件内流动，绝不计入任何可储存资源。</summary>
public sealed class FormationCircuitRuntime
{
    private readonly FormationCircuitData _data;
    private readonly GodotObject _owner;
    private readonly BattleManager _battle;
    private readonly System.Collections.Generic.Dictionary<string, FormationNodeData> _nodes = [];
    private readonly System.Collections.Generic.Dictionary<string, int> _counters = [];
    private readonly HashSet<string> _firedThisTurn = [];
    private int _sourceEnergy;

    private readonly record struct Pulse(string SourceTag, int Amplitude);

    public FormationCircuitRuntime(FormationCircuitData data, GodotObject owner, BattleManager battle)
    {
        _data = data; _owner = owner; _battle = battle;
        foreach (var node in data.nodes) if (node is not null && node.node_id.ToString().Length > 0) _nodes[node.node_id] = node;
        _sourceEnergy = Mathf.Max(0, data.hardware?.effective_source_capacity() ?? 0);
    }

    public Dictionary snapshot() => new()
    {
        ["circuit_id"] = _data.circuit_id.ToString(), ["circuit_name"] = _data.circuit_name,
        ["source_energy"] = _sourceEnergy, ["source_capacity"] = _data.hardware?.effective_source_capacity() ?? 0,
        ["valid"] = _data.validate_circuit().Count == 0
    };

    public void begin_owner_turn()
    {
        var hardware = _data.hardware; if (hardware is null) return;
        _firedThisTurn.Clear();
        _sourceEnergy = Mathf.Min(hardware.effective_source_capacity(), _sourceEnergy + hardware.effective_source_recharge());
        emit("turn_start", "turn", 1);
    }

    public void emit(string eventId, string sourceTag, int amplitude = 1)
    {
        if (_data.hardware is null || _data.validate_circuit().Count > 0) return;
        var pending = new Queue<(string nodeId, Pulse pulse)>();
        foreach (var node in _nodes.Values.Where(n => n.kind == FormationNodeData.NodeKind.INPUT && n.input_event == eventId && (n.accepted_source_tags.Count == 0 || n.accepted_source_tags.Contains(sourceTag))))
            forward(node.node_id.ToString(), new Pulse(sourceTag, Mathf.Max(1, amplitude)), pending);

        // 每次战斗事件限制为 128 跳，防止损坏或未来反馈阵图卡住主战斗循环。
        for (var hops = 0; pending.Count > 0 && hops < 128; hops++)
        {
            var (id, pulse) = pending.Dequeue();
            if (!_nodes.TryGetValue(id, out var node)) continue;
            switch (node.kind)
            {
                case FormationNodeData.NodeKind.THRESHOLD when pulse.Amplitude >= node.minimum_amplitude:
                    forward(id, pulse, pending); break;
                case FormationNodeData.NodeKind.COUNTER:
                    _counters[id] = _counters.GetValueOrDefault(id) + 1;
                    if (_counters[id] >= Mathf.Max(1, node.interval)) { _counters[id] = 0; forward(id, pulse, pending); }
                    break;
                case FormationNodeData.NodeKind.DIVIDER:
                    _counters[id] = _counters.GetValueOrDefault(id) + 1;
                    if ((_counters[id] - 1) % Mathf.Max(1, node.interval) == 0) forward(id, pulse, pending);
                    break;
                case FormationNodeData.NodeKind.SPLITTER or FormationNodeData.NodeKind.MERGER:
                    forward(id, pulse, pending); break;
                case FormationNodeData.NodeKind.ACTUATOR:
                    try_activate(node);
                    break;
            }
        }
        if (pending.Count > 0) GD.PushWarning($"阵图 {_data.circuit_name} 的本次脉冲超过安全跳数，已切断。");
    }

    private void forward(string from, Pulse pulse, Queue<(string nodeId, Pulse pulse)> pending)
    {
        foreach (var wire in _data.pulse_wires.Where(w => w is not null && w.from_node_id.ToString() == from)) pending.Enqueue((wire.to_node_id.ToString(), pulse));
    }

    private void try_activate(FormationNodeData actuator)
    {
        var hardware = _data.hardware!;
        if (actuator.once_per_owner_turn && _firedThisTurn.Contains(actuator.node_id.ToString())) return;
        var demand = Mathf.Max(actuator.energy_cost, actuator.peak_load);
        // 控制信号到达不等于成功：阵源、灵络和阵盘负荷必须同时允许。
        if (_sourceEnergy < actuator.energy_cost || demand > hardware.effective_source_peak() || demand > hardware.effective_link_peak() || demand > hardware.effective_board_load()) return;
        _sourceEnergy -= actuator.energy_cost;
        _firedThisTurn.Add(actuator.node_id.ToString());
        _battle.resolve_formation_actuator(_owner, actuator);
    }
}
