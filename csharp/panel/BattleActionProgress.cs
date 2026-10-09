using Godot;

/// <summary>战斗中上方的行动进度条：展示每名存活单位积蓄到本次行动所需的进度。</summary>
public partial class BattleActionProgress : Control
{
    private const float Threshold = 1000f;
    private Control? _markerLayer;
    private Label? _state;
    private ColorRect? _rail;
    private readonly Godot.Collections.Dictionary _markers = [];
    private readonly Dictionary<GodotObject,ColorRect> _rows = [];

    public override void _Ready()
    {
        _markerLayer = GetNodeOrNull<Control>("MarkerLayer");
        _state = GetNodeOrNull<Label>("State");
        _rail = GetNodeOrNull<ColorRect>("Rail");
        GetNodeOrNull<Panel>("Track")?.AddThemeStyleboxOverride("panel", BattleUiArt.Style("timeline_panel", new(680,78),80,16));
        GetNodeOrNull<Label>("Label")?.Hide();
        _state?.Hide();
        _rail?.Hide();
    }

    public override void _Process(double _)
    {
        var manager = GetTree().GetFirstNodeInGroup("battle_mgr") as BattleManager;
        if (manager is null || _markerLayer is null)
        {
            Hide();
            return;
        }
        Visible = manager.get_segment_phase() == BattleManager.SegmentPhase.Executing;
        if (!Visible) return;
        _state?.SetText("按角色速度推进");
        var activeCount=manager.all_units.Count(unit=>GodotObject.IsInstanceValid(unit)&&unit.Get("current_health").AsInt32()>0&&manager.battle_units.ContainsKey(unit)&&manager.get_battle_unit(unit).skill_queue.Count>0);
        Size=new Vector2(Size.X,Mathf.Max(60,activeCount*22+16));
        foreach (var markerValue in _markers.Values)
            if (markerValue.AsGodotObject() is CanvasItem oldMarker) oldMarker.Visible = false;

        foreach(var row in _rows.Values)row.Hide();
        var lane=0;
        foreach (var unit in manager.all_units)
        {
            if (!GodotObject.IsInstanceValid(unit) || unit.Get("current_health").AsInt32() <= 0) continue;
            if (!manager.battle_units.ContainsKey(unit) || manager.get_battle_unit(unit).skill_queue.Count == 0) continue;
            if (!_markers.TryGetValue(unit, out var value) || value.AsGodotObject() is not Panel marker)
            {
                var row=new ColorRect{Color=new Color(BattleUiArt.Accent,.2f),MouseFilter=MouseFilterEnum.Ignore};_markerLayer.AddChild(row);_rows[unit]=row;
                marker = CreateMarker(unit); _markerLayer.AddChild(marker); _markers[unit] = marker;
            }
            marker.Visible = true;
            var progress = Mathf.Clamp(manager.action_gauges.GetValueOrDefault(unit, 0f).AsSingle() / Threshold, 0f, 1f);
            var y = 7f+lane++*22;
            var rowTrack=_rows[unit];rowTrack.Show();rowTrack.Position=new(18,y+19);rowTrack.Size=new(Size.X-36,1);
            var startX = 18f;
            var endX = Mathf.Max(startX, Size.X - marker.Size.X - 16f);
            marker.Position = new Vector2(Mathf.Lerp(startX, endX, progress), y);
        }
    }

    private static Panel CreateMarker(GodotObject unit)
    {
        var player = unit.Get("faction").AsInt32() == (int)WorldEntity.Faction.PLAYER;
        var marker = new Panel { Size = new Vector2(106,20), MouseFilter = MouseFilterEnum.Pass, TooltipText = unit.Get("entity_name").AsString() };
        marker.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        var icon=BattleUiArt.Icon(player ? "icon_timeline_ally" : "icon_timeline_enemy",new(14,14));icon.Position=new(0,3);marker.AddChild(icon);
        var name=new Label{Text=unit.Get("entity_name").AsString(),Position=new(18,0),Size=new(88,20),VerticalAlignment=VerticalAlignment.Center,ClipText=true,MouseFilter=MouseFilterEnum.Ignore};name.AddThemeFontSizeOverride("font_size",12);name.AddThemeColorOverride("font_color",BattleUiArt.Text);marker.AddChild(name);
        return marker;
    }
}
