using Godot;
using System.Linq;
using D = Godot.Collections.Dictionary;

public partial class MainScene
{
    public System.Collections.Generic.IEnumerable<NpcInteractionAction> get_baishi_location_questions(WorldNpcData npc)
    {
        if (_player_world_node_id != BaishiNodeId || location_interior?.Visible != true ||
            !string.IsNullOrEmpty(social_contact_failure_reason(npc))) yield break;
        var space = location_interior.get_baishi_space_id();
        if (space.Length == 0 || npc.current_subscene_id != space) yield break;
        foreach (Variant value in BaishiCityContent.Questions(space))
        {
            var question = value.AsGodotDictionary();
            var id = question["id"].AsString();
            yield return new("baishi.local." + space + "." + id, question["label"].AsString(),
                () => ask_baishi_location_question(npc, space, id));
        }
    }

    private void ask_baishi_location_question(WorldNpcData npc, string space, string id)
    {
        if (location_interior?.get_baishi_space_id() != space ||
            !string.IsNullOrEmpty(social_contact_failure_reason(npc)) || npc.current_subscene_id != space) return;
        var question = BaishiCityContent.Questions(space).Select(v => v.AsGodotDictionary())
            .FirstOrDefault(q => q["id"].AsString() == id);
        if (question is null || !try_spend_player_world_stamina(WorldStaminaRules.ConversationCost, "交谈")) return;
        info?.hide_panel(); npc_interactions?.close();
        var beats = question["lines"].AsGodotArray().Select(v =>
            (DialogueBeatKind.NpcSpeech, npc.npc_name, v.AsString())).ToArray();
        show_dialogue_sequence(() => mark_save_dirty(), beats);
    }

    private void on_baishi_location_action(string action)
    {
        if (_player_world_node_id != BaishiNodeId || location_interior?.Visible != true) return;
        var space = location_interior.get_baishi_space_id();
        if (space == "lecture" && action == "lecture.listen") hear_baishi_lecture();
        else if (space == "lecture" && action == "lecture.roots") check_baishi_roots();
        else if (space == "lecture" && action == "lecture.sect") open_baishi_sect_choice();
        else if (space == "lecture" && action == "lecture.wait") wait_for_baishi_roots();
        else if (space == "lecture" && action == "lecture.board") choose_baishi_boat();
        else if (space == "lecture" && action == "lecture.farewell") farewell_baishi_fang();
        else if (space == "lecture" && action == "lecture.leave") leave_baishi_after_lecture();
        else if (space == "notice_hall" && action.StartsWith("notice.", StringComparison.Ordinal))
            read_baishi_notice(action[7..]);
        else if (space == "bookshop" && action == "books.catalog") read_baishi_book_catalog();
        else if (space == "lecture_study" && action == "lesson.study") open_baishi_study();
        else if (space == "lecture_study" && action == "lesson.rest" &&
                 _baishiLecture is { roots_checked: true, reached_yunxi: false, fang_returned: true }) request_baishi_rest();
    }

    private void read_baishi_notice(string id)
    {
        string title;
        string[] lines;
        if (id == "lecture")
        {
            title = "讲法告示";
            var state = _baishiLecture;
            var host = state is null ? null : _npc_world?.get_npc(state.host_actor_id) ?? state.host_snapshot;
            lines = state is { departed_qingxi: true, finished: false } &&
                    host is { is_alive: true, is_traveling: false, current_node_id: BaishiNodeId }
                ? [$"本期讲法设在讲法前院，当值主持为{host.npc_name}。可前去听讲、查验灵根，再问初学门径。",
                   "验灵后选好去向，待本场结束再随接引灵舟出发。入门功法到宗门藏经处选取。"]
                : ["白石的讲法与验灵设在讲法前院，城守府内另有研习处。",
                   "当期主持与安排，以城守府公示及实际在场人员为准。"];
        }
        else
        {
            var notice = BaishiCityContent.Notice(id);
            if (notice.Count == 0) return;
            title = notice["title"].AsString();
            lines = notice["lines"].AsGodotArray().Select(v => v.AsString()).ToArray();
        }
        var beats = lines.Select((line, i) => (DialogueBeatKind.Narration, "", i == 0 ? title + "\n" + line : line)).ToArray();
        show_dialogue_sequence(() => { }, beats);
    }

