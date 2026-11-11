using Godot;
using System.Text;

/// <summary>固定占位的小书脊。题签动态适配，选中只移动视觉层，命中区及相邻书不挪动。</summary>
public partial class YuxuManualSpine : Button
{
    public static readonly Vector2 SpineSize = new(42, 170);
    private static readonly Vector2 PullOffset = new(-5, -14);
    private Control _visual = null!;
    private Label _title = null!;
    private Texture2D? _coverTexture;
    private Tween? _tween;
    private bool _selected;
    private float _pullProgress;
    public void Configure(string name, Texture2D? manual)
    {
        Size = SpineSize; MouseDefaultCursorShape = CursorShape.PointingHand;
        _coverTexture = manual;
        YuxuLibraryArt.ButtonStyle(this, 18, false);
        _visual = new Control { Name = "SpineVisual", Size = SpineSize, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_visual);
        _visual.Draw += DrawBookDepth;
        var backing = new ColorRect { Size = SpineSize, Color = new("64717b"), MouseFilter = MouseFilterEnum.Ignore }; _visual.AddChild(backing);
        if (manual is not null)
        {
            var texture = new TextureRect
            {
                Texture = new AtlasTexture { Atlas = manual, Region = new Rect2(0, 0, manual.GetWidth() * .33f, manual.GetHeight()) },
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale, Size = SpineSize,
                MouseFilter = MouseFilterEnum.Ignore
            };
            _visual.AddChild(texture);
        }
        var paper = new Panel { Position = new(8, 7), Size = new(25, 155), MouseFilter = MouseFilterEnum.Ignore };
        paper.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new("e7dbc1"), BorderColor = new("9c8d71"), BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1 });
        _visual.AddChild(paper);
        var characters = name.EnumerateRunes().Select(r => r.ToString()).ToArray();
        var font = YuxuLibraryArt.Font(); int size = 18;
        _title = new Label { Text = string.Join("\n", characters), HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center, ClipText = true, MouseFilter = MouseFilterEnum.Ignore };
        _title.AddThemeFontOverride("font", font); _title.AddThemeFontSizeOverride("font_size", size);
        _title.AddThemeColorOverride("font_color", YuxuLibraryArt.Ink); _title.AddThemeConstantOverride("line_spacing", -4);
        paper.AddChild(_title);
        // 以实际排版最小高度适配，避免字体行高／主题缓存与手算值不同而撑出题签。
        while (size > 9 && (_title.GetMinimumSize().Y > 149 || characters.Any(c => font.GetStringSize(c, fontSize: size).X > 23)))
            _title.AddThemeFontSizeOverride("font_size", --size);
        _title.Size = new(25, 155); SetMeta("title_font_size", size);
    }
    public void SetMatched(bool matched) => _visual.Modulate = matched ? Colors.White : new Color(.48f, .48f, .48f);
    public void SetSelected(bool selected)
    {
        if (_selected == selected) return;
        _selected = selected;
        _tween?.Kill();
        var target = selected ? 1f : 0f;
        if (!IsInsideTree()) { SetPullProgress(target); return; }
        _tween = CreateTween().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        _tween.TweenMethod(Callable.From<float>(SetPullProgress), _pullProgress, target, .24);
    }
    private void SetPullProgress(float progress)
    {
        _pullProgress = progress;
        _visual.Position = PullOffset * progress;
        ZIndex = _selected || progress > 0 ? 3 : 1;
        _visual.QueueRedraw();
    }
    private void DrawBookDepth()
    {
        var p = _pullProgress;
        var shadow = new Vector2(3 + 3 * p, 4 + 10 * p);
        _visual.DrawRect(new Rect2(shadow + Vector2.One, SpineSize), new Color(0, 0, 0, .05f));
        _visual.DrawRect(new Rect2(shadow, SpineSize), new Color(0, 0, 0, .17f + .05f * p));
        if (p <= 0) return;

        // 右侧为布面封面，顶部为纸页；页层纹理沿纵深延伸，与两侧封皮平行。
        float w = SpineSize.X, h = SpineSize.Y;
        var depth = new Vector2(5 * p, -2 * p);
        _visual.DrawColoredPolygon([new(0, 0), depth, new Vector2(w, 0) + depth, new(w, 0)], new Color("7a8388"));
        var paperLeft = new Vector2(1.3f, 0);
        var paperRight = new Vector2(w - 1.3f, 0);
        _visual.DrawColoredPolygon([paperLeft, paperLeft + depth, paperRight + depth, paperRight], new Color("e1d6bd"));
        for (float x = 3; x < w - 2; x += 3)
        {
            var sheet = new Vector2(x, 0);
            var layerWidth = new Vector2(.6f, 0);
            _visual.DrawColoredPolygon([sheet, sheet + depth, sheet + depth + layerWidth, sheet + layerWidth],
                new Color(.58f, .53f, .44f, .5f * p));
        }
        Vector2[] cover = [new(w - .5f, 0), new Vector2(w, 0) + depth,
            new Vector2(w, h) + depth, new(w - .5f, h)];
        _visual.DrawColoredPolygon(cover, new Color("64717b"));
        if (_coverTexture is not null)
            _visual.DrawPolygon(cover, new Color[] { new(.82f, .85f, .88f) },
                new Vector2[] { new(.20f, .08f), new(.88f, .08f), new(.88f, .90f), new(.20f, .90f) }, _coverTexture);
        _visual.DrawLine(new Vector2(w, 0) + depth, new Vector2(w, h) + depth, new Color("49545c"), .6f, true);
    }
}
