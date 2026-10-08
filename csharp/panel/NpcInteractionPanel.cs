using Godot;

/// <summary>点选地点人物后的行动菜单，避免直接跳入人物台词。</summary>
public partial class NpcInteractionPanel : Control
{
    private Label? _name;
    private VBoxContainer? _actions;
    private PanelContainer? _frame;

    public override void _Ready()
    {
        AddToGroup("npc_interaction_panel");
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        ZIndex = 240;

        var frame = new PanelContainer { MouseFilter = MouseFilterEnum.Stop };
        _frame = frame;
        frame.AnchorLeft = 0;
        frame.AnchorRight = 0;
        frame.AnchorTop = 0;
        frame.AnchorBottom = 0;
        // 紧挨左侧人物列表展开，保留名单本身可见。
        frame.OffsetLeft = 318;
        frame.OffsetTop = 136;
        frame.OffsetRight = 553;
        frame.OffsetBottom = 196;
        frame.AddThemeStyleboxOverride("panel", make_frame_style());
        AddChild(frame);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 16);
        margin.AddThemeConstantOverride("margin_top", 12);
        margin.AddThemeConstantOverride("margin_right", 16);
        margin.AddThemeConstantOverride("margin_bottom", 12);
        frame.AddChild(margin);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 8);
        margin.AddChild(column);
        _name = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _name.AddThemeFontSizeOverride("font_size", 21);
        _name.AddThemeColorOverride("font_color", new Color("e6c77e"));
        column.AddChild(_name);
        _actions = new VBoxContainer();
        _actions.AddThemeConstantOverride("separation", 6);
        column.AddChild(_actions);
        Hide();
    }

    public void show_menu(string name, IEnumerable<(string label, Action action)> options)
    {
        place_next_to_roster();
        _name!.Text = name;
        foreach (var child in _actions!.GetChildren()) child.QueueFree();
        foreach (var (label, action) in options)
        {
            var button = new Button { Text = label, CustomMinimumSize = new Vector2(0, 34) };
            button.AddThemeFontSizeOverride("font_size", 17);
            button.AddThemeColorOverride("font_color", Colors.White);
            button.Pressed += () => { Hide(); action(); };
            _actions.AddChild(button);
        }
        var leave = new Button { Text = "关闭", CustomMinimumSize = new Vector2(0, 30) };
        leave.AddThemeFontSizeOverride("font_size", 16);
        leave.AddThemeColorOverride("font_color", new Color("d8d1c2"));
        leave.Pressed += Hide;
        _actions.AddChild(leave);
        Show();
        MoveToFront();
    }

    private void place_next_to_roster()
    {
        if (_frame is null) return;
        var roster = GetTree().GetFirstNodeInGroup("cell_character_list") as CellCharacterList;
        if (roster is null) return;
        var rect = roster.get_ui_rect();
        var viewport = GetViewportRect().Size;
        const float width = 235f;
        const float height = 160f;
        var left = Mathf.Clamp(rect.End.X + 12f, 8f, Mathf.Max(8f, viewport.X - width - 8f));
        var top = Mathf.Clamp(rect.Position.Y, 8f, Mathf.Max(8f, viewport.Y - height - 8f));
        _frame.OffsetLeft = left;
        _frame.OffsetTop = top;
        _frame.OffsetRight = left + width;
        _frame.OffsetBottom = top + height;
    }

    public void close() => Hide();

    private static StyleBoxFlat make_frame_style()
    {
        return new StyleBoxFlat
        {
            BgColor = new Color(0.02f, 0.027f, 0.022f, 0.94f),
            BorderColor = new Color("9a7a3d"),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6
        };
    }
}
