using Godot;
using System.Linq;

/// <summary>静室内以整页格子只读展示灵感用途、品级与有效期。</summary>
public partial class CultivationUI
{
    private PanelContainer? _inspirationPage;
    private HFlowContainer? _inspirationGrid;
    private Label? _inspirationSummary;
    private OptionButton? _manualRefinementInspiration;
    private InspirationQualityMark? _manualRefinementOrb;
    private Label? _manualRefinementPreview, _manualRefinementFeedback;
    private Button? _manualRefinementButton;
    private readonly List<string> _manualRefinementRecordIds = [];
    private string _manualRefinementSelectedId = "";
    private bool _refreshingManualRefinementUi;
    private readonly List<Button> _inspirationFilterButtons = [];
    private string _inspirationFilterDomain = "";

    private static readonly (string Id, string Label)[] InspirationFilters =
    [
        ("", "全部"),
        (CultivationInspirationTuning.CombatDomain, "战斗"),
        (CultivationInspirationTuning.GatheringDomain, "采集"),
        (CultivationInspirationTuning.TravelDomain, "旅途"),
        (CultivationInspirationTuning.StudyDomain, "研读"),
        (CultivationInspirationTuning.SectDutyDomain, "宗门公事")
    ];

    private void build_room_inspiration()
    {
        _inspirationPage = room_page("InspirationPage");
        var root = room_box(_inspirationPage);
        root.AddThemeConstantOverride("separation", 9);

        var headingPaper = new PanelContainer();
        headingPaper.AddThemeStyleboxOverride("panel", QuietRoomStyle.Paper(1, 16));
        root.AddChild(headingPaper);
        var heading = new VBoxContainer();
        heading.AddThemeConstantOverride("separation", 4);
        headingPaper.AddChild(heading);
        heading.AddChild(QuietRoomStyle.Text("灵感清册", 29));
        var intro = QuietRoomStyle.Text("按灵感的方向用途与品级收存。停留在光团上，可查看实际效果与经历来源。", 16);
        intro.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        heading.AddChild(intro);

        var summaryRow = new HBoxContainer { CustomMinimumSize = new Vector2(0, 30) };
        root.AddChild(summaryRow);
        _inspirationSummary = QuietRoomStyle.Text("", 17);
        _inspirationSummary.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        summaryRow.AddChild(_inspirationSummary);
        var readonlyNote = QuietRoomStyle.Text("只作查阅 · 使用入口位于对应页面", 15);
        readonlyNote.HorizontalAlignment = HorizontalAlignment.Right;
        summaryRow.AddChild(readonlyNote);

        var filterRow = new HBoxContainer();
        filterRow.AddThemeConstantOverride("separation", 8);
        root.AddChild(filterRow);
        foreach (var (domainId, label) in InspirationFilters)
        {
            var filter = new Button
            {
                Text = label,
                ToggleMode = true,
                CustomMinimumSize = new Vector2(112, 42),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                ButtonPressed = domainId == _inspirationFilterDomain
            };
            filter.AddThemeFontSizeOverride("font_size", 17);
            var capturedDomain = domainId;
            filter.Pressed += () => select_inspiration_filter(capturedDomain);
            filterRow.AddChild(filter);
            _inspirationFilterButtons.Add(filter);
        }

        var recordsCard = new PanelContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        recordsCard.AddThemeStyleboxOverride("panel", QuietRoomStyle.Paper(.74f, 16));
        root.AddChild(recordsCard);
        var recordsBox = room_box(recordsCard);
        recordsBox.AddThemeConstantOverride("separation", 8);
        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        recordsBox.AddChild(scroll);
        _inspirationGrid = new HFlowContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _inspirationGrid.AddThemeConstantOverride("h_separation", 10);
        _inspirationGrid.AddThemeConstantOverride("v_separation", 10);
        scroll.AddChild(_inspirationGrid);
    }

    private void select_inspiration_filter(string domainId)
    {
        _inspirationFilterDomain = domainId;
        foreach (var button in _inspirationFilterButtons)
        {
            if (button.GetParent() is not HBoxContainer row) continue;
            var index = row.GetChildren().IndexOf(button);
            button.ButtonPressed = index >= 0 && index < InspirationFilters.Length && InspirationFilters[index].Id == domainId;
        }
        refresh_room_inspiration();
    }

