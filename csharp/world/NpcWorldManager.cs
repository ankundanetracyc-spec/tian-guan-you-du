using Godot;
using Godot.Collections;

/// <summary>
/// 百川区域 NPC 的唯一运行时入口。它只在玩家主动推进时间并跨月时结算，
/// 并把每次状态变化写成可查询的行动记录。
/// </summary>
public partial class NpcWorldManager : Node
{
    public event System.Action<WorldNpcData, string, string>? DungeonNodeStepped;
    [Signal] public delegate void world_npcs_changedEventHandler();
    [Signal] public delegate void npc_action_recordedEventHandler(string npc_id, NpcActionRecord record);

    [Export] public bool simulation_enabled;
    [Export] public Array<WorldNpcData> npcs = [];
    public int population_version { get; set; }
    private Array<EastWoodsNpcIncidentRecord> _east_woods_npc_incidents = [];
    internal Array<EastWoodsNpcIncidentRecord> PersistentIncidentView => _east_woods_npc_incidents;
    private HumanData? _playerData;

    private int _last_processed_month = -1;
    private WorldNodeMapData? _map_data;
    private Node? _tile_world_map;
    private int _last_world_grid_hour = -1;
    private bool _worldGridRosterCacheDirty = true;
    private readonly System.Collections.Generic.Dictionary<string, List<WorldNpcData>> _worldNodeRosterCache = new(StringComparer.Ordinal);
    private readonly System.Collections.Generic.Dictionary<Vector2I, List<WorldNpcData>> _ordinaryCellRosterCache = new();

    public override void _Ready()
    {
        world_npcs_changed += invalidate_world_grid_roster_cache;
        var time = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
        if (time is not null)
        {
            _last_processed_month = month_index(time);
            _last_world_grid_hour = time.get_total_game_hours();
            time.time_changed += on_time_changed;
        }
    }

