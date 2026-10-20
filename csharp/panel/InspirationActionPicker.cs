using Godot;
using System.Linq;

/// <summary>Action-page selector for one existing inspiration use domain.</summary>
public partial class InspirationActionPicker : PanelContainer
{
    private Character? _character;
    private OptionButton? _recordPicker;
    private Label? _title, _effect, _feedback;
    private InspirationQualityMark? _orb;
    private readonly List<string> _recordIds = [];
    private string _domainId = "";
    private string _actionLabel = "行动";
    private bool _darkStyle;
    private bool _refreshing;
    private bool _built;

    public void Configure(Character? character, string domainId, string actionLabel, bool darkStyle = false)
    {
        if (_character is not null)
            _character.CultivationInspirationChanged -= on_inspiration_changed;
        _character = character;
        if (_character is not null)
            _character.CultivationInspirationChanged += on_inspiration_changed;
        _domainId = domainId ?? "";
        _actionLabel = string.IsNullOrWhiteSpace(actionLabel) ? "行动" : actionLabel;
        _darkStyle = darkStyle;
        if (_built)
        {
            apply_style();
            refresh();
        }
    }

    public override void _Ready()
    {
        build();
        apply_style();
        refresh();
    }

    public override void _ExitTree()
    {
        if (_character is not null)
            _character.CultivationInspirationChanged -= on_inspiration_changed;
    }

