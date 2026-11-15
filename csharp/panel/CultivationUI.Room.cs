using Godot;

public partial class CultivationUI
{
    private Control _room = null!, _roomContent = null!;
    private PanelContainer _studyPage = null!, _breakPage = null!;
    private ScrollContainer _roomBodyScroll = null!, _roomNatureScroll = null!;
    private Label _roomTime = null!, _roomStamina = null!, _roomMain = null!, _roomBottleneckStatus = null!, _roomWorldStatus = null!, _roomInspirationSummary = null!, _studyTitle = null!;
    private Button _roomInspirationButton = null!;
    private CheckBox _autoRefinementToggle = null!;
    private OptionButton _autoRefinementInspiration = null!;
    private InspirationQualityMark? _autoRefinementOrb = null;
    private SpinBox _autoRefinementThreshold = null!;
    private Label _autoRefinementHint = null!, _autoRefinementFeedback = null!;
    private VBoxContainer _studyList = null!;
    private VBoxContainer? _studyRequirementList;
    private Label? _studyRequirementTitle;
    private InspirationActionPicker? _studyInspirationPicker = null;
    private ProgressBar _roomPowerBar = null!, _studyProgress = null!;
    private readonly List<Button> _roomTabs = [], _modeTabs = [];
    private readonly Dictionary<string, Texture2D> _manualIcons = [];
    private GameTimeMgr? _roomClock;
    private bool _roomActions;
    private bool _refreshingAutoRefinementUi;
    private readonly List<string> _autoRefinementRecordIds = [];
    private int _roomPage;

    private void build_room_shell() => build_paper_room_shell();

    private static void style_room_navigation(Button tab) => PaperButtonFeedback.Apply(tab, CraftingPaperStyle.ButtonKind.Navigation, 23);

