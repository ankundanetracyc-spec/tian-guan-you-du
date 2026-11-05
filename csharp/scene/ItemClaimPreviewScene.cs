using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

/// <summary>Interactive preview using a disposable character, never the active save.</summary>
public partial class ItemClaimPreviewScene : Control
{
    private Character? _preview;
    public override void _Ready()
    {
        _preview = new Character { entity_data = new HumanData { entity_name = "领取界面预览" } };
        _preview.add_item_to_inventory("res://resources/entities/items/玄枢含章经秘籍.tres");
        _preview.add_item_to_inventory("res://resources/entities/items/灵石.tres", 8);
        _preview.add_item_to_inventory("res://resources/entities/items/玄铁.tres");
        _preview.add_item_to_inventory("res://resources/entities/items/alchemy/huichun_2.tres");
        var rewards = new GArray();
        foreach (var (path, count) in new[] {
            ("res://resources/entities/items/alchemy/huichun_2.tres", 2),
            ("res://resources/entities/items/灵石.tres", 6), ("res://resources/entities/items/玄铁.tres", 1),
            ("res://resources/entities/items/玄枢含章经秘籍.tres", 1), ("res://resources/entities/items/兽皮.tres", 2) })
            rewards.Add(new GDictionary { ["item_path"] = path, ["count"] = count });
        var panel = new ItemClaimPanel(); AddChild(panel); panel.Present(_preview, rewards);
        panel.Closed += () => GetTree().ChangeSceneToFile("res://tscn/scene/StartMenu.tscn");
    }
    public override void _ExitTree() { _preview?.Free(); }
}
