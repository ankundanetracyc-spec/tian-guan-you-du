using Godot;

public partial class MainHUD : Control
{
    private Button? _taskTrackerToggle;
    private bool _taskTrackerExpanded = true, _taskTrackerResizeSyncConnected;
    private (string Id, string Title, string Body)[] _taskTrackerPresentation = [];
    private bool _taskTrackerHeightSyncQueued;
    private const int TrackerWidth = 336, ToggleWidth = 30, TrackerRight = 52;
    public void set_task_tracker_horizontal_layout() => CallDeferred(nameof(apply_task_tracker_horizontal_layout));
    private void apply_task_tracker_horizontal_layout()
    {
        if (_taskTracker is not PanelContainer panel || _taskList is null) return;
        if (!_taskTrackerResizeSyncConnected)
        {
            panel.Resized += sync_task_tracker_toggle_vertical_bounds;
            panel.MinimumSizeChanged += queue_task_tracker_height_sync;
            Resized += () => CallDeferred(nameof(sync_task_tracker_toggle_vertical_bounds));
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
            WorldHudStyle.StylePaperButton(_taskTrackerToggle, 14);
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
        _taskTrackerToggle.SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft);
        _taskTrackerToggle.Position = new Vector2(Size.X - 14 - ToggleWidth, rect.Position.Y);
        _taskTrackerToggle.Size = new Vector2(ToggleWidth, rect.Size.Y);
    }
    private void queue_task_tracker_height_sync()
    {
        if (_taskTrackerHeightSyncQueued) return;
        _taskTrackerHeightSyncQueued = true;
        CallDeferred(nameof(resize_task_tracker_to_content));
    }
    private void resize_task_tracker_to_content()
    {
        _taskTrackerHeightSyncQueued = false;
        if (_taskTracker is null) return;
        var height = Math.Max(1, _taskTracker.GetCombinedMinimumSize().Y);
        // Text layout may temporarily clamp the actual size above the requested
        // offsets. When its minimum shrinks, unchanged offsets do not resize it.
        // Explicitly restore the actual size as well as the anchored bounds.
        _taskTracker.OffsetTop = 0;
        if (Math.Abs(_taskTracker.OffsetBottom - height) > .5f) _taskTracker.OffsetBottom = height;
        if (Math.Abs(_taskTracker.Size.Y - height) > .5f)
            _taskTracker.Size = new Vector2(_taskTracker.Size.X, height);
        sync_task_tracker_toggle_vertical_bounds();
    }
}
