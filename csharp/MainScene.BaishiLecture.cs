using Godot;

public partial class MainScene
{
    private BaishiLectureProgress? _baishiLecture;
    private bool _baishiLectureSpaceOpen;
    private bool _baishiDialogueBusy;
    private const string BaishiNodeId = "white_stone_town";
    private const string BaishiEscortBoatPath = "res://resources/world/bai_chuan/yuxu_escort_boat_travel_20261106.tres";

    private void show_baishi_opening()
    {
        if (_baishiLecture is not { } state) return;
         hud?.set_paper_scene(true);
        _baishiLectureSpaceOpen = true;
        var wilds = new WorldNodeData
        {
            node_id = "home_village", node_name = state.met_fang ? "青溪里外 · 野地" : "溪边野地",
            node_type = WorldNodeData.NodeType.Wilderness, terrain = "溪边野地",
            description = state.met_fang
                ? "你与方远结伴离开家乡，赶往白石。方才你在青溪里外晕倒，方远一直守在身边。"
                : "溪水从近处流过。方远守在你身边，见你睁眼，递来一只水囊。"
        };
        location_interior?.show_npc_space(wilds, _npc_world, "res://textures/village_overview/qingxi_back_hill_v2.png");
        show_baishi_roster();
        location_interior?.set_qingxi_opening_action(state.saw_awakening && !_baishiDialogueBusy ? "qingxi.talk" : "");
        if (state.saw_awakening && state.met_fang && !state.departed_qingxi && !_baishiDialogueBusy)
        {
            depart_for_baishi();
            return;
        }
        if (!state.saw_awakening)
        {
            _baishiDialogueBusy = true;
            show_dialogue_sequence(() => { state.saw_awakening = true; _baishiDialogueBusy = false; mark_save_dirty(); show_baishi_opening(); },
                (DialogueBeatKind.PlayerThought, "", "痛……头好痛。"),
                (DialogueBeatKind.Narration, "", "你撑着地面坐起，掌心压到湿冷的泥土。眼前一阵发黑，缓了片刻，才看清脚边的溪水和衣袖上沾着的草叶。"),
                (DialogueBeatKind.Narration, "", "溪岸、树影、远处的山路，瞧着熟悉，却又有些说不出的陌生。"),
                (DialogueBeatKind.Narration, "", "你想起和方远一同离开家乡，沿途赶路，又有些零乱的画面掺在其中，一时想不分明。"),
                (DialogueBeatKind.Narration, "", "方远蹲在旁边，见你醒了，忙把水囊递过来。"),
                (DialogueBeatKind.PlayerThought, "", "方远……我们走到哪儿了？"));
        }
    }

    private void on_qingxi_opening_action(string action)
    {
        if (_baishiDialogueBusy || _baishiLecture is not { departed_qingxi: false, saw_awakening: true } state ||
            location_interior?.Visible != true) return;
        if (action == "qingxi.talk")
        {
            if (state.met_fang) depart_for_baishi();
            else try_talk_baishi_actor(state.companion);
        }
    }

    private void talk_baishi_fang()
    {
            if (_baishiDialogueBusy || _baishiLecture is not { departed_qingxi: false } state) return;
            if (state.met_fang) { depart_for_baishi(); return; }
            _baishiDialogueBusy = true;
            show_npc_dialogue("方远", "先别急着起身，喝口水。方才你忽然倒下，我叫了你好几声都没应。", state.companion, () =>
                show_dialogue_sequence(() =>
                {
                    _baishiDialogueBusy = false;
                    state.met_fang = true; mark_save_dirty(); show_baishi_opening();
                }, (DialogueBeatKind.PlayerSpeech, "", "方远……我们走到哪儿了？"),
                (DialogueBeatKind.Narration, "", "水囊里的凉水压下喉间的干涩。溪声渐渐清晰，散乱的记忆却仍像隔着一层薄雾。"),
                (DialogueBeatKind.PlayerSpeech, "", "我记得我们一起赶路……别的事，就像被什么搅乱了。"),
                (DialogueBeatKind.NpcSpeech, "方远", "想不起来便先别勉强。这里是青溪里外，白石城就在前头。"),
                (DialogueBeatKind.PlayerSpeech, "", "我们是去听讲法、验灵根？"),
                (DialogueBeatKind.NpcSpeech, "方远", "嗯。城里有修士讲些入门的门道，也会替来问道的人查验灵根。验过之后要去哪儿，再慢慢打算。"),
                (DialogueBeatKind.Narration, "", "方远收起水囊，沿着溪边小道望向远处。山风掠过树梢，吹散了两人之间短暂的沉默。"),
                (DialogueBeatKind.NpcSpeech, "方远", "能走了就出发吧。到了白石，先把眼前的事弄明白。")));
    }

