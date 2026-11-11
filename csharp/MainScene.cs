using Godot;

/// <summary>主场景协调器：建立单个苏醒者、连接个人 HUD 与世界节点地图。</summary>
public partial class MainScene : Node2D
{
    // Main scene coordinates world actions and stamina settlement.
    private enum DialogueBeatKind { Narration, PlayerSpeech, PlayerThought, NpcSpeech }
    private static readonly System.Collections.Generic.Dictionary<string,string> ResourceItems=new()
    {
        ["mat_bitter_moss_root"]="res://resources/entities/items/苦苔根.tres",
        ["mat_spirit_spring_algae"]="res://resources/entities/items/灵泉藻.tres",
        ["spirit_stones"]="res://resources/entities/items/灵石.tres"
    };
    private MainHUD? hud; private GameTimeUI? time_ui; private StoragePanel? storage; private EstateResourceSystem? estate_resources; private InfoPanel? info; private CultivationUI? cultivation_ui; private CondensationCardGamePanel? condensation_panel; private RealmPanel? realm_panel; private StoryDialoguePanel? story_dialogue; private NpcInteractionPanel? npc_interactions; private TradePaperPanel? trade_panel; private WorldNodeMapData? _world_node_data; private WorldLocationInteriorPanel? location_interior; private JournalPanel? journal; private BaigongPanel? baigong; private ActionTransitionPanel? action_transition; private bool _suppress_persistent_chrome; private Character? player; private int _journal_unread; private readonly List<JournalPanel.Entry> _earned_rumors=[]; private readonly List<JournalPanel.Entry> _completed_quests=[]; private readonly List<Character> family=[]; private readonly List<Character> retainers=[];
    private string _player_world_node_id = "home_village"; private NpcWorldManager? _npc_world; private bool _cloud_state_initialized; private bool _world_open_in_progress; private bool _trade_hud_was_visible;
    public bool StartupComplete { get; private set; }
    public Exception? StartupError { get; private set; }
    public override async void _Ready()
    {
        try
        {
        await JourneyCreationLoadingView.StageAsync(GetTree(), .78, "正在布置行旅界面…");
        AddToGroup("main_scene");
        AddToGroup("gameplay_scene");
        connect_world_npc_interaction_components();
        prepare_save_session();
        hud=GetNodeOrNull<MainHUD>("HUDLayer/MainHUD");
        time_ui=GetNodeOrNull<GameTimeUI>("HUDLayer/WorldInfo");
        storage=GetNodeOrNull<StoragePanel>("CanvasLayer/StoragePanel");
        info=GetNodeOrNull<InfoPanel>("CanvasLayer/HumanInfoPanel");
        if (info is not null) info.AppearanceChanged += mark_save_dirty;
        cultivation_ui=GetNodeOrNull<CultivationUI>("CanvasLayer/CultivationUI");
        condensation_panel = new CondensationCardGamePanel { Name = "CondensationCardGamePanel" };
        GetNode<CanvasLayer>("CanvasLayer").AddChild(condensation_panel);
        realm_panel=GetNodeOrNull<RealmPanel>("CanvasLayer/RealmPanel");
        if (realm_panel is not null)
        {
            realm_panel.SetPresentationReadiness(() => !GetTree().Paused && action_transition?.IsPlaying != true &&
                _npc_world?.HasPendingSettlement != true && GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.IsSmoothAdvanceBusy != true);
            realm_panel.PresentationStarted += () =>
            {
                cultivation_ui?.close_panel();
                condensation_panel?.SuspendResultForRealmPresentation();
            };
            realm_panel.Closed += () =>
            {
                if (IsInsideTree() && !IsQueuedForDeletion()) condensation_panel?.RestoreResultAfterRealmPresentation();
            };
        }
        story_dialogue=GetNodeOrNull<StoryDialoguePanel>("DialogueLayer/StoryDialoguePanel");
        npc_interactions=GetNodeOrNull<NpcInteractionPanel>("CanvasLayer/NpcInteractionPanel");
        await JourneyCreationLoadingView.StageAsync(GetTree(), .81, "正在安放百川地貌…");
        _world_node_data = GD.Load<WorldNodeMapData>("res://resources/world/bai_chuan/bai_chuan_locations_20261101.tres");
        if (_world_node_data is null) throw new System.InvalidOperationException("百川节点地点目录加载失败");
        _npc_world=GetNodeOrNull<NpcWorldManager>("/root/NpcWorldManager");
        _npc_world?.initialize_node_world_graph();
        _npc_world?.set_travel_party_capacity_guard(can_fit_player_travel_party);
        if (_npc_world is not null) _npc_world.Connect("world_npcs_changed", Callable.From(on_world_npcs_changed_for_interactions));
        _interactionRoster = GetTree().GetFirstNodeInGroup("cell_character_list") as CellCharacterList;
        if (_interactionRoster is not null) _interactionRoster.RosterClosed += on_interaction_roster_closed;
        await JourneyCreationLoadingView.StageAsync(GetTree(), .85, "正在整理各地人物…");
        restore_npc_save_state();
        restore_node_world_position();
        journal = new JournalPanel { Name = "JournalPanel" };
        GetNode<CanvasLayer>("CanvasLayer").AddChild(journal);
        action_transition = new ActionTransitionPanel { Name = "ActionTransitionPanel" };
        var actionLoadingLayer = new CanvasLayer { Name = "ActionLoadingLayer", Layer = 240, ProcessMode = ProcessModeEnum.Always };
        AddChild(actionLoadingLayer);
        actionLoadingLayer.AddChild(action_transition);
        baigong = new BaigongPanel { Name = "BaigongPanel" };
        GetNode<CanvasLayer>("CanvasLayer").AddChild(baigong);
        _locationCrafting = new LocationCraftingPanel { Name = "LocationCraftingPanel" };
        GetNode<CanvasLayer>("CanvasLayer").AddChild(_locationCrafting);
        location_interior = new WorldLocationInteriorPanel { Name = "WorldLocationInteriorPanel" };
        GetNode<CanvasLayer>("SceneLayer").AddChild(location_interior);
        trade_panel = new TradePaperPanel { Name = "TradePanel" };
        GetNode<CanvasLayer>("CanvasLayer").AddChild(trade_panel);
        if (trade_panel is not null) { trade_panel.Closed += on_trade_closed; trade_panel.TradeCompleted += mark_save_dirty; }
        connect_ui();
        setup_letters();
        setup_banquets();
        if (GetTree().GetFirstNodeInGroup("cell_character_list") is CellCharacterList character_list)
            character_list.configure_side_toggle();
        hud?.set_world_map_available(false);
        await JourneyCreationLoadingView.StageAsync(GetTree(), .89, "正在描摹你的形貌与天资…");
        spawn_or_restore_player();
        _npc_world?.set_player_data(player?.entity_data as HumanData);
        if (player is not null) player.CultivationInspirationChanged += on_player_cultivation_inspiration_changed;
        await JourneyCreationLoadingView.StageAsync(GetTree(), .92, "正在落定入世之处…");
        if (_loaded_game_save is null)
            initialize_story_free_start();
        expire_player_world_statuses();
        if (player is not null) realm_panel?.bind_player(player);
        hud?.bind_player(player);
        storage?.bind_player(player);
        if(player is not null) { player.breakthrough_completed += on_player_breakthrough; player.MainGongfaChanged += (_, _) => refresh_qingxi_quest(); player.GongfaChapterCompleted += (_, _, _, _, _) => refresh_qingxi_quest(); player.GongfaChapterCompleted += on_yuxu_gongfa_chapter_completed; player.GongfaChapterCompleted += on_player_gongfa_chapter_completed; player.ItemAcquired += on_player_item_acquired; player.InventoryChanged += on_player_inventory_save_changed; }
        if(GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager") is { } time) { time.time_changed += on_action_time_changed; time.time_changed += update_yuxu_monthly_allowance; time.time_changed += mark_save_dirty; time.time_changed += on_world_status_time_changed; time.time_changed += on_dynamic_dungeon_time_changed; }
        if (GetNodeOrNull<BattleTransitionManager>("/root/BattleTransitionManager") is { } examTransition)
            examTransition.BriefingCancelled += on_yuxu_exam_briefing_cancelled;
        initialize_yuxu_provisional_exam();
        process_yuxu_provisional_exam_clock();
        await JourneyCreationLoadingView.StageAsync(GetTree(), .95, "正在编排见闻与行囊…");
        refresh_qingxi_quest();
        if (_loaded_game_save is null && _story_free_start && JourneyCreationLoadingView.IsLoading)
        {
            await JourneyCreationLoadingView.StageAsync(GetTree(), .97, "正在安排各地人物与行旅…");
            enter_cloud_state_if_departed();
        }
        if (_loaded_game_save is null) CallDeferred(nameof(show_story_free_start));
        else CallDeferred(nameof(show_loaded_game));
        }
        catch (Exception ex)
        {
            StartupError = ex;
            _skipExitSave = true;
            GD.PushError($"旅程初始化失败：{ex}");
        }
        finally
        {
            StartupComplete = true;
            if (StartupError is null)
                GetNodeOrNull<BattleTransitionManager>("/root/BattleTransitionManager")?.CallDeferred("resume_pending_loot");
        }
    }
    public override void _Process(double delta){if (!StartupComplete || StartupError is not null) return; update_persistent_chrome();process_save_autosave(delta);process_relationship_month();if (_pendingNpcProbe.Count == 0) process_npc_social_visit(delta);process_banquet_activity(delta);process_active_world_npc_interaction_refresh(delta);process_npc_probe_interaction(delta);}
    public override void _ExitTree()
    {
        clear_world_loot_receipts();
        _npc_world?.clear_travel_party_capacity_guard(can_fit_player_travel_party);
        _npc_world?.end_east_woods_route_signal_batch();
        if (!_skipExitSave) save_progress_now(false);
        disconnect_world_npc_interaction_components();
        if (_npc_world is not null && _npc_world.IsConnected("world_npcs_changed", Callable.From(on_world_npcs_changed_for_interactions)))
            _npc_world.Disconnect("world_npcs_changed", Callable.From(on_world_npcs_changed_for_interactions));
        if (_interactionRoster is not null) _interactionRoster.RosterClosed -= on_interaction_roster_closed;
        if (player is not null) player.CultivationInspirationChanged -= on_player_cultivation_inspiration_changed;
        if (player is not null) { player.GongfaChapterCompleted -= on_yuxu_gongfa_chapter_completed; player.GongfaChapterCompleted -= on_player_gongfa_chapter_completed; player.ItemAcquired -= on_player_item_acquired; player.InventoryChanged -= on_player_inventory_save_changed; player.breakthrough_completed -= on_player_breakthrough; }
        if (GetNodeOrNull<BattleTransitionManager>("/root/BattleTransitionManager") is { } examTransition)
            examTransition.BriefingCancelled -= on_yuxu_exam_briefing_cancelled;
        if (GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager") is { } time)
        {
            time.time_changed -= on_action_time_changed;
            time.time_changed -= update_yuxu_monthly_allowance;
            time.time_changed -= mark_save_dirty;
            time.time_changed -= on_world_status_time_changed;
            time.time_changed -= on_dynamic_dungeon_time_changed;
            time.TimeAdvanceStarting -= capture_letter_closure;
        }
        _letterMailbox.Changed -= on_letters_changed;
    }
    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key) return;
        if (key.CtrlPressed && key.Keycode == Key.S)
        {
            save_progress_now(true);
            GetViewport().SetInputAsHandled();
            return;
        }
    }

