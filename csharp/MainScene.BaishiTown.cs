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
        if (space == "notice_hall" && action.StartsWith("notice.", StringComparison.Ordinal))
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
                ? [$"本期讲法设在城守府前院，当值主持为{host.npc_name}。可前去旁听、查验灵根，再问初学门径。",
                   "讲法后的研习处在府内侧屋。听讲、选读首卷与宗门录籍是各自的事。"]
                : ["白石的讲法、验灵设在城守府前院，后侧另有研习处。",
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
        beats.Add((DialogueBeatKind.Narration, "", "本期公开首卷须先验灵，再向主持修士选读。翻看书目不会取得卷册或研习权。"));
        show_dialogue_sequence(() => { }, beats.ToArray());
    }

    private void refresh_baishi_location_actions()
    {
        var actions = new Godot.Collections.Array<string> { "notice.lecture", "notice.city", "notice.admission", "books.catalog" };
        if (_baishiLecture is { roots_checked: true, reached_yunxi: false, fang_returned: true } &&
            player?.entity_data is HumanData human && !string.IsNullOrEmpty(human.baishi_first_volume_path))
        {
            actions.Add("lesson.study"); actions.Add("lesson.rest");
        }
        location_interior?.set_baishi_actions(actions);
    }
}