    private void read_baishi_book_catalog()
    {
        var books = GongfaAvailability.EnabledBooks().OrderBy(b => b.gongfa_name, StringComparer.Ordinal).ToArray();
        var beats = new System.Collections.Generic.List<(DialogueBeatKind, string, string)>
        {
            (DialogueBeatKind.Narration, "", "书目里记着几部入门功法的名称。卷册来源与研习门槛，还要另行看清。")
        };
        foreach (var group in books.Chunk(3))
            beats.Add((DialogueBeatKind.Narration, "", string.Join("\n", group.Select(b => "《" + b.gongfa_name + "》"))));
        beats.Add((DialogueBeatKind.Narration, "", "入门功法须拜入宗门后到藏经处选取；此处可翻看书目。"));
        show_dialogue_sequence(() => { }, beats.ToArray());
    }

    private void refresh_baishi_location_actions()
    {
        var actions = new Godot.Collections.Array<string> { "notice.lecture", "notice.city", "notice.admission", "books.catalog" };
        var space = location_interior?.get_baishi_space_id() ?? "";
        var canListen = !_baishiDialogueBusy && _baishiLecture is { departed_qingxi: true, finished: false, heard_lecture: false } lecture &&
            baishi_present_actor(lecture.host_actor_id) is not null;
        var canCheckRoots = !_baishiDialogueBusy && _baishiLecture is { departed_qingxi: true, finished: false, heard_lecture: true, roots_checked: false } waiting &&
            baishi_present_actor(waiting.host_actor_id) is not null;
        if (canListen) actions.Add("lecture.listen");
        if (canCheckRoots) actions.Add("lecture.roots");
        if (!_baishiDialogueBusy && is_baishi_departure_site() && _baishiLecture is { spirit_boat_in_transit: false } departure)
        {
            if (string.IsNullOrEmpty(departure.selected_sect_id)) actions.Add("lecture.sect");
            else if (departure.selected_sect_id == YuxuSectId)
                actions.Add(departure.roots_session_finished ? "lecture.board" : "lecture.wait");
        }
        if (_baishiLecture is { roots_checked: true, reached_yunxi: false, fang_returned: true } &&
            player?.entity_data is HumanData human && !string.IsNullOrEmpty(human.baishi_first_volume_path))
        {
            actions.Add("lesson.study"); actions.Add("lesson.rest");
        }
        if (!_baishiDialogueBusy && _baishiLecture is { roots_checked: true, finished: false, spirit_boat_in_transit: false } state &&
            _player_world_node_id == BaishiNodeId && location_interior?.get_baishi_space_id() == "lecture")
        {
            if (!state.fang_returned && (get_player_spirit_root_count() == 0 ||
                player?.entity_data is HumanData oldSave && !string.IsNullOrEmpty(oldSave.baishi_first_volume_path)))
                actions.Add("lecture.farewell");
            else actions.Add("lecture.leave");
        }
        location_interior?.set_baishi_actions(actions);
        bool showOverviewGuide = space.Length == 0 && _baishiLecture is { departed_qingxi: true, finished: false, heard_lecture: false };
        location_interior?.set_baishi_guidance(showOverviewGuide ? "place:lecture" : canListen ? "lecture.listen" : canCheckRoots ? "lecture.roots" : "",
            showOverviewGuide ? "主线目标\n前往讲法前院，听讲法并查验灵根。" : canListen
                ? "听讲并验灵\n听完讲法后，当场查验灵根。" : canCheckRoots ? "查验灵根\n完成讲法后继续查验。" : "");
    }

    private void leave_baishi_after_lecture()
    {
        if (_baishiDialogueBusy || story_dialogue?.Visible == true || _baishiLecture is not { roots_checked: true, finished: false } state ||
            _player_world_node_id != BaishiNodeId || location_interior?.get_baishi_space_id() != "lecture") return;
        after_baishi_farewell(() => { state.finished = true; mark_save_dirty(); refresh_qingxi_quest(); open_world(); });
    }
}
