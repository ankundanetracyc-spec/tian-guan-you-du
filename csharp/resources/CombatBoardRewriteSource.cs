using Godot;
using Godot.Collections;

[GlobalClass]
public partial class CombatBoardRewriteSource : Resource
{
    [Export] public string source_key = "";
    [Export] public string title = "";
    [Export] public string source_name = "";
    [Export] public string description = "";
    [Export] public string icon_path = "";
    [Export] public int max_placements = 1;
    [Export] public Array<string> target_keys = [];
    [Export] public Array<string> effect_ids = [];
    [Export] public Array<string> tags = [];

    public string get_board_display_name() => $"{source_name}*{max_placements}";
}
