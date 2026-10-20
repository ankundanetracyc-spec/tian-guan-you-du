using Godot;

public partial class MainHUD : Control
{
    private Button? _taskTrackerToggle;
    private bool _taskTrackerExpanded = true, _taskTrackerResizeSyncConnected;
    private const int TrackerWidth = 336, ToggleWidth = 30, TrackerRight = 52;
    public void set_task_tracker_horizontal_layout() => CallDeferred(nameof(apply_task_tracker_horizontal_layout));
    private void apply_task_tracker_horizontal_layout()
    {
        if (_taskTracker is not PanelContainer panel || _taskList is null) return;
        if (!_taskTrackerResizeSyncConnected)
        {
            panel.Resized += sync_task_tracker_toggle_vertical_bounds;
            panel.MinimumSizeChanged += queue_task_tracker_height_sync;
            _taskTrackerResizeSyncConnected = true;
        }
        panel.AnchorTop = panel.AnchorBottom = .25f;
        panel.OffsetTop = 0;
        panel.OffsetLeft = _taskTrackerExpanded ? -TrackerRight - TrackerWidth : 8;
        panel.OffsetRight = _taskTrackerExpanded ? -TrackerRight : TrackerWidth + 8;
        panel.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        if (panel.GetNodeOrNull<MarginContainer>("Margin") is { } margin)
        {
            margin.AddThemeConstantOverride("margin_left", 16);
            margin.AddThemeConstantOverride("margin_right", 16);
            margin.AddThemeConstantOverride("margin_top", 14);
            margin.AddThemeConstantOverride("margin_bottom", 14);
        }
        _taskList.AddThemeConstantOverride("separation", 12);
        CallDeferred(nameof(resize_task_tracker_to_content));
        if (_taskTrackerToggle is null)
        {
            _taskTrackerToggle = new Button { Name = "TaskTrackerToggle", ZIndex = 11, FocusMode = FocusModeEnum.None };
            _taskTrackerToggle.SetAnchorsPreset(LayoutPreset.TopRight);
            WorldHudStyle.StyleSmallButton(_taskTrackerToggle, 14);
            foreach (var state in new[] { "normal", "hover", "pressed" })
                _taskTrackerToggle.AddThemeStyleboxOverride(state, new StyleBoxFlat
                {
                    BgColor = new Color(state == "normal" ? "eee9d9" : "e3e2d2"),
                    BorderColor = new Color("968772"),
                    BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1
                });
            _taskTrackerToggle.Pressed += () => { _taskTrackerExpanded = !_taskTrackerExpanded; apply_task_tracker_horizontal_layout(); };
            AddChild(_taskTrackerToggle);
        }
        _taskTrackerToggle.OffsetLeft = -14 - ToggleWidth; _taskTrackerToggle.OffsetRight = -14;
        _taskTrackerToggle.Text = _taskTrackerExpanded ? "▶" : "◀";
        _taskTrackerToggle.Visible = panel.Visible;
        CallDeferred(nameof(sync_task_tracker_toggle_vertical_bounds));
    }
    private void sync_task_tracker_toggle_vertical_bounds()
    {
        if (_taskTracker is null || _taskTrackerToggle is null) return;
        var rect = _taskTracker.GetRect();
        _taskTrackerToggle.AnchorTop = _taskTrackerToggle.AnchorBottom = 0;
        var centerY = rect.Position.Y + rect.Size.Y / 2;
        _taskTrackerToggle.OffsetTop = centerY - 22; _taskTrackerToggle.OffsetBottom = centerY + 22;
    }
    private void queue_task_tracker_height_sync() => CallDeferred(nameof(resize_task_tracker_to_content));
    private void resize_task_tracker_to_content()
    {
        if (_taskTracker is null) return;
        _taskTracker.OffsetBottom = Math.Max(1, _taskTracker.GetCombinedMinimumSize().Y);
        sync_task_tracker_toggle_vertical_bounds();
    }
}
