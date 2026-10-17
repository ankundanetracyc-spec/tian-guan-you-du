using Godot;

/// <summary>静室中的灵感清册与瓶颈炼化入口。</summary>
public partial class CultivationUI
{
    private PanelContainer? _inspirationPage;
    private VBoxContainer? _inspirationList;
    private Label? _inspirationBottleSummary, _inspirationBottlePreview, _inspirationResult;
    private Button? _inspirationRefine;
    private string _selectedInspirationId = "";

    private void build_room_inspiration()
    {
        _inspirationPage = room_page("InspirationPage");
        var root = room_box(_inspirationPage);
        root.AddThemeConstantOverride("separation", 14);

        var headingPaper = new PanelContainer(); headingPaper.AddThemeStyleboxOverride("panel", QuietRoomStyle.Paper(1, 14)); root.AddChild(headingPaper);
        var heading = new VBoxContainer();
        heading.AddThemeConstantOverride("separation", 4);
        headingPaper.AddChild(heading);
        heading.AddChild(QuietRoomStyle.Text("灵感与瓶颈", 28));
        var intro = QuietRoomStyle.Text("从经历中获得的灵感会记录在此。选中一份，可炼化当前瓶颈，或预留给用途相符的行动；两种用法互斥。", 17);
        intro.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        heading.AddChild(intro);

        var columns = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        columns.AddThemeConstantOverride("separation", 20);
        root.AddChild(columns);

        var bottleneckCard = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsStretchRatio = 0.9f };
        bottleneckCard.AddThemeStyleboxOverride("panel", QuietRoomStyle.Paper(.95f, 24));
        columns.AddChild(bottleneckCard);
        var bottleBox = room_box(bottleneckCard);
        bottleBox.AddChild(QuietRoomStyle.Text("当前瓶颈", 25));
        _inspirationBottleSummary = QuietRoomStyle.Text("", 20);
        _inspirationBottleSummary.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        bottleBox.AddChild(_inspirationBottleSummary);
        var divider = new HSeparator();
        bottleBox.AddChild(divider);
        _inspirationBottlePreview = QuietRoomStyle.Text("选择一份灵感，查看炼化后的减速。", 18);
        _inspirationBottlePreview.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        bottleBox.AddChild(_inspirationBottlePreview);
        _inspirationResult = QuietRoomStyle.Text("", 17);
        _inspirationResult.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        bottleBox.AddChild(_inspirationResult);
        bottleBox.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore });
        _inspirationRefine = new Button { Text = "炼化选中灵感", CustomMinimumSize = new Vector2(0, 58) };
        QuietRoomStyle.Primary(_inspirationRefine);
        _inspirationRefine.Pressed += refine_selected_inspiration;
        bottleBox.AddChild(_inspirationRefine);

        var recordsCard = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsStretchRatio = 1.1f };
        recordsCard.AddThemeStyleboxOverride("panel", QuietRoomStyle.Paper(.95f, 22));
        columns.AddChild(recordsCard);
        var recordsBox = room_box(recordsCard);
        recordsBox.AddChild(QuietRoomStyle.Text("可用灵感", 25));
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        recordsBox.AddChild(scroll);
        _inspirationList = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _inspirationList.AddThemeConstantOverride("separation", 12);
        scroll.AddChild(_inspirationList);
    }

    private void refresh_room_inspiration()
    {
        if (_inspirationPage is null || _inspirationList is null || _inspirationBottleSummary is null ||
            _inspirationBottlePreview is null || _inspirationRefine is null || _target is not Character character)
            return;

        foreach (var child in _inspirationList.GetChildren())
        {
            _inspirationList.RemoveChild(child);
            child.QueueFree();
        }

        var available = character.get_cultivation_inspiration_records();
        refresh_room_inspiration_access();
        if (!available.Any(record => record.record_id == _selectedInspirationId))
            _selectedInspirationId = available.FirstOrDefault()?.record_id ?? "";

        foreach (var record in available)
        {
            var source = string.IsNullOrWhiteSpace(record.source_label) ? record.source_id : record.source_label;
            var recordBox = new VBoxContainer();
            recordBox.AddThemeConstantOverride("separation", 5);
            _inspirationList.AddChild(recordBox);
            var button = new Button
            {
                Text = $"灵感·{record.get_quality_name()}\n品级：{record.get_quality_name()}　·　来源：{source}\n用途方向：{record.use_domain_id}\n有效至：{format_inspiration_expiry(record.expires_at_game_hour)}",
                ToggleMode = true,
                ButtonPressed = record.record_id == _selectedInspirationId,
                Alignment = HorizontalAlignment.Left,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(0, 112),
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            button.AddThemeFontSizeOverride("font_size", 17);
            button.Pressed += () =>
            {
                _selectedInspirationId = record.record_id;
                _inspirationResult!.Text = "";
                refresh_room_inspiration();
            };
            recordBox.AddChild(button);
            var actionLabel = CultivationInspirationTuning.action_domain_label(record.use_domain_id);
            var reserve = new Button
            {
                Text = record.armed_for_action ? $"已预留用于{actionLabel} · 点击取消" : $"预留用于{actionLabel}",
                CustomMinimumSize = new Vector2(0, 38),
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            reserve.Pressed += () =>
            {
                character.set_cultivation_inspiration_action_reservation(record.record_id, !record.armed_for_action);
                _inspirationResult!.Text = record.armed_for_action
                    ? $"已取消「{record.get_quality_name()}」的行动预留。"
                    : $"已预留「{record.get_quality_name()}」用于{actionLabel}；对应行动开始时才会消耗。";
                refresh_room_inspiration();
            };
            recordBox.AddChild(reserve);
        }

        if (available.Count == 0)
        {
            var empty = QuietRoomStyle.Text("当前没有可用灵感。", 18);
            empty.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _inspirationList.AddChild(empty);
        }

        var bottle = character.get_cultivation_bottleneck_summary();
        var applicable = bottle.GetValueOrDefault("applicable", false).AsBool();
        var level = bottle.GetValueOrDefault("level", 0).AsInt32();
        var penalty = bottle.GetValueOrDefault("penalty", 0d).AsDouble();
        var gainMultiplier = bottle.GetValueOrDefault("gain_multiplier", 1d).AsDouble();
        if (!applicable)
        {
            _inspirationBottleSummary.Text = "当前阶段没有可炼化的云息瓶颈。";
            _inspirationBottlePreview.Text = "归元九息不受此瓶颈影响。";
            _inspirationRefine.Disabled = true;
            return;
        }

        _inspirationBottleSummary.Text = level <= 0
            ? $"当前瓶颈阶数：0 / 8\n当前减速：{penalty:P1}\n当前修炼效率：{gainMultiplier:P1}"
            : $"瓶颈：{level} / 8 阶\n当前减速：{penalty:P1}\n当前修炼效率：{gainMultiplier:P1}";

        var selected = available.FirstOrDefault(record => record.record_id == _selectedInspirationId);
        if (level > 0 && penalty > 0d && selected is not null && !selected.armed_for_action)
        {
            var nextPenalty = CultivationBottleneckRules.relieve_penalty(penalty, level, selected.quality_rank);
            _inspirationBottlePreview.Text =
                $"以「{selected.get_quality_name()}」炼化一份\n减速：{penalty:P1} × {level} / ({level} + {selected.quality_rank})\n炼化后减速：{nextPenalty:P1}　·　修炼效率：{1d - nextPenalty:P1}";
            _inspirationRefine.Disabled = false;
        }
        else
        {
            _inspirationBottlePreview.Text = selected?.armed_for_action == true
                ? "此份灵感已预留用于行动；如要炼化，请先取消预留。"
                : level <= 0
                ? penalty > 0d ? "当前瓶颈阶数为 0，暂不可炼化。" : "瓶颈形成后，方可在此炼化灵感。"
                : "选择一份可用灵感，查看炼化后的减速。";
            _inspirationRefine.Disabled = true;
        }
    }

    private void refine_selected_inspiration()
    {
        if (_target is not Character character || string.IsNullOrWhiteSpace(_selectedInspirationId)) return;
        if (!character.try_refine_cultivation_bottleneck(_selectedInspirationId, out var result))
        {
            _inspirationResult!.Text = "未能炼化；请确认所选灵感仍可用，且当前已有瓶颈。";
            refresh_room_inspiration();
            return;
        }

        var oldPenalty = result.GetValueOrDefault("old_penalty", 0d).AsDouble();
        var newPenalty = result.GetValueOrDefault("new_penalty", 0d).AsDouble();
        _inspirationResult!.Text = $"已炼化一份灵感，瓶颈减速由 {oldPenalty:P1} 降至 {newPenalty:P1}。";
        _selectedInspirationId = "";
        refresh_room_inspiration();
    }

    private static string format_inspiration_expiry(long totalHours)
    {
        if (totalHours < 0) return "时间未记";
        var dayIndex = totalHours / 24;
        var year = dayIndex / 360 + 1;
        var dayOfYear = dayIndex % 360;
        var hour = totalHours % 24;
        return $"{year}年{dayOfYear / 30 + 1}月{dayOfYear % 30 + 1}日 {hour:00}时";
    }
}