    public override void _ExitTree()
    {
        world_npcs_changed -= invalidate_world_grid_roster_cache;
        if (GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager") is { } time)
            time.time_changed -= on_time_changed;
    }

    public void set_world_map(WorldNodeMapData map_data)
    {
        _map_data = map_data;
        foreach (var npc in npcs)
            normalize_npc_location(npc);
        invalidate_world_grid_roster_cache();
    }

    public void set_tile_world_map(Node tileWorldMap)
    {
        _tile_world_map = tileWorldMap;
        foreach (var npc in npcs)
        {
            normalize_npc_location(npc);
            initialize_world_grid_position(npc);
        }
        var time = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
        if (time is not null) _last_world_grid_hour = time.get_total_game_hours();
        EmitSignal(SignalName.world_npcs_changed);
    }

    /// <summary>Attach the reviewed node graph without instantiating a tile map.</summary>
    public void initialize_node_world_graph()
    {
        if (_tile_world_map is not null && GodotObject.IsInstanceValid(_tile_world_map) &&
            _tile_world_map.HasMethod("get_npc_edge_travel_hours")) return;
        var script = GD.Load<GDScript>("res://gdscripts/world/BaiChuanNpcNodeGraph.gd");
        var graph = script?.New().AsGodotObject() as Node;
        if (graph is null || !graph.Call("initialize").AsBool())
        {
            graph?.Free();
            GD.PushError("无法加载 NPC 百川节点路网");
            return;
        }
        graph.Name = "BaiChuanNpcNodeGraph";
        AddChild(graph);
        set_tile_world_map(graph);
    }

    public Vector2I node_world_cell_for_node_id(string nodeId) =>
        _tile_world_map?.HasMethod("node_id_to_cell") == true
            ? _tile_world_map.Call("node_id_to_cell", nodeId).AsVector2I() : new Vector2I(-1, -1);

    public string node_world_node_id_for_cell(Vector2I cell) =>
        _tile_world_map?.HasMethod("cell_to_node_id") == true
            ? _tile_world_map.Call("cell_to_node_id", cell).AsString() : "";

    public void set_player_data(HumanData? playerData) => _playerData = playerData;

    public bool has_world_grid_anchor(string nodeId)
    {
        if (_tile_world_map is null) return true; // Preserve data-only use in tests and non-map tools.
        var cell = get_location_anchor_cell(nodeId);
        return is_valid_world_grid_cell(cell) && _tile_world_map.Call("is_cell_traversable", cell).AsBool();
    }

    public void set_simulation_enabled(bool enabled)
    {
        var was_enabled = simulation_enabled;
        simulation_enabled = enabled;
        if (enabled && !was_enabled && GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager") is { } time)
        {
            _last_processed_month = month_index(time);
            _last_world_grid_hour = time.get_total_game_hours();
        }
    }

    public int get_last_processed_month() => _last_processed_month;

    public void clear_for_new_game()
    {
        population_version = 0;
        simulation_enabled = false;
        npcs.Clear();
        _playerData = null;
        _east_woods_npc_incidents.Clear();
        var time = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
        _last_processed_month = time is null ? -1 : month_index(time);
        _last_world_grid_hour = time is null ? -1 : time.get_total_game_hours();
        EmitSignal(SignalName.world_npcs_changed);
    }

    public void restore_persistent_state(
        Array<WorldNpcData> savedNpcs,
        bool enabled,
        int lastProcessedMonth,
        Array<EastWoodsNpcIncidentRecord>? savedIncidents = null,
        int savedPopulationVersion = 0)
    {
        population_version = Math.Max(0, savedPopulationVersion);
        npcs.Clear();
        foreach (var savedNpc in savedNpcs)
        {
            if (savedNpc is null) continue;
            var copy = savedNpc.Duplicate(true) as WorldNpcData;
            if (copy is not null) npcs.Add(copy);
        }
        _east_woods_npc_incidents.Clear();
        if (savedIncidents is not null)
            foreach (var savedIncident in savedIncidents)
            {
                if (savedIncident is null || string.IsNullOrWhiteSpace(savedIncident.incident_id)) continue;
                if (savedIncident.Duplicate(true) is EastWoodsNpcIncidentRecord copy)
                    _east_woods_npc_incidents.Add(copy);
            }
        simulation_enabled = enabled;
        var time = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
        if (time is not null)
            foreach (var npc in npcs)
                if (npc.relationship_last_processed_month < 0)
                    npc.relationship_last_processed_month = month_index(time);
        _last_processed_month = lastProcessedMonth >= 0
            ? lastProcessedMonth
            : time is null ? -1 : month_index(time);
        _last_world_grid_hour = time is null ? -1 : time.get_total_game_hours();
        if (_map_data is not null)
            foreach (var npc in npcs)
            {
                normalize_npc_location(npc);
                initialize_world_grid_position(npc);
            }
        EmitSignal(SignalName.world_npcs_changed);
    }

    public void register_npc(WorldNpcData npc, bool notify = true)
    {
        if (npc is null || string.IsNullOrWhiteSpace(npc.npc_id)) return;
        if (!normalize_npc_location(npc)) return;
        initialize_world_grid_position(npc);
        if (npc.relationship_last_processed_month < 0 && GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager") is { } clock)
            npc.relationship_last_processed_month = month_index(clock);
        var existing = get_npc(npc.npc_id);
        if (existing is not null) npcs.Remove(existing);
        npcs.Add(npc);
        if (notify) EmitSignal(SignalName.world_npcs_changed);
    }

    public WorldNpcData? get_npc(string npc_id) => npcs.FirstOrDefault(npc => npc.npc_id == npc_id);

    /// <summary>返回当前已登记、仍在世的人物；青溪里序章人物不在此登记表内。</summary>
    public Array<WorldNpcData> get_active_npcs()
    {
        var result = new Array<WorldNpcData>();
        foreach (var npc in npcs)
            if (npc.is_alive) result.Add(npc);
        return result;
    }

    public Array<WorldNpcData> get_persistent_npcs()
    {
        var result = new Array<WorldNpcData>();
        foreach (var npc in npcs)
            if (npc is not null && npc.Duplicate(true) is WorldNpcData copy)
                result.Add(copy);
        return result;
    }

    public Array<EastWoodsNpcIncidentRecord> get_persistent_east_woods_incidents()
    {
        var result = new Array<EastWoodsNpcIncidentRecord>();
        foreach (var incident in _east_woods_npc_incidents)
            if (incident is not null && incident.Duplicate(true) is EastWoodsNpcIncidentRecord copy)
                result.Add(copy);
        return result;
    }

    public Array<EastWoodsNpcIncidentRecord> get_east_woods_incidents_for_review()
    {
        var result = new Array<EastWoodsNpcIncidentRecord>();
        foreach (var incident in _east_woods_npc_incidents)
        {
            if (incident is null || incident.case_status is not (EastWoodsNpcIncidentRecord.StatusAvailable or EastWoodsNpcIncidentRecord.StatusAssigned))
                continue;
            if (incident.Duplicate(true) is EastWoodsNpcIncidentRecord copy) result.Add(copy);
        }
        return result;
    }

    public EastWoodsNpcIncidentRecord? get_east_woods_incident(string incidentId)
        => _east_woods_npc_incidents.FirstOrDefault(incident => incident is not null && incident.incident_id == incidentId);

    public Array<EastWoodsNpcIncidentRecord> get_all_east_woods_incidents()
    {
        var result = new Array<EastWoodsNpcIncidentRecord>();
        foreach (var incident in _east_woods_npc_incidents)
            if (incident is not null && incident.Duplicate(true) is EastWoodsNpcIncidentRecord copy)
                result.Add(copy);
        return result;
    }

    public bool try_assign_yuxu_case_task(string incidentId, string taskId, string dutyInstanceId)
    {
        var incident = get_east_woods_incident(incidentId);
        var task = incident?.field_tasks?.FirstOrDefault(item => item is not null && item.task_id == taskId);
        if (task is null || task.status != YuxuSectCaseTaskRecord.StatusAvailable ||
            string.IsNullOrWhiteSpace(dutyInstanceId) ||
            incident!.legal_status is not (YuxuSectDraftLawEvaluator.LegalStatusEnforcementPending or YuxuSectDraftLawEvaluator.LegalStatusAppealPending))
            return false;
        task.status = YuxuSectCaseTaskRecord.StatusAssigned;
        task.duty_instance_id = dutyInstanceId;
        task.updated_at = current_case_timestamp();
        return true;
    }

    public bool release_yuxu_case_task(string incidentId, string taskId, string dutyInstanceId)
    {
        var incident = get_east_woods_incident(incidentId);
        var task = incident?.field_tasks?.FirstOrDefault(item => item is not null && item.task_id == taskId);
        if (task is null || task.status != YuxuSectCaseTaskRecord.StatusAssigned ||
            task.duty_instance_id != dutyInstanceId ||
            task.temporary_party_member_active || task.journey_status != YuxuSectCaseTaskRecord.JourneyNotStarted)
            return false;
        task.status = YuxuSectCaseTaskRecord.StatusAvailable;
        task.duty_instance_id = "";
        task.updated_at = current_case_timestamp();
        return true;
    }

    public bool mark_yuxu_case_task_objective_complete(string incidentId, string taskId, string dutyInstanceId)
    {
        var incident = get_east_woods_incident(incidentId);
        var task = incident?.field_tasks?.FirstOrDefault(item => item is not null && item.task_id == taskId);
        if (task is null || task.status != YuxuSectCaseTaskRecord.StatusAssigned ||
            task.duty_instance_id != dutyInstanceId ||
            task.task_type == "appeal_transport" &&
            (task.journey_status != YuxuSectCaseTaskRecord.JourneyHandedOff || task.temporary_party_member_active))
            return false;
        task.status = YuxuSectCaseTaskRecord.StatusObjectiveComplete;
        task.updated_at = current_case_timestamp();
        return true;
    }

    public bool complete_yuxu_case_task(string incidentId, string taskId, string dutyInstanceId)
    {
        var incident = get_east_woods_incident(incidentId);
        if (incident is null) return false;
        incident.field_tasks ??= [];
        var task = incident.field_tasks.FirstOrDefault(item => item is not null && item.task_id == taskId);
        if (task is null || task.status != YuxuSectCaseTaskRecord.StatusObjectiveComplete ||
            task.duty_instance_id != dutyInstanceId ||
            task.task_type == "appeal_transport" &&
            (task.journey_status != YuxuSectCaseTaskRecord.JourneyHandedOff || task.temporary_party_member_active))
            return false;
        task.status = YuxuSectCaseTaskRecord.StatusCompleted;
        task.duty_instance_id = "";
        task.updated_at = current_case_timestamp();
        if (incident.field_tasks.All(item => item is null || item.status is YuxuSectCaseTaskRecord.StatusCompleted or YuxuSectCaseTaskRecord.StatusCanceled))
        {
            if (incident.legal_status != YuxuSectDraftLawEvaluator.LegalStatusAppealPending)
                incident.legal_status = YuxuSectDraftLawEvaluator.LegalStatusClosed;
        }
        return true;
    }

    public bool record_east_woods_npc_incident(EastWoodsNpcIncidentRecord incident)
    {
        if (incident is null || string.IsNullOrWhiteSpace(incident.incident_id) ||
            string.IsNullOrWhiteSpace(incident.attacker_npc_id) || string.IsNullOrWhiteSpace(incident.target_npc_id) ||
            string.IsNullOrWhiteSpace(incident.location_id) || string.IsNullOrWhiteSpace(incident.occurred_at))
            return false;
        if (_east_woods_npc_incidents.Any(existing => existing is not null && existing.incident_id == incident.incident_id))
            return false;
        incident.item_transfers ??= [];
        incident.evidence_source_refs ??= [];
        incident.ruling_rule_ids ??= [];
        incident.ruling_sanction_ids ??= [];
        incident.appeal_records ??= [];
        incident.field_tasks ??= [];
        if (incident.Duplicate(true) is not EastWoodsNpcIncidentRecord copy) return false;
        _east_woods_npc_incidents.Add(copy);
        EmitSignal(SignalName.world_npcs_changed);
        return true;
    }

    private string current_case_timestamp()
    {
        var time = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
        return time is null ? "" : $"{time.year}年{time.month}月{time.day}日";
    }

    public bool try_assign_east_woods_incident(string incidentId, string dutyInstanceId)
    {
        var incident = get_east_woods_incident(incidentId);
        if (incident is null || incident.case_status != EastWoodsNpcIncidentRecord.StatusAvailable ||
            string.IsNullOrWhiteSpace(dutyInstanceId)) return false;
        incident.case_status = EastWoodsNpcIncidentRecord.StatusAssigned;
        incident.assigned_duty_instance_id = dutyInstanceId;
        return true;
    }

    public bool release_east_woods_incident(string incidentId, string dutyInstanceId)
    {
        var incident = get_east_woods_incident(incidentId);
        if (incident is null || incident.case_status != EastWoodsNpcIncidentRecord.StatusAssigned ||
            incident.assigned_duty_instance_id != dutyInstanceId) return false;
        incident.case_status = EastWoodsNpcIncidentRecord.StatusAvailable;
        incident.assigned_duty_instance_id = "";
        return true;
    }

    public bool try_submit_east_woods_incident(string incidentId, string dutyInstanceId, string submittedAt)
    {
        var incident = get_east_woods_incident(incidentId);
        if (incident is null || incident.case_status != EastWoodsNpcIncidentRecord.StatusAssigned ||
            incident.assigned_duty_instance_id != dutyInstanceId) return false;
        incident.case_status = EastWoodsNpcIncidentRecord.StatusSubmittedPendingRuling;
        incident.assigned_duty_instance_id = "";
        incident.submitted_at = submittedAt;
        incident.legal_status = YuxuSectDraftLawEvaluator.LegalStatusPending;
        return true;
    }

    public bool try_adjudicate_yuxu_case(string incidentId, YuxuSectLawDraftConfig config, out string resultText)
    {
        resultText = "";
        var incident = get_east_woods_incident(incidentId);
        if (incident is null || incident.case_status != EastWoodsNpcIncidentRecord.StatusSubmittedPendingRuling ||
            incident.legal_status != YuxuSectDraftLawEvaluator.LegalStatusPending || config is null)
            return false;

        var decision = YuxuSectDraftLawEvaluator.Evaluate(incident, get_all_east_woods_incidents(), config);
        incident.ruling_rule_ids = decision.RuleIds.Duplicate();
        incident.ruling_sanction_ids = decision.SanctionIds.Duplicate();
        incident.ruling_offender_npc_id = decision.OffenderNpcId;
        incident.prior_final_offense_count = decision.PriorFinalFindings;
        incident.ruling_at = current_case_timestamp();
        incident.ruling_findings = decision.FindingText;
        incident.capture_warrant_authorized = decision.CaptureWarrantAuthorized;
        incident.capture_warrant_kill_authorized = decision.CaptureKillAuthorized;
        incident.capture_warrant_target_npc_id = decision.CaptureWarrantAuthorized ? decision.OffenderNpcId : "";
        incident.legal_status = YuxuSectDraftLawEvaluator.LegalStatusRuled;
        resultText = decision.HasJurisdiction
            ? $"案卷 {incident.incident_id} 已由执中峰依配置门规完成裁断：{decision.FindingText}"
            : $"案卷 {incident.incident_id} 经审查不属本宗管辖或事实不足：{decision.FindingText}";
        EmitSignal(SignalName.world_npcs_changed);
        return true;
    }

    public bool try_file_yuxu_case_appeal(string incidentId, string appellantNpcId, string groundId, out string resultText)
    {
        resultText = "";
        var incident = get_east_woods_incident(incidentId);
        var config = YuxuSectDraftLawEvaluator.LoadConfig();
        var appellant = get_npc(appellantNpcId);
        if (incident is null || appellant is null || !appellant.is_alive || string.IsNullOrWhiteSpace(appellantNpcId) || incident.sanctions_executed ||
            incident.legal_status is not (YuxuSectDraftLawEvaluator.LegalStatusRuled or YuxuSectDraftLawEvaluator.LegalStatusAppealReviewed) ||
            appellantNpcId != incident.attacker_npc_id && appellantNpcId != incident.target_npc_id)
            return false;

        incident.appeal_records ??= [];
        if (incident.appeal_records.Count(record => record is not null && record.appellant_npc_id == appellantNpcId) >= Math.Max(1, config.appeals_per_party))
        {
            resultText = "此当事人已经用过本案申诉次数。";
            return false;
        }

        var appeal = new YuxuSectCaseAppealRecord
        {
            appellant_npc_id = appellantNpcId,
            ground_id = string.IsNullOrWhiteSpace(groundId) ? "request_record_review" : groundId,
            filed_at = current_case_timestamp(),
            status = YuxuSectCaseAppealRecord.StatusPending,
        };
        incident.appeal_records.Add(appeal);
        incident.appeal_status = YuxuSectDraftLawEvaluator.AppealStatusPending;
        incident.appellant_npc_id = appellantNpcId;
        incident.appeal_ground_id = appeal.ground_id;
        incident.appeal_filed_at = appeal.filed_at;
        incident.legal_status = YuxuSectDraftLawEvaluator.LegalStatusAppealPending;

        incident.field_tasks ??= [];
        var transportTaskId = $"appeal_transport:{appellantNpcId}:{incident.appeal_records.Count}";
        add_case_field_task(incident, new YuxuSectCaseTaskRecord
        {
            task_id = transportTaskId,
            task_type = "appeal_transport",
            title = $"案卷押送 · {appellant?.npc_name ?? appellantNpcId}",
            target_npc_id = appellantNpcId,
            target_npc_name = appellant?.npc_name ?? appellantNpcId,
            goal = $"将案卷指定的申诉当事人 {appellant?.npc_name ?? appellantNpcId} 作为临时队伍成员，自其所在位置押送至玉虚仙宗执中峰。",
            completion_conditions = "在和光峰事务板接取后，与案卷指定当事人当面启程；按自动指派路线推进，途中遭拦截时进入现有战斗；抵达执中峰后向案卷官员交接。战斗结果不等同于缉捕，不授权击杀案卷目标。",
            return_instruction = "回和光峰事务板回报押送交接结果。",
            contribution_reward = 0,
            kill_authorized = false,
        });
        resultText = $"{appellant?.npc_name ?? appellantNpcId} 已递交本案申诉；待复核期间，原裁断暂不执行。案卷押送事务已刊入和光峰事务板。";
        EmitSignal(SignalName.world_npcs_changed);
        return true;
    }

    public bool join_yuxu_case_task_party(
        string incidentId,
        string taskId,
        string dutyInstanceId,
        string npcId,
        Vector2I playerCell,
        string playerLocationNodeId,
        string destinationNodeId,
        Array<Vector2I> route,
        float remainingHours,
        bool insideDungeon)
    {
        var incident = get_east_woods_incident(incidentId);
        var task = get_yuxu_case_task(incidentId, taskId);
        var npc = get_npc(npcId);
        if (incident is null || task is null || npc is null || !npc.is_alive ||
            incident.appellant_npc_id != npcId ||
            task.task_type != "appeal_transport" || task.target_npc_id != npcId ||
            task.status != YuxuSectCaseTaskRecord.StatusAssigned || task.duty_instance_id != dutyInstanceId ||
            task.journey_status != YuxuSectCaseTaskRecord.JourneyNotStarted ||
            !string.IsNullOrEmpty(npc.temporary_party_task_id) ||
            string.IsNullOrWhiteSpace(destinationNodeId))
            return false;

        task.temporary_party_member_active = true;
        task.journey_party_npc_id = npcId;
        task.journey_destination_node_id = destinationNodeId;
        task.journey_route = route?.Duplicate() ?? [];
        task.journey_next_index = task.journey_route.Count > 1 ? 1 : 0;
        task.journey_current_cell = playerCell;
        task.journey_remaining_hours = Mathf.Max(0f, remainingHours);
        task.journey_status = insideDungeon
            ? YuxuSectCaseTaskRecord.JourneyExitingDungeon
            : YuxuSectCaseTaskRecord.JourneyInTransit;
        task.updated_at = current_case_timestamp();
        npc.temporary_party_task_id = task.task_id;
        npc.current_grid_cell = playerCell;
        npc.has_world_grid_position = true;
        if (!insideDungeon)
        {
            if (!string.IsNullOrWhiteSpace(playerLocationNodeId)) npc.current_node_id = playerLocationNodeId;
            npc.dungeon_id = "";
            npc.dungeon_node_id = "";
            npc.dungeon_previous_node_id = "";
            npc.current_subscene_id = "";
        }
        npc.current_state = "案卷押送同行";
        EmitSignal(SignalName.world_npcs_changed);
        return true;
    }

    public bool reset_yuxu_case_task_party_start(string incidentId, string taskId, string dutyInstanceId)
    {
        var task = get_yuxu_case_task(incidentId, taskId);
        if (task is null || task.status != YuxuSectCaseTaskRecord.StatusAssigned ||
            task.duty_instance_id != dutyInstanceId || task.journey_status is not (YuxuSectCaseTaskRecord.JourneyInTransit or YuxuSectCaseTaskRecord.JourneyExitingDungeon))
            return false;
        if (get_npc(task.journey_party_npc_id) is { } npc && npc.temporary_party_task_id == task.task_id)
        {
            npc.temporary_party_task_id = "";
            npc.current_state = "空闲";
        }
        task.temporary_party_member_active = false;
        task.journey_party_npc_id = "";
        task.journey_destination_node_id = "";
        task.journey_route.Clear();
        task.journey_next_index = 0;
        task.journey_remaining_hours = 0f;
        task.journey_status = YuxuSectCaseTaskRecord.JourneyNotStarted;
        task.updated_at = current_case_timestamp();
        EmitSignal(SignalName.world_npcs_changed);
        return true;
    }

    public bool update_yuxu_case_transport_position(
        string incidentId,
        string taskId,
        string dutyInstanceId,
        Vector2I playerCell,
        string locationNodeId,
        string dungeonNodeId,
        float remainingHours)
    {
        var task = get_yuxu_case_task(incidentId, taskId);
        if (task is null || !task.temporary_party_member_active || task.duty_instance_id != dutyInstanceId ||
            task.journey_status is not (YuxuSectCaseTaskRecord.JourneyExitingDungeon or YuxuSectCaseTaskRecord.JourneyInTransit or YuxuSectCaseTaskRecord.JourneyIntercepted))
            return false;
        var npc = get_npc(task.journey_party_npc_id);
        if (npc is null || !npc.is_alive || npc.temporary_party_task_id != task.task_id) return false;

        task.journey_current_cell = playerCell;
        task.journey_remaining_hours = Mathf.Max(0f, remainingHours);
        if (task.journey_route.Count > 0)
            for (var index = 0; index < task.journey_route.Count; index++)
                if (task.journey_route[index] == playerCell)
                {
                    task.journey_next_index = Math.Min(task.journey_route.Count, index + 1);
                    break;
                }

        npc.current_grid_cell = playerCell;
        npc.has_world_grid_position = true;
        if (!string.IsNullOrWhiteSpace(locationNodeId)) npc.current_node_id = locationNodeId;
        npc.current_subscene_id = "";
        npc.is_traveling = false;
        npc.travel_origin_node_id = "";
        npc.travel_destination_node_id = "";
        npc.world_travel_path.Clear();
        npc.world_travel_next_cell_index = 1;
        npc.world_travel_cell_progress_hours = 0f;
        if (!string.IsNullOrWhiteSpace(dungeonNodeId))
        {
            npc.dungeon_id = EastWoodsDungeonRules.DungeonId;
            npc.dungeon_node_id = dungeonNodeId;
            npc.current_state = "案卷押送同行";
        }
        else
        {
            npc.dungeon_id = "";
            npc.dungeon_node_id = "";
            npc.dungeon_previous_node_id = "";
            npc.current_state = "案卷押送途中";
        }
        task.updated_at = current_case_timestamp();
        EmitSignal(SignalName.world_npcs_changed);
        return true;
    }

    public bool set_yuxu_case_transport_world_route(
        string incidentId,
        string taskId,
        string dutyInstanceId,
        Vector2I playerCell,
        string locationNodeId,
        Array<Vector2I> route,
        float remainingHours)
    {
        var task = get_yuxu_case_task(incidentId, taskId);
        if (task is null || !task.temporary_party_member_active || task.duty_instance_id != dutyInstanceId ||
            task.journey_status is not (YuxuSectCaseTaskRecord.JourneyExitingDungeon or YuxuSectCaseTaskRecord.JourneyInTransit))
            return false;
        var npc = get_npc(task.journey_party_npc_id);
        if (npc is null || !npc.is_alive || npc.temporary_party_task_id != task.task_id) return false;
        task.journey_status = YuxuSectCaseTaskRecord.JourneyInTransit;
        task.journey_route = route?.Duplicate() ?? [];
        task.journey_next_index = task.journey_route.Count > 1 ? 1 : 0;
        task.journey_current_cell = playerCell;
        task.journey_remaining_hours = Mathf.Max(0f, remainingHours);
        task.updated_at = current_case_timestamp();
        npc.current_grid_cell = playerCell;
        npc.has_world_grid_position = true;
        if (!string.IsNullOrWhiteSpace(locationNodeId)) npc.current_node_id = locationNodeId;
        npc.current_subscene_id = "";
        npc.dungeon_id = "";
        npc.dungeon_node_id = "";
        npc.dungeon_previous_node_id = "";
        npc.is_traveling = false;
        npc.travel_origin_node_id = "";
        npc.travel_destination_node_id = "";
        npc.world_travel_path.Clear();
        npc.world_travel_next_cell_index = 1;
        npc.world_travel_cell_progress_hours = 0f;
        npc.current_state = "案卷押送途中";
        EmitSignal(SignalName.world_npcs_changed);
        return true;
    }

    public bool mark_yuxu_case_transport_intercepted(
        string incidentId,
        string taskId,
        string dutyInstanceId,
        string attackerNpcId,
        string source,
        Vector2I cell)
    {
        var task = get_yuxu_case_task(incidentId, taskId);
        if (task is null || !task.temporary_party_member_active || task.duty_instance_id != dutyInstanceId ||
            task.journey_status is not (YuxuSectCaseTaskRecord.JourneyExitingDungeon or YuxuSectCaseTaskRecord.JourneyInTransit) ||
            get_npc(attackerNpcId) is not { is_alive: true })
            return false;
        task.journey_phase_after_interception = task.journey_status;
        task.journey_status = YuxuSectCaseTaskRecord.JourneyIntercepted;
        task.journey_interception_npc_id = attackerNpcId;
        task.journey_interception_source = source;
        task.journey_interception_cell = cell;
        task.journey_interception_count++;
        task.journey_last_interception_result = "战斗待处理";
        task.updated_at = current_case_timestamp();
        return true;
    }

    public bool finish_yuxu_case_transport_interception(string incidentId, string taskId, string dutyInstanceId, string result)
    {
        var task = get_yuxu_case_task(incidentId, taskId);
        if (task is null || task.journey_status != YuxuSectCaseTaskRecord.JourneyIntercepted ||
            task.duty_instance_id != dutyInstanceId || !task.temporary_party_member_active ||
            task.journey_phase_after_interception is not (YuxuSectCaseTaskRecord.JourneyExitingDungeon or YuxuSectCaseTaskRecord.JourneyInTransit))
            return false;
        task.journey_last_interception_result = result;
        task.journey_status = task.journey_phase_after_interception;
        task.journey_phase_after_interception = "";
        task.updated_at = current_case_timestamp();
        return true;
    }

    public Dictionary try_find_yuxu_case_transport_interceptor(Vector2I cell, HumanData playerData, string incidentId, string taskId)
    {
        var task = get_yuxu_case_task(incidentId, taskId);
        if (playerData is null || task is null || !task.temporary_party_member_active ||
            task.journey_status != YuxuSectCaseTaskRecord.JourneyInTransit)
            return [];
        var time = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
        var actionStamp = time?.get_total_game_hours() ?? 0;
        foreach (var npc in npcs.Where(item => item is not null && item.is_alive && item.has_world_grid_position &&
                     item.current_grid_cell == cell && string.IsNullOrEmpty(item.dungeon_id) &&
                     string.IsNullOrEmpty(item.temporary_party_task_id))
                     .OrderBy(item => item.npc_id, StringComparer.Ordinal))
        {
            var chance = EastWoodsDungeonRules.attack_chance(
                npc,
                playerData.sect_affiliation_id,
                RegionalRenownSystem.get_bai_chuan_total(playerData),
                get_hostility_bias(npc, "player"),
                EastWoodsDungeonRules.realm_score(playerData.cultivation_realm));
            if (!EastWoodsDungeonRules.roll_attack($"{actionStamp}|{npc.npc_id}|case-transport|{task.task_id}|{cell.X},{cell.Y}", chance)) continue;
            npc.current_state = "押送途中拦截";
            record(npc, time is null ? 0 : month_index(time), "yuxu_case_transport_intercept", "拦截押送行程",
                "在案卷指定的押送路线与同行队伍相遇后主动发起战斗。", "进入由玩家应对的现有战斗；胜负不构成拘押事件。", 0, "player");
            return new Dictionary { ["attacker_npc_id"] = npc.npc_id, ["source"] = "world_map", ["cell"] = cell };
        }
        return [];
    }

    public bool mark_yuxu_case_transport_arrived(string incidentId, string taskId, string dutyInstanceId, string destinationNodeId)
    {
        var task = get_yuxu_case_task(incidentId, taskId);
        var npc = task is null ? null : get_npc(task.journey_party_npc_id);
        if (task is null || task.task_type != "appeal_transport" || !task.temporary_party_member_active ||
            task.status != YuxuSectCaseTaskRecord.StatusAssigned || task.duty_instance_id != dutyInstanceId ||
            task.journey_status != YuxuSectCaseTaskRecord.JourneyInTransit || task.journey_destination_node_id != destinationNodeId ||
            npc is null || !npc.is_alive || npc.temporary_party_task_id != task.task_id ||
            !string.IsNullOrEmpty(npc.dungeon_id) || npc.current_node_id != destinationNodeId)
            return false;
        task.journey_status = YuxuSectCaseTaskRecord.JourneyArrived;
        task.journey_remaining_hours = 0f;
        task.updated_at = current_case_timestamp();
        return true;
    }

    public bool handoff_yuxu_case_transport(string incidentId, string taskId, string dutyInstanceId, string officialNodeId, out string resultText)
    {
        resultText = "";
        var task = get_yuxu_case_task(incidentId, taskId);
        var npc = task is null ? null : get_npc(task.journey_party_npc_id);
        if (task is null || npc is null || task.task_type != "appeal_transport" ||
            task.status != YuxuSectCaseTaskRecord.StatusAssigned || task.duty_instance_id != dutyInstanceId ||
            task.journey_status != YuxuSectCaseTaskRecord.JourneyArrived ||
            task.journey_destination_node_id != "yuxu_xianzong_core" || officialNodeId != "zhizhong" ||
            !task.temporary_party_member_active || npc.temporary_party_task_id != task.task_id ||
            !npc.is_alive || !string.IsNullOrEmpty(npc.dungeon_id) || npc.current_node_id != task.journey_destination_node_id)
            return false;

        task.temporary_party_member_active = false;
        npc.temporary_party_task_id = "";
        npc.current_state = "已交由执中峰官员接待";
        task.journey_status = YuxuSectCaseTaskRecord.JourneyHandedOff;
        task.handoff_node_id = officialNodeId;
        task.handoff_at = current_case_timestamp();
        task.handoff_result = "指定当事人已由执中峰官员接手；同行状态解除，案卷复核可继续。";
        task.status = YuxuSectCaseTaskRecord.StatusObjectiveComplete;
        task.updated_at = task.handoff_at;
        resultText = task.handoff_result;
        EmitSignal(SignalName.world_npcs_changed);
        return true;
    }

    public bool apply_yuxu_case_transport_battle_snapshot(BattleEntityData snapshot)
    {
        if (snapshot is null || !snapshot.uid.StartsWith("worldnpc:", StringComparison.Ordinal)) return false;
        var npc = get_npc(snapshot.uid["worldnpc:".Length..]);
        if (npc is null || !npc.is_alive) return false;
        if (!string.IsNullOrEmpty(npc.temporary_party_task_id))
        {
            npc.current_health = Mathf.Clamp(snapshot.current_health, 1, npc.max_health);
            npc.current_state = "案卷押送途中负伤";
            record(npc, GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager") is { } time ? month_index(time) : 0,
                "yuxu_case_transport_battle", "押送途中遇袭", "作为案卷外勤临时队伍成员参与拦截战。",
                $"战后仍存活，生命 {npc.current_health}/{npc.max_health}；交接前继续同行。", 0);
        }
        else if (snapshot.current_health <= 0)
        {
            mark_dead(npc, GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager") is { } time ? month_index(time) : 0,
                "押送途中拦截战", "在拦截押送队伍的战斗中身亡；该结果不登记为案卷拘押或任务完成。");
        }
        else
        {
            npc.current_health = Mathf.Clamp(snapshot.current_health, 1, npc.max_health);
            npc.current_state = "拦截战后负伤";
            record(npc, GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager") is { } time ? month_index(time) : 0,
                "yuxu_case_transport_battle", "押送途中拦截战", "在押送行程中与押送队伍发生战斗。",
                $"战后生命 {npc.current_health}/{npc.max_health}。", 0, "player");
        }
        EmitSignal(SignalName.world_npcs_changed);
        return true;
    }

    public bool try_review_yuxu_case_appeal(string incidentId, YuxuSectLawDraftConfig config, out string resultText)
    {
        resultText = "";
        var incident = get_east_woods_incident(incidentId);
        var pendingAppeal = incident?.appeal_records?.LastOrDefault(record => record is not null && record.status == YuxuSectCaseAppealRecord.StatusPending);
        if (incident is null || pendingAppeal is null || incident.legal_status != YuxuSectDraftLawEvaluator.LegalStatusAppealPending ||
            incident.sanctions_executed || config is null)
            return false;

        var pendingTransport = incident.field_tasks?.FirstOrDefault(task => task is not null &&
            task.task_type == "appeal_transport" && task.target_npc_id == pendingAppeal.appellant_npc_id);
        if (pendingTransport is null || pendingTransport.journey_status != YuxuSectCaseTaskRecord.JourneyHandedOff ||
            pendingTransport.temporary_party_member_active)
        {
            resultText = "案卷指定当事人尚未由执中峰官员完成押送交接；案卷复核暂缓。";
            return false;
        }

        var originalRuleIds = incident.ruling_rule_ids?.ToArray() ?? [];
        var originalSanctionIds = incident.ruling_sanction_ids?.ToArray() ?? [];
        var originalOffender = incident.ruling_offender_npc_id;
        var decision = YuxuSectDraftLawEvaluator.Evaluate(incident, get_all_east_woods_incidents(), config);
        incident.ruling_rule_ids = decision.RuleIds.Duplicate();
        incident.ruling_sanction_ids = decision.SanctionIds.Duplicate();
        incident.ruling_offender_npc_id = decision.OffenderNpcId;
        incident.prior_final_offense_count = decision.PriorFinalFindings;
        incident.ruling_at = current_case_timestamp();
        incident.ruling_findings = decision.FindingText;
        incident.capture_warrant_authorized = decision.CaptureWarrantAuthorized;
        incident.capture_warrant_kill_authorized = decision.CaptureKillAuthorized;
        incident.capture_warrant_target_npc_id = decision.CaptureWarrantAuthorized ? decision.OffenderNpcId : "";

        var changed = originalOffender != decision.OffenderNpcId || !originalRuleIds.SequenceEqual(decision.RuleIds) ||
            !originalSanctionIds.SequenceEqual(decision.SanctionIds);
        pendingAppeal.status = changed ? YuxuSectCaseAppealRecord.StatusChanged : YuxuSectCaseAppealRecord.StatusUpheld;
        pendingAppeal.reviewed_at = current_case_timestamp();
        pendingAppeal.outcome = changed
            ? "复核发现现存结构化记录与原裁断不一致，已按复核结果更正；未改写来源事实。"
            : "复核未发现足以改变条款适用的结构化事实；维持原裁断。";
        incident.appeal_status = changed ? YuxuSectDraftLawEvaluator.AppealStatusChanged : YuxuSectDraftLawEvaluator.AppealStatusUpheld;
        incident.appeal_reviewed_at = pendingAppeal.reviewed_at;
        incident.appeal_outcome = pendingAppeal.outcome;
        incident.legal_status = YuxuSectDraftLawEvaluator.LegalStatusAppealReviewed;
        resultText = $"案卷 {incident.incident_id} 的申诉已由复核官完成审查：{pendingAppeal.outcome}";
        EmitSignal(SignalName.world_npcs_changed);
        return true;
    }

    public bool try_begin_yuxu_case_enforcement(string incidentId, YuxuSectLawDraftConfig config, out string resultText)
    {
        resultText = "";
        var incident = get_east_woods_incident(incidentId);
        if (incident is null || incident.sanctions_executed || config is null ||
            incident.legal_status is not (YuxuSectDraftLawEvaluator.LegalStatusRuled or YuxuSectDraftLawEvaluator.LegalStatusAppealReviewed))
            return false;

        incident.ruling_sanction_ids ??= [];
        incident.field_tasks ??= [];
        var offender = get_npc(incident.ruling_offender_npc_id);
        foreach (var sanctionId in incident.ruling_sanction_ids)
        {
            if (sanctionId == config.second_offense_sanction_id && offender is not null)
                offender.sect_case_demerit_count = Math.Max(0, offender.sect_case_demerit_count) + 1;
            else if (sanctionId == config.repeat_offense_sanction_id && offender is not null)
            {
                if (string.IsNullOrEmpty(offender.sect_case_former_faction_id))
                    offender.sect_case_former_faction_id = offender.faction_id;
                offender.sect_case_expelled_by_case_id = incident.incident_id;
                offender.faction_id = "yuxu_expelled";
                offender.formal_identity = "玉虚宗除名修士";
                // The warrant alone publishes no capture task and does not record an apprehension.
            }
            else if (sanctionId == config.first_offense_sanction_id)
            {
                // The reprimand is represented by the immutable ruling record; it does not alter hidden attitude values.
            }
        }

        if (incident.ruling_sanction_ids.Contains(config.restitution_sanction_id))
        {
            for (var index = 0; index < incident.item_transfers.Count; index++)
            {
                var transfer = incident.item_transfers[index];
                if (transfer is null || transfer.quantity <= 0 || transfer.to_npc_id != incident.ruling_offender_npc_id ||
                    transfer.from_npc_id == incident.ruling_offender_npc_id) continue;
                add_case_field_task(incident, new YuxuSectCaseTaskRecord
                {
                    task_id = $"restitution:{index}",
                    task_type = "restitution",
                    title = $"依案追还 · {transfer.item_path.Split('/').LastOrDefault() ?? "原物"}",
                    target_npc_id = transfer.to_npc_id,
                    target_npc_name = transfer.to_npc_name,
                    source_transfer_index = index,
                    goal = $"向{transfer.to_npc_name}（原持有人）追还案卷记录的原物 ×{transfer.quantity}。",
                    completion_conditions = "与指定被处分人当面办理原物追还；数量以案卷流水为准，不增不减。",
                    return_instruction = "回和光峰事务板回报。",
                    contribution_reward = config.field_task_contribution_reward,
                    kill_authorized = false,
                });
            }
        }

        incident.sanctions_executed = true;
        incident.sanctions_executed_at = current_case_timestamp();
        incident.legal_status = incident.field_tasks.Any(task => task is not null && task.is_open)
            ? YuxuSectDraftLawEvaluator.LegalStatusEnforcementPending
            : YuxuSectDraftLawEvaluator.LegalStatusClosed;
        resultText = incident.ruling_sanction_ids.Count == 0
            ? $"案卷 {incident.incident_id} 已执行“不予处分/证据不足”裁定。"
            : $"案卷 {incident.incident_id} 的裁定已由宗门执行；所需外勤事项已刊入和光峰事务板。";
        EmitSignal(SignalName.world_npcs_changed);
        return true;
    }

    public Array<Dictionary> get_yuxu_case_task_summaries(string activeIncidentId = "", string activeTaskId = "")
    {
        var result = new Array<Dictionary>();
        foreach (var incident in _east_woods_npc_incidents)
        {
            if (incident?.field_tasks is null) continue;
            foreach (var task in incident.field_tasks)
            {
                if (task is null) continue;
                var targetNpc = get_npc(task.target_npc_id);
                var targetLocation = targetNpc is null
                    ? "目标位置待核"
                    : !string.IsNullOrWhiteSpace(targetNpc.dungeon_id)
                        ? $"{get_node_name(targetNpc.current_node_id)}·林径 {targetNpc.dungeon_node_id}"
                        : targetNpc.is_traveling
                            ? $"{get_node_name(targetNpc.current_node_id)}（行旅中）"
                            : !string.IsNullOrWhiteSpace(targetNpc.current_node_id)
                                ? get_node_name(targetNpc.current_node_id)
                                : targetNpc.has_world_grid_position
                                    ? "世界行旅节点"
                                    : "目标位置待核";
                var isActiveForPlayer = incident.incident_id == activeIncidentId && task.task_id == activeTaskId &&
                    task.status is YuxuSectCaseTaskRecord.StatusAssigned or YuxuSectCaseTaskRecord.StatusObjectiveComplete;
                if (task.status != YuxuSectCaseTaskRecord.StatusAvailable && !isActiveForPlayer) continue;
                result.Add(new Dictionary
                {
                    ["incident_id"] = incident.incident_id,
                    ["task_id"] = task.task_id,
                    ["task_type"] = task.task_type,
                    ["title"] = task.title,
                    ["target_npc_id"] = task.target_npc_id,
                    ["target_npc_name"] = task.target_npc_name,
                    ["target_location"] = targetLocation,
                    ["goal"] = task.goal,
                    ["conditions"] = task.completion_conditions,
                    ["return_instruction"] = task.return_instruction,
                    ["reward"] = task.contribution_reward,
                    ["kill_authorized"] = task.kill_authorized,
                    ["status"] = task.status,
                    ["legal_status"] = incident.legal_status,
                    ["journey_status"] = task.journey_status,
                    ["journey_destination_node_id"] = task.journey_destination_node_id,
                    ["journey_remaining_hours"] = task.journey_remaining_hours,
                    ["temporary_party_member_active"] = task.temporary_party_member_active,
                });
            }
        }
        return result;
    }

    public YuxuSectCaseTaskRecord? get_yuxu_case_task(string incidentId, string taskId)
        => get_east_woods_incident(incidentId)?.field_tasks?.FirstOrDefault(task => task is not null && task.task_id == taskId);

    public YuxuSectCaseTaskRecord? get_active_yuxu_case_transport(HumanData playerData)
    {
        if (playerData is null || playerData.sect_active_yuxu_duty_id != "yuxu_case_field_task") return null;
        var task = get_yuxu_case_task(playerData.sect_active_yuxu_duty_incident_id, playerData.sect_active_yuxu_duty_task_id);
        return task is not null && task.task_type == "appeal_transport" &&
            task.status is YuxuSectCaseTaskRecord.StatusAssigned or YuxuSectCaseTaskRecord.StatusObjectiveComplete &&
            task.duty_instance_id == playerData.sect_active_yuxu_duty_instance_id
            ? task
            : null;
    }

    public bool try_execute_yuxu_restitution_task(string incidentId, string taskId, string dutyInstanceId, string npcId, out string resultText)
    {
        resultText = "";
        var incident = get_east_woods_incident(incidentId);
        var task = get_yuxu_case_task(incidentId, taskId);
        if (incident is not null) incident.item_transfers ??= [];
        if (incident is null || task is null || task.task_type != "restitution" ||
            task.status != YuxuSectCaseTaskRecord.StatusAssigned || task.duty_instance_id != dutyInstanceId ||
            task.target_npc_id != npcId || task.source_transfer_index < 0 ||
            task.source_transfer_index >= incident.item_transfers.Count)
            return false;

        var transfer = incident.item_transfers[task.source_transfer_index];
        var looter = get_npc(transfer?.to_npc_id ?? "");
        var originalHolder = get_npc(transfer?.from_npc_id ?? "");
        if (transfer is null || looter is null || originalHolder is null || !looter.is_alive || !originalHolder.is_alive ||
            string.IsNullOrWhiteSpace(transfer.item_path) || transfer.quantity <= 0)
        {
            resultText = "案卷指定当事人或原物记录已不适用；未改动任何物品。请交执中峰复核。";
            return false;
        }
        if (looter.get_carried_item_count(transfer.item_path) < transfer.quantity)
        {
            resultText = "被处分人当前随身物品不足以完整追还本笔原物；未扣除或补造物品。请交执中峰复核。";
            return false;
        }

        looter.change_carried_item(transfer.item_path, -transfer.quantity);
        originalHolder.change_carried_item(transfer.item_path, transfer.quantity);
        var time = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
        var month = time is null ? 0 : month_index(time);
        record(looter, month, "case_restitution", "依案追还原物", $"执中峰已生效裁断 {incidentId}。", $"实际转出 {transfer.item_path} ×{transfer.quantity} 给原持有人 {originalHolder.npc_name}。", -transfer.quantity, originalHolder.npc_id);
        if (looter.action_history.Count > 0) looter.action_history[looter.action_history.Count - 1].dungeon_incident_id = incidentId;
        record(originalHolder, month, "case_restitution_received", "收回案涉原物", $"执中峰已生效裁断 {incidentId}。", $"实际收回 {transfer.item_path} ×{transfer.quantity}，来源为 {looter.npc_name}。", transfer.quantity, looter.npc_id);
        if (originalHolder.action_history.Count > 0) originalHolder.action_history[originalHolder.action_history.Count - 1].dungeon_incident_id = incidentId;
        task.status = YuxuSectCaseTaskRecord.StatusObjectiveComplete;
        task.updated_at = current_case_timestamp();
        resultText = $"已按案卷流水，将 {transfer.item_path} ×{transfer.quantity} 从 {looter.npc_name} 实际转回原持有人 {originalHolder.npc_name}；请回和光峰报告。";
        EmitSignal(SignalName.world_npcs_changed);
        EmitSignal(SignalName.npc_action_recorded, looter.npc_id, looter.action_history[looter.action_history.Count - 1]);
        EmitSignal(SignalName.npc_action_recorded, originalHolder.npc_id, originalHolder.action_history[originalHolder.action_history.Count - 1]);
        return true;
    }

    private static void add_case_field_task(EastWoodsNpcIncidentRecord incident, YuxuSectCaseTaskRecord task)
    {
        incident.field_tasks ??= [];
        if (!incident.field_tasks.Any(existing => existing is not null && existing.task_id == task.task_id))
            incident.field_tasks.Add(task);
    }

    public bool reconcile_east_woods_submissions(System.Func<string, bool> hasContributionTransaction)
    {
        if (hasContributionTransaction is null) return false;
        var changed = false;
        foreach (var incident in _east_woods_npc_incidents)
        {
            if (incident is null || !hasContributionTransaction(incident.incident_id)) continue;
            if (incident.case_status == EastWoodsNpcIncidentRecord.StatusSubmittedPendingRuling &&
                string.IsNullOrWhiteSpace(incident.assigned_duty_instance_id)) continue;
            incident.case_status = EastWoodsNpcIncidentRecord.StatusSubmittedPendingRuling;
            incident.assigned_duty_instance_id = "";
            changed = true;
        }
        return changed;
    }

    public bool reconcile_east_woods_assignments(string activeIncidentId, string activeDutyInstanceId)
    {
        var changed = false;
        foreach (var incident in _east_woods_npc_incidents)
        {
            if (incident is null || incident.case_status != EastWoodsNpcIncidentRecord.StatusAssigned) continue;
            if (incident.incident_id == activeIncidentId && incident.assigned_duty_instance_id == activeDutyInstanceId) continue;
            incident.case_status = EastWoodsNpcIncidentRecord.StatusAvailable;
            incident.assigned_duty_instance_id = "";
            changed = true;
        }
        return changed;
    }

    public int get_node_npc_count(string node_id) => get_node_roster(node_id).Count;

    /// <summary>世界地图节点人数包含驻留者，以及当前格正处于该节点占地内的行旅者。</summary>
    public int get_world_location_population_count(string nodeId)
    {
        if (_tile_world_map is null) return get_node_npc_count(nodeId);
        var count = 0;
        foreach (var npc in npcs)
        {
            if (npc is null || !npc.is_alive || !string.IsNullOrEmpty(npc.dungeon_id) || !npc.has_world_grid_position ||
                !string.IsNullOrEmpty(npc.temporary_party_task_id)) continue;
            if (!npc.is_traveling && npc.current_node_id == nodeId)
            {
                count++;
                continue;
            }
            if (npc.is_traveling && get_location_node_id_at_cell(npc.current_grid_cell) == nodeId) count++;
        }
        return count;
    }

    /// <summary>Build every map location count in one NPC pass for the visible world map.</summary>
    public Godot.Collections.Dictionary get_world_location_population_counts()
    {
        var counts = new Godot.Collections.Dictionary();
        if (_tile_world_map is null)
        {
            foreach (var npc in npcs)
            {
                if (npc is null || !npc.is_alive || npc.is_traveling || string.IsNullOrEmpty(npc.current_node_id)) continue;
                counts[npc.current_node_id] = counts.ContainsKey(npc.current_node_id) ? counts[npc.current_node_id].AsInt32() + 1 : 1;
            }
            return counts;
        }

        foreach (var npc in npcs)
        {
            if (npc is null || !npc.is_alive || !string.IsNullOrEmpty(npc.dungeon_id) || !npc.has_world_grid_position ||
                !string.IsNullOrEmpty(npc.temporary_party_task_id)) continue;
            var nodeId = npc.is_traveling ? get_location_node_id_at_cell(npc.current_grid_cell) : npc.current_node_id;
            if (string.IsNullOrEmpty(nodeId)) continue;
            counts[nodeId] = counts.ContainsKey(nodeId) ? counts[nodeId].AsInt32() + 1 : 1;
        }
        return counts;
    }

    /// <summary>Counts the same living world NPCs shown by the node roster, keyed by graph node ID.</summary>
    public Godot.Collections.Dictionary get_node_world_population_counts()
    {
        var counts = new Godot.Collections.Dictionary();
        if (_tile_world_map?.HasMethod("cell_to_node_id") != true) return counts;
        var locationGraphIds = new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var npc in npcs)
        {
            if (npc is null || !npc.is_alive || !npc.has_world_grid_position || !string.IsNullOrEmpty(npc.dungeon_id)) continue;
            string id;
            if (npc.is_traveling || string.IsNullOrEmpty(npc.current_node_id))
                id = node_world_node_id_for_cell(npc.current_grid_cell);
            else if (!locationGraphIds.TryGetValue(npc.current_node_id, out id!))
            {
                id = node_world_node_id_for_cell(get_location_anchor_cell(npc.current_node_id));
                locationGraphIds[npc.current_node_id] = id;
            }
            if (string.IsNullOrEmpty(id)) continue;
            counts[id] = counts.ContainsKey(id) ? counts[id].AsInt32() + 1 : 1;
        }
        return counts;
    }

    /// <summary>普通格的人数徽标只统计当前实际经过该格的行旅者。</summary>
    public Godot.Collections.Dictionary get_ordinary_grid_population_counts()
    {
        var counts = new Godot.Collections.Dictionary();
        if (_tile_world_map is null) return counts;
        foreach (var npc in npcs)
        {
            if (npc is null || !npc.is_alive || !npc.is_traveling || !npc.has_world_grid_position || !string.IsNullOrEmpty(npc.dungeon_id)) continue;
            if (!string.IsNullOrEmpty(get_location_node_id_at_cell(npc.current_grid_cell))) continue;
            var cell = npc.current_grid_cell;
            counts[cell] = counts.ContainsKey(cell) ? counts[cell].AsInt32() + 1 : 1;
        }
        return counts;
    }

    /// <summary>地点占地内共用稳定节点名单；普通格只显示当前真实落在该格的人物。</summary>
    public Array<WorldNpcData> get_world_grid_roster(Vector2I cell, string locationNodeId = "")
    {
        var result = new Array<WorldNpcData>();
        if (_worldGridRosterCacheDirty) rebuild_world_grid_roster_cache();
        var cached = string.IsNullOrEmpty(locationNodeId)
            ? _ordinaryCellRosterCache.GetValueOrDefault(cell)
            : _worldNodeRosterCache.GetValueOrDefault(locationNodeId);
        if (cached is null) return result;
        foreach (var npc in cached)
            result.Add(npc);
        return result;
    }

    private void invalidate_world_grid_roster_cache()
    {
        _worldGridRosterCacheDirty = true;
    }

    private void rebuild_world_grid_roster_cache()
    {
        _worldNodeRosterCache.Clear();
        _ordinaryCellRosterCache.Clear();
        foreach (var npc in npcs)
        {
            if (npc is null || !npc.is_alive || !npc.has_world_grid_position || !string.IsNullOrEmpty(npc.dungeon_id)) continue;
            if (!_ordinaryCellRosterCache.TryGetValue(npc.current_grid_cell, out var cellRoster))
                _ordinaryCellRosterCache[npc.current_grid_cell] = cellRoster = [];
            cellRoster.Add(npc);

            var nodeId = !npc.is_traveling ? npc.current_node_id : get_location_node_id_at_cell(npc.current_grid_cell);
            if (string.IsNullOrEmpty(nodeId)) continue;
            if (!_worldNodeRosterCache.TryGetValue(nodeId, out var nodeRoster))
                _worldNodeRosterCache[nodeId] = nodeRoster = [];
            nodeRoster.Add(npc);
        }
        _worldGridRosterCacheDirty = false;
    }

    /// <summary>返回地点下的空间划分，供地点人物名单按子场景分组。</summary>
    public Array<WorldSubsceneData> get_node_subscenes(string node_id)
    {
        var result = new Array<WorldSubsceneData>();
        var node = _map_data?.get_node(node_id);
        if (node is null) return result;
        foreach (var subscene in node.subscenes)
            if (subscene is not null) result.Add(subscene);
        return result;
    }

    public string get_subscene_name(string node_id, string subscene_id)
        => get_node_subscenes(node_id).FirstOrDefault(subscene => subscene.subscene_id == subscene_id)?.subscene_name
           ?? (string.IsNullOrWhiteSpace(subscene_id) ? "地点内" : subscene_id);

    public string get_node_name(string node_id)
        => _map_data?.get_node(node_id)?.node_name
           ?? (string.IsNullOrWhiteSpace(node_id) ? "未定地点" : node_id);

    /// <summary>把人物放入一个已存在的节点/子场景；后续旅行系统也复用这一入口。</summary>
    public bool place_npc(string npc_id, string node_id, string subscene_id = "", string state = "空闲")
    {
        var npc = get_npc(npc_id);
        var node = _map_data?.get_node(node_id);
        if (npc is null || node is null) return false;
        if (!string.IsNullOrWhiteSpace(subscene_id) && node.subscenes.All(item => item is null || item.subscene_id != subscene_id)) return false;
        npc.current_node_id = node_id;
        npc.current_subscene_id = subscene_id;
        npc.travel_origin_node_id = "";
        npc.travel_destination_node_id = "";
        npc.travel_days_remaining = 0;
        npc.travel_days_total = 0;
        npc.is_traveling = false;
        npc.world_travel_path.Clear();
        npc.world_travel_next_cell_index = 1;
        npc.world_travel_cell_progress_hours = 0f;
        set_npc_grid_position(npc, get_location_anchor_cell(node_id));
        npc.current_state = state;
        npc.dungeon_id = "";
        npc.dungeon_node_id = "";
        npc.dungeon_previous_node_id = "";
        npc.dungeon_travel_progress = 0f;
        EmitSignal(SignalName.world_npcs_changed);
        return true;
    }

    public Array<NpcActionRecord> get_npc_history(string npc_id)
    {
        var result = new Array<NpcActionRecord>();
        var npc = get_npc(npc_id);
        if (npc is null) return result;
        foreach (var record in npc.action_history)
            if (record is not null) result.Add(record);
        return result;
    }

    /// <summary>节点名单包含该节点所有子场景；在途与死亡人物均不列入。</summary>
    public Array<WorldNpcData> get_node_roster(string node_id)
    {
        var result = new Array<WorldNpcData>();
        foreach (var npc in npcs)
            if (npc.is_alive && !npc.is_traveling && npc.current_node_id == node_id) result.Add(npc);
        return result;
    }

    /// <summary>子场景名单只返回实际在场的在世人物。</summary>
    public Array<WorldNpcData> get_subscene_roster(string node_id, string subscene_id)
    {
        var result = new Array<WorldNpcData>();
        foreach (var npc in npcs)
            if (npc.is_alive && !npc.is_traveling && string.IsNullOrWhiteSpace(npc.dungeon_id) && npc.current_node_id == node_id && npc.current_subscene_id == subscene_id) result.Add(npc);
        return result;
    }

    /// <summary>驻留东林地的人物分散入场；仅迁移旧版未行动的入口堆叠，再次进入不重置行程。</summary>
    public void enter_east_woods_route()
    {
        var changed = false;
        var spawnNodes = EastWoodsDungeonRules.get_nodes().OrderBy(node => node.Id, StringComparer.Ordinal).ToArray();
        string InitialNode(WorldNpcData npc) => spawnNodes[(int)((uint)EastWoodsDungeonRules.stable_seed(
            $"spawn|{EastWoodsDungeonRules.LayoutSeed}|{npc.npc_id}") % (uint)spawnNodes.Length)].Id;
        foreach (var npc in npcs.Where(npc => npc is not null && npc.is_alive && !npc.is_traveling && npc.current_node_id == "east_woods"))
        {
            if (npc.dungeon_id == EastWoodsDungeonRules.DungeonId)
            {
                // Migrate only the old untouched entrance placement, never an actual journey.
                if (npc.dungeon_placement_version < 1)
                {
                    if (npc.dungeon_node_id == "forest_edge" && npc.dungeon_step_index == 0 &&
                        npc.dungeon_travel_progress == 0 && string.IsNullOrEmpty(npc.dungeon_previous_node_id))
                        npc.dungeon_node_id = InitialNode(npc);
                    npc.dungeon_placement_version = 1;
                    changed = true;
                }
                if (!string.IsNullOrWhiteSpace(npc.current_subscene_id))
                {
                    npc.current_subscene_id = "";
                    changed = true;
                }
                continue;
            }
            if (!string.IsNullOrWhiteSpace(npc.dungeon_id)) continue;
            npc.dungeon_id = EastWoodsDungeonRules.DungeonId;
            npc.dungeon_node_id = InitialNode(npc);
            npc.dungeon_placement_version = 1;
            npc.dungeon_previous_node_id = "";
            npc.dungeon_travel_progress = 0f;
            npc.dungeon_step_index = 0;
            npc.current_subscene_id = "";
            npc.dun_speed = Mathf.Max(0.01f, npc.dun_speed);
            npc.current_state = "深入东林地";
            changed = true;
        }
        if (changed) EmitSignal(SignalName.world_npcs_changed);
    }

    public Array<WorldNpcData> get_dungeon_node_roster(string dungeonId, string routeNodeId)
    {
        var result = new Array<WorldNpcData>();
        foreach (var npc in npcs)
            if (npc is not null && npc.is_alive && npc.dungeon_id == dungeonId && npc.dungeon_node_id == routeNodeId)
                result.Add(npc);
        return result;
    }

    /// <summary>玩家走过一条路线边后，按各自遁速累积 NPC 行程，并返回本次唯一遭遇。</summary>
    public Dictionary advance_east_woods_route(string playerFromNodeId, string playerRouteNodeId, float elapsedGameHours, HumanData? playerData)
    {
        // Scan and order the East Woods roster once per route edge, then reuse it
        // for movement, player encounters, and NPC conflict checks.
        var local = npcs.Where(npc => npc is not null && npc.is_alive && npc.dungeon_id == EastWoodsDungeonRules.DungeonId &&
                string.IsNullOrEmpty(npc.temporary_party_task_id))
            .OrderBy(npc => npc.npc_id, StringComparer.Ordinal).ToArray();
        var moved = new HashSet<string>();
        var previousNodes = new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal);
        var time = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
        var actionStamp = time?.get_total_game_hours() ?? 0;
        foreach (var npc in local)
        {
            previousNodes[npc.npc_id] = npc.dungeon_node_id;
            npc.dungeon_travel_progress += Mathf.Max(0f, elapsedGameHours);
            var edgeHours = EastWoodsDungeonRules.npc_edge_cost(npc.dun_speed);
            while (npc.dungeon_travel_progress + 0.0001f >= edgeHours)
            {
                npc.dungeon_travel_progress -= edgeHours;
                var from = npc.dungeon_node_id;
                var to = choose_next_dungeon_node(npc);
                if (string.IsNullOrWhiteSpace(to)) break;
                npc.dungeon_previous_node_id = from;
                npc.dungeon_node_id = to;
                npc.dungeon_step_index++;
                npc.current_state = "林中行进";
                record_dungeon_action(npc, "dungeon_move", "林中行进", $"依遁速 {npc.dun_speed:0.#} 沿副本路线前往下一处。", $"由 {from} 抵达 {to}。", to, fromRouteNodeId: from, toRouteNodeId: to);
                moved.Add(npc.npc_id);
                DungeonNodeStepped?.Invoke(npc, from, to);
            }
        }

        if (local.Length > 0) notify_east_woods_route_changed();
        if (moved.Count == 0) return [];
        if (playerData is not null)
        {
            foreach (var npc in local)
            {
                var crossedPlayer = previousNodes.TryGetValue(npc.npc_id, out var previousNode) &&
                    previousNode == playerRouteNodeId && npc.dungeon_node_id == playerFromNodeId;
                if (npc.dungeon_node_id != playerRouteNodeId && !crossedPlayer) continue;
                var chance = EastWoodsDungeonRules.attack_chance(
                    npc,
                    playerData.sect_affiliation_id,
                    RegionalRenownSystem.get_bai_chuan_total(playerData),
                    get_hostility_bias(npc, "player"),
                    EastWoodsDungeonRules.realm_score(playerData.cultivation_realm));
                var progressStamp = Mathf.RoundToInt(npc.dungeon_travel_progress * 10000f);
                if (!EastWoodsDungeonRules.roll_attack($"{actionStamp}|{npc.npc_id}|player|{npc.dungeon_step_index}|{progressStamp}|{playerRouteNodeId}", chance)) continue;
                npc.current_state = "向玩家出手";
                record_dungeon_action(npc, "dungeon_attack", "突袭", $"性情、百川声望、势力态度、私人关系与境界共同影响出手判断；短测概率 {chance:P0}。", "在副本路线与玩家相遇后主动发起战斗。", npc.dungeon_node_id, "player");
                notify_east_woods_route_changed();
                return new Dictionary { ["attacker_npc_id"] = npc.npc_id, ["target_player"] = true, ["route_node_id"] = playerRouteNodeId };
            }
        }

        // Candidate collisions are local to a route node or a pair of crossed edges.
        // Group first so a sparse dungeon does not compare every resident with every other one.
        var occupantsByNode = new System.Collections.Generic.Dictionary<string, List<int>>(StringComparer.Ordinal);
        var moversByEdge = new System.Collections.Generic.Dictionary<(string From, string To), List<int>>();
        for (var index = 0; index < local.Length; index++)
        {
            var npc = local[index];
            if (!occupantsByNode.TryGetValue(npc.dungeon_node_id, out var occupants))
                occupantsByNode[npc.dungeon_node_id] = occupants = [];
            occupants.Add(index);
            if (!moved.Contains(npc.npc_id) || !previousNodes.TryGetValue(npc.npc_id, out var previousNode)) continue;
            var edge = (previousNode, npc.dungeon_node_id);
            if (!moversByEdge.TryGetValue(edge, out var edgeMovers)) moversByEdge[edge] = edgeMovers = [];
            edgeMovers.Add(index);
        }

        var collisionPairSet = new HashSet<(int Left, int Right)>();
        foreach (var occupants in occupantsByNode.Values)
        for (var left = 0; left < occupants.Count; left++)
        for (var right = left + 1; right < occupants.Count; right++)
        {
            var leftIndex = occupants[left];
            var rightIndex = occupants[right];
            if (!moved.Contains(local[leftIndex].npc_id) && !moved.Contains(local[rightIndex].npc_id)) continue;
            collisionPairSet.Add((leftIndex, rightIndex));
        }
        foreach (var (edge, edgeMovers) in moversByEdge)
        {
            if (string.CompareOrdinal(edge.From, edge.To) >= 0 ||
                !moversByEdge.TryGetValue((edge.To, edge.From), out var reverseMovers)) continue;
            foreach (var leftIndex in edgeMovers)
            foreach (var rightIndex in reverseMovers)
                collisionPairSet.Add(leftIndex < rightIndex ? (leftIndex, rightIndex) : (rightIndex, leftIndex));
        }
        var collisionPairs = collisionPairSet.ToList();
        collisionPairs.Sort(static (left, right) => left.Left != right.Left
            ? left.Left.CompareTo(right.Left)
            : left.Right.CompareTo(right.Right));

        foreach (var (leftIndex, rightIndex) in collisionPairs)
        {
            var first = local[leftIndex];
            var second = local[rightIndex];
            if (should_npc_attack(first, second, actionStamp, out var firstChance))
            {
                record_dungeon_action(first, "dungeon_attack", "突袭", $"与 {second.npc_name} 相遇；短测概率 {firstChance:P0}。", "沿途 NPC 冲突按既有战斗规则由双方 AI 在后台自动完成。", first.dungeon_node_id, second.npc_id);
                return new Dictionary { ["attacker_npc_id"] = first.npc_id, ["target_npc_id"] = second.npc_id, ["target_player"] = false, ["route_node_id"] = first.dungeon_node_id, ["attack_trigger_id"] = "npc_opportunistic_attack" };
            }
            if (should_npc_attack(second, first, actionStamp, out var secondChance))
            {
                record_dungeon_action(second, "dungeon_attack", "突袭", $"与 {first.npc_name} 相遇；短测概率 {secondChance:P0}。", "沿途 NPC 冲突按既有战斗规则由双方 AI 在后台自动完成。", second.dungeon_node_id, first.npc_id);
                return new Dictionary { ["attacker_npc_id"] = second.npc_id, ["target_npc_id"] = first.npc_id, ["target_player"] = false, ["route_node_id"] = second.dungeon_node_id, ["attack_trigger_id"] = "npc_opportunistic_attack" };
            }
        }
        return [];
    }

    public void apply_dungeon_battle_snapshot(BattleEntityData snapshot)
    {
        if (snapshot is null || !snapshot.uid.StartsWith("worldnpc:", StringComparison.Ordinal)) return;
        var npc = get_npc(snapshot.uid["worldnpc:".Length..]);
        if (npc is null || !npc.is_alive) return;
        npc.current_health = Mathf.Clamp(snapshot.current_health, 0, npc.max_health);
        if (npc.current_health <= 0)
        {
            var time = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
            mark_dead(npc, time is null ? 0 : month_index(time), "东林地副本冲突", "在副本遭遇战中伤重身亡；人物 ID 与死因保留在百川 NPC 存档。 ");
        }
        else
        {
            npc.current_state = "遭遇后负伤";
            record_dungeon_action(npc, "dungeon_battle", "副本冲突", "与另一名修行者发生战斗。", $"战后生命 {npc.current_health}/{npc.max_health}。", npc.dungeon_node_id);
        }
        EmitSignal(SignalName.world_npcs_changed);
    }

    public string remove_one_carried_item(WorldNpcData npc)
    {
        if (npc is null || npc.carried_items.Count == 0) return "";
        var choices = new List<string>();
        foreach (var key in npc.carried_items.Keys)
        {
            var path = key.AsString();
            if (npc.get_carried_item_count(path) > 0 && ResourceLoader.Load<ItemData>(path) is not null) choices.Add(path);
        }
        choices.Sort(StringComparer.Ordinal);
        if (choices.Count == 0) return "";
        var time = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
        var seed = EastWoodsDungeonRules.stable_seed($"loot|{npc.npc_id}|{npc.dungeon_step_index}|{time?.get_total_game_hours() ?? 0}");
        var index = (int)(seed % (ulong)choices.Count);
        var selected = choices[index];
        npc.change_carried_item(selected, -1);
        EmitSignal(SignalName.world_npcs_changed);
        return selected;
    }

    public void add_carried_item(WorldNpcData npc, string itemPath, int count = 1)
    {
        if (npc is null || count <= 0 || ResourceLoader.Load<ItemData>(itemPath) is null) return;
        npc.change_carried_item(itemPath, count);
        EmitSignal(SignalName.world_npcs_changed);
    }

    public int transfer_all_carried_items(WorldNpcData from, WorldNpcData to)
        => transfer_all_carried_items_with_records(from, to).Sum(transfer => transfer.quantity);

    public Array<EastWoodsNpcItemTransferRecord> transfer_all_carried_items_with_records(WorldNpcData from, WorldNpcData to)
    {
        var transfers = new Array<EastWoodsNpcItemTransferRecord>();
        if (from is null || to is null) return transfers;
        var paths = new List<string>();
        foreach (var key in from.carried_items.Keys) paths.Add(key.AsString());
        foreach (var path in paths)
        {
            var count = from.get_carried_item_count(path);
            if (count <= 0 || ResourceLoader.Load<ItemData>(path) is null) continue;
            from.change_carried_item(path, -count);
            to.change_carried_item(path, count);
            transfers.Add(new EastWoodsNpcItemTransferRecord
            {
                item_path = path,
                quantity = count,
                from_npc_id = from.npc_id,
                from_npc_name = from.npc_name,
                to_npc_id = to.npc_id,
                to_npc_name = to.npc_name,
            });
        }
        if (transfers.Count > 0) EmitSignal(SignalName.world_npcs_changed);
        return transfers;
    }

    public void record_dungeon_battle_outcome(WorldNpcData npc, string targetId, string outcome, string itemSummary, string incidentId = "")
    {
        if (npc is null) return;
        var text = string.IsNullOrWhiteSpace(itemSummary) ? outcome : $"{outcome}；{itemSummary}";
        record_dungeon_action(npc, "dungeon_battle_result", "战斗结算", "依据正式战斗结果结算伤亡与随身物品。", text, npc.dungeon_node_id, targetId, !npc.is_alive, incidentId: incidentId);
        EmitSignal(SignalName.world_npcs_changed);
    }

    public Array<WorldNpcData> get_travelers()
    {
        var result = new Array<WorldNpcData>();
        foreach (var npc in npcs) if (npc.is_alive && npc.is_traveling) result.Add(npc);
        return result;
    }

    private void on_time_changed()
    {
        if (_deferSettlement || IsSettling) return;
        if (!simulation_enabled) return;
        var time = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
        if (time is null) return;
        var currentHour = time.get_total_game_hours();
        var currentMonth = month_index(time);
        // Repeated notifications in the same hour have no settlement work.
        if (_last_world_grid_hour >= 0 && currentHour == _last_world_grid_hour &&
            _last_processed_month >= currentMonth)
            return;
        begin_settlement_batch();
        try { foreach (var _ in settle_time_steps(currentHour, currentMonth)) { } }
        finally { end_settlement_batch(); }
    }

    private System.Collections.Generic.IEnumerable<int> settle_time_steps(int nowHour, int targetMonth)
    {
        if (!simulation_enabled)
        {
            yield break;
        }
        if (_last_world_grid_hour < 0 || _last_processed_month < 0)
        {
            _last_world_grid_hour = nowHour;
            _last_processed_month = targetMonth;
            yield break;
        }
        if (nowHour < _last_world_grid_hour || targetMonth < _last_processed_month)
        {
            // 读档/测试回拨历法时不倒放 NPC 行程，也不补演未来月份。
            _last_world_grid_hour = nowHour;
            _last_processed_month = targetMonth;
            yield break;
        }

        var cursorHour = _last_world_grid_hour;
        for (var month = _last_processed_month + 1; month <= targetMonth; month++)
        {
            var monthBoundaryHour = month * 30 * 24;
            if (monthBoundaryHour > cursorHour)
            {
                foreach (var step in advance_world_grid_travel_steps(monthBoundaryHour - cursorHour, cursorHour)) yield return step;
                cursorHour = monthBoundaryHour;
            }
            foreach (var step in simulate_month_steps(month)) yield return step;
            _last_processed_month = month;
            // Month boundaries commit both travel and monthly world simulation.
            cursorHour = Math.Max(cursorHour, monthBoundaryHour);
            _last_world_grid_hour = cursorHour;
        }
        // Commit month-internal travel, so a later month boundary never replays it.
        if (nowHour > cursorHour)
        {
            foreach (var step in advance_world_grid_travel_steps(nowHour - cursorHour, cursorHour)) yield return step;
            _last_world_grid_hour = nowHour;
        }
    }

    private System.Collections.Generic.IEnumerable<int> advance_world_grid_travel_steps(int elapsedHours, int segmentStartHour)
    {
        if (_tile_world_map is null || elapsedHours <= 0) yield break;
        var anyCellChanged = false;
        foreach (var npc in npcs
                     .Where(item => item is not null && item.is_alive && item.is_traveling && item.has_world_grid_position && string.IsNullOrEmpty(item.dungeon_id))
                     .OrderBy(item => item.npc_id, StringComparer.Ordinal))
        {
            var remainingHours = (double)elapsedHours;
            var usedHours = 0.0;
            var guard = 0;
            while (remainingHours > 0.0001 && npc.world_travel_next_cell_index < npc.world_travel_path.Count && guard++ < 40000)
            {
                var nextCell = npc.world_travel_path[npc.world_travel_next_cell_index];
                var edgeHours = get_npc_cell_travel_hours(npc.current_grid_cell, nextCell, npc.dun_speed);
                if (!double.IsFinite(edgeHours) || edgeHours <= 0.0) break;
                var neededHours = Math.Max(0.0, edgeHours - npc.world_travel_cell_progress_hours);
                if (remainingHours + 0.0001 < neededHours)
                {
                    npc.world_travel_cell_progress_hours += (float)remainingHours;
                    usedHours += remainingHours;
                    remainingHours = 0.0;
                    break;
                }

                remainingHours -= neededHours;
                usedHours += neededHours;
                npc.current_grid_cell = nextCell;
                npc.world_travel_cell_progress_hours = 0f;
                npc.world_travel_next_cell_index++;
                anyCellChanged = true;
                if (npc.world_travel_next_cell_index >= npc.world_travel_path.Count)
                {
                    var arrivalHour = segmentStartHour + usedHours;
                    var arrivalMonth = Math.Max(0, (int)Math.Floor(arrivalHour / (30.0 * 24.0)));
                    complete_world_grid_travel(npc, arrivalMonth);
                    break;
                }
            }

            if (npc.is_traveling)
            {
                var remainingTripHours = get_remaining_world_travel_hours(npc);
                npc.travel_days_remaining = Math.Max(1, Mathf.CeilToInt((float)(remainingTripHours / 24.0)));
            }
            yield return 1;
        }
        if (anyCellChanged) notify_settlement_changed();
    }

    private void complete_world_grid_travel(WorldNpcData npc, int month)
    {
        var origin = npc.travel_origin_node_id;
        var destination = npc.travel_destination_node_id;
        npc.current_node_id = destination;
        var destinationNode = _map_data?.get_node(destination);
        npc.current_subscene_id = destinationNode is null ? "" : choose_arrival_subscene(destinationNode, month, npc.npc_id);
        npc.current_state = "抵达";
        npc.is_traveling = false;
        npc.travel_origin_node_id = "";
        npc.travel_destination_node_id = "";
        var totalDays = Math.Max(1, npc.travel_days_total);
        npc.travel_days_remaining = 0;
        npc.travel_days_total = 0;
        npc.world_travel_path.Clear();
        npc.world_travel_next_cell_index = 1;
        npc.world_travel_cell_progress_hours = 0f;
        var subsceneNote = string.IsNullOrWhiteSpace(npc.current_subscene_id)
            ? ""
            : $"，进入 {get_subscene_name(destination, npc.current_subscene_id)}";
        record(npc, month, "arrive", "抵达", "按遁速沿节点路线完成行旅。", $"抵达 {destination}{subsceneNote}。", 0, "", false, origin, destination, totalDays, 0);
    }

    private double get_remaining_world_travel_hours(WorldNpcData npc)
    {
        if (_tile_world_map is null || npc.world_travel_path.Count == 0) return 0.0;
        var totalHours = 0.0;
        for (var index = npc.world_travel_next_cell_index; index < npc.world_travel_path.Count; index++)
            totalHours += get_npc_cell_travel_hours(index == npc.world_travel_next_cell_index ? npc.current_grid_cell : npc.world_travel_path[index - 1], npc.world_travel_path[index], npc.dun_speed);
        return Math.Max(0.0, totalHours - npc.world_travel_cell_progress_hours);
    }

    private void initialize_world_grid_position(WorldNpcData npc)
    {
        if (_tile_world_map is null || npc is null) return;
        if (!npc.is_traveling)
        {
            if (!npc.has_world_grid_position || !is_valid_world_grid_cell(npc.current_grid_cell))
                set_npc_grid_position(npc, get_location_anchor_cell(npc.current_node_id));
            return;
        }

        if (npc.has_world_grid_position && is_valid_world_grid_cell(npc.current_grid_cell) &&
            npc.world_travel_path.Count > 1 && npc.world_travel_next_cell_index < npc.world_travel_path.Count)
            return;

        if (!npc.has_world_grid_position || !is_valid_world_grid_cell(npc.current_grid_cell))
            set_npc_grid_position(npc, get_location_anchor_cell(npc.travel_origin_node_id));
        if (!npc.has_world_grid_position) return;
        var destinationCell = get_location_anchor_cell(npc.travel_destination_node_id);
        var path = get_npc_grid_path(npc.current_grid_cell, destinationCell);
        if (path.Count > 1)
        {
            npc.world_travel_path.Clear();
            foreach (var cell in path) npc.world_travel_path.Add(cell);
            npc.world_travel_next_cell_index = 1;
            npc.world_travel_cell_progress_hours = 0f;
            var remainingDays = Math.Max(1, Mathf.CeilToInt((float)(get_remaining_world_travel_hours(npc) / 24.0)));
            npc.travel_days_remaining = remainingDays;
            npc.travel_days_total = Math.Max(npc.travel_days_total, remainingDays);
        }
    }

    private void set_npc_grid_position(WorldNpcData npc, Vector2I cell)
    {
        npc.has_world_grid_position = _tile_world_map is not null && is_valid_world_grid_cell(cell) &&
            _tile_world_map.Call("is_cell_traversable", cell).AsBool();
        npc.current_grid_cell = npc.has_world_grid_position ? cell : new Vector2I(-1, -1);
        if (!npc.is_traveling)
        {
            npc.world_travel_path.Clear();
            npc.world_travel_next_cell_index = 1;
            npc.world_travel_cell_progress_hours = 0f;
        }
    }

    private Vector2I get_location_anchor_cell(string nodeId)
    {
        if (_tile_world_map is null || string.IsNullOrWhiteSpace(nodeId)) return new Vector2I(-1, -1);
        return _tile_world_map.Call("get_location_anchor_cell", nodeId).AsVector2I();
    }

    private string get_location_node_id_at_cell(Vector2I cell)
        => _tile_world_map is null ? "" : _tile_world_map.Call("get_location_node_id_at_cell", cell).AsString();

    private Array<Vector2I> get_npc_grid_path(Vector2I fromCell, Vector2I toCell)
    {
        var result = new Array<Vector2I>();
        if (_tile_world_map is null) return result;
        if (_settlementBatch && _settlementPaths.TryGetValue((fromCell, toCell), out var cached))
        {
            foreach (var cell in cached) result.Add(cell);
            return result;
        }
        var path = _tile_world_map.Call("get_npc_grid_path", fromCell, toCell).AsGodotArray<Vector2I>();
        foreach (var cell in path) result.Add(cell);
        if (_settlementBatch) _settlementPaths[(fromCell, toCell)] = result.ToArray();
        return result;
    }

    private double get_npc_cell_travel_hours(Vector2I from, Vector2I cell, float dunSpeed)
    {
        if (_tile_world_map is null) return 2400.0 / Math.Max(0.01, dunSpeed);
        if (_tile_world_map.HasMethod("get_npc_edge_travel_hours"))
            return _tile_world_map.Call("get_npc_edge_travel_hours", from, cell, dunSpeed).AsDouble();
        if (!_settlementBatch) return _tile_world_map.Call("get_npc_cell_travel_hours", cell, dunSpeed).AsDouble();
        // Cache the exact cell/speed pair so floating-point behavior matches the old bridge.
        if (_settlementTravelHours.TryGetValue((cell, dunSpeed), out var hours)) return hours;
        hours = _tile_world_map.Call("get_npc_cell_travel_hours", cell, dunSpeed).AsDouble();
        _settlementTravelHours[(cell, dunSpeed)] = hours;
        return hours;
    }

    private static bool is_valid_world_grid_cell(Vector2I cell)
        => cell.X >= 0 && cell.X < 200 && cell.Y >= 0 && cell.Y < 200;

    /// <summary>把遗漏的月份逐月结算；只由主动时间推进或测试调用。</summary>
    public void simulate_until_month(int target_month)
    {
        if (!simulation_enabled || target_month < 0) return;
        var time = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
        var nowHour = time?.get_total_game_hours() ?? target_month * 30 * 24;
        begin_settlement_batch();
        try { foreach (var _ in settle_time_steps(nowHour, target_month)) { } }
        finally { end_settlement_batch(); }
    }

    private System.Collections.Generic.IEnumerable<int> simulate_month_steps(int month)
    {
        _monthlyResidents = _tile_world_map is null ? null : npcs.Where(n => n.is_alive && !n.is_traveling && string.IsNullOrEmpty(n.temporary_party_task_id))
            .GroupBy(n => n.current_node_id).ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.Ordinal);
        foreach (var npc in npcs)
        {
            if (!npc.is_alive) continue;
            if (!string.IsNullOrEmpty(npc.temporary_party_task_id)) continue;
            CharacterRelationshipRules.SettleMonth(npc, month);
            // 复用月度遍历；无新证据立即返回，不额外扫描人口或历史。
            NpcPlayerImpressionRules.SettleMonth(npc, month);
            // 师承流程的候选角色是宗门常驻人物，不参与尚未配置其修行/行程规则的月度演算。
            if (npc.is_yuxu_lineage_staff) continue;
            update_age(npc, month);
            if (!npc.is_alive) continue;
            if (!string.IsNullOrEmpty(npc.public_lecture_event_id)) continue;
            NpcPurchaseRules.SettleMonth(npc, month);
            // 副本行程由同一遁速时间轴推进；不能在月演算里把人抽走到大世界地点。
            if (!string.IsNullOrWhiteSpace(npc.dungeon_id)) continue;
            resolve_month_action(npc, month);
            yield return 1;
        }
        _monthlyResidents = null;
        notify_settlement_changed();
    }

