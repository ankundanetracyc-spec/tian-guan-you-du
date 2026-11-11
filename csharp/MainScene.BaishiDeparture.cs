using Godot;

public partial class MainScene
{
    private bool is_baishi_departure_site() =>
        _baishiLecture is { roots_checked: true, finished: false } state && get_player_spirit_root_count() > 0 &&
        _player_world_node_id == BaishiNodeId && location_interior?.get_baishi_space_id() == "lecture" &&
        baishi_present_actor(state.host_actor_id) is not null;

    private bool can_handle_baishi_departure() => !_baishiDialogueBusy && story_dialogue?.Visible != true && is_baishi_departure_site();

    private bool baishi_boarding_ready() => _baishiLecture is
        { roots_checked: true, selected_sect_id: YuxuSectId, roots_session_finished: true, finished: false, spirit_boat_in_transit: false };

    private string baishi_host_reminder()
    {
        if (_baishiLecture is not { heard_lecture: true } state) return "讲席已经开了，可以先去旁听。讲完再来验灵。";
        if (!state.roots_checked) return "讲完了，想验灵根的到石案前来。";
        if (get_player_spirit_root_count() == 0) return "没有灵根，引气入道这条路走不了。别为这个去买包你入道的东西。";
        if (string.IsNullOrEmpty(state.selected_sect_id)) return "想去玉虚问道，可以先定下。等这场验灵结束，再随接引舟去山门。";
        if (!state.roots_session_finished) return "还有人没验，先等一会儿。都验完了再一起走。";
        return "接引舟已经备好，准备好了就上舟。到了山门，还要照规矩问道。";
    }

    private void open_baishi_sect_choice()
    {
        if (!can_handle_baishi_departure() || _baishiLecture is not { selected_sect_id: "" }) return;
        info?.hide_panel(); npc_interactions?.close();
        _baishiDialogueBusy = true;
        story_dialogue?.show_narration_choices("择定去向", "灵根已经验明。你可以先定下求道的宗门，待本场验灵结束后随接引舟启程。",
            new (string Label, Action Activate)[] {
                ("玉虚仙宗", () => { _baishiDialogueBusy = false; select_baishi_sect(YuxuSectId); }),
                ("暂不决定", cancel_baishi_sect_choice)
            }, cancel_baishi_sect_choice);
        refresh_baishi_location_actions();
    }

    private void cancel_baishi_sect_choice()
    {
        _baishiDialogueBusy = false;
        show_baishi_lecture();
    }

    private void select_baishi_sect(string sectId)
    {
        if (sectId != YuxuSectId || !can_handle_baishi_departure() || _baishiLecture is not { selected_sect_id: "" } state) return;
        state.selected_sect_id = sectId;
        mark_save_dirty(); refresh_qingxi_quest();
        lecture_dialogue(show_baishi_lecture, (DialogueBeatKind.NpcSpeech, ensure_baishi_host()?.npc_name ?? "当值修士",
            "好，去玉虚。还有人没验，先在廊下等一会儿，等这边结束再一起出发。"));
        refresh_baishi_location_actions();
    }

    private void wait_for_baishi_roots()
    {
        if (!can_handle_baishi_departure() || _baishiLecture is not
            { selected_sect_id: YuxuSectId, roots_session_finished: false } state) return;
        info?.hide_panel(); npc_interactions?.close();
        lecture_dialogue(() =>
        {
            state.roots_session_finished = true;
            mark_save_dirty(); show_baishi_lecture();
        },
            (DialogueBeatKind.Narration, "", "你在廊下等候。石案前的人依次验过灵根，登记各自的去向。"),
            (DialogueBeatKind.NpcSpeech, ensure_baishi_host()?.npc_name ?? "当值修士", "都验过了。去玉虚的，等会儿随接引弟子登舟。"));
        refresh_baishi_location_actions();
    }
}
