using Godot;

public partial class MainScene
{
    private const string YuxuSectId = "yuxu_xianzong";
    private const string SpiritStoneItemPath = "res://resources/entities/items/灵石.tres";
    private const string TianwenPracticeBeastPath = "res://resources/beast_templates/林地凶狼.tres";
    private const string YuxuStudyDutyId = "study_chapter";
    private const string YuxuYaopuWatchDutyId = "yaopu_watch";
    private const string YuxuYaopuCultivationDutyId = "yaopu_cultivate_mist_bell";
    private const string YuxuEastWoodsPatrolDutyId = "east_woods_patrol";
    private const string YuxuEastWoodsConflictReviewDutyId = "east_woods_conflict_review";
    private const string YuxuCaseFieldTaskDutyId = "yuxu_case_field_task";
    private const string YuxuTianwenPracticeDutyId = "tianwen_practice";
    private const string YuxuBeastBoneSubmissionDutyId = "beast_bone_submission";
    private const string YuxuBitterMossRootDutyId = "bitter_moss_root_submission";
    private const string BitterMossRootItemPath = "res://resources/entities/items/苦苔根.tres";
    private const string MistBellFlowerItemPath = "res://resources/entities/items/雾铃花.tres";
    private const string BeastBoneItemPath = "res://resources/entities/items/兽骨.tres";
    private const int YuxuStudyDutyContributionReward = YuxuSectPrototypeBalance.OfficialStudyDutyReward;
    private const int YuxuTianwenPracticeDutyReward = 5;
    private const long YuxuTrueDiscipleLectureInitialDelayHours = 360L * 24L;
    private const long YuxuTrueDiscipleLecturePeriodHours = 180L * 24L;

    private string _player_current_yuxu_peak_id = "";
    private bool _yuxu_tianwen_practice_confirm_open;
    private long _yuxu_lecture_context_due_hour = long.MinValue;
    private bool _yuxu_lecture_context_is_due;
    private int _yuxu_lecture_context_missed_count = -1;
    private int _yuxu_lecture_context_consecutive_leave_count = -1;
    private long _yuxu_lecture_context_last_attended_hour = long.MinValue;
    private TrueDiscipleLecturePanel? _yuxu_true_disciple_lecture_panel;
    private readonly List<JournalPanel.Entry> _discovered_yuxu_peak_entries = [];

    private static string get_yuxu_peak_name(string peakId) => peakId switch
    {
        "heguang" => "和光峰",
        "guanfu" => "观复峰",
        "zifu" => "紫府峰",
        "xuanlan" => "玄览峰",
        "tianwen" => "天问峰",
        "tiangong" => "天工峰",
        "zhizhong" => "执中峰",
        "yaopu" => "瑶圃峰",
        _ => "玉虚仙宗"
    };

    private void on_yuxu_peak_selected(string peakId)
    {
        _player_current_yuxu_peak_id = peakId;
        refresh_yuxu_life_context();
        if (GetTree().GetFirstNodeInGroup("cell_character_list") is CellCharacterList roster)
            roster.show_world_context("yuxu_xianzong_core", peakId, $"interior:yuxu_xianzong:{peakId}");

        var peakInterior = GetNodeOrNull<Node>("SceneLayer/YuxuXianzongOverview/PeakInterior");
        if (peakInterior is null || !peakInterior.HasMethod("get_peak_journal_entry")) return;
        var details = peakInterior.Call("get_peak_journal_entry", peakId).AsGodotDictionary();
        var title = details.GetValueOrDefault("title", "").AsString();
        var subtitle = details.GetValueOrDefault("subtitle", "").AsString();
        var body = details.GetValueOrDefault("body", "").AsString();
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(body) ||
            _discovered_yuxu_peak_entries.Any(entry => entry.Title == title)) return;

