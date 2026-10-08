using Godot;

public partial class MainHUD : Control
{
    private Button? _taskTrackerToggle;
    private bool _taskTrackerExpanded = true;
    private bool _taskTrackerResizeSyncConnected;
    private Tween? _taskTrackerTween;

    public void set_task_tracker_horizontal_layout()
    {
        CallDeferred(nameof(apply_task_tracker_horizontal_layout));
    }

    private void apply_task_tracker_horizontal_layout()
    {
        if (_taskTracker is null || _taskList is null) return;
        if (!_taskTrackerResizeSyncConnected)
        {
            _taskTracker.Resized += sync_task_tracker_toggle_vertical_bounds;
            Resized += queue_task_tracker_toggle_vertical_sync;
            _taskTrackerResizeSyncConnected = true;
        }
        _taskTracker.AnchorTop = .25f;
        _taskTracker.AnchorBottom = .25f;
        _taskTracker.OffsetTop = 0;
        _taskTracker.OffsetBottom = 138;
        _taskTracker.OffsetLeft = _taskTrackerExpanded ? -368 : 2;
        _taskTracker.OffsetRight = _taskTrackerExpanded ? -40 : 342;
        _taskTracker.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(.015f, .02f, .016f, .74f),
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 0,
            CornerRadiusBottomLeft = 3,
            CornerRadiusBottomRight = 0
        });

        if (_taskTracker.FindChild("Margin", true, false) is MarginContainer margin)
        {
            margin.AddThemeConstantOverride("margin_left", 16);
            margin.AddThemeConstantOverride("margin_right", 8);
            margin.AddThemeConstantOverride("margin_top", 16);
            margin.AddThemeConstantOverride("margin_bottom", 16);
        }

        foreach (var node in _taskTracker.FindChildren("*", "Label", true, false))
            if (node is Label label && label.Text == "阶段目标 · 当前任务") label.Hide();

        foreach (var child in _taskList.GetChildren())
        {
            if (child is not Button button) continue;
            var lines = button.Text.Split('\n');
            var summary = lines.Length > 1 ? lines[1] : "";
            if (summary.Length > 21) summary = summary[..20] + "…";
            button.Text = lines[0] + "\n" + summary;
            button.CustomMinimumSize = new Vector2(0, 50);
            button.AddThemeFontSizeOverride("font_size", 13);
            button.AddThemeColorOverride("font_color", new Color("F4F0DF"));
            button.AddThemeStyleboxOverride("normal", new StyleBoxFlat
            {
                BgColor = new Color(0, 0, 0, 0),
                ContentMarginLeft = 8,
                ContentMarginRight = 8,
                ContentMarginTop = 3,
                ContentMarginBottom = 3
            });
            button.AddThemeStyleboxOverride("hover", new StyleBoxFlat
            {
                BgColor = new Color(1, 1, 1, .08f),
                ContentMarginLeft = 8,
                ContentMarginRight = 8,
                ContentMarginTop = 3,
                ContentMarginBottom = 3
            });
        }
        _taskList.AddThemeConstantOverride("separation", 5);

        if (_taskTrackerToggle is null)
        {
            _taskTrackerToggle = new Button
            {
                Name = "TaskTrackerToggle",
                FocusMode = Control.FocusModeEnum.None,
                ZIndex = 11,
                MouseFilter = Control.MouseFilterEnum.Stop,
                MouseDefaultCursorShape = Control.CursorShape.PointingHand,
                Alignment = HorizontalAlignment.Center,
                CustomMinimumSize = new Vector2(40, 0)
            };
            _taskTrackerToggle.SetAnchorsPreset(LayoutPreset.TopRight);
            _taskTrackerToggle.AnchorTop = .25f;
            _taskTrackerToggle.AnchorBottom = .25f;
            _taskTrackerToggle.OffsetLeft = -40;
            _taskTrackerToggle.OffsetTop = 0;
            _taskTrackerToggle.OffsetRight = 0;
            _taskTrackerToggle.OffsetBottom = 0;
            _taskTrackerToggle.AddThemeFontSizeOverride("font_size", 14);
            _taskTrackerToggle.AddThemeColorOverride("font_color", new Color("D8D0B8"));
            _taskTrackerToggle.AddThemeColorOverride("font_hover_color", new Color("F1D77A"));
            _taskTrackerToggle.AddThemeStyleboxOverride("normal", new StyleBoxFlat
            {
                BgColor = new Color(.015f, .02f, .016f, .82f),
                BorderColor = new Color(.55f, .5f, .39f, .55f),
                BorderWidthLeft = 1,
                BorderWidthTop = 1,
                BorderWidthBottom = 1,
                CornerRadiusTopLeft = 0,
                CornerRadiusBottomLeft = 0
            });
            _taskTrackerToggle.AddThemeStyleboxOverride("hover", new StyleBoxFlat
            {
                BgColor = new Color(.08f, .085f, .07f, .94f),
                BorderColor = new Color(.78f, .66f, .45f, .84f),
                BorderWidthLeft = 1,
                BorderWidthTop = 1,
                BorderWidthBottom = 1,
                CornerRadiusTopLeft = 0,
                CornerRadiusBottomLeft = 0
            });
            _taskTrackerToggle.Pressed += toggle_task_tracker;
            AddChild(_taskTrackerToggle);
        }
        CallDeferred(nameof(sync_task_tracker_toggle_vertical_bounds));
        _taskTrackerToggle.ZIndex = 11;
        _taskTrackerToggle.MouseFilter = Control.MouseFilterEnum.Stop;
        _taskTrackerToggle.OffsetLeft = -40;
        _taskTrackerToggle.OffsetRight = 0;
        _taskTrackerToggle.Visible = _taskTracker.Visible;
        update_task_tracker_toggle();
    }

    private void sync_task_tracker_toggle_vertical_bounds()
    {
        if (_taskTracker is null || _taskTrackerToggle is null) return;
        var panelRect = _taskTracker.GetRect();
        _taskTrackerToggle.AnchorTop = 0;
        _taskTrackerToggle.AnchorBottom = 0;
        _taskTrackerToggle.OffsetTop = panelRect.Position.Y;
        _taskTrackerToggle.OffsetBottom = panelRect.End.Y;
    }

    private void queue_task_tracker_toggle_vertical_sync()
    {
        CallDeferred(nameof(sync_task_tracker_toggle_vertical_bounds));
    }

    private void toggle_task_tracker()
    {
        if (_taskTracker is null) return;
        _taskTrackerExpanded = !_taskTrackerExpanded;
        _taskTrackerTween?.Kill();
        _taskTrackerTween = CreateTween();
        _taskTrackerTween.SetTrans(Tween.TransitionType.Cubic);
        _taskTrackerTween.SetEase(Tween.EaseType.InOut);
        _taskTrackerTween.TweenProperty(_taskTracker, "offset_left", _taskTrackerExpanded ? -368 : 2, .2);
        _taskTrackerTween.Parallel().TweenProperty(_taskTracker, "offset_right", _taskTrackerExpanded ? -40 : 342, .2);
        update_task_tracker_toggle();
    }

    private void update_task_tracker_toggle()
    {
        if (_taskTrackerToggle is null) return;
        _taskTrackerToggle.Text = _taskTrackerExpanded ? "▶" : "◀";
        _taskTrackerToggle.TooltipText = _taskTrackerExpanded ? "向右收起任务栏" : "展开任务栏";
    }
}