    public bool try_return_to_previous_page()
    {
        if (action_transition?.IsPlaying == true || GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.IsSmoothAdvanceBusy == true) return true;
        if (try_close_topmost_popup_window()) return true;
        if (has_active_item_claim()) return true;
        if (_yuxuMarket?.HandleBack() == true) return true;
        if (_yuxuManualLibrary?.Visible == true) { _yuxuManualLibrary.Hide(); return true; }
        if (_locationCrafting?.Visible == true) { _locationCrafting.ReturnToLocation(); return true; }
        if (GetNodeOrNull<BattleTransitionManager>("/root/BattleTransitionManager") is { } transition && transition.has_open_flow_view())
            return transition.try_return_from_open_flow();
        if (hud?.close_player_profile() == true) return true;
        if (trade_panel?.Visible == true) { trade_panel.close(); return true; }
        if (story_dialogue?.Visible == true) { story_dialogue.close(); return true; }
        if (npc_interactions?.Visible == true) { npc_interactions.close(); return true; }
        if (_restPanel?.Visible == true) { _restPanel.Close(); return true; }
        if (cultivation_ui?.Visible == true) { cultivation_ui.close_panel(); return true; }
        if (condensation_panel?.Visible == true)
        {
            condensation_panel.TryCloseTopModal();
            return true;
        }
        if (realm_panel?.Visible == true) { realm_panel.Hide(); return true; }
        if (info?.Visible == true) { if (info.dismiss_top_overlay()) return true; info.hide_panel(); return true; }
        if (storage?.Visible == true) { storage.hide_panel(); return true; }
        if (_letters?.Visible == true) { _letters.Close(); return true; }
        if (journal?.Visible == true) { journal.hide_panel(); return true; }
        if (baigong?.Visible == true) { baigong.close(); return true; }
        // Shared overlays are above the current scene. Never return the scene underneath them.
        if (GetNodeOrNull<EastWoodsDungeonPanel>("SceneLayer/EastWoodsDungeonPanel") is { Visible: true } east_woods_dungeon) { east_woods_dungeon.return_one_level(); return true; }
        if (location_interior?.Visible == true)
        {
            // The waking place is the opening's root, not a panel to dismiss into an empty scene.
            if (_baishiLecture is { departed_qingxi: false }) return false;
            if (location_interior.return_one_level()) return true;
            close_world_location(); return true;
        }
        if (GetNodeOrNull<Control>("SceneLayer/YuxuXianzongOverview") is { Visible: true } yuxuOverview)
        {
            if (yuxuOverview.HasMethod("handle_back") && yuxuOverview.Call("handle_back").AsBool()) return true;
            yuxuOverview.Call("close_overview");
            return true;
        }
        // After leaving the prologue, the world is the root page, not a Qingxi overlay.
        if (_nodeWorldMap?.Visible == true) return false;
        return false;
    }

