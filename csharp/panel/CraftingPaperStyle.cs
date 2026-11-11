using Godot;
using System;
using System.Collections.Generic;

/// <summary>炼丹页显式采用的纸墨材质；不修改静室样式、全局主题或任何炼制规则。</summary>
public static class CraftingPaperStyle
{
    public const string Assets = "res://textures/ui/crafting_paper_20261108/";
    public const string FontPath = "res://resources/font/LXGWZhenKaiSlabGB-Regular.ttf";
    public const int RegularHeight = 43, InputHeight = 43, IconSize = 34, PrimaryHeight = 56;
    public const float SelectedDarkening = .84f;
    public static readonly Color WarmControlTint = new(1.025f, 1f, .975f);
    public static readonly Color Ink = new("303e37"), Muted = new("687265"),
        Accent = new("536c5d"), Danger = new("925547"), DisabledInk = new("858779");
    public enum ButtonKind { Regular, Primary, Tab, Icon, Input, Navigation }
    public enum ButtonState { Normal, Hover, Pressed, Disabled, Selected }

    // Slices are source pixels; Rendered is the independent border width in design pixels.
    private sealed record Frame(string File, Rect2I Bounds, Vector4I Slices, Vector4I Rendered, bool CleanAlpha = false);
    private static readonly Frame PanelFrame = new("frame_atlas.png", new(1105, 160, 537, 744), new(38, 38, 38, 38), new(10, 10, 10, 10));
    private static readonly Frame PaperFrame = new("frame_atlas.png", new(1141, 200, 450, 520), new(12, 12, 12, 12), new(8, 8, 8, 8));
    private static readonly Frame TitleFrame = new("frame_atlas.png", new(525, 3, 620, 97), new(38, 30, 38, 28), new(30, 22, 30, 20));
    private static readonly Frame RegularFrame = new("button_regular.png", new(121, 200, 1823, 361), new(85, 55, 85, 55), new(10, 5, 10, 6), true);
    private static readonly Frame PrimaryFrame = new("button_primary.png", new(55, 190, 2062, 343), new(90, 65, 90, 78), new(16, 6, 16, 10), true);
    private static readonly Frame TabFrame = new("button_tab.png", new(86, 157, 1772, 495), new(65, 100, 190, 115), new(5, 7, 13, 8), true);
    private static readonly Frame IconFrame = new("button_icon.png", new(272, 279, 712, 699), new(135, 135, 135, 135), new(8, 8, 8, 8), true);
    private static readonly Frame InputFrame = new("input.png", new(82, 316, 1610, 255), new(45, 45, 45, 45), new(6, 6, 6, 6), true);
    private static readonly Frame NavigationFrame = new("button_navigation.png", new(253, 110, 1853, 502), new(85, 85, 90, 80), new(8, 8, 10, 8), true);
    private static readonly Frame NavigationBindingFrame = new("button_navigation.png", new(68, 110, 184, 502), new(30, 85, 30, 80), new(3, 8, 3, 8), true);
    private static readonly Dictionary<string, Rect2I> SpriteRegions = new(StringComparer.Ordinal)
    {
        ["checkOff"] = new(68, 93, 155, 158), ["checkOn"] = new(343, 93, 155, 159),
        ["radioOff"] = new(613, 89, 160, 170), ["radioOn"] = new(890, 87, 165, 172),
        ["left"] = new(73, 393, 139, 113), ["right"] = new(361, 393, 134, 113),
        ["up"] = new(633, 403, 122, 89), ["down"] = new(915, 407, 121, 91),
        ["search"] = new(66, 646, 155, 152), ["close"] = new(350, 652, 140, 138),
        ["help"] = new(609, 640, 166, 169), ["reset"] = new(898, 650, 156, 151),
        ["scrollTrack"] = new(123, 885, 44, 211), ["scrollThumb"] = new(392, 929, 57, 131),
        ["progressTrack"] = new(574, 965, 245, 58), ["progressFill"] = new(857, 967, 238, 54),
        ["node"] = new(62, 1188, 165, 166), ["nodeSelected"] = new(338, 1187, 167, 168),
        ["divider"] = new(580, 1246, 229, 48), ["grip"] = new(900, 1232, 149, 77)
    };
    private static readonly Dictionary<string, Texture2D> Textures = new(StringComparer.Ordinal);
    private static readonly Dictionary<Frame, AtlasTexture> OriginalFrames = [];
    private static readonly Dictionary<(Frame Frame, float Saturation, bool Selected), Texture2D> PreparedFrames = [];
    private static FontFile? _font;
    private static FontFile Font => _font ??= GD.Load<FontFile>(FontPath);

