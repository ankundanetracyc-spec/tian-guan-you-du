using Godot;
using System.Text.Json;

/// <summary>复用原题字笔迹，逐字顶对齐；从右向左第一、三句高，第二、四句低。</summary>
public partial class RealmPoemVisual : Control
{
    private readonly List<(TextureRect Image, Rect2 Region, int Column, int Row)> _glyphs = [];
    private float _lowOffset;
    private float _rowPitch;
    private float _glyphWidth;
    private float _glyphHeight;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        using var json = JsonDocument.Parse(Godot.FileAccess.GetFileAsString("res://resources/ui/realm_poem_layout.json"));
        var layout = json.RootElement;
        var source = ResourceLoader.Load<Texture2D>($"res://{layout.GetProperty("source").GetString()}");
        if (source is null) return;
        _lowOffset = layout.GetProperty("low_offset").GetSingle();
        _rowPitch = layout.GetProperty("row_pitch").GetSingle();
        _glyphWidth = layout.GetProperty("glyph_width").GetSingle();
        _glyphHeight = layout.GetProperty("glyph_height").GetSingle();
        var opacity = layout.GetProperty("opacity").GetSingle();
        var columnIndex = 0;
        foreach (var column in layout.GetProperty("columns_left_to_right").EnumerateArray())
        {
            var row = 0;
            foreach (var glyph in column.EnumerateArray())
            {
                var region = new Rect2(glyph[0].GetSingle(), glyph[1].GetSingle(), glyph[2].GetSingle(), glyph[3].GetSingle());
                var image = new TextureRect
                {
                    Texture = new AtlasTexture { Atlas = source, Region = region, FilterClip = true },
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.Scale,
                    MouseFilter = MouseFilterEnum.Ignore,
                    Modulate = new Color(1f, 1f, 1f, opacity)
                };
                AddChild(image);
                _glyphs.Add((image, region, columnIndex, row++));
            }
            columnIndex++;
        }
        Resized += LayoutGlyphs;
        LayoutGlyphs();
    }

    private void LayoutGlyphs()
    {
        if (_glyphs.Count == 0) return;
        var maxWidth = _glyphs.Max(glyph => glyph.Region.Size.X);
        var maxHeight = _glyphs.Max(glyph => glyph.Region.Size.Y);
        // 所有字统一等比缩放，逐字按可见墨迹上沿排布，不在格子内垂直居中。
        var scale = Mathf.Min(Size.X * _glyphWidth / maxWidth, Size.Y * _glyphHeight / maxHeight);
        foreach (var (image, region, column, row) in _glyphs)
        {
            image.Size = region.Size * scale;
            var x = Size.X * (.1f + column * (.8f / 3f)) - image.Size.X * .5f;
            var y = Size.Y * (row * _rowPitch + (column % 2 == 0 ? _lowOffset : 0f));
            image.Position = new Vector2(x, y);
        }
    }
}
