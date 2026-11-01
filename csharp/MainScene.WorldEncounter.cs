using Godot;
using Godot.Collections;

public partial class MainScene
{
    private Dictionary _worldEncounterState = [];
    private GodotObject? _roadEncounterClock;
    private bool _roadEncounterBriefingConnected;

    private bool has_pending_world_encounter() =>
        _worldEncounterState.GetValueOrDefault("pending", new Dictionary()).AsGodotDictionary().Count > 0;

    private bool initialize_road_encounter_clock()
    {
        if (_roadEncounterClock is not null && GodotObject.IsInstanceValid(_roadEncounterClock)) return true;
        var script = GD.Load<GDScript>("res://gdscripts/world/BaiChuanRoadEncounters.gd");
        _roadEncounterClock = script?.New().AsGodotObject();
        if (_roadEncounterClock is null) return false;
        var saved = _worldEncounterState.GetValueOrDefault("clock", new Dictionary()).AsGodotDictionary();
        if (saved.Count == 0) _roadEncounterClock.Call("initialize", (long)GD.Randi());
        else if (!_roadEncounterClock.Call("restore_state", saved).AsBool())
        {
            GD.PushError("大世界遭遇状态无法恢复");
            _roadEncounterClock.Dispose();
            _roadEncounterClock = null;
            return false;
        }
        return true;
    }

    private void check_world_road_encounter(string nodeId)
    {
        if (_nodeWorldMap is null || player is null || has_pending_world_encounter()) return;
        // Existing escort encounters own the same travel interruption and party state.
        if (player.entity_data is HumanData human && _npc_world?.get_active_yuxu_case_transport(human) is { temporary_party_member_active: true }) return;
        var runtimeId = _nodeWorldMap.Call("get_runtime_id", nodeId).AsString();
        var place = _world_node_data?.get_node(runtimeId);
        var eligible = place is null || place.node_type is not (WorldNodeData.NodeType.City or WorldNodeData.NodeType.Sect or WorldNodeData.NodeType.Village or WorldNodeData.NodeType.Home);
        var step = _nodeWorldMap.Call("get_last_travel_step").AsGodotDictionary();
        var km = step.GetValueOrDefault("kilometres", 0.0).AsDouble();
        if (!initialize_road_encounter_clock()) return;
        var pending = _roadEncounterClock!.Call("consume_edge", km, eligible).AsGodotDictionary();
        _worldEncounterState["clock"] = _roadEncounterClock.Call("persistent_state").AsGodotDictionary();
        if (pending.Count > 0)
        {
            _worldEncounterState["pending"] = pending;
            _nodeWorldMap.Call("pause_travel_at_node");
            _nodeWorldMap.Call("set_encounter_blocked", true);
            CallDeferred(nameof(show_world_encounter_event));
        }
        mark_save_dirty();
    }

    private void resolve_world_encounter_and_continue()
    {
        if (!initialize_road_encounter_clock()) return;
        _roadEncounterClock!.Call("resolve_encounter");
        _worldEncounterState["clock"] = _roadEncounterClock.Call("persistent_state").AsGodotDictionary();
        _worldEncounterState.Remove("pending");
        _worldEncounterState.Remove("enemy_human");
        _worldEncounterState.Remove("enemy_snapshot");
        mark_save_dirty();
        _nodeWorldMap?.Call("set_encounter_blocked", false);
        _nodeWorldMap?.CallDeferred("resume_saved_travel");
    }

    private void finish_world_encounter_battle(Dictionary summary, Dictionary context)
    {
        var pending = _worldEncounterState.GetValueOrDefault("pending", new Dictionary()).AsGodotDictionary();
        if (pending.GetValueOrDefault("id", "").AsString() != context.GetValueOrDefault("encounter_id", "").AsString()) return;
        if (!summary.GetValueOrDefault("player_victory", false).AsBool())
        {
            if (_nodeWorldMap is not null) _nodeWorldMap.Call("interrupt_assigned_route");
            else _nodeWorldState["remaining_route"] = new Godot.Collections.Array<string>();
        }
        resolve_world_encounter_and_continue();
    }

    private void connect_road_encounter_briefing()
    {
        if (_roadEncounterBriefingConnected || GetNodeOrNull<BattleTransitionManager>("/root/BattleTransitionManager") is not { } transition) return;
        System.Action<Dictionary> cancelled = context =>
        {
            if (IsInsideTree() && context.GetValueOrDefault("kind", "").AsString() == "world_road_encounter" && has_pending_world_encounter())
                show_world_encounter_event();
        };
        transition.BriefingCancelled += cancelled;
        TreeExiting += () => { if (IsInstanceValid(transition)) transition.BriefingCancelled -= cancelled; };
        _roadEncounterBriefingConnected = true;
    }
}