    public bool try_close_topmost_popup_window()
    {
        var windows = GetTree().Root.FindChildren("*", "Window", true, false);
        for (var index = windows.Count - 1; index >= 0; index--)
        {
            if (windows[index] is not Window popup) continue;
            if (!popup.Visible) continue;
            if (popup is ConfirmationDialog confirmation && confirmation.GetCancelButton() is { } cancel)
            {
                cancel.EmitSignal(BaseButton.SignalName.Pressed);
                return true;
            }
            popup.Hide();
            return true;
        }
        return false;
    }

    private void update_persistent_chrome(){var modal=(_locationCrafting?.Visible??false)||(_restPanel?.Visible??false)||(storage?.Visible??false)||(info?.Visible??false)||(realm_panel?.Visible??false)||(cultivation_ui?.Visible??false)||(condensation_panel?.Visible??false)||(story_dialogue?.Visible??false)||(journal?.Visible??false)||(baigong?.Visible??false)||(trade_panel?.Visible??false)||(_letters?.Visible??false)||(hud?.player_profile_visible??false);var visible=!modal&&!_suppress_persistent_chrome;hud?.set_estate_summary_visible(visible);if(GetNodeOrNull<Control>("HUDLayer/WorldInfo") is { } timePanel)timePanel.Visible=visible;}
    private void connect_ui()
    {
        if(hud is not null)
        {
            hud.CharacterRequested+=open_player_profile;
            hud.PlayerStatusRequested+=open_player_status_profile;
            hud.RumorsRequested+=open_rumors;
            hud.LettersRequested+=open_letters;
            hud.CraftRequested+=()=> { if(player is not null) baigong?.show_for(player); };
            hud.InventoryRequested+=open_storage;
            hud.RealmRequested+=()=> { if(player is not null) realm_panel?.show_for(player); };
            hud.CultivationRequested+=open_cultivation_overview;
            hud.WorldMapRequested+=open_world;
            hud.TaskRequested+=open_journal_task;
        }
        if (journal is not null) journal.TrackingRequested += OnJournalTrackingRequested;
        if (info is not null)
        {
            info.NpcTradeRequested += open_world_npc_trade;
            info.NpcTalkRequested += open_world_npc_talk;
            info.NpcSparRequested += npc => request_npc_spar(npc);
            info.NpcPurchaseRequested += npc => request_npc_purchase(npc);
            info.NpcSparAvailable = can_sect_spar;
            info.NpcPurchaseAvailable = can_npc_purchase;
        }
        if(cultivation_ui is not null) { cultivation_ui.CondensationRequested+=open_condensation; cultivation_ui.CondensationTestRequested+=open_condensation_test; }
        if(condensation_panel is not null) { condensation_panel.Closed+=close_condensation; condensation_panel.CondensationFinished+=on_condensation_finished; }
        if (_world_node_data is { } mapData) _npc_world?.set_world_map(mapData);
        if (location_interior is not null)
        {
            location_interior.Closed += close_world_location;
            location_interior.SubsceneEntered += on_baishi_subscene_entered;
            location_interior.DynamicDungeonEntryRequested += on_dynamic_dungeon_enter_requested;
            location_interior.BaishiLocationActionRequested += on_baishi_location_action;
            location_interior.QingxiOpeningActionRequested += on_qingxi_opening_action;
        }
        if (GetNodeOrNull<Node>("SceneLayer/YuxuXianzongOverview") is { } yuxu && yuxu.HasSignal("overview_closed"))
            yuxu.Connect("overview_closed", Callable.From(on_yuxu_overview_closed));
        if (GetNodeOrNull<Node>("SceneLayer/YuxuXianzongOverview") is { } yuxuPeak && yuxuPeak.HasSignal("peak_selected"))
            yuxuPeak.Connect("peak_selected", Callable.From<string>(on_yuxu_peak_selected));
        if (GetNodeOrNull<Node>("SceneLayer/YuxuXianzongOverview") is { } yuxuAdmission && yuxuAdmission.HasSignal("admission_requested"))
            yuxuAdmission.Connect("admission_requested", Callable.From(on_yuxu_admission_requested));
        if (GetNodeOrNull<Node>("SceneLayer/YuxuXianzongOverview") is { } yuxuLife && yuxuLife.HasSignal("life_action_requested"))
            yuxuLife.Connect("life_action_requested", Callable.From<string, string>(on_yuxu_life_action_requested));
        if (GetNodeOrNull<Node>("SceneLayer/YuxuXianzongOverview") is { } yuxuExchange && yuxuExchange.HasSignal("exchange_purchase_requested"))
            yuxuExchange.Connect("exchange_purchase_requested", Callable.From<string, int>(on_yuxu_exchange_purchase_requested));
    }
    private void spawn_family(){if(family.Count>0)return;var generator=new CharacterGenerator();var names=new NameGenerator();var profile=GetNodeOrNull<GameStartProfile>("/root/GameStartProfile");string FamilyName(string given,int generation)=>profile?.compose_family_name(given,generation)??"李"+given;var fatherGiven=names.generate_single_given(0);var motherName=names.generate_single_full(1);while(motherName.StartsWith(profile?.surname??"李"))motherName=names.generate_single_full(1);var son1=names.generate_single_given(0);var son2=names.generate_single_given(0);while(son2==son1)son2=names.generate_single_given(0);var son3=names.generate_single_given(0);while(son3==son1||son3==son2)son3=names.generate_single_given(0);var daughter=names.generate_single_given(1);var memberNames=new[]{FamilyName(fatherGiven,1),motherName,FamilyName(son1,2),FamilyName(son2,2),FamilyName(son3,2),FamilyName(daughter,2)};var ages=new[]{38,37,18,16,14,12};for(var i=0;i<memberNames.Length;i++){var data=generator.generate(i==0?"云息":"凡人",(int)WorldEntity.Faction.PLAYER);if(i==0){data.main_gongfa_path=GongfaAvailability.DefaultBookPath;data.learned_gongfa_paths.Add(data.main_gongfa_path);}if(i==4)profile?.apply_to_youngest(data);data.entity_name=memberNames[i];data.gender=i is 1 or 5?1:0;data.current_age=ages[i];data.faction_name=profile?.family_name??"本家";if(i==0)data.spouse_name=motherName;else if(i==1)data.spouse_name=memberNames[0];var character=new Character();character.load_from_data(data);character.entity_type=WorldEntity.EntityType.CHARACTER;character.faction=(int)WorldEntity.Faction.PLAYER;character.is_family=true;character.family_gen=i<2?1:2;character.family_gender=i is 1 or 5?"女":"男";family.Add(character);AddChild(character);if(i==0)character.cultivation_realm="云息六层";}}
    private void seed_retainers()
    {
        // 外姓人将在“人事”页统一生成、招募和派任；这里保留初始班底的接入点。
        if (retainers.Count > 0) return;
    }
    private void spawn_player()
    {
        if (player is not null) return;
        var profile = GetNodeOrNull<GameStartProfile>("/root/GameStartProfile");
        var data = new CharacterGenerator().generate("凡人", (int)WorldEntity.Faction.PLAYER);
        profile?.apply_to_youngest(data);
        var generated_lifespan = Math.Max(10, data.max_age - data.current_age);
        data.current_age = 16;
        data.max_age = data.current_age + generated_lifespan;
        data.entity_name = string.IsNullOrWhiteSpace(data.entity_name) ? "苏醒者" : data.entity_name;
        player = new Character();
        player.load_from_data(data);
        player.entity_type = WorldEntity.EntityType.CHARACTER;
        player.faction = (int)WorldEntity.Faction.PLAYER;
        AddChild(player);
        // Character._Ready performs inventory/clothing normalization after load_from_data.
        // Reapply the confirmed creation appearance to the final player resource before
        // the opening dialogue renders its portrait.
        if (player.entity_data is HumanData createdPlayer) profile?.apply_player_appearance(createdPlayer);
    }
    public Character? get_player_character() => player;

