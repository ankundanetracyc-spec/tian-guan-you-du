using Godot;
using System.Globalization;

public partial class QuietRoomPaperTab : Button
{
    private Panel _face = null!, _binding = null!;
    private readonly string _caption;
    private readonly Texture2D _icon;
    private Tween? _movement, _fade;
    private bool _selected, _hovered, _held;
    public float Reveal => _face?.Position.X ?? 0;
    public float BindingOpacity => _binding?.Modulate.A ?? 0;

    public QuietRoomPaperTab() : this("", new GradientTexture2D()) { }
    public QuietRoomPaperTab(string caption, Texture2D icon) { _caption = caption; _icon = icon; }
    public override void _Ready()
    {
        ToggleMode = true; CustomMinimumSize = new Vector2(0, 69);
        PaperButtonFeedback.Apply(this, CraftingPaperStyle.ButtonKind.Navigation, 23, trackPress: false);
        foreach (string state in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled" })
            AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        _binding = new Panel { Name = "NavigationBinding", MouseFilter = MouseFilterEnum.Ignore, Modulate = new Color(1, 1, 1, 0) };
        _binding.AddThemeStyleboxOverride("panel", CraftingPaperStyle.BindingStyle()); AddChild(_binding);
        _face = new Panel { Name = "NavigationPaper", MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_face);
        var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 7); _face.AddChild(row); row.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        row.OffsetLeft = 8; row.OffsetRight = -8;
        row.AddChild(new TextureRect { Texture = _icon, CustomMinimumSize = new Vector2(26, 26),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore });
        row.AddChild(CraftingPaperStyle.Text(_caption, 23));
        Resized += Fit;
        MouseEntered += () => { _hovered = true; Skin(true); };
        MouseExited += () => { _hovered = false; Skin(false); };
        ButtonDown += () => { _held = true; Skin(_hovered); };
        ButtonUp += () => { _held = false; Skin(_hovered); };
        Fit(); Skin(false);
    }
    private void Fit()
    {
        if (_face is null) return;
        _binding.Position = Vector2.Zero; _binding.Size = new Vector2(18, Size.Y);
        _face.Size = new Vector2(Math.Max(1, Size.X - 18), Size.Y);
    }
    private void Skin(bool hover) => _face.AddThemeStyleboxOverride("panel", PaperButtonFeedback.Face(
        CraftingPaperStyle.ButtonKind.Navigation, _held ? CraftingPaperStyle.ButtonState.Pressed
            : hover ? CraftingPaperStyle.ButtonState.Hover : CraftingPaperStyle.ButtonState.Normal, _selected));
    public void SelectPaper(bool selected, bool reducedMotion = false)
    {
        SetPressedNoSignal(selected);
        if (_face is null) { _selected = selected; return; }
        bool changed = _selected != selected; _selected = selected; Skin(false);
        if (!changed && !reducedMotion) return;
        _movement?.Kill(); _fade?.Kill();
        float destination = selected ? 18 : 0, opacity = selected ? 1 : 0;
        if (reducedMotion || !IsVisibleInTree())
        {
            _face.Position = new Vector2(destination, 0); _binding.Modulate = new Color(1, 1, 1, opacity); return;
        }
        _movement = CreateTween().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        _movement.TweenProperty(_face, "position:x", destination, .22);
        _fade = CreateTween(); _fade.TweenProperty(_binding, "modulate:a", opacity, .14);
    }
    public override void _ExitTree() { _movement?.Kill(); _fade?.Kill(); }
}
