using Godot;

public partial class EastWoodsDungeonPanel
{
    private Control? _mapViewport;
    private float _mapZoom = 1;
    private Vector2 _cameraFocus;
    private bool _followPlayer = true, _cameraDragging, _cameraMoved;
    private Vector2 _cameraLastMouse, _cameraPressMouse;
    private Vector2? _travelCameraPosition;
    private const float MinMapZoom = .35f, MaxMapZoom = 1.8f;

    private void build_map_viewport()
    {
        _mapViewport = new Control { Name = "MapViewport", ClipContents = true, MouseFilter = MouseFilterEnum.Stop };
        _mapViewport.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_mapViewport);
        _mapViewport.GuiInput += handle_map_camera_input;
        _mapRoot = new Control { Name = "OrthogonalNodeMap", MouseFilter = MouseFilterEnum.Ignore };
        _mapViewport.AddChild(_mapRoot);
    }

    private Vector2 pawn_map_position(Control pawn) => _mapRoot!.GetGlobalTransform().AffineInverse() * pawn.GlobalPosition;

    private void reset_map_camera()
    {
        _mapZoom = 1; _followPlayer = true; _cameraDragging = false;
        update_map_camera();
    }

    private void update_map_camera()
    {
        if (_mapRoot is null || _mapViewport is null) return;
        if (_followPlayer)
            // Follow the stable route centre, not a pawn's bobbing/rotating local slot.
            _cameraFocus = _travelCameraPosition ?? node_center(route_node(_current)!);
        var scale = Vector2.One * _mapZoom;
        var position = _mapViewport.Size * .5f - _cameraFocus * _mapZoom;
        if (!_mapRoot.Scale.IsEqualApprox(scale)) _mapRoot.Scale = scale;
        if (!_mapRoot.Position.IsEqualApprox(position)) _mapRoot.Position = position;
        position_current_node_actions();
    }

    private void handle_map_camera_input(InputEvent input)
    {
        if (!Visible || _actionModal?.Visible == true || _actionBusy) return;
        if (input is not InputEventMouseButton button) return;
        if (button.Pressed && button.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
        {
            // Zoom leaves the world point at the viewport centre unchanged; never use the mouse as anchor.
            _mapZoom = Mathf.Clamp(_mapZoom * (button.ButtonIndex == MouseButton.WheelUp ? 1.12f : 1 / 1.12f), MinMapZoom, MaxMapZoom);
            clear_hover(); update_map_camera(); GetViewport().SetInputAsHandled();
        }
        else if (button.ButtonIndex == MouseButton.Middle && button.Pressed)
        {
            _cameraDragging = true; _cameraMoved = false;
            _cameraLastMouse = _cameraPressMouse = GetGlobalMousePosition();
            clear_hover(); GetViewport().SetInputAsHandled();
        }
    }

    public override void _Input(InputEvent input)
    {
        if (!_cameraDragging) return;
        if (!Visible || _actionModal?.Visible == true) { _cameraDragging = false; return; }
        if (input is InputEventMouseMotion motion)
        {
            if (motion.Position.DistanceTo(_cameraPressMouse) > 6) _cameraMoved = true;
            if (_cameraMoved)
            {
                _followPlayer = false;
                _cameraFocus -= (motion.Position - _cameraLastMouse) / _mapZoom;
                _cameraFocus = _cameraFocus.Clamp(Vector2.Zero, _mapRoot!.Size);
                update_map_camera();
            }
            _cameraLastMouse = motion.Position;
            GetViewport().SetInputAsHandled();
        }
        else if (input is InputEventMouseButton { ButtonIndex: MouseButton.Middle, Pressed: false })
        {
            _cameraDragging = false;
            if (!_cameraMoved) { _followPlayer = true; update_map_camera(); }
            GetViewport().SetInputAsHandled();
        }
    }
}