    private void build_manual_refinement_ui(VBoxContainer parent)
    {
        var card = new PanelContainer();
        card.AddThemeStyleboxOverride("panel", QuietRoomStyle.Paper(.92f, 12));
        parent.AddChild(card);
        var box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        box.AddThemeConstantOverride("separation", 7);
        card.AddChild(box);
        box.AddChild(QuietRoomStyle.Text("手动炼化", 21));
        var description = QuietRoomStyle.Text("在吐纳页面使用一份灵感，缓解当前瓶颈减速。炼化不回退已累计的闭关时长。", 15);
        description.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        box.AddChild(description);

        var selectionRow = new HBoxContainer();
        selectionRow.AddThemeConstantOverride("separation", 10);
        box.AddChild(selectionRow);
        selectionRow.AddChild(QuietRoomStyle.Text("灵感", 16));
        _manualRefinementInspiration = new OptionButton
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 38)
        };
        _manualRefinementInspiration.AddThemeFontSizeOverride("font_size", 16);
        selectionRow.AddChild(_manualRefinementInspiration);
        _manualRefinementOrb = new InspirationQualityMark(1, CultivationInspirationTuning.CombatDomain)
        {
            CustomMinimumSize = new Vector2(48, 48),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            Visible = false
        };
        selectionRow.AddChild(_manualRefinementOrb);

        _manualRefinementPreview = QuietRoomStyle.Text("", 16);
        _manualRefinementPreview.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        box.AddChild(_manualRefinementPreview);
        _manualRefinementFeedback = QuietRoomStyle.Text("", 15);
        _manualRefinementFeedback.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        box.AddChild(_manualRefinementFeedback);
        _manualRefinementButton = new Button { Text = "炼化所选灵感", CustomMinimumSize = new Vector2(0, 46) };
        QuietRoomStyle.Primary(_manualRefinementButton);
        _manualRefinementButton.Pressed += refine_manual_selected_inspiration;
        box.AddChild(_manualRefinementButton);
        _manualRefinementInspiration.ItemSelected += select_manual_refinement_record;
    }

    private void select_manual_refinement_record(long index)
    {
        if (_refreshingManualRefinementUi || index < 0 || index >= _manualRefinementRecordIds.Count)
            return;
        _manualRefinementSelectedId = _manualRefinementRecordIds[(int)index];
        refresh_manual_refinement_ui();
    }

    private void refresh_manual_refinement_ui()
    {
        if (_manualRefinementInspiration is null || _manualRefinementOrb is null ||
            _manualRefinementPreview is null || _manualRefinementButton is null || _target is not Character character)
            return;

        _refreshingManualRefinementUi = true;
        try
        {
            var available = character.get_cultivation_inspiration_records()
                .Where(record => !record.armed_for_action)
                .OrderBy(record => record.expires_at_game_hour)
                .ToList();
            _manualRefinementRecordIds.Clear();
            _manualRefinementInspiration.Clear();
            foreach (var record in available)
            {
                var source = CultivationInspirationTuning.source_display_label(record.source_id, record.source_label);
                _manualRefinementInspiration.AddItem($"{record.get_quality_name()} · {source} · 有效至 {format_inspiration_expiry(record.expires_at_game_hour)}");
                _manualRefinementRecordIds.Add(record.record_id);
            }

            if (!available.Any(record => record.record_id == _manualRefinementSelectedId))
                _manualRefinementSelectedId = available.FirstOrDefault()?.record_id ?? "";
            var selectedIndex = _manualRefinementRecordIds.IndexOf(_manualRefinementSelectedId);
            if (available.Count == 0)
            {
                _manualRefinementInspiration.AddItem("暂无可用于炼化的灵感");
                _manualRefinementRecordIds.Add("");
                selectedIndex = 0;
            }
            _manualRefinementInspiration.Select(Math.Max(0, selectedIndex));
            _manualRefinementInspiration.Disabled = available.Count == 0;
            var selected = available.FirstOrDefault(record => record.record_id == _manualRefinementSelectedId);
            if (selected is not null)
            {
                _manualRefinementOrb.Visible = true;
                _manualRefinementOrb.SetQualityRank(selected.quality_rank);
                _manualRefinementOrb.SetDomainId(selected.use_domain_id);
                _manualRefinementOrb.TooltipText = $"{selected.get_quality_name()} · {CultivationInspirationTuning.action_domain_label(selected.use_domain_id)}";
            }
            else
            {
                _manualRefinementOrb.Visible = false;
            }

            var bottleneck = character.get_cultivation_bottleneck_summary();
            var applicable = bottleneck.GetValueOrDefault("applicable", false).AsBool();
            var penalty = bottleneck.GetValueOrDefault("current_penalty", bottleneck.GetValueOrDefault("penalty", 0d)).AsDouble();
            var efficiency = bottleneck.GetValueOrDefault("gain_multiplier", 1d).AsDouble();
            var tier = bottleneck.GetValueOrDefault("realm_tier", 1).AsInt32();
            if (!applicable)
            {
                _manualRefinementPreview.Text = "当前境界没有可炼化的云息瓶颈；归元九息不受瓶颈减速。";
                _manualRefinementButton.Disabled = true;
                return;
            }

            _manualRefinementPreview.Text = $"当前减速 {penalty:P1}　·　吐纳效率 {efficiency:P1}";
            if (selected is null)
            {
                _manualRefinementPreview.Text += "\n没有可用于炼化的灵感。";
                _manualRefinementButton.Disabled = true;
            }
            else if (penalty <= 0d)
            {
                _manualRefinementPreview.Text += "\n瓶颈形成后，方可炼化灵感。";
                _manualRefinementButton.Disabled = true;
            }
            else
            {
                var nextPenalty = CultivationBottleneckRules.relieve_penalty(penalty, tier, selected.quality_rank);
                _manualRefinementPreview.Text += $"\n选用「{selected.get_quality_name()}」后，减速将降至 {nextPenalty:P1}，吐纳效率升至 {1d - nextPenalty:P1}。";
                _manualRefinementButton.Disabled = false;
            }
        }
        finally
        {
            _refreshingManualRefinementUi = false;
        }
    }

    private void refine_manual_selected_inspiration()
    {
        if (_refreshingManualRefinementUi || _target is not Character character || string.IsNullOrWhiteSpace(_manualRefinementSelectedId))
            return;
        if (!character.try_refine_cultivation_bottleneck(_manualRefinementSelectedId, out var result))
        {
            _manualRefinementFeedback!.Text = "未能炼化；请确认所选灵感仍可用，且当前已有瓶颈。";
            refresh_manual_refinement_ui();
            return;
        }

        var oldPenalty = result.GetValueOrDefault("old_penalty", 0d).AsDouble();
        var newPenalty = result.GetValueOrDefault("new_penalty", 0d).AsDouble();
        _manualRefinementFeedback!.Text = $"已炼化一份灵感。减速 {oldPenalty:P1} → {newPenalty:P1}；吐纳效率 {1d - oldPenalty:P1} → {1d - newPenalty:P1}。";
        _manualRefinementSelectedId = "";
        refresh_manual_refinement_ui();
        refresh_room_header();
        refresh_plan();
    }

    private void refresh_room_inspiration()
    {
        if (_inspirationPage is null || _inspirationGrid is null || _inspirationSummary is null ||
            _target is not Character character)
            return;

        foreach (var child in _inspirationGrid.GetChildren())
        {
            _inspirationGrid.RemoveChild(child);
            child.QueueFree();
        }

        var available = character.get_cultivation_inspiration_records()
            .OrderByDescending(record => record.armed_for_action)
            .ThenBy(record => record.expires_at_game_hour)
            .ToList();
        var armedCount = available.Count(record => record.armed_for_action);
        var qualityCounts = new int[4];
        foreach (var record in available)
            if (record.quality_rank is >= 1 and <= 3) qualityCounts[record.quality_rank]++;
        _inspirationSummary.Text =
            $"可用 {available.Count} 份　·　已选用于行动 {armedCount} 份　·　见几 {qualityCounts[1]}　明辨 {qualityCounts[2]}　通变 {qualityCounts[3]}";

        var visibleRecords = available
            .Where(record => string.IsNullOrEmpty(_inspirationFilterDomain) || record.use_domain_id == _inspirationFilterDomain)
            .ToList();
        foreach (var record in visibleRecords)
            add_inspiration_record_card(record, character);

        if (visibleRecords.Count == 0)
        {
            var empty = QuietRoomStyle.Text(
                available.Count == 0 ? "当前没有可用灵感。\n战斗、采集、旅途、研读与宗门活动的实际结果都可能产生新的记录。" : "这个用途下暂时没有可用灵感。",
                20);
            empty.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            empty.CustomMinimumSize = new Vector2(360, 150);
            empty.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _inspirationGrid.AddChild(empty);
        }
    }

    private void add_inspiration_record_card(CultivationInspirationRecord record, Character character)
    {
        var card = new PanelContainer
        {
            Name = "InspirationRecord_" + record.record_id,
            CustomMinimumSize = new Vector2(174, 172),
            TooltipText = inspiration_record_tooltip(record, character),
            MouseFilter = Control.MouseFilterEnum.Pass
        };
        card.AddThemeStyleboxOverride("panel", QuietRoomStyle.Paper(.92f, 12));
        _inspirationGrid!.AddChild(card);

        var content = new VBoxContainer();
        content.MouseFilter = Control.MouseFilterEnum.Ignore;
        content.AddThemeConstantOverride("separation", 2);
        card.AddChild(content);

        var topLine = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        topLine.AddThemeConstantOverride("separation", 4);
        content.AddChild(topLine);
        var rank = QuietRoomStyle.Text(record.get_quality_name(), 15);
        rank.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        rank.MouseFilter = Control.MouseFilterEnum.Ignore;
        topLine.AddChild(rank);
        var state = inspiration_record_short_status(record, character);
        if (!string.IsNullOrWhiteSpace(state))
        {
            var stateLabel = QuietRoomStyle.Text(state, 13);
            stateLabel.HorizontalAlignment = HorizontalAlignment.Right;
            stateLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
            topLine.AddChild(stateLabel);
        }

        var orbRow = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore };
        orbRow.Alignment = BoxContainer.AlignmentMode.Center;
        orbRow.AddChild(new InspirationQualityMark(record.quality_rank, record.use_domain_id)
        {
            CustomMinimumSize = new Vector2(82, 82),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            MouseFilter = Control.MouseFilterEnum.Ignore
        });
        content.AddChild(orbRow);

        var usage = QuietRoomStyle.Text(inspiration_usage_label(record.use_domain_id), 19);
        usage.HorizontalAlignment = HorizontalAlignment.Center;
        usage.MouseFilter = Control.MouseFilterEnum.Ignore;
        content.AddChild(usage);
        var remaining = QuietRoomStyle.Text($"尚余 {inspiration_remaining_text(record.expires_at_game_hour)}", 14);
        remaining.HorizontalAlignment = HorizontalAlignment.Center;
        remaining.MouseFilter = Control.MouseFilterEnum.Ignore;
        content.AddChild(remaining);
    }

    private static string inspiration_record_tooltip(CultivationInspirationRecord record, Character character)
    {
        var source = CultivationInspirationTuning.source_display_label(record.source_id, record.source_label);
        return $"{record.get_quality_name()}灵感 · {inspiration_usage_label(record.use_domain_id)}\n" +
               $"实际效果：{CultivationInspirationTuning.action_effect_text(record.use_domain_id, record.quality_rank)}\n" +
               $"经历来源：{source}\n" +
               $"剩余有效期：{inspiration_remaining_text(record.expires_at_game_hour)}\n" +
               $"有效至：{format_inspiration_expiry(record.expires_at_game_hour)}\n" +
               $"当前状态：{inspiration_record_status(record, character)}";
    }

    private static string inspiration_record_short_status(CultivationInspirationRecord record, Character character)
    {
        if (record.armed_for_action)
            return "已预约";
        var settings = character.get_cultivation_auto_refinement_settings();
        return settings.GetValueOrDefault("enabled", false).AsBool() &&
               settings.GetValueOrDefault("record_id", "").AsString() == record.record_id
            ? "自动炼化" : "";
    }

    private static string inspiration_usage_label(string domainId) => domainId == CultivationInspirationTuning.StudyDomain
        ? "研读"
        : CultivationInspirationTuning.action_domain_label(domainId);

    private static string inspiration_record_status(CultivationInspirationRecord record, Character character)
    {
        if (record.armed_for_action)
            return $"已预约用于下一次{inspiration_usage_label(record.use_domain_id)}";

        var settings = character.get_cultivation_auto_refinement_settings();
        if (settings.GetValueOrDefault("enabled", false).AsBool() &&
            settings.GetValueOrDefault("record_id", "").AsString() == record.record_id)
            return "已留用于吐纳自动炼化";

        return "尚未使用";
    }

    private static string inspiration_remaining_text(long expiresAtGameHour)
    {
        var now = (long)(Engine.GetMainLoop() is SceneTree tree
            ? tree.Root.GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? 0
            : 0);
        var hours = Math.Max(0, expiresAtGameHour - now);
        var days = hours / 24;
        var remainder = hours % 24;
        return days > 0 ? $"{days}日 {remainder}小时" : $"{remainder}小时";
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
