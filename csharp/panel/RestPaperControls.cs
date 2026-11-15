using Godot;
using System.Globalization;

/// <summary>休息页数字输入；原SpinBox仅保存数值，明面使用纸槽和裸三角。</summary>
public partial class RestPaperNumber : Control
{
    private readonly SpinBox _value;
    private readonly int _fontSize;
    private readonly string _description;
    private LineEdit _input = null!;
    private RestPaperTriangle _up = null!, _down = null!;
    public RestPaperNumber() : this(new SpinBox(), 27, "数值") { }
    public RestPaperNumber(SpinBox value, int fontSize, string description)
    { _value = value; _fontSize = fontSize; _description = description; }

    public override void _Ready()
    {
        _value.Hide(); AddChild(_value);
        var well = new Panel { Name = "NumberWell", MouseFilter = MouseFilterEnum.Ignore };
        well.AddThemeStyleboxOverride("panel", CraftingPaperStyle.ButtonStyle(CraftingPaperStyle.ButtonKind.Input, horizontalPadding: 0, verticalPadding: 0));
        AddChild(well); well.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _input = new LineEdit { Name = "NumberInput", Alignment = HorizontalAlignment.Center,
            ContextMenuEnabled = false, SelectAllOnFocus = true, TooltipText = _description };
        CraftingPaperStyle.ApplyInput(_input, _fontSize, 2, 0); _input.CustomMinimumSize = Vector2.Zero;
        foreach (string state in new[] { "normal", "read_only" }) _input.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        AddChild(_input); _input.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); _input.OffsetRight = -28;
        _up = new RestPaperTriangle { Name = "NumberPlus", Up = true, TooltipText = "增加" + _description };
        _down = new RestPaperTriangle { Name = "NumberMinus", TooltipText = "减少" + _description };
        AddChild(_up); AddChild(_down);
        _up.Pressed += () => _value.Value += 1; _down.Pressed += () => _value.Value -= 1;
        _input.TextSubmitted += _ => CommitInput(); _input.FocusExited += CommitInput;
        _value.ValueChanged += Changed; Resized += Fit; Fit(); SyncValue();
    }
    private void Fit()
    {
        _up.Position = new Vector2(Size.X - 27, 1); _up.Size = new Vector2(25, Size.Y / 2 - 1);
        _down.Position = new Vector2(Size.X - 27, Size.Y / 2); _down.Size = new Vector2(25, Size.Y / 2 - 1);
    }
    public void CommitInput()
    {
        if (_input is null) return;
        if (double.TryParse(_input.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number)) _value.Value = number;
        SyncValue();
    }
    private void Changed(double _) => SyncValue();
    public void SyncValue()
    {
        if (_input is null) return;
        _input.Text = _value.Value.ToString("0", CultureInfo.InvariantCulture);
        _input.Editable = _value.Editable;
        _up.Disabled = !_value.Editable || _value.Value >= _value.MaxValue;
        _down.Disabled = !_value.Editable || _value.Value <= _value.MinValue;
        _up.QueueRedraw(); _down.QueueRedraw();
    }
    public override void _ExitTree() { if (GodotObject.IsInstanceValid(_value)) _value.ValueChanged -= Changed; }
}

/// <summary>用户明确保留的无素材小三角，用于增减与右上返回。</summary>
public partial class RestPaperTriangle : Button
{
    public bool Up { get; set; }
    public bool Right { get; set; }
    public override Vector2 _GetMinimumSize() => Vector2.Zero;
    public override void _Ready()
    {
        foreach (string state in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled", "focus" })
            AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        MouseDefaultCursorShape = CursorShape.PointingHand;
    }
    public override void _Draw()
    {
        var color = CraftingPaperStyle.Accent; if (Disabled) color.A = .35f;
        var center = Size / 2;
        Vector2[] points = Right ? [new(0, 0), new(Size.X, Size.Y / 2), new(0, Size.Y)]
            : Up ? [center + new Vector2(-5, 3), center + new Vector2(5, 3), center + new Vector2(0, -3)]
            : [center + new Vector2(-5, -3), center + new Vector2(5, -3), center + new Vector2(0, 3)];
        DrawColoredPolygon(points, color);
    }
}