    private WorldNpcData? baishi_present_actor(string id)
    {
        if (_baishiLecture is not { } state) return null;
        if (!state.fang_returned && state.companion.npc_id == id && state.companion.is_player_travel_companion &&
            (_nodeWorldMap?.Visible == true || location_interior?.Visible == true && _player_world_node_id == BaishiNodeId))
            return _npc_world?.get_npc(id);
        if (!_baishiLectureSpaceOpen || location_interior?.Visible != true) return null;
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
            if (!state.fang_returned && state.companion.is_alive && roster.All(n => n.npc_id != state.companion.npc_id))
            {
                if (!state.companion.is_player_travel_companion) state.companion.current_subscene_id = "lecture";
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
            else if (_nodeWorldMap?.Visible == true)
                lecture_dialogue(() => { }, (DialogueBeatKind.NpcSpeech, "方远", "白石的讲法咱们还得去听。赶路时多留神，别累着了。"));
            else if (state.roots_checked) farewell_baishi_fang();
            else lecture_dialogue(() => { }, (DialogueBeatKind.NpcSpeech, npc.npc_name, "先去听听讲法吧。验灵的事，也问问清楚。"));
        }
        else if (npc.npc_id == state.host_actor_id)
        {
            lecture_dialogue(show_baishi_lecture, (DialogueBeatKind.NpcSpeech, npc.npc_name, baishi_host_reminder()));
        }
        else lecture_dialogue(show_baishi_lecture, (DialogueBeatKind.NpcSpeech, npc.npc_name,
            $"我是{(npc.faction_id == "zhenyue_guan" ? "镇岳观" : "抱朴山")}来白石接待求道者的修士。你可以先了解本门，也可以多问几家，再决定去处。这里不替山门录籍。"));
        return true;
    }

    private void depart_for_baishi()
    {
        if (_baishiLecture is not { departed_qingxi: false, met_fang: true } state || _npc_world is null) return;
        state.departed_qingxi = true;
        state.traveling_to_baishi = true;
        hud?.set_world_map_available(true);
        enter_cloud_state_if_departed();
        if (!_npc_world.join_player_travel_party(state.companion))
        {
            state.departed_qingxi = false;
            state.traveling_to_baishi = false;
            hud?.set_world_map_available(false);
            show_message("暂不能同行", "方远暂时无法加入同行，请稍后再试。");
            return;
        }
        mark_save_dirty(); refresh_qingxi_quest(); open_world();
    }

    private bool arrive_baishi_with_companion()
    {
        if (_baishiLecture is not { traveling_to_baishi: true } state) return true;
        var fang = _npc_world?.get_npc(state.companion.npc_id);
        if (fang is not { is_player_travel_companion: true })
        {
            show_message("同行状态异常", "方远的同行状态尚未恢复，请保留存档并重试。");
            return false;
        }
        state.companion = fang;
        state.traveling_to_baishi = false;
        mark_save_dirty(); refresh_qingxi_quest();
        return true;
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
        RefreshJournalTracker();
    }

