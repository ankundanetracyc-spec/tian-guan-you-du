using Godot;

/// <summary>建档控件：主次按钮、圆形步进、书写线输入与展开列表。</summary>
public partial class StartMenu
{
    private static readonly Color CreationInk = new("344e43"), CreationMuted = new("8f9587");
    private static readonly Color CreationPaper = new("f2efe3");
    private const string CreationFont = "res://resources/font/LXGWZhenKaiSlabGB-Regular.ttf";

    private static StyleBoxFlat creation_edge(Color fill, Color edge, int bottom = 1, bool outline = false) => new()
    {
        BgColor = fill, BorderColor = edge,
        BorderWidthBottom = bottom, BorderWidthTop = outline ? 1 : 0,
        BorderWidthLeft = outline ? 1 : 0, BorderWidthRight = outline ? 1 : 0,
        ContentMarginLeft = 12, ContentMarginRight = 12, ContentMarginTop = 6, ContentMarginBottom = 6
    };

    private static Button creation_button(string text)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(0, 40) };
        WorldHudStyle.StyleSmallButton(button, 17);
        button.AddThemeColorOverride("font_disabled_color", new Color("a1a497"));
        button.AddThemeColorOverride("font_pressed_color", CreationInk);
        button.AddThemeStyleboxOverride("normal", creation_edge(new Color(.95f, .94f, .89f, .7f), CreationMuted));
        button.AddThemeStyleboxOverride("hover", creation_edge(new Color("e4e8db"), CreationInk, 2));
        button.AddThemeStyleboxOverride("pressed", creation_edge(new Color("d2dccb"), CreationInk, 2));
        button.AddThemeStyleboxOverride("hover_pressed", creation_edge(new Color("d2dccb"), CreationInk, 2));
        button.AddThemeStyleboxOverride("disabled", creation_edge(new Color("e8e7de"), new Color("b5b7aa")));
        button.AddThemeStyleboxOverride("focus", creation_edge(Colors.Transparent, CreationInk, 2, true));
        return button;
    }

    private static void style_creation_primary(Button button)
    {
        foreach (var state in new[] { "normal", "hover", "pressed", "disabled" })
        {
            var fill = new Color(state switch { "hover" => "456455", "pressed" => "253e34", "disabled" => "909a8c", _ => "344e43" });
            button.AddThemeStyleboxOverride(state, creation_edge(fill, fill, 1, true));
        }
        foreach (var state in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color", "font_disabled_color" })
            button.AddThemeColorOverride(state, CreationPaper);
        button.AddThemeStyleboxOverride("focus", creation_edge(Colors.Transparent, new Color("b4baa1"), 2, true));
    }

    private static void style_creation_round(Button button)
    {
        button.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        button.AddThemeFontSizeOverride("font_size", 20);
        foreach (var state in new[] { "normal", "hover", "pressed", "disabled", "focus" })
        {
            var style = creation_edge(state == "focus" ? Colors.Transparent : new Color(state switch
            { "hover" => "e1e7d7", "pressed" => "cdd9c5", "disabled" => "eae9e0", _ => "f2efe3" }),
                state == "disabled" ? new Color("c5c7b9") : state is "hover" or "focus" or "pressed" ? CreationInk : CreationMuted, 1, true);
            style.SetCornerRadiusAll(20);
            style.ContentMarginLeft = style.ContentMarginRight = 0;
            style.ContentMarginTop = style.ContentMarginBottom = 0;
            button.AddThemeStyleboxOverride(state, style);
        }
    }

    private static Control creation_selector(OptionButton selector)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 8);
        var previous = creation_button("◀");
        var next = creation_button("▶");
        foreach (var button in new[] { previous, next })
        {
            style_creation_round(button);
            button.AddThemeFontSizeOverride("font_size", 12);
            button.CustomMinimumSize = new Vector2(34, 34);
            button.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        }
        void step(int direction)
        {
            if (selector.ItemCount == 0) return;
            var selected = (selector.Selected + direction + selector.ItemCount) % selector.ItemCount;
            selector.Select(selected);
            selector.EmitSignal(OptionButton.SignalName.ItemSelected, selected);
        }
        previous.Pressed += () => step(-1);
        next.Pressed += () => step(1);
        row.AddChild(previous); row.AddChild(selector); row.AddChild(next);
        selector.Alignment = HorizontalAlignment.Center;
        return row;
    }

    private static void style_creation_input(LineEdit input)
    {
        input.AddThemeFontOverride("font", GD.Load<FontFile>(CreationFont));
        input.AddThemeFontSizeOverride("font_size", 18);
        input.AddThemeColorOverride("font_color", CreationInk);
        input.AddThemeColorOverride("font_placeholder_color", new Color("8a9082"));
        input.AddThemeColorOverride("caret_color", CreationInk);
        input.AddThemeColorOverride("selection_color", new Color("cdd8c3"));
        input.AddThemeStyleboxOverride("normal", creation_edge(new Color(.97f, .96f, .92f, .72f), CreationMuted));
        input.AddThemeStyleboxOverride("focus", creation_edge(new Color("f7f5eb"), CreationInk, 2));
        input.AddThemeStyleboxOverride("read_only", creation_edge(new Color("e8e7df"), CreationMuted));
    }

    private static void style_creation_option(OptionButton option)
    {
        WorldHudStyle.StyleSmallButton(option, 17);
        option.ClipText = true;
        option.AddThemeIconOverride("arrow", GD.Load<Texture2D>("res://textures/ui/journey_creation_20261015/controls/selection_arrow.svg"));
        option.AddThemeConstantOverride("arrow_margin", 10);
        option.AddThemeConstantOverride("h_separation", 10);
        option.AddThemeStyleboxOverride("normal", creation_edge(CreationPaper, CreationMuted));
        option.AddThemeStyleboxOverride("hover", creation_edge(new Color("e4e8db"), CreationInk, 2));
        option.AddThemeStyleboxOverride("pressed", creation_edge(new Color("d8e0ce"), CreationInk, 2));
        option.AddThemeStyleboxOverride("focus", creation_edge(Colors.Transparent, CreationInk, 2));
        var popup = option.GetPopup();
        popup.AddThemeFontOverride("font", GD.Load<FontFile>(CreationFont));
        popup.AddThemeFontSizeOverride("font_size", 17);
        popup.AddThemeColorOverride("font_color", CreationInk);
        popup.AddThemeColorOverride("font_hover_color", CreationInk);
        popup.AddThemeColorOverride("font_disabled_color", CreationMuted);
        popup.AddThemeConstantOverride("v_separation", 10);
        popup.AddThemeStyleboxOverride("panel", creation_edge(CreationPaper, CreationMuted, 1, true));
        popup.AddThemeStyleboxOverride("hover", creation_edge(new Color("d9e2ce"), CreationInk, 1));
    }

    private static void style_creation_tab(Button button, bool active)
    {
        button.AddThemeStyleboxOverride("normal", creation_edge(Colors.Transparent, active ? CreationInk : new Color("c1c3b5"), active ? 3 : 1));
        button.AddThemeStyleboxOverride("hover", creation_edge(new Color(.86f, .9f, .83f, .5f), CreationInk, active ? 3 : 2));
        button.AddThemeStyleboxOverride("pressed", creation_edge(new Color("dbe3d1"), CreationInk, 3));
        button.AddThemeColorOverride("font_color", active ? CreationInk : new Color("757f71"));
        button.AddThemeColorOverride("font_hover_color", CreationInk);
    }
}