    private static Texture2D Texture(string file)
    {
        if (!Textures.TryGetValue(file, out var result))
        {
            var path=Assets+file;
            result=ResourceLoader.Exists(path)?GD.Load<Texture2D>(path):null;
            // 独立入口也会创建人物详情；新PNG尚未经编辑器导入时直接读原图。
            if(result is null && System.IO.File.Exists(ProjectSettings.GlobalizePath(path)))
            {
                using var image=Image.LoadFromFile(ProjectSettings.GlobalizePath(path));
                result=ImageTexture.CreateFromImage(image);
            }
            Textures[file]=result??throw new InvalidOperationException("Missing crafting material: "+path);
        }
        return result;
    }

    private static AtlasTexture Source(Frame frame)
    {
        if (!OriginalFrames.TryGetValue(frame, out var result))
            OriginalFrames[frame] = result = new AtlasTexture
            {
                Atlas = Texture(frame.File), Region = new Rect2(frame.Bounds.Position, frame.Bounds.Size), FilterClip = true
            };
        return result;
    }

    private static Frame ButtonFrame(ButtonKind kind) => kind switch
    {
        ButtonKind.Primary => PrimaryFrame, ButtonKind.Tab => TabFrame,
        ButtonKind.Icon => IconFrame, ButtonKind.Input => InputFrame,
        ButtonKind.Navigation => NavigationFrame, _ => RegularFrame
    };

    /// <summary>原PNG的真实裁取区，供只读核对；绘制缓存不覆盖此纹理。</summary>
    public static AtlasTexture SourceTexture(ButtonKind kind = ButtonKind.Regular) => Source(ButtonFrame(kind));

    // StyleBoxTexture uses one margin for source and destination. Prepare its nine patches
    // in memory so the approved 85px source corner can draw as a 10px border. No PNG is saved.
    private static Texture2D Prepared(Frame frame, float saturation = 1, bool selected = false)
    {
        var key = (frame, saturation, selected);
        if (PreparedFrames.TryGetValue(key, out var cached)) return cached;
        using var source = Source(frame).GetImage();
        if (source.IsCompressed()) source.Decompress();
        source.Convert(Image.Format.Rgba8);
        var slices = frame.Slices; var rendered = frame.Rendered;
        int centerWidth = frame.Bounds.Size.X - slices.X - slices.Z;
        int centerHeight = frame.Bounds.Size.Y - slices.Y - slices.W;
        int width = centerWidth + rendered.X + rendered.Z;
        int height = centerHeight + rendered.Y + rendered.W;
        using var prepared = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
        int[] sx = [0, slices.X, frame.Bounds.Size.X - slices.Z, frame.Bounds.Size.X];
        int[] sy = [0, slices.Y, frame.Bounds.Size.Y - slices.W, frame.Bounds.Size.Y];
        int[] dx = [0, rendered.X, width - rendered.Z, width];
        int[] dy = [0, rendered.Y, height - rendered.W, height];
        for (int row = 0; row < 3; row++) for (int col = 0; col < 3; col++)
        {
            using var patch = source.GetRegion(new Rect2I(sx[col], sy[row], sx[col + 1] - sx[col], sy[row + 1] - sy[row]));
            int patchWidth = dx[col + 1] - dx[col], patchHeight = dy[row + 1] - dy[row];
            if (patch.GetWidth() != patchWidth || patch.GetHeight() != patchHeight)
                patch.Resize(patchWidth, patchHeight, Image.Interpolation.Lanczos);
            prepared.BlitRect(patch, new Rect2I(0, 0, patchWidth, patchHeight), new Vector2I(dx[col], dy[row]));
        }
        var data = prepared.GetData();
        for (int index = 0; index < data.Length; index += 4)
        {
            if (frame.CleanAlpha)
                data[index + 3] = (byte)Math.Clamp((int)Math.Round((data[index + 3] - 8) * 255d / 247), 0, 255);
            double red = data[index], green = data[index + 1], blue = data[index + 2];
            if (selected)
            {
                // Keep the original warm paper hue; selection only darkens it.
                red *= SelectedDarkening; green *= SelectedDarkening; blue *= SelectedDarkening;
            }
            double luminance = .213 * red + .715 * green + .072 * blue;
            data[index] = Channel(luminance + (red - luminance) * saturation);
            data[index + 1] = Channel(luminance + (green - luminance) * saturation);
            data[index + 2] = Channel(luminance + (blue - luminance) * saturation);
        }
        using var tinted = Image.CreateFromData(width, height, false, Image.Format.Rgba8, data);
        var texture = ImageTexture.CreateFromImage(tinted);
        texture.ResourceName = $"CraftingPaper/{frame.File}/saturation{saturation}/selected{selected}";
        return PreparedFrames[key] = texture;
    }