    private void update_age(WorldNpcData npc, int month)
    {
        if (month % 12 != 0) return;
        npc.current_age++;
        if (npc.current_age < npc.max_age) return;
        var rng = new RandomNumberGenerator { Seed = stable_seed(month, npc.npc_id, "lifespan") };
        var chance = Mathf.Clamp(.18f + (npc.current_age - npc.max_age) * .16f, .18f, .92f);
        if (rng.Randf() < chance) mark_dead(npc, month, "寿尽", "寿元已尽，生机自然断绝。");
    }

    private void resolve_month_action(WorldNpcData npc, int month)
    {
        finish_seclusion_and_process_acquaintance_notes(npc, month);
        var rng = new RandomNumberGenerator { Seed = stable_seed(month, npc.npc_id, "action") };
        if (npc.is_traveling)
        {
            advance_travel(npc, month);
            return;
        }

        var roll = rng.RandiRange(0, 99);
        if (roll < 28) cultivate(npc, month, rng);
        else if (roll < 53) work(npc, month, rng);
        else if (roll < 72) socialize(npc, month, rng);
        else if (roll < 88) explore(npc, month, rng);
        else travel(npc, month, rng);
    }

    private void cultivate(WorldNpcData npc, int month, RandomNumberGenerator rng)
    {
        var gain = npc.spirit_root_name == "无灵根" ? 0 : rng.RandiRange(1, 3);
        npc.spirit_strands += gain;
        npc.current_state = "闭关修行";
        npc.seclusion_started_month = month;
        npc.seclusion_until_month = month + 1;
        record(npc, month, "cultivate", "闭关修行",
            gain > 0 ? "以自身灵根与已学功法修行，本月闭关至下次月度结算。" : "无灵根，改为闭关调养气血至下次月度结算。",
            gain > 0 ? $"灵力增加 {gain} 缕。" : "本月未积累灵力。", gain);
    }

