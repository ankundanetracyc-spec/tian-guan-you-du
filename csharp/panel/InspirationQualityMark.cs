using Godot;

/// <summary>Static, low-saturation ink-mist orb for an inspiration use domain and quality rank.</summary>
public partial class InspirationQualityMark : Control
{
    private static readonly Color Rule = new("a39372");
    private static readonly Color Paper = new("f1e8d5");
    private static readonly Color Ink = new("303b38");
    private static readonly Color Accent = new("8c7657");

    private int _qualityRank;
    private string _domainId = "";

    public InspirationQualityMark()
    {
        CustomMinimumSize = new Vector2(60, 60);
        SizeFlagsVertical = SizeFlags.ShrinkCenter;
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public InspirationQualityMark(int qualityRank) : this()
    {
        SetQualityRank(qualityRank);
    }

    public InspirationQualityMark(int qualityRank, string domainId) : this(qualityRank)
    {
        SetDomainId(domainId);
    }

    public void SetQualityRank(int qualityRank)
    {
        _qualityRank = qualityRank is >= 1 and <= 3 ? qualityRank : 0;
        TooltipText = _qualityRank switch
        {
            1 => "见几",
            2 => "明辨",
            3 => "通变",
            _ => "未定品级"
        };
        QueueRedraw();
    }

    public void SetDomainId(string domainId)
    {
        _domainId = domainId ?? "";
        QueueRedraw();
    }

    public override void _Draw()
    {
        var side = Mathf.Min(Size.X, Size.Y);
        if (side <= 0f)
            return;

        var scale = side / 60f;
        var center = Size * 0.5f;
        var pigment = domain_color(_domainId);
        var softInk = mix(pigment, Paper, .48f);
        var core = mix(pigment, Paper, .25f);

        // Layered, translucent washes create a still watercolor cloud instead of a hard jewel icon.
        DrawCircle(center, 16.2f * scale, with_alpha(Paper, .78f));
        draw_wash(center, scale, softInk);
        DrawCircle(point(center, scale, -3.5f, -1.0f), 10.8f * scale, with_alpha(pigment, .20f));
        DrawCircle(point(center, scale, 4.8f, 2.4f), 9.5f * scale, with_alpha(softInk, .24f));
        DrawCircle(point(center, scale, .5f, -1.0f), 7.5f * scale, with_alpha(core, .46f));
        DrawCircle(point(center, scale, -2.8f, -4.1f), 3.2f * scale, with_alpha(Paper, .62f));

        // The halo count mirrors the text rank shown next to the orb in the UI.
        var haloColor = with_alpha(mix(pigment, Accent, .40f), .44f);
        for (var i = 0; i < Mathf.Clamp(_qualityRank, 0, 3); i++)
        {
            var radius = (20.5f + i * 3.5f) * scale;
            DrawArc(center, radius, 0f, Mathf.Tau, 48, haloColor, 1.05f * scale, true);
        }

        DrawCircle(center, 16.2f * scale, with_alpha(Rule, .52f), false, 1.0f * scale, true);
    }

    private void draw_wash(Vector2 center, float scale, Color color)
    {
        // Keep every form abstract. Each use direction only nudges the soft cloud's
        // balance and silhouette; none of the marks depict an object or a scene.
        var (variants, radii) = _domainId switch
        {
            CultivationInspirationTuning.CombatDomain => (
                new[] { new Vector2(-8f, 3f), new Vector2(-4f, -5f), new Vector2(2f, -7f), new Vector2(8f, -2f), new Vector2(5f, 6f), new Vector2(-2f, 8f) },
                new[] { 7.8f, 7.2f, 7.0f, 7.6f, 8.0f, 6.8f }),
            CultivationInspirationTuning.GatheringDomain => (
                new[] { new Vector2(-10f, 1f), new Vector2(-6f, -6f), new Vector2(0f, -7f), new Vector2(8f, -4f), new Vector2(10f, 3f), new Vector2(3f, 8f), new Vector2(-5f, 7f) },
                new[] { 6.6f, 7.1f, 7.8f, 7.0f, 6.7f, 7.4f, 7.6f }),
            CultivationInspirationTuning.TravelDomain => (
                new[] { new Vector2(-7f, -7f), new Vector2(1f, -8f), new Vector2(7f, -3f), new Vector2(6f, 5f), new Vector2(-1f, 8f), new Vector2(-8f, 3f) },
                new[] { 6.8f, 7.4f, 7.8f, 7.1f, 7.6f, 6.8f }),
            CultivationInspirationTuning.StudyDomain => (
                new[] { new Vector2(-7f, 1f), new Vector2(-4f, -6f), new Vector2(3f, -7f), new Vector2(8f, -1f), new Vector2(4f, 6f), new Vector2(-3f, 7f) },
                new[] { 7.1f, 7.7f, 7.0f, 7.5f, 7.3f, 7.0f }),
            CultivationInspirationTuning.SectDutyDomain => (
                new[] { new Vector2(-7f, -6f), new Vector2(0f, -8f), new Vector2(7f, -6f), new Vector2(-9f, 1f), new Vector2(0f, 2f), new Vector2(9f, 1f), new Vector2(-5f, 8f), new Vector2(5f, 8f) },
                new[] { 6.7f, 7.4f, 6.7f, 6.8f, 7.8f, 6.8f, 6.9f, 6.9f }),
            _ => (
                new[] { new Vector2(-9f, -1f), new Vector2(-5f, 7f), new Vector2(3f, 8f), new Vector2(9f, 2f), new Vector2(6f, -6f), new Vector2(-2f, -9f) },
                new[] { 7.2f, 8.2f, 7.7f, 6.8f, 7.8f, 6.8f })
        };
        for (var i = 0; i < variants.Length; i++)
            DrawCircle(point(center, scale, variants[i].X, variants[i].Y), radii[i] * scale, with_alpha(color, .31f));
    }

    private static Color domain_color(string domainId) => domainId switch
    {
        CultivationInspirationTuning.CombatDomain => new Color("a97970"),
        CultivationInspirationTuning.GatheringDomain => new Color("7e9885"),
        CultivationInspirationTuning.TravelDomain => new Color("8097a0"),
        CultivationInspirationTuning.StudyDomain => new Color("85869b"),
        CultivationInspirationTuning.SectDutyDomain => new Color("b1996b"),
        _ => new Color("87928b")
    };

    private static Color mix(Color first, Color second, float amount)
        => first.Lerp(second, Mathf.Clamp(amount, 0f, 1f));

    private static Color with_alpha(Color color, float alpha)
        => new(color.R, color.G, color.B, alpha);

    private static Vector2 point(Vector2 center, float scale, float x, float y)
        => center + new Vector2(x, y) * scale;
}
