using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GDictionary = Godot.Collections.Dictionary;

public partial class MainScene
{
    private const long YuxuProvisionalExamHours = 360L * 24L;
    private const string YuxuExamTrialKind = "yuxu_provisional_exam_trial";
    private const string YuxuExamWolfPath = "res://resources/beast_templates/林地凶狼.tres";
    private const string YuxuExamBeiPath = "res://resources/beast_templates/林地妖狈.tres";

    private static bool yuxu_exam_is_active(HumanData human) => human.sect_affiliation_id == YuxuSectId &&
        human.sect_current_rank == "预备真传" && human.sect_yuxu_exam_start_hour >= 0 && human.sect_yuxu_exam_result == 0;

    private static long yuxu_exam_deadline(HumanData human) => human.sect_yuxu_exam_start_hour + YuxuProvisionalExamHours;

    private static string format_yuxu_exam_date(long totalHours)
    {
        if (totalHours < 0) return "日期未定";
        var days = totalHours / 24;
        return $"昭明历{GameTimeMgr.ZhaomingStartYear + days / 360}年{days % 360 / 30 + 1}月{days % 30 + 1}日{totalHours % 24}时";
    }

    /// <summary>新入门从报到起算；未保存本考核的旧预备真传从升级后首次进入起算。</summary>
    private void initialize_yuxu_provisional_exam()
    {
        if (player?.entity_data is not HumanData human || human.sect_affiliation_id != YuxuSectId ||
            human.sect_current_rank != "预备真传" || human.sect_yuxu_exam_result != 0 ||
            GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager") is not { } time) return;
        var now = (long)time.get_total_game_hours();
        if (human.sect_yuxu_exam_start_hour < 0)
        {
            human.sect_yuxu_exam_start_hour = now;
            human.sect_yuxu_exam_realm_hour = has_reached_yuxu_exam_realm(player) ? now : -1;
            human.sect_yuxu_exam_book_hour = has_learned_yuxu_exam_book(player, human) ? now : -1;
            human.sect_yuxu_exam_trial_hour = -1;
            human.sect_yuxu_exam_pending_attempt_id = "";
            mark_save_dirty();
        }
        else if (!_returning_from_battle && !string.IsNullOrEmpty(human.sect_yuxu_exam_pending_attempt_id))
        {
            // 没有战斗返回数据的存档不能占住下一次演法，也不能凭此认定获胜。
            human.sect_yuxu_exam_pending_attempt_id = "";
            mark_save_dirty();
        }
    }

    private bool has_reached_yuxu_exam_realm(Character actor)
    {
        if (RealmManager.get_cloud_realm_tier(actor.cultivation_realm) >= 3) return true;
        var realms = GetNodeOrNull<RealmManager>("/root/RealmManager");
        var current = realms?.realm_order.IndexOf(actor.cultivation_realm) ?? -1;
        var required = realms?.realm_order.IndexOf("云息三层") ?? -1;
        return current >= 0 && required >= 0 && current >= required;
    }

    private static GongfaResource? get_yuxu_exam_book(HumanData human)
    {
        var path = GongfaAvailability.EnabledPath(human.sect_library_first_manual_path);
        return path.Length == 0 ? null : ResourceLoader.Load<GongfaResource>(path);
    }

    private static bool has_learned_yuxu_exam_book(Character actor, HumanData human)
    {
        var book = get_yuxu_exam_book(human);
        var chapters = book?.get_sorted_chapters().Take(2).ToArray();
        return book is not null && chapters is { Length: 2 } &&
            chapters.All(chapter => actor.is_gongfa_chapter_learned(book, chapter.get_id()));
    }

    private bool can_show_yuxu_provisional_exam(HumanData human)
    {
        var time = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
        return yuxu_exam_is_active(human) && time is not null && (long)time.get_total_game_hours() < yuxu_exam_deadline(human);
    }

    private bool can_offer_yuxu_provisional_trial(HumanData human)
        => can_show_yuxu_provisional_exam(human) && human.sect_yuxu_exam_trial_hour < 0;

    private void on_yuxu_exam_breakthrough(Character actor)
    {
        if (actor != player || actor.entity_data is not HumanData human || !yuxu_exam_is_active(human) ||
            human.sect_yuxu_exam_realm_hour >= 0 || !has_reached_yuxu_exam_realm(actor)) return;
        var now = (long)(GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? -1);
        var finished = actor.get_last_breakthrough_completion_hour();
        if (finished < human.sect_yuxu_exam_start_hour || finished > now) finished = now;
        human.sect_yuxu_exam_realm_hour = finished;
        mark_save_dirty();
        evaluate_yuxu_provisional_exam(human, now);
    }

    private void on_yuxu_exam_chapter_completed(Character actor, string bookName)
    {
        if (actor != player || actor.entity_data is not HumanData human || !yuxu_exam_is_active(human) ||
            human.sect_yuxu_exam_book_hour >= 0 || !has_learned_yuxu_exam_book(actor, human) ||
            get_yuxu_exam_book(human)?.gongfa_name != bookName) return;
        var now = (long)(GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? -1);
        var finished = actor.get_study_action_completion_hour();
        if (finished < human.sect_yuxu_exam_start_hour) finished = now;
        human.sect_yuxu_exam_book_hour = finished;
        mark_save_dirty();
        // 卷章回调先于时钟前进；只有已实际经过完成时辰才能结算身份。
        if (finished <= now) evaluate_yuxu_provisional_exam(human, now);
    }

    private void on_yuxu_exam_first_copy_committed()
    {
        if (player?.entity_data is not HumanData human || !yuxu_exam_is_active(human) ||
            human.sect_yuxu_exam_book_hour >= 0 || !has_learned_yuxu_exam_book(player, human)) return;
        var now = (long)(GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? -1);
        human.sect_yuxu_exam_book_hour = now;
        mark_save_dirty(); evaluate_yuxu_provisional_exam(human, now);
    }

    private void process_yuxu_provisional_exam_clock()
    {
        if (player?.entity_data is not HumanData human || !yuxu_exam_is_active(human) ||
            GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager") is not { } time) return;
        evaluate_yuxu_provisional_exam(human, time.get_total_game_hours());
        GetNodeOrNull<Node>("SceneLayer/YuxuXianzongOverview/PeakInterior")?.Call("set_provisional_exam_context", can_show_yuxu_provisional_exam(human));
        if (_baishiLecture is null) RefreshJournalTracker();
    }

    private void evaluate_yuxu_provisional_exam(HumanData human, long now)
    {
        if (!yuxu_exam_is_active(human) || now < human.sect_yuxu_exam_start_hour) return;
        var due = yuxu_exam_deadline(human);
        var achievements = new[] { human.sect_yuxu_exam_realm_hour, human.sect_yuxu_exam_book_hour, human.sect_yuxu_exam_trial_hour };
        if (achievements.All(hour => hour >= human.sect_yuxu_exam_start_hour && hour <= due && hour <= now))
            resolve_yuxu_provisional_exam(human, true, achievements.Max());
        else if (now >= due)
            resolve_yuxu_provisional_exam(human, false, due);
    }

    /// <summary>身份发生在长行动的中途时，先按旧身份结清转变前的月份。</summary>
    private void accrue_yuxu_allowance_before_exam_resolution(HumanData human, long resolutionHour)
    {
        var targetMonth = (int)Math.Clamp(resolutionHour / (30L * 24L), 0L, int.MaxValue);
        if (human.sect_allowance_month_cursor < 0) { human.sect_allowance_month_cursor = targetMonth; return; }
        if (targetMonth <= human.sect_allowance_month_cursor) return;
        var elapsed = (long)targetMonth - human.sect_allowance_month_cursor;
        var accrued = elapsed * get_yuxu_monthly_stipend(human.sect_current_rank);
        human.sect_unclaimed_spirit_stones = (int)Math.Min(int.MaxValue, (long)Math.Max(0, human.sect_unclaimed_spirit_stones) + accrued);
        human.sect_allowance_month_cursor = targetMonth;
    }

    private void resolve_yuxu_provisional_exam(HumanData human, bool passed, long atHour)
    {
        if (player?.entity_data != human || !yuxu_exam_is_active(human)) return;
        accrue_yuxu_allowance_before_exam_resolution(human, atHour);
        human.sect_yuxu_exam_result = passed ? 1 : 2;
        human.sect_yuxu_exam_resolved_hour = atHour;
        human.sect_yuxu_exam_pending_attempt_id = "";
        human.sect_current_rank = passed ? "真传" : "内门弟子";
        if (passed) human.sect_true_disciple_lecture_next_due_hour = atHour + YuxuTrueDiscipleLectureInitialDelayHours;
        update_yuxu_monthly_allowance();
        mark_save_dirty();
        refresh_yuxu_admission_context(); refresh_yuxu_life_context();
        hud?.show_event_notification("真传考核", passed
            ? "考核已通过，正式列为真传；可前往紫府峰择师。"
            : "一年期已满，考核未完成，已转为内门弟子。", "修行");
    }

    private List<JournalPanel.Entry> get_yuxu_provisional_exam_tasks()
    {
        if (player?.entity_data is not HumanData human || human.sect_affiliation_id != YuxuSectId) return [];
        var tasks = new List<JournalPanel.Entry>();
        if (yuxu_exam_is_active(human))
        {
            tasks.Add(IdentifyJournalTask(new JournalPanel.Entry("真传考核", "一年内完成真传考核。", "玉虚仙宗", "天问峰", "进行中",
                "截止：" + format_yuxu_exam_date(yuxu_exam_deadline(human)), "yuxu.provisional.exam")));
        }
        else if (human.sect_yuxu_exam_result is 1 or 2)
        {
            tasks.Add(IdentifyJournalTask(new JournalPanel.Entry("真传考核", human.sect_yuxu_exam_result == 1 ? "考核通过，已列真传。" : "一年期满未完成考核，已列内门。",
                "玉虚仙宗", "天问峰", "已完成", format_yuxu_exam_date(human.sect_yuxu_exam_resolved_hour), "yuxu.provisional.exam")));
        }
        if (is_yuxu_true_disciple(human.sect_current_rank) && string.IsNullOrWhiteSpace(human.sect_teacher_npc_id))
            tasks.Add(IdentifyJournalTask(new JournalPanel.Entry("真传择师", "择定师父。", "玉虚仙宗", "紫府峰", "进行中", Id: "yuxu.provisional.teacher")));
        return tasks;
    }

    private void on_yuxu_exam_briefing_cancelled(GDictionary context)
    {
        if (context.GetValueOrDefault("kind", "").AsString() != YuxuExamTrialKind ||
            player?.entity_data is not HumanData human ||
            human.sect_yuxu_exam_pending_attempt_id != context.GetValueOrDefault("attempt_id", "").AsString()) return;
        human.sect_yuxu_exam_pending_attempt_id = ""; mark_save_dirty();
    }

    private void request_yuxu_provisional_trial()
    {
        var overview = GetNodeOrNull<Control>("SceneLayer/YuxuXianzongOverview");
        if (player?.entity_data is not HumanData human || !can_show_yuxu_provisional_exam(human) ||
            _player_current_yuxu_peak_id != "tianwen" || overview?.Visible != true ||
            overview.GetNodeOrNull<Control>("PeakInterior")?.Visible != true || story_dialogue is null || story_dialogue.Visible) return;
        var remaining = yuxu_exam_deadline(human) - GetNode<GameTimeMgr>("/root/GameTimeManager").get_total_game_hours();
        var details = "一年内达到云息三层，学会首本秘籍第二卷，并胜过一只林地妖狈和一只林地凶狼。全部完成即列真传，逾期转内门。\n" +
            $"境界：{(human.sect_yuxu_exam_realm_hour >= 0 ? "已达成" : "未达成")}　" +
            $"秘籍：{(human.sect_yuxu_exam_book_hour >= 0 ? "已学会" : "未学会")}　" +
            $"演法：{(human.sect_yuxu_exam_trial_hour >= 0 ? "已取胜" : "未取胜")}\n" +
            $"距期限还有 {remaining / 24} 日 {remaining % 24} 时。";
        var choices = can_offer_yuxu_provisional_trial(human)
            ? new (string Label, Action Activate)[] { ("开始演法", start_yuxu_provisional_trial), ("稍后再来", () => { }) }
            : new (string Label, Action Activate)[] { ("知道了", () => { }) };
        story_dialogue.show_narration_choices("真传考核", details, choices);
    }

    private (PackedScene? scene, BattleEntityData? snapshot) make_yuxu_exam_beast(string path)
    {
        var template = ResourceLoader.Load<BeastTemplate>(path);
        if (template is null) return (null, null);
        var beast = new Beast { Name = "YuxuExamBeast", entity_name = template.display_name,
            unique_id = "temp_enemy_yuxu_exam_" + Guid.NewGuid().ToString("N"), faction = (int)WorldEntity.Faction.ENEMY,
            beast_template = template, entity_data = new LivingData(), Visible = false };
        AddChild(beast);
        var scene = beast.pack_for_battle(); var snapshot = beast.get_snapshot_data();
        RemoveChild(beast); beast.Free();
        return (scene, snapshot);
    }

    private void start_yuxu_provisional_trial()
    {
        if (player?.entity_data is not HumanData human || !can_offer_yuxu_provisional_trial(human) ||
            _player_current_yuxu_peak_id != "tianwen" || GetTree().CurrentScene is null) return;
        var transition = GetNodeOrNull<BattleTransitionManager>("/root/BattleTransitionManager");
        var playerScene = player.pack_for_battle(); var playerSnapshot = player.get_snapshot_data();
        var (wolfScene, wolfSnapshot) = make_yuxu_exam_beast(YuxuExamWolfPath);
        var (beiScene, beiSnapshot) = make_yuxu_exam_beast(YuxuExamBeiPath);
        if (transition is null || transition.is_flow_transition_active() || transition.has_open_flow_view() ||
            playerScene is null || playerSnapshot is null || wolfScene is null || wolfSnapshot is null || beiScene is null || beiSnapshot is null)
        { show_message("演法暂缓", "考核场或对手暂未准备好，此次没有开始。"); return; }
        var attempt = Guid.NewGuid().ToString("N");
        human.sect_yuxu_exam_pending_attempt_id = attempt; mark_save_dirty();
        transition.set_battle_return_context(new GDictionary {
            ["kind"] = YuxuExamTrialKind, ["is_training"] = true, ["title"] = "真传考核 · 演法",
            ["allow_prebattle_cancel"] = true, ["attempt_id"] = attempt });
        transition.start_team_battle(new Godot.Collections.Array<PackedScene> { playerScene },
            new Godot.Collections.Array<PackedScene> { wolfScene, beiScene },
            new Godot.Collections.Array { playerSnapshot }, new Godot.Collections.Array { wolfSnapshot, beiSnapshot },
            GetTree().CurrentScene.SceneFilePath);
    }

    public async void show_yuxu_provisional_trial_result(GDictionary summary, GDictionary context)
    {
        for (var i = 0; i < 600 && !StartupComplete && IsInsideTree(); i++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!IsInsideTree() || StartupError is not null || player?.entity_data is not HumanData human) return;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var attempt = context.GetValueOrDefault("attempt_id", "").AsString();
        if (attempt.Length == 0 || attempt != human.sect_yuxu_exam_pending_attempt_id || !yuxu_exam_is_active(human)) return;
        human.sect_yuxu_exam_pending_attempt_id = "";
        var now = (long)(GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? -1);
        var victory = summary.GetValueOrDefault("player_victory", false).AsBool() && !summary.GetValueOrDefault("escaped", false).AsBool();
        if (victory && now >= human.sect_yuxu_exam_start_hour && now <= yuxu_exam_deadline(human)) human.sect_yuxu_exam_trial_hour = now;
        mark_save_dirty(); evaluate_yuxu_provisional_exam(human, now); refresh_yuxu_life_context();
        restore_tianwen_practice_location();
        show_dialogue_sequence((DialogueBeatKind.Narration, "", victory
            ? "你在演法中取胜，这一场已记入真传考核。"
            : "这次演法未能取胜。一年期限内仍可再试。"));
    }
}