    private void open_location_map()
    {
        if (_baishiLecture is { departed_qingxi: false }) show_baishi_opening();
        else open_world();
    }

    private Godot.Collections.Array family_array(){var result=new Godot.Collections.Array();foreach(var character in family)result.Add(character);return result;}
    private Godot.Collections.Array roster_array(){var result=family_array();foreach(var character in retainers)result.Add(character);return result;}
    private void open_player_profile(){if(player is not null)show_character(player);}
    private void open_player_status_profile()
    {
        if (player is null) return;
        hud?.show_player_profile(player, get_current_profile_location());
    }
    private void open_cultivation_overview()
    {
        if(player is not null)cultivation_ui?.show_cultivation_overview(player);
    }
    private void open_condensation(Character target)
    {
        if(condensation_panel is null || !GodotObject.IsInstanceValid(target)) return;
        if (target != player) return;
        if (!target.can_attempt_condensation_breakthrough())
        {
            cultivation_ui?.show_entity(target);
            return;
        }
        cultivation_ui?.close_panel();
        condensation_panel.ShowFor(target, false);
    }
    private void open_condensation_test(Character target)
    {
        if(condensation_panel is null || !GodotObject.IsInstanceValid(target)) return;
        cultivation_ui?.close_panel();
        condensation_panel.ShowFor(target, true);
    }
    private void close_condensation()
    {
        condensation_panel?.Hide();
        if(player is not null) cultivation_ui?.show_entity(player);
    }
    private void on_condensation_finished(Character target, bool success, int finalScore)
    {
        if (target != player) return;
        if (success)
        {
            if (target.complete_condensation(finalScore))
            {
                condensation_panel?.SuspendResultForRealmPresentation();
                mark_save_dirty();
                refresh_qingxi_quest();
            }
            else show_message("凝核条件已变化", "人物条件已变化，本次未写入境界或凝核结果。请重新查看突破条件。 ");
            return;
        }
        if (!apply_player_severe_injury_from_condensation_failure())
            show_message("凝核失败", "本次没有扣除灵力，但重伤状态未能写入。请重新查看人物状态。 ");
    }
    public void open_world_npc_actions(WorldNpcData npc)
    {
        if (!GodotObject.IsInstanceValid(npc)) return;
        if (_nodeWorldMap?.IsVisibleInTree() == true && _nodeWorldMap.HasMethod("stop_free_travel"))
            _nodeWorldMap.Call("stop_free_travel");
        var name = string.IsNullOrWhiteSpace(npc.npc_name) ? "无名修士" : npc.npc_name;
        _activeWorldNpcInteractionId = npc.npc_id;
        var selectedId = npc.npc_id;
        WorldNpcData? ResolveTarget() => baishi_present_actor(selectedId) ?? _npc_world?.get_npc(selectedId);
        npc_interactions?.show_npc_page(npc, ResolveTarget, () =>
        {
            var current = ResolveTarget();
            return current is not null && GodotObject.IsInstanceValid(current)
                ? collect_world_npc_interaction_actions(current).ToArray()
                : System.Array.Empty<NpcInteractionAction>();
        });
    }
    private void refresh_qingxi_quest()
    {
        refresh_baishi_progress();
        RefreshJournalTracker();
        mark_save_dirty();
    }