    private IEnumerable<NpcInteractionAction> baishi_actor_actions(WorldNpcData npc)
    {
        yield return new("talk", "交谈", () => open_world_npc_talk(npc));
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
        _nodeWorldMap?.Hide(); 
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
        if (_baishiDialogueBusy || story_dialogue?.Visible == true ||
            _baishiLecture is not { departed_qingxi: true, finished: false, heard_lecture: false } state ||
            _player_world_node_id != BaishiNodeId || location_interior?.get_baishi_space_id() != "lecture" ||
            baishi_present_actor(state.host_actor_id) is null) return;
        info?.hide_panel(); npc_interactions?.close();
        var speaker = ensure_baishi_host()?.npc_name ?? "当值修士";
        if (player?.entity_data is not HumanData human) return;
        var roots = string.Join("、", human.root_weights.Select((w, i) => $"{new[] { "金", "木", "水", "火", "土" }[Math.Min(i, 4)]}{w.AsInt32()}%"));
        lecture_dialogue(() =>
        {
            state.heard_lecture = true;
            state.roots_checked = true;
            human.sect_yuxu_admission_rank = get_yuxu_entry_rank(get_player_spirit_root_count());
            human.sect_yuxu_admission_rank_checked = true;
            show_baishi_lecture();
        },
            (DialogueBeatKind.Narration, "", "讲案前的人声渐渐低下去。主持修士抬手示意，待院中安静，才从修行根基讲起。"),
            (DialogueBeatKind.NpcSpeech, speaker, "求道先看灵根。它关乎能否感应天地灵气；即便有灵根，也还要辨明自身所长，再循序修习。"),
            (DialogueBeatKind.NpcSpeech, speaker, "引气入体，踏入云息，方算迈过第一道门槛。各家功法不同，入门之后再依自身根性择卷。"),
            (DialogueBeatKind.Narration, "", "讲法告一段落，主持修士没有催促，只让求道者依次上前。石案旁的测灵器具映着天光，轮到你时，他示意你将手按在石面。"),
            (DialogueBeatKind.NpcSpeech, speaker, $"修士查看测灵器具后，向你说明查验结果：{human.spirit_root_name}。五行权重为{roots}。"),
            (DialogueBeatKind.NpcSpeech, speaker, get_player_spirit_root_count() > 0
                ? "查验只明根性，不替你择门。若想入宗，再问清各家门规与去处。"
                : "你没有可供引气入体的灵根。今日所听可以记下，往后的路仍由你自己选择。"));
        refresh_baishi_location_actions();
    }

    private void check_baishi_roots()
    {
        if (_baishiDialogueBusy || story_dialogue?.Visible == true ||
            _baishiLecture is not { departed_qingxi: true, finished: false, heard_lecture: true, roots_checked: false } state ||
            _player_world_node_id != BaishiNodeId || location_interior?.get_baishi_space_id() != "lecture" ||
            baishi_present_actor(state.host_actor_id) is null || player?.entity_data is not HumanData human) return;
        var speaker = ensure_baishi_host()?.npc_name ?? "当值修士";
        var roots = string.Join("、", human.root_weights.Select((w, i) => $"{new[] { "金", "木", "水", "火", "土" }[Math.Min(i, 4)]}{w.AsInt32()}%"));
        lecture_dialogue(() =>
        {
            state.roots_checked = true;
            human.sect_yuxu_admission_rank = get_yuxu_entry_rank(get_player_spirit_root_count());
            human.sect_yuxu_admission_rank_checked = true;
            mark_save_dirty();
            show_baishi_lecture();
        },
            (DialogueBeatKind.NpcSpeech, speaker, "方远，没有查出灵根。修行入道，需要灵根。"),
            (DialogueBeatKind.NpcSpeech, "方远", "那就不是换本书、再练练能成的？"),
            (DialogueBeatKind.NpcSpeech, speaker, "不是。别为这个再去买那些包你入道的东西。"),
            (DialogueBeatKind.NpcSpeech, "方远", "明白了。多谢。"),
            (DialogueBeatKind.Narration, "", $"轮到你时，修士如实说明查验结果：{human.spirit_root_name}。\n五行权重：{roots}。"),
            (DialogueBeatKind.NpcSpeech, speaker, get_player_spirit_root_count() > 0 ? "若想去宗门问道，先定个去处。等其他人都验完，再随接引舟出发。" : "你也没有灵根。可以继续旁听，但不能引气入道。"));
        refresh_baishi_location_actions();
    }

