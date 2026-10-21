using Godot;
using System.Collections.Generic;

/// <summary>炼器专用齐边纸面与青绿纹理；剑形保持独立透明素材。</summary>
public static class ForgingInkStyle
{
    public const string Assets = "res://textures/ui/forging_20261021/";
    private static readonly Dictionary<string, Texture2D> Textures = [];
    // 主体 alpha >=64 的边界外保留4像素；忽略生成图透明区的零星极淡像素。
    // 仅改变图集取样区域，保留原始透明PNG，避免纸片文字落在画布留白上。
    private static readonly Dictionary<string, Rect2> Regions = new()
    {
        ["material_slip_v2.png"] = new(60, 84, 2050, 560),
        ["preparation_ledger_v2.png"] = new(26, 39, 798, 1792),
        ["action_brush_v2.png"] = new(36, 132, 2113, 450),
        ["slender_sword_ink_v2.png"] = new(33, 249, 2140, 242),
        ["broad_sword_ink_v2.png"] = new(7, 263, 1996, 290),
        ["spined_sword_ink_v2.png"] = new(14, 232, 2144, 270)
    };
    public static Texture2D Texture(string file)
    {
        if (Textures.TryGetValue(file, out var texture)) return texture;
        var source = GD.Load<Texture2D>(Assets + file);
        texture = new AtlasTexture { Atlas = source, Region = Regions.GetValueOrDefault(file, new Rect2(Vector2.Zero, source.GetSize())), FilterClip = true };
        Textures[file] = texture;
        return texture;
    }
    private static Texture2D PlainPaper()
    {
        const string key = "paper-center-v3";
        if (!Textures.TryGetValue(key, out var texture))
        {
            texture = new AtlasTexture { Atlas = GD.Load<Texture2D>(Assets + "material_panel_v3.png"), Region = new Rect2(600, 180, 972, 340), FilterClip = true };
            Textures[key] = texture;
        }
        return texture;
    }
    public static StyleBoxTexture Paper(bool tall = false, float alpha = 1, bool selected = false, int padding = 8, bool plain = false)
    {
        return new StyleBoxTexture
        {
            Texture = plain ? PlainPaper() : Texture(tall ? "ledger_panel_v3.png" : "material_panel_v3.png"),
            TextureMarginLeft = 12, TextureMarginRight = 12, TextureMarginTop = 12, TextureMarginBottom = 12,
            ContentMarginLeft = padding, ContentMarginRight = padding, ContentMarginTop = padding, ContentMarginBottom = padding,
            ModulateColor = selected ? new Color(.95f, .99f, .97f, alpha) : new Color(1, 1, 1, alpha)
        };
    }
    public static StyleBoxTexture Brush(float alpha = 1)
    {
        return new StyleBoxTexture
        {
            Texture = Texture("jade_button_v3.png"), TextureMarginLeft = 16, TextureMarginRight = 16,
            TextureMarginTop = 8, TextureMarginBottom = 8,
            ContentMarginLeft = 14, ContentMarginRight = 14, ContentMarginTop = 6, ContentMarginBottom = 6,
            ModulateColor = new Color(1, 1, 1, alpha)
        };
    }
    public static void Button(Button button, bool primary = false)
    {
        button.AddThemeStyleboxOverride("normal", primary ? Brush() : Paper(alpha: .82f, padding: 5, plain: true));
        button.AddThemeStyleboxOverride("hover", primary ? Brush(.85f) : Paper(selected: true, padding: 5, plain: true));
        button.AddThemeStyleboxOverride("pressed", primary ? Brush(.95f) : Paper(selected: true, padding: 5, plain: true));
        button.AddThemeStyleboxOverride("disabled", primary ? Brush(.38f) : Paper(alpha: .45f, padding: 5, plain: true));
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        if (primary)
        {
            foreach (var name in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color", "font_disabled_color" })
                button.AddThemeColorOverride(name, new Color("f3eedc"));
        }
    }
    public static string SwordFile(string form) => form switch
    {
        "细身剑" => "slender_sword_ink_v2.png", "阔刃剑" => "broad_sword_ink_v2.png", _ => "spined_sword_ink_v2.png"
    };
}
