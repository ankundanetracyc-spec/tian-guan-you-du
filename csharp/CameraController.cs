using Godot;

/// <summary>带边界约束的 2D 摄像机：键盘、中键拖动与平滑滚轮缩放。</summary>
public partial class CameraController : Camera2D
{
    [Export] public float move_speed = 1200.0f;
    [Export] public float zoom_step = 0.08f;
    [Export] public float zoom_min = 0.1f;
    [Export] public float zoom_max = 4.0f;
    [Export] public float zoom_smooth = 16.0f;
    [Export] public float pan_smooth = 14.0f;
    public bool zoom_enabled = true;
    [Export] public float drag_sensitivity = 1.0f;
    [Export] public float bounds_margin;

    private bool is_dragging;
    private Vector2 drag_start_pos;
    private Vector2 camera_start_pos;
    private Vector2 target_zoom = Vector2.One;
    private Vector2 target_position;
    private Rect2 world_bounds;

    public override void _Ready()
    {
        if (!IsCurrent()) MakeCurrent();
        target_zoom = Zoom;
        target_position = Position;
        refresh_world_bounds();
    }

    public void refresh_bounds()
    {
        refresh_world_bounds();
        target_position = clamp_position(target_position);
        Position = clamp_position(Position);
    }

    public void snap_to_position(Vector2 world_pos)
    {
        refresh_world_bounds();
        target_position = clamp_position(world_pos);
        Position = target_position;
    }

    public void set_zoom_enabled(bool enabled) => zoom_enabled = enabled;

    public override void _Process(double delta)
    {
        if (is_world_map_active())
        {
            is_dragging = false;
            target_position = Position;
            target_zoom = Zoom;
            return;
        }
        var uiDelta = (float)delta / Mathf.Max((float)Engine.TimeScale, 0.001f);
        if (world_bounds.Size.X <= 0) refresh_world_bounds();
        clamp_to_bounds();

        var direction = Vector2.Zero;
        if (Input.IsKeyPressed(Key.W)) direction.Y--;
        if (Input.IsKeyPressed(Key.S)) direction.Y++;
        if (Input.IsKeyPressed(Key.A)) direction.X--;
        if (Input.IsKeyPressed(Key.D)) direction.X++;
        if (direction != Vector2.Zero)
        {
            direction = direction.Normalized();
            var right = Vector2.Right.Rotated(Rotation);
            var down = Vector2.Down.Rotated(Rotation);
            target_position += (right * direction.X + down * direction.Y) * move_speed * uiDelta;
        }

        Zoom = Zoom.Lerp(target_zoom, 1.0f - Mathf.Exp(-zoom_smooth * uiDelta));
        target_position = clamp_position(target_position);
        Position = clamp_position(Position.Lerp(target_position, 1.0f - Mathf.Exp(-pan_smooth * uiDelta)));
        clamp_to_bounds();
    }

    public override void _Input(InputEvent @event)
    {
        if (is_world_map_active())
        {
            is_dragging = false;
            return;
        }
        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Middle } mouse)
        {
            is_dragging = mouse.Pressed;
            if (is_dragging)
            {
                drag_start_pos = GetGlobalMousePosition();
                camera_start_pos = target_position;
            }
        }
        if (@event is InputEventMouseMotion && is_dragging)
            target_position = camera_start_pos - (GetGlobalMousePosition() - drag_start_pos) * drag_sensitivity / Zoom;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (is_world_map_active() || !zoom_enabled || @event is not InputEventMouseButton mouse) return;
        if (mouse.ButtonIndex is not (MouseButton.WheelUp or MouseButton.WheelDown)) return;
        var factor = mouse.ButtonIndex == MouseButton.WheelUp ? 1.0f + zoom_step : 1.0f / (1.0f + zoom_step);
        var minimum = get_effective_min_zoom();
        target_zoom = (target_zoom * factor).Clamp(new Vector2(minimum, minimum), new Vector2(zoom_max, zoom_max));
    }

    private bool is_world_map_active() =>
        GetTree().GetFirstNodeInGroup("tile_world_map_presentation") is CanvasItem worldMap && worldMap.IsVisibleInTree() ||
        GetTree().GetFirstNodeInGroup("east_woods_dungeon_panel") is CanvasItem dungeonMap && dungeonMap.IsVisibleInTree();

    private void refresh_world_bounds()
    {
        foreach (var node in GetTree().GetNodesInGroup("base_area"))
            if (node is BaseArea area) { world_bounds = area.get_global_bounds(); return; }
    }

    private float get_effective_min_zoom()
    {
        if (world_bounds.Size.X <= 0 || world_bounds.Size.Y <= 0) return zoom_min;
        var viewport = GetViewportRect().Size;
        var width = world_bounds.Size.X - 2.0f * bounds_margin;
        var height = world_bounds.Size.Y - 2.0f * bounds_margin;
        return Mathf.Max(Mathf.Max(viewport.X / Mathf.Max(width, 1.0f), viewport.Y / Mathf.Max(height, 1.0f)), zoom_min);
    }

    private Vector2 get_viewport_half_size() => GetViewportRect().Size / Zoom * 0.5f;

    private Vector2 clamp_position(Vector2 position)
    {
        if (world_bounds.Size.X <= 0 || world_bounds.Size.Y <= 0) return position;
        var half = get_viewport_half_size();
        var left = world_bounds.Position.X + bounds_margin;
        var right = world_bounds.End.X - bounds_margin;
        var top = world_bounds.Position.Y + bounds_margin;
        var bottom = world_bounds.End.Y - bounds_margin;
        position.X = half.X * 2 >= right - left ? (left + right) * 0.5f : Mathf.Clamp(position.X, left + half.X, right - half.X);
        position.Y = half.Y * 2 >= bottom - top ? (top + bottom) * 0.5f : Mathf.Clamp(position.Y, top + half.Y, bottom - half.Y);
        return position;
    }

    private void clamp_to_bounds()
    {
        if (world_bounds.Size.X <= 0 || world_bounds.Size.Y <= 0) return;
        var minimum = get_effective_min_zoom();
        if (Zoom.X < minimum || Zoom.Y < minimum) { Zoom = new Vector2(minimum, minimum); target_zoom = Zoom; }
    }
}
