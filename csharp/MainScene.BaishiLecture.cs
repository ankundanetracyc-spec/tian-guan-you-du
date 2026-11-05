using Godot;

public partial class MainScene
{
    private BaishiLectureProgress? _baishiLecture;
    private bool _baishiLectureSpaceOpen;
    private bool _baishiDialogueBusy;
    private const string BaishiNodeId = "white_stone_town";

    private void show_baishi_opening()
    {
        if (_baishiLecture is not { } state) return;
        estate?.Hide(); hud?.set_paper_scene(true);
        _baishiLectureSpaceOpen = true;
        var wilds = new WorldNodeData
        {
            node_id = "home_village", node_name = state.met_fang ? "青溪里外 · 野地" : "溪边野地",
            node_type = WorldNodeData.NodeType.Wilderness, terrain = "溪边野地",
            description = state.met_fang
                ? "方远在青溪里外的野地发现了昏迷的你。他准备去白石赴讲法会，可以从人物列表与他交谈，再决定是否同行。"
                : "溪水从近处流过。一名年轻人蹲在旁边，见你睁眼，递来一只水囊。"
        };
        location_interior?.show_npc_space(wilds, _npc_world, "res://textures/village_overview/qingxi_back_hill_v2.png");
        show_baishi_roster();
        if (!state.saw_awakening)
        {
            _baishiDialogueBusy = true;
            show_dialogue_sequence(() => { state.saw_awakening = true; _baishiDialogueBusy = false; mark_save_dirty(); },
                (DialogueBeatKind.PlayerThought, "", "痛……头好痛。"),
                (DialogueBeatKind.Narration, "", "你撑着地面坐起，掌心压到湿冷的泥土。眼前一阵发黑，缓了片刻，才看清脚边的溪水和衣袖上沾着的草叶。"),
                (DialogueBeatKind.Narration, "", "溪岸、树影、远处的山路，全都陌生。你低头看看自己，身上穿着一件不认得的粗布旧衣。"),
                (DialogueBeatKind.PlayerThought, "", "我怎么会在这里？"),
                (DialogueBeatKind.Narration, "", "你试着回想来这里之前发生了什么，太阳穴却又抽痛起来。脑中零碎的记忆，怎么也接不上眼前这片荒野。"),
                (DialogueBeatKind.Narration, "", "旁边蹲着一个年轻人，束着发，穿一身布衣。他见你醒了，递来一只水囊。"),
                (DialogueBeatKind.PlayerThought, "", "这衣服，这地方……难道我穿越了？先问问他。"));
        }
    }

    private void talk_baishi_fang()
    {
            if (_baishiDialogueBusy || _baishiLecture is not { departed_qingxi: false } state) return;
            if (state.met_fang) { show_baishi_opening(); return; }
            _baishiDialogueBusy = true;
            show_npc_dialogue("方远", "醒了？先喝口水。我路过溪边，看你倒在野地里，叫了几声都没应。", state.companion, () =>
                show_dialogue_sequence(() =>
                {
                    _baishiDialogueBusy = false;
                    state.met_fang = true; mark_save_dirty(); refresh_qingxi_quest(); show_baishi_opening();
                }, (DialogueBeatKind.PlayerSpeech, "", "多谢……这是哪儿？"),
                (DialogueBeatKind.NpcSpeech, "方远", "青溪里外头。沿溪往回走就是村子。我叫方远，刚从那边过来。你怎么一个人倒在这儿？"),
                (DialogueBeatKind.PlayerSpeech, "", "我也记不清了，头还疼。你这是要出远门？"),
                (DialogueBeatKind.NpcSpeech, "方远", "去白石城。那儿要开讲法会，有修士讲入门的门道，还给人验灵根。我想去试试。"),
                (DialogueBeatKind.PlayerSpeech, "", "修士？真能修仙？"),
                (DialogueBeatKind.NpcSpeech, "方远", "自然有。不过能不能修，得先验过才知道。我也是头一回去，别的就不敢乱说了。"),
                (DialogueBeatKind.PlayerThought, "", "连修仙都有……看来真不是原来的地方了。眼下也无处可去，先到城里看看。"),
                (DialogueBeatKind.NpcSpeech, "方远", "你先缓缓，能走了再说。要是没别的去处，咱们就一起去白石。")));
    }

