using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>玉虚山门迎真与入门分流；只用可追溯事件评述经历，不以业力或声望代替跟脚。</summary>
public partial class MainScene
{
    private bool _yuxuAdmissionInProgress;
    private string _yuxuGateElderId = "";

    private string get_recorded_yuxu_admission_rank(HumanData human)
    {
        if (human.sect_yuxu_admission_rank_checked) return human.sect_yuxu_admission_rank;
        if (_baishiLecture is not { roots_checked: true }) return "";
        human.sect_yuxu_admission_rank = get_yuxu_entry_rank(get_player_spirit_root_count());
        human.sect_yuxu_admission_rank_checked = true;
        mark_save_dirty();
        return human.sect_yuxu_admission_rank;
    }

    private bool yuxu_npc_is_present(WorldNpcData? npc, string subscene)
    {
        var hour = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? 0;
        return npc is { is_alive: true, is_traveling: false, faction_id: YuxuSectId, current_node_id: "yuxu_xianzong_core" } &&
            npc.current_subscene_id == subscene && string.IsNullOrEmpty(npc.dungeon_id) &&
            string.IsNullOrEmpty(npc.temporary_party_task_id) && !npc.is_player_travel_companion && !npc.is_in_seclusion_at_hour(hour);
    }

    private static WorldNpcData? choose_yuxu_present_npc(IEnumerable<WorldNpcData> npcs)
        // 人物在世界创建时已经随机；同一批在场人物不要因反复打开吊牌而重新抽签。
        => npcs.OrderBy(npc => npc.npc_id, StringComparer.Ordinal).FirstOrDefault();

    private void open_yuxu_gate_entrance()
    {
        var overview = GetNodeOrNull<Node>("SceneLayer/YuxuXianzongOverview");
        if (overview is null) { open_world(); return; }
        refresh_yuxu_admission_context(overview);
        overview.Call("open_gate_welcome");
    }

    private void begin_yuxu_gate_welcome()
    {
        if (_yuxuAdmissionInProgress) return;
        if (player?.entity_data is not HumanData human || _world_node_data is not { } mapData)
        {
            open_world();
            return;
        }

        var manager = _npc_world ?? GetNodeOrNull<NpcWorldManager>("/root/NpcWorldManager");
        if (manager is null)
        {
            open_world();
            return;
        }

        _npc_world = manager;
        manager.set_world_map(mapData);
        var elder = choose_yuxu_present_npc(manager.get_active_npcs().Where(n =>
            n.relation_tags.Contains("山门接引") && yuxu_npc_is_present(n, "")));
        if (elder is null)
        {
            show_message("暂不能迎真", "山门接引长老暂不在此，请稍后再来。");
            return;
        }

        _yuxuGateElderId = elder.npc_id;
        _yuxuAdmissionInProgress = true;
        // Keep the current world as a dimmed backdrop for the interaction rather than exposing a black screen.
        open_world();
        GetNodeOrNull<Control>("SceneLayer/YuxuXianzongOverview")?.Hide();
        hide_world_character_list();


        location_interior?.Hide();

        // 灵根已在白石查验；迎真只评述已有经历，入门身份沿用角色的资质记录。
        var lines = new List<string>();
        if (human.sect_yuxu_admission_rank_checked || _baishiLecture is { roots_checked: true })
            lines.Add("长老核对了白石留下的验灵记录，确认结果已在案，没有再让你查验灵根。");
        foreach (var line in human.assess_player_causal_history().get_yuxu_gate_judgment_lines())
            if (!string.IsNullOrWhiteSpace(line)) lines.Add(line);
        if (lines.Count == 0)
            lines.Add("长老循着因果脉络看了片刻，未作仓促评断。");

        if (story_dialogue is null)
        {
            finish_yuxu_gate_and_return_to_world();
            return;
        }

        story_dialogue.show_yuxu_admission_review(
            elder,
            lines,
            continue_yuxu_gate_after_review,
            finish_yuxu_gate_and_return_to_world, player: player);
    }

    private void continue_yuxu_gate_after_review()
    {
        if (!_yuxuAdmissionInProgress || player?.entity_data is not HumanData human ||
            _npc_world?.get_npc(_yuxuGateElderId) is not { } elder || !yuxu_npc_is_present(elder, ""))
        {
            finish_yuxu_gate_and_return_to_world();
            return;
        }

        if (string.IsNullOrEmpty(get_recorded_yuxu_admission_rank(human)))
        {
            story_dialogue?.show_yuxu_admission_result(
                elder,
                ["长老翻过白石名册，没有找到可入门的验灵记录，便合上名册：‘今日先不办入门。’"],
                finish_yuxu_gate_and_return_to_world, player: player);
            return;
        }

        story_dialogue?.show_yuxu_admission_question(
            elder,
            answerHasSect => resolve_yuxu_gate_sect_answer(human, elder, answerHasSect),
            finish_yuxu_gate_and_return_to_world, player: player);
    }