    public override void _ExitTree() { if (GodotObject.IsInstanceValid(_roomClock)) _roomClock!.time_changed -= room_time_changed; }
    private void room_time_changed() { if (Visible && _room.Visible) { refresh_room_header(); if (_panel?.Visible == true) refresh_plan(); if (_inspirationPage?.Visible == true) refresh_room_inspiration(); } }
    private PanelContainer room_page(string name)
    {
        var page = new PanelContainer { Name = name, MouseFilter = MouseFilterEnum.Pass };
        page.AddThemeStyleboxOverride("panel", new StyleBoxEmpty()); _roomContent.AddChild(page); page.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); page.Hide(); return page;
    }
    private static VBoxContainer room_box(PanelContainer parent)
    {
        var box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill }; box.AddThemeConstantOverride("separation", 14); parent.AddChild(box); return box;
    }
    private void build_room_ui() => build_paper_practice();

    private void build_room_study() => build_paper_study();

    private void build_room_breakthrough() => build_paper_breakthrough();

    private ScrollContainer mount_room_panel(PanelContainer panel)
    {
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; _roomContent.AddChild(scroll); scroll.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        panel.Reparent(scroll); panel.SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft); panel.SizeFlagsHorizontal = SizeFlags.ExpandFill; panel.SizeFlagsVertical = SizeFlags.ExpandFill; panel.ZIndex = 0;
        panel.AddThemeStyleboxOverride("panel", QuietRoomStyle.Paper(1, 16));
        foreach (var text in panel.FindChildren("*", "Label", true, false).OfType<Label>())
            if (text.Text.Length > 24) text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        foreach (var column in panel.FindChildren("*", "PanelContainer", true, false).OfType<PanelContainer>())
            if (column.CustomMinimumSize.X >= 520) column.CustomMinimumSize = new Vector2(380, column.CustomMinimumSize.Y);
        if (panel.GetChildOrNull<VBoxContainer>(0) is { } root)
        {
            // 公共顶栏负责标题、日期和退出，避免每页重复占用空间。
            if (root.GetChildOrNull<Control>(0) is { } head) head.Hide();
            if (root.GetChildOrNull<Control>(1) is { } name) name.Hide();
            if (root.GetChildOrNull<Control>(2) is { } realm) realm.Hide();
        }
        scroll.Hide(); return scroll;
    }
    private void finish_room_shell()
    {
        // Existing read-only legacy APIs keep their own panels, outside the three-page navigation.
        _roomBodyScroll = mount_room_panel(_bodyPanel!);
        _roomNatureScroll = mount_room_panel(_visualPanel!);
    }

    private Texture2D? manual_icon(GongfaResource book)
    {
        if (_manualIcons.TryGetValue(book.ResourcePath, out var cached)) return cached;
        // 已有秘籍按此约定命名；通过 Godot 加载，导出包内也可读取。
        var path = "res://resources/entities/items/" + book.gongfa_name + "秘籍.tres";
        if (ResourceLoader.Exists(path) && GD.Load<Resource>(path) is ItemData item && !string.IsNullOrEmpty(item.icon_path) && ResourceLoader.Exists(item.icon_path))
        {
            var texture = GD.Load<Texture2D>(item.icon_path); _manualIcons[book.ResourcePath] = texture; return texture;
        }
        var fallback = GD.Load<Texture2D>("res://textures/items/inventory_density_20261029/manual_generic_2x3.png");
        if (fallback is not null) _manualIcons[book.ResourcePath] = fallback;
        return fallback;
    }
    private void select_room_page(int page)
    {
        if (page == 5) page = 0;
        if (!_roomActions && page != _roomPage) return;
        _roomPage = page; _room.Show();
        _overviewPanel?.Hide();
        _panel!.Visible = page == 0; _studyPage.Visible = page == 1; _breakPage.Visible = page == 2; if (_inspirationPage is not null) _inspirationPage.Visible = page == 5;
        _roomBodyScroll.Visible = page == 3; _bodyPanel!.Visible = page == 3;
        _roomNatureScroll.Visible = page == 4; _visualPanel!.Visible = page == 4;
        _yuanxiPanel?.Hide();
        if (page == 0 && _target is Character yuanxi && yuanxi.is_yuanxi_realm())
        {
            _panel.Hide();
            if (_yuanxiPanel is null)
            {
                _yuanxiPanel = new YuanxiPanel { EmbeddedInQuietRoom = true };
                _roomContent.AddChild(_yuanxiPanel); _yuanxiPanel.Closed += close_panel;
            }
            _yuanxiPanel.ShowFor(yuanxi);
            style_paper_yuanxi();
        }
        for (var i = 0; i < _roomTabs.Count; i++)
        {
            if (_roomTabs[i] is QuietRoomPaperTab tab) tab.SelectPaper(i == page, _paperReducedMotion);
            else _roomTabs[i].SetPressedNoSignal(i == page);
            _roomTabs[i].Disabled = !_roomActions && i != page;
        }
        refresh_room_header();
        if (page == 3) refresh_body_ui(); else if (page == 4) refresh_visualization_ui(); else if (page == 5) refresh_room_inspiration(); else refresh();
    }
    private void refresh_room_header()
    {
        if (_roomClock is not null) _roomTime.Text = QuietRoomStyle.Date(_roomClock);
        _name!.Text = value("entity_name", "").AsString();
        _realm!.Text = value("cultivation_realm", "凡人").AsString();
        _roomStamina.Text = $"{value("current_world_stamina", 0f).AsSingle():0.#} / {value("max_world_stamina", 100f).AsSingle():0.#}";
        refresh_world_status();
        refresh_paper_room_header();
    }

    private void refresh_room_inspiration_access()
    {
        if (_roomInspirationButton is null || _roomInspirationSummary is null) return;
        _roomInspirationButton.Visible = _roomInspirationSummary.Visible = CultivationInspirationSystem.Enabled;
        if (!CultivationInspirationSystem.Enabled) return;
        if (_target is not Character character)
        {
            _roomInspirationButton.Text = "灵感清册";
            _roomInspirationSummary.Text = "灵感需由经历获得。";
            return;
        }

        var available = character.get_cultivation_inspiration_records();
        var counts = new int[4];
        var actionSelected = 0;
        foreach (var record in available)
        {
            if (record.quality_rank >= 1 && record.quality_rank <= 3)
                counts[record.quality_rank]++;
            if (record.armed_for_action)
                actionSelected++;
        }
        _roomInspirationButton.Text = available.Count > 0 ? $"灵感清册　{available.Count}" : "灵感清册";
        if (_roomTabs.Count > 5)
            _roomTabs[5].Text = "灵感清册";
        _roomInspirationSummary.Text = available.Count > 0
            ? $"可用灵感 {available.Count} 份：见几 {counts[1]} · 明辨 {counts[2]} · 通变 {counts[3]}\n行动页面已选择 {actionSelected} 份。灵感清册只展示记录；使用入口位于对应页面。"
            : "暂无可用灵感。战斗、采集、旅途、研读与宗门活动的实际结果都可能产生记录。点击上方“灵感清册”查看。";
    }

    private void refresh_auto_refinement_ui()
    {
        if (!CultivationBottleneckRules.Enabled) return;
        if (_autoRefinementToggle is null || _autoRefinementInspiration is null || _autoRefinementThreshold is null ||
            _autoRefinementHint is null || _target is not Character character)
            return;

        _refreshingAutoRefinementUi = true;
        try
        {
            var settings = character.get_cultivation_auto_refinement_settings();
            var enabled = settings.GetValueOrDefault("enabled", false).AsBool();
            var savedRecordId = settings.GetValueOrDefault("record_id", "").AsString();
            var savedThreshold = settings.GetValueOrDefault("threshold_penalty", .02d).AsDouble();
            var currentPenalty = settings.GetValueOrDefault("current_penalty", 0d).AsDouble();
            var available = character.get_cultivation_inspiration_records()
                .Where(record => !record.armed_for_action)
                .ToList();

            _autoRefinementRecordIds.Clear();
            _autoRefinementInspiration.Clear();
            var selectedRecordIndex = -1;
            if (!string.IsNullOrWhiteSpace(savedRecordId) && !available.Any(record => record.record_id == savedRecordId) && enabled)
            {
                _autoRefinementInspiration.AddItem("已选灵感不可用，请重新选择");
                _autoRefinementRecordIds.Add("");
                selectedRecordIndex = 0;
            }
            foreach (var record in available)
            {
                var source = CultivationInspirationTuning.source_display_label(record.source_id, record.source_label);
                _autoRefinementInspiration.AddItem($"{record.get_quality_name()} · {source} · 有效至 {format_inspiration_expiry(record.expires_at_game_hour)}");
                _autoRefinementRecordIds.Add(record.record_id);
                if (record.record_id == savedRecordId) selectedRecordIndex = _autoRefinementRecordIds.Count - 1;
            }
            if (_autoRefinementRecordIds.Count == 0)
            {
                _autoRefinementInspiration.AddItem("暂无可用于炼化的灵感");
                _autoRefinementRecordIds.Add("");
                selectedRecordIndex = 0;
            }
            else if (selectedRecordIndex < 0)
            {
                selectedRecordIndex = 0;
            }
            _autoRefinementInspiration.Select(selectedRecordIndex);
            _autoRefinementInspiration.Disabled = available.Count == 0;
            var selectedRecordId = selectedRecordIndex >= 0 && selectedRecordIndex < _autoRefinementRecordIds.Count
                ? _autoRefinementRecordIds[selectedRecordIndex]
                : "";
            var selectedRecord = available.FirstOrDefault(record => record.record_id == selectedRecordId);
            if (_autoRefinementOrb is not null)
            {
                _autoRefinementOrb.Visible = selectedRecord is not null;
                if (selectedRecord is not null)
                {
                    _autoRefinementOrb.SetQualityRank(selectedRecord.quality_rank);
                    _autoRefinementOrb.SetDomainId(selectedRecord.use_domain_id);
                    _autoRefinementOrb.TooltipText = $"{selectedRecord.get_quality_name()} · {CultivationInspirationTuning.action_domain_label(selectedRecord.use_domain_id)}";
                }
            }

            _autoRefinementThreshold.Value = Math.Clamp(savedThreshold * 100d, 1d, 90d);
            _autoRefinementThreshold.Editable = true;
            _autoRefinementToggle.ButtonPressed = enabled;

            if (!enabled)
            {
                _autoRefinementHint.Text = $"开启后，本段修炼首次达到瓶颈减速 {savedThreshold:P0} 时炼化一份；未达到则不消耗。";
            }
            else if (!settings.GetValueOrDefault("eligible", false).AsBool())
            {
                _autoRefinementHint.Text = settings.GetValueOrDefault("reason", "当前设置暂不可用。").AsString();
            }
            else
            {
                var quality = settings.GetValueOrDefault("quality_name", "所选").AsString();
                _autoRefinementHint.Text = currentPenalty >= savedThreshold
                    ? $"当前瓶颈减速 {currentPenalty:P1} 已达到阈值；本段开始修炼时会立即消耗一份「{quality}」，只触发一次。"
                    : $"当前减速 {currentPenalty:P1}；首次达到 {savedThreshold:P0} 时消耗一份「{quality}」，只触发一次。";
            }
        }
        finally
        {
            _refreshingAutoRefinementUi = false;
        }
    }

    private void save_auto_refinement_settings()
    {
        if (_refreshingAutoRefinementUi || _target is not Character character) return;
        var settings = character.get_cultivation_auto_refinement_settings();
        var enabled = _autoRefinementToggle.ButtonPressed;
        var recordIndex = _autoRefinementInspiration.Selected;
        var recordId = recordIndex >= 0 && recordIndex < _autoRefinementRecordIds.Count
            ? _autoRefinementRecordIds[recordIndex]
            : "";
        var threshold = _autoRefinementThreshold.Value / 100d;

        if (!character.set_cultivation_auto_refinement_settings(enabled, recordId, threshold, out var reason))
        {
            _autoRefinementHint.Text = reason;
            if (enabled)
            {
                _autoRefinementToggle.ButtonPressed = false;
                character.set_cultivation_auto_refinement_settings(false, "", threshold, out _);
            }
        }
        else
        {
            _autoRefinementFeedback.Text = enabled ? "自动炼化设置已保存。" : "自动炼化已关闭；已选灵感不会被消耗。";
        }

        refresh_auto_refinement_ui();
        refresh_plan();
    }

    private void show_auto_refinement_result(Character character)
    {
        if (_autoRefinementFeedback is null) return;
        var result = character.get_last_cultivation_auto_refinement_result();
        if (!result.GetValueOrDefault("configured", false).AsBool())
        {
            _autoRefinementFeedback.Text = "";
            return;
        }

        var threshold = result.GetValueOrDefault("threshold_penalty", 0d).AsDouble();
        if (result.GetValueOrDefault("consumed", false).AsBool())
        {
            var rank = result.GetValueOrDefault("quality_rank", 1).AsInt32();
            var qualityName = rank switch { 1 => "见几", 2 => "明辨", 3 => "通变", _ => "" };
            var oldPenalty = result.GetValueOrDefault("old_penalty", 0d).AsDouble();
            var newPenalty = result.GetValueOrDefault("new_penalty", 0d).AsDouble();
            _autoRefinementFeedback.Text =
                $"瓶颈减速达到 {threshold:P0}，已消耗一份{qualityName}灵感；减速由 {oldPenalty:P1} 降至 {newPenalty:P1}。本段仅触发一次。";
        }
        else if (result.GetValueOrDefault("failed", false).AsBool())
        {
            _autoRefinementFeedback.Text = $"瓶颈减速达到 {threshold:P0}，但灵感在结算时已不可用；本段按未炼化结算。";
        }
        else if (result.GetValueOrDefault("expired_before_threshold", false).AsBool())
        {
            _autoRefinementFeedback.Text = $"本段未能在灵感有效期内达到瓶颈减速 {threshold:P0}；未消耗灵感。";
        }
        else
        {
            _autoRefinementFeedback.Text = $"本段未达到瓶颈减速 {threshold:P0}，未消耗灵感；自动设置仍保留。";
        }
    }

    private void refresh_bottleneck_status()
    {
        if (_roomBottleneckStatus is null) return;
        _roomBottleneckStatus.Visible = CultivationBottleneckRules.Enabled;
        if (!CultivationBottleneckRules.Enabled) return;
        if (_target is not Character character)
        {
            _roomBottleneckStatus.Text = "闭关瓶颈：当前境界不受此规则影响。";
            return;
        }

        var summary = character.get_cultivation_bottleneck_summary();
        if (!summary.GetValueOrDefault("applicable", false).AsBool())
        {
            _roomBottleneckStatus.Text = "闭关瓶颈：当前境界不受此规则影响；归元九息不受瓶颈减速。";
            return;
        }

        var penalty = summary.GetValueOrDefault("penalty", 0d).AsDouble();
        var accumulated = summary.GetValueOrDefault("accumulated_cultivation_hours", 0d).AsDouble();
        var grace = summary.GetValueOrDefault("grace_hours", 0d).AsDouble();
        var trainingRate = character.get_cultivation_rate_after_bottleneck();

        if (penalty <= 0d)
        {
            var untilFirstStage = grace + CultivationBottleneckRules.get_stage_interval_hours() - accumulated;
            _roomBottleneckStatus.Text =
                $"当前瓶颈减速 {penalty:P1}　·　吐纳效率 {1d - penalty:P1}\n" +
                $"累计闭关吐纳 {format_training_days(accumulated)} 日；预计约 {format_training_days(untilFirstStage)} 日后开始减速 · 当前修炼速率 {trainingRate:0.####} 缕／时";
            return;
        }

        _roomBottleneckStatus.Text =
            $"当前瓶颈减速 {penalty:P1}　·　吐纳效率 {1d - penalty:P1}\n" +
            $"累计闭关吐纳 {format_training_days(accumulated)} 日　·　当前修炼速率 {trainingRate:0.####} 缕／时";
    }
    private static string format_training_days(double hours)
        => $"{Math.Max(0d, hours) / CultivationBottleneckRules.HoursPerGameDay:0.#}";
    private void refresh_world_status()
    {
        if (_roomWorldStatus is null) return;
        if (_target is not Character character || character.entity_data is not HumanData human)
        {
            _roomWorldStatus.Hide();
            return;
        }

        var now = _roomClock?.get_total_game_hours() ?? 0;
        var summary = WorldStatusRules.format_summary(PillToxicityRules.WithStatus(human, now), now);
        _roomWorldStatus.Text = summary == "暂无特殊状态" ? "身体状态：暂无特殊状态" : $"身体状态：{summary}";
        _roomWorldStatus.Visible = summary != "暂无特殊状态";
    }
    private void sync_mode_tabs()
    {
        for (var i = 0; i < _modeTabs.Count; i++)
        {
            _modeTabs[i].SetPressedNoSignal(i == _mode!.Selected);
            PaperButtonFeedback.Apply(_modeTabs[i], CraftingPaperStyle.ButtonKind.Regular, 23, i == _mode.Selected);
        }
    }
    private void refresh_room_study()
    {
        _studyInspirationPicker?.Configure(_target as Character, CultivationInspirationTuning.StudyDomain, "功法研读");
        foreach (var node in _studyList.GetChildren()) { _studyList.RemoveChild(node); node.QueueFree(); }
        if (_studyCandidates.Count == 0) { var empty = QuietRoomStyle.Text("暂无可研读功法", 19); _studyList.AddChild(empty); }
        for (var i = 0; i < _studyCandidates.Count; i++)
        {
            var index = i; var book = _studyCandidates[i];
            var button = new Button { Text = book.gongfa_name, ToggleMode = true, ButtonPressed = i == _studyBook!.Selected, Name = "StudyBook_" + i, CustomMinimumSize = new Vector2(0, 94), SizeFlagsHorizontal = SizeFlags.ExpandFill, ExpandIcon = true, IconAlignment = HorizontalAlignment.Left, Alignment = HorizontalAlignment.Left, AutowrapMode = TextServer.AutowrapMode.WordSmart };
            button.Icon = manual_icon(book);
            PaperButtonFeedback.Apply(button, CraftingPaperStyle.ButtonKind.Regular, 22, i == _studyBook!.Selected);
            button.AddThemeConstantOverride("icon_max_width", 54); button.Pressed += () => select_study_book(index); _studyList.AddChild(button);
        }
        _studyTitle.Text = _studyCandidates.Count > 0 ? _studyCandidates[Math.Clamp(_studyBook!.Selected, 0, _studyCandidates.Count - 1)].gongfa_name : "尚无读本";
        var selected = selected_study_book();
        var chapter = _target is Character reader && selected is not null ? selected.get_sorted_chapters().FirstOrDefault(c => !reader.is_gongfa_chapter_learned(selected,c.get_id())) : null;
        _studyProgress.MaxValue = Math.Max(1f, chapter?.required_study_progress ?? selected?.required_study_progress ?? 1f);
        _studyProgress.Value = chapter is not null && _target is Character student ? student.get_gongfa_chapter_study_progress(selected!,chapter.get_id()) : 0;
        refresh_paper_study_chapters(selected, _target as Character, chapter);
    }

    private void refresh_room_study_requirements(Character? student, GongfaResource? book, GongfaChapterConfig? chapter, bool firstVolumeAccess = false)
    {
        if (_studyRequirementList is null || _studyRequirementTitle is null) return;
        foreach (var node in _studyRequirementList.GetChildren()) { _studyRequirementList.RemoveChild(node); node.QueueFree(); }
        if (student is null || book is null || chapter is null)
        {
            _studyRequirementTitle.Hide(); _studyRequirementList.Hide(); return;
        }

        var requirements = new List<(string Text, bool Met)>();
        if (firstVolumeAccess)
        {
            // 首卷访问按静室现行规则直接开放；这里显示实际入口规则，避免列出不会阻止首卷的后续卷门槛。
            requirements.Add(("首卷可直接研读", true));
        }
        else
        {
            if (is_lineage_first_volume_restricted(student, book, chapter))
                requirements.Add(("师承权限：仅开放所选秘籍第一卷", false));

            if (book.wuxing != GongfaResource.Wuxing.NONE)
            {
                var root = (GongfaResource.Wuxing)student.spirit_root_type;
                var required = book.wuxing == GongfaResource.Wuxing.ALL
                    ? "有灵根"
                    : wuxing_names(book.wuxing) + (book.wuxing_requirement_mode == GongfaResource.WuxingRequirementMode.AND ? "（需兼具）" : "（任一匹配）");
                requirements.Add(($"灵根相性：当前{wuxing_name(root)}，要求{required}", book.is_wuxing_compatible(student.spirit_root_type)));
            }

            if (!string.IsNullOrWhiteSpace(chapter.required_realm))
            {
                var realms = student.GetNodeOrNull<RealmManager>("/root/RealmManager");
                var met = realms?.is_realm_after_or_equal(student.cultivation_realm, chapter.required_realm)
                    ?? student.cultivation_realm == chapter.required_realm;
                requirements.Add(($"境界：当前{student.cultivation_realm} / 要求{chapter.required_realm}", met));
            }
            foreach (Variant key in chapter.required_attributes.Keys)
            {
                var name = key.AsString(); var need = chapter.required_attributes[key].AsSingle(); var actual = CardRules.Number(student, name);
                requirements.Add(($"{study_attribute_name(name)}：{actual:0.0} / {need:0.#}", actual >= need));
            }
            foreach (var id in chapter.required_chapter_ids)
            {
                var met = student.is_gongfa_chapter_learned(book, id);
                var requiredChapter = book.get_chapter_by_id(id);
                requirements.Add(($"前置卷：{requiredChapter?.chapter_name ?? id}（{(met ? "已研读" : "未研读")}）", met));
            }
            if (requirements.Count == 0) requirements.Add(("当前卷无额外研读条件", true));
        }

        _studyRequirementTitle.Show(); _studyRequirementList.Show();
        foreach (var requirement in requirements)
        {
            var label = QuietRoomStyle.Text($"{(requirement.Met ? "✓" : "✕")} {requirement.Text}", 18);
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            label.AddThemeColorOverride("font_color", requirement.Met ? new Color(.18f, .42f, .24f) : new Color(.64f, .19f, .14f));
            _studyRequirementList.AddChild(label);
        }
    }

    private static bool is_lineage_first_volume_restricted(Character student, GongfaResource book, GongfaChapterConfig chapter)
    {
        if (student.entity_data is HumanData baishi && baishi.baishi_first_volume_only &&
            baishi.baishi_first_volume_path == book.ResourcePath && book.get_sorted_chapters().FirstOrDefault()?.get_id() != chapter.get_id()) return true;
        if (student.entity_data is not HumanData human || human.sect_affiliation_id != "yuxu_xianzong" ||
            human.sect_lineage_gongfa_path != book.ResourcePath ||
            !human.sect_lineage_first_volume_only_access ||
            human.sect_current_rank is not ("真传" or "真传弟子")) return false;
        var firstChapter = book.get_sorted_chapters().FirstOrDefault();
        return firstChapter is not null && firstChapter.get_id() != chapter.get_id() &&
            student.is_gongfa_chapter_learned(book, firstChapter.get_id());
    }

    private static string wuxing_names(GongfaResource.Wuxing value) => string.Join("、", value.ToString().Split('_').Select(part => part switch
    {
        "METAL" => "金", "WOOD" => "木", "WATER" => "水", "FIRE" => "火", "EARTH" => "土", _ => part
    }));

    private static string wuxing_name(GongfaResource.Wuxing value) => value switch
    {
        GongfaResource.Wuxing.NONE => "无灵根", GongfaResource.Wuxing.ALL => "五行灵根", _ => wuxing_names(value) + "灵根"
    };

    private static string study_attribute_name(string name) => name switch
    {
        "physique" => "体魄", "intelligence" => "灵慧", "soul" => "神魂", "fortune" => "机缘",
        "comprehension" => "悟性", "charisma" => "魅力", "social" => "交际", "management" => "治事",
        "body_proficiency" => "体道造诣", "sword_proficiency" => "剑道造诣", _ => name
    };
}
