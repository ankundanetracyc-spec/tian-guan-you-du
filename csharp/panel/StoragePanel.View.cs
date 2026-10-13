using Godot;

/// <summary>按1672×941理想图坐标组织原生行囊控件；文字与物品均由真实数据填充。</summary>
public partial class StoragePanel
{
    private static readonly Vector2 DesignSize = new(1672, 941);
    private static readonly Color Ink = new("252920"), PaperText = new("f0ead7"), MutedPaper = new("d2ceba");
    private Control? _canvas;
    private FontFile? _inventory_font;
    private Texture2D? _navigation_atlas;
    private GridContainer? _grid;
    private ScrollContainer? _grid_scroll, _detail_scroll;
    private Label? _empty, _summary, _info_name, _info_category, _info_grade, _info_count, _info_effect_title;
    private TextureRect? _info_icon;
    private RichTextLabel? _info_tags, _info_desc;
    private PanelContainer? _detail_panel;
    private VBoxContainer? _detail_content;
    private HBoxContainer? _action_row;
    private Button? _action_button, _cancel_button;
    private InventoryPaperFrame? _popup_frame;

    private void build_inventory_view()
    {
        _inventory_font = GD.Load<FontFile>("res://resources/font/LXGWZhenKaiSlabGB-Regular.ttf");
        _navigation_atlas = GD.Load<Texture2D>("res://textures/ui/inventory_20261012/category_icons_v1.png");
        var matte = new ColorRect { Color = new Color("26291f"), MouseFilter = MouseFilterEnum.Ignore };
        matte.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(matte);
        _canvas = new Control { Name = "InventoryCanvas", Size = DesignSize, MouseFilter = MouseFilterEnum.Pass };
        AddChild(_canvas);
        var background = new TextureRect
        {
            Texture = GD.Load<Texture2D>("res://textures/ui/inventory_20261012/approved_shell_v1.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore
        };
        place(background, _canvas, 0, 0, DesignSize.X, DesignSize.Y);

        var back = paper_button("←", 30, true);
        place(back, _canvas, 29, 19, 48, 48);
        back.Pressed += hide_panel;
        place(text("行囊", 34, Ink), _canvas, 91, 16, 150, 51);
        var organize = paper_button("整理", 25, true);
        organize.Icon = navigation_icon(0);
        organize.ExpandIcon = true;
        organize.AddThemeConstantOverride("icon_max_width", 28);
        place(organize, _canvas, 1509, 20, 136, 47);
        organize.Pressed += organize_inventory;

        var railTitle = text("行囊", 34, PaperText);
        railTitle.HorizontalAlignment = HorizontalAlignment.Center;
        place(railTitle, _canvas, 36, 94, 203, 46);
        var subtitle = text("随身物资", 20, MutedPaper);
        subtitle.HorizontalAlignment = HorizontalAlignment.Center;
        place(subtitle, _canvas, 36, 143, 203, 33);
        string[] categories = ["", "装备", "材料", "秘籍", "阵盘", "器具", "符箓", "丹药", "灵膳", ArtifactFilter];
        string[] names = ["全部", "装备", "材料", "秘籍", "阵盘", "器具", "符箓", "丹药", "灵膳", "法宝"];
        for (var index = 0; index < categories.Length; index++)
        {
            var category = categories[index];
            var button = new Button
            {
                Text = names[index], ToggleMode = true, Alignment = HorizontalAlignment.Left,
                Icon = navigation_icon(index), ExpandIcon = true, MouseDefaultCursorShape = CursorShape.PointingHand
            };
            button.AddThemeFontOverride("font", _inventory_font);
            button.AddThemeFontSizeOverride("font_size", 30);
            button.AddThemeConstantOverride("icon_max_width", 35);
            button.AddThemeConstantOverride("h_separation", 20);
            var idle = new StyleBoxEmpty { ContentMarginLeft = 21, ContentMarginRight = 16 };
            button.AddThemeStyleboxOverride("normal", idle);
            button.AddThemeStyleboxOverride("hover", idle);
            button.MouseEntered += () => button.Modulate = new Color(1.07f, 1.05f, 1.01f);
            button.MouseExited += () => button.Modulate = Colors.White;
            foreach (var state in new[] { "pressed", "hover_pressed" })
            {
                var paper = WorldHudStyle.Paper();
                paper.ContentMarginLeft = 21; paper.ContentMarginRight = 16;
                button.AddThemeStyleboxOverride(state, paper);
            }
            button.AddThemeStyleboxOverride("focus", focus_style());
            place(button, _canvas, 35, 190 + 64 * index, 204, 56);
            var outline = new InventoryPaperFrame { Gold = true, FillPaper = false };
            button.AddChild(outline);
            outline.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            outline.Visible = false;
            _category_buttons[category] = button;
            _category_frames[category] = outline;
            button.Pressed += () => filter_to_category(category);
        }

        var search = new LineEdit
        {
            PlaceholderText = "搜索物品名称", ClearButtonEnabled = true, MaxLength = 80
        };
        style_input(search);
        place(search, _canvas, 284, 94, 1224, 39);
        search.TextChanged += value =>
        {
            _search_text = value.Trim();
            _grid_scroll!.ScrollVertical = 0;
            refresh_slots();
        };
        var sort = new OptionButton { MouseDefaultCursorShape = CursorShape.PointingHand, TooltipText = "选择排序方式；按位置排列时可拖动物品调序" };
        foreach (var option in new[] { "名称", "数量", "类别", "位置" }) sort.AddItem(option);
        style_button(sort, 20, true);
        var popup = sort.GetPopup();
        popup.AddThemeFontOverride("font", _inventory_font);
        popup.AddThemeFontSizeOverride("font_size", 20);
        popup.AddThemeColorOverride("font_color", Ink);
        popup.AddThemeStyleboxOverride("panel", WorldHudStyle.Paper());
        place(sort, _canvas, 1520, 94, 111, 39);
        sort.ItemSelected += value => { _sort_mode = (int)value; _grid_scroll!.ScrollVertical = 0; refresh_slots(); };
        place(rule(), _canvas, 284, 140, 1348, 1);

        _grid_scroll = new ScrollContainer
        {
            Name = "InventoryScroll", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto, MouseFilter = MouseFilterEnum.Stop
        };
        place(_grid_scroll, _canvas, 284, 156, 1348, 704);
        _grid = new GridContainer { Name = "SlotGrid", Columns = GridMaximumColumns, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _grid.AddThemeConstantOverride("h_separation", (int)GridHorizontalGap);
        _grid.AddThemeConstantOverride("v_separation", 14);
        _grid_scroll.AddChild(_grid);
        style_scrollbar(_grid_scroll.GetVScrollBar());
        _empty = text("", 22, new Color("766d56"));
        _empty.HorizontalAlignment = HorizontalAlignment.Center;
        _empty.MouseFilter = MouseFilterEnum.Ignore;
        place(_empty, _canvas, 284, 460, 1348, 30);

        place(rule(), _canvas, 284, 874, 1348, 1);
        var footerMountain = new TextureRect
        {
            Texture = navigation_icon(10), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            Modulate = new Color("798674"), MouseFilter = MouseFilterEnum.Ignore
        };
        place(footerMountain, _canvas, 299, 882, 60, 30);
        place(text("悬停查看物品详情 · 左键选择可用物品", 19, new Color("514c3a")), _canvas, 378, 881, 750, 34);
        _summary = text("", 16, new Color("7b735e"));
        _summary.HorizontalAlignment = HorizontalAlignment.Right;
        place(_summary, _canvas, 1210, 883, 400, 30);
        build_detail_popup();
    }

    private void build_detail_popup()
    {
        _detail_panel = new PanelContainer
        {
            Name = "ItemPopup", CustomMinimumSize = new Vector2(PopupWidth, 0),
            Size = new Vector2(PopupWidth, 360), ZIndex = 30, MouseFilter = MouseFilterEnum.Stop
        };
        _detail_panel.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        _canvas!.AddChild(_detail_panel);
        _popup_frame = new InventoryPaperFrame { Dark = true };
        _detail_panel.AddChild(_popup_frame);
        var margin = new MarginContainer();
        foreach (var side in new[] { "left", "right" }) margin.AddThemeConstantOverride("margin_" + side, 22);
        foreach (var side in new[] { "top", "bottom" }) margin.AddThemeConstantOverride("margin_" + side, 20);
        _detail_panel.AddChild(margin);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 12);
        margin.AddChild(column);
        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 16);
        column.AddChild(header);
        _info_icon = new TextureRect
        {
            CustomMinimumSize = new Vector2(80, 80), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore
        };
        header.AddChild(_info_icon);
        var titles = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        titles.AddThemeConstantOverride("separation", 6);
        header.AddChild(titles);
        _info_name = text("", 29, PaperText);
        _info_name.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        titles.AddChild(_info_name);
        var category = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ShrinkBegin, MouseFilter = MouseFilterEnum.Ignore };
        category.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("414e40"), BorderColor = new Color("7b8971"),
            BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 7, CornerRadiusTopRight = 7, CornerRadiusBottomLeft = 7, CornerRadiusBottomRight = 7,
            ContentMarginLeft = 11, ContentMarginRight = 11, ContentMarginTop = 3, ContentMarginBottom = 3
        });
        titles.AddChild(category);
        _info_category = text("", 18, PaperText);
        _info_category.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        category.AddChild(_info_category);
        column.AddChild(rule(true));
        _detail_scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto, SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        column.AddChild(_detail_scroll);
        _detail_content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(PopupWidth - 58, 0) };
        _detail_content.AddThemeConstantOverride("separation", 10);
        _detail_scroll.AddChild(_detail_content);
        _info_count = text("", 20, MutedPaper); _detail_content.AddChild(_info_count);
        _info_grade = text("", 19, new Color("dbcdab")); _detail_content.AddChild(_info_grade);
        _info_tags = rich_text(20, PaperText); _detail_content.AddChild(_info_tags);
        _detail_content.AddChild(rule(true));
        _info_effect_title = text("使用效果", 19, new Color("d7c8a0")); _detail_content.AddChild(_info_effect_title);
        _info_desc = rich_text(19, MutedPaper); _detail_content.AddChild(_info_desc);
        style_scrollbar(_detail_scroll.GetVScrollBar());
        _action_row = new HBoxContainer();
        _action_row.AddThemeConstantOverride("separation", 14);
        column.AddChild(_action_row);
        _action_button = paper_button("使用", 27, true);
        _cancel_button = paper_button("取消", 27, false);
        _action_button.CustomMinimumSize = _cancel_button.CustomMinimumSize = new Vector2(0, 52);
        _action_button.SizeFlagsHorizontal = _cancel_button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _action_row.AddChild(_action_button); _action_row.AddChild(_cancel_button);
        _action_button.Pressed += on_action_pressed;
        _cancel_button.Pressed += clear_active_info;
        _detail_panel.MouseEntered += on_detail_mouse_entered;
        _detail_panel.MouseExited += on_detail_mouse_exited;
        _detail_panel.Hide();
    }

    private void layout_detail_popup(StorageItemSlot slot)
    {
        if (_canvas is null || _detail_panel is null || _detail_content is null || _detail_scroll is null) return;
        var actionHeight = _action_row?.Visible == true ? 64f : 0f;
        var natural = _detail_content.GetCombinedMinimumSize().Y;
        var bodyHeight = Mathf.Clamp(natural, 40f, 560f - actionHeight);
        if (!Mathf.IsEqualApprox(_detail_scroll.CustomMinimumSize.Y, bodyHeight))
        {
            _detail_scroll.CustomMinimumSize = new Vector2(0, bodyHeight);
            _detail_panel.ResetSize();
        }
        var popupSize = _detail_panel.GetCombinedMinimumSize();
        popupSize.X = PopupWidth;
        _detail_panel.Size = popupSize;
        var local = _canvas.GetGlobalTransform().AffineInverse() * slot.GetGlobalRect().Position;
        var leftSide = local.X < DesignSize.X * .4f;
        var anchorX = local.X + slot.Size.X * (leftSide ? .8f : .5f);
        var x = leftSide ? local.X + slot.Size.X * .6f : anchorX - PopupWidth * .5f;
        x = Mathf.Clamp(x, 276f, DesignSize.X - PopupWidth - 24f);
        var y = local.Y + slot.Size.Y + 13f;
        var above = false;
        var pointer = true;
        if (y + popupSize.Y > DesignSize.Y - 25f)
        {
            above = local.Y - popupSize.Y - 13f >= 78f;
            if (above) y = local.Y - popupSize.Y - 13f;
            else
            {
                pointer = false;
                y = Mathf.Clamp(local.Y, 78f, Mathf.Max(78f, DesignSize.Y - popupSize.Y - 25f));
                x = local.X + slot.Size.X + 14f;
                if (x + PopupWidth > DesignSize.X - 24f) x = local.X - PopupWidth - 14f;
                x = Mathf.Clamp(x, 276f, DesignSize.X - PopupWidth - 24f);
            }
        }
        _detail_panel.Position = new Vector2(x, y);
        _popup_frame!.PointerX = pointer ? Mathf.Clamp(anchorX - x, 16f, PopupWidth - 16f) : -1;
        _popup_frame.PointerAtBottom = above;
        _popup_frame.QueueRedraw();
    }

    private void fit_inventory_canvas()
    {
        if (_canvas is null) return;
        var viewport = Size.X > 0 && Size.Y > 0 ? Size : GetViewportRect().Size;
        var scale = Mathf.Min(viewport.X / DesignSize.X, viewport.Y / DesignSize.Y);
        _canvas.Scale = Vector2.One * scale;
        _canvas.Position = (viewport - DesignSize * scale) * .5f;
    }
    private Texture2D? navigation_icon(int index) => _navigation_atlas is null ? null : new AtlasTexture
    {
        Atlas = _navigation_atlas,
        Region = new Rect2((index % 4) * _navigation_atlas.GetWidth() / 4f, (index / 4) * _navigation_atlas.GetHeight() / 3f,
            _navigation_atlas.GetWidth() / 4f, _navigation_atlas.GetHeight() / 3f), FilterClip = true
    };
    private Label text(string value, int size, Color color)
    {
        var label = new Label { Text = value, VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
        label.AddThemeFontOverride("font", _inventory_font);
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeConstantOverride("outline_size", 0);
        return label;
    }
    private RichTextLabel rich_text(int size, Color color)
    {
        var label = new RichTextLabel
        {
            FitContent = true, ScrollActive = false, MouseFilter = MouseFilterEnum.Pass,
            SizeFlagsHorizontal = SizeFlags.ExpandFill, AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        label.AddThemeFontOverride("normal_font", _inventory_font);
        label.AddThemeFontSizeOverride("normal_font_size", size);
        label.AddThemeColorOverride("default_color", color);
        label.AddThemeConstantOverride("line_separation", 5);
        return label;
    }
    private Button paper_button(string value, int size, bool light)
    {
        var button = new Button { Text = value, MouseDefaultCursorShape = CursorShape.PointingHand };
        style_button(button, size, light);
        return button;
    }
    private void style_button(Button button, int size, bool light)
    {
        button.AddThemeFontOverride("font", _inventory_font);
        button.AddThemeFontSizeOverride("font_size", size);
        foreach (var state in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color", "font_hover_pressed_color" })
            button.AddThemeColorOverride(state, light ? Ink : PaperText);
        foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed" })
        {
            if (light) button.AddThemeStyleboxOverride(state, WorldHudStyle.Paper());
            else button.AddThemeStyleboxOverride(state, new StyleBoxFlat
            {
                BgColor = new Color(.08f, .09f, .07f, .84f),
                BorderColor = new Color(state is "hover" or "hover_pressed" ? "d4bc87" : "b6aa8a"), BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
                ContentMarginLeft = 12, ContentMarginRight = 12, CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4,
                CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4
            });
        }
        button.MouseEntered += () => { if (!button.Disabled) button.Modulate = new Color(1.07f, 1.05f, 1.01f); };
        button.MouseExited += () => button.Modulate = Colors.White;
        button.AddThemeStyleboxOverride("focus", focus_style());
    }
    private void style_input(LineEdit input)
    {
        input.AddThemeFontOverride("font", _inventory_font);
        input.AddThemeFontSizeOverride("font_size", 20);
        input.AddThemeColorOverride("font_color", Ink);
        input.AddThemeColorOverride("font_placeholder_color", new Color("827a64"));
        input.AddThemeColorOverride("caret_color", Ink);
        input.AddThemeStyleboxOverride("normal", WorldHudStyle.Paper());
        input.AddThemeStyleboxOverride("focus", focus_style());
    }
    private static void style_category_text(Button button, bool selected)
    {
        foreach (var state in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_hover_pressed_color" })
            button.AddThemeColorOverride(state, selected ? Ink : PaperText);
        button.AddThemeColorOverride("icon_normal_color", selected ? new Color("5a5945") : PaperText);
        button.AddThemeColorOverride("icon_pressed_color", new Color("5a5945"));
    }
    private static StyleBoxFlat focus_style() => new()
    {
        BgColor = Colors.Transparent, BorderColor = new Color("6e8a76"),
        BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1
    };
    private static ColorRect rule(bool dark = false) => new()
    {
        CustomMinimumSize = new Vector2(0, 1), Color = dark ? new Color("9c967c") : new Color("b4a687"),
        MouseFilter = MouseFilterEnum.Ignore
    };
    private static void style_scrollbar(VScrollBar bar)
    {
        bar.CustomMinimumSize = new Vector2(8, 0);
        bar.AddThemeStyleboxOverride("scroll", new StyleBoxFlat { BgColor = new Color(.3f, .3f, .24f, .06f), ContentMarginLeft = 3, ContentMarginRight = 3 });
        foreach (var state in new[] { "grabber", "grabber_highlight", "grabber_pressed" })
            bar.AddThemeStyleboxOverride(state, new StyleBoxFlat
            {
                BgColor = new Color(.42f, .46f, .36f, state == "grabber" ? .5f : .8f),
                ContentMarginLeft = 3, ContentMarginRight = 3, CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3,
                CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3
            });
    }
    private static void place(Control child, Control parent, float x, float y, float width, float height)
    {
        parent.AddChild(child);
        child.Position = new Vector2(x, y);
        child.Size = new Vector2(width, height);
    }
}
