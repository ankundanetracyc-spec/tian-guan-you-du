using Godot;
using System.Globalization;

public partial class QuietRoomNumber : Control
{
    private readonly SpinBox _value;
    private readonly bool _spirit;
    private LineEdit _input = null!;
    private QuietRoomTriangle _up = null!, _down = null!;
    public QuietRoomNumber() : this(new SpinBox(), false) { }
    public QuietRoomNumber(SpinBox value, bool spirit = false) { _value = value; _spirit = spirit; }
    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(Math.Max(70, CustomMinimumSize.X), 46);
        _value.Visible = false; AddChild(_value);
        var well = new Panel { Name = "NumberWell", MouseFilter = MouseFilterEnum.Ignore };
        well.AddThemeStyleboxOverride("panel", CraftingPaperStyle.ButtonStyle(CraftingPaperStyle.ButtonKind.Input, horizontalPadding: 0, verticalPadding: 0));
        AddChild(well); well.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _input = new LineEdit { Name = "NumberInput", Alignment = HorizontalAlignment.Center, ContextMenuEnabled = false, SelectAllOnFocus = true };
        CraftingPaperStyle.ApplyInput(_input, 24, 2, 0); _input.CustomMinimumSize = Vector2.Zero;
        foreach (string state in new[] { "normal", "read_only" }) _input.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        if (_spirit) _input.AddThemeColorOverride("font_color", new Color("5d9da8"));
        AddChild(_input); _input.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); _input.OffsetRight = -28;
        _up = new QuietRoomTriangle { Name = "NumberPlus", Up = true, TooltipText = "增加" };
        _down = new QuietRoomTriangle { Name = "NumberMinus", TooltipText = "减少" };
        AddChild(_up); AddChild(_down);
        void Fit()
        {
            _up.Position = new Vector2(Size.X - 27, 1); _up.Size = new Vector2(25, Size.Y / 2 - 1);
            _down.Position = new Vector2(Size.X - 27, Size.Y / 2); _down.Size = new Vector2(25, Size.Y / 2 - 1);
        }
        _up.Pressed += () => _value.Value += 1; _down.Pressed += () => _value.Value -= 1;
        _input.TextSubmitted += _ => Commit(); _input.FocusExited += Commit;
        _value.ValueChanged += Changed; Resized += Fit; Fit(); Changed(_value.Value);
    }
    private void Commit()
    {
        if (double.TryParse(_input.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)) _value.Value = number;
        Changed(_value.Value);
    }
    private void Changed(double number)
    {
        _input.Text = number.ToString("0", CultureInfo.InvariantCulture);
        _up.Disabled = !_value.Editable || number >= _value.MaxValue; _down.Disabled = !_value.Editable || number <= _value.MinValue;
        _input.Editable = _value.Editable; _up.QueueRedraw(); _down.QueueRedraw();
    }
    public void RefreshNumber() { if (_input is not null) Changed(_value.Value); }
    public override void _ExitTree() { if (GodotObject.IsInstanceValid(_value)) _value.ValueChanged -= Changed; }
}