    private static byte Channel(double value) => (byte)Math.Clamp((int)Math.Round(value), 0, 255);

    private static StyleBoxTexture FrameStyle(Frame frame, int horizontalPadding, int verticalPadding,
        float brightness = 1, float alpha = 1, float saturation = 1, bool selected = false) => new()
    {
        Texture = Prepared(frame, saturation, selected),
        TextureMarginLeft = frame.Rendered.X, TextureMarginTop = frame.Rendered.Y,
        TextureMarginRight = frame.Rendered.Z, TextureMarginBottom = frame.Rendered.W,
        AxisStretchHorizontal = StyleBoxTexture.AxisStretchMode.Stretch,
        AxisStretchVertical = StyleBoxTexture.AxisStretchMode.Stretch,
        ContentMarginLeft = horizontalPadding, ContentMarginRight = horizontalPadding,
        ContentMarginTop = verticalPadding, ContentMarginBottom = verticalPadding,
        ModulateColor = new Color(brightness, brightness, brightness, alpha)
    };

    public static StyleBoxTexture Paper(int padding = 18, bool bordered = true, float brightness = .88f, float alpha = 1, float? saturation = null) =>
        FrameStyle(bordered ? PanelFrame : PaperFrame, padding, padding, brightness, alpha, saturation ?? (bordered ? .7f : 1));

    public static StyleBoxTexture Title(int padding = 12) => FrameStyle(TitleFrame, padding, 9);

    /// <summary>选中导航纸签左侧显露的原灰蓝装订边；由调用页控制18px位移及显隐。</summary>
    public static StyleBoxTexture BindingStyle() => FrameStyle(NavigationBindingFrame, 0, 0, .94f, 1, .8f);

    // -1 selects the sample's per-kind padding, including the compact 34px icon control.
    public static StyleBoxTexture ButtonStyle(ButtonKind kind = ButtonKind.Regular, ButtonState state = ButtonState.Normal,
        int horizontalPadding = -1, int verticalPadding = -1) =>
        CreateButtonStyle(kind, state, state == ButtonState.Selected, horizontalPadding, verticalPadding);

    private static StyleBoxTexture CreateButtonStyle(ButtonKind kind, ButtonState state, bool selected,
        int horizontalPadding = -1, int verticalPadding = -1)
    {
        int x = horizontalPadding >= 0 ? horizontalPadding : kind switch { ButtonKind.Icon => 0, ButtonKind.Input => 10, ButtonKind.Tab => 10, ButtonKind.Navigation => 12, _ => 18 };
        int y = verticalPadding >= 0 ? verticalPadding : kind switch { ButtonKind.Icon => 0, ButtonKind.Input => 4, ButtonKind.Tab => 6, ButtonKind.Navigation => 12, _ => 7 };
        float brightness = state switch { ButtonState.Hover => .99f, ButtonState.Pressed => .88f, ButtonState.Disabled => .91f, _ => .94f };
        float saturation = state == ButtonState.Disabled ? .45f : 1;
        bool showSelection = selected && kind != ButtonKind.Navigation && state != ButtonState.Disabled;
        var face = FrameStyle(ButtonFrame(kind), x, y, brightness, 1, saturation, showSelection);
        if (state != ButtonState.Disabled) face.ModulateColor *= WarmControlTint;
        return face;
    }

