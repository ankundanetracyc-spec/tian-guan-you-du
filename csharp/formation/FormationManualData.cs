using Godot;
using Godot.Collections;

/// <summary>阵法录只提供感应与阵器端口；它不提供候枢、阈门等逻辑元件。</summary>
public partial class FormationManualData : Resource
{
    [Export] public StringName manual_id = new();
    [Export] public string manual_name = "无名阵法录";
    [Export(PropertyHint.MultilineText)] public string description = "";
    [Export] public Array<FormationPortData> input_ports = [];
    [Export] public Array<FormationPortData> output_ports = [];
}