    private void farewell_baishi_fang() => after_baishi_farewell(() =>
    {
        if (location_interior?.get_baishi_space_id() == "lecture") show_baishi_lecture();
        else location_interior?.refresh_location_roster();
    });
    private void after_baishi_farewell(Action next, bool travelingToSect = false)
    {
        if (_baishiLecture is not { } state || state.fang_returned) { next(); return; }
        lecture_dialogue(() =>
        {
            if (state.companion.is_player_travel_companion)
                _npc_world?.leave_player_travel_party(state.companion.npc_id, BaishiNodeId, location_interior?.get_baishi_space_id() ?? "", "返乡");
            state.fang_returned = true; state.companion.current_node_id = ""; state.companion.current_state = "返乡";
            state.companion.public_lecture_event_id = ""; _npc_world?.npcs.Remove(state.companion); next();
        },
            (DialogueBeatKind.NpcSpeech, "方远", "车找到了，我跟着回去。你这边怎么样？"),
            (DialogueBeatKind.PlayerSpeech, "", travelingToSect || state.selected_sect_id == YuxuSectId ? "我打算随玉虚的灵舟去山门看看。" : "我还没定去处，打算再看看。"),
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
        show_message("方远还在等你", "开始长时间研习或休息前，先回讲法前院与方远道别。");
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

    private void choose_baishi_boat()
    {
        if (!can_handle_baishi_departure() || !baishi_boarding_ready()) return;
        after_baishi_farewell(() => lecture_dialogue(begin_baishi_boat_flight,
            (DialogueBeatKind.NpcSpeech, ensure_baishi_host()?.npc_name ?? "当值修士", "接引舟已经备好，路上的用度由宗门筹备。到了山门，再照规矩问道录籍。")), travelingToSect: true);
        refresh_baishi_location_actions();
    }

    private void begin_baishi_boat_flight()
    {
        if (!baishi_boarding_ready() || _baishiLecture is not { roots_checked: true, fang_returned: true, spirit_boat_in_transit: false } state ||
            _npc_world is not { } manager || player is null) return;
        var travel = GD.Load<ItemTravelData>(BaishiEscortBoatPath);
        if (travel?.try_get_dun_speed(out var speed) != true || travel.try_get_passenger_capacity(out var capacity) != true)
        {
            show_message("暂无法乘舟", "接引灵舟尚未准备妥当，请稍后再试。"); return;
        }
        if (manager.get_player_travel_party().Count + 2 > capacity)
        {
            show_message("随行人数过多", $"接引灵舟最多可载{capacity}人，人数包括驾舟弟子。"); return;
        }
        var generator = new CharacterGenerator();
        var data = generator.generate("云息", (int)WorldEntity.Faction.NEUTRAL);
        var driver = new WorldNpcData
        {
            npc_id = "baishi_boat_driver_" + Guid.NewGuid().ToString("N"),
            npc_name = new NameGenerator().generate_single_full(data.gender), gender = data.gender,
            faction_id = YuxuSectId, formal_identity = "玉虚弟子", cultivation_realm = "云息十层",
            spirit_root_name = data.spirit_root_name, root_weights = new Godot.Collections.Array<int>(data.root_weights.Select(w => w.AsInt32())),
            physique = data.physique, intelligence = data.intelligence, soul = data.soul,
            current_age = data.current_age, max_age = data.max_age,
            current_node_id = BaishiNodeId, origin_node_id = "yuxu_xianzong_core",
            current_state = "接引", is_alive = true, public_lecture_event_id = state.event_id,
            origin_description = "奉玉虚之命驾驶灵舟，接引白石求道者。"
        };
        driver.ensure_appearance_identity();
        if (!manager.join_player_travel_party(driver))
        {
            show_message("暂无法乘舟", "接引弟子尚未准备妥当，请稍后再试。"); return;
        }
        state.spirit_boat_driver_id = driver.npc_id;
        state.spirit_boat_in_transit = true;
        state.chose_yuxu = true;
        open_world();
        if (_nodeWorldMap?.Call("begin_spirit_boat_flight", "yuxu_xianzong_core", speed, driver.npc_id).AsBool() != true)
        {
            state.spirit_boat_in_transit = false;
            state.spirit_boat_driver_id = "";
            state.chose_yuxu = false;
            manager.leave_player_travel_party(driver.npc_id, BaishiNodeId, "", "接引");
            driver.public_lecture_event_id = "";
            mark_save_dirty(); sync_player_travel_party_map(); refresh_qingxi_quest();
            show_message("暂无法乘舟", "当前无法启航，你仍在白石附近，可以稍后再试。"); return;
        }
        sync_player_travel_party_map();
        mark_save_dirty(); refresh_qingxi_quest();
    }

    private void complete_baishi_boat_flight()
    {
        if (_baishiLecture is not { spirit_boat_in_transit: true } state || _nodeWorldMap is null ||
            _nodeWorldMap.Call("get_current_location_runtime_id").AsString() != "yuxu_xianzong_core") return;
        if (!string.IsNullOrEmpty(state.spirit_boat_driver_id) && _npc_world?.get_npc(state.spirit_boat_driver_id) is { } driver &&
            _npc_world.leave_player_travel_party(driver.npc_id, "yuxu_xianzong_core", "", "返回玉虚"))
            driver.public_lecture_event_id = "";
        state.spirit_boat_in_transit = false;
        sync_player_travel_party_map();
        mark_save_dirty(); refresh_qingxi_quest();
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
        if (!s.departed_qingxi) { id = "awake"; title = s.met_fang ? "前往白石城" : "溪边醒来"; body = s.met_fang ? "抵达白石城。" : "弄清自己的处境。"; }
        else if (s.traveling_to_baishi) { id = "journey"; title = "前往白石城"; body = "抵达白石城。"; }
        else if (!s.heard_lecture) { id = "listen"; title = "白石讲法"; body = "听讲法并查验灵根。"; }
        else if (!s.roots_checked) { id = "roots"; title = "查验灵根"; body = "查明自身灵根。"; }
        else if (get_player_spirit_root_count() == 0)
        { id = s.fang_returned ? "no_root" : "farewell"; title = s.fang_returned ? "另寻去处" : "方远返乡"; body = s.fang_returned ? "另寻去处。" : "向方远道别。"; }
        else if (s.spirit_boat_in_transit) { id = "boat"; title = "乘舟赴玉虚"; body = "抵达玉虚仙宗。"; }
        else if (player?.entity_data is HumanData { sect_yuxu_registration_pending: true, sect_affiliation_id: "" })
        { id = "registration"; title = "玉虚报到"; body = "在观复峰办理入门。"; }
        else if (s.chose_yuxu) { id = "yuxu"; title = "前往玉虚"; body = "前往玉虚山门问道。"; }
        else if (string.IsNullOrEmpty(s.selected_sect_id)) { id = "sect"; title = "择定宗门"; body = "选定求道宗门。"; }
        else if (!s.roots_session_finished) { id = "wait"; title = "静候启程"; body = "等候本场验灵结束。"; }
        else { id = "board"; title = "乘舟赴玉虚"; body = "乘舟前往玉虚仙宗。"; }
        return new(title, body, s.met_fang ? "序章 · 白石问道" : "序章 · 初醒", s.departed_qingxi ? "白石城" : s.met_fang ? "青溪里外" : "溪边野地", s.finished ? "已完成" : "进行中", Id: "baishi." + id);
    }
}