    private void finish_seclusion_and_process_acquaintance_notes(WorldNpcData npc, int month)
    {
        if (npc.seclusion_until_month < 0 || month < npc.seclusion_until_month) return;
        npc.current_state = "空闲";
        // Keep the end cursor if player data is temporarily unavailable so a saved note
        // can still be resolved on a later monthly step instead of being stranded.
        if (_playerData is null) return;
        if (month <= npc.last_processed_acquaintance_month)
        {
            npc.seclusion_started_month = -1;
            npc.seclusion_until_month = -1;
            return;
        }

        npc.acquaintance_notes ??= [];
        var boundaryHour = month * 30 * 24;
        foreach (var note in npc.acquaintance_notes)
        {
            if (note is null || note.GetValueOrDefault("status", "").AsString() != "pending" ||
                string.IsNullOrWhiteSpace(note.GetValueOrDefault("note_id", "").AsString()) ||
                note.GetValueOrDefault("received_hour", int.MaxValue).AsInt32() >= boundaryHour) continue;

            // Reuse the existing social acceptance rules as a pure decision. Do not call Remember:
            // a note response alone neither exchanges names nor records relationship evidence.
            var decision = NpcSocialRules.Evaluate(npc, _playerData, "introduce", proactive: true);
            var accepted = decision.ExchangesName && !CharacterRelationshipRules.IsHostile(npc, CharacterRelationshipRules.PlayerCharacterId);
            note["status"] = accepted ? "accepted" : "declined";
            note["resolved_month"] = month;
            if (accepted)
                npc.acquaintance_window_until_hour = boundaryHour + 5 * 360 * 24;
            record(npc, month, "acquaintance_note_decision", "结识拜帖处理",
                "出关后依照当下交往情形处理门外拜帖。",
                accepted ? "愿意日后与留帖者结识；尚未交换姓名，也未形成关系记事。" : "婉谢门外拜帖；不生怨结，也未交换姓名。",
                0, CharacterRelationshipRules.PlayerCharacterId);
        }
        npc.last_processed_acquaintance_month = month;
        npc.seclusion_started_month = -1;
        npc.seclusion_until_month = -1;
    }