    private void build()
    {
        if (_built) return;
        _built = true;
        MouseFilter = MouseFilterEnum.Stop;
        CustomMinimumSize = new Vector2(420, 104);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 5);
        AddChild(box);
        _title = new Label { Text = "" };
        _title.AddThemeFontSizeOverride("font_size", 17);
        box.AddChild(_title);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        box.AddChild(row);
        _orb = new InspirationQualityMark(1, _domainId)
        {
            CustomMinimumSize = new Vector2(48, 48),
            Visible = false
        };
        row.AddChild(_orb);
        _recordPicker = new OptionButton
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 40)
        };
        _recordPicker.AddThemeFontSizeOverride("font_size", 15);
        _recordPicker.ItemSelected += on_record_selected;
        row.AddChild(_recordPicker);

        _effect = new Label { Text = "", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _effect.AddThemeFontSizeOverride("font_size", 15);
        box.AddChild(_effect);
        _feedback = new Label { Text = "", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _feedback.AddThemeFontSizeOverride("font_size", 14);
        box.AddChild(_feedback);
    }

    private void apply_style()
    {
        if (_darkStyle)
        {
            var panel = new StyleBoxFlat
            {
                BgColor = new Color("1b3031e8"),
                BorderColor = new Color("8f9d83"),
                BorderWidthLeft = 1,
                BorderWidthRight = 1,
                BorderWidthTop = 1,
                BorderWidthBottom = 1,
                ContentMarginLeft = 10,
                ContentMarginRight = 10,
                ContentMarginTop = 8,
                ContentMarginBottom = 8
            };
            AddThemeStyleboxOverride("panel", panel);
            _title?.AddThemeColorOverride("font_color", new Color("eee7d4"));
            _effect?.AddThemeColorOverride("font_color", new Color("d4ddcf"));
            _feedback?.AddThemeColorOverride("font_color", new Color("c5d1c1"));
        }
        else
        {
            AddThemeStyleboxOverride("panel", QuietRoomStyle.Paper(.96f, 12));
            _title?.AddThemeColorOverride("font_color", QuietRoomStyle.Ink);
            _effect?.AddThemeColorOverride("font_color", QuietRoomStyle.Ink);
            _feedback?.AddThemeColorOverride("font_color", new Color("655d4d"));
        }
    }

    private void refresh()
    {
        if (!_built || _recordPicker is null || _title is null || _effect is null || _feedback is null || _orb is null)
            return;

        _refreshing = true;
        try
        {
            var armed = _character?.get_armed_cultivation_inspiration_for_action(_domainId);
            var available = _character?.get_cultivation_inspiration_records()
                .Where(record => record.use_domain_id == _domainId)
                .OrderBy(record => record.expires_at_game_hour)
                .ToList() ?? [];
            var autoSettings = _character?.get_cultivation_auto_refinement_settings() ?? new Godot.Collections.Dictionary();
            var autoRecordId = autoSettings.GetValueOrDefault("enabled", false).AsBool()
                ? autoSettings.GetValueOrDefault("record_id", "").AsString()
                : "";

            _recordPicker.Clear();
            _recordIds.Clear();
            _recordPicker.AddItem("不使用灵感");
            _recordIds.Add("");
            var selectedIndex = armed is null ? 0 : -1;
            foreach (var record in available)
            {
                var source = CultivationInspirationTuning.source_display_label(record.source_id, record.source_label);
                var suffix = record.record_id == autoRecordId ? " · 已选入吐纳自动炼化" : "";
                _recordPicker.AddItem($"{record.get_quality_name()} · {source}{suffix}");
                _recordIds.Add(record.record_id);
                var index = _recordIds.Count - 1;
                if (record.record_id == autoRecordId)
                    _recordPicker.SetItemDisabled(index, true);
                if (record.record_id == armed?.record_id)
                    selectedIndex = index;
            }
            _recordPicker.Select(Math.Max(0, selectedIndex));
            _recordPicker.Disabled = _character is null || (available.Count == 0 && armed is null);
            _title.Text = $"下一次{_actionLabel}使用的灵感";

            if (_character is null)
            {
                _orb.Hide();
                _effect.Text = "当前角色数据不可用。";
                _feedback.Text = "";
                return;
            }

            if (armed is not null)
            {
                _orb.Show();
                _orb.SetQualityRank(armed.quality_rank);
                _orb.SetDomainId(armed.use_domain_id);
                _orb.TooltipText = $"{armed.get_quality_name()} · {_actionLabel}";
                _effect.Text = CultivationInspirationTuning.action_effect_text(_domainId, armed.quality_rank);
                _feedback.Text = $"当前已选择「{armed.get_quality_name()}」。行动真正开始时才消耗。";
            }
            else
            {
                _orb.Hide();
                _effect.Text = available.Count == 0
                    ? "暂无可用于此行动的灵感。"
                    : $"选择一份灵感，供下一次{_actionLabel}使用；也可保持不使用。";
                _feedback.Text = autoRecordId.Length > 0 && available.Any(record => record.record_id == autoRecordId)
                    ? "其中一份灵感已留给吐纳自动炼化。"
                    : "尚未选择灵感。";
            }
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void on_record_selected(long index)
    {
        if (_refreshing || _recordPicker is null || _character is null || index < 0 || index >= _recordIds.Count)
            return;

        var previous = _character.get_armed_cultivation_inspiration_for_action(_domainId);
        var recordId = _recordIds[(int)index];
        if (string.IsNullOrWhiteSpace(recordId))
        {
            if (previous is not null)
            {
                _character.set_cultivation_inspiration_action_reservation(previous.record_id, false);
                _feedback!.Text = $"已取消下一次{_actionLabel}的灵感选择。";
            }
            else
            {
                _feedback!.Text = "下一次行动不会使用灵感。";
            }
        }
        else
        {
            var selected = _character.get_cultivation_inspiration_records().FirstOrDefault(record => record.record_id == recordId);
            if (selected is null || !_character.set_cultivation_inspiration_action_reservation(recordId, true))
            {
                _feedback!.Text = "这份灵感当前不可用，请重新选择。";
            }
            else if (previous is not null && previous.record_id != recordId)
            {
                _feedback!.Text = $"已将下一次{_actionLabel}改为使用「{selected.get_quality_name()}」，原选择已解除。";
            }
            else
            {
                _feedback!.Text = $"已选择「{selected.get_quality_name()}」用于下一次{_actionLabel}。";
            }
        }
        refresh();
    }

    private void on_inspiration_changed(Character _)
        => refresh();
}