    private WorldNpcData? baishi_present_actor(string id)
    {
        if (_baishiLecture is not { } state || !_baishiLectureSpaceOpen || location_interior?.Visible != true) return null;
        if (!state.fang_returned && state.companion.npc_id == id)
            return !state.departed_qingxi || location_interior.get_baishi_space_id() == "lecture" ? state.companion : null;
        if (!state.departed_qingxi || _player_world_node_id != BaishiNodeId) return null;
        if (location_interior.get_baishi_space_id() != "lecture") return null;
        var saved = state.host_snapshot?.npc_id == id ? state.host_snapshot : state.representatives.FirstOrDefault(n => n.npc_id == id);
        var current = saved is null ? null : _npc_world?.get_npc(id) ?? saved;
        return current is { is_alive: true, is_traveling: false } && current.current_node_id == BaishiNodeId &&
            current.current_subscene_id == "lecture" ? current : null;
    }

    private void show_baishi_roster()
    {
        if (_baishiLecture is not { } state) return;
        var roster = new Godot.Collections.Array<WorldNpcData>();
        if (!state.departed_qingxi)
        {
            if (!state.fang_returned) roster.Add(state.companion);
        }
        else
        {
            if (location_interior?.get_baishi_space_id() != "lecture") return;
            roster = _npc_world?.get_subscene_roster(BaishiNodeId, "lecture") ?? [];
            if (!state.fang_returned && state.companion.is_alive)
            {
                state.companion.current_subscene_id = "lecture";
                roster.Add(state.companion);
            }
            foreach (var saved in state.representatives.Prepend(state.host_snapshot))
            {
                if (saved is null) continue;
                var current = _npc_world?.get_npc(saved.npc_id) ?? saved;
                if (current.is_alive && !current.is_traveling && current.current_node_id == BaishiNodeId &&
                    current.current_subscene_id == "lecture" && roster.All(n => n.npc_id != current.npc_id)) roster.Add(current);
            }
        }
        (GetTree().GetFirstNodeInGroup("cell_character_list") as CellCharacterList)?.show_world_npcs(roster,
            state.departed_qingxi ? "interior:white_stone_town:lecture" : "interior:qingxi:awakening");
    }

    private bool try_talk_baishi_actor(WorldNpcData npc)
    {
        if (baishi_present_actor(npc.npc_id) is null || _baishiLecture is not { } state) return false;
        info?.hide_panel(); npc_interactions?.close();
        if (_baishiDialogueBusy) return true;
        if (!try_spend_player_world_stamina(WorldStaminaRules.ConversationCost, "交谈")) return true;
        if (npc.npc_id == state.companion.npc_id)
        {
            if (!state.departed_qingxi) talk_baishi_fang();
            else if (state.roots_checked) farewell_baishi_fang();
            else lecture_dialogue(show_baishi_lecture, (DialogueBeatKind.NpcSpeech, npc.npc_name, "先去听听讲法吧。验灵的事，也问问清楚。"));
        }
        else if (npc.npc_id == state.host_actor_id)
        {
            if (state.heard_lecture && !state.roots_checked) check_baishi_roots();
            else lecture_dialogue(show_baishi_lecture, (DialogueBeatKind.NpcSpeech, npc.npc_name,
                !state.heard_lecture ? "要听讲法，可以在前面坐一坐。讲完再来验灵。" : "想接着学，就去后面看看功法。哪部合适，先看首卷要求，不明白的再问。你也可以向各宗来使问问去处。"));
        }
        else lecture_dialogue(show_baishi_lecture, (DialogueBeatKind.NpcSpeech, npc.npc_name,
            $"我是{(npc.faction_id == "zhenyue_guan" ? "镇岳观" : "抱朴山")}来白石接待求道者的修士。你可以先了解本门，也可以多问几家，再决定去处。这里不替山门录籍。"));
        return true;
    }

    private void depart_for_baishi()
    {
        if (_baishiLecture is not { departed_qingxi: false } state) return;
        state.departed_qingxi = true; state.companion.current_node_id = BaishiNodeId;
        state.companion.current_subscene_id = "lecture";
        _player_world_node_id = BaishiNodeId;
        hud?.set_world_map_available(true);
        enter_cloud_state_if_departed();
        mark_save_dirty(); refresh_qingxi_quest(); show_baishi_lecture();
    }