    private void work(WorldNpcData npc, int month, RandomNumberGenerator rng, string reason = "")
    {
        var gain = rng.RandiRange(1, 4);
        npc.change_resource("灵石", gain);
        npc.current_state = "营生";
        record(npc, month, "work", "营生", string.IsNullOrWhiteSpace(reason) ? "依照身份、技能与所在地点寻找稳定收入。" : reason, $"获得灵石 {gain}。", gain);
    }

    private void socialize(WorldNpcData npc, int month, RandomNumberGenerator rng)
    {
        npc.current_state = "访人";
        var candidates = _monthlyResidents is null ? npcs.AsEnumerable() : _monthlyResidents.GetValueOrDefault(npc.current_node_id, []);
        WorldNpcData? target = null;
        var best = ulong.MaxValue;
        foreach (var other in candidates)
        {
            if (other == npc || !other.is_alive || other.is_traveling || other.current_node_id != npc.current_node_id) continue;
            var score = stable_seed(month, npc.npc_id, other.npc_id);
            if (target is not null && score >= best) continue;
            target = other; best = score;
        }
        var result = target is null ? "此处暂无可拜访的熟人，转为打听消息。" : $"与 {target.npc_name} 往来，交换近况与消息。";
        if (target is not null)
            CharacterRelationshipRules.RecordConversation(npc, target, $"npc-socialize:{month}:{npc.npc_id}:{target.npc_id}", month);
        record(npc, month, "socialize", "访人", "根据性情与当前地点寻找可交往对象。", result, 0, target?.npc_id ?? "");
    }

