using Godot;

/// <summary>药材先浮起、弧线牵入，末段化成同色淡墨；只呈现已提交的投料。</summary>
public partial class AlchemyIngredientFlow : Control
{
    public Texture2D? Texture;
    public Vector2 From, Mouth;
    public int Seed;
    public float LiftHeight = 60, EntryReach = 64, CurlStrength = 1, FinalScale = .54f, TrailOpacity = 1;
    private TextureRect _image = null!;
    private ShaderMaterial _wash = null!;
    private Color _pigment = new(.52f, .56f, .43f);
    private const int Strands = 3, Samples = 18;
    private readonly Vector2[][] _trails = new Vector2[Strands][];
    private readonly Color[][] _colors = new Color[Strands][];
    private float _progress;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _wash = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/alchemy_ingredient_wash.gdshader") };
        _wash.SetShaderParameter("phase", Seed * 1.73f);
        _image = new TextureRect { Texture = Texture, Material = _wash,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore, Size = Size, PivotOffset = Size * .5f };
        AddChild(_image);
        for (int strand = 0; strand < Strands; strand++)
        {
            _trails[strand] = new Vector2[Samples];
            _colors[strand] = new Color[Samples];
        }
        if (Texture is not null)
        {
            using var pixels = Texture.GetImage();
            pixels.Resize(12, 12, Image.Interpolation.Lanczos);
            Vector3 sum = Vector3.Zero; float weight = 0;
            for (int y = 0; y < 12; y++) for (int x = 0; x < 12; x++)
            {
                var pixel = pixels.GetPixel(x, y);
                sum += new Vector3(pixel.R, pixel.G, pixel.B) * pixel.A; weight += pixel.A;
            }
            if (weight > .001f) { sum /= weight; _pigment = new Color(sum.X, sum.Y, sum.Z); }
        }
        SetProgress(0);
    }

    private Vector2 Point(float t)
    {
        t = Mathf.Clamp(t, 0, 1);
        float travel = t * t * t * (t * (t * 6 - 15) + 10);
        float side = From.X < Mouth.X ? -1 : 1;
        var lift = From + new Vector2(-side * 16, -LiftHeight - Seed % 3 * 9);
        var entry = Mouth + new Vector2(side * (EntryReach + Seed % 2 * 12), -30);
        var point = From.BezierInterpolate(lift, entry, Mouth, travel);
        float curl = Mathf.SmoothStep(.30f, .64f, t) * (1 - Mathf.SmoothStep(.78f, 1, t));
        float angle = travel * Mathf.Tau * .8f + Seed * .35f;
        return point + new Vector2(Mathf.Sin(angle) * 12, Mathf.Cos(angle) * 6) * curl * CurlStrength;
    }

    public void SetProgress(float progress)
    {
        _progress = Mathf.Clamp(progress, 0, 1);
        var center = Point(_progress);
        Position = center - Size * .5f;
        var direction = (Point(_progress + .012f) - Point(_progress - .012f)).Normalized();
        float turn = Mathf.Sin(_progress * Mathf.Pi) * (.09f + (Seed % 2 == 0 ? -.04f : .04f));
        float scale = Mathf.Lerp(1, FinalScale, Mathf.SmoothStep(.35f, .94f, _progress));
        float pull = Mathf.SmoothStep(.42f, .86f, _progress) * .17f;
        _image.Scale = new Vector2(1 + pull * Mathf.Abs(direction.X), 1 - pull * .40f) * scale;
        _image.Rotation = turn;
        _image.Modulate = new Color(1, 1, 1, 1 - Mathf.SmoothStep(.92f, 1, _progress));
        _wash.SetShaderParameter("dissolve", Mathf.SmoothStep(.43f, .97f, _progress));
        _wash.SetShaderParameter("pull_direction", direction.Rotated(-turn));
        QueueRedraw();
    }

    public override void _Draw()
    {
        float ink = Mathf.SmoothStep(.28f, .64f, _progress) * (1 - Mathf.SmoothStep(.82f, 1, _progress)) * TrailOpacity;
        if (ink <= .001f) return;
        for (int strand = 0; strand < Strands; strand++)
        {
            for (int j = 0; j < Samples; j++)
            {
                float t = j / (Samples - 1f);
                float along = Mathf.Max(0, _progress - (1 - t) * (.14f + strand * .025f));
                var at = Point(along);
                var direction = (Point(along + .01f) - Point(along - .01f)).Normalized();
                var across = new Vector2(-direction.Y, direction.X);
                float sway = Mathf.Sin(t * 6 + _progress * 7 + Seed + strand) * (3 + strand * 2) * Mathf.Sin(t * Mathf.Pi);
                _trails[strand][j] = at + across * sway - Position;
                _colors[strand][j] = new Color(_pigment,
                    Mathf.Max(0, Mathf.Sin(t * Mathf.Pi)) * ink * (.14f - strand * .025f));
            }
            DrawPolylineColors(_trails[strand], _colors[strand], 3.2f - strand * .8f, true);
        }
        // A few pigment flecks continue into the opening after the solid contour loosens.
        for (int fleck = 0; fleck < 5; fleck++)
        {
            float t = Mathf.SmoothStep(.48f + fleck * .025f, .99f, _progress);
            var from = Point(.52f + fleck * .025f);
            var bend = new Vector2((fleck % 2 == 0 ? -1 : 1) * (12 + fleck * 2), -18);
            var at = from.BezierInterpolate(from + bend, Mouth + bend * .35f, Mouth, t);
            float alpha = Mathf.Max(0, Mathf.Sin(t * Mathf.Pi)) * ink * .40f;
            DrawCircle(at - Position, (1.6f + fleck % 3 * .4f) * (1 - t * .65f), new Color(_pigment, alpha));
        }
    }
}
