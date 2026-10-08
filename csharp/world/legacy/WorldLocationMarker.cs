using Godot;

/// <summary>世界地图上的可点击地点标记。</summary>
public partial class WorldLocationMarker : Node2D
{
    [Export] public string location_id = "";
    [Export] public string display_name = "未知地点";
    [Export] public Texture2D? icon_texture;
    [Export] public bool requires_unlock;
    public bool unlocked = true;

    private Area2D? _area;
    private Sprite2D? _sprite;
    private Label? _label;

    [Signal] public delegate void marker_clickedEventHandler(string marker_id);

    public override void _Ready()
    {
        _area = GetNodeOrNull<Area2D>("Area2D");
        _sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
        _label = GetNodeOrNull<Label>("Label");
        if (_label is not null) _label.Text = display_name;
        if (_area is not null) _area.InputEvent += (_, inputEvent, _) => on_input_event(inputEvent);
    }

    private void on_input_event(InputEvent @event)
    {
        if (unlocked && @event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            EmitSignal(SignalName.marker_clicked, location_id);
    }

    public void set_unlocked(bool value)
    {
        unlocked = value;
        if (_sprite is not null) _sprite.Modulate = value ? Colors.White : new Color(0.3f, 0.3f, 0.3f, 0.5f);
        if (_label is not null) _label.AddThemeColorOverride("font_color", value ? Colors.White : new Color(0.5f, 0.5f, 0.5f));
    }
}
