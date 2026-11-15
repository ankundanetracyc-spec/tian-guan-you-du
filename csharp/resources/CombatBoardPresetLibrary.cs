using Godot;

[GlobalClass]
public partial class CombatBoardPresetLibrary : Resource
{
    [Export] public Godot.Collections.Array<CombatBoardLayout> layouts = [];
    /// <summary>局部搭配的相对坐标；局内只读取，不负责命名或写回。</summary>
    [Export] public Godot.Collections.Array<CombatBoardLayout> combinations = [];
    public CombatBoardPresetLibrary CopyConfiguration() => new()
    {
        layouts = new(layouts.Where(item => item is not null).Select(item => item.CopyConfiguration())),
        combinations = new(combinations.Where(item => item is not null).Select(item => item.CopyConfiguration()))
    };
}