    private WorldNpcData? ensure_baishi_host()
    {
        if (_baishiLecture is not { } state) return null;
        if (!string.IsNullOrEmpty(state.host_actor_id)) return state.host_snapshot;
        var manager = _npc_world;
        if (manager is null) return null;
        var eligible = manager.npcs.Where(n => n.is_alive && !n.is_yuxu_lineage_staff &&
            n.faction_id == YuxuSectId && n.current_node_id == BaishiNodeId && !n.is_traveling && string.IsNullOrEmpty(n.public_lecture_event_id) && n.cultivation_realm == "云息十层").ToArray();
        var rng = new RandomNumberGenerator(); rng.Randomize();
        WorldNpcData host;
        if (eligible.Length > 0) host = eligible[rng.RandiRange(0, eligible.Length - 1)];
        else
        {
            var gender = rng.RandiRange(0, 1);
            var generator = new CharacterGenerator();
            var draft = generator.generate("云息", (int)WorldEntity.Faction.NEUTRAL);
            while (draft.root_weights.Count(w => w.AsInt32() > 0) != 2) draft = generator.generate("云息", (int)WorldEntity.Faction.NEUTRAL);
            host = new WorldNpcData
            {
                npc_id = "baishi_speaker_" + Guid.NewGuid().ToString("N"),
                npc_name = new NameGenerator().generate_single_full(gender), gender = gender,
                faction_id = YuxuSectId, formal_identity = "内门弟子", cultivation_realm = "云息十层",
                spirit_root_name = draft.spirit_root_name, root_weights = new Godot.Collections.Array<int>(draft.root_weights.Select(w => w.AsInt32())),
                physique = draft.physique, intelligence = draft.intelligence, soul = draft.soul,
                current_age = draft.current_age, max_age = draft.max_age,
                current_node_id = BaishiNodeId, origin_node_id = "yuxu_xianzong_core",
                current_subscene_id = "lecture", current_state = "驻城讲习", is_alive = true,
                origin_description = "本期由玉虚派驻白石，承担讲法与入门答疑。"
            };
            host.ensure_appearance_identity(); manager.npcs.Add(host);
            manager.EmitSignal(NpcWorldManager.SignalName.world_npcs_changed);
        }
        state.AssignHost(host.npc_id, false, host);
        host.public_lecture_event_id = state.event_id;
        mark_save_dirty(); return state.host_snapshot;
    }

    private void on_baishi_subscene_entered(string nodeId, string spaceId)
    {
        if (nodeId != BaishiNodeId) return;
        _baishiLectureSpaceOpen = spaceId == "lecture" && _baishiLecture is { departed_qingxi: true, finished: false };
        if (_baishiLectureSpaceOpen)
        {
            ensure_baishi_host(); ensure_baishi_representatives();
            show_baishi_roster();
        }
        refresh_baishi_location_actions();
    }

    private IEnumerable<NpcInteractionAction> baishi_actor_actions(WorldNpcData npc)
    {
        yield return new("talk", "交谈", () => open_world_npc_talk(npc));
        if (_baishiLecture is not { } state) yield break;
        if (npc.npc_id == state.companion.npc_id)
        {
            if (state.met_fang && !state.departed_qingxi)
                yield return new("baishi.depart", "一同前往白石", depart_for_baishi);
            yield break;
        }
        if (npc.npc_id != state.host_actor_id) yield break;
        if (!state.heard_lecture) yield return new("baishi.listen", "听讲法", hear_baishi_lecture);
        else if (!state.roots_checked) yield return new("baishi.roots", "查验灵根", check_baishi_roots);
        else
        {
            if (get_player_spirit_root_count() > 0)
            {
                yield return new("baishi.books", "选读功法", open_baishi_books);
                if (player?.entity_data is HumanData h && !string.IsNullOrEmpty(h.baishi_first_volume_path) && !state.reached_yunxi)
                {
                    yield return new("baishi.study", "研读与吐纳", open_baishi_study);
                    yield return new("baishi.rest", "在研习处休息", request_baishi_rest);
                }
                if (state.reached_yunxi) yield return new("baishi.yuxu", "前往玉虚问道", choose_baishi_yuxu);
            }
            yield return new("baishi.leave", "辞别后自行游历", () =>
            {
                state.finished = true; mark_save_dirty(); refresh_qingxi_quest(); open_world();
            });
        }
    }

