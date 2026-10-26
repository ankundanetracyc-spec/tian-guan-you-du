using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class MainScene
{
    private static string[] YuxuLineageGongfaCandidates => GongfaAvailability.EnabledBooks().Select(book => book.ResourcePath).ToArray();

    private bool ensure_yuxu_lineage_staff()
    {
        var manager = _npc_world ?? GetNodeOrNull<NpcWorldManager>("/root/NpcWorldManager");
        if (manager is null || _world_node_data is not { } mapData) return false;
        _npc_world = manager;
        var changed = CloudStateNpcCatalog.ensure_yuxu_lineage_staff(manager, mapData);
        if (changed) mark_save_dirty();
        return changed;
    }

    private List<WorldNpcData> get_yuxu_lineage_teachers()
    {
        ensure_yuxu_lineage_staff();
        var manager = _npc_world ?? GetNodeOrNull<NpcWorldManager>("/root/NpcWorldManager");
        if (manager is null) return [];
        return manager.get_active_npcs()
            .Where(npc => npc.is_yuxu_lineage_staff && npc.faction_id == YuxuSectId &&
                (npc.cultivation_realm is "金丹后期" or "金丹圆满") &&
                !string.IsNullOrWhiteSpace(npc.sect_assigned_guide_npc_id) &&
                manager.get_npc(npc.sect_assigned_guide_npc_id) is { is_alive: true, cultivation_realm: "云息十层" })
            .OrderBy(npc => npc.cultivation_realm == "金丹后期" ? 0 : 1)
            .ThenBy(npc => npc.npc_id, StringComparer.Ordinal)
            .ToList();
    }

    private void open_yuxu_lineage_teacher_picker(Action<WorldNpcData> onTeacherSelected, Action? onCancelled = null)
    {
        var candidates = get_yuxu_lineage_teachers();
        if (candidates.Count == 0)
        {
            show_message("师父名录暂缺", "当前没有可供选择的金丹后期或圆满师父及其云息十层带教同门。师承候选未能登记，入门状态不会改变。 ");
            onCancelled?.Invoke();
            return;
        }

        var dialog = new ConfirmationDialog
        {
            Title = "紫府峰·拜师",
            DialogText = "接引弟子领你来到紫府峰。天灵根入门即为真传；请在宗内金丹后期或圆满师长中择一位为师。师父事务繁忙，会另指派一位云息十层的师兄或师姐照应你。",
            OkButtonText = "拜入此师门",
            CancelButtonText = "暂不拜师"
        };
        var body = new VBoxContainer { CustomMinimumSize = new Vector2(520, 56) };
        var choice = new OptionButton();
        foreach (var teacher in candidates)
            choice.AddItem($"{teacher.npc_name} · {teacher.cultivation_realm}");
        choice.Select(0);
        body.AddChild(choice);
        dialog.AddChild(body);
        AddChild(dialog);
        dialog.PopupCentered();
        dialog.Confirmed += () =>
        {
            var selected = choice.Selected >= 0 && choice.Selected < candidates.Count
                ? candidates[choice.Selected]
                : null;
            dialog.QueueFree();
            if (selected is not null) onTeacherSelected(selected);
        };
        dialog.Canceled += () =>
        {
            dialog.QueueFree();
            onCancelled?.Invoke();
        };
    }

    private bool assign_yuxu_lineage_teacher(HumanData human, WorldNpcData teacher)
    {
        var manager = _npc_world ?? GetNodeOrNull<NpcWorldManager>("/root/NpcWorldManager");
        if (manager is null || !teacher.is_yuxu_lineage_staff ||
            manager.get_npc(teacher.sect_assigned_guide_npc_id) is not { is_alive: true, cultivation_realm: "云息十层" } guide)
            return false;
        human.sect_teacher_npc_id = teacher.npc_id;
        human.sect_guide_npc_id = guide.npc_id;
        return true;
    }

    private void request_yuxu_lineage_teacher_choice(HumanData human)
    {
        if (human.sect_affiliation_id != YuxuSectId || !is_yuxu_true_disciple(human.sect_current_rank)) return;
        var manager = _npc_world ?? GetNodeOrNull<NpcWorldManager>("/root/NpcWorldManager");
        if (manager?.get_npc(human.sect_teacher_npc_id) is { is_alive: true } &&
            manager.get_npc(human.sect_guide_npc_id) is { is_alive: true }) return;
        open_yuxu_lineage_teacher_picker(teacher =>
        {
            if (!assign_yuxu_lineage_teacher(human, teacher))
            {
                show_message("师承登记未完成", "师父或带教师兄/师姐的身份记录不完整，未改动你的师承。 ");
                return;
            }
            mark_save_dirty();
            refresh_yuxu_life_context();
            var guide = _npc_world?.get_npc(human.sect_guide_npc_id);
            show_message("师承已登记", $"你已拜入{teacher.npc_name}门下。师父命{guide?.npc_name ?? "一位云息十层的同门"}先带你熟悉紫府峰与玄览峰；首卷带教之外的日常修行由你自行安排，师父不会事事手把手陪练。 ");
        });
    }

    private void complete_yuxu_admission(string rank, WorldNpcData? teacher = null, WorldNpcData? admissionGuide = null)
    {
        if (player?.entity_data is not HumanData human)
        {
            finish_yuxu_gate_and_return_to_world();
            return;
        }
        if (!string.IsNullOrEmpty(human.sect_affiliation_id))
        {
            finish_yuxu_gate_and_return_to_world();
            return;
        }
        var currentRank = get_yuxu_entry_rank(get_player_spirit_root_count());
        if (string.IsNullOrEmpty(currentRank) || currentRank != rank)
        {
            finish_yuxu_gate_and_return_to_world();
            return;
        }
        if (admissionGuide is null || string.IsNullOrWhiteSpace(admissionGuide.npc_id) ||
            _npc_world?.get_npc(admissionGuide.npc_id) is not { is_alive: true })
        {
            show_message("入门手续未完成", "接引弟子的名录尚未落定，宗门身份没有变更。 ");
            finish_yuxu_gate_and_return_to_world();
            return;
        }
        if (is_yuxu_true_disciple(currentRank) && (teacher is null || !assign_yuxu_lineage_teacher(human, teacher)))
        {
            show_message("入门尚未完成", "真传师承还未完成登记；未加入宗门，请重新办理入门。 ");
            finish_yuxu_gate_and_return_to_world();
            return;
        }

        human.sect_affiliation_id = YuxuSectId;
        human.sect_entry_rank = currentRank;
        human.sect_current_rank = currentRank;
        human.sect_admission_guide_npc_id = admissionGuide.npc_id;
        human.sect_entry_total_game_hours = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? -1;
        human.sect_true_disciple_lecture_next_due_hour = is_yuxu_true_disciple(currentRank) && human.sect_entry_total_game_hours >= 0
            ? human.sect_entry_total_game_hours + YuxuTrueDiscipleLectureInitialDelayHours
            : -1;
        human.sect_true_disciple_lecture_last_attended_hour = -1;
        human.sect_true_disciple_lecture_consecutive_leave_count = 0;
        human.sect_true_disciple_lecture_last_leave_hour = -1;
        human.sect_true_disciple_lecture_missed_count = 0;
        human.sect_true_disciple_lecture_last_missed_hour = -1;
        human.sect_lineage_gongfa_path = "";
        human.sect_lineage_first_volume_only_access = false;
        human.sect_lineage_reported_first_volume = false;
        if (human.sect_allowance_month_cursor < 0 && GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager") is { } gameTime)
            human.sect_allowance_month_cursor = get_game_month_index(gameTime);
        mark_save_dirty();
        refresh_yuxu_admission_context();
        refresh_yuxu_life_context();
        GetNodeOrNull<Node>("SceneLayer/YuxuXianzongOverview")?.Call("refresh_admission_detail");
        open_yuxu_peak_after_admission(is_yuxu_true_disciple(currentRank) ? "zifu" : "guanfu");
    }

    private List<GongfaResource> get_yuxu_lineage_book_choices(Character reader)
    {
        var result = new List<GongfaResource>();
        foreach (var path in YuxuLineageGongfaCandidates)
        {
            if (ResourceLoader.Load<GongfaResource>(path) is not { } book || book.get_sorted_chapters().FirstOrDefault() is not { } first)
                continue;
            if (reader.is_gongfa_chapter_learned(book, first.get_id()) || reader.can_learn_gongfa_chapter(book, first))
                result.Add(book);
        }
        return result;
    }

    private void request_yuxu_lineage_book_choice(HumanData human)
    {
        if (player is null || human.sect_affiliation_id != YuxuSectId || !is_yuxu_true_disciple(human.sect_current_rank)) return;
        if (string.IsNullOrWhiteSpace(human.sect_teacher_npc_id) || string.IsNullOrWhiteSpace(human.sect_guide_npc_id))
        {
            show_message("师承尚未登记", "请先回紫府峰选择师父并登记带教师兄/师姐。 ");
            return;
        }
        if (GongfaAvailability.EnabledPath(human.sect_lineage_gongfa_path).Length > 0)
        {
            show_message("首卷传承", "你已选定师门首卷。回紫府峰静室，在研读界面选择这本秘籍继续修习。 ");
            return;
        }

        var choices = get_yuxu_lineage_book_choices(player);
        if (choices.Count == 0)
        {
            show_message("暂不能选书", "当前候选首卷均未满足各自已有的境界、灵根或属性条件。不会绕过门槛；你可以继续修行，之后再回玄览峰查看。 ");
            return;
        }

        var guide = _npc_world?.get_npc(human.sect_guide_npc_id);
        var dialog = new ConfirmationDialog
        {
            Title = "玄览峰·师门首卷",
            DialogText = $"{guide?.npc_name ?? "带教师兄/师姐"}陪你来到玄览峰。请选择一本当前可研读的云息秘籍；研读沿用秘籍原有的卷章条件、耗时与精力消耗。读完第一卷后，回紫府峰向同门汇报。",
            OkButtonText = "选定此卷",
            CancelButtonText = "稍后再选"
        };
        var body = new VBoxContainer { CustomMinimumSize = new Vector2(520, 56) };
        var choice = new OptionButton();
        foreach (var book in choices) choice.AddItem($"《{book.gongfa_name}》");
        choice.Select(0);
        body.AddChild(choice);
        dialog.AddChild(body);
        AddChild(dialog);
        dialog.PopupCentered();
        dialog.Confirmed += () =>
        {
            var selected = choice.Selected >= 0 && choice.Selected < choices.Count ? choices[choice.Selected] : null;
            dialog.QueueFree();
            if (selected is null || player?.entity_data is not HumanData current || current.sect_affiliation_id != YuxuSectId) return;
            var wasAlreadyKnown = player.get_known_gongfas().Any(book => book.ResourcePath == selected.ResourcePath);
            current.sect_lineage_gongfa_path = selected.ResourcePath;
            current.sect_lineage_first_volume_only_access = !wasAlreadyKnown;
            current.sect_lineage_reported_first_volume = false;
            mark_save_dirty();
            refresh_yuxu_life_context();
            show_message("首卷已选定", $"你选择了《{selected.gongfa_name}》。回紫府峰静室，在研读界面选择此书；卷章门槛、研读进度与技能仍由原秘籍系统处理。 ");
        };
        dialog.Canceled += () => dialog.QueueFree();
    }

    private bool has_yuxu_lineage_first_volume(HumanData human)
    {
        if (player is null || string.IsNullOrWhiteSpace(human.sect_lineage_gongfa_path) ||
            ResourceLoader.Load<GongfaResource>(human.sect_lineage_gongfa_path) is not { } book ||
            book.get_sorted_chapters().FirstOrDefault() is not { } first)
            return false;
        return player.is_gongfa_chapter_learned(book, first.get_id());
    }

    private void report_yuxu_lineage_first_volume(HumanData human)
    {
        if (player is null || human.sect_affiliation_id != YuxuSectId || !is_yuxu_true_disciple(human.sect_current_rank)) return;
        if (string.IsNullOrWhiteSpace(human.sect_guide_npc_id) || string.IsNullOrWhiteSpace(human.sect_lineage_gongfa_path))
        {
            show_message("师承记录不完整", "未找到带教同门或选定秘籍，无法登记首卷汇报。 ");
            return;
        }
        if (human.sect_lineage_reported_first_volume)
        {
            show_message("首卷汇报", "这本秘籍的首卷汇报已经登记。 ");
            return;
        }
        if (!has_yuxu_lineage_first_volume(human))
        {
            show_message("尚未读完首卷", "先在静室读完选定秘籍的第一卷，再回紫府峰汇报。 ");
            return;
        }

        var guide = (_npc_world ?? GetNodeOrNull<NpcWorldManager>("/root/NpcWorldManager"))?.get_npc(human.sect_guide_npc_id);
        human.sect_lineage_reported_first_volume = true;
        mark_save_dirty();
        refresh_yuxu_life_context();
        show_message("向同门汇报", $"你将首卷心得讲给{guide?.npc_name ?? "带教师兄/师姐"}。同门确认了你的研读记录，并提醒你之后按自己的节奏修行。此次汇报不发放宗门贡献或额外属性。 ");
    }
}