    public static void ApplyButton(Button button, ButtonKind kind = ButtonKind.Regular, int fontSize = 22, bool selected = false)
    {
        foreach (var (name, state) in new[]
        {
            ("normal", ButtonState.Normal), ("hover", ButtonState.Hover), ("pressed", ButtonState.Pressed),
            ("hover_pressed", ButtonState.Hover), ("disabled", ButtonState.Disabled)
        }) button.AddThemeStyleboxOverride(name, CreateButtonStyle(kind, state, selected));
        button.AddThemeStyleboxOverride("focus", Focus());
        foreach (var name in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_hover_pressed_color", "font_focus_color" })
            button.AddThemeColorOverride(name, Ink);
        button.AddThemeColorOverride("font_disabled_color", DisabledInk);
        button.AddThemeFontOverride("font", Font); button.AddThemeFontSizeOverride("font_size", fontSize);
        button.AddThemeConstantOverride("outline_size", 0);
        button.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
        if (kind == ButtonKind.Icon)
        {
            button.ExpandIcon = true;
            button.IconAlignment = HorizontalAlignment.Center;
            button.VerticalIconAlignment = VerticalAlignment.Center;
            button.AddThemeConstantOverride("icon_max_width", 18);
        }
        int minimum = kind switch { ButtonKind.Primary => PrimaryHeight, ButtonKind.Icon => IconSize, ButtonKind.Tab => 39, ButtonKind.Navigation => 69, _ => RegularHeight };
        button.CustomMinimumSize = new Vector2(kind == ButtonKind.Icon ? IconSize : button.CustomMinimumSize.X,
            Math.Max(minimum, button.CustomMinimumSize.Y));
    }

    public static void ApplyInput(LineEdit input, int fontSize = 23, int horizontalPadding = 10, int verticalPadding = 4)
    {
        input.AddThemeColorOverride("font_placeholder_color", Muted);
        input.AddThemeStyleboxOverride("normal", ButtonStyle(ButtonKind.Input, horizontalPadding: horizontalPadding, verticalPadding: verticalPadding));
        input.AddThemeStyleboxOverride("read_only", ButtonStyle(ButtonKind.Input, ButtonState.Disabled, horizontalPadding, verticalPadding));
        input.AddThemeStyleboxOverride("focus", Focus());
        input.AddThemeFontOverride("font", Font); input.AddThemeFontSizeOverride("font_size", fontSize);
        input.AddThemeColorOverride("font_color", Ink); input.AddThemeColorOverride("font_uneditable_color", DisabledInk);
        input.AddThemeColorOverride("caret_color", Accent); input.AddThemeColorOverride("selection_color", new Color(.33f, .42f, .36f, .24f));
        input.AddThemeConstantOverride("outline_size", 0);
        input.CustomMinimumSize = new Vector2(input.CustomMinimumSize.X, Math.Max(InputHeight, input.CustomMinimumSize.Y));
    }

    private static StyleBoxFlat Focus() => new()
    {
        BgColor = Colors.Transparent, DrawCenter = false, BorderColor = new Color("89745f"),
        BorderWidthLeft = 2, BorderWidthRight = 2, BorderWidthTop = 2, BorderWidthBottom = 2,
        ExpandMarginLeft = 3, ExpandMarginRight = 3, ExpandMarginTop = 3, ExpandMarginBottom = 3
    };

    public static Texture2D Sprite(string name)
    {
        string key = "sprite:" + name;
        if (Textures.TryGetValue(key, out var result)) return result;
        if (!SpriteRegions.TryGetValue(name, out var bounds)) throw new ArgumentException("Unknown crafting sprite: " + name, nameof(name));
        return Textures[key] = new AtlasTexture
        {
            Atlas = Texture("control_atlas.png"), Region = new Rect2(bounds.Position, bounds.Size), FilterClip = true
        };
    }

    private static Texture2D SmallSprite(string name, int size)
    {
        string key = "small:" + name + ":" + size;
        if (Textures.TryGetValue(key, out var result)) return result;
        using var image = Sprite(name).GetImage();
        if (image.IsCompressed()) image.Decompress();
        image.Resize(size, size, Image.Interpolation.Lanczos);
        return Textures[key] = ImageTexture.CreateFromImage(image);
    }

    public static Label Text(string text, int size = 22, bool muted = false)
    {
        var label = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontOverride("font", Font); label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", muted ? Muted : Ink);
        label.AddThemeConstantOverride("outline_size", 0);
        return label;
    }

    private static StyleBoxTexture SpriteStyle(string name, int left = 0, int top = 0, int right = 0, int bottom = 0) => new()
    {
        Texture = Sprite(name), TextureMarginLeft = 0, TextureMarginTop = 0, TextureMarginRight = 0, TextureMarginBottom = 0,
        ContentMarginLeft = left, ContentMarginTop = top, ContentMarginRight = right, ContentMarginBottom = bottom
    };

    public static HSeparator Divider() => new() { MouseFilter = Control.MouseFilterEnum.Ignore };

    private static StyleBoxLine SeparatorStyle() => new()
    {
        Color = new Color("a59783"), Thickness = 1, Vertical = false
    };

    /// <summary>返回新主题，只赋给炼丹页；绝不修改默认Theme或其他界面。</summary>
    public static Theme MakeTheme()
    {
        var theme = new Theme { DefaultFont = Font, DefaultFontSize = 22 };
        theme.SetColor("font_color", "Label", Ink); theme.SetColor("default_color", "RichTextLabel", Ink);
        theme.SetColor("font_shadow_color", "Label", Colors.Transparent);
        theme.SetConstant("outline_size", "Label", 0);
        foreach (var type in new[] { "Button", "OptionButton", "CheckButton", "CheckBox" })
        {
            theme.SetFontSize("font_size", type, 22);
            foreach (var name in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_hover_pressed_color", "font_focus_color" })
                theme.SetColor(name, type, Ink);
            theme.SetColor("font_disabled_color", type, DisabledInk); theme.SetConstant("outline_size", type, 0);
            theme.SetStylebox("normal", type, ButtonStyle()); theme.SetStylebox("hover", type, ButtonStyle(state: ButtonState.Hover));
            theme.SetStylebox("pressed", type, ButtonStyle(state: ButtonState.Selected));
            theme.SetStylebox("hover_pressed", type, ButtonStyle(state: ButtonState.Selected));
            theme.SetStylebox("disabled", type, ButtonStyle(state: ButtonState.Disabled)); theme.SetStylebox("focus", type, Focus());
        }
        theme.SetStylebox("panel", "Panel", Paper()); theme.SetStylebox("panel", "PanelContainer", Paper());
        theme.SetStylebox("normal", "LineEdit", ButtonStyle(ButtonKind.Input));
        theme.SetStylebox("read_only", "LineEdit", ButtonStyle(ButtonKind.Input, ButtonState.Disabled));
        theme.SetStylebox("focus", "LineEdit", Focus()); theme.SetFontSize("font_size", "LineEdit", 23);
        theme.SetColor("font_color", "LineEdit", Ink); theme.SetColor("font_uneditable_color", "LineEdit", DisabledInk);
        theme.SetColor("caret_color", "LineEdit", Accent); theme.SetConstant("outline_size", "LineEdit", 0);
        theme.SetColor("selection_color", "LineEdit", new Color(.33f, .42f, .36f, .24f));
        theme.SetStylebox("panel", "PopupMenu", Paper(16, brightness: .92f));
        theme.SetStylebox("hover", "PopupMenu", ButtonStyle(ButtonKind.Regular, ButtonState.Selected));
        theme.SetColor("font_color", "PopupMenu", Ink); theme.SetColor("font_hover_color", "PopupMenu", Ink);
        theme.SetStylebox("panel", "TooltipPanel", Paper(18, false, 1));
        theme.SetColor("font_color", "TooltipLabel", Ink); theme.SetFontSize("font_size", "TooltipLabel", 18);
        theme.SetStylebox("separator", "HSeparator", SeparatorStyle());
        theme.SetStylebox("background", "ProgressBar", SpriteStyle("progressTrack"));
        theme.SetStylebox("fill", "ProgressBar", SpriteStyle("progressFill"));
        theme.SetStylebox("scroll", "VScrollBar", PaperScrollbarStyle.Create());
        theme.SetStylebox("scroll_focus", "VScrollBar", PaperScrollbarStyle.Create());
        theme.SetStylebox("grabber", "VScrollBar", PaperScrollbarStyle.Create(grabber: true));
        theme.SetStylebox("grabber_highlight", "VScrollBar", PaperScrollbarStyle.Create(grabber: true, highlighted: true));
        theme.SetStylebox("grabber_pressed", "VScrollBar", PaperScrollbarStyle.Create(grabber: true, pressed: true));
        foreach (var type in new[] { "CheckBox", "CheckButton" })
        {
            theme.SetIcon("checked", type, SmallSprite("checkOn", 22)); theme.SetIcon("unchecked", type, SmallSprite("checkOff", 22));
            theme.SetIcon("checked_disabled", type, SmallSprite("checkOn", 22)); theme.SetIcon("unchecked_disabled", type, SmallSprite("checkOff", 22));
            theme.SetConstant("h_separation", type, 10);
        }
        theme.SetIcon("arrow", "OptionButton", SmallSprite("down", 14));
        return theme;
    }
}