        _discovered_yuxu_peak_entries.Add(new JournalPanel.Entry(
            title,
            string.IsNullOrWhiteSpace(subtitle) ? body : $"{subtitle}\n\n{body}",
            "玉虚仙宗峰内见闻",
            $"玉虚仙宗·{title}",
            "已记"));
        refresh_journal_entries();
    }
    private void clear_yuxu_peak_location() => _player_current_yuxu_peak_id = "";

    private string get_current_profile_location()
    {
        var overview = GetNodeOrNull<Control>("SceneLayer/YuxuXianzongOverview");
        if (!string.IsNullOrEmpty(_player_current_yuxu_peak_id) && overview?.Visible == true &&
            overview.GetNodeOrNull<Control>("PeakInterior")?.Visible == true)
            return "玉虚仙宗·" + get_yuxu_peak_name(_player_current_yuxu_peak_id);
        var locationName = _world_node_data?.get_node(_player_world_node_id)?.node_name;
        if (!string.IsNullOrWhiteSpace(locationName)) return locationName;
        return _player_world_node_id == "home_village" ? "青溪里" : "位置未详";
    }

    public string get_profile_location_for(WorldEntity entity)
    {
        if (!ReferenceEquals(entity, player)) return "位置未详";
        var location = get_current_profile_location();
        if (string.IsNullOrWhiteSpace(location) || location == "位置未详") location = "百川";
        return location;
    }

    private static bool is_yuxu_true_disciple(string rank)
        => rank is "真传" or "真传弟子";

    private static bool is_supported_yuxu_life_rank(string rank)
        => rank is "杂役" or "杂役弟子" or "外门" or "外门弟子" or "内门" or "内门弟子" or "真传" or "真传弟子";

    private static int get_yuxu_monthly_stipend(string rank)
        => YuxuSectPrototypeBalance.monthly_stipend(rank);

    public void on_yuxu_membership_state_changed()
    {
        sync_social_membership_month();
        mark_save_dirty();
        refresh_yuxu_life_context();
    }

    private static int get_game_month_index(GameTimeMgr time)
    {
        var index = ((long)Math.Max(1, time.year) - 1L) * 12L + Math.Clamp(time.month, 1, 12) - 1L;
        return (int)Math.Clamp(index, 0L, int.MaxValue);
    }

    private void refresh_yuxu_life_context()
    {
        if (player?.entity_data is HumanData dutyHuman)
        {
            if (dutyHuman.sect_affiliation_id == YuxuSectId)
            {
                sync_yuxu_true_disciple_lecture_schedule(dutyHuman);
                reconcile_yuxu_conflict_duty_state(dutyHuman);
            }
            if (_east_woods_dungeon is not null)
                _east_woods_dungeon.set_active_duty_context(dutyHuman.sect_affiliation_id == YuxuSectId ? dutyHuman.sect_active_yuxu_duty_id : "", dutyHuman.sect_active_yuxu_duty_stage, dutyHuman.sect_active_yuxu_duty_ready);
        }
        RefreshJournalTracker();
        refresh_journal_entries();
        var overview = GetNodeOrNull<Node>("SceneLayer/YuxuXianzongOverview");
        if (overview is null || player?.entity_data is not HumanData human) return;
        var isMember = human.sect_affiliation_id == YuxuSectId;
        if (isMember && !string.IsNullOrWhiteSpace(human.sect_active_yuxu_duty_id) &&
            !is_supported_yuxu_duty(human.sect_active_yuxu_duty_id))
        {
            clear_active_yuxu_duty(human);
            mark_save_dirty();
        }
        overview.Call("set_life_context",
            isMember,
            human.sect_current_rank,
            get_yuxu_monthly_stipend(human.sect_current_rank),
            human.sect_unclaimed_spirit_stones);

        var peakInterior = GetNodeOrNull<Node>("SceneLayer/YuxuXianzongOverview/PeakInterior");
        var activeIncidentId = isMember && human.sect_active_yuxu_duty_id == YuxuEastWoodsConflictReviewDutyId
            ? human.sect_active_yuxu_duty_incident_id
            : isMember && human.sect_active_yuxu_duty_id == YuxuCaseFieldTaskDutyId
                ? human.sect_active_yuxu_duty_incident_id
            : "";
        peakInterior?.Call("set_sect_duty_context",
            isMember,
            isMember ? human.sect_active_yuxu_duty_id : "",
            isMember && human.sect_active_yuxu_duty_ready,
            isMember ? human.sect_active_yuxu_duty_stage : 0,
            isMember && player.has_inventory_item(BeastBoneItemPath),
            isMember ? player.get_inventory_total_count(BitterMossRootItemPath) : 0,
            activeIncidentId,
            isMember ? get_yuxu_conflict_case_summaries() : new Godot.Collections.Array<Godot.Collections.Dictionary>(),
            isMember ? get_yuxu_case_task_summaries() : new Godot.Collections.Array<Godot.Collections.Dictionary>(),
            isMember && human.sect_active_yuxu_duty_id == YuxuCaseFieldTaskDutyId ? human.sect_active_yuxu_duty_task_id : "");
        var isTrueDisciple = isMember && is_yuxu_true_disciple(human.sect_current_rank);
        var npcManager = _npc_world ?? GetNodeOrNull<NpcWorldManager>("/root/NpcWorldManager");
        var teacher = isTrueDisciple ? npcManager?.get_npc(human.sect_teacher_npc_id) : null;
        var guide = isTrueDisciple ? npcManager?.get_npc(human.sect_guide_npc_id) : null;
        var lineageBook = isTrueDisciple && !string.IsNullOrWhiteSpace(human.sect_lineage_gongfa_path)
            ? ResourceLoader.Load<GongfaResource>(human.sect_lineage_gongfa_path)
            : null;
        var lineageFirst = lineageBook?.get_sorted_chapters().FirstOrDefault();
        var lineageFirstComplete = lineageBook is not null && lineageFirst is not null &&
            player.is_gongfa_chapter_learned(lineageBook, lineageFirst.get_id());
        peakInterior?.Call("set_lineage_context",
            isTrueDisciple,
            teacher?.npc_name ?? "",
            guide?.npc_name ?? "",
            lineageBook?.gongfa_name ?? "",
            lineageFirstComplete,
            isTrueDisciple && human.sect_lineage_reported_first_volume);
        refresh_yuxu_true_disciple_lecture_context(human, isTrueDisciple);
        peakInterior?.Call("set_contribution_balance",
            isMember ? human.get_yuxu_contribution_total() : 0L);
        if (_east_woods_dungeon is not null)
            _east_woods_dungeon.set_active_duty_context(
                isMember ? human.sect_active_yuxu_duty_id : "",
                isMember ? human.sect_active_yuxu_duty_stage : 0,
                isMember && human.sect_active_yuxu_duty_ready);
    }

    private void sync_yuxu_true_disciple_lecture_schedule(HumanData human)
    {
        if (human.sect_affiliation_id != YuxuSectId || !is_yuxu_true_disciple(human.sect_current_rank)) return;
        var now = (long)(GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? 0);
        var changed = false;
        if (human.sect_true_disciple_lecture_next_due_hour < 0)
        {
            var firstDue = human.sect_entry_total_game_hours >= 0
                ? human.sect_entry_total_game_hours + YuxuTrueDiscipleLectureInitialDelayHours
                : now + YuxuTrueDiscipleLectureInitialDelayHours;
            if (now > firstDue)
                firstDue += ((now - firstDue) / YuxuTrueDiscipleLecturePeriodHours) * YuxuTrueDiscipleLecturePeriodHours;
            human.sect_true_disciple_lecture_next_due_hour = firstDue;
            changed = true;
        }

        var overduePeriods = (now - human.sect_true_disciple_lecture_next_due_hour) / YuxuTrueDiscipleLecturePeriodHours;
        if (overduePeriods > 0)
        {
            var currentMissedCount = Math.Max(0, human.sect_true_disciple_lecture_missed_count);
            var addedMisses = (int)Math.Min(overduePeriods, int.MaxValue - (long)currentMissedCount);
            human.sect_true_disciple_lecture_missed_count = currentMissedCount + addedMisses;
            human.sect_true_disciple_lecture_consecutive_leave_count = 0;
            human.sect_true_disciple_lecture_last_missed_hour = human.sect_true_disciple_lecture_next_due_hour + overduePeriods * YuxuTrueDiscipleLecturePeriodHours;
            human.sect_true_disciple_lecture_next_due_hour += overduePeriods * YuxuTrueDiscipleLecturePeriodHours;
            changed = true;
        }
        if (changed) mark_save_dirty();
    }

    private void refresh_yuxu_true_disciple_lecture_context(HumanData human, bool isTrueDisciple)
    {
        var peakInterior = GetNodeOrNull<Node>("SceneLayer/YuxuXianzongOverview/PeakInterior");
        var available = false;
        var status = "";
        long dueHour = -1;
        long lastAttendedHour = -1;
        var missedCount = 0;
        if (isTrueDisciple && human.sect_affiliation_id == YuxuSectId)
        {
            var now = (long)(GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? 0);
            dueHour = human.sect_true_disciple_lecture_next_due_hour;
            lastAttendedHour = human.sect_true_disciple_lecture_last_attended_hour;
            missedCount = Math.Max(0, human.sect_true_disciple_lecture_missed_count);
            var consecutiveLeaveCount = Math.Clamp(human.sect_true_disciple_lecture_consecutive_leave_count, 0, 2);
            available = dueHour >= 0 && now >= dueHour;
            var missedText = missedCount == 0 ? "暂无缺席记档" : $"缺席记档 {missedCount} 期";
            missedText += $" · 连续请假 {consecutiveLeaveCount}/2";
            if (available)
                status = $"本期讲席待开 · {missedText}";
            else if (dueHour >= 0 && lastAttendedHour >= dueHour - YuxuTrueDiscipleLecturePeriodHours && lastAttendedHour < dueHour)
                status = $"上期已到场 · 下期于{format_yuxu_lecture_date(dueHour)}开放 · {missedText}";
            else
                status = $"下期讲席于{format_yuxu_lecture_date(dueHour)}开放 · {missedText}";
        }

        peakInterior?.Call("set_lineage_lecture_context", available, status);
        if (dueHour == _yuxu_lecture_context_due_hour && available == _yuxu_lecture_context_is_due &&
            missedCount == _yuxu_lecture_context_missed_count &&
            human.sect_true_disciple_lecture_consecutive_leave_count == _yuxu_lecture_context_consecutive_leave_count &&
            lastAttendedHour == _yuxu_lecture_context_last_attended_hour)
            return;
        _yuxu_lecture_context_due_hour = dueHour;
        _yuxu_lecture_context_is_due = available;
        _yuxu_lecture_context_missed_count = missedCount;
        _yuxu_lecture_context_consecutive_leave_count = human.sect_true_disciple_lecture_consecutive_leave_count;
        _yuxu_lecture_context_last_attended_hour = lastAttendedHour;
        RefreshJournalTracker();
        refresh_journal_entries();
    }

    private static string format_yuxu_lecture_date(long totalHours)
    {
        if (totalHours < 0) return "日期未定";
        var totalDays = totalHours / 24;
        var year = totalDays / 360 + 1;
        var month = totalDays % 360 / 30 + 1;
        var day = totalDays % 30 + 1;
        return $"第{year}年{month}月{day}日";
    }

    private void open_yuxu_true_disciple_lecture(HumanData human)
    {
        if (_yuxu_true_disciple_lecture_panel?.IsOpen == true || player?.entity_data != human) return;
        var canvas = GetNodeOrNull<CanvasLayer>("CanvasLayer");
        if (canvas is null) return;
        _yuxu_true_disciple_lecture_panel ??= new TrueDiscipleLecturePanel { Name = "TrueDiscipleLecturePanel" };
        if (_yuxu_true_disciple_lecture_panel.GetParent() is null)
            canvas.AddChild(_yuxu_true_disciple_lecture_panel);

        sync_yuxu_true_disciple_lecture_schedule(human);
        var periodDueHour = human.sect_true_disciple_lecture_next_due_hour;
        if (!record_yuxu_true_disciple_lecture_attendance(human)) return;
        var promptVariant = (int)Math.Max(0L,
            (periodDueHour - YuxuTrueDiscipleLectureInitialDelayHours) / YuxuTrueDiscipleLecturePeriodHours % TrueDiscipleLecturePanel.PromptVariantCount);
        _yuxu_true_disciple_lecture_panel.ShowLecture(format_yuxu_lecture_date(periodDueHour), promptVariant);
    }

    private bool record_yuxu_true_disciple_lecture_attendance(HumanData human)
    {
        var overview = GetNodeOrNull<Control>("SceneLayer/YuxuXianzongOverview");
        if (player?.entity_data != human || human.sect_affiliation_id != YuxuSectId || !is_yuxu_true_disciple(human.sect_current_rank) ||
            _player_current_yuxu_peak_id != "guanfu" || overview?.Visible != true ||
            overview.GetNodeOrNull<Control>("PeakInterior")?.IsVisibleInTree() != true || _east_woods_dungeon?.Visible == true)
            return false;
        var time = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
        if (time is null)
        {
            show_message("讲席未记", "游戏历法尚未就绪，本次到场未能登记。 ");
            return false;
        }
        sync_yuxu_true_disciple_lecture_schedule(human);
        var now = (long)time.get_total_game_hours();
        if (human.sect_true_disciple_lecture_next_due_hour < 0 || now < human.sect_true_disciple_lecture_next_due_hour)
        {
            show_message("尚未到讲席期", $"下期讲席将于{format_yuxu_lecture_date(human.sect_true_disciple_lecture_next_due_hour)}开放。 ");
            refresh_yuxu_true_disciple_lecture_context(human, true);
            return false;
        }

        human.sect_true_disciple_lecture_last_attended_hour = now;
        human.sect_true_disciple_lecture_next_due_hour += YuxuTrueDiscipleLecturePeriodHours;
        human.sect_true_disciple_lecture_consecutive_leave_count = 0;
        mark_save_dirty();
        refresh_yuxu_true_disciple_lecture_context(human, true);
        return true;
    }

    private void request_yuxu_true_disciple_lecture_leave(HumanData human)
    {
        var overview = GetNodeOrNull<Control>("SceneLayer/YuxuXianzongOverview");
        if (player is null || human.sect_affiliation_id != YuxuSectId || !is_yuxu_true_disciple(human.sect_current_rank) ||
            _player_current_yuxu_peak_id != "zifu" || overview?.Visible != true ||
            overview.GetNodeOrNull<Control>("PeakInterior")?.IsVisibleInTree() != true || _east_woods_dungeon?.Visible == true)
            return;
        sync_yuxu_true_disciple_lecture_schedule(human);
        if (human.sect_true_disciple_lecture_consecutive_leave_count >= 2)
        {
            show_message("暂不能续请", "连续两期请假已用尽；需先亲赴一期观复峰讲席，之后才能再次申请。 ");
            refresh_yuxu_true_disciple_lecture_context(human, true);
            return;
        }
        var time = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
        if (time is null || human.sect_true_disciple_lecture_next_due_hour < 0)
        {
            show_message("请假未受理", "游戏历法尚未就绪，暂不能登记讲席请假。 ");
            return;
        }

        var scheduledPeriod = human.sect_true_disciple_lecture_next_due_hour;
        var dialog = new ConfirmationDialog
        {
            Title = "真传讲席请假",
            DialogText = $"为当前或下一期观复峰讲席申请一次免赴，可用于长时间闭关或外出。此申请不消耗时间与资源，获准后本期不记缺席；最多连续请假两期。\n\n拟免赴期：{format_yuxu_lecture_date(scheduledPeriod)}开放的一期。",
            OkButtonText = "递交请假",
            CancelButtonText = "再考虑"
        };
        AddChild(dialog);
        dialog.PopupCentered();
        dialog.Confirmed += () =>
        {
            dialog.QueueFree();
            if (player?.entity_data is not HumanData current || current != human || current.sect_affiliation_id != YuxuSectId ||
                !is_yuxu_true_disciple(current.sect_current_rank) || _player_current_yuxu_peak_id != "zifu" ||
                overview?.Visible != true || overview.GetNodeOrNull<Control>("PeakInterior")?.IsVisibleInTree() != true)
                return;
            sync_yuxu_true_disciple_lecture_schedule(current);
            if (current.sect_true_disciple_lecture_next_due_hour != scheduledPeriod)
            {
                show_message("请假未受理", "讲席期次已变化，请查看最新安排后重新申请。 ");
                refresh_yuxu_true_disciple_lecture_context(current, true);
                return;
            }
            if (current.sect_true_disciple_lecture_consecutive_leave_count >= 2)
            {
                show_message("暂不能续请", "连续两期请假已用尽；需先亲赴一期观复峰讲席，之后才能再次申请。 ");
                return;
            }
            current.sect_true_disciple_lecture_last_leave_hour = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? -1;
            current.sect_true_disciple_lecture_consecutive_leave_count++;
            current.sect_true_disciple_lecture_next_due_hour += YuxuTrueDiscipleLecturePeriodHours;
            mark_save_dirty();
            refresh_yuxu_true_disciple_lecture_context(current, true);
            show_message("讲席请假已登记", "本期免赴，不记缺席；后续仍须按期履职，连续请假不得超过两期。 ");
        };
        dialog.Canceled += () => dialog.QueueFree();
    }

    private static bool is_supported_yuxu_duty(string dutyId)
        => dutyId is YuxuStudyDutyId or YuxuYaopuWatchDutyId or YuxuYaopuCultivationDutyId or YuxuEastWoodsPatrolDutyId or YuxuEastWoodsConflictReviewDutyId or YuxuCaseFieldTaskDutyId or YuxuTianwenPracticeDutyId or YuxuBeastBoneSubmissionDutyId or YuxuBitterMossRootDutyId;

    private static void clear_active_yuxu_duty(HumanData human)
    {
        human.sect_active_yuxu_duty_id = "";
        human.sect_active_yuxu_duty_instance_id = "";
        human.sect_active_yuxu_duty_incident_id = "";
        human.sect_active_yuxu_duty_task_id = "";
        human.sect_active_yuxu_duty_started_hour = -1;
        human.sect_active_yuxu_duty_stage = 0;
        human.sect_active_yuxu_duty_ready = false;
        human.sect_active_yuxu_inspiration_bonus = 0;
    }

    private void reconcile_yuxu_conflict_duty_state(HumanData human)
    {
        if (_npc_world is null) return;
        var changed = _npc_world.reconcile_east_woods_submissions(incidentId =>
        {
            var sourceId = $"yuxu:official:{YuxuEastWoodsConflictReviewDutyId}:{incidentId}";
            return human.sect_contribution_records.Any(record => record is not null && record.source_transaction_id == sourceId);
        });
        var activeCaseId = human.sect_active_yuxu_duty_id == YuxuEastWoodsConflictReviewDutyId
            ? human.sect_active_yuxu_duty_incident_id
            : "";
        var activeInstanceId = activeCaseId.Length > 0 ? human.sect_active_yuxu_duty_instance_id : "";
        changed |= _npc_world.reconcile_east_woods_assignments(activeCaseId, activeInstanceId);

        if (human.sect_active_yuxu_duty_id == YuxuEastWoodsConflictReviewDutyId)
        {
            var incident = _npc_world.get_east_woods_incident(human.sect_active_yuxu_duty_incident_id);
            if (incident is null || incident.case_status != EastWoodsNpcIncidentRecord.StatusAssigned ||
                incident.assigned_duty_instance_id != human.sect_active_yuxu_duty_instance_id)
            {
                clear_active_yuxu_duty(human);
                changed = true;
            }
        }
        else if (human.sect_active_yuxu_duty_id == YuxuCaseFieldTaskDutyId)
        {
            var task = _npc_world.get_yuxu_case_task(human.sect_active_yuxu_duty_incident_id, human.sect_active_yuxu_duty_task_id);
            if (task is null || task.status is not (YuxuSectCaseTaskRecord.StatusAssigned or YuxuSectCaseTaskRecord.StatusObjectiveComplete) ||
                task.duty_instance_id != human.sect_active_yuxu_duty_instance_id)
            {
                clear_active_yuxu_duty(human);
                changed = true;
            }
            else
            {
                var ready = task.status == YuxuSectCaseTaskRecord.StatusObjectiveComplete;
                if (human.sect_active_yuxu_duty_ready != ready)
                {
                    human.sect_active_yuxu_duty_ready = ready;
                    changed = true;
                }
            }
        }
        else if (!string.IsNullOrWhiteSpace(human.sect_active_yuxu_duty_incident_id))
        {
            human.sect_active_yuxu_duty_incident_id = "";
            changed = true;
        }
        if (changed) mark_save_dirty();
    }

    private Godot.Collections.Array<Godot.Collections.Dictionary> get_yuxu_conflict_case_summaries()
    {
        var summaries = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        if (_npc_world is null) return summaries;
        foreach (var incident in _npc_world.get_east_woods_incidents_for_review())
        {
            summaries.Add(new Godot.Collections.Dictionary
            {
                ["incident_id"] = incident.incident_id,
                ["occurred_at"] = incident.occurred_at,
                ["location_name"] = incident.location_name,
                ["route_node_id"] = incident.route_node_id,
                ["attacker_npc_id"] = incident.attacker_npc_id,
                ["attacker_npc_name"] = incident.attacker_npc_name,
                ["target_npc_id"] = incident.target_npc_id,
                ["target_npc_name"] = incident.target_npc_name,
                ["case_status"] = incident.case_status,
            });
        }
        return summaries;
    }

    private void handle_yuxu_yaopu_action(string actionId, HumanData human)
    {
        if (player is null) return;
        if (actionId == "yaopu_watch")
        {
            var activeWatchDuty = human.sect_active_yuxu_duty_id == YuxuYaopuWatchDutyId;
            var dutyInstanceId = activeWatchDuty ? human.sect_active_yuxu_duty_instance_id : "";
            var time = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
            var staminaCost = YuxuSectPrototypeBalance.YaopuWatchStaminaCost;
            if (time is null || !player.can_spend_world_stamina(staminaCost))
            {
                show_message("暂不能当值", time is null ? "游戏历法尚未就绪。" : $"本次值守需 {staminaCost:0.#} 点精力，当前精力不足；可休息后再来。");
                return;
            }

            run_time_transition(
                "药园值守",
                $"在瑶圃峰药园完成 {YuxuSectPrototypeBalance.YaopuWatchHours} 个游戏时辰的值守；消耗精力 {staminaCost:0.#} 点。",
                activeWatchDuty
                    ? "当值结束。药架、灵土与苗圃均已巡看完毕，可向执事回报。"
                    : "当值结束。药架、灵土与苗圃均已巡看完毕。",
                () =>
                {
                    if (player is null || player.entity_data is not HumanData current ||
                        (activeWatchDuty && (current.sect_active_yuxu_duty_id != YuxuYaopuWatchDutyId ||
                            current.sect_active_yuxu_duty_instance_id != dutyInstanceId)) ||
                        !player.try_spend_world_stamina(staminaCost))
                    {
                        show_message("值守未完成", "事务状态或精力已变化，未推进历法，也未发放贡献。");
                        return;
                    }
                    time.add_hours(YuxuSectPrototypeBalance.YaopuWatchHours);
                    if (!activeWatchDuty)
                    {
                        on_yuxu_daily_activity_completed($"sect:yaopu-watch:{time.get_total_game_hours()}");
                        mark_save_dirty();
                        show_message("当值结束", "本次瑶圃日常值守已完成；事务贡献仅在接取对应公事后结算。");
                        return;
                    }
                    settle_yuxu_official_duty(
                        current,
                        YuxuYaopuWatchDutyId,
                        "瑶圃药园值守",
                        dutyInstanceId,
                        $"完成药园值守；消耗精力 {staminaCost:0.#} 点并当值四个游戏时辰。",
                        "药园值守已核验。",
                        null);
                });
            return;
        }

        if (actionId != "yaopu_cultivate") return;
        var overview = GetNodeOrNull<Control>("SceneLayer/YuxuXianzongOverview");
        if (_player_current_yuxu_peak_id != "yaopu" || overview?.Visible != true ||
            overview.GetNodeOrNull<Control>("PeakInterior")?.IsVisibleInTree() != true || _east_woods_dungeon?.Visible == true)
            return;
        if (human.sect_active_yuxu_duty_id != YuxuYaopuCultivationDutyId)
        {
            handle_yuxu_free_yaopu_cultivation(human);
            return;
        }

        var cultivationTime = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
        if (cultivationTime is null)
        {
            show_message("培护未完成", "游戏历法尚未就绪。");
            return;
        }

        if (human.sect_active_yuxu_duty_stage <= 0 || human.sect_active_yuxu_duty_started_hour < 0)
        {
            var startHours = YuxuSectPrototypeBalance.YaopuCultivationStartHours;
            var startCost = YuxuSectPrototypeBalance.YaopuCultivationStartStaminaCost;
            if (!player.can_spend_world_stamina(startCost))
            {
                show_message("精力不足", $"照料苗株需 {startCost:0.#} 点精力，当前精力不足；可休息后再来。");
                return;
            }
            run_time_transition(
                "照料雾铃花",
                $"前往雾铃园培护一株幼苗，用时 {startHours} 个游戏时辰，消耗精力 {startCost:0.#} 点。成熟后再回来采收。",
                "幼苗已妥善照料。待其生长满一个游戏昼夜，再来采收。",
                () =>
                {
                    if (player is null || player.entity_data is not HumanData current ||
                        current.sect_active_yuxu_duty_id != YuxuYaopuCultivationDutyId ||
                        !player.try_spend_world_stamina(startCost))
                    {
                        show_message("照料未完成", "事务状态或精力已变化，未推进历法。");
                        return;
                    }
                    cultivationTime.add_hours(startHours);
                    current.sect_active_yuxu_duty_started_hour = cultivationTime.get_total_game_hours();
                    current.sect_active_yuxu_duty_stage = 1;
                    mark_save_dirty();
                    refresh_yuxu_life_context();
                });
            return;
        }

        var harvestReadyForSettlement = human.sect_active_yuxu_duty_stage >= 2;
        if (!harvestReadyForSettlement)
        {
            var elapsed = Math.Max(0, cultivationTime.get_total_game_hours() - human.sect_active_yuxu_duty_started_hour);
            var required = YuxuSectPrototypeBalance.YaopuCultivationHours;
            if (elapsed < required)
            {
                var remaining = required - elapsed;
                show_message("雾铃花尚未成熟", $"苗株已生长 {elapsed} / {required} 个游戏时辰；还需 {remaining} 个游戏时辰。你可以先处理其他事务，之后再回来。");
                return;
            }
        }

        var yieldCount = YuxuSectPrototypeBalance.YaopuCultivationYieldCount;
        var deliveryCount = YuxuSectPrototypeBalance.YaopuCultivationSectDeliveryCount;
        var keptCount = Math.Max(0, yieldCount - deliveryCount);
        if (!harvestReadyForSettlement && !player.add_item_to_inventory(MistBellFlowerItemPath, yieldCount))
        {
            show_message("行囊已满", $"雾铃花已成熟，但行囊无法容纳 {yieldCount} 株；整理行囊后再来采收。任务进度会保留。");
            return;
        }
        if (!harvestReadyForSettlement)
        {
            human.sect_active_yuxu_duty_stage = 2;
            mark_save_dirty();
            refresh_yuxu_life_context();
        }
        if (player.get_inventory_total_count(MistBellFlowerItemPath) < deliveryCount)
        {
            show_message("尚缺交付物", $"本次采收已记入事务进度；提交仍需雾铃花 ×{deliveryCount}，物品不足时可补足后再来。");
            return;
        }
        if (!player.remove_item_from_inventory(MistBellFlowerItemPath, deliveryCount))
        {
            show_message("交收未完成", "雾铃花交收数量发生变化；已采收物品留在行囊，事务进度保留，可稍后重试。");
            return;
        }
        if (!settle_yuxu_official_duty(
                human,
                YuxuYaopuCultivationDutyId,
                "培育雾铃花",
                human.sect_active_yuxu_duty_instance_id,
                $"培护并采收雾铃花 ×{yieldCount}；交宗门 ×{deliveryCount}，弟子自留 ×{keptCount}；等待二十四个游戏时辰。",
                $"雾铃花 ×{deliveryCount} 已交宗门，×{keptCount} 留归弟子。",
                rollback: () => player?.add_item_to_inventory_silently(MistBellFlowerItemPath, deliveryCount) ?? false,
                rewardOverride: YuxuSectPrototypeBalance.OfficialYaopuCultivationReward,
                itemMessage: $"雾铃花 ×{keptCount} 已留入行囊。 ",
                treasuryItemPath: MistBellFlowerItemPath,
                treasuryItemCount: deliveryCount))
            return;
    }

    private void handle_yuxu_free_yaopu_cultivation(HumanData human)
    {
        if (player is null) return;
        var time = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
        if (time is null)
        {
            show_message("培护未完成", "游戏历法尚未就绪。");
            return;
        }

        var stage = human.sect_yaopu_free_cultivation_stage;
        var startedHour = human.sect_yaopu_free_cultivation_started_hour;
        if (stage == 1 && startedHour >= 0)
        {
            var elapsed = Math.Max(0, time.get_total_game_hours() - startedHour);
            var required = YuxuSectPrototypeBalance.YaopuCultivationHours;
            if (elapsed < required)
            {
                var remaining = required - elapsed;
                show_message("雾铃花尚未成熟", $"苗株已生长 {elapsed} / {required} 个游戏时辰；还需 {remaining} 个游戏时辰。之后再回来采收。日常培护不需要另行接取公事。");
                return;
            }

            var yieldCount = YuxuSectPrototypeBalance.YaopuCultivationYieldCount;
            if (!player.add_item_to_inventory(MistBellFlowerItemPath, yieldCount))
            {
                show_message("行囊已满", $"雾铃花已成熟，但行囊无法容纳 {yieldCount} 株；整理行囊后再来采收，生长状态会保留。");
                return;
            }
            human.sect_yaopu_free_cultivation_stage = 2;
            mark_save_dirty();
            refresh_yuxu_life_context();
            show_message("采收雾铃花", $"雾铃花 ×{yieldCount} 已采收入行囊，归你所有。此次为峰内日常培护，不入宗门公库，也不结算贡献；再次培护须由你主动开始。");
            return;
        }

        var careHours = YuxuSectPrototypeBalance.YaopuCultivationStartHours;
        var staminaCost = YuxuSectPrototypeBalance.YaopuCultivationStartStaminaCost;
        if (!player.can_spend_world_stamina(staminaCost))
        {
            show_message("精力不足", $"照料苗株需 {staminaCost:0.#} 点精力，当前精力不足；可休息后再来。");
            return;
        }

        run_time_transition(
            "照料雾铃花",
            $"在瑶圃峰雾铃园培护一株幼苗，用时 {careHours} 个游戏时辰，消耗精力 {staminaCost:0.#} 点。成熟后再回来采收。",
            "幼苗已妥善照料。待其生长满一个游戏昼夜，再来采收。",
            () =>
            {
                if (player is null || player.entity_data is not HumanData current ||
                    current.sect_active_yuxu_duty_id == YuxuYaopuCultivationDutyId ||
                    current.sect_yaopu_free_cultivation_stage != stage ||
                    !player.try_spend_world_stamina(staminaCost))
                {
                    show_message("照料未完成", "事务状态或精力已变化，未推进历法。");
                    return;
                }
                time.add_hours(careHours);
                current.sect_yaopu_free_cultivation_started_hour = time.get_total_game_hours();
                current.sect_yaopu_free_cultivation_stage = 1;
                mark_save_dirty();
                refresh_yuxu_life_context();
                show_message("幼苗已照料", "已用时并消耗精力完成培护。等待一个游戏昼夜后，可回来采收归自己的雾铃花。");
            });
    }

    private void on_east_woods_route_node_entered(string nodeId)
    {
        if (_east_woods_dungeon?.is_dynamic_instance == true) return;
        if (player?.entity_data is not HumanData human || human.sect_affiliation_id != YuxuSectId ||
            human.sect_active_yuxu_duty_id != YuxuEastWoodsPatrolDutyId)
            return;
        var time = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
        if (time is null)
        {
            show_message("巡线暂停", "游戏历法尚未就绪，本次节点行进没有计入巡线进度。");
            return;
        }
        if (human.sect_active_yuxu_duty_ready) return;
        human.sect_active_yuxu_duty_stage++;
        mark_save_dirty();
        refresh_yuxu_life_context();
        if (nodeId != "quiet_grove") return;
        if (human.sect_active_yuxu_duty_stage < YuxuSectPrototypeBalance.EastWoodsPatrolRequiredNodeMoves)
        {
            show_message("巡线尚未完成", "已到静冠洼地，但标定路线尚未走完；继续按路线巡回后再回此处复核。");
            return;
        }
        human.sect_active_yuxu_duty_ready = true;
        mark_save_dirty();
        refresh_yuxu_life_context();
        _east_woods_dungeon?.show_action_result("巡查复核完成", "本次巡线未见需要处理的异常。记录已保留，请沿林径返回林缘，离开后前往和光峰宗门事务处回报；贡献尚未核发。");
    }

    private bool settle_yuxu_official_duty(
        HumanData human,
        string dutyId,
        string title,
        string transactionSuffix,
        string costSummary,
        string resultSummary,
        Func<bool>? rollback = null,
        int rewardOverride = 0,
        string itemMessage = "",
        string treasuryItemPath = "",
        int treasuryItemCount = 0)
    {
        bool try_rollback_consumed_items()
        {
            if (rollback is null || rollback()) return true;
            GD.PushError($"玉虚事务 {dutyId} / {transactionSuffix} 回滚行囊物品失败。");
            return false;
        }

        if (player is null)
        {
            try_rollback_consumed_items();
            return false;
        }
        if (human.sect_active_yuxu_duty_id != dutyId ||
            string.IsNullOrWhiteSpace(transactionSuffix) ||
            human.sect_active_yuxu_duty_instance_id != transactionSuffix)
        {
            try_rollback_consumed_items();
            return false;
        }
        if (treasuryItemCount < 0 ||
            (treasuryItemCount == 0) != string.IsNullOrWhiteSpace(treasuryItemPath))
        {
            try_rollback_consumed_items();
            show_message("事务未结算", "公库物资数量或物品定义无效；交付物已退回，未发放贡献。");
            return false;
        }
        var reward = rewardOverride > 0 ? rewardOverride : YuxuSectPrototypeBalance.OfficialYaopuDutyReward;
        if (CultivationInspirationSystem.Enabled) reward += Math.Max(0, human.sect_active_yuxu_inspiration_bonus);
        var sourceTransactionId = $"yuxu:official:{dutyId}:{transactionSuffix}";
        var record = new YuxuSectContributionRecord
        {
            record_id = Guid.NewGuid().ToString("N"),
            source_transaction_id = sourceTransactionId,
            transaction_kind = YuxuSectContributionRecord.KindOfficialDutyReward,
            source_title = $"宗门公事·{title}",
            occurred_at = get_yuxu_contribution_timestamp(),
            contribution_delta = reward,
            cost_summary = costSummary,
            result_summary = $"{resultSummary} 核发 {reward} 点贡献。"
        };

        var treasuryRecordAdded = false;
        if (treasuryItemCount > 0)
        {
            var treasuryRecord = new YuxuSectTreasuryRecord
            {
                record_id = Guid.NewGuid().ToString("N"),
                source_transaction_id = sourceTransactionId,
                source_duty_id = dutyId,
                movement_kind = YuxuSectTreasuryRecord.KindDeposit,
                item_path = treasuryItemPath,
                quantity = treasuryItemCount,
                source_title = $"宗门公事·{title}",
                occurred_at = record.occurred_at,
            };
            if (!_yuxu_sect_treasury_ledger.try_record_movement(treasuryRecord))
            {
                try_rollback_consumed_items();
                show_message("事务未结算", "公库入库流水无法登记；交付物已退回，未发放贡献。可检查事务状态后再试。");
                return false;
            }
            treasuryRecordAdded = true;
        }
        if (!human.record_yuxu_contribution(record))
        {
            if (treasuryRecordAdded)
                _yuxu_sect_treasury_ledger.remove_record_by_source_transaction(sourceTransactionId);
            try_rollback_consumed_items();
            show_message("事务未结算", "公事回执/贡献流水无法登记；公库账已回退，交付物已退回，未发放贡献。可检查事务状态后再试。");
            return false;
        }
        on_yuxu_official_duty_settled(human, record.source_transaction_id, record.source_title);
        clear_active_yuxu_duty(human);
        mark_save_dirty();
        refresh_yuxu_life_context();
        var itemText = itemMessage;
        show_message("事务完成", $"{itemText}{resultSummary}宗门核发 {reward} 点贡献，已记入个人档案。");
        return true;
    }

    private void update_yuxu_monthly_allowance()
    {
        if (player?.entity_data is not HumanData human || human.sect_affiliation_id != YuxuSectId)
            return;
        var time = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
        if (time is null) return;

        var currentMonth = get_game_month_index(time);
        var previousMonthCursor = human.sect_allowance_month_cursor;
        var previousBalance = human.sect_unclaimed_spirit_stones;
        if (human.sect_allowance_month_cursor < 0 || currentMonth < human.sect_allowance_month_cursor)
        {
            // 旧记录首次接入时不追溯发放；只从当前游戏月开始累计。
            human.sect_allowance_month_cursor = currentMonth;
        }
        else if (currentMonth > human.sect_allowance_month_cursor)
        {
            var elapsedMonths = (long)currentMonth - human.sect_allowance_month_cursor;
            var accrued = elapsedMonths * get_yuxu_monthly_stipend(human.sect_current_rank);
            human.sect_unclaimed_spirit_stones = (int)Math.Min(int.MaxValue, (long)Math.Max(0, human.sect_unclaimed_spirit_stones) + accrued);
            human.sect_allowance_month_cursor = currentMonth;
        }
        if (previousMonthCursor != human.sect_allowance_month_cursor || previousBalance != human.sect_unclaimed_spirit_stones)
        {
            mark_save_dirty();
            refresh_yuxu_life_context();
        }
    }

    private void on_yuxu_life_action_requested(string actionId, string peakId)
    {
        if (player?.entity_data is not HumanData human || human.sect_affiliation_id != YuxuSectId ||
            !is_supported_yuxu_life_rank(human.sect_current_rank))
            return;

        if (peakId == "xuanlan" && actionId == "lineage_book_selection")
        {
            request_yuxu_lineage_book_choice(human);
            return;
        }

        if (peakId == "guanfu" && actionId == "lineage_lecture_checkin")
        {
            open_yuxu_true_disciple_lecture(human);
            return;
        }

        if (peakId == "xuanlan" && actionId == "xuanlan_study")
        {
            var overview = GetNodeOrNull<Control>("SceneLayer/YuxuXianzongOverview");
            if (_player_current_yuxu_peak_id != "xuanlan" || overview?.Visible != true ||
                overview.GetNodeOrNull<Control>("PeakInterior")?.Visible != true)
                return;
            if (human.sect_active_yuxu_duty_id == YuxuStudyDutyId)
            {
                show_message("静室研读事务", "这项公事须在身份对应的观复峰或紫府峰静室完成；玄览峰研习不会结算或替代该事务。");
                return;
            }
            if (is_yuxu_true_disciple(human.sect_current_rank) &&
                !string.IsNullOrWhiteSpace(human.sect_lineage_gongfa_path) &&
                !human.sect_lineage_reported_first_volume)
            {
                show_message("师门首卷", "真传首卷仍按师承流程回紫府峰静室研读，并在完成后向带教师兄/师姐汇报。玄览峰可以选书；首卷汇报后可在此研习其他已开放典籍。");
                return;
            }
            cultivation_ui?.show_teaching_for(player);
            return;
        }

        if (peakId == "zifu" && actionId == "lineage_choose_teacher")
        {
            request_yuxu_lineage_teacher_choice(human);
            return;
        }

        if (peakId == "zifu" && actionId == "lineage_report")
        {
            report_yuxu_lineage_first_volume(human);
            return;
        }

        if (peakId == "zifu" && actionId == "lineage_lecture_leave_request")
        {
            request_yuxu_true_disciple_lecture_leave(human);
            return;
        }

        if (peakId == "zhizhong" && actionId == "zhizhong_case_review")
        {
            show_yuxu_conflict_review_dialog(human);
            return;
        }

        if (peakId == "zhizhong" && actionId is "zhizhong_case_docket" or "zhizhong_case_enforce" or "zhizhong_case_handoff")
        {
            on_yuxu_case_docket_action(actionId, human);
            return;
        }

        if (peakId == "heguang")
        {
            if (actionId == "heguang_banquet")
            {
                open_banquet_setup();
                return;
            }
            handle_yuxu_heguang_action(actionId, human);
            return;
        }

        if (peakId == "yaopu" && (actionId is "yaopu_watch" or "yaopu_cultivate"))
        {
            handle_yuxu_yaopu_action(actionId, human);
            return;
        }

        if (peakId == "tianwen" && actionId == "tianwen_practice")
        {
            request_yuxu_tianwen_practice();
            return;
        }

        if (peakId == "tiangong" && actionId is "tiangong_alchemy" or "tiangong_forging")
        {
            open_yuxu_crafting(actionId);
            return;
        }

        if (peakId == "tiangong" && actionId == "tiangong_baigong")
        {
            var overview = GetNodeOrNull<Control>("SceneLayer/YuxuXianzongOverview");
            if (_player_current_yuxu_peak_id != "tiangong" || overview?.Visible != true ||
                overview.GetNodeOrNull<Control>("PeakInterior")?.Visible != true)
                return;
            if (baigong is not null && player is not null)
                baigong.show_for(player);
            return;
        }

        var isTrueDisciple = is_yuxu_true_disciple(human.sect_current_rank);
        var correctPeak = isTrueDisciple ? "zifu" : "guanfu";
        if (peakId != correctPeak) return;

        update_yuxu_monthly_allowance();
        switch (actionId)
        {
            case "meditation":
                cultivation_ui?.show_entity(player);
                break;
            case "rest":
                request_yuxu_rest(isTrueDisciple ? "紫府峰居所" : "观复峰居所");
                break;
            case "teaching":
                cultivation_ui?.show_teaching_for(player);
                break;
            case "claim_allowance":
                claim_yuxu_monthly_allowance(human);
                break;
            case "promotion":
                if (peakId == "guanfu") request_yuxu_rank_promotion(human);
                break;
        }
    }

    private void handle_yuxu_heguang_peer_qa(HumanData human)
    {
        var overview = GetNodeOrNull<Control>("SceneLayer/YuxuXianzongOverview");
        var peakInterior = overview?.GetNodeOrNull<Control>("PeakInterior");
        var manager = _npc_world ?? GetNodeOrNull<NpcWorldManager>("/root/NpcWorldManager");
        var time = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
        if (player is null || story_dialogue is null || manager is null || time is null ||
            human.sect_affiliation_id != YuxuSectId || !is_supported_yuxu_life_rank(human.sect_current_rank) ||
            _player_current_yuxu_peak_id != "heguang" || overview?.Visible != true || peakInterior?.IsVisibleInTree() != true ||
            _east_woods_dungeon?.Visible == true)
            return;

        var peer = manager.get_subscene_roster("yuxu_xianzong_core", "heguang")
            .Where(npc => npc.faction_id == YuxuSectId && !npc.is_yuxu_lineage_staff &&
                is_supported_yuxu_life_rank(npc.formal_identity))
            .OrderBy(npc => npc.npc_id, StringComparer.Ordinal)
            .FirstOrDefault();
        if (peer is null)
        {
            show_message("同门答疑", "和光峰此刻没有在场的同门可供请教。稍后再来，不消耗精力。");
            return;
        }

        info?.hide_panel();
        npc_interactions?.close();
        var cost = WorldStaminaRules.ConversationCost;
        void Answer(bool ask)
        {
            if (!ask)
            {
                story_dialogue.show_social_result(peer, new("你决定改日再来请教。", "", []) { ActionName = "暂缓答疑" });
                return;
            }
            if (player?.entity_data is not HumanData current || current.sect_affiliation_id != YuxuSectId ||
                !is_supported_yuxu_life_rank(current.sect_current_rank) || _player_current_yuxu_peak_id != "heguang" ||
                overview.Visible != true || peakInterior.IsVisibleInTree() != true || !can_contact_social_npc(peer) ||
                manager.get_npc(peer.npc_id) != peer)
            {
                story_dialogue.show_social_result(peer, new("同门已不在和光峰，或你已离开此处；本次没有消耗精力，也未记下答疑。", "", []) { ActionName = "答疑未成" });
                return;
            }
            if (!player.can_spend_world_stamina(cost) || !player.try_spend_world_stamina(cost))
            {
                story_dialogue.show_social_result(peer, new("你有些疲惫，便把请教留待改日。", "", []) { ActionName = "精力不足" });
                return;
            }

            var monthIndex = get_game_month_index(time);
            var dateLabel = $"{time.year}年{time.month}月{time.day}日";
            const string outcome = "你们就宗门日常与修行中的疑问作了交流；此次答疑不改变同门关系，也不结算贡献。";
            peer.record(new NpcActionRecord
            {
                month_index = monthIndex,
                date_label = dateLabel,
                action_id = "heguang_peer_qa",
                action_name = "同门答疑",
                node_id = peer.current_node_id,
                subscene_id = peer.current_subscene_id,
                target_npc_id = "player",
                reason = "玩家在和光峰主动请教同门，交流已实际完成。",
                outcome = outcome,
            });
            mark_save_dirty();
            story_dialogue.show_social_result(peer, new(outcome, "", ["你们围绕宗门日常与修行中的疑问交流了一番。"])
            {
                StaminaSpent = cost,
                ActionName = "同门答疑",
            });
        }

        story_dialogue.show_social_choices(player, peer,
            $"你向{peer.npc_name}请教宗门日常。交流需消耗精力 {cost:0.#} 点。",
            new (string, Action)[]
            {
                ($"请教宗门日常 · 精力 {cost:0.#}", () => Answer(true)),
                ("改日再问", () => Answer(false)),
            },
            () => Answer(false), "同门答疑", "右键改日再问");
    }

    private void handle_yuxu_heguang_action(string actionId, HumanData human)
    {
        if (actionId == "heguang_peer_qa")
        {
            handle_yuxu_heguang_peer_qa(human);
            return;
        }
        if (handle_yuxu_case_task_heguang_action(actionId, human)) return;
        const string submitDutyPrefix = "heguang_submit_duty:";
        if (actionId.StartsWith(submitDutyPrefix, StringComparison.Ordinal))
        {
            var dutyId = actionId[submitDutyPrefix.Length..];
            if (human.sect_affiliation_id != YuxuSectId || _player_current_yuxu_peak_id != "heguang" || _east_woods_dungeon?.Visible == true) return;
            if (dutyId == YuxuEastWoodsPatrolDutyId)
            {
                if (human.sect_active_yuxu_duty_id != dutyId || !human.sect_active_yuxu_duty_ready) return;
                settle_yuxu_official_duty(human, dutyId, "东林地巡查", human.sect_active_yuxu_duty_instance_id,
                    $"沿标定路线推进 {human.sect_active_yuxu_duty_stage} 个节点并在静冠洼地复核；返回和光峰回报，无物品消耗。", "巡线记录已收讫，沿途未见异常。");
                return;
            }
            if (dutyId is not (YuxuBeastBoneSubmissionDutyId or YuxuBitterMossRootDutyId) || human.sect_active_yuxu_duty_id != dutyId) return;
            var isRoot = dutyId == YuxuBitterMossRootDutyId;
            var itemPath = isRoot ? BitterMossRootItemPath : BeastBoneItemPath;
            var itemName = isRoot ? "苦苔根" : "兽骨";
            var quantity = isRoot ? YuxuSectPrototypeBalance.BitterMossRootSubmissionCount : 1;
            if (player is null || player.get_inventory_total_count(itemPath) < quantity)
            {
                show_message("尚缺交付物", $"需携带{itemName} ×{quantity}，回和光峰提交；已有存货也可交付。");
                return;
            }
            if (!player.remove_item_from_inventory(itemPath, quantity))
            {
                show_message("提交未完成", $"{itemName}数量发生变化，未扣除物品或发放贡献。");
                return;
            }
            settle_yuxu_official_duty(
                human,
                dutyId,
                isRoot ? "苦苔根交付" : "上缴兽骨",
                human.sect_active_yuxu_duty_instance_id,
                $"提交{itemName} ×{quantity}，由宗门登记。",
                $"{itemName}已收讫并完成登记。",
                rollback: () => player?.add_item_to_inventory_silently(itemPath, quantity) ?? false,
                rewardOverride: isRoot ? YuxuSectPrototypeBalance.OfficialBitterMossRootSubmissionReward : YuxuSectPrototypeBalance.OfficialBeastDropSubmissionReward,
                itemMessage: $"{itemName} ×{quantity} 已交宗门。 ",
                treasuryItemPath: isRoot ? "" : itemPath,
                treasuryItemCount: isRoot ? 0 : quantity);
            return;
        }

        const string acceptDutyPrefix = "heguang_accept_duty:";
        if (actionId.StartsWith(acceptDutyPrefix, StringComparison.Ordinal))
        {
            var dutyId = actionId[acceptDutyPrefix.Length..];
            var incidentId = "";
            var incidentDutyPrefix = YuxuEastWoodsConflictReviewDutyId + ":";
            if (dutyId.StartsWith(incidentDutyPrefix, StringComparison.Ordinal))
            {
                incidentId = dutyId[incidentDutyPrefix.Length..];
                dutyId = YuxuEastWoodsConflictReviewDutyId;
            }
            if (!is_supported_yuxu_duty(dutyId)) return;
            if (dutyId == YuxuEastWoodsConflictReviewDutyId)
            {
                var incident = _npc_world?.get_east_woods_incident(incidentId);
                if (string.IsNullOrWhiteSpace(incidentId) || incident is null || incident.case_status != EastWoodsNpcIncidentRecord.StatusAvailable)
                {
                    refresh_yuxu_life_context();
                    show_message("案件已不可接取", "该事件已被接取、上呈或不再存在；事务板已更新，未产生贡献流水。");
                    return;
                }
            }
            if (dutyId == YuxuStudyDutyId && !has_available_yuxu_study_chapter())
            {
                refresh_yuxu_life_context();
                show_message("暂不可接", "你目前没有符合条件、尚未研读的秘籍章节。取得或安排可研读的秘籍后，再来承接这项事务。");
                return;
            }
            if (dutyId == YuxuYaopuCultivationDutyId && human.sect_yaopu_free_cultivation_stage == 1)
            {
                refresh_yuxu_life_context();
                show_message("雾铃花仍在生长", "先回瑶圃峰采收当前这株自养幼苗，再接取宗门培育公事；峰内收获不能代替公事的独立照料与等待。");
                return;
            }
            if (!string.IsNullOrWhiteSpace(human.sect_active_yuxu_duty_id) && human.sect_active_yuxu_duty_id != dutyId)
            {
                refresh_yuxu_life_context();
                show_message("宗门事务", "你已有一项事务在办；可先打开该事务并取消接取。");
                return;
            }
            if (!string.IsNullOrWhiteSpace(human.sect_active_yuxu_duty_id))
            {
                refresh_yuxu_life_context();
                return;
            }
            var dutyInstanceId = Guid.NewGuid().ToString("N");
            if (dutyId == YuxuEastWoodsConflictReviewDutyId &&
                _npc_world?.try_assign_east_woods_incident(incidentId, dutyInstanceId) != true)
            {
                refresh_yuxu_life_context();
                show_message("案件已不可接取", "此事件已不在可接取状态；没有变更人物关系或发放贡献。");
                return;
            }
            human.sect_active_yuxu_duty_id = dutyId;
            human.sect_active_yuxu_duty_instance_id = dutyInstanceId;
            human.sect_active_yuxu_duty_incident_id = incidentId;
            human.sect_active_yuxu_duty_started_hour = -1;
            human.sect_active_yuxu_duty_stage = 0;
            human.sect_active_yuxu_duty_ready = false;
            on_yuxu_official_duty_started(human, dutyId, dutyInstanceId);
            mark_save_dirty();
            refresh_yuxu_life_context();
            show_message("接取事务", dutyId switch
            {
                YuxuStudyDutyId => "已接取静室研习。按弟子身份前往所属静室，完成一卷当前可研读的秘籍即可交由宗门核验；可随时回和光峰取消。",
                YuxuYaopuWatchDutyId => "已接取药园值守。前往瑶圃峰，在药园当值四个游戏时辰；完成后核发贡献。",
                YuxuYaopuCultivationDutyId => "已接取雾铃花培育。前往瑶圃峰照料苗株，等待一个游戏昼夜后采收；四株中三株交宗门，一株自留。",
                YuxuEastWoodsPatrolDutyId => "已接取东林地巡查。进入东林地副本，沿路线至少推进四个节点，在静冠洼地复核后回和光峰回报；节点耗时按当前遁速动态结算，不要求探查、采集或杀怪。",
                YuxuEastWoodsConflictReviewDutyId => $"已接取东林地冲突核验，案卷 ID：{incidentId}。此事务固定关联该事件；前往执中峰查看结构化来源事实，核验后上呈。",
                YuxuTianwenPracticeDutyId => $"已接取天问峰演法公事。前往天问峰完成一场现有林地凶狼演练并回传结果；不要求获胜，结果回传后核发 {YuxuTianwenPracticeDutyReward} 点贡献，不掉落物品。",
                YuxuBeastBoneSubmissionDutyId => "已接取兽骨上缴。携带一份现有妖兽掉落物兽骨，回到此处打开详情并提交。",
                YuxuBitterMossRootDutyId => "已接取苦苔根交付。可在东林地林地、阴湿地带探查采集；携带苦苔根两份回和光峰提交，奖励5贡献。已有存货也可交付。",
                _ => "已接取宗门事务。"
            });
            return;
        }

        const string cancelDutyPrefix = "heguang_cancel_duty:";
        if (actionId.StartsWith(cancelDutyPrefix, StringComparison.Ordinal))
        {
            var dutyId = actionId[cancelDutyPrefix.Length..];
            var incidentId = "";
            var incidentDutyPrefix = YuxuEastWoodsConflictReviewDutyId + ":";
            if (dutyId.StartsWith(incidentDutyPrefix, StringComparison.Ordinal))
            {
                incidentId = dutyId[incidentDutyPrefix.Length..];
                dutyId = YuxuEastWoodsConflictReviewDutyId;
            }
            if (human.sect_active_yuxu_duty_id != dutyId) return;
            if (dutyId == YuxuEastWoodsConflictReviewDutyId)
            {
                if (human.sect_active_yuxu_duty_incident_id != incidentId ||
                    _npc_world?.release_east_woods_incident(incidentId, human.sect_active_yuxu_duty_instance_id) != true)
                {
                    refresh_yuxu_life_context();
                    return;
                }
            }
            clear_active_yuxu_duty(human);
            mark_save_dirty();
            refresh_yuxu_life_context();
            show_message("取消接取", "已取消这项事务。贡献和物品均未发生变化。");
            return;
        }
    }

    private void show_yuxu_conflict_review_dialog(HumanData human)
    {
        var overview = GetNodeOrNull<Control>("SceneLayer/YuxuXianzongOverview");
        var interior = overview?.GetNodeOrNull<Control>("PeakInterior");
        var incidentId = human.sect_active_yuxu_duty_incident_id;
        var incident = _npc_world?.get_east_woods_incident(incidentId);
        if (_player_current_yuxu_peak_id != "zhizhong" || overview?.Visible != true || interior?.Visible != true ||
            human.sect_active_yuxu_duty_id != YuxuEastWoodsConflictReviewDutyId || incident is null ||
            incident.case_status != EastWoodsNpcIncidentRecord.StatusAssigned ||
            incident.assigned_duty_instance_id != human.sect_active_yuxu_duty_instance_id)
        {
            show_message("暂无待核验案卷", "请先在和光峰事务板接取一件仍可核验的东林地 NPC 冲突，再到执中峰查看来源事实。");
            return;
        }

        var dutyInstanceId = human.sect_active_yuxu_duty_instance_id;
        var dialog = new ConfirmationDialog
        {
            Title = "执中峰 · 案件核验",
            DialogText = format_yuxu_conflict_incident_facts(incident),
            OkButtonText = "核验并上呈",
            CancelButtonText = "返回案卷",
            MinSize = new Vector2I(960, 560),
        };
        dialog.Confirmed += () =>
        {
            submit_yuxu_conflict_incident(human, incidentId, dutyInstanceId);
            dialog.QueueFree();
        };
        dialog.Canceled += () => dialog.QueueFree();
        GetNode<CanvasLayer>("CanvasLayer").AddChild(dialog);
        dialog.PopupCentered();
    }

    private static string format_yuxu_conflict_incident_facts(EastWoodsNpcIncidentRecord incident)
    {
        var result = incident.battle_result switch
        {
            EastWoodsNpcIncidentRecord.ResultAttackerWon => "发起方战胜",
            EastWoodsNpcIncidentRecord.ResultTargetWon => "被袭方战胜",
            _ => "未分胜负",
        };
        var attackerPath = $"{incident.attacker_previous_node_id} → {incident.attacker_node_id}";
        var targetPath = $"{incident.target_previous_node_id} → {incident.target_node_id}";
        var lines = new System.Collections.Generic.List<string>
        {
            $"案卷 ID：{incident.incident_id}",
            $"发生日期：{incident.occurred_at}",
            $"地点：{incident.location_name} · 路线节点 {incident.route_node_id}",
            $"发起方：{incident.attacker_npc_name}（NPC ID：{incident.attacker_npc_id}；路线 {attackerPath}）",
            $"被袭方：{incident.target_npc_name}（NPC ID：{incident.target_npc_id}；路线 {targetPath}）",
            $"战斗结果：{result}",
            $"死亡事实：发起方{(incident.attacker_died ? "死亡" : "未死亡")}；被袭方{(incident.target_died ? "死亡" : "未死亡")}",
            "实际物品转移：",
        };
        if (incident.item_transfers is null || incident.item_transfers.Count == 0)
        {
            lines.Add("未发生实际物品转移。");
        }
        else
        {
            foreach (var transfer in incident.item_transfers)
            {
                if (transfer is null || transfer.quantity <= 0) continue;
                var itemName = ResourceLoader.Load<ItemData>(transfer.item_path)?.entity_name ?? "物品";
                lines.Add($"{itemName}（ItemData：{transfer.item_path}）×{transfer.quantity}；{transfer.from_npc_name}（{transfer.from_npc_id}）→ {transfer.to_npc_name}（{transfer.to_npc_id}）");
            }
        }
        lines.Add("仅核对以上来源事实。确认后将上呈执中峰并结束本事务；本次不作责任判断、惩戒或关系处置。上呈状态为“待裁断”。");
        return string.Join("\n", lines);
    }

    private void submit_yuxu_conflict_incident(HumanData human, string incidentId, string dutyInstanceId)
    {
        var overview = GetNodeOrNull<Control>("SceneLayer/YuxuXianzongOverview");
        var interior = overview?.GetNodeOrNull<Control>("PeakInterior");
        var incident = _npc_world?.get_east_woods_incident(incidentId);
        if (player?.entity_data != human || human.sect_affiliation_id != YuxuSectId ||
            _player_current_yuxu_peak_id != "zhizhong" || overview?.Visible != true || interior?.Visible != true ||
            human.sect_active_yuxu_duty_id != YuxuEastWoodsConflictReviewDutyId ||
            human.sect_active_yuxu_duty_incident_id != incidentId ||
            human.sect_active_yuxu_duty_instance_id != dutyInstanceId || incident is null ||
            incident.case_status != EastWoodsNpcIncidentRecord.StatusAssigned ||
            incident.assigned_duty_instance_id != dutyInstanceId || _npc_world is null)
        {
            show_message("案卷状态已变化", "当前人物、接取事务或案卷状态已改变；没有结算贡献或更改案卷状态。");
            refresh_yuxu_life_context();
            return;
        }

        var transactionId = $"yuxu:official:{YuxuEastWoodsConflictReviewDutyId}:{incident.incident_id}";
        var record = new YuxuSectContributionRecord
        {
            record_id = Guid.NewGuid().ToString("N"),
            source_transaction_id = transactionId,
            transaction_kind = YuxuSectContributionRecord.KindOfficialDutyReward,
            source_title = "宗门公事·东林地冲突事实核验与上呈",
            occurred_at = get_yuxu_contribution_timestamp(),
            contribution_delta = YuxuSectPrototypeBalance.OfficialConflictVerificationReward,
            cost_summary = $"核验并上呈东林地冲突案卷 {incident.incident_id} 的结构化来源事实；未消耗物品。",
            result_summary = "来源事实已核验并上呈执中峰；状态为已上呈、待裁断，不作责任裁定。"
        };
        if (!human.record_yuxu_contribution(record))
        {
            show_message("案卷未结算", "贡献流水未能登记；案卷仍保留原接取状态，没有重复发放贡献。");
            refresh_yuxu_life_context();
            return;
        }
        if (!_npc_world.try_submit_east_woods_incident(incident.incident_id, dutyInstanceId, record.occurred_at))
        {
            if (!human.remove_yuxu_contribution(record.record_id))
                GD.PushError($"执中峰案卷 {incident.incident_id} 上呈状态冲突后，无法回退尚未提交的贡献记录。");
            show_message("案卷状态已变化", "案卷上呈状态未能登记，贡献流水已回退；请重新核对案卷状态。");
            mark_save_dirty();
            refresh_yuxu_life_context();
            return;
        }

        clear_active_yuxu_duty(human);
        mark_save_dirty();
        refresh_yuxu_life_context();
        show_message("案卷已上呈", $"东林地案卷 {incident.incident_id} 已上呈执中峰，当前状态为“已上呈·待裁断”。本期不处理最终裁断、申诉或责任后果；宗门核发 {YuxuSectPrototypeBalance.OfficialConflictVerificationReward} 点贡献。");
    }

    private bool has_available_yuxu_study_chapter()
    {
        if (player is null) return false;
        var books = player.get_known_gongfas();
        var currentBook = player.get_current_study_gongfa();
        if (currentBook is not null && !books.Any(book => book.ResourcePath == currentBook.ResourcePath))
            books.Add(currentBook);

        foreach (var book in books)
        {
            if (book is null) continue;
            foreach (var chapter in book.get_sorted_chapters())
            {
                if (chapter is null || player.is_gongfa_chapter_learned(book, chapter.get_id())) continue;
                if (player.can_learn_gongfa_chapter(book, chapter)) return true;
            }
        }
        return false;
    }

    private void on_yuxu_gongfa_chapter_completed(Character character, string bookName, string chapterId, string chapterName, Godot.Collections.Dictionary _)
    {
        if (character != player || player?.entity_data is not HumanData human || human.sect_affiliation_id != YuxuSectId)
            return;

        var bookPath = player.get_known_gongfas()
            .FirstOrDefault(book => book is not null && book.gongfa_name == bookName)?.ResourcePath ?? bookName;
        var lineageBook = string.IsNullOrWhiteSpace(human.sect_lineage_gongfa_path)
            ? null
            : ResourceLoader.Load<GongfaResource>(human.sect_lineage_gongfa_path);
        var lineageFirstChapter = lineageBook?.get_sorted_chapters().FirstOrDefault();
        if (!human.sect_lineage_reported_first_volume && lineageBook is not null && lineageFirstChapter is not null &&
            bookPath == human.sect_lineage_gongfa_path && chapterId == lineageFirstChapter.get_id())
        {
            mark_save_dirty();
            refresh_yuxu_life_context();
            show_message("师门首卷已读成", $"《{bookName}》第一卷已经完成。回紫府峰向带教师兄/师姐汇报；本次研读仍按原秘籍规则结算。 ");
        }

        if (human.sect_active_yuxu_duty_id != YuxuStudyDutyId) return;

        var expectedPeak = is_yuxu_true_disciple(human.sect_current_rank) ? "zifu" : "guanfu";
        var overview = GetNodeOrNull<Control>("SceneLayer/YuxuXianzongOverview");
        if (_player_current_yuxu_peak_id != expectedPeak || overview?.Visible != true ||
            overview.GetNodeOrNull<Control>("PeakInterior")?.Visible != true)
            return;

        var transactionId = $"yuxu:official:{YuxuStudyDutyId}:{bookPath}:{chapterId}";
        var record = new YuxuSectContributionRecord
        {
            record_id = Guid.NewGuid().ToString("N"),
            source_transaction_id = transactionId,
            transaction_kind = YuxuSectContributionRecord.KindOfficialDutyReward,
            source_title = $"宗门公事·静室研习《{bookName}》·{chapterName}",
            occurred_at = get_yuxu_contribution_timestamp(),
            contribution_delta = YuxuStudyDutyContributionReward,
            cost_summary = "未消耗物品；完成一卷静室研读。",
            result_summary = $"研读完成并经宗门事务核验，核发{YuxuStudyDutyContributionReward}点贡献。"
        };

        clear_active_yuxu_duty(human);
        if (!human.record_yuxu_contribution(record))
        {
            mark_save_dirty();
            refresh_yuxu_life_context();
            show_message("事务已核验", "这卷研读对应的贡献流水已经存在，本次不会重复发放。");
            return;
        }

        mark_save_dirty();
        refresh_yuxu_life_context();
        show_message("事务完成", $"《{bookName}·{chapterName}》研读完成，宗门核发 {YuxuStudyDutyContributionReward} 点贡献。贡献已记入个人档案。");
    }

    private static string get_yuxu_contribution_timestamp()
    {
        if (Engine.GetMainLoop() is SceneTree tree && tree.Root.GetNodeOrNull<GameTimeMgr>("GameTimeManager") is { } time)
            return $"{time.year}年{time.month}月{time.day}日";
        return "游戏时间未载";
    }

    private void request_yuxu_rest(string restPlace)
        => open_general_rest_dialog(restPlace, update_yuxu_monthly_allowance);

    private void claim_yuxu_monthly_allowance(HumanData human)
    {
        var amount = Math.Max(0, human.sect_unclaimed_spirit_stones);
        if (amount <= 0)
        {
            show_message("月例", "当前没有尚未领取的月例。月例按月累积，未领部分不会清空，也不生利息。");
            return;
        }
        if (player is null || !player.add_item_to_inventory(SpiritStoneItemPath, amount))
        {
            show_message("行囊已满", "灵石未能放入行囊，未领取月例仍完整保留。");
            return;
        }
        human.sect_unclaimed_spirit_stones = 0;
        mark_save_dirty();
        refresh_yuxu_life_context();
        show_message("领取月例", $"你领取了 {amount} 枚灵石。灵石已放入行囊，可用于交易。");
    }

    private void request_yuxu_rank_promotion(HumanData human)
    {
        var rule = YuxuSectPrototypeBalance.promotion_rule(human.sect_current_rank);
        if (rule is null)
        {
            show_message("身份晋升", "当前身份没有开放的晋升路径。真传只由天灵根入门取得，不可通过晋升成为。");
            return;
        }

        var service = human.get_yuxu_official_service_total();
        var realmManager = GetNodeOrNull<RealmManager>("/root/RealmManager");
        var realmReady = realmManager?.is_realm_after_or_equal(player?.cultivation_realm ?? "", rule.RequiredRealm) == true;
        var serviceReady = service >= rule.RequiredOfficialService;
        var progress = $"当前身份：{human.sect_current_rank}　→　晋升后：{rule.TargetRank}\n" +
            $"境界：{player?.cultivation_realm ?? "未详"} / 需达到 {rule.RequiredRealm}\n" +
            $"累计公事履职贡献：{service} / {rule.RequiredOfficialService}\n\n" +
            "晋升只核验境界与已完成宗门公事的累计履职记录；兑换花费不会减少履职累计。以上门槛为首轮测试值。";
        if (!realmReady || !serviceReady)
        {
            show_message("晋升条件", progress + "\n\n条件尚未齐备，可继续修行并完成宗门事务。");
            return;
        }

        var dialog = new ConfirmationDialog
        {
            Title = "申请晋升",
            DialogText = progress + "\n\n是否向观复峰登记晋升？",
            OkButtonText = "确认申请",
            CancelButtonText = "暂不申请"
        };
        GetNode<CanvasLayer>("CanvasLayer").AddChild(dialog);
        dialog.Confirmed += () =>
        {
            dialog.GetParent()?.RemoveChild(dialog);
            dialog.QueueFree();
            if (player?.entity_data is not HumanData current || current.sect_affiliation_id != YuxuSectId ||
                current.sect_current_rank != human.sect_current_rank)
                return;

            var currentRule = YuxuSectPrototypeBalance.promotion_rule(current.sect_current_rank);
            var currentRealmManager = GetNodeOrNull<RealmManager>("/root/RealmManager");
            if (currentRule is null || currentRule.TargetRank != rule.TargetRank ||
                currentRealmManager?.is_realm_after_or_equal(player.cultivation_realm, currentRule.RequiredRealm) != true ||
                current.get_yuxu_official_service_total() < currentRule.RequiredOfficialService)
            {
                show_message("晋升未完成", "确认时晋升条件已发生变化，请重新查看当前要求。");
                return;
            }

            update_yuxu_monthly_allowance();
            current.sect_current_rank = currentRule.TargetRank;
            mark_save_dirty();
            refresh_yuxu_life_context();
            show_message("晋升登记", $"观复峰已登记你的新身份：{currentRule.TargetRank}。入门起始身份与既有履职记录均予保留。");
        };
        dialog.Canceled += () => dialog.QueueFree();
        dialog.PopupCentered();
    }

    private void on_yuxu_exchange_purchase_requested(string itemPath, int requestedCost)
    {
        var result = YuxuExchangePurchase.Purchase(player, itemPath, requestedCost, get_yuxu_contribution_timestamp());
        if (result.Success)
        {
            mark_save_dirty();
            refresh_yuxu_life_context();
        }
        resolve_yuxu_exchange_purchase(result.Success, result.Balance, result.Message);
    }

    private void resolve_yuxu_exchange_purchase(bool success, long balance, string message)
    {
        var overview = GetNodeOrNull<Node>("SceneLayer/YuxuXianzongOverview");
        overview?.Call("resolve_exchange_purchase", success, balance, message);
    }

    private void request_yuxu_tianwen_practice()
    {
        if (_yuxu_tianwen_practice_confirm_open || player is null) return;
        var activeDutyId = player.entity_data is HumanData human ? human.sect_active_yuxu_duty_id : "";
        var activeDutyInstanceId = player.entity_data is HumanData activeHuman ? activeHuman.sect_active_yuxu_duty_instance_id : "";
        var dutyText = activeDutyId == YuxuTianwenPracticeDutyId && !string.IsNullOrWhiteSpace(activeDutyInstanceId)
            ? $"你已接取天问峰演法公事。本场战斗结果回传后即核验并核发 {YuxuTianwenPracticeDutyReward} 点贡献；不要求获胜，不掉落物品。"
            : "本场照常回传战斗结果，但不结算宗门贡献，也不掉落物品；可先到和光峰事务板接取演法公事。";
        var dialog = new ConfirmationDialog
        {
            Title = "天问峰 · 演法台",
            DialogText = $"以当前人物与技能配置，和按现有兽种资料生成的林地凶狼（凡兽）进行一场演练。战斗沿用正式战斗规则，战斗结果会照常回传。{dutyText}战斗中按 Esc 呼出暂歇菜单；选择“继续”可回到战局。",
            OkButtonText = "开始演练",
            CancelButtonText = "暂不前往"
        };
        _yuxu_tianwen_practice_confirm_open = true;
        GetNode<CanvasLayer>("CanvasLayer").AddChild(dialog);
        dialog.Confirmed += () =>
        {
            _yuxu_tianwen_practice_confirm_open = false;
            dialog.GetParent()?.RemoveChild(dialog);
            dialog.QueueFree();
            start_yuxu_tianwen_practice();
        };
        dialog.Canceled += () =>
        {
            _yuxu_tianwen_practice_confirm_open = false;
            dialog.GetParent()?.RemoveChild(dialog);
            dialog.QueueFree();
        };
        dialog.PopupCentered();
    }

    private void start_yuxu_tianwen_practice()
    {
        if (player is null || GetTree().CurrentScene is null) return;
        var transition = GetNodeOrNull<BattleTransitionManager>("/root/BattleTransitionManager");
        var playerScene = player.pack_for_battle();
        var template = ResourceLoader.Load<BeastTemplate>(TianwenPracticeBeastPath);
        if (transition is null || playerScene is null || template is null)
        {
            show_message("演法未能开始", "战斗场景、主角或林地凶狼的现有配置未能载入；没有消耗物品或贡献。");
            return;
        }

        // 临时加入场景仅为复用正式 Beast 初始化与战斗快照；打包后立即移除，
        // 演练对象不会留在峰内或进入世界人物/NPC 数据。
        var opponent = new Beast
        {
            Name = "TianwenPracticeOpponent",
            entity_name = "林地凶狼",
            unique_id = $"temp_enemy_tianwen_{GD.Randi()}",
            faction = (int)WorldEntity.Faction.ENEMY,
            beast_template = template,
            entity_data = new LivingData(),
            Visible = false
        };
        AddChild(opponent);
        var opponentScene = opponent.pack_for_battle();
        var opponentSnapshot = opponent.get_snapshot_data();
        RemoveChild(opponent);
        opponent.Free();

        if (opponentScene is null || opponentSnapshot is null)
        {
            show_message("演法未能开始", "林地凶狼的演练快照未能建立；没有消耗物品或贡献。");
            return;
        }

        transition.set_battle_return_context(new Godot.Collections.Dictionary
        {
            ["kind"] = "yuxu_tianwen_practice",
            ["duty_instance_id"] = player.entity_data is HumanData human &&
                human.sect_affiliation_id == YuxuSectId &&
                human.sect_active_yuxu_duty_id == YuxuTianwenPracticeDutyId
                    ? human.sect_active_yuxu_duty_instance_id
                    : ""
        });
        transition.start_team_battle(
            new Godot.Collections.Array<PackedScene> { playerScene },
            new Godot.Collections.Array<PackedScene> { opponentScene },
            new Godot.Collections.Array { player.get_snapshot_data() },
            new Godot.Collections.Array { opponentSnapshot },
            GetTree().CurrentScene.SceneFilePath);
    }

    public void show_yuxu_tianwen_practice_result(Godot.Collections.Dictionary summary, Godot.Collections.Dictionary context)
    {
        // MainScene's deferred loaded-game presentation runs first; then restore the origin peak.
        CallDeferred(nameof(restore_tianwen_practice_location));
        var attackersAlive = summary.GetValueOrDefault("attackers_alive", 0).AsInt32();
        var defendersAlive = summary.GetValueOrDefault("defenders_alive", 0).AsInt32();
        var outcome = summary.GetValueOrDefault("player_victory", false).AsBool()
            ? "你在本场演法中取胜。"
            : summary.GetValueOrDefault("is_draw", false).AsBool()
                ? "双方同时倒下，本场演法以平局结束。"
                : summary.GetValueOrDefault("escaped", false).AsBool()
                    ? "本场演法以撤离结果结束。"
                    : attackersAlive > 0 && defendersAlive > 0
                    ? "本场演法已由你提前结束。"
                    : "本场演法结束，你未能取胜。";
        if (player?.entity_data is HumanData human &&
            human.sect_affiliation_id == YuxuSectId &&
            human.sect_active_yuxu_duty_id == YuxuTianwenPracticeDutyId &&
            !string.IsNullOrWhiteSpace(human.sect_active_yuxu_duty_instance_id) &&
            context.GetValueOrDefault("duty_instance_id", "").AsString() == human.sect_active_yuxu_duty_instance_id)
        {
            settle_yuxu_official_duty(
                human,
                YuxuTianwenPracticeDutyId,
                "天问峰凶狼演法",
                human.sect_active_yuxu_duty_instance_id,
                "在天问峰完成一次林地凶狼演法并回传战斗结果；无物品消耗，不设胜负门槛。",
                outcome.TrimEnd('。') + "演法结果已回传；不掉落物品。",
                rewardOverride: YuxuTianwenPracticeDutyReward);
            return;
        }
        show_message("演法结束", outcome + "本场不发放物品或宗门贡献。");
    }

    private void restore_tianwen_practice_location()
    {
        if (!has_open_world_access() || _player_world_node_id != "yuxu_xianzong_core") return;
        estate?.Hide(); _nodeWorldMap?.Hide();  location_interior?.Hide();
        open_faction_overview_if_available(_player_world_node_id);
        GetNodeOrNull<Node>("SceneLayer/YuxuXianzongOverview")?.Call("_on_peak_pressed", "tianwen");
        mark_save_dirty();
    }
}
