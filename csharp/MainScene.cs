using Godot;

/// <summary>主场景协调器：建立单个苏醒者、连接个人 HUD 与世界节点地图。</summary>
public partial class MainScene : Node2D
{
    // Main scene coordinates world actions and stamina settlement.
    private enum QingxiPrologueStep { Awakened, MetLiFamily, BeastHuntResolved, Departed }
    private enum DialogueBeatKind { Narration, PlayerSpeech, PlayerThought, NpcSpeech }
    private static readonly System.Collections.Generic.Dictionary<string,string> ResourceItems=new()
    {
        ["mat_bitter_moss_root"]="res://resources/entities/items/苦苔根.tres",
        ["mat_spirit_spring_algae"]="res://resources/entities/items/灵泉藻.tres",
        ["spirit_stones"]="res://resources/entities/items/灵石.tres"
    };
    private MainHUD? hud; private GameTimeUI? time_ui; private Button? _prologue_skip_button; private bool _prologue_skip_available; private StoragePanel? storage; private EstateResourceSystem? estate_resources; private InfoPanel? info; private CultivationUI? cultivation_ui; private CondensationCardGamePanel? condensation_panel; private RealmPanel? realm_panel; private StoryDialoguePanel? story_dialogue; private NpcInteractionPanel? npc_interactions; private TradePanel? trade_panel; private PrologueGuide? prologue_guide; private WorldNodeMapData? _world_node_data; private WorldLocationInteriorPanel? location_interior; private EstateOverview? estate; private DungeonExploreUI? back_hill_trial; private QingxiTerritoryState? territory_state; private QingxiTerritoryPanel? territory_panel; private QingxiFarmlandPanel? farmland_panel; private FamilyResidencePanel? residence_panel; private TerritoryLedgerPanel? territory_ledger; private OpeningIntro? opening_intro; private JournalPanel? journal; private BaigongPanel? baigong; private ActionTransitionPanel? action_transition; private bool _suppress_persistent_chrome; private Character? player; private QingxiPrologueStep _qingxi_prologue = QingxiPrologueStep.Awakened; private bool _xuanjian_awakened, _rested_after_xuanjian, _static_room_authorized, _first_breakthrough_reported, _back_hill_departure_arranged, _rested_for_back_hill, _first_back_hill_battle_explained, _qingxi_herbs_gathered, _back_hill_nest_seen, _back_hill_trial_complete, _beast_hunt_prompt_open, _beast_hunt_trigger_queued; private int _qingxi_arrival_hour, _journal_started_hour=-1, _journal_rumor_stage, _journal_unread; private readonly List<JournalPanel.Entry> _earned_rumors=[]; private readonly List<JournalPanel.Entry> _completed_quests=[]; private JournalPanel.Entry? _active_quest; private bool _prologue_goal_recorded; private readonly List<Character> family=[]; private readonly List<Character> retainers=[];
    private string _player_world_node_id = "home_village"; private NpcWorldManager? _npc_world; private WorldNpcData? _qingxi_shopkeeper; private bool _cloud_state_initialized; private bool _world_open_in_progress; private bool _trade_hud_was_visible;
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
        create_prologue_skip_button();
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
        if (_npc_world is not null) _npc_world.Connect("world_npcs_changed", Callable.From(on_world_npcs_changed_for_interactions));
        _interactionRoster = GetTree().GetFirstNodeInGroup("cell_character_list") as CellCharacterList;
        if (_interactionRoster is not null) _interactionRoster.RosterClosed += on_interaction_roster_closed;
        await JourneyCreationLoadingView.StageAsync(GetTree(), .85, "正在整理各地人物…");
        restore_npc_save_state();
        restore_node_world_position();
        estate=GetNodeOrNull<EstateOverview>("SceneLayer/QingxiLocation");
        back_hill_trial=GetNodeOrNull<DungeonExploreUI>("CanvasLayer/BackHillTrial");
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
        trade_panel = new TradePanel { Name = "TradePanel" };
        GetNode<CanvasLayer>("CanvasLayer").AddChild(trade_panel);
        if (trade_panel is not null) { trade_panel.Closed += on_trade_closed; trade_panel.TradeCompleted += mark_save_dirty; }
        connect_ui();
        setup_letters();
        setup_banquets();
        if (GetTree().GetFirstNodeInGroup("cell_character_list") is CellCharacterList character_list)
            character_list.configure_side_toggle();
        estate?.set_unlocked_slips(["li_residence", "village", "farmland", "back_hill"]);
        hud?.set_world_map_available(false);
        estate?.set_back_hill_trial_available(false);
        estate?.set_meditation_available(false);
        estate?.set_rest_available(false);
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
        if(GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager") is { } time) { if(_loaded_game_save is null&&!_story_free_start)_qingxi_arrival_hour=time.get_total_game_hours(); time.time_changed += on_action_time_changed; time.time_changed += update_yuxu_monthly_allowance; time.time_changed += mark_save_dirty; time.time_changed += on_world_status_time_changed; time.time_changed += on_dynamic_dungeon_time_changed; }
        await JourneyCreationLoadingView.StageAsync(GetTree(), .95, "正在编排见闻与行囊…");
        refresh_qingxi_quest();
        set_prologue_skip_available(_loaded_game_save is null && !_story_free_start);
        if (_loaded_game_save is null && _story_free_start && JourneyCreationLoadingView.IsLoading)
        {
            await JourneyCreationLoadingView.StageAsync(GetTree(), .97, "正在安排各地人物与行旅…");
            enter_cloud_state_if_departed();
        }
        if (_loaded_game_save is null && _story_free_start) CallDeferred(nameof(show_story_free_start));
        else if (_loaded_game_save is null) CallDeferred(nameof(show_opening));
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
    public override void _Process(double delta){if (!StartupComplete || StartupError is not null) return; update_persistent_chrome();process_save_autosave(delta);process_relationship_month();process_npc_social_visit(delta);process_banquet_activity(delta);process_active_world_npc_interaction_refresh(delta);}
    public override void _ExitTree()
    {
        clear_world_loot_receipts();
        _npc_world?.end_east_woods_route_signal_batch();
        if (!_skipExitSave) save_progress_now(false);
        disconnect_world_npc_interaction_components();
        if (_npc_world is not null && _npc_world.IsConnected("world_npcs_changed", Callable.From(on_world_npcs_changed_for_interactions)))
            _npc_world.Disconnect("world_npcs_changed", Callable.From(on_world_npcs_changed_for_interactions));
        if (_interactionRoster is not null) _interactionRoster.RosterClosed -= on_interaction_roster_closed;
        if (player is not null) player.CultivationInspirationChanged -= on_player_cultivation_inspiration_changed;
        if (player is not null) { player.GongfaChapterCompleted -= on_yuxu_gongfa_chapter_completed; player.GongfaChapterCompleted -= on_player_gongfa_chapter_completed; player.ItemAcquired -= on_player_item_acquired; player.InventoryChanged -= on_player_inventory_save_changed; player.breakthrough_completed -= on_player_breakthrough; }
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

    private void update_persistent_chrome(){var modal=(_locationCrafting?.Visible??false)||(_restPanel?.Visible??false)||(storage?.Visible??false)||(info?.Visible??false)||(realm_panel?.Visible??false)||(cultivation_ui?.Visible??false)||(condensation_panel?.Visible??false)||(story_dialogue?.Visible??false)||(journal?.Visible??false)||(baigong?.Visible??false)||(trade_panel?.Visible??false)||(_letters?.Visible??false)||(hud?.player_profile_visible??false);var visible=!modal&&!_suppress_persistent_chrome;hud?.set_estate_summary_visible(visible);if(GetNodeOrNull<Control>("HUDLayer/WorldInfo") is { } timePanel)timePanel.Visible=visible;if(_prologue_skip_button is not null)_prologue_skip_button.Visible=_prologue_skip_available&&visible;}
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
        if(estate is not null) { estate.SlipOpened+=open_qingxi_location; estate.ResidentSelected+=open_qingxi_resident; estate.BackHillTrialRequested+=open_back_hill_trial; estate.MeditationRequested+=open_li_residence_meditation; estate.RestRequested+=rest_at_li_residence; estate.TaskRequested+=open_journal_task; }
        if(back_hill_trial is not null) { back_hill_trial.battle_requested+=resolve_back_hill_beast; back_hill_trial.resource_collected+=collect_back_hill_resource; back_hill_trial.event_requested+=resolve_back_hill_event; back_hill_trial.exploration_finished+=finish_back_hill_trial; }
        if (_world_node_data is { } mapData) _npc_world?.set_world_map(mapData);
        if (location_interior is not null)
        {
            location_interior.Closed += close_world_location;
            location_interior.SubsceneEntered += on_baishi_subscene_entered;
            location_interior.BaishiLocationActionRequested += on_baishi_location_action;
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
    private void create_prologue_skip_button()
    {
        var canvas = GetNodeOrNull<CanvasLayer>("TestingOverlay");
        if (canvas is null) return;
        _prologue_skip_button = canvas.GetNodeOrNull<Button>("PrologueSkipButton") ?? new Button { Name = "PrologueSkipButton" };
        _prologue_skip_button.Text = "跳过序章";
        _prologue_skip_button.Position = new Vector2(320, 16);
        _prologue_skip_button.Size = new Vector2(96, 34);
        _prologue_skip_button.Visible = false;
        _prologue_skip_button.MouseFilter = Control.MouseFilterEnum.Stop;
        _prologue_skip_button.ZIndex = 1101;
        _prologue_skip_button.AddThemeFontSizeOverride("font_size", 14);
        _prologue_skip_button.AddThemeStyleboxOverride("normal", new StyleBoxFlat
        {
            BgColor = new Color(.22f, .16f, .09f, .9f), BorderColor = new Color(.68f, .5f, .27f, .8f),
            BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
        });
        _prologue_skip_button.Pressed += request_prologue_skip;
        _prologue_skip_button.GuiInput += input =>
        {
            if (input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            {
                request_prologue_skip();
                GetViewport().SetInputAsHandled();
            }
        };
        if (_prologue_skip_button.GetParent() is null) canvas.AddChild(_prologue_skip_button);
    }
    private void request_prologue_skip()
    {
        if (!_prologue_skip_available) return;
        skip_qingxi_prologue_for_testing();
    }
    private void set_prologue_skip_available(bool available)
    {
        _prologue_skip_available = available;
        if (_prologue_skip_button is not null) _prologue_skip_button.Visible = available && !_suppress_persistent_chrome;
    }
    /// <summary>普通凡人村庄的余粮与建材储备；灵石只是一枚偶得之物。</summary>
    /// <summary>辖地储备由抽象资源账本持有；灵石作为可携带物品发放。</summary>
    private void seed_estate_resources()
    {
        estate_resources?.add(EstateResourceSystem.Food, 90);
        estate_resources?.add(EstateResourceSystem.Stone, 30);
        estate_resources?.add(EstateResourceSystem.Herbs, 20);
        estate_resources?.add(EstateResourceSystem.Ore, 8);
        var spiritStones = get_resource_item_path(EstateResourceSystem.SpiritStones);
        if (player is not null && !string.IsNullOrEmpty(spiritStones)) player.add_item_to_inventory(spiritStones, 1);
        estate_resources?.register_facility("meditation_room");
        estate_resources?.register_facility("meditation_room");
        territory_state?.recalculate_auto_labor(get_estate_resource_amount(EstateResourceSystem.Food), territory_state?.total_labor ?? 0);
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
        var data = new CharacterGenerator().generate("凡人", (int)WorldEntity.Faction.PLAYER);
        GetNodeOrNull<GameStartProfile>("/root/GameStartProfile")?.apply_to_youngest(data);
        var generated_lifespan = Math.Max(10, data.max_age - data.current_age);
        data.current_age = 16;
        data.max_age = data.current_age + generated_lifespan;
        data.entity_name = string.IsNullOrWhiteSpace(data.entity_name) ? "苏醒者" : data.entity_name;
        player = new Character();
        player.load_from_data(data);
        player.entity_type = WorldEntity.EntityType.CHARACTER;
        player.faction = (int)WorldEntity.Faction.PLAYER;
        AddChild(player);
    }
    public Character? get_player_character() => player;

    private void show_opening(){estate?.Show();estate?.MoveToFront();estate?.open_location_view("li_residence");hud?.Show();hud?.MoveToFront();show_dialogue_sequence((DialogueBeatKind.PlayerSpeech,"","痛，痛，头好痛！！！"),(DialogueBeatKind.Narration,"","你从一张硬木床榻上醒来。旧木梁撑着低矮的屋顶，床侧小几上搁着半盏凉茶，窗棂外竹影轻晃。"),(DialogueBeatKind.Narration,"","你下意识伸手去够床头。"),(DialogueBeatKind.Narration,"","指尖只碰到冰凉的硬木床沿，什么都没有。"),(DialogueBeatKind.PlayerThought,"","手机……不在了。"),(DialogueBeatKind.Narration,"","视线落回屋内：青瓷花瓶，山水卷轴，一盏油灯。没有电灯，没有电线，没有一样熟悉的东西。"),(DialogueBeatKind.PlayerThought,"","这里看起来不像现代……我不会是穿越了吧？"),(DialogueBeatKind.Narration,"","屋外传出一阵脚步声，在门口停了一瞬。"));}
    private void show_prologue_title(){open_territory();_suppress_persistent_chrome=true;var screen=new ColorRect{Color=Colors.Black,MouseFilter=Control.MouseFilterEnum.Stop};screen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);var title=new Label{Text="序章·启明",HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,MouseFilter=Control.MouseFilterEnum.Ignore};title.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);title.AddThemeFontSizeOverride("font_size",48);title.AddThemeColorOverride("font_color",new Color(.96f,.9f,.7f));screen.AddChild(title);GetNode<CanvasLayer>("CanvasLayer").AddChild(screen);var tween=CreateTween();tween.TweenInterval(1.5);tween.TweenProperty(screen,"modulate:a",0f,.8);tween.TweenCallback(Callable.From(screen.QueueFree));tween.TweenCallback(Callable.From(begin_prologue_guide));}
    private void begin_prologue_guide(){_suppress_persistent_chrome=false;if(family.Count<=4)return;prologue_guide?.begin(family[4]);hud?.set_guided_button("BtnCharacter");}
    private void open_territory(){estate?.Hide();territory_panel?.open();hud?.Show();hud?.MoveToFront();}
    private void open_ledger(string mode){territory_panel?.Hide();territory_ledger?.open(mode);hud?.Hide();}
    private void open_location_map(){if(has_open_world_access()){open_world();return;}territory_panel?.Hide();estate?.Show();estate?.MoveToFront();hud?.Show();hud?.MoveToFront();}
    private void open_territory_section(string section){switch(section){case "residential":estate?.Hide();residence_panel?.open("meditation");break;case "village":estate?.Hide();territory_panel?.open("governance");break;case "farmland":estate?.Hide();farmland_panel?.open();break;case "storage":open_storage();break;case "gate":estate?.Hide();territory_panel?.open("defense");break;}}
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
    private void open_qingxi_location(string location)
    {
        switch(location)
        {
            case "li_residence":
                show_message("李宅",_qingxi_prologue==QingxiPrologueStep.Awakened?"李守拙正在宅中。先与他交谈，弄清自己身在何处。":"李守拙为你留着偏房。这里是你在青溪里的第一处落脚点。 ");
                break;
            case "village":
                show_message("槐荫坪","村里人闲时常在老槐树下歇脚闲谈。周满仓也会带些日用物件来，想换东西可以找他商量。 ");
                break;
            case "farmland":
                if (!_qingxi_herbs_gathered)
                {
                    var harvest = new Dictionary<string, int> { ["res://resources/entities/items/苦苔根.tres"] = 1 };
                    if (player?.can_add_inventory_batch(harvest) != true)
                    {
                        show_message("田陌", "行囊里放不下这截苦苔根。先腾出合适的空间，再来采集。");
                        break;
                    }
                    if (!player.try_exchange_inventory(new Dictionary<string, int>(), harvest, announceAcquisition: true,
                        beforeCommit: () => try_spend_player_world_stamina(WorldStaminaRules.InteractionCost, "采集"))) break;
                    _qingxi_herbs_gathered = true;
                    mark_save_dirty();
                    record_player_causal_event(
                        "qingxi.herb_gathered",
                        "foraging",
                        "采得苦苔根",
                        "home_village",
                        "青溪里田陌",
                        "在田埂边采集药根时，也留意到兽蹄印绕开了有人烟的田地。",
                        "苦苔根收进行囊。 ");
                    show_message("田陌","你从田埂边采下一截苦苔根，也看见兽蹄印绕开了有人烟的田地。兽患会是往后的事；这份根材可在行囊中查看。 ");
                }
                else show_message("田陌","农人仍照看着田地。这里可采集寻常药草、听农户闲谈，也会随时令出现新的小事。 ");
                break;
            case "back_hill":
                break;
        }
    }
    private void open_qingxi_resident(string resident)
    {
        var (name, options) = resident switch
        {
            "li_head" => ("李守拙（里正）", new List<(string, Action)> { ("交谈", () => start_resident_dialogue(resident)) }),
            "shopkeeper" => ("周满仓（掌柜）", new List<(string, Action)> { ("交谈", () => start_resident_dialogue(resident)), ("交易", open_shop_trade) }),
            "farmer" => ("陈素秋（田农）", new List<(string, Action)> { ("交谈", () => start_resident_dialogue(resident)) }),
            "watchman" => ("吴承安（更夫）", new List<(string, Action)> { ("交谈", () => start_resident_dialogue(resident)) }),
            _ => ("村民", new List<(string, Action)> { ("交谈", () => start_resident_dialogue("villager")) })
        };
        npc_interactions?.show_menu(name, options);
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
    private void start_resident_dialogue(string resident)
    {
        if (!try_spend_player_world_stamina(WorldStaminaRules.ConversationCost, "交谈")) return;
        if(resident=="li_head"){li_head_dialogue();return;}
        switch(resident)
        {
            case "shopkeeper":show_dialogue_sequence((DialogueBeatKind.NpcSpeech,"周满仓","咱村里买卖，要么以物易物，要么用银子。灵石是修仙界的东西，咱这小地方见不着。"),(DialogueBeatKind.NpcSpeech,"周满仓","要问消息，得先分辨谁说得真。近来最多的传闻，还是后山夜里那阵不寻常的兽鸣。"));break;
            case "farmer":show_dialogue_sequence((DialogueBeatKind.NpcSpeech,"陈素秋","田里的东西得顺着时令来。人也一样，急不得。"),(DialogueBeatKind.Narration,"","她指了指田埂。"),(DialogueBeatKind.NpcSpeech,"陈素秋","蹄印比牛蹄大，爪尖深。夜里有东西从田边过，却绕开了有人烟的地方。"));break;
            case "watchman":show_dialogue_sequence((DialogueBeatKind.NpcSpeech,"吴承安","天一擦黑，我便沿村里几条小路走走。山里近来不太平，夜里别往深处去。"),(DialogueBeatKind.NpcSpeech,"吴承安","这几日的夜鸣不对。寻常野兽叫一声就走，这东西，叫一声，停一停，再叫一声。"));break;
            default:show_npc_dialogue("村民","对方只是朝你点了点头。");break;
        }
    }
    private void open_shop_trade()
    {
        if (_qingxi_shopkeeper is null)
        {
            _qingxi_shopkeeper = new WorldNpcData
            {
                npc_id = "qingxi_shopkeeper",
                npc_name = "周满仓",
                species = "人族",
                formal_identity = "青溪里掌柜",
                cultivation_realm = "凡人",
                current_node_id = "home_village",
                current_state = "营生",
            };
        }
        open_world_npc_trade(_qingxi_shopkeeper);
    }
    private void refresh_qingxi_quest()
    {
        if (_story_free_start)
        {
            refresh_baishi_progress();
            RefreshJournalTracker();
            mark_save_dirty();
            return;
        }
        if(_qingxi_prologue==QingxiPrologueStep.Departed)
        {
            if(_active_quest is not null){_completed_quests.Add(_active_quest with { State = "已完成" });_active_quest=null;}
            if(!_story_free_start&&!_prologue_goal_recorded){_completed_quests.Add(new JournalPanel.Entry("初醒青溪","确认身处何地并踏入云息，取得离村资格。","阶段目标","青溪里","已完成"));_prologue_goal_recorded=true;}
        }
        else if(_qingxi_prologue==QingxiPrologueStep.Awakened) set_qingxi_task("初醒", "与李宅主人李守拙交谈，弄清自己身在何处。");
        else if(_qingxi_prologue==QingxiPrologueStep.BeastHuntResolved) set_qingxi_task("与李守拙道别", "兽患已平。回到李宅与李守拙道别，之后世界地图才会开放。");
        else if(_back_hill_trial_complete) set_qingxi_task("村中岁月", _back_hill_nest_seen ? "后山深处另有危险。先留在青溪里修行、接取委托、交易并与村民交谈，慢慢积蓄力量。" : "后山外缘已走通。留在青溪里修行、采集与打听消息，慢慢积蓄力量。");
        else if(_back_hill_departure_arranged&&!_rested_for_back_hill) set_qingxi_task("明日上山", "李守拙约你明日一早去后山。先休息至次日清晨。 ");
        else if(_back_hill_departure_arranged) set_qingxi_task("后山初探", "前往后山，依李守拙的约定走一趟后山外围。 ");
        else if(!_xuanjian_awakened) set_qingxi_task("初醒", "先与李守拙交谈，弄清自己在哪。");
        else if(!_rested_after_xuanjian) set_qingxi_task("夜深", "与李守拙交谈后夜色已深。点击休息，休息至明日再作打算。");
        else if(!_static_room_authorized) set_qingxi_task("问道", "与李守拙说话，问起偏院那间旧屋。");
        else if(player is not null&&!player.has_completed_intro_gongfa_first_volume()) set_qingxi_task("玄鉴初读", "进入静室，研读完玄鉴传入功法的第一卷。");
        else if(player?.main_gongfa is null) set_qingxi_task("定为主修", "打开角色页面，将已研读的功法装配为主修。");
        else if(player?.cultivation_realm=="云息一层"&&!_first_breakthrough_reported) set_qingxi_task("初入云息", "你已突破云息一层。去找李守拙，将此事告诉他。");
        else set_qingxi_task("静室引气", "进入静室，继续引气修行。");
        RefreshJournalTracker();
        mark_save_dirty();
    }

    private void set_qingxi_task(string title, string body)
    {
        _ = IdentifyJournalTask(get_phase_goal());
        if (_active_quest is not null && _active_quest.Title != title)
            _completed_quests.Add(_active_quest with { State = "已完成" });
        if (_active_quest is null || _active_quest.Title != title || _active_quest.Body != body)
            _active_quest = IdentifyJournalTask(new JournalPanel.Entry(title, body, "阶段任务", "青溪里", "进行中"));
    }

    private List<JournalPanel.Entry> get_quest_entries()
    {
        for (var i = 0; i < _completed_quests.Count; i++) _completed_quests[i] = IdentifyJournalTask(_completed_quests[i]);
        var entries = new List<JournalPanel.Entry> { get_phase_goal() };
        if (_active_quest is not null) entries.Add(_active_quest);
        if (_baishiLecture is not null) entries.Add(baishi_current_task());
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

    private JournalPanel.Entry get_phase_goal() => _baishiLecture is { finished: false }
        ? (_baishiLecture.met_fang
            ? new("白石问道", "随方远到白石赴会，查验灵根、选择首卷并踏入云息，再决定去向。", "阶段目标", "白石城", "进行中", Id: "phase.baishi")
            : new("身在何处", "先向身旁的年轻人问清情况。", "阶段目标", "溪边野地", "进行中", Id: "phase.baishi"))
        : has_open_world_access()
        ? new("行旅与积累", "在百川区域自由选择修炼、交易、旅行、探索、委托或接触势力，积累下一次境界突破所需的资源与条件。", "阶段目标", "百川区域", "进行中")
        : new("初醒青溪", "确认身处何地并踏入云息，取得离村资格。", "阶段目标", "青溪里", "进行中");

    private void open_journal_task(string title)
    {
        if (journal is null) return;
        refresh_journal_entries();
        journal.show_panel(JournalPanel.Section.Tasks, title);
    }
    private void open_back_hill_trial()
    {
        if(!_rested_for_back_hill){show_message("后山","李守拙约你次日一早上山。先回李宅休息。 ");return;}
        void open_graph()=>back_hill_trial?.load_graph(new BackHillTrialDungeon());
        if(_back_hill_trial_complete){open_graph();return;}
        show_dialogue_sequence(open_graph,
            (DialogueBeatKind.Narration,"","次日清晨，李宅门口。李守拙背着一个布包，手里拿着一根木棍，已经在等你。"),
            (DialogueBeatKind.NpcSpeech,"李守拙","干粮和金疮药，收好。今天走后山外围，跟紧我，别往深处去。"),
            (DialogueBeatKind.PlayerSpeech,"","好。"),
            (DialogueBeatKind.Narration,"","两人出了李宅，往村后山的方向走去。"));
    }
    private void li_head_dialogue()
    {
        if(_qingxi_prologue==QingxiPrologueStep.Awakened)
        {
            _qingxi_prologue=QingxiPrologueStep.MetLiFamily;refresh_qingxi_quest();
            var player_name=player?.entity_name??"我";
            show_dialogue_sequence(begin_xuanjian_awaken,(DialogueBeatKind.Narration,"","门被推开。一个穿青布短褐的中年人走进来，目光在你身上停了一瞬。"),(DialogueBeatKind.NpcSpeech,"李守拙","醒了。"),(DialogueBeatKind.PlayerSpeech,"","……你是？"),(DialogueBeatKind.NpcSpeech,"李守拙","我姓李，李守拙，这宅子的主人。前天夜里，老更夫在村中老槐树下发现你，是我把你背回来的。"),(DialogueBeatKind.PlayerSpeech,"","……这是哪？"),(DialogueBeatKind.NpcSpeech,"李守拙","青溪里。山窝子里一个不起眼的小村。"),(DialogueBeatKind.NpcSpeech,"李守拙","你叫什么？家在何处？"),(DialogueBeatKind.PlayerSpeech,"",$"我……我叫{player_name}。可我从哪来、家在何处，一点都想不起来。"),(DialogueBeatKind.NpcSpeech,"李守拙","想不起来就先别想了。住下吧，这宅子就我一人住，空屋子多的是。"),(DialogueBeatKind.NpcSpeech,"李守拙","青溪里不大，你歇一天，有什么事随时问我。往后日子长，先把身子养好了，再说别的。"));
            return;
        }
        if(!_rested_after_xuanjian){show_npc_dialogue("李守拙","夜深了。你先歇下，明日有什么想问的，再来找我。 ");return;}
        if(!_static_room_authorized){li_head_ask_about_static_room();return;}
        if(player is not null&&!player.has_completed_intro_gongfa_first_volume()){show_npc_dialogue("李守拙","玄鉴传入的东西，既已在你脑中显现，就去静室里慢慢研读。修行最忌心浮气躁。 ");return;}
        if(player?.main_gongfa is null){show_npc_dialogue("李守拙","第一卷既已读成，便去角色页将它装配为主修。功法在身，却不运转，终究只是纸上字。 ");return;}
        if(player?.cultivation_realm=="云息一层"&&!_first_breakthrough_reported){show_dialogue_sequence(()=>{_first_breakthrough_reported=true;_back_hill_departure_arranged=true;refresh_qingxi_quest();},(DialogueBeatKind.Narration,"","你走出静室。院子里，李守拙正蹲在墙角整理农具。"),(DialogueBeatKind.PlayerSpeech,"","我……突破了。云息一层。"),(DialogueBeatKind.NpcSpeech,"李守拙","云息一层，算是入了门。接下来光靠吐纳进境慢，得有外物辅助。"),(DialogueBeatKind.NpcSpeech,"李守拙","后山有几样东西，你刚入云息一层用得上。明天一早，我带你去走一趟，也让你见见实战。"),(DialogueBeatKind.NpcSpeech,"李守拙","头一回遇上东西，我会同你讲清楚怎么看。再往后，就得你自己拿主意了。"),(DialogueBeatKind.NpcSpeech,"李守拙","今晚好好休息。"));return;}
        if(_qingxi_prologue==QingxiPrologueStep.BeastHuntResolved)
        {
            show_dialogue_sequence(finish_qingxi_departure,
                (DialogueBeatKind.Narration,"","兽患平息后的次日清晨，李宅院中。李守拙已替你收好行囊。"),
                (DialogueBeatKind.PlayerSpeech,"","李叔，我该去山外看看了。"),
                (DialogueBeatKind.NpcSpeech,"李守拙","到了山外，先找个宗门安顿下来，好好修行。若遇上能治旧伤的法子，也替我留意。"),
                (DialogueBeatKind.NpcSpeech,"李守拙","青溪里不大，却是你醒来后第一个落脚的地方。往后走自己的路，记住这里就好。"),
                (DialogueBeatKind.PlayerSpeech,"","我记下了。李叔，保重。"),
                (DialogueBeatKind.NpcSpeech,"李守拙","去吧。该你自己看看外面的天地了。"));
            return;
        }
        show_npc_dialogue("李守拙","静室既已交给你用，就安心修行。有不懂的地方，来问我。 ");
    }
    private void finish_qingxi_departure()
    {
        _qingxi_prologue=QingxiPrologueStep.Departed;
        refresh_qingxi_quest();
        enter_cloud_state_if_departed();
        hud?.set_world_map_available(true);
        set_prologue_skip_available(false);
        show_message("告别青溪里","你与李守拙道别。世界按钮已开放，可以前往山外。 ");
    }
    private void begin_xuanjian_awaken()
    {
        if(_xuanjian_awakened)return;
        _xuanjian_awakened=true;
        // 初次与里正交谈结束后，才有在李宅休息的入口。
        estate?.set_rest_available(true);
        var profile=GetNodeOrNull<GameStartProfile>("/root/GameStartProfile");
        var gongfa=ResourceLoader.Load<GongfaResource>(profile?.selected_initial_gongfa_path??GongfaAvailability.DefaultBookPath);
        if(player is not null&&gongfa is not null&&player.get_pending_intro_gongfa() is null&&player.main_gongfa is null)player.grant_intro_gongfa(gongfa);
        var gongfa_name=gongfa?.gongfa_name??"入门功法";
        refresh_qingxi_quest();
        show_dialogue_sequence((DialogueBeatKind.Narration,"","房门合上。屋里只剩你一个人。"),(DialogueBeatKind.Narration,"","你正要躺下，怀中那面随身的古镜忽然微热，镜面泛起一层浅光。"),(DialogueBeatKind.Narration,"","一缕清亮的气息没入眉心——你只觉脑海中一片清明，像是有什么东西和你连在了一起。"),(DialogueBeatKind.Narration,"","你只觉周身一轻，像洗去了一层浊尘。"),(DialogueBeatKind.Narration,"","你试着闭目——原本寻常的宅院，在你感知中有了层次。"),(DialogueBeatKind.Narration,"","院中几处地方，隐隐吸引着你。最强的一处，在偏院那间旧屋的方向。"),(DialogueBeatKind.Narration,"",$"随即，一本功法在脑海中浮现——其名为《{gongfa_name}》。"),(DialogueBeatKind.Narration,"","你顺着读下去，不知不觉已接近末尾，只觉心神微倦，余下的一时读不进去了。"),(DialogueBeatKind.PlayerThought,"","偏院……那间屋子？明天问问李叔。"));
    }
    private void li_head_ask_about_static_room()
    {
        show_dialogue_sequence(() => { _static_room_authorized=true; estate?.set_meditation_available(true); refresh_qingxi_quest(); },
            (DialogueBeatKind.PlayerSpeech,"","李叔，偏院那间旧屋……是什么地方？我夜里总觉得那边不太一样。"),
            (DialogueBeatKind.NpcSpeech,"李守拙","你能感知到灵力？"),
            (DialogueBeatKind.PlayerSpeech,"","……是。昨夜闭目的时候，隐约能感觉到。"),
            (DialogueBeatKind.Narration,"","李守拙停下手里的活，沉默了一会儿。"),
            (DialogueBeatKind.NpcSpeech,"李守拙","身具灵根之人，这世上可不多见。"),
            (DialogueBeatKind.Narration,"","他看向偏院方向，语气有些复杂。"),
            (DialogueBeatKind.NpcSpeech,"李守拙","那间屋子……是我刚来这时所修。"),
            (DialogueBeatKind.PlayerSpeech,"","李叔你……也是修士？"),
            (DialogueBeatKind.NpcSpeech,"李守拙","你既然有灵根，有些事该让你知道。你知道修行分几个境界吗？"),
            (DialogueBeatKind.Narration,"","你摇了摇头。"),
            (DialogueBeatKind.NpcSpeech,"李守拙","始于云息，引气入体，共有十层。十层修完，还要归元养息；九息圆满之后，才算准备好铸玄基。云息之上是玄基，筑道基、开灵府，算是真正踏入修行的门了。再往上是金丹，结丹一成，就算是个人物了，走到哪都有人给几分面子，没人敢轻易招惹。"),
            (DialogueBeatKind.NpcSpeech,"李守拙","金丹之上是什么样，我就不清楚了。我这辈子，止步于玄基，再没往上走过。"),
            (DialogueBeatKind.PlayerSpeech,"","那您现在……怎么会在这儿当里正？"),
            (DialogueBeatKind.NpcSpeech,"李守拙","年轻时修到玄基，本来以为能再往上走走。后来跑商，路上出了意外，被人暗算了一道，道基直接损了，境界跌到如今的云息六层，再没上去过。"),
            (DialogueBeatKind.NpcSpeech,"李守拙","修行这条路，道基损了就是损了，哪有那么容易恢复。没办法，只能找个地方安顿下来。后来辗转来到这青溪里，见这儿偏静，就接了里正的差事，混口饭吃。"),
            (DialogueBeatKind.PlayerSpeech,"","那间屋子……"),
            (DialogueBeatKind.NpcSpeech,"李守拙","来这儿之后修的。那时候还不死心，想着说不定哪天能恢复，再修回去，就修了这间静室。"),
            (DialogueBeatKind.NpcSpeech,"李守拙","后来才知道，道基损了就是损了，十几年了，一点起色都没有。就一直空着，再也没进去过。"),
            (DialogueBeatKind.NpcSpeech,"李守拙","你既然有灵根，就别浪费了这天分。那间屋子你想用就去用，反正空着也是空着。屋里的东西别弄坏就行。"));
    }
    private void open_li_residence_meditation()
    {
        if(player is null)return;
        if(!_static_room_authorized){show_message("静室", "未经李守拙应允，不宜擅入偏院旧屋。 ");return;}
        cultivation_ui?.show_entity(player);
    }
    private void on_player_breakthrough(Character _, string __)
    {
        refresh_qingxi_quest();
        refresh_player_world_status_ui();
        hud?.show_event_notification("境界精进", $"修为已达 {player?.cultivation_realm ?? "新境界"}。", "修行");
    }

    private void on_player_gongfa_chapter_completed(Character character, string bookName, string _, string chapterName, Godot.Collections.Dictionary appliedBonuses)
    {
        if (character != player) return;
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
    private void rest_at_li_residence()
    {
        if(player is null)return;
        var time=GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager"); if(time is null)return;
        if(_xuanjian_awakened&&!_rested_after_xuanjian)
        {
            var dawnHours=Math.Max(1,((time.get_total_game_hours()/24)+1)*24+6-time.get_total_game_hours());
            var restored = Mathf.Min(player.max_world_stamina-player.current_world_stamina,dawnHours*WorldStaminaRules.RestRecoveryPerGameHour);
            open_rest_panel("李宅 · 偏房", _ => run_time_transition("休息至次日", $"夜色渐深。你将休息 {GameTimeText.Duration(dawnHours)}，恢复精力 {restored:0.#} 点，至次日清晨。", "晨光透过窗棂。你已休息至次日清晨。",
                () => { player.current_health=player.max_health; player.restore_world_stamina(dawnHours*WorldStaminaRules.RestRecoveryPerGameHour); advance_player_world_status_rest(dawnHours); time.add_hours(dawnHours); _rested_after_xuanjian=true; refresh_qingxi_quest(); },
                () => show_message("醒来", "晨光透过窗棂。你已休息至次日清晨。")), dawnHours, "夜已深了，休息至次日清晨，明日再去问李守拙。"); return;
        }
        if(_back_hill_departure_arranged&&!_rested_for_back_hill)
        {
            var dawnHours=Math.Max(1,((time.get_total_game_hours()/24)+1)*24+6-time.get_total_game_hours());
            var restored = Mathf.Min(player.max_world_stamina-player.current_world_stamina,dawnHours*WorldStaminaRules.RestRecoveryPerGameHour);
            open_rest_panel("李宅 · 偏房", _ => run_time_transition("休息至次日", $"你将在李宅歇息 {GameTimeText.Duration(dawnHours)}，恢复精力 {restored:0.#} 点，养足精神后赴约上山。", "李守拙已在宅门外等候。可以前往后山。",
                () => { player.current_health=player.max_health; player.restore_world_stamina(dawnHours*WorldStaminaRules.RestRecoveryPerGameHour); advance_player_world_status_rest(dawnHours); time.add_hours(dawnHours); _rested_for_back_hill=true; estate?.set_back_hill_trial_available(true); refresh_qingxi_quest(); },
                () => show_message("天明", "李守拙已在宅门外等候。可以前往后山。")), dawnHours, "李守拙约你明日一早上山。先休息至次日清晨。"); return;
        }
        open_general_rest_dialog("李宅", () =>
        {
            if (_xuanjian_awakened) _rested_after_xuanjian = true;
            refresh_qingxi_quest();
        });
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
    private void resolve_back_hill_beast(string enemyId,int count,bool elite)
    {
        var name=enemyId switch{"wild_boar"=>"灰背野猪","forest_wolf"=>"林地凶狼","giant_toad"=>"巨沼蟾蜍","poison_snake"=>"赤环毒蛇",_=>"山中野兽"};
        void open_battle_prompt(){var dialog=new ConfirmationDialog{Title=$"{name}拦路",DialogText=$"{name}挡在山路上。你可以应战，也可以暂避其锋后返回山路。",OkButtonText="应战",CancelButtonText="暂避"}; AddChild(dialog);dialog.PopupCentered();dialog.Confirmed+=()=>{grant_back_hill_beast_materials(enemyId,count,elite);back_hill_trial?.resolve_external_battle(true);dialog.QueueFree();};dialog.Canceled+=()=>{back_hill_trial?.resolve_external_battle(false);dialog.QueueFree();};}
        if(_first_back_hill_battle_explained){open_battle_prompt();return;}
        show_dialogue_sequence(()=>{_first_back_hill_battle_explained=true;open_battle_prompt();},
            (DialogueBeatKind.NpcSpeech,"李守拙","先别急着出手。看清它手里的招式和剩下的体力；体力不够，它便打不出费力的招。"),
            (DialogueBeatKind.NpcSpeech,"李守拙","你手上的牌不只用来进攻。遇上重招，留一张化解或护身，比一味抢攻稳当。"),
            (DialogueBeatKind.NpcSpeech,"李守拙","看它怎么动，再决定这回合要不要出手。往后遇敌，就靠你自己判断了。"));
    }
    private void grant_back_hill_beast_materials(string enemyId,int count,bool elite)
    {
        if (player is null) return;
        var quantity=Math.Max(1,count); var hides=enemyId is "wild_boar" or "forest_wolf" ? quantity : 0; var bones=quantity+(elite?1:0);
        var rewards = new Dictionary<string, int> { ["res://resources/entities/items/兽骨.tres"] = bones };
        if (hides > 0) rewards["res://resources/entities/items/兽皮.tres"] = hides;
        receive_world_loot("战利品", rewards,
            $"你收好{(hides>0?$"兽皮 ×{hides}、":"")}兽骨 ×{bones}，可在行囊中查看品阶与功用。");
    }
    private void collect_back_hill_resource(string resource,int amount)
    {
        if (amount <= 0 || !ResourceItems.TryGetValue(resource, out var path) || player is null) return;
        var name = ResourceLoader.Load<ItemData>(path)?.entity_name ?? "材料";
        receive_world_loot("采集所得", new Dictionary<string, int> { [path] = amount }, $"你收好{name} ×{amount}。");
    }
    private void resolve_back_hill_event(string eventId,string title)
    {
        if(eventId=="back_hill_high_beast")
        {
            _back_hill_nest_seen=true;refresh_qingxi_quest();
            show_dialogue_sequence(
                (DialogueBeatKind.Narration,"","岩石上留着一道深重爪痕，周围残留的气息让人胸口发闷。"),
                (DialogueBeatKind.NpcSpeech,"李守拙","前方有高阶妖兽出没。这不是你现在能对付的，等你筑基之后再来。"),
                (DialogueBeatKind.NpcSpeech,"李守拙","走，先回去。"));
            return;
        }
        show_message(title,"这点山间见闻被你默默记下。");
    }
    private void finish_back_hill_trial()
    {
        var first = !_back_hill_trial_complete;
        _back_hill_trial_complete = true;
        if (first)
        {
            _journal_started_hour = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? -1;
            record_player_causal_event(
                "qingxi.back_hill_trial.completed",
                "exploration",
                "初探青溪里后山",
                "home_village",
                "青溪里后山",
                "沿回村小径深入后山外围，见到半山腰留下的深重爪痕。",
                "沿小径平安下山，将所见记在心中。 ");
        }
        refresh_qingxi_quest();
        show_message("后山初探", first ? "你沿回村小径下山。后山外围的东西，比往年凶得多；而半山腰那道爪痕，更让人无法忽视。" : "你再次沿回村小径下山，收好这一路的所得。");
        if (first) try_trigger_qingxi_beast_hunt();
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
        var rumors = new List<JournalPanel.Entry>
        {
            new("后山夜声", "近来后山夜里常有短促兽鸣，入夜后忽远忽近。猎户说这不是寻常时节该有的动静，却也无人敢深入查看。", "村中猎户所言", "青溪里后山", "待查")
        };
        if (_qingxi_herbs_gathered) rumors.Add(new("田陌蹄印", "田垄湿土里留下数道深蹄印，绕开了有人烟的屋舍，直往林木茂密处去了。", "田边农人所言", "青溪里田陌", "待查"));
        if (_back_hill_nest_seen) rumors.Add(new("半山爪痕", "有人远远看见半山腰岩壁上有深重爪痕。那一带近来格外安静，连寻常野兽也不愿靠近。", "采药人所言", "后山半山腰", "已留意"));
        if (_qingxi_prologue == QingxiPrologueStep.BeastHuntResolved) rumors.Add(new("山外来客", "路过的货郎说，附近几处村落也有人提起妖兽反常出没，或许并非一地偶发。", "槐荫坪闲谈", "青溪里·槐荫坪", "待查"));
        rumors.AddRange(_earned_rumors);

        var journalEntries = new List<JournalPanel.Entry>
        {
            new("初醒李宅", "你从李宅偏房醒来，陌生的陈设与窗外的山影提醒着你：这里已不是原来的世界。", "亲历", "青溪里·李宅", "已记", "序章")
        };
        if (_xuanjian_awakened) journalEntries.Add(new("玄鉴有感", "玄鉴在掌中微热，你第一次察觉到天地间游离的灵力。", "亲历", "青溪里·李宅", "已记"));
        if (_back_hill_trial_complete) journalEntries.Add(new("后山初探", "你走过后山外围，亲眼见到兽踪与半山腰那道令人心惊的爪痕。", "亲历", "青溪里后山", "已记"));
        journalEntries.AddRange(_discovered_yuxu_peak_entries);

        var people = new List<JournalPanel.Entry>
        {
            new("李守拙", "青溪里里正，也是李宅主人。他收留了初醒的你，并引你踏上修行之路。谈及后山时，他总比旁人更为谨慎。", "亲历与交谈", "青溪里·李宅", "已相识")
        };
        var events = new List<JournalPanel.Entry>();
        if (_back_hill_nest_seen) events.Add(new("后山妖兽异动", "后山半山腰出现远超寻常野兽的痕迹。李守拙判断前方有高阶妖兽出没，当前不宜深入。", "多方见闻已印证", "后山半山腰", "持续中"));
        if (_back_hill_trial_complete && _qingxi_prologue < QingxiPrologueStep.BeastHuntResolved) events.Add(new("青溪里兽患", "山中兽群在入冬后逼近村落。满一年之夜，兽患终于袭来。", "青溪里近况", "青溪里", "将起"));
        if (_qingxi_prologue == QingxiPrologueStep.BeastHuntResolved) events.Add(new("兽患暂歇", "兽群已退，青溪里暂时平安。", "亲历", "青溪里", "余波未明"));

        journal.set_entries(JournalPanel.Section.Rumors, rumors);
        journal.set_entries(JournalPanel.Section.Journal, journalEntries);
        journal.set_entries(JournalPanel.Section.People, people);
        journal.set_entries(JournalPanel.Section.Events, events);
        journal.set_entries(JournalPanel.Section.Tasks, get_quest_entries());
    }

    // 见闻只由玩家主动消耗时间的行动触发；没有自然流逝，也不会用弹窗打断结算。
    private void on_action_time_changed()
    {
        info?.refresh_current_age();
        if (_baishiLecture is not null) { refresh_baishi_progress(); RefreshJournalTracker(); }
        if (player?.entity_data is HumanData human && human.sect_affiliation_id == YuxuSectId)
        {
            sync_yuxu_true_disciple_lecture_schedule(human);
            refresh_yuxu_true_disciple_lecture_context(human, is_yuxu_true_disciple(human.sect_current_rank));
        }
        refresh_letter_invitation_tasks_if_changed();
        try_trigger_qingxi_beast_hunt();
        if(_journal_started_hour<0 || _journal_rumor_stage>=3) return;
        var time=GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager"); if(time is null) return;
        var elapsed=time.get_total_game_hours()-_journal_started_hour;
        var thresholds=new[]{48,24*8,24*20};
        if(elapsed<thresholds[_journal_rumor_stage]) return;
        var entry=_journal_rumor_stage switch
        {
            0 => new JournalPanel.Entry("槐荫闲话", "有走村串巷的货郎说，近来几处村子都在问驱兽药和止血散。山路上的人，比往常少了许多。", "槐荫坪闲谈", "青溪里·槐荫坪", "待查"),
            1 => new JournalPanel.Entry("北坡青光", "一名采药人称，后山北坡雨后偶有青光从石缝透出，靠近时又会无端消散。他没敢再往里走。", "采药人所言", "后山北坡", "待查"),
            _ => new JournalPanel.Entry("商路告示", "邻县商路传来消息：有修士正在收购疗伤灵材，给价不低。缘由无人说得清楚，或与近来的山中异动有关。", "往来商旅所言", "邻县商路", "待查")
        };
        _earned_rumors.Add(entry); _journal_rumor_stage++; _journal_unread++; hud?.set_journal_unread_count(_journal_unread);
    }
    private void try_trigger_qingxi_beast_hunt()
    {
        var time=GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
        if(time is null || !_back_hill_trial_complete || _qingxi_prologue>=QingxiPrologueStep.BeastHuntResolved || _beast_hunt_prompt_open || _beast_hunt_trigger_queued || time.get_total_game_hours()-_qingxi_arrival_hour<360*24 || (time.hour>=5 && time.hour<20)) return;
        _beast_hunt_trigger_queued=true;
        CallDeferred(nameof(open_beast_hunt));
    }
    private void open_beast_hunt()
    {
        _beast_hunt_trigger_queued=false;
        if(_qingxi_prologue>=QingxiPrologueStep.BeastHuntResolved || !_back_hill_trial_complete) return;
        _beast_hunt_prompt_open=true;
        var dialog=new ConfirmationDialog{Title="青溪里兽患",DialogText="入村满一年后的冬夜，山魈率兽群冲下山来，直扑青溪里。李守拙正组织村民退守李宅，你必须迎战首领、护住村子。\n\n此战目前以事件结算呈现，后续再接入正式局内战斗。",OkButtonText="迎战",CancelButtonText="稍后"};
        AddChild(dialog); dialog.PopupCentered();
        dialog.Confirmed+=()=>
        {
            _beast_hunt_prompt_open = false;
            _qingxi_prologue = QingxiPrologueStep.BeastHuntResolved;
            record_player_causal_event(
                "qingxi.beast_hunt.resolved",
                "defense",
                "平息青溪里兽患",
                "home_village",
                "青溪里",
                "山魈率兽群冲下山来，村中人依李守拙安排退守。",
                "山魈伏诛，兽群退去，青溪里暂时平安。 ");
            refresh_qingxi_quest();
            show_message("兽患已除","山魈伏诛，兽群退去。青溪里暂时平安。次日清晨，回李宅与李守拙道别后，才能启用世界地图。 ");
            dialog.QueueFree();
        };
        dialog.Canceled+=()=>{_beast_hunt_prompt_open=false;dialog.QueueFree();};
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
        estate?.Hide();
        territory_panel?.Hide();

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
    private void skip_qingxi_prologue_for_testing()
    {
        if (has_open_world_access()) return;
        var profile = GetNodeOrNull<GameStartProfile>("/root/GameStartProfile");
        var gongfa = ResourceLoader.Load<GongfaResource>(profile?.selected_initial_gongfa_path ?? GongfaAvailability.DefaultBookPath);
        if (player is null || gongfa is null || !player.prepare_prologue_skip_state(gongfa))
        {
            show_message("测试跳过", "无法准备测试角色的功法状态，请重新开始后再试。");
            return;
        }
        _qingxi_prologue = QingxiPrologueStep.Departed;
        // 测试跳过仍落在青溪里记忆锚点；显示李宅签牌供场景/摆动物理验看，正常剧情解锁不受影响。
        estate?.set_meditation_available(true);
        estate?.set_rest_available(true);
        refresh_qingxi_quest();
        enter_cloud_state_if_departed();
        hud?.set_world_map_available(true);
        set_prologue_skip_available(false);
        show_message("测试跳过", $"已略过青溪里序章。\n\n你已至云息三层，《{gongfa.gongfa_name}》第一卷已研读并装配为主修。世界入口已经开启，当前测试落点为青溪里记忆锚点；点击“世界”即可查看大陆地图与人物。 ");
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
        estate?.Hide();
        if (node_id == "yuxu_xianzong_core")
        {
            if (player?.entity_data is HumanData human && human.sect_affiliation_id == YuxuSectId)
                open_faction_overview_if_available(node_id);
            else
                begin_yuxu_gate_welcome();
            return;
        }
        if (node_id == "east_woods")
        {
            open_east_woods_dungeon();
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
        1 => "真传弟子",
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
        overview.Call("set_admission_context", human.spirit_root_name, get_yuxu_entry_rank(get_player_spirit_root_count()), human.sect_affiliation_id == "yuxu_xianzong", human.sect_current_rank, !string.IsNullOrEmpty(human.sect_affiliation_id) && human.sect_affiliation_id != "yuxu_xianzong");
    }

    private void on_yuxu_admission_requested()
    {
        begin_yuxu_gate_welcome();
    }

    private void show_character(GodotObject character){if(character is not WorldEntity entity)return;info?.show_entity(entity);if(player is not null&&entity==player)hud?.set_guided_button(null);}
    private void on_territory_opened(string section){prologue_guide?.on_territory_opened(section);}
    private void advance_to_next_month(){GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.slumber(30);}
    // 日常局势不再以弹窗打断玩家，而是交由辖地总览中的局势簿保留处理权。
    private void show_territory_event(string eventId,string title,string body,string primary,string secondary)=>territory_state?.record_situation(eventId,title,body,primary,secondary);
    private void resolve_territory_situation(string eventId,bool primary){estate_resources?.resolve_territory_event(eventId,primary);territory_state?.resolve_situation(eventId);}
    private void advance_territory_time(int days){GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.slumber(days);territory_panel?.open();}
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
        if (_baishiLecture?.companion.npc_name == speaker) return _baishiLecture.companion;
        if (_baishiLecture?.host_snapshot?.npc_name == speaker) return _baishiLecture.host_snapshot;
        if (_baishiLecture?.representatives.FirstOrDefault(n => n.npc_name == speaker) is { } representative) return representative;
        return QingxiResidentAppearance.Find(speaker);
    }
    private void show_player_thought(string body,Action? on_closed=null)
    {
        if(player is not null) story_dialogue?.show_player_thought(player.entity_name,body,player,on_closed);
        else story_dialogue?.show_player_thought("我",body,on_closed);
    }
}
