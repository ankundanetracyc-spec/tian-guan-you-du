using Godot;
using Godot.Collections;

public partial class FormationPortData : Resource
{
    public enum PortKind { INPUT, OUTPUT }
    [Export] public StringName port_id = new();
    [Export] public string display_name = "端口";
    [Export] public PortKind kind;
    [Export] public string battle_event = "";
    [Export] public Array<string> source_tags = [];
    [Export] public FormationNodeData.ActuatorEffect actuator_effect;
    [Export(PropertyHint.Range, "0,99,1")] public int minimum_energy_cost;
    [Export] public Array<string> required_material_tags = [];
}
