using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class MainScene
{
    private static string[] YuxuLineageGongfaCandidates => GongfaAvailability.EnabledBooks().Select(book => book.ResourcePath).ToArray();

    private List<WorldNpcData> get_yuxu_lineage_teachers()
    {
        var manager = _npc_world ?? GetNodeOrNull<NpcWorldManager>("/root/NpcWorldManager");
        if (manager is null) return [];
        // “师承候选”是该真实人物已登记的收徒资格与意愿，不凭境界推定。
        return manager.get_active_npcs()
            .Where(npc => npc.faction_id == YuxuSectId && npc.relation_tags.Contains("师承候选") &&
                (npc.cultivation_realm is "金丹后期" or "金丹圆满"))
            .OrderBy(npc => npc.cultivation_realm == "金丹后期" ? 0 : 1)
            .ThenBy(npc => npc.npc_name, StringComparer.Ordinal)
            .ThenBy(npc => npc.npc_id, StringComparer.Ordinal)
            .ToList();
    }

    private string yuxu_teacher_unavailable_reason(WorldNpcData? teacher)
    {
        if (teacher is not { is_alive: true } || teacher.faction_id != YuxuSectId ||
            teacher.cultivation_realm is not ("金丹后期" or "金丹圆满") || !teacher.relation_tags.Contains("师承候选"))
            return "这位师长目前不在择师名录中。";
        var hour = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? 0;
        if (teacher.is_in_seclusion_at_hour(hour)) return "师长正在闭关，暂不能会面。";
        if (!yuxu_npc_is_present(teacher, "zifu")) return "师长此刻不在紫府峰，暂不能会面。";
        return "";
    }

    private bool at_yuxu_teacher_choice_location()
    {
        var overview = GetNodeOrNull<Control>("SceneLayer/YuxuXianzongOverview");
        return _player_current_yuxu_peak_id == "zifu" && overview?.Visible == true &&
            overview.GetNodeOrNull<Control>("PeakInterior")?.Visible == true;
    }

    private void open_yuxu_lineage_teacher_picker(HumanData human)
    {
        if (story_dialogue is null || npc_interactions is null || player?.entity_data != human ||
            human.sect_affiliation_id != YuxuSectId || !is_yuxu_true_disciple(human.sect_current_rank) ||
            !string.IsNullOrWhiteSpace(human.sect_teacher_npc_id) || !at_yuxu_teacher_choice_location()) return;
        _activeWorldNpcInteractionId = "";
        var candidates = get_yuxu_lineage_teachers();
        var choices = new List<(string Label, Action Activate)>();
        foreach (var teacher in candidates)
        {
            var id = teacher.npc_id;
            var suffix = yuxu_teacher_unavailable_reason(teacher).Length == 0 ? "" : " · 暂不可面见";
            choices.Add(($"{teacher.npc_name} · {teacher.cultivation_realm}{suffix}", () => show_yuxu_teacher_profile(human, id)));
        }
        choices.Add(("暂不择师", () => { }));
        story_dialogue.show_narration_choices("择师", candidates.Count == 0 ? "目前没有可选的师长，之后可以再来。" : "请选择想了解的师长。", choices);
    }

    private void show_yuxu_teacher_profile(HumanData human, string teacherId)
    {
        var manager = _npc_world ?? GetNodeOrNull<NpcWorldManager>("/root/NpcWorldManager");
        if (manager?.get_npc(teacherId) is not { } teacher || npc_interactions is null) { open_yuxu_lineage_teacher_picker(human); return; }
        _activeWorldNpcInteractionId = "";
        npc_interactions.show_npc_page(teacher, () => manager.get_npc(teacherId), () =>
        {
            var reason = at_yuxu_teacher_choice_location() ? yuxu_teacher_unavailable_reason(manager.get_npc(teacherId)) : "请回紫府峰择师。";
            return new NpcInteractionAction[]
            {
                new("lineage.confirm", "确认拜师", () => confirm_yuxu_teacher(human, teacherId), reason.Length == 0, reason),
                new("lineage.back", "再看看", () => open_yuxu_lineage_teacher_picker(human))
            };
        });
    }

    private bool assign_yuxu_lineage_teacher(HumanData human, WorldNpcData teacher)
    {
        var manager = _npc_world ?? GetNodeOrNull<NpcWorldManager>("/root/NpcWorldManager");
        if (manager is null || manager.get_npc(teacher.npc_id) != teacher ||
            !string.IsNullOrWhiteSpace(human.sect_teacher_npc_id) || yuxu_teacher_unavailable_reason(teacher).Length > 0)
            return false;
        human.sect_teacher_npc_id = teacher.npc_id;
        var guide = manager.get_npc(teacher.sect_assigned_guide_npc_id);
        human.sect_guide_npc_id = guide is { is_alive: true, faction_id: YuxuSectId } &&
            guide.sect_teacher_npc_id == teacher.npc_id ? guide.npc_id : "";
        return true;
    }

    private void request_yuxu_lineage_teacher_choice(HumanData human)
    {
        open_yuxu_lineage_teacher_picker(human);
    }

    private void confirm_yuxu_teacher(HumanData human, string teacherId)
    {
        var teacher = _npc_world?.get_npc(teacherId);
        if (player?.entity_data != human || human.sect_affiliation_id != YuxuSectId ||
            !is_yuxu_true_disciple(human.sect_current_rank) || !at_yuxu_teacher_choice_location() ||
            teacher is null || !assign_yuxu_lineage_teacher(human, teacher))
        {
            show_message("暂不能拜师", "师长目前无法会面，请稍后再试或另选一位。");
            return;
        }
        mark_save_dirty(); refresh_yuxu_life_context(); refresh_qingxi_quest();
        if (story_dialogue is not null)
            story_dialogue.show_player_speech(player.entity_name, "弟子见过师父。", player,
                () => show_npc_dialogue(teacher.npc_name, "嗯，不必多礼。", teacher));
    }

    private void complete_yuxu_admission(string rank, WorldNpcData? teacher = null, WorldNpcData? admissionGuide = null)
    {
        if (player?.entity_data is not HumanData human)
        {
            finish_yuxu_gate_and_return_to_world();
            return;
        }
        if (!string.IsNullOrEmpty(human.sect_affiliation_id) || !human.sect_yuxu_registration_pending)
        {
            finish_yuxu_gate_and_return_to_world();
            return;
        }
        var currentRank = get_recorded_yuxu_admission_rank(human);
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
        human.sect_affiliation_id = YuxuSectId;
        human.sect_yuxu_registration_pending = false;
        human.sect_entry_rank = currentRank;
        human.sect_current_rank = currentRank;
        human.sect_admission_guide_npc_id = admissionGuide.npc_id;
        human.sect_entry_total_game_hours = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? -1;
        initialize_yuxu_provisional_exam();
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
        refresh_baishi_progress();
        refresh_qingxi_quest();
        open_yuxu_peak_after_admission("guanfu");
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
        if (human.sect_affiliation_id == YuxuSectId) open_yuxu_manual_library();
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
