using Godot;

public partial class StoragePanel
{
    private bool _nativeCategoryPressed, _nativeCategoryPanning;
    private Vector2 _nativeCategoryStart;
    private int _nativeCategoryStartScroll;
    private Button? _nativeCategoryButton;

    private bool handle_native_category_input(InputEvent input)
    {
        if (_nativeContainerId.Length > 0 || _nativeCategoryScroll?.IsVisibleInTree() != true) return false;
        if (input is InputEventMouseButton { Pressed: true } press)
        {
            var local = _nativeCategoryScroll.GetGlobalTransformWithCanvas().AffineInverse() * press.Position;
            if (!new Rect2(Vector2.Zero, _nativeCategoryScroll.Size).HasPoint(local)) return false;
            if (press.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
            {
                _nativeCategoryScroll.ScrollHorizontal += press.ButtonIndex == MouseButton.WheelDown ? 80 : -80;
                GetViewport().SetInputAsHandled(); return true;
            }
            if (press.ButtonIndex != MouseButton.Left) return false;
            _nativeCategoryPressed = true; _nativeCategoryPanning = false;
            _nativeCategoryStart = press.Position; _nativeCategoryStartScroll = _nativeCategoryScroll.ScrollHorizontal;
            _nativeCategoryButton = _category_buttons.Values.FirstOrDefault(button =>
                new Rect2(Vector2.Zero, button.Size).HasPoint(button.GetGlobalTransformWithCanvas().AffineInverse() * press.Position));
            GetViewport().SetInputAsHandled(); return true;
        }
        if (!_nativeCategoryPressed) return false;
        if (input is InputEventMouseMotion motion)
        {
            _nativeCategoryPanning |= Math.Abs(motion.Position.X - _nativeCategoryStart.X) >= 4;
            if (_nativeCategoryPanning)
            {
                _nativeCategoryScroll.ScrollHorizontal = _nativeCategoryStartScroll - Mathf.RoundToInt((motion.Position.X - _nativeCategoryStart.X) / _canvas!.Scale.X);
                HoverInfoManager.HideAll();
            }
            GetViewport().SetInputAsHandled(); return true;
        }
        if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false })
        {
            var button = _nativeCategoryButton; var click = !_nativeCategoryPanning;
            _nativeCategoryPressed = _nativeCategoryPanning = false; _nativeCategoryButton = null;
            if (click && button is not null && GodotObject.IsInstanceValid(button)) button.EmitSignal(BaseButton.SignalName.Pressed);
            GetViewport().SetInputAsHandled(); return true;
        }
        return false;
    }
}
