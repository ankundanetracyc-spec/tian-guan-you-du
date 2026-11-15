using Godot;

/// <summary>一份盘面对象的持久配置；不保存能刷新资源的战斗运行态。</summary>
public partial class CombatBoardPlacement : Resource
{
    public enum ObjectKind { Skill, Rewrite, ZhouTianIndicator, Pill, Talisman }

    [Export] public string placement_id = "";
    [Export] public ObjectKind kind;
    [Export] public string source_key = "";
    [Export] public Vector2I cell;
    [Export] public int quarter_turns;
    /// <summary>化法箭头的四个共边方向：右、下、左、上。</summary>
    [Export] public int rewrite_direction;
    [Export] public bool locked;
    /// <summary>消耗品已实际使用，保留空瓶或符痕占格；整理时才移出或补入。</summary>
    [Export] public bool expended;

    public CombatBoardPlacement CopyConfiguration() => new()
    {
        placement_id = placement_id, kind = kind, source_key = source_key, cell = cell,
        quarter_turns = quarter_turns, rewrite_direction = rewrite_direction, locked = locked, expended = expended
    };

    public Vector2I get_rewrite_offset() => (((rewrite_direction % 4) + 4) % 4) switch
    {
        0 => Vector2I.Right, 1 => Vector2I.Down, 2 => Vector2I.Left, _ => Vector2I.Up
    };
}
