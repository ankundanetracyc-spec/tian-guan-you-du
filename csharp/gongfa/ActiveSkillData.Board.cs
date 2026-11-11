using Godot;

public partial class ActiveSkillData
{
    [ExportGroup("招式盘")]
    /// <summary>0表示尚无正式招式盘定义，不由旧槽位或图片推断占格。</summary>
    [Export] public int board_definition_version;
    [Export] public Vector2I board_size;
    /// <summary>本角色盘面同时可摆份数，与使用次数、冷却和每份本轮机会分开。</summary>
    [Export] public int board_max_placements;
    [Export] public bool board_rotation_allowed;
    /// <summary>化法副本仍引用原招式身份，使念、限次和冷却不因改写分裂。</summary>
    [Export] public string board_base_skill_path = "";

    public bool has_board_definition() => board_definition_version > 0
        && board_size.X > 0 && board_size.Y > 0 && board_max_placements > 0;

    public string get_board_display_name(string? name = null)
    {
        var label = name ?? skill_name;
        if (!has_board_definition()) return label;
        label += $"*{board_max_placements}";
        var battleLimit = uses_per_battle > 0 ? uses_per_battle : max_charges;
        return battleLimit > 0 ? label + $" 限{battleLimit}" : label;
    }

    public Vector2I get_board_footprint(int quarterTurns = 0)
    {
        if (!has_board_definition()) return Vector2I.Zero;
        var turns = ((quarterTurns % 4) + 4) % 4;
        if (turns != 0 && !board_rotation_allowed) return Vector2I.Zero;
        return turns % 2 == 0 ? board_size : new Vector2I(board_size.Y, board_size.X);
    }

    public string get_board_skill_key() => !string.IsNullOrWhiteSpace(board_base_skill_path)
        ? board_base_skill_path : !string.IsNullOrWhiteSpace(combat_source_key)
            ? combat_source_key : ResourcePath;
}