    private void resolve_yuxu_gate_sect_answer(HumanData human, WorldNpcData elder, bool answeredHasSect)
    {
        if (!_yuxuAdmissionInProgress || player?.entity_data != human)
        {
            finish_yuxu_gate_and_return_to_world();
            return;
        }

        if (answeredHasSect)
        {
            story_dialogue?.show_yuxu_admission_result(
                elder,
                ["长老点了点头，没有再问宗门来历：‘既有所属，便不相邀。小友请便。’"],
                finish_yuxu_gate_and_return_to_world, player: player);
            return;
        }

        // Recheck the saved affiliation before registration; a contradictory answer must never overwrite it.
        if (!string.IsNullOrWhiteSpace(human.sect_affiliation_id))
        {
            story_dialogue?.show_yuxu_admission_result(
                elder,
                ["长老看过已有的宗门名录，未再追问，只道：‘既已有所归属，今日便不办入门。’"],
                finish_yuxu_gate_and_return_to_world, player: player);
            return;
        }

        var manager = _npc_world ?? GetNodeOrNull<NpcWorldManager>("/root/NpcWorldManager");
        var escort = _baishiLecture is { chose_yuxu: true, spirit_boat_in_transit: false } trip && manager is not null
            ? manager.get_npc(trip.spirit_boat_driver_id) : null;
        var guide = yuxu_npc_is_present(escort, "") ? escort : manager is null ? null :
            choose_yuxu_present_npc(manager.get_active_npcs().Where(n => n.npc_id != elder.npc_id &&
                n.relation_tags.Contains("山门接引弟子") && yuxu_npc_is_present(n, "")));
        if (guide is null)
        {
            story_dialogue?.show_yuxu_admission_result(
                elder,
                ["长老命人去传接引弟子，片刻后却未能等到回音；他暂且收起名册，示意改日再办。"],
                finish_yuxu_gate_and_return_to_world, player: player);
            return;
        }

        human.sect_admission_guide_npc_id = guide.npc_id;
        mark_save_dirty();
        story_dialogue?.show_yuxu_admission_result(
            elder,
            [$"长老唤来{guide.npc_name}：‘既无宗门，便随这位弟子去办入门之礼吧。’"],
            () => begin_yuxu_formal_entry(human, elder, guide), player: player);
    }

    private void begin_yuxu_formal_entry(HumanData human, WorldNpcData elder, WorldNpcData admissionGuide)
    {
        if (!_yuxuAdmissionInProgress || player?.entity_data != human || !yuxu_npc_is_present(admissionGuide, "") ||
            !string.IsNullOrWhiteSpace(human.sect_affiliation_id))
        {
            finish_yuxu_gate_and_return_to_world();
            return;
        }

        var rank = get_recorded_yuxu_admission_rank(human);
        if (string.IsNullOrEmpty(rank))
        {
            story_dialogue?.show_yuxu_admission_result(
                elder,
                ["白石名册尚未记下验灵结果，入门手续暂时无法办理。"],
                finish_yuxu_gate_and_return_to_world, player: player);
            return;
        }

        admissionGuide.current_subscene_id = "guanfu";
        admissionGuide.current_state = "接引新弟子";
        human.sect_yuxu_registration_pending = true;
        mark_save_dirty();
        refresh_qingxi_quest();
        open_yuxu_peak_after_admission("guanfu");
    }

    private void finish_yuxu_gate_and_return_to_world()
    {
        _yuxuAdmissionInProgress = false;
        _yuxuGateElderId = "";
        open_world();
    }

    private void cancel_yuxu_formal_entry(HumanData human, WorldNpcData? admissionGuide)
    {
        if (string.IsNullOrWhiteSpace(human.sect_affiliation_id))
        {
            human.sect_admission_guide_npc_id = "";
            if (admissionGuide is not null)
            {
                admissionGuide.current_subscene_id = "";
                admissionGuide.current_state = "师门常驻";
            }
            mark_save_dirty();
        }
        finish_yuxu_gate_and_return_to_world();
    }

    private void open_yuxu_peak_after_admission(string peakId)
    {
        _yuxuAdmissionInProgress = false;
        _yuxuGateElderId = "";
        var overview = GetNodeOrNull<Node>("SceneLayer/YuxuXianzongOverview");
        if (overview is null)
        {
            open_world();
            return;
        }

        hide_world_character_list();

        _nodeWorldMap?.Hide();

        location_interior?.Hide();
        refresh_yuxu_admission_context(overview);
        overview.Call("open_overview");
        overview.Call("_on_peak_pressed", peakId);
    }

    private void record_player_causal_event(
        string eventId,
        string eventTypeId,
        string eventTypeName,
        string locationId,
        string locationName,
        string context,
        string outcome)
    {
        if (player?.entity_data is not HumanData human || string.IsNullOrWhiteSpace(eventId)) return;
        var time = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
        var occurredAt = time?.get_total_game_hours() ?? -1;
        if (occurredAt < 0) return;

        if (human.record_player_causal_event(new PlayerCausalEventRecord
        {
            event_id = eventId,
            occurred_at_total_game_hours = occurredAt,
            event_type_id = eventTypeId,
            event_type_name = eventTypeName,
            location_id = locationId,
            location_name = locationName,
            context = context,
            outcome = outcome
        }))
            mark_save_dirty();
    }
}
