using Godot;

/// <summary>村庄功能区入口的单片竹简。</summary>
public partial class BambooSlip : Control
{
    [Signal] public delegate void slip_clickedEventHandler(string slip_id);
    [Signal] public delegate void slip_double_clickedEventHandler(string slip_id);

    public string slip_id = "";
    public string display_name = "";
    private Label? title_label;
    private TextureRect? slip_texture;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        title_label = GetNodeOrNull<Label>("TitleLabel");
        slip_texture = GetNodeOrNull<TextureRect>("SlipTexture");
        GuiInput += on_gui_input;
        MouseEntered += on_mouse_entered;
        MouseExited += on_mouse_exited;
        if (title_label is not null && !string.IsNullOrEmpty(display_name)) title_label.Text = to_vertical_text(display_name);
    }

    public string to_vertical_text(string text) => string.Join("\n", text.ToCharArray());

    private void on_mouse_entered()
    {
        animate_scale(new Vector2(1.04f, 1.04f));
        if (slip_texture is not null) slip_texture.Modulate = new Color(1.15f, 1.1f, 1.0f, 1.0f);
    }

    private void on_mouse_exited()
    {
        animate_scale(Vector2.One);
        if (slip_texture is not null) slip_texture.Modulate = Colors.White;
    }

    private void on_gui_input(InputEvent @event)
    {
        if (@event is not InputEventMouseButton { Pressed: true } mouseEvent) return;
        if (mouseEvent.ButtonIndex != MouseButton.Left) return;
        EmitSignal(mouseEvent.DoubleClick ? SignalName.slip_double_clicked : SignalName.slip_clicked, slip_id);
    }

    private void animate_scale(Vector2 target)
    {
        var tween = CreateTween();
        tween.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
        tween.TweenProperty(this, "scale", target, 0.15f);
    }
}
