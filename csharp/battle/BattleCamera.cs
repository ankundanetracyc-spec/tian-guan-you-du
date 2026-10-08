using Godot;

public partial class BattleCamera : Camera2D
{
    [Export] public float move_speed = 900;
    [Export] public float zoom_step = .1f;
    [Export] public float zoom_min = .4f;
    [Export] public float zoom_max = 2.5f;
    [Export] public float zoom_lerp_speed = 12;
    [Export] public Rect2 map_bounds = new(-5000, -5000, 10000, 10000);
    public Vector2 target_zoom = Vector2.One;

    public override void _Ready() { MakeCurrent(); target_zoom = Zoom; PositionSmoothingEnabled = true; PositionSmoothingSpeed = 12; }
    public override void _Process(double delta)
    {
        var input = new Vector2((Input.IsKeyPressed(Key.D) ? 1 : 0) - (Input.IsKeyPressed(Key.A) ? 1 : 0), (Input.IsKeyPressed(Key.S) ? 1 : 0) - (Input.IsKeyPressed(Key.W) ? 1 : 0));
        if (input != Vector2.Zero) GlobalPosition += input * move_speed * (float)delta;
        Zoom = Zoom.Lerp(target_zoom, (float)delta * zoom_lerp_speed);
        GlobalPosition = new Vector2(Mathf.Clamp(GlobalPosition.X, map_bounds.Position.X, map_bounds.End.X), Mathf.Clamp(GlobalPosition.Y, map_bounds.Position.Y, map_bounds.End.Y));
    }
    public override async void _Input(InputEvent @event)
    {
        if (@event is not InputEventMouseButton { Pressed: true } mouse || mouse.ButtonIndex is not (MouseButton.WheelUp or MouseButton.WheelDown)) return;
        var before = GetGlobalMousePosition();
        var change = mouse.ButtonIndex == MouseButton.WheelUp ? zoom_step : -zoom_step;
        target_zoom = (target_zoom + Vector2.One * change).Clamp(Vector2.One * zoom_min, Vector2.One * zoom_max);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GlobalPosition += before - GetGlobalMousePosition();
    }
}
