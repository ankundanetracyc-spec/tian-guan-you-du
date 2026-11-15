using Godot;
using Godot.Collections;

/// <summary>法宝炼制追溯记录；用于详情、修复和存档兼容。</summary>
public partial class ArtifactRecipeRecord : Resource
{
    [Export] public Array<string> material_definition_ids = [];
    [Export] public Dictionary material_slots = [];
    [Export] public string fuel_id = "";
    [Export] public Dictionary result_summary = [];
    [Export] public int created_hour;
}