    private List<JournalPanel.Entry> get_quest_entries()
    {
        for (var i = 0; i < _completed_quests.Count; i++) _completed_quests[i] = IdentifyJournalTask(_completed_quests[i]);
        var entries = new List<JournalPanel.Entry>();
        // 报到已有明确的当前任务；避免阶段目标和当前任务重复占用两行。
        if (player?.entity_data is not HumanData { sect_yuxu_registration_pending: true, sect_affiliation_id: "" })
            entries.Add(get_phase_goal());
        if (_baishiLecture is not null) entries.Add(baishi_current_task());
        if ((_baishiLecture is null || _baishiLecture.finished) && get_yuxu_registration_journal_task() is { } registration) entries.Add(registration);
        entries.AddRange(get_yuxu_provisional_exam_tasks());
        if (has_open_world_access()) entries.Add(get_cultivation_guidance_task());
        entries.AddRange(get_yuxu_lineage_journal_tasks());
        if (get_active_yuxu_true_disciple_lecture_journal_task() is { } lecture) entries.Add(lecture);
        if (get_active_yuxu_journal_task() is {} duty) entries.Add(duty);
        entries.AddRange(get_letter_invitation_tasks());
        if (player?.entity_data is HumanData human)
            entries.AddRange(human.sect_contribution_records.Where(r => r.transaction_kind == YuxuSectContributionRecord.KindOfficialDutyReward)
                .Reverse().Select(r => new JournalPanel.Entry(r.source_title, r.cost_summary + "\n" + r.result_summary, "玉虚仙宗·宗门公事", "玉虚仙宗", "已完成", r.occurred_at, "yuxu.settled." + r.source_transaction_id)));
        entries.AddRange(_completed_quests.AsEnumerable().Reverse());
        return entries.Select(IdentifyJournalTask).ToList();
    }

    private JournalPanel.Entry get_cultivation_guidance_task()
    {
        var realm = player?.cultivation_realm ?? "凡人";
        var tier = RealmManager.get_cloud_realm_tier(realm);

        if (realm == "凡人" && player?.entity_data is HumanData { sect_affiliation_id: YuxuSectId })
            return new("引气入道", "踏入云息境。", "长期主线", "个人修行", "进行中", Id: "cultivation.enter_yunxi");

        if (tier is >= 1 and < 6)
            return new("精进修为", $"当前境界：{realm}。继续修行与历练，先达到云息六层，为凝核作准备。", "长期主线", "个人修行", "进行中");
        if (tier == 6)
            return new("凝核准备", "你已修至云息六层。六层至七层是统一凝核节点；查看修炼界面的突破条件，准备进入凝核牌局。", "长期主线", "个人修行", "进行中");
        if (tier is 7 or 8)
            return new("稳固灵核", $"当前境界：{realm}。继续养护灵核并精进修为，目标是云息九层。", "长期主线", "个人修行", "进行中");
        if (tier == 9)
            return new("晋入云息十层", "你已修至云息九层。积累足够灵力、保持有效主修，并备妥归元定息丹后，可在静室闭关突破。晋入十层后开始归元九息。", "长期主线", "个人修行", "进行中");
		if (tier >= 10 && player?.is_yunxi_complete() == true)
			return new("准备铸玄基", "归元九息已成，云息十层圆满。铸玄基后的修行内容暂未开放。", "长期主线", "个人修行", "进行中");
		if (tier >= 10)
			return new("归元养息", "云息十层之后，继续修习归元九息。九息圆满后，便可准备铸玄基；归元息不是新的境界层数。", "长期主线", "个人修行", "进行中");
        return new("继续精进", $"当前境界：{realm}。继续修行，在修炼界面查看当前开放的境界目标与突破条件。", "长期主线", "个人修行", "进行中");
    }

    private JournalPanel.Entry get_phase_goal() => player?.entity_data is HumanData { sect_yuxu_registration_pending: true, sect_affiliation_id: "" }
        ? new("玉虚问道", "在观复峰办理入门。", "阶段目标", "玉虚仙宗", "进行中", Id: "phase.yuxu_registration")
        : _baishiLecture is { finished: false }
        ? (_baishiLecture.met_fang
            ? new("白石问道", "前往白石城，听讲法并查验灵根。", "阶段目标", "白石城", "进行中", Id: "phase.baishi")
            : new("身在何处", "弄清自己的处境。", "阶段目标", "溪边野地", "进行中", Id: "phase.baishi"))
        : new("行旅与积累", "在百川区域自由选择修炼、交易、旅行、探索、委托或接触势力，积累下一次境界突破所需的资源与条件。", "阶段目标", "百川区域", "进行中");

    private void open_journal_task(string title)
    {
        if (journal is null) return;
        refresh_journal_entries();
        journal.show_panel(JournalPanel.Section.Tasks, title);
    }
    private void on_player_breakthrough(Character character, string _)
    {
        on_yuxu_exam_breakthrough(character);
        refresh_qingxi_quest();
        refresh_player_world_status_ui();
        hud?.show_event_notification("境界精进", $"修为已达 {player?.cultivation_realm ?? "新境界"}。", "修行");
    }

    private void on_player_gongfa_chapter_completed(Character character, string bookName, string _, string chapterName, Godot.Collections.Dictionary appliedBonuses)
    {
        if (character != player) return;
        on_yuxu_exam_chapter_completed(character, bookName);
        var bonusText = appliedBonuses.Count == 0
            ? "本章修习已记入功法谱。"
            : "永久所得：" + string.Join("、", appliedBonuses.Keys.Select(key =>
            {
                var attribute = key.AsString();
                var name = attribute switch
                {
                    "physique" => "体魄", "intelligence" => "灵慧", "coordination" => "身心协调值", "soul" => "神魂",
                    "fortune" => "福缘", "comprehension" => "悟性", "body_proficiency" => "体道造诣",
                    "magic_proficiency" => "术法造诣", "sword_proficiency" => "剑道造诣", "blade_proficiency" => "刀道造诣",
                    "spear_proficiency" => "枪道造诣", "forging_proficiency" => "器道造诣", "talisman_proficiency" => "符道造诣",
                    "formation_proficiency" => "阵道造诣", "alchemy_proficiency" => "丹道造诣", _ => attribute
                };
                return $"{name} +{appliedBonuses[key].AsDouble():0.##}";
            }));
        hud?.show_event_notification("研读完成", $"《{bookName}》· {chapterName}\n{bonusText}", "修行");
    }

