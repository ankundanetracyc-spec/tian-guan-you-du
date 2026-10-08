using Godot;

/// <summary>田畴专用页：展示自治耕作结果，调度规则统一由辖地章程决定。</summary>
public partial class QingxiFarmlandPanel : Control
{
    public event Action? Closed;
    private QingxiTerritoryState? _state; private EstateResourceSystem? _resources;
    private Label? _summary;
    private Label? _farming;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop; ZIndex = 90; Build(); Hide();
    }

    public void bind_state(QingxiTerritoryState state, EstateResourceSystem? resources = null)
    {
        if (_state is not null) _state.TerritoryChanged -= Refresh;
        _state = state; _state.TerritoryChanged += Refresh; Refresh();
        if (_resources is not null) _resources.ResourcesChanged -= Refresh;
        _resources = resources;
        if (_resources is not null) _resources.ResourcesChanged += Refresh;
        Refresh();
    }

    public void open() { Show(); MoveToFront(); Refresh(); }

    private void Build()
    {
        var shade = new ColorRect { Color = new Color(0.04f, 0.035f, 0.025f, 0.72f), MouseFilter = MouseFilterEnum.Stop };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); shade.GuiInput += input => { if (input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) Close(); }; AddChild(shade);
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(680, 440) };
        panel.SetAnchorsPreset(LayoutPreset.Center); panel.OffsetLeft = -340; panel.OffsetTop = -220; panel.OffsetRight = 340; panel.OffsetBottom = 220; panel.AddThemeStyleboxOverride("panel", Style()); AddChild(panel);
        var margin = new MarginContainer(); margin.AddThemeConstantOverride("margin_left", 28); margin.AddThemeConstantOverride("margin_right", 28); margin.AddThemeConstantOverride("margin_top", 24); margin.AddThemeConstantOverride("margin_bottom", 24); panel.AddChild(margin);
        var content = new VBoxContainer(); content.AddThemeConstantOverride("separation", 16); margin.AddChild(content);
        var header = new HBoxContainer(); var title = new Label { Text = "青溪里 · 田畴", SizeFlagsHorizontal = SizeFlags.ExpandFill }; title.AddThemeFontSizeOverride("font_size", 28); title.Modulate = new Color(0.98f, 0.91f, 0.7f); header.AddChild(title);
        var close = new Button { Text = "返回辖地", CustomMinimumSize = new Vector2(104, 38) }; close.Pressed += Close; header.AddChild(close); content.AddChild(header);
        content.AddChild(new Label { Text = "田畴由田庄头人和佃户自行维持。耕作人数根据辖地的储粮底线自动调整，无须逐月点选。", AutowrapMode = TextServer.AutowrapMode.WordSmart });
        var row = new HBoxContainer(); row.AddChild(new Label { Text = "当前自动耕作", SizeFlagsHorizontal = SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center }); _farming = new Label { CustomMinimumSize = new Vector2(120, 40), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center }; row.AddChild(_farming); content.AddChild(row);
        _summary = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(0, 74) }; _summary.Modulate = new Color(0.84f, 0.8f, 0.68f); content.AddChild(_summary);
    }

    private void Refresh()
    {
        if (_state is null) return;
        var farming = _state.get_labor(QingxiTerritoryState.Labor.Farming);
        if (_farming is not null) _farming.Text = $"{farming} 民力";
        var food = _resources?.get_amount(EstateResourceSystem.Food) ?? 0;
        if (_summary is not null) _summary.Text = $"预计月收成：{Mathf.FloorToInt(farming * 3 * _state.get_production_multiplier(QingxiTerritoryState.Labor.Farming))} 粮食　｜　现有粮食：{food}\n储粮低于辖地章程中的底线时，系统会优先补足耕作人手；人手不足时，田庄会将情况记入局势簿。";
    }

    private void Close() { Hide(); Closed?.Invoke(); }
    private static StyleBoxFlat Style() => new() { BgColor = new Color(0.13f, 0.115f, 0.08f, 0.985f), BorderColor = new Color(0.72f, 0.59f, 0.32f, 0.95f), BorderWidthLeft = 2, BorderWidthTop = 2, BorderWidthRight = 2, BorderWidthBottom = 2, CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10, CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10 };
}
