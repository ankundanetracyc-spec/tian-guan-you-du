using Godot;
using System;
using System.Linq;

/// <summary>只揭示已经提交的制作结果，不调用制作、库存或历法。</summary>
public partial class TalismanCraftingPresentation : Control
{
    public string CraftMode { get; init; } = "paper";
    public TextureRect Artwork { get; init; } = null!;
    public int PhaseIndex { get; private set; }
    public string PhaseText => Captions[Math.Clamp(PhaseIndex, 0, 2)];
    public bool Playing => !_ended;
    public event Action? Completed;
    public event Action<string>? PhaseChanged;

    private const float Duration = 3f;
    private float _elapsed;
    private bool _ended;
    private Vector2 _artOrigin;
    private TextureRect? _brush;
    private ShaderMaterial? _reveal;
    private static Shader? _revealShader;
    private string[] Captions => CraftMode switch
    {
        "ink" => ["墨蕴渐聚", "墨色渐匀", "符墨渐成"],
        "drawing" => ["落笔起式", "行纹成符", "收锋封符"],
        _ => ["灵丝相合", "纸形渐成", "凝成符纸"]
    };

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Artwork.MouseFilter = MouseFilterEnum.Ignore;
        Artwork.Name = "PresentationArtwork";
        Artwork.Size = new Vector2(800, 650);
        _artOrigin = new Vector2(100, 0);
        Artwork.Position = _artOrigin;
        Artwork.PivotOffset = Artwork.Size * .5f;
        if (CraftMode == "drawing")
        {
            var faint = new TextureRect
            {
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                Name = "PresentationFaintArtwork",
                Texture = Artwork.Texture, Position = _artOrigin, Size = Artwork.Size,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = MouseFilterEnum.Ignore, Modulate = new Color(1, 1, 1, .13f)
            };
            AddChild(faint);
            _revealShader ??= new Shader { Code = """
                shader_type canvas_item;
                uniform float reveal : hint_range(0.0, 1.1) = 0.0;
                void fragment() {
                    COLOR.a *= 1.0 - smoothstep(reveal - 0.045, reveal + 0.045, UV.y);
                }
                """ };
            _reveal = new ShaderMaterial { Shader = _revealShader };
            Artwork.Material = _reveal;
            _brush = Artwork.GetChildren().OfType<TextureRect>().FirstOrDefault();
            if (_brush is not null)
            {
                // The brush keeps its own painted face while the talisman is revealed.
                _brush.ShowBehindParent = false;
                _brush.PivotOffset = _brush.Size * new Vector2(.5f, .82f);
            }
        }
        AddChild(Artwork);
        ApplyFrame(0);
        PhaseChanged?.Invoke(PhaseText);
    }

    public override void _Process(double delta)
    {
        if (_ended) return;
        _elapsed = Math.Min(Duration, _elapsed + (float)delta);
        float progress = _elapsed / Duration;
        int phase = Math.Min(2, (int)(progress * 3));
        if (phase != PhaseIndex)
        {
            PhaseIndex = phase;
            PhaseChanged?.Invoke(PhaseText);
        }
        ApplyFrame(progress);
        if (progress >= 1) Finish();
    }

    private void ApplyFrame(float progress)
    {
        float breath = Mathf.Sin(progress * Mathf.Pi);
        if (CraftMode == "paper")
        {
            Artwork.Position = _artOrigin + new Vector2(0, 17 * (1 - progress));
            Artwork.Scale = Vector2.One * (.92f + .08f * progress);
            Artwork.Rotation = -.025f * (1 - progress);
            Artwork.Modulate = new Color(1, 1, 1, .25f + .75f * Mathf.SmoothStep(0, 1, progress));
        }
        else if (CraftMode == "ink")
        {
            Artwork.Position = _artOrigin + new Vector2(Mathf.Sin(progress * Mathf.Tau) * 3, -breath * 8);
            Artwork.Scale = Vector2.One * (.96f + .04f * progress + breath * .012f);
            Artwork.Modulate = new Color(1, 1, 1, .4f + .6f * Mathf.SmoothStep(0, 1, progress));
        }
        else
        {
            _reveal?.SetShaderParameter("reveal", Mathf.Lerp(.05f, 1.08f, progress));
            if (_brush is not null)
            {
                float sweep = Mathf.Sin(progress * Mathf.Pi * 6) * 12 * breath;
                _brush.OffsetLeft = _brush.OffsetRight = sweep;
                _brush.OffsetTop = _brush.OffsetBottom = 36 * progress - 18;
                _brush.Rotation = -.035f * breath;
            }
        }
    }

    public void Skip() => Finish();

    private void Finish()
    {
        if (_ended) return;
        _ended = true;
        SetProcess(false);
        var complete = Completed;
        Completed = null; PhaseChanged = null;
        complete?.Invoke();
    }

    public void Stop()
    {
        if (_ended) return;
        _ended = true;
        SetProcess(false);
        Completed = null; PhaseChanged = null;
    }

    public override void _ExitTree() => Stop();
}