    private void explore(WorldNpcData npc, int month, RandomNumberGenerator rng)
    {
        npc.current_state = "探索";
        var damage = rng.RandiRange(0, 12);
        npc.current_health = Mathf.Max(0, npc.current_health - damage);
        if (npc.current_health == 0)
        {
            mark_dead(npc, month, "战斗身亡", "探索途中遭遇战斗，伤势过重而亡。");
            return;
        }
        var gain = rng.RandiRange(0, 3);
        npc.change_resource("杂材", gain);
        record(npc, month, "explore", "探索", "基于目标、所在地点与可用精力进行探索。", damage > 0 ? $"受伤 {damage} 点，获得杂材 {gain}。" : $"平安归来，获得杂材 {gain}。", gain - damage);
    }

    private void travel(WorldNpcData npc, int month, RandomNumberGenerator rng)
    {
        var neighbors = get_available_travel_neighbors(npc.current_node_id);
        if (neighbors.Count == 0)
        {
            work(npc, month, rng, "当前地点没有开放且可通行的路线，本月改为营生。");
            return;
        }
        var destination = neighbors[rng.RandiRange(0, neighbors.Count - 1)];
        var route = _map_data?.get_route_between(npc.current_node_id, destination);
        var origin = npc.current_node_id;
        var travel_days = Mathf.Max(1, route?.travel_days ?? 1);
        if (_tile_world_map is not null)
        {
            initialize_world_grid_position(npc);
            var destinationCell = get_location_anchor_cell(destination);
            var path = get_npc_grid_path(npc.current_grid_cell, destinationCell);
            if (!is_valid_world_grid_cell(npc.current_grid_cell) || path.Count < 2)
            {
                work(npc, month, rng, "相邻地点在当前节点路网中没有可通行路径，本月改为营生。");
                return;
            }
            npc.world_travel_path.Clear();
            foreach (var cell in path) npc.world_travel_path.Add(cell);
            npc.world_travel_next_cell_index = 1;
            npc.world_travel_cell_progress_hours = 0f;
            npc.has_world_grid_position = true;
            var totalHours = get_remaining_world_travel_hours(npc);
            travel_days = Math.Max(1, Mathf.CeilToInt((float)(totalHours / 24.0)));
        }
        npc.travel_origin_node_id = npc.current_node_id;
        npc.is_traveling = true;
        npc.travel_destination_node_id = destination;
        npc.travel_days_remaining = travel_days;
        npc.travel_days_total = travel_days;
        npc.current_subscene_id = "";
        npc.current_state = "途中";
        record(npc, month, "travel", "启程", "沿已知且开放的相邻路线移动。", $"自 {npc.travel_origin_node_id} 前往 {destination}，预计 {travel_days} 日。", 0, "", false, npc.travel_origin_node_id, destination, travel_days, travel_days);
    }

