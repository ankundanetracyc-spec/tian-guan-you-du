using Godot;
using Godot.Collections;

public partial class BattleStatusPanel : Control
{
    [Export] public PackedScene? status_bar_scene;
    private VBoxContainer? left_container;
    private VBoxContainer? right_container;
    private readonly Dictionary player_bars = [];
    private readonly Dictionary enemy_bars = [];

    public override void _Ready() { AddToGroup("battle_status_panel"); left_container = GetNodeOrNull<VBoxContainer>("LeftContainer"); right_container = GetNodeOrNull<VBoxContainer>("RightContainer"); }
    public void add_unit_bar(GodotObject unit)
    {
        if (!GodotObject.IsInstanceValid(unit) || player_bars.ContainsKey(unit) || enemy_bars.ContainsKey(unit) || status_bar_scene is null) return;
        var bar = status_bar_scene.Instantiate<Node>(); bar.Call("setup", unit);
        var player = unit.Get("faction").AsInt32() == 1;
        (player ? left_container : right_container)?.AddChild(bar); (player ? player_bars : enemy_bars)[unit] = bar;
    }
    public void remove_unit_bar(GodotObject unit) { if (!remove_from(player_bars, unit)) remove_from(enemy_bars, unit); }
    public void refresh_all() { clean_and_refresh(player_bars); clean_and_refresh(enemy_bars); }
    public void clear_all() { clear(player_bars); clear(enemy_bars); }
    private static bool remove_from(Dictionary bars, GodotObject unit)
    { if (!bars.TryGetValue(unit, out var value)) return false; var bar=value.AsGodotObject(); if (bar is Node node && GodotObject.IsInstanceValid(node)) node.QueueFree(); bars.Remove(unit); return true; }
    private static void clean_and_refresh(Dictionary bars)
    { foreach (var key in new List<Variant>(bars.Keys)) { var unit=key.AsGodotObject(); var bar=bars[key].AsGodotObject(); if (!GodotObject.IsInstanceValid(unit)) { if (bar is Node node && GodotObject.IsInstanceValid(node)) node.QueueFree(); bars.Remove(key); } else if (GodotObject.IsInstanceValid(bar)) bar!.Call("refresh",unit); } }
    private static void clear(Dictionary bars) { foreach (var value in bars.Values) { var bar=value.AsGodotObject(); if (bar is Node node && GodotObject.IsInstanceValid(node)) node.QueueFree(); } bars.Clear(); }
}
