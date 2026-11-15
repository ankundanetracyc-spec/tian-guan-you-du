using Godot;
using Godot.Collections;

public partial class FormationNodeData : Resource
{
    // DIVIDER编号3保留旧资源兼容，新计数器用COUNTER及first_immediate。
    public enum NodeKind { INPUT, THRESHOLD, COUNTER, DIVIDER, SPLITTER, MERGER, ACTUATOR }
    public enum ActuatorEffect { SHIELD, RESTORE_MANA, MAGIC_DAMAGE }
    [Export] public StringName node_id = new();
    [Export] public string display_name = "元件";
    [Export] public NodeKind kind;
    [Export] public string input_event = "";
    [Export] public Array<string> accepted_source_tags = [];
    [Export(PropertyHint.Range, "1,9,1")] public int minimum_amplitude = 1;
    [Export(PropertyHint.Range, "1,12,1")] public int interval = 2;
    [Export] public bool first_immediate;
    [Export(PropertyHint.Range, "0,99,1")] public int energy_cost;
    [Export(PropertyHint.Range, "0,99,1")] public int peak_load;
    [Export(PropertyHint.Range, "0,99,1")] public int priority;
    [Export(PropertyHint.Range, "0,99,1")] public int effect_value = 1;
    [Export] public ActuatorEffect actuator_effect;
    [Export] public bool once_per_owner_turn = true;
    [Export] public Array<string> required_material_tags = [];
    public int complexity_cost() => kind switch
    {
        NodeKind.INPUT or NodeKind.SPLITTER or NodeKind.MERGER or NodeKind.THRESHOLD => 1,
        NodeKind.COUNTER or NodeKind.DIVIDER => 2,
        NodeKind.ACTUATOR => 1, _ => 99
    };
}