    private void ensure_baishi_representatives()
    {
        if (_baishiLecture is not { } state || _npc_world is null || state.representatives.Count > 0) return;
        foreach (var (faction, title, origin) in new[] { ("zhenyue_guan", "镇岳观来使", "bc_place_005"), ("baopu_shan", "抱朴山来使", "bc_place_006") })
        {
            var data = new CharacterGenerator().generate("云息");
            while (data.spirit_root_type == 0) data = new CharacterGenerator().generate("云息");
            var envoy = new WorldNpcData
            {
                npc_id = "baishi_envoy_" + Guid.NewGuid().ToString("N"), npc_name = new NameGenerator().generate_single_full(data.gender),
                gender = data.gender, formal_identity = title, faction_id = faction, origin_node_id = origin,
                current_node_id = BaishiNodeId, current_subscene_id = "lecture", current_state = "白石问道接待",
                cultivation_realm = "云息十层", spirit_root_name = data.spirit_root_name,
                root_weights = new Godot.Collections.Array<int>(data.root_weights.Select(w => w.AsInt32())),
                current_age = data.current_age, max_age = data.max_age, is_alive = true,
                public_lecture_event_id = state.event_id
            };
            envoy.ensure_appearance_identity(); _npc_world.npcs.Add(envoy);
            state.representatives.Add((WorldNpcData)envoy.Duplicate(true));
        }
        _npc_world.EmitSignal(NpcWorldManager.SignalName.world_npcs_changed); mark_save_dirty();
    }

    private void show_baishi_lecture()
    {
        if (_baishiLecture is not { departed_qingxi: true, finished: false } || _player_world_node_id != BaishiNodeId ||
            _world_node_data?.get_node(BaishiNodeId) is not { } node) return;
        hud?.Show(); hud?.set_paper_scene(true);
        ensure_baishi_host(); ensure_baishi_representatives(); refresh_baishi_progress();
        _nodeWorldMap?.Hide(); estate?.Hide();
        const string space = "lecture";
        location_interior?.show_baishi_space(node, _npc_world, space);
        _baishiLectureSpaceOpen = space == "lecture";
        if (_baishiLectureSpaceOpen) show_baishi_roster();
        refresh_baishi_location_actions();
    }

    private void lecture_dialogue(Action done, params (DialogueBeatKind, string, string)[] beats)
    {
        if (_baishiDialogueBusy) return;
        _baishiDialogueBusy = true;
        show_dialogue_sequence(() => { _baishiDialogueBusy = false; done(); mark_save_dirty(); refresh_qingxi_quest(); }, beats);
    }

    private void hear_baishi_lecture()
    {
        if (_baishiLecture is not { heard_lecture: false } state) return;
        var speaker = ensure_baishi_host()?.npc_name ?? "当值修士";
        lecture_dialogue(() => { state.heard_lecture = true; show_baishi_lecture(); },
            (DialogueBeatKind.NpcSpeech, "方远", "先听听吧。路上咱俩猜了半天，听听人家怎么说。"),
            (DialogueBeatKind.NpcSpeech, speaker, "修行先要有灵根。不过有灵根，也不是往这儿一坐就会了。功法怎么读，气怎么引，都得从头来。"),
            (DialogueBeatKind.NpcSpeech, speaker, "引气入体，踏入云息，才算开始。想学的，可以先验灵根，再去后面的研习处看书。有疑问就问。"),
            (DialogueBeatKind.NpcSpeech, speaker, "今天验过灵根，不等于拜了师。想去哪个宗门，自己问清楚再作打算。"));
    }

