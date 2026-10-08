using Godot;
using Godot.Collections;

/// <summary>
/// 可保存的个人战斗阵图。它只描述接口、元件和脉纹；阵源余量、计数等运行状态由 FormationCircuitRuntime 持有。
/// </summary>
public partial class FormationCircuitData : Resource
{
    [Export] public StringName circuit_id = new();
    [Export] public string circuit_name = "未命名阵图";
    [Export(PropertyHint.MultilineText)] public string description = "";
    [Export(PropertyHint.Range, "0,999,1")] public float required_formation_proficiency;
    [Export] public FormationHardwareData? hardware;
    /// <summary>此阵图实际引用的阵法录；载入战斗时必须已由角色解锁。</summary>
    [Export] public Array<string> required_manual_ids = [];
    [Export] public Array<FormationNodeData> nodes = [];
    [Export] public Array<FormationWireData> pulse_wires = [];

    public Array<string> validate_circuit()
    {
        var errors = new Array<string>();
        if (circuit_id.ToString().Length == 0) errors.Add("阵图缺少稳定 ID circuit_id");
        if (hardware is null) errors.Add("阵图缺少阵盘硬件配置");
        var ids = new HashSet<string>();
        foreach (var node in nodes)
        {
            if (node is null || string.IsNullOrWhiteSpace(node.node_id)) { errors.Add("阵图存在没有 ID 的元件"); continue; }
            if (!ids.Add(node.node_id)) errors.Add($"元件 ID 重复：{node.node_id}");
        }
        foreach (var wire in pulse_wires)
            if (!ids.Contains(wire.from_node_id) || !ids.Contains(wire.to_node_id)) errors.Add($"脉纹连接了不存在的元件：{wire.from_node_id} → {wire.to_node_id}");
        if (hardware is not null && nodes.Count > hardware.effective_component_slots()) errors.Add($"元件数 {nodes.Count} 超过阵盘格位 {hardware.effective_component_slots()}");
        if (hardware is not null && complexity_cost() > hardware.effective_eye_complexity()) errors.Add($"阵眼复杂度不足：需要 {complexity_cost()}，当前 {hardware.effective_eye_complexity()}");
        return errors;
    }

    public int complexity_cost() => nodes.Sum(node => node is null ? 0 : node.complexity_cost());
}
