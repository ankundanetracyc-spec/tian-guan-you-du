using Godot;

/// <summary>宣纸切角签框。纹理由既有纸面素材提供，轮廓由控件尺寸绘制。</summary>
public partial class WorldHudFrame : Control
{
    private Texture2D _paper = null!;
    public bool ShowBorder { get; set; } = true;
    public bool Circular { get; set; }
    public bool Rectangular { get; set; }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _paper = GD.Load<Texture2D>(WorldHudStyle.PaperPath);
        Resized += QueueRedraw;
    }

    public override void _Draw()
    {
        if (_paper is null || Size.X <= 0 || Size.Y <= 0) return;
        var w = Size.X - 1;
        var h = Size.Y - 1;
        const float cut = 7;
        Vector2[] edge = [new(cut, 0), new(w-cut, 0), new(w, cut), new(w, h-cut),
            new(w-cut, h), new(cut, h), new(0, h-cut), new(0, cut)];
        if (Rectangular)
            edge = [new(0, 0), new(w, 0), new(w, h), new(0, h)];
        if (Circular)
        {
            edge = new Vector2[64];
            var center = new Vector2(w, h) / 2;
            var radius = Mathf.Min(w, h) / 2;
            for (var i = 0; i < edge.Length; i++)
                edge[i] = center + Vector2.FromAngle(Mathf.Tau * i / edge.Length) * radius;
        }
        var uv = new Vector2[edge.Length];
        var textureSize = _paper.GetSize();
        var paperOrigin = Rectangular ? new Vector2(100, 140) : new Vector2(20, 78);
        var paperSize = Rectangular ? new Vector2(1972, 420) : new Vector2(2132, 568);
        for (var i = 0; i < edge.Length; i++)
            uv[i] = (paperOrigin + edge[i] / Size * paperSize) / textureSize;
        DrawPolygon(edge, new Color[] { Colors.White }, uv, _paper);
        if (!ShowBorder) return;
        Vector2[] outline = [..edge, edge[0]];
        DrawPolyline(outline, new Color("968772"), 1, true);
        if (Circular || Rectangular) return;
        // 角内短笔只占用边缘，不侵入文字区。
        DrawLine(new Vector2(10, 4), new Vector2(24, 4), new Color(.50f, .45f, .36f, .35f), 1, true);
        DrawLine(new Vector2(w-24, h-4), new Vector2(w-10, h-4), new Color(.50f, .45f, .36f, .35f), 1, true);
    }
}