    private void check_baishi_roots()
    {
        if (_baishiLecture is not { heard_lecture: true, roots_checked: false } state || player?.entity_data is not HumanData human) return;
        var speaker = ensure_baishi_host()?.npc_name ?? "当值修士";
        var roots = string.Join("、", human.root_weights.Select((w, i) => $"{new[] { "金", "木", "水", "火", "土" }[Math.Min(i, 4)]}{w.AsInt32()}%"));
        lecture_dialogue(() => { state.roots_checked = true; show_baishi_lecture(); },
            (DialogueBeatKind.NpcSpeech, speaker, "方远，没有查出灵根。修行入道，需要灵根。"),
            (DialogueBeatKind.NpcSpeech, "方远", "那就不是换本书、再练练能成的？"),
            (DialogueBeatKind.NpcSpeech, speaker, "不是。别为这个再去买那些包你入道的东西。"),
            (DialogueBeatKind.NpcSpeech, "方远", "明白了。多谢。"),
            (DialogueBeatKind.Narration, "", $"轮到你时，修士如实说明查验结果：{human.spirit_root_name}。\n五行权重：{roots}。"),
            (DialogueBeatKind.NpcSpeech, speaker, get_player_spirit_root_count() > 0 ? "想接着学，就去后边看看书。先看看各自练些什么，不明白的可以来问。" : "你也没有灵根。可以继续旁听，但不能引气入道。"));
    }

    private void farewell_baishi_fang() => after_baishi_farewell(show_baishi_lecture);
    private void after_baishi_farewell(Action next)
    {
        if (_baishiLecture is not { } state || state.fang_returned) { next(); return; }
        lecture_dialogue(() => { state.fang_returned = true; state.companion.current_node_id = ""; state.companion.current_state = "返乡"; next(); },
            (DialogueBeatKind.NpcSpeech, "方远", "车找到了，我跟着回去。你这边怎么样？"),
            (DialogueBeatKind.PlayerSpeech, "", "准备先学学看。"),
            (DialogueBeatKind.NpcSpeech, "方远", "行。我走了，路上小心。"));
    }

    private static string baishi_requirements(Character reader, GongfaResource book)
    {
        var chapter = book.get_sorted_chapters().FirstOrDefault();
        if (chapter is null) return "没有可研读首卷。";
        var lines = new List<string> { book.is_wuxing_compatible(reader.spirit_root_type) ? "灵根相性：符合" : "灵根相性：不符合", "境界要求：" + (string.IsNullOrEmpty(chapter.required_realm) ? "无额外要求" : chapter.required_realm) };
        foreach (Variant key in chapter.required_attributes.Keys)
            lines.Add($"{key.AsString() switch { "physique" => "体魄", "intelligence" => "灵慧", "soul" => "神魂", "body_proficiency" => "体道造诣", _ => key.AsString() }}：{CardRules.Number(reader, key.AsString()):0.#} / {chapter.required_attributes[key].AsSingle():0.#}");
        lines.Add(reader.can_learn_gongfa_chapter(book, chapter) ? "当前可以开始研读。" : "当前条件尚未满足。请先查看缺项。" );
        return string.Join("\n", lines);
    }

    private void open_baishi_books()
    {
        if (_baishiLecture is not { roots_checked: true } || player is null || get_player_spirit_root_count() == 0) return;
        info?.show_baishi_first_volume_picker(player, book => baishi_requirements(player, book), book =>
        {
            if (player.try_select_baishi_first_volume(book))
            { info?.hide_panel(); mark_save_dirty(); refresh_qingxi_quest(); show_baishi_lecture(); }
            else show_message("选读功法", "本期研习权已开始使用，或尚未满足这部首卷的要求。已学内容不会被替换。");
        });
    }

    private void open_baishi_study()
    {
        if (_baishiLecture is not { roots_checked: true, reached_yunxi: false } ||
            player?.entity_data is not HumanData human || string.IsNullOrEmpty(human.baishi_first_volume_path)) return;
        if (!baishi_farewell_done()) return;
        if (_world_node_data?.get_node(BaishiNodeId) is not { } node) return;
        location_interior?.show_baishi_space(node, _npc_world, "lecture_study");
            cultivation_ui?.show_teaching_for(player);
            cultivation_ui?.SetLocationSessionAccess(() => _player_world_node_id == BaishiNodeId &&
                location_interior?.Visible == true && location_interior.get_baishi_space_id() == "lecture_study" &&
                _baishiLecture is { roots_checked: true, reached_yunxi: false });
    }

