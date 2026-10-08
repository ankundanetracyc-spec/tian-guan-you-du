using Godot;

public partial class EstateOverview
{
    private Button? _compactQuestToggle;
    private bool _compactQuestExpanded = true;

    public void set_compact_task_tracker_visible(bool visible)
    {
        if (_quest_panel is not null) _quest_panel.Visible = visible;
        if (_quest_frame is not null) _quest_frame.Visible = visible && _compactQuestExpanded;
        if (_compactQuestToggle is not null) _compactQuestToggle.Visible = visible;
    }

    public void apply_compact_task_tracker()
    {
        CallDeferred(nameof(apply_compact_task_tracker_layout));
    }

    private void apply_compact_task_tracker_layout()
    {
        if (_quest_panel is null || _task_list is null) return;
        const float height = 138;
        _quest_panel.AnchorTop = .25f;
        _quest_panel.AnchorBottom = .25f;
        _quest_panel.OffsetTop = 0;
        _quest_panel.OffsetBottom = height;
        _quest_panel.OffsetLeft = _compactQuestExpanded ? -368 : 2;
        _quest_panel.OffsetRight = _compactQuestExpanded ? -28 : 342;
        _quest_panel.ZIndex = 10;
        _quest_panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(.015f, .02f, .016f, .74f),
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            CornerRadiusBottomLeft = 3,
            CornerRadiusBottomRight = 3
        });

        if (_quest_frame is TextureRect frame)
        {
            frame.ZIndex = 9;
            frame.AnchorTop = .25f;
            frame.AnchorBottom = .25f;
            frame.OffsetTop = 0;
            frame.OffsetBottom = 138;
            frame.OffsetLeft = _compactQuestExpanded ? -368 : 2;
            frame.OffsetRight = _compactQuestExpanded ? -28 : 342;
            frame.Texture = GD.Load<Texture2D>("res://textures/ui/qingxi/quest_tracker_ink_border_v1.png");
            frame.Visible = _quest_panel.Visible && _compactQuestExpanded;
        }

        if (_quest_panel.GetNodeOrNull<MarginContainer>("Margin") is { } margin)
        {
            margin.AddThemeConstantOverride("margin_left", 16);
            margin.AddThemeConstantOverride("margin_top", 16);
            margin.AddThemeConstantOverride("margin_right", 8);
            margin.AddThemeConstantOverride("margin_bottom", 16);
        }
        if (_quest_panel.FindChild("Header", true, false) is Label header) header.Hide();

        foreach (var child in _task_list.GetChildren())
        {
            if (child is not Button button) continue;
            var lines = button.Text.Split('\n');
            var summary = lines.Length > 1 ? lines[1] : "";
            if (summary.Length > 21) summary = summary[..20] + "…";
            button.Text = lines[0] + "\n" + summary;
            button.CustomMinimumSize = new Vector2(0, 50);
            button.AddThemeFontSizeOverride("font_size", 13);
            button.AddThemeColorOverride("font_color", new Color("F4F0DF"));
            button.AddThemeStyleboxOverride("normal", new StyleBoxFlat
            {
                BgColor = new Color(0, 0, 0, 0),
                ContentMarginLeft = 8,
                ContentMarginRight = 8,
                ContentMarginTop = 3,
                ContentMarginBottom = 3
            });
            button.AddThemeStyleboxOverride("hover", new StyleBoxFlat
            {
                BgColor = new Color(1, 1, 1, .08f),
                ContentMarginLeft = 8,
                ContentMarginRight = 8,
                ContentMarginTop = 3,
                ContentMarginBottom = 3
            });
        }
        _task_list.AddThemeConstantOverride("separation", 5);

        if (_compactQuestToggle is null)
        {
            _compactQuestToggle = new Button
            {
                FocusMode = Control.FocusModeEnum.None,
                ZIndex = 11,
                MouseFilter = Control.MouseFilterEnum.Stop,
                MouseDefaultCursorShape = Control.CursorShape.PointingHand,
                CustomMinimumSize = new Vector2(40, 138)
            };
            _compactQuestToggle.SetAnchorsPreset(LayoutPreset.TopRight);
            _compactQuestToggle.AnchorTop = .25f;
            _compactQuestToggle.AnchorBottom = .25f;
            _compactQuestToggle.OffsetLeft = -40;
            _compactQuestToggle.OffsetTop = 0;
            _compactQuestToggle.OffsetRight = 0;
            _compactQuestToggle.OffsetBottom = 138;
            _compactQuestToggle.AddThemeFontSizeOverride("font_size", 14);
            _compactQuestToggle.AddThemeColorOverride("font_color", new Color("F4F0DF"));
            _compactQuestToggle.AddThemeColorOverride("font_hover_color", new Color("FFF4CC"));
            _compactQuestToggle.AddThemeColorOverride("font_pressed_color", new Color("FFF4CC"));
            _compactQuestToggle.AddThemeStyleboxOverride("normal", new StyleBoxFlat
            {
                BgColor = new Color(.025f, .03f, .026f, .96f),
                BorderColor = new Color(.72f, .65f, .47f, .78f),
                BorderWidthLeft = 1,
                BorderWidthTop = 1,
                BorderWidthBottom = 1,
                CornerRadiusTopLeft = 0,
                CornerRadiusBottomLeft = 0
            });
            _compactQuestToggle.AddThemeStyleboxOverride("hover", new StyleBoxFlat
            {
                BgColor = new Color(.14f, .15f, .12f, .98f),
                BorderColor = new Color(.88f, .77f, .52f, .95f),
                BorderWidthLeft = 1,
                BorderWidthTop = 1,
                BorderWidthBottom = 1,
                CornerRadiusTopLeft = 0,
                CornerRadiusBottomLeft = 0
            });
            _compactQuestToggle.Pressed += toggle_compact_quest_tracker;
            AddChild(_compactQuestToggle);
        }
        _compactQuestToggle.ZIndex = 11;
        _compactQuestToggle.MouseFilter = Control.MouseFilterEnum.Stop;
        _compactQuestToggle.OffsetLeft = -40;
        _compactQuestToggle.Visible = _quest_panel.Visible;
        _compactQuestToggle.Text = _compactQuestExpanded ? "▶" : "◀";
        _compactQuestToggle.TooltipText = _compactQuestExpanded ? "向右收起任务栏" : "展开任务栏";
    }

    private void toggle_compact_quest_tracker()
    {
        _compactQuestExpanded = !_compactQuestExpanded;
        if (_compactQuestToggle is not null)
        {
            _compactQuestToggle.Text = _compactQuestExpanded ? "▶" : "◀";
            _compactQuestToggle.TooltipText = _compactQuestExpanded ? "向右收起任务栏" : "展开任务栏";
        }
        apply_compact_task_tracker();
    }
}