    private void advance_travel(WorldNpcData npc, int month)
    {
        var origin = npc.travel_origin_node_id;
        var destination = npc.travel_destination_node_id;
        var total = Mathf.Max(1, npc.travel_days_total);
        if (_tile_world_map is not null)
        {
            npc.travel_days_remaining = Math.Max(1, Mathf.CeilToInt((float)(get_remaining_world_travel_hours(npc) / 24.0)));
            npc.current_state = "途中";
            record(npc, month, "travel_progress", "途中", "行程尚未完成，继续沿已知路线前行。", $"仍需 {npc.travel_days_remaining} 日抵达。", 0, "", false, origin, destination, total, npc.travel_days_remaining);
            return;
        }

        // 无固定格网的纯数据测试场景保留旧兼容路径；实际世界地图通过上面的逐格时间轴行进。
        npc.travel_days_remaining = Mathf.Max(0, npc.travel_days_remaining - 30);
        if (npc.travel_days_remaining > 0)
        {
            npc.current_state = "途中";
            record(npc, month, "travel_progress", "途中", "行程尚未完成，继续沿已知路线前行。", $"仍需 {npc.travel_days_remaining} 日抵达。", 0, "", false, origin, destination, total, npc.travel_days_remaining);
            return;
        }

        npc.current_node_id = destination;
        var destination_node = _map_data?.get_node(destination);
        npc.current_subscene_id = destination_node is null ? "" : choose_arrival_subscene(destination_node, month, npc.npc_id);
        npc.current_state = "抵达";
        npc.is_traveling = false;
        npc.travel_origin_node_id = "";
        npc.travel_destination_node_id = "";
        npc.travel_days_total = 0;
        npc.world_travel_path.Clear();
        npc.world_travel_next_cell_index = 1;
        npc.world_travel_cell_progress_hours = 0f;
        var subscene_note = string.IsNullOrWhiteSpace(npc.current_subscene_id)
            ? ""
            : $"，进入 {get_subscene_name(destination, npc.current_subscene_id)}";
        record(npc, month, "arrive", "抵达", "沿既定路线完成行程，并在目的地点选择可用的内部空间。", $"抵达 {destination}{subscene_note}。", 0, "", false, origin, destination, total, 0);
    }

