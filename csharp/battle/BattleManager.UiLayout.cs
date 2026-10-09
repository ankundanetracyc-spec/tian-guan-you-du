using Godot;

public partial class BattleManager
{
    private Vector2 _layoutViewportSize;
    private void ApplyBattleArtwork(Node scene)
    {
        scene.GetNodeOrNull<CanvasItem>("InkMountainValley")?.Hide();
        scene.GetNodeOrNull<CanvasItem>("UILayer/BattleHeader")?.Hide();
        scene.GetNodeOrNull<CanvasItem>("UILayer/Title")?.Hide();
        scene.GetNodeOrNull<CanvasItem>("UILayer/Hint")?.Hide();
        var layer = new CanvasLayer { Name = "BattleBackgroundArtwork", Layer = -20 };
        scene.AddChild(layer);
        var background = new TextureRect
        {
            Texture = BattleUiArt.Texture("res://textures/battle/ink_terrace_multiteam_20261009_v2.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        layer.AddChild(background);
        background.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
    }

    public float get_unit_display_height() => new[] { 320f, 280f, 240f, 210f, 190f }[Mathf.Clamp(Math.Max(player_units.Count, enemy_units.Count), 1, 5) - 1] * BattleUiArt.Scale(GetViewport());
    public Rect2 get_unit_screen_rect(Node2D unit)
    {
        var height = get_unit_display_height();
        var width = height * .72f;
        return new Rect2(unit.GetGlobalTransformWithCanvas().Origin - new Vector2(width * .5f, height), new Vector2(width, height));
    }

    private void FitBattleFormation(bool force = false)
    {
        var viewport = GetViewport();
        var size = viewport.GetVisibleRect().Size;
        if (!force && size == _layoutViewportSize || all_units.Count == 0) return;
        _layoutViewportSize = size;
        Vector2[][] layouts =
        [
            [new(520, 720)],
            [new(390, 610), new(650, 720)],
            [new(350, 565), new(690, 565), new(520, 720)],
            [new(330, 565), new(600, 565), new(440, 720), new(710, 720)],
            [new(300, 565), new(500, 565), new(700, 565), new(400, 720), new(620, 720)]
        ];
        var scale = BattleUiArt.Scale(viewport);
        var origin = BattleUiArt.Origin(viewport);
        var canvasInverse = viewport.GetCanvasTransform().AffineInverse();
        foreach (var team in new[] { player_units, enemy_units })
        {
            var positions = layouts[Mathf.Clamp(team.Count, 1, 5) - 1];
            for (var i = 0; i < team.Count; i++)
            {
                if (team[i] is not Node2D unit) continue;
                var anchor = positions[Math.Min(i, positions.Length - 1)];
                if (team == enemy_units) anchor.X = BattleUiArt.Canvas.X - anchor.X;
                unit.GlobalPosition = canvasInverse * (origin + anchor * scale);
                unit.ZIndex = Mathf.RoundToInt(anchor.Y);
                unit.GetNodeOrNull<BattlePaperDoll>("BattlePaperDoll")?.SetDisplayHeight(get_unit_display_height() / viewport.GetCanvasTransform().Scale.Y);
            }
        }
    }
}
