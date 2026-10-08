using Godot;
using Godot.Collections;

public partial class BuildPopup : Control
{
    [Signal] public delegate void BuildingSelectedEventHandler(string building_data_path);
    [Signal] public delegate void PopupClosedEventHandler();
    private Panel? _panel; private VBoxContainer? _list; private Button? _close;
    public override void _Ready() { AddToGroup("map_overlay_blocker"); _panel = GetNodeOrNull<Panel>("PopupPanel"); _list = GetNodeOrNull<VBoxContainer>("PopupPanel/BuildingList") ?? GetNodeOrNull<VBoxContainer>("BuildingList"); _close = GetNodeOrNull<Button>("PopupPanel/CloseBtn") ?? GetNodeOrNull<Button>("CloseBtn"); if (_close is not null) _close.Pressed += close; Hide(); }
    public override void _GuiInput(InputEvent @event) { if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mouse && _panel is not null && !_panel.GetRect().HasPoint(mouse.Position)) close(); }
    public void close() { EmitSignal(SignalName.PopupClosed); Hide(); }
    public void open(Godot.Collections.Array building_data_list)
    {
        if (_list is null) return; foreach (var child in _list.GetChildren()) child.QueueFree();
        if (building_data_list.Count == 0) _list.AddChild(new Label { Text = "（暂无可用建筑）", HorizontalAlignment = HorizontalAlignment.Center });
        else foreach (var item in building_data_list) _list.AddChild(make_building_button(item)); Show();
    }
    private Control make_building_button(Variant data)
    {
        var wrapper = new VBoxContainer(); var button = new Button { CustomMinimumSize = new Vector2(0,42), Alignment = HorizontalAlignment.Left };
        var values = data.VariantType == Variant.Type.Dictionary ? data.AsGodotDictionary() : new Dictionary();
        var source = data.AsGodotObject(); var name = values.TryGetValue("entity_name", out var value) ? value.AsString() : GodotObject.IsInstanceValid(source) ? source!.Get("entity_name").AsString() : "未知道具";
        var enabled = !values.TryGetValue("enabled", out var state) || state.AsBool(); var cost = values.TryGetValue("cost_text", out var costValue) ? costValue.AsString() : ""; button.Text = "🏠 " + name + (string.IsNullOrEmpty(cost) ? "" : "　·　" + cost); button.Disabled = !enabled;
        button.Pressed += () => { var path = values.TryGetValue("resource_path", out var resource) ? resource.AsString() : GodotObject.IsInstanceValid(source) ? source!.Get("resource_path").AsString() : ""; EmitSignal(SignalName.BuildingSelected, path); close(); }; wrapper.AddChild(button); return wrapper;
    }
}