    private void on_player_item_acquired(Character character, ItemData item, int count)
    {
        if (character != player || item is null || count <= 0) return;
        if (character.entity_data is HumanData acquired && item.gongfa is GongfaResource fullBook && acquired.baishi_first_volume_path == fullBook.ResourcePath)
            acquired.baishi_first_volume_only = false;
        var description = string.IsNullOrWhiteSpace(item.description) ? "已收入行囊。" : item.description.Trim();
        hud?.show_event_notification("收获入囊", $"{item.entity_name} ×{count} · {item.item_category}\n{description}", "物资");
    }
    private void open_general_rest_dialog(string restPlace, Action? afterRest = null)
    {
        if (player is null || GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager") is not { } time) return;
        open_rest_panel(restPlace, count =>
        {
            var end = time.get_total_game_hours() + count; var day = end / 24;
            var ending = $"{day / 360 + 1}年{day % 360 / 30 + 1}月{day % 30 + 1}日";
            run_time_transition("休息", $"你将在{restPlace}休息 {GameTimeText.Duration(count)}，醒来日期：{ending}。",
                "休养过后，你的气血已复，行旅精力也得以恢复。",
                () => { if (player is null) return; player.current_health = player.max_health; player.restore_world_stamina(count * WorldStaminaRules.RestRecoveryPerGameHour); advance_player_world_status_rest(count); time.add_hours(count); afterRest?.Invoke(); },
                () => show_message("醒来", "休养过后，你的气血已复，行旅精力也得以恢复。"));
        });
    }
    private void run_time_transition(string title,string plan,string complete,Action settle,Action? after=null)
    {
        if(action_transition is not null){action_transition.play(title,plan,complete,settle,after);return;}
        settle();after?.Invoke();
    }
    private void open_rumors()
    {
        if (journal is null) return;
        refresh_journal_entries();
        _journal_unread=0; hud?.set_journal_unread_count(0);
        var peakTitle = string.IsNullOrEmpty(_player_current_yuxu_peak_id) ? "" : get_yuxu_peak_name(_player_current_yuxu_peak_id);
        var hasPeakEntry = peakTitle.Length > 0 && _discovered_yuxu_peak_entries.Any(entry => entry.Title == peakTitle);
        journal.show_panel(hasPeakEntry ? JournalPanel.Section.Journal : JournalPanel.Section.Rumors, hasPeakEntry ? peakTitle : null);
    }

    private void refresh_journal_entries()
    {
        if (journal is null) return;
        // Saved records remain readable; retired prologue events are never fabricated.
        journal.set_entries(JournalPanel.Section.Rumors, _earned_rumors);
        journal.set_entries(JournalPanel.Section.Journal, _discovered_yuxu_peak_entries);
        journal.set_entries(JournalPanel.Section.People, []);
        journal.set_entries(JournalPanel.Section.Events, []);
        journal.set_entries(JournalPanel.Section.Tasks, get_quest_entries());
    }

    // 见闻只由玩家主动消耗时间的行动触发；没有自然流逝，也不会用弹窗打断结算。
    private void on_action_time_changed()
    {
        info?.refresh_current_age();
        process_yuxu_provisional_exam_clock();
        if (_baishiLecture is not null) { refresh_baishi_progress(); RefreshJournalTracker(); }
        if (player?.entity_data is HumanData human && human.sect_affiliation_id == YuxuSectId)
        {
            sync_yuxu_true_disciple_lecture_schedule(human);
            refresh_yuxu_true_disciple_lecture_context(human, is_yuxu_true_disciple(human.sect_current_rank));
        }
        refresh_letter_invitation_tasks_if_changed();
    }

    private void open_storage(){storage?.show_panel();storage?.MoveToFront();}
    public void open_world_npc_trade(WorldNpcData npc)
    {
        var contactFailure = social_contact_failure_reason(npc);
        if (contactFailure.Length > 0)
        {
            npc_interactions?.close();
            show_message(npc?.npc_name ?? "暂不能交易", contactFailure);
            return;
        }
        if (is_npc_in_seclusion_now(npc)) { explain_npc_seclusion(npc); return; }
        if (player is null || trade_panel is null)
        {
            show_message(npc.npc_name, player is null ? "当前没有可以进行交易的角色。" : "交易界面尚未准备就绪，请稍后再试。");
            return;
        }
        _trade_hud_was_visible = hud?.Visible == true;
        info?.hide_panel();
        npc_interactions?.close();
        hud?.Hide();
        trade_panel.show_for(player, npc);
    }
    private void on_trade_closed()
    {
        if (_trade_hud_was_visible) hud?.Show();
        _trade_hud_was_visible = false;
        mark_save_dirty();
    }
    public void open_world_npc_talk(WorldNpcData npc)
    {
        if (try_talk_baishi_actor(npc)) return;
        begin_npc_social_talk(npc, false);
    }
    private void open_world()
    {
        _baishiLectureSpaceOpen = false;
        hud?.set_paper_scene(false);
        if (_east_woods_dungeon?.Visible == true)
        {
            _east_woods_dungeon.locate_exit();
            return;
        }
        if(!has_open_world_access()) return;
        enter_cloud_state_if_departed();
        var yuxuOverview = GetNodeOrNull<Control>("SceneLayer/YuxuXianzongOverview");
        if (yuxuOverview?.Visible == true)
        {
            _world_open_in_progress = true;
            try { yuxuOverview.Call("close_overview"); }
            finally { _world_open_in_progress = false; }
        }
        clear_yuxu_peak_location();
        location_interior?.hide_roster();
        location_interior?.Hide();

        _nodeWorldMap?.Hide();
        show_node_world_map();
        hud?.Show();
        hud?.MoveToFront();
        hud?.set_world_map_available(has_open_world_access());
    }

    /// <summary>开启自由世界状态时保留当前有效地点；正常序章离村仍沿用青溪里锚点。</summary>
    private void enter_cloud_state_if_departed()
    {
        if (!has_open_world_access() || _cloud_state_initialized) return;
        if (_world_node_data is not { } mapData) return;
        var world = _npc_world ?? GetNodeOrNull<NpcWorldManager>("/root/NpcWorldManager");
        if (world is not null) CloudStateNpcCatalog.activate(world, mapData);
        _cloud_state_initialized = true;
    }
    private void close_world_map()
    {
        // There is no location page behind the world after departure. Keep its camera and travel intact.
        if (has_open_world_access()) return;
        _nodeWorldMap?.Hide();
        hide_world_character_list();

        open_location_map();
    }

    /// <summary>打开当前节点的内部地点界面；按钮只会由地图当前节点发出。</summary>
    private void on_node_enter_requested(string node_id)
    {
        if (_world_node_data is not { } data || data.get_node(node_id) is not { } node)
            return;
        if (node_id != _player_world_node_id || !is_world_location_unlocked(node_id) || !node.enterable)
            return;
        if (has_open_world_access() && node_id == "home_village")
        {
            show_message("旧地不可入", "青溪里已经留在身后。你只能沿着新的道路继续前行。");
            return;
        }

        hide_world_character_list();

        _nodeWorldMap?.Hide();

        if (node_id == "yuxu_xianzong_core")
        {
            if (player?.entity_data is HumanData human && human.sect_affiliation_id == YuxuSectId)
                open_faction_overview_if_available(node_id);
            else if (player?.entity_data is HumanData { sect_yuxu_registration_pending: true, sect_affiliation_id: "" })
                open_yuxu_peak_after_admission("guanfu");
            else
                open_yuxu_gate_entrance();
            return;
        }
        if (node_id == "east_woods")
        {
            open_east_woods_dungeon();
            return;
        }
        if (DynamicDungeonExpedition.FindInstanceByWorldNodeId(_dynamic_dungeon_state, node_id) is { } towerInstance)
        {
            location_interior?.show_dynamic_dungeon(node, _npc_world,
                towerInstance.GetValueOrDefault("instance_id", "").AsString(),
                DynamicDungeonExpedition.ExpectedRealmRange(towerInstance));
            return;
        }
        if (node_id == BaishiNodeId)
        {
            _baishiLectureSpaceOpen = false;
            hud?.set_paper_scene(true);
        }
        location_interior?.show_for(node, _npc_world);
    }

    private void close_world_location()
    {
        if (_baishiLecture is { departed_qingxi: false }) { show_baishi_opening(); return; }
        location_interior?.hide_roster();
        location_interior?.Hide();
        open_world();
    }

    private void on_yuxu_overview_closed()
    {
        clear_yuxu_peak_location();
        if (_world_open_in_progress) return;
        open_world();
    }

    private void open_faction_overview_if_available(string node_id)
    {
        if (node_id != "yuxu_xianzong_core") return;
        var overview = GetNodeOrNull<Node>("SceneLayer/YuxuXianzongOverview");
        refresh_yuxu_admission_context(overview);
        overview?.Call("open_overview");
        overview?.GetNodeOrNull<Control>("CloseButton")?.Show();
    }

    private int get_player_spirit_root_count()
    {
        if (player?.entity_data is not HumanData human) return 0;
        return human.root_weights.Count(weight => weight.AsInt32() > 0);
    }

    private static string get_yuxu_entry_rank(int rootCount) => rootCount switch
    {
        1 => "预备真传",
        2 => "内门弟子",
        3 => "外门弟子",
        4 or 5 => "杂役",
        _ => ""
    };

    private void refresh_yuxu_admission_context(Node? overview = null)
    {
        overview ??= GetNodeOrNull<Node>("SceneLayer/YuxuXianzongOverview");
        if (overview is null || player?.entity_data is not HumanData human) return;
        update_yuxu_monthly_allowance();
        overview.Call("set_admission_context", human.spirit_root_name, get_recorded_yuxu_admission_rank(human), human.sect_affiliation_id == "yuxu_xianzong", human.sect_current_rank, !string.IsNullOrEmpty(human.sect_affiliation_id) && human.sect_affiliation_id != "yuxu_xianzong");
        overview.GetNodeOrNull<Node>("PeakInterior")?.Call("set_registration_context", human.sect_yuxu_registration_pending && string.IsNullOrEmpty(human.sect_affiliation_id));
    }

    private void on_yuxu_admission_requested()
    {
        begin_yuxu_gate_welcome();
    }

    private void show_character(GodotObject character){if(character is not WorldEntity entity)return;info?.show_entity(entity);if(player is not null&&entity==player)hud?.set_guided_button(null);}
    private void advance_to_next_month(){GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.slumber(30);}
    public bool has_available_meditation_room(Character? except=null)
    {
        var rooms=estate_resources?.get_facility_count("meditation_room")??0;
        var occupied=family.Count(member=>member!=except&&GodotObject.IsInstanceValid(member)&&member.meditation_room_index>=0);
        return occupied<rooms;
    }
    public bool assign_available_meditation_room(Character member)
    {
        var rooms=estate_resources?.get_facility_count("meditation_room")??0;
        for(var room=0;room<rooms;room++) if(!family.Any(other=>other!=member&&GodotObject.IsInstanceValid(other)&&other.meditation_room_index==room)) return member.assign_meditation_room(room);
        return false;
    }
    public string get_meditation_room_status()
    {
        var rooms=estate_resources?.get_facility_count("meditation_room")??0; var occupied=family.Count(member=>GodotObject.IsInstanceValid(member)&&member.meditation_room_index>=0);
        return rooms==0?"尚无静室。族人仍可于宅院闭关，但无法获得静室加速。":$"静室 {rooms} 间，闭关中 {occupied} 人，可用 {Math.Max(0,rooms-occupied)} 间。";
    }
    public bool add_item_to_inventory(string item_path,int count=1)=>player?.add_item_to_inventory(item_path,count)??false;
    // 旧调用点保留兼容名，但运行时统一写入当前玩家行囊。
    public bool add_item_to_warehouse(string item_path,int count=1)=>add_item_to_inventory(item_path,count);
    public bool remove_item_from_inventory(string item_path,int count=1)=>player?.remove_item_from_inventory(item_path,count)??false;
    public bool remove_item_from_warehouse(string item_path,int count=1)=>remove_item_from_inventory(item_path,count);
    public int get_inventory_item_count(string item_path)=>player?.get_inventory_total_count(item_path)??0;
    public int get_warehouse_item_count(string item_path)=>get_inventory_item_count(item_path);
    public string get_resource_item_path(string resource_id)=>ResourceItems.GetValueOrDefault(resource_id,"");
    public int get_estate_resource_amount(string resource_id)
    {
        var path = get_resource_item_path(resource_id);
        return !string.IsNullOrEmpty(path) && ResourceLoader.Load<ItemData>(path) is not null ? get_inventory_item_count(path) : estate_resources?.get_amount(resource_id) ?? 0;
    }
    public int consume_estate_resource(string resource_id,int amount)
    {
        if (amount <= 0) return 0;
        var path = get_resource_item_path(resource_id);
        if (!string.IsNullOrEmpty(path) && ResourceLoader.Load<ItemData>(path) is not null)
        {
            var paid = Math.Min(amount, get_inventory_item_count(path));
            return paid > 0 && remove_item_from_inventory(path, paid) ? paid : 0;
        }
        if (estate_resources is null) return 0;
        var ledgerPaid = Math.Min(amount, estate_resources.get_amount(resource_id));
        if (ledgerPaid > 0) estate_resources.spend(resource_id,ledgerPaid);
        return ledgerPaid;
    }
    public bool can_afford_building(BuildingData data)
    {
        if (player is null) return false;
        for (var i=0; i<building_cost_count(data); i++)
        {
            var amount = i<data.build_cost_counts.Length ? data.build_cost_counts[i] : 1;
            var path = i < data.build_cost_item_paths.Length ? data.build_cost_item_paths[i] : "";
            if (!string.IsNullOrEmpty(path))
            {
                if (!player.has_inventory_item(path, amount)) return false;
                continue;
            }
            if (i >= data.build_cost_resource_ids.Length) return false;
            var resourceId = data.build_cost_resource_ids[i];
            var resourceItemPath = get_resource_item_path(resourceId);
            if (!string.IsNullOrEmpty(resourceItemPath) && ResourceLoader.Load<ItemData>(resourceItemPath) is not null)
            {
                if (!player.has_inventory_item(resourceItemPath, amount)) return false;
            }
            else if (estate_resources?.can_afford(resourceId, amount) != true) return false;
        }
        return true;
    }
    public bool consume_building_cost(BuildingData data)
    {
        if (!can_afford_building(data) || player is null) return false;
        var removedItems = new List<(string path,int amount)>();
        var spentResources = new List<(string resourceId,int amount)>();
        for (var i=0; i<building_cost_count(data); i++)
        {
            var amount = i<data.build_cost_counts.Length ? data.build_cost_counts[i] : 1;
            var path = i < data.build_cost_item_paths.Length ? data.build_cost_item_paths[i] : "";
            var resourceId = i < data.build_cost_resource_ids.Length ? data.build_cost_resource_ids[i] : "";
            if (string.IsNullOrEmpty(path))
            {
                var resourceItemPath = get_resource_item_path(resourceId);
                if (!string.IsNullOrEmpty(resourceItemPath) && ResourceLoader.Load<ItemData>(resourceItemPath) is not null) path = resourceItemPath;
            }
            var paid = !string.IsNullOrEmpty(path)
                ? player.remove_item_from_inventory(path, amount)
                : !string.IsNullOrEmpty(resourceId) && estate_resources?.spend(resourceId, amount) == true;
            if (!paid)
            {
                foreach (var (rollbackPath,rollbackAmount) in removedItems) player.add_item_to_inventory_silently(rollbackPath,rollbackAmount);
                foreach (var (rollbackResourceId,rollbackAmount) in spentResources) estate_resources?.add(rollbackResourceId,rollbackAmount);
                return false;
            }
            if (!string.IsNullOrEmpty(path)) removedItems.Add((path, amount));
            else spentResources.Add((resourceId, amount));
        }
        return true;
    }
    public void register_built_building(BuildingData data){if(string.IsNullOrEmpty(data.estate_facility_id))return;estate_resources?.register_facility(data.estate_facility_id);}
    private void RefreshCultivationEnvironment(){const float pressure=100f;foreach(var member in family)if(GodotObject.IsInstanceValid(member))member.ambient_lingqi_pressure=pressure;}
    private static int building_cost_count(BuildingData data)=>Math.Max(data.build_cost_item_paths.Length,data.build_cost_resource_ids.Length);
    public Godot.Collections.Array get_village_home_characters(){return roster_array();}
    public Godot.Collections.Array get_player_characters()=>get_village_home_characters(); public void process_character_labor(Character character,float hours){if(!GodotObject.IsInstanceValid(character)||hours<=0)return;character.spend_stamina(Mathf.CeilToInt(hours));}
    public void show_village_building_info(Vector2I _,Building building)=>show_message(building.entity_name,building.entity_data is BuildingData data?data.summary:"");
    public void mark_node_npc_contacted(string _){} public void begin_character_contact(WorldEntity target,Godot.Collections.Array speakers){show_character(target);}
    public void show_battle_entry_blocked(string reason)=>show_message("暂不能进入战斗",reason);
    private void show_message(string title,string body,Action? on_closed=null)
    {
        story_dialogue?.hide_speaker_portrait();story_dialogue?.show_narration(body,on_closed);
    }
    private void show_dialogue_sequence(params (DialogueBeatKind kind,string speaker,string body)[] beats)=>show_dialogue_sequence(null,beats);
    private void show_dialogue_sequence(Action? on_completed,params (DialogueBeatKind kind,string speaker,string body)[] beats)
    {
        void show_next(int index)
        {
            if(index>=beats.Length){on_completed?.Invoke();return;}
            var beat=beats[index];
            Action next=()=>show_next(index+1);
            switch(beat.kind)
            {
                case DialogueBeatKind.NpcSpeech:show_npc_dialogue(beat.speaker,beat.body,next);break;
                case DialogueBeatKind.PlayerSpeech:if(player is not null) story_dialogue?.show_player_speech(player.entity_name,beat.body,player,next);else story_dialogue?.show_player_speech("我",beat.body,next);break;
                case DialogueBeatKind.PlayerThought:show_player_thought(beat.body,next);break;
                default:story_dialogue?.hide_speaker_portrait();story_dialogue?.show_narration(beat.body,next);break;
            }
        }
        show_next(0);
    }
    private void show_npc_dialogue(string speaker,string body,Action? on_closed=null)=>show_npc_dialogue(speaker,body,resolve_scripted_dialogue_npc(speaker),on_closed);
    private void show_npc_dialogue(string speaker,string body,WorldNpcData? npc,Action? on_closed=null)
    {
        if(npc is not null) story_dialogue?.show_npc_speech(speaker,body,npc,on_closed);
        else story_dialogue?.show_npc_speech(speaker,body,on_closed);
    }
    private WorldNpcData? resolve_scripted_dialogue_npc(string speaker)
    {
        if (_yuxuAdmissionInProgress && player?.entity_data is HumanData human &&
            _npc_world?.get_npc(human.sect_admission_guide_npc_id) is { } admissionGuide && admissionGuide.npc_name == speaker)
            return admissionGuide;
        if (_baishiLecture?.companion.npc_name == speaker) return _baishiLecture.companion;
        if (_baishiLecture?.host_snapshot?.npc_name == speaker) return _baishiLecture.host_snapshot;
        if (_baishiLecture?.representatives.FirstOrDefault(n => n.npc_name == speaker) is { } representative) return representative;
        return null;
    }
    private void show_player_thought(string body,Action? on_closed=null)
    {
        if(player is not null) story_dialogue?.show_player_thought(player.entity_name,body,player,on_closed);
        else story_dialogue?.show_player_thought("我",body,on_closed);
    }
}
