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
        if (unit is ArtifactBody) return BattleLocalFocusView.FrameUnitRect(unit, new Rect2(unit.GetGlobalTransformWithCanvas().Origin + new Vector2(-65, -52), new Vector2(130, 115)));
        if (unit.GetNodeOrNull<BattlePaperDoll>("BattlePaperDoll")?.static_screen_rect() is Rect2 visible) return BattleLocalFocusView.FrameUnitRect(unit, visible);
        var height = get_unit_display_height();
        var width = height * .72f;
        return BattleLocalFocusView.FrameUnitRect(unit, new Rect2(unit.GetGlobalTransformWithCanvas().Origin - new Vector2(width * .5f, height), new Vector2(width, height)));
    }

    private void FitBattleFormation(bool force = false)
    {
        var viewport = GetViewport();
        var size = viewport.GetVisibleRect().Size;
        if (!force && size == _layoutViewportSize || all_units.Count == 0) return;
        _layoutViewportSize = size;
        // X 为相对半场中心的偏移，Y 为前后排系数；左右镜像，不向中线挤。
        Vector2[][] layouts =
        [
            [new(0, 0)],
            [new(-120, -.5f), new(120, .5f)],
            [new(-210, -.5f), new(210, -.5f), new(0, .5f)],
            [new(-220, -.5f), new(60, -.5f), new(-60, .5f), new(220, .5f)],
            [new(-230, -.5f), new(0, -.5f), new(230, -.5f), new(-115, .5f), new(115, .5f)]
        ];
        var scale = BattleUiArt.Scale(viewport);
        var origin = BattleUiArt.Origin(viewport);
        var canvasInverse = viewport.GetCanvasTransform().AffineInverse();
        var height = get_unit_display_height() / scale;
        var rowDepth = Mathf.Clamp(360f - height, 0f, 170f);
        var fit = 1f;
        var centerY = Math.Max(player_units.Count, enemy_units.Count) == 1 ? 495f : 540f;
        foreach (var team in new[] { player_units, enemy_units })
        {
            if (team.Count == 0) continue;
            var positions = layouts[Mathf.Clamp(team.Count, 1, 5) - 1];
            var center = new Vector2(BattleUiArt.Canvas.X * (team == enemy_units ? .75f : .25f), centerY);
            Rect2? teamBounds = null;
            for (var i = 0; i < team.Count; i++)
            {
                if (team[i] is not Node2D unit) continue;
                var offset = positions[Math.Min(i, positions.Length - 1)];
                if (team == enemy_units) offset.X = -offset.X;
                var anchor = center + new Vector2(offset.X, height * .5f + offset.Y * rowDepth);
                unit.GlobalPosition = canvasInverse * (origin + anchor * scale);
                unit.ZIndex = Mathf.RoundToInt(anchor.Y);
                unit.GetNodeOrNull<BattlePaperDoll>("BattlePaperDoll")?.SetDisplayHeight(get_unit_display_height() / viewport.GetCanvasTransform().Scale.Y);
                var rect = get_unit_screen_rect(unit);
                teamBounds = teamBounds?.Merge(rect) ?? rect;
            }
            if (teamBounds is not Rect2 bounds) continue;
            // 按可见人群外包框居中，包含宽袖、兽尾等，而非透明图片画布。
            var correction = origin + center * scale - bounds.GetCenter();
            var worldCorrection = canvasInverse * correction - canvasInverse * Vector2.Zero;
            foreach (var unit in team.OfType<Node2D>()) unit.GlobalPosition += worldCorrection;
            fit = Mathf.Min(fit, (BattleUiArt.Canvas.X * .5f - 80f) * scale / Mathf.Max(1f, bounds.Size.X));
        }
        // 极宽生灵超出半场时，双方整体统一收缩，保持多对少的相同比例。
        if (fit < 1f)
            foreach (var team in new[] { player_units, enemy_units })
            {
                var center = origin + new Vector2(BattleUiArt.Canvas.X * (team == enemy_units ? .75f : .25f), centerY) * scale;
                foreach (var unit in team.OfType<Node2D>())
                {
                    var screen = viewport.GetCanvasTransform() * unit.GlobalPosition;
                    unit.GlobalPosition = canvasInverse * (center + (screen - center) * fit);
                    var doll = unit.GetNodeOrNull<BattlePaperDoll>("BattlePaperDoll");
                    if (doll is not null) doll.Scale *= fit;
                }
            }
    }
}
