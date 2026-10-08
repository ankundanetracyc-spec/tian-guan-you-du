using Godot;

/// <summary>选择天数并推进游戏时间的休眠面板。</summary>
public partial class SlumberPanel : Control
{
    private HSlider? _daysSlider;
    private Label? _daysLabel;
    private Button? _slumberButton;
    private bool _slumbering;

    public override void _Ready()
    {
        _daysSlider = GetNodeOrNull<HSlider>("HBoxContainer/DaysSlider");
        _daysLabel = GetNodeOrNull<Label>("HBoxContainer/DaysLabel");
        _slumberButton = GetNodeOrNull<Button>("HBoxContainer/SlumberBtn");
        if (_daysSlider is null || _slumberButton is null) return;

        _daysSlider.MinValue = 1;
        _daysSlider.MaxValue = 30;
        _daysSlider.Step = 1;
        _daysSlider.Value = 7;
        _daysSlider.ValueChanged += _ => update_label();
        _slumberButton.Pressed += on_slumber_pressed;
        update_label();
    }

    private void update_label()
    {
        if (_daysSlider is not null && _daysLabel is not null)
            _daysLabel.Text = $"推进 {(int)_daysSlider.Value} 天";
    }

    private void on_slumber_pressed()
    {
        var manager = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
        if (_slumbering || manager is null || _daysSlider is null || _slumberButton is null) return;

        var days = (int)_daysSlider.Value;
        _slumbering = true;
        _slumberButton.Disabled = true;
        var tween = CreateTween();
        tween.TweenProperty(this, "modulate:a", 0.0f, 0.5f);
        tween.TweenCallback(Callable.From(() => manager.slumber(days)));
        tween.TweenProperty(this, "modulate:a", 1.0f, 0.5f);
        tween.TweenCallback(Callable.From(on_slumber_done));
    }

    private void on_slumber_done()
    {
        _slumbering = false;
        if (_slumberButton is not null) _slumberButton.Disabled = false;
    }
}
