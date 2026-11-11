using Godot;

public partial class BattleManager
{
    private Vector2 _layoutViewportSize;
    private TextureRect? _legacyBattleBackground;
    private BattleBoardStoneStage? _boardStoneStage;
    // Explicit art-trial flag. Ordinary encounters retain their existing layout.
    public bool distant_avatar_preview;
    public bool is_distant_avatar_preview() => isolated_trial_battle && distant_avatar_preview;
    public bool uses_compact_battle_presentation() => is_board_battle() || is_distant_avatar_preview();
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
        _legacyBattleBackground=background;
        background.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _boardStoneStage=new BattleBoardStoneStage{Name="BoardStoneStage",Visible=false};layer.AddChild(_boardStoneStage);
        _boardStoneStage.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
    }

    private void RefreshBoardStage()
    {
        if(_legacyBattleBackground is not null)_legacyBattleBackground.Visible=!is_board_battle();
        if(_boardStoneStage is not null)_boardStoneStage.Visible=is_board_battle();
        if(is_board_battle())SetMeta("combat_presentation_profile",BattleBoardStoneStage.ProfileId);
        else RemoveMeta("combat_presentation_profile");
        FitBattleFormation(true);
    }
    private void ResetBoardStage()
    {
        if(GodotObject.IsInstanceValid(_legacyBattleBackground))_legacyBattleBackground!.Show();
        if(GodotObject.IsInstanceValid(_boardStoneStage))_boardStoneStage!.Hide();
        if(HasMeta("combat_presentation_profile"))RemoveMeta("combat_presentation_profile");
    }

    private bool UsesBasicPairFormation() => !uses_compact_battle_presentation() && all_units.OfType<Node>()
        .Any(unit=>unit.GetNodeOrNull<BattlePaperDoll>("BattlePaperDoll")?.uses_basic_pair_avatar()==true);
    public float get_unit_display_height() => (uses_compact_battle_presentation() ? 160f
        : (UsesBasicPairFormation()?new[] {288f,260f,237f,210f,190f}:new[] {320f,280f,240f,210f,190f})
            [Mathf.Clamp(Math.Max(player_units.Count, enemy_units.Count),1,5)-1]) * BattleUiArt.Scale(GetViewport());
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
        if (UsesBasicPairFormation()) { FitBasicPairFormation(); return; }
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
        var rowDepth = uses_compact_battle_presentation() ? 96f : Mathf.Clamp(360f - height, 0f, 170f);
        var fit = 1f;
        var centerY = uses_compact_battle_presentation() ? 500f : Math.Max(player_units.Count, enemy_units.Count) == 1 ? 495f : 540f;
        foreach (var team in new[] { player_units, enemy_units })
        {
            if (team.Count == 0) continue;
            var positions = uses_compact_battle_presentation() && team.Count == 3
                ? new Vector2[] { new(-160, -.55f), new(165, 0), new(-20, .65f) }
                : layouts[Mathf.Clamp(team.Count, 1, 5) - 1];
            var center = new Vector2(BattleUiArt.Canvas.X * (team == enemy_units ? (uses_compact_battle_presentation()?.69f:.75f) : (uses_compact_battle_presentation()?.31f:.25f)), centerY);
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
                var center = origin + new Vector2(BattleUiArt.Canvas.X * (team == enemy_units ? (uses_compact_battle_presentation()?.69f:.75f) : (uses_compact_battle_presentation()?.31f:.25f)), centerY) * scale;
                foreach (var unit in team.OfType<Node2D>())
                {
                    var screen = viewport.GetCanvasTransform() * unit.GlobalPosition;
                    unit.GlobalPosition = canvasInverse * (center + (screen - center) * fit);
                    var doll = unit.GetNodeOrNull<BattlePaperDoll>("BattlePaperDoll");
                    if (doll is not null) doll.Scale *= fit;
                }
            }
    }

    private void FitBasicPairFormation()
    {
        // The native basic figures keep their full feet above the existing skill HUD.
        // These screen anchors affect presentation only, never cells or target rules.
        Vector2[][] anchors =
        [
            [new(430,550)],
            [new(285,510),new(510,570)],
            [new(550,525),new(245,485),new(385,570)],
            [new(240,475),new(485,475),new(345,565),new(590,565)],
            [new(180,470),new(390,470),new(600,470),new(285,565),new(510,565)]
        ];
        var viewport=GetViewport(); var scale=BattleUiArt.Scale(viewport); var origin=BattleUiArt.Origin(viewport);
        var inverse=viewport.GetCanvasTransform().AffineInverse();
        foreach(var team in new[]{player_units,enemy_units})
        {
            var positions=anchors[Mathf.Clamp(team.Count,1,5)-1];
            for(var i=0;i<team.Count;i++)
            {
                if(team[i] is not Node2D unit) continue;
                var point=positions[Math.Min(i,positions.Length-1)];
                if(team==enemy_units) point.X=1440-point.X;
                var canvasPoint=point*new Vector2(BattleUiArt.Canvas.X/1440f,BattleUiArt.Canvas.Y/810f);
                unit.GlobalPosition=inverse*(origin+canvasPoint*scale);
                unit.ZIndex=Mathf.RoundToInt(canvasPoint.Y);
                unit.GetNodeOrNull<BattlePaperDoll>("BattlePaperDoll")?.SetDisplayHeight(get_unit_display_height()/viewport.GetCanvasTransform().Scale.Y);
            }
        }
    }
}