    private void mark_dead(WorldNpcData npc, int month, string cause, string outcome)
    {
        npc.is_alive = false;
        npc.is_traveling = false;
        npc.travel_origin_node_id = "";
        npc.travel_destination_node_id = "";
        npc.travel_days_remaining = 0;
        npc.travel_days_total = 0;
        npc.world_travel_path.Clear();
        npc.world_travel_next_cell_index = 1;
        npc.world_travel_cell_progress_hours = 0f;
        npc.current_state = "已故";
        npc.death_cause = cause;
        record(npc, month, "death", "死亡", cause, outcome, 0, "", true);
        if (_playerData is not null)
            CharacterRelationshipRules.RecordDeathFor(_playerData, npc.npc_id, month, cause);
        foreach (var observer in npcs)
            if (observer is not null && observer != npc)
                CharacterRelationshipRules.RecordDeathFor(observer, npc.npc_id, month, cause);
    }

    private void record(WorldNpcData npc, int month, string actionId, string actionName, string reason, string outcome, int resourceDelta, string targetNpcId = "", bool death = false, string fromNodeId = "", string toNodeId = "", int travelDays = 0, int travelDaysRemaining = 0)
    {
        var record = new NpcActionRecord
        {
            month_index = month,
            date_label = format_month(month),
            action_id = actionId,
            action_name = actionName,
            node_id = npc.current_node_id,
            subscene_id = npc.current_subscene_id,
            from_node_id = fromNodeId,
            to_node_id = toNodeId,
            travel_days = travelDays,
            travel_days_remaining = travelDaysRemaining,
            target_npc_id = targetNpcId,
            reason = reason,
            outcome = outcome,
            resource_delta = resourceDelta,
            is_death_record = death,
        };
        npc.record(record);
        EmitSignal(SignalName.npc_action_recorded, npc.npc_id, record);
    }

    private Array<string> get_available_travel_neighbors(string node_id)
    {
        var result = new Array<string>();
        if (_map_data is null) return result;
        if (_tile_world_map is not null && !has_world_grid_anchor(node_id)) return result;
        var neighbors = _tile_world_map?.HasMethod("get_npc_location_neighbors") == true
            ? _tile_world_map.Call("get_npc_location_neighbors", node_id).AsGodotArray<string>()
            : _map_data.get_neighbors(node_id);
        foreach (var neighbor_id in neighbors)
        {
            var neighbor = _map_data.get_node(neighbor_id);
            var route = _map_data.get_route_between(node_id, neighbor_id);
            if (neighbor is null || (_tile_world_map?.HasMethod("get_npc_location_neighbors") != true && (route is null || !route.unlocked))) continue;
            if (neighbor.requires_unlock && !neighbor.unlocked) continue;
            if (_tile_world_map is not null && !has_world_grid_anchor(neighbor_id)) continue;
            if (!result.Contains(neighbor_id)) result.Add(neighbor_id);
        }
        return result;
    }

    /// <summary>抵达地点后自动选一个已解锁子场景；选择使用稳定种子，便于行动记录复现。</summary>
    private string choose_arrival_subscene(WorldNodeData node, int month, string npc_id)
    {
        var available = new Array<string>();
        foreach (var subscene in node.subscenes)
            if (subscene is not null && subscene.unlocked && !string.IsNullOrWhiteSpace(subscene.subscene_id))
                available.Add(subscene.subscene_id);
        if (available.Count == 0) return "";
        var rng = new RandomNumberGenerator { Seed = stable_seed(month, npc_id, $"arrival:{node.node_id}") };
        return available[rng.RandiRange(0, available.Count - 1)];
    }

    private bool normalize_npc_location(WorldNpcData npc)
    {
        if (_map_data is null) return true;
        var node = _map_data.get_node(npc.current_node_id);
        if (node is null)
        {
            GD.PushWarning($"npc_location_invalid npc={npc.npc_id} node={npc.current_node_id}");
            return false;
        }
        if (!string.IsNullOrWhiteSpace(npc.current_subscene_id) && node.subscenes.All(item => item is null || item.subscene_id != npc.current_subscene_id))
        {
            GD.PushWarning($"npc_subscene_missing npc={npc.npc_id} node={npc.current_node_id} subscene={npc.current_subscene_id}");
            npc.current_subscene_id = "";
        }
        initialize_world_grid_position(npc);
        return true;
    }

    private static string choose_next_dungeon_node(WorldNpcData npc)
    {
        var options = new List<string>(EastWoodsDungeonRules.get_neighbors(npc.dungeon_node_id));
        if (options.Count > 1 && options.Contains(npc.dungeon_previous_node_id)) options.Remove(npc.dungeon_previous_node_id);
        if (options.Count == 0) return npc.dungeon_previous_node_id;
        options.Sort(StringComparer.Ordinal);
        var seed = EastWoodsDungeonRules.stable_seed($"route|{npc.npc_id}|{npc.dungeon_step_index + 1}");
        return options[(int)(seed % (ulong)options.Count)];
    }

    private static bool should_npc_attack(WorldNpcData attacker, WorldNpcData target, long actionStamp, out float chance)
    {
        chance = EastWoodsDungeonRules.attack_chance(
            attacker,
            target.faction_id,
            target.get_regional_renown(RegionalRenownSystem.BaiChuanRegionId),
            get_hostility_bias(attacker, target.npc_id),
            EastWoodsDungeonRules.realm_score(target.cultivation_realm));
        return EastWoodsDungeonRules.roll_attack(
            $"{actionStamp}|{attacker.npc_id}|{target.npc_id}|{attacker.dungeon_step_index}|{target.dungeon_step_index}|{Mathf.RoundToInt(attacker.dungeon_travel_progress * 10000f)}",
            chance);
    }

    private static int get_hostility_bias(WorldNpcData npc, string targetNpcId)
        => CharacterRelationshipRules.HostilityBias(npc, targetNpcId);

    private void record_dungeon_action(
        WorldNpcData npc,
        string actionId,
        string actionName,
        string reason,
        string outcome,
        string nodeId,
        string targetId = "",
        bool death = false,
        string fromRouteNodeId = "",
        string toRouteNodeId = "",
        string incidentId = "")
    {
        var time = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
        var month = time is null ? 0 : month_index(time);
        var date = time is null ? format_month(month) : $"{time.year}年{time.month}月{time.day}日";
        var entry = new NpcActionRecord
        {
            month_index = month,
            date_label = date,
            action_id = actionId,
            action_name = actionName,
            node_id = npc.current_node_id,
            subscene_id = string.IsNullOrWhiteSpace(npc.dungeon_id) ? npc.current_subscene_id : $"dungeon:{npc.dungeon_id}:{nodeId}",
            from_node_id = fromRouteNodeId,
            to_node_id = toRouteNodeId,
            target_npc_id = targetId,
            dungeon_incident_id = incidentId,
            reason = reason,
            outcome = outcome,
            is_death_record = death,
        };
        npc.record(entry);
        EmitSignal(SignalName.npc_action_recorded, npc.npc_id, entry);
    }

    private static int month_index(GameTimeMgr time) => (time.year - 1) * 12 + time.month - 1;
    private static string format_month(int month) => $"{month / 12 + 1}年{month % 12 + 1}月";
    private static ulong stable_seed(int month, string first, string second)
    {
        unchecked
        {
            ulong hash = 1469598103934665603;
            foreach (var c in $"{month}|{first}|{second}") { hash ^= c; hash *= 1099511628211; }
            return hash;
        }
    }
}
