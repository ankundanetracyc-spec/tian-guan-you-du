using Godot;
using System;
using System.Collections.Generic;

/// <summary>玉虚山门迎真与入门分流；只用可追溯事件评述经历，不以业力或声望代替跟脚。</summary>
public partial class MainScene
{
    private bool _yuxuAdmissionInProgress;

    private void begin_yuxu_gate_welcome()
    {
        if (_yuxuAdmissionInProgress) return;
        if (player?.entity_data is not HumanData human || node_map?.map_data is not { } mapData)
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
        var elder = CloudStateNpcCatalog.ensure_yuxu_admission_elder(manager, mapData);
        if (elder is null)
        {
            open_world();
            return;
        }

        mark_save_dirty();
        _yuxuAdmissionInProgress = true;
        // Keep the current world as a dimmed backdrop for the interaction rather than exposing a black screen.
        open_world();
        GetNodeOrNull<Control>("SceneLayer/YuxuXianzongOverview")?.Hide();
        node_map?.hide_world_roster();
        node_map?.Hide();
        estate?.Hide();
        location_interior?.Hide();

        var lines = new List<string>();
        foreach (var line in human.assess_player_causal_history().get_yuxu_gate_judgment_lines())
            if (!string.IsNullOrWhiteSpace(line)) lines.Add(line);
        if (lines.Count == 0)
            lines.Add("长老循着因果脉络看了片刻，未作仓促评断，随后转而察看你周身灵机。");
        lines.Add(get_yuxu_gate_aptitude_line(human));

        if (story_dialogue is null)
        {
            finish_yuxu_gate_and_return_to_world();
            return;
        }

        story_dialogue.show_yuxu_admission_review(
            elder,
            lines,
            continue_yuxu_gate_after_review,
            finish_yuxu_gate_and_return_to_world, player: player, lastLineIsAptitude: true);
    }

    private string get_yuxu_gate_aptitude_line(HumanData human)
    {
        var rootCount = get_player_spirit_root_count();
        var rank = get_yuxu_entry_rank(rootCount);
        var rootName = string.IsNullOrWhiteSpace(human.spirit_root_name) ? "灵根未明" : human.spirit_root_name.Trim();
        if (string.IsNullOrEmpty(rank))
            return $"长老的灵识在灵机间停了停，缓缓摇头：‘{rootName}，尚不合玉虚录籍之规。’";
        if (rank == "真传弟子")
            return $"长老察看灵机，目光微凝，随即颔首：‘{rootName}。依宗门规制，入门即列真传。’";
        return $"长老细察灵机后收回灵识：‘{rootName}，依宗门规制，可列{rank}。’";
    }

    private void continue_yuxu_gate_after_review()
    {
        if (!_yuxuAdmissionInProgress || player?.entity_data is not HumanData human ||
            _npc_world?.get_npc("yuxu_gate_elder") is not { } elder)
        {
            finish_yuxu_gate_and_return_to_world();
            return;
        }

        if (string.IsNullOrEmpty(get_yuxu_entry_rank(get_player_spirit_root_count())))
        {
            story_dialogue?.show_yuxu_admission_result(
                elder,
                ["长老收回灵识，语气仍然温和：‘小友资质尚未达到本宗录籍之规，今日便不办入门了。’"],
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
                ["长老点了点头，没有再问宗门来历：‘既有所属，老夫便不相邀。小友请便。’"],
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
        var mapData = node_map?.map_data;
        var guide = manager is not null && mapData is not null
            ? CloudStateNpcCatalog.ensure_yuxu_admission_guide(manager, mapData)
            : null;
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
            [$"长老唤来{guide.npc_name}：‘既无宗门，便随这位弟子去吧，余下的入门之礼由他带你办妥。’"],
            () => begin_yuxu_formal_entry(human, elder, guide), player: player);
    }

    private void begin_yuxu_formal_entry(HumanData human, WorldNpcData elder, WorldNpcData admissionGuide)
    {
        if (!_yuxuAdmissionInProgress || player?.entity_data != human ||
            !string.IsNullOrWhiteSpace(human.sect_affiliation_id))
        {
            finish_yuxu_gate_and_return_to_world();
            return;
        }

        var rank = get_yuxu_entry_rank(get_player_spirit_root_count());
        if (string.IsNullOrEmpty(rank))
        {
            story_dialogue?.show_yuxu_admission_result(
                elder,
                ["长老复核名录时发现灵根记录有缺，先将入门手续搁下，未作录籍。"],
                finish_yuxu_gate_and_return_to_world, player: player);
            return;
        }

        var peakId = is_yuxu_true_disciple(rank) ? "zifu" : "guanfu";
        admissionGuide.current_subscene_id = peakId;
        admissionGuide.current_state = "师门常驻";
        mark_save_dirty();

        if (is_yuxu_true_disciple(rank))
        {
            open_yuxu_lineage_teacher_picker(
                teacher => show_yuxu_formal_teacher_ceremony(human, rank, teacher),
                () => cancel_yuxu_formal_entry(human, admissionGuide));
            return;
        }

        story_dialogue?.show_yuxu_admission_result(
            admissionGuide,
            [
                $"接引弟子领你办妥门籍，依{human.spirit_root_name}录为{rank}。",
                "名册落定后，他陪你回到观复峰安置。静室与休息仍按弟子原有安排开放，不另发物品或贡献。"
            ],
            () => complete_yuxu_admission(rank, null, admissionGuide),
            "入门录籍", player: player);
    }

    private void show_yuxu_formal_teacher_ceremony(HumanData human, string rank, WorldNpcData teacher)
    {
        if (!_yuxuAdmissionInProgress || player?.entity_data != human ||
            !string.IsNullOrWhiteSpace(human.sect_affiliation_id))
        {
            cancel_yuxu_formal_entry(human, _npc_world?.get_npc(human.sect_admission_guide_npc_id));
            return;
        }

        var guide = _npc_world?.get_npc(teacher.sect_assigned_guide_npc_id);
        if (guide is null)
        {
            cancel_yuxu_formal_entry(human, _npc_world?.get_npc(human.sect_admission_guide_npc_id));
            return;
        }

        story_dialogue?.show_yuxu_admission_result(
            teacher,
            [
                $"你随接引弟子来到紫府峰，向{teacher.npc_name}行礼。",
                $"{teacher.npc_name}察看你的灵根与名册，收你为徒；宗门随即录入真传身份。",
                $"师父尚有峰务在身，命{guide.npc_name}先陪你熟悉紫府峰与玄览峰，日常修行由你自行安排。"
            ],
            () => complete_yuxu_admission(rank, teacher, _npc_world?.get_npc(human.sect_admission_guide_npc_id)),
            "拜师礼", player: player);
    }

    private void finish_yuxu_gate_and_return_to_world()
    {
        _yuxuAdmissionInProgress = false;
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
        var overview = GetNodeOrNull<Node>("SceneLayer/YuxuXianzongOverview");
        if (overview is null)
        {
            open_world();
            return;
        }

        node_map?.hide_world_roster();
        node_map?.Hide();
        tile_world_map?.Hide();
        estate?.Hide();
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