    private bool baishi_farewell_done()
    {
        if (_baishiLecture?.fang_returned == true) return true;
        show_message("方远还在等你", "开始长时间研习或休息前，先从人物列表选中方远，与他交谈道别。");
        return false;
    }

    private void request_baishi_rest()
    {
        if (_baishiLecture is not { roots_checked: true, reached_yunxi: false } ||
            player?.entity_data is not HumanData human || string.IsNullOrEmpty(human.baishi_first_volume_path) ||
            !baishi_farewell_done() || _world_node_data?.get_node(BaishiNodeId) is not { } node) return;
        location_interior?.show_baishi_space(node, _npc_world, "lecture_study");
        open_general_rest_dialog("白石研习处", refresh_qingxi_quest);
    }

    private void choose_baishi_yuxu()
    {
        if (_baishiLecture is not { reached_yunxi: true } state) return;
        after_baishi_farewell(() => lecture_dialogue(() =>
        {
            state.chose_yuxu = true; open_world();
        }, (DialogueBeatKind.Narration, "", "你决定前往最近的玉虚问道。可以在世界地图主动行旅，再进入山门说明来意。白石的讲法与查验并不等于入宗。")));
    }

    private void refresh_baishi_progress()
    {
        if (_baishiLecture is not { } state || player is null) return;
        if (player.cultivation_realm != "凡人") state.reached_yunxi = true;
        if (player.entity_data is HumanData h && h.sect_affiliation_id == YuxuSectId) state.finished = true;
        if (state.finished && !state.assignments_released)
        {
            foreach (var npc in _npc_world?.npcs ?? [])
                if (npc.public_lecture_event_id == state.event_id) npc.public_lecture_event_id = "";
            state.assignments_released = true;
        }
        refresh_baishi_location_actions();
    }

    private JournalPanel.Entry baishi_current_task()
    {
        var s = _baishiLecture!;
        string id, title, body;
        if (!s.departed_qingxi) { id = "awake"; title = s.met_fang ? "结伴初行" : "初醒异地"; body = s.met_fang ? "与方远交谈，决定是否同去白石。" : "与身旁的年轻人交谈，问清这里是什么地方。"; }
        else if (!s.heard_lecture) { id = "listen"; title = "白石讲法"; body = "进入白石城讲法处，旁听本期讲法。"; }
        else if (!s.roots_checked) { id = "roots"; title = "查验灵根"; body = "在讲法处查验已有灵根，了解入道门径。"; }
        else if (!s.fang_returned) { id = "farewell"; title = "方远返乡"; body = "从人物列表选中方远，交谈道别后再安排修行。"; }
        else if (get_player_spirit_root_count() == 0) { id = "no_root"; title = "另寻去处"; body = "没有灵根，不能引气入道。可在讲法处选择自行游历。"; }
        else if (player?.entity_data is HumanData h && string.IsNullOrEmpty(h.baishi_first_volume_path)) { id = "choose"; title = "选读首卷"; body = "到白石研习处查看功法，自行选择一部首卷。"; }
        else if (player?.has_completed_intro_gongfa_first_volume() != true) { id = "study"; title = "研读首卷"; body = "在研习处读成所选首卷，要求和耗时按功法实际规则。"; }
        else if (player.main_gongfa is null) { id = "equip"; title = "定为主修"; body = "打开角色功法页，将已读成的功法装配为主修。"; }
        else if (!s.reached_yunxi) { id = "cultivate"; title = "引气入道"; body = "在白石研习处吐纳，按实际门槛踏入云息一层。"; }
        else if (!s.chose_yuxu) { id = "direction"; title = "仙门问路"; body = "回白石讲法处询问去向，可选择前往玉虚或自行游历。"; }
        else { id = "yuxu"; title = "前往玉虚"; body = "在世界地图主动行旅至玉虚仙宗，再进入山门问道；也可自行游历。"; }
        return new(title, body, s.met_fang ? "序章 · 白石问道" : "序章 · 初醒", s.departed_qingxi ? "白石城" : s.met_fang ? "青溪里外" : "溪边野地", s.finished ? "已完成" : "进行中", Id: "baishi." + id);
    }
}
