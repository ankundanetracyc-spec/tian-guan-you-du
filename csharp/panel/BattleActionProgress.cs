using Godot;

/// <summary>战斗中上方的行动进度条：展示每名存活单位积蓄到本次行动所需的进度。</summary>
public partial class BattleActionProgress : Control
{
    private const float Threshold = 1000f;
    private Control? _markerLayer;
    private Label? _state;
    private readonly Godot.Collections.Dictionary _markers = [];

    public override void _Ready()
    {
        _markerLayer = GetNodeOrNull<Control>("MarkerLayer");
        _state = GetNodeOrNull<Label>("State");
    }

    public override void _Process(double _)
    {
        var manager = GetTree().GetFirstNodeInGroup("battle_mgr") as BattleManager;
        if (manager is null || _markerLayer is null) return;
        var queues = manager.get_queue_snapshot();
        _state?.SetText(queues.Count == 0 ? "行动蓄势" : string.Join("    ", queues.Select(item =>
        {
            var unit = item["unit"].AsGodotObject();
            return $"{(GodotObject.IsInstanceValid(unit) ? unit!.Get("entity_name").AsString() : "未知")}：{item["text"].AsString()}";
        })));
        foreach (var unit in manager.all_units)
        {
            if (!GodotObject.IsInstanceValid(unit) || unit.Get("current_health").AsInt32() <= 0) continue;
            if (!_markers.TryGetValue(unit, out var value) || value.AsGodotObject() is not Panel marker)
            {
                marker = CreateMarker(unit); _markerLayer.AddChild(marker); _markers[unit] = marker;
            }
            var progress = Mathf.Clamp(manager.action_gauges.GetValueOrDefault(unit, 0f).AsSingle() / Threshold, 0f, 1f);
            marker.Position = new Vector2(Mathf.Lerp(10, Mathf.Max(10, Size.X - marker.Size.X - 10), progress), 20);
            var queue = queues.FirstOrDefault(item => item["unit"].AsGodotObject() == unit);
            marker.GetNodeOrNull<Label>("Queue")?.SetText(queue is null ? "" : queue["text"].AsString());
        }
    }

    private static Panel CreateMarker(GodotObject unit)
    {
        var player = unit.Get("faction").AsInt32() == (int)WorldEntity.Faction.PLAYER;
        var marker = new Panel { Size = new Vector2(88, 34), MouseFilter = player ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore, TooltipText = player ? "点击取消队列最前方、尚未结算的牌" : "敌方行动条" };
        marker.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = player ? new Color(.12f, .35f, .28f, .96f) : new Color(.42f, .16f, .14f, .96f),
            BorderColor = player ? new Color(.54f, .82f, .64f, 1) : new Color(.92f, .55f, .40f, 1),
            BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8
        });
        var text = new Label { Text = unit.Get("entity_name").AsString(), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
        text.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); text.AddThemeFontSizeOverride("font_size", 13); marker.AddChild(text);
        var queue = new Label { Name = "Queue", Position = new Vector2(-44, 38), Size = new Vector2(176, 26), HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
        queue.AddThemeFontSizeOverride("font_size", 12); queue.AddThemeColorOverride("font_color", new Color("f3e6bd")); marker.AddChild(queue);
        if (player) marker.GuiInput += input =>
        {
            if (input is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) return;
            if (marker.GetTree().GetFirstNodeInGroup("battle_mgr") is BattleManager manager) manager.cancel_queued_card(unit, 0);
            marker.AcceptEvent();
        };
        return marker;
    }
}
