using Godot;
using Godot.Collections;
using GArray = Godot.Collections.Array;

/// <summary>保存战前快照、切换战斗场景并回传战斗结果的自动加载服务。</summary>
public partial class BattleTransitionManager : Node
{
    private Godot.Collections.Array<PackedScene> pending_attackers = [];
    private Godot.Collections.Array<PackedScene> pending_defenders = [];
    private GArray pending_attacker_data = [];
    private GArray pending_defender_data = [];
    private string world_scene_path = "";
    private GArray result_attackers = [];
    private GArray result_defenders = [];
    private bool has_results;
    private Dictionary last_summary = [];
    private Dictionary return_context = [];
    private PackedScene? saved_world;
    private Dictionary saved_entity_data = [];

    public void start_team_battle(Godot.Collections.Array<PackedScene> attacker_packeds, Godot.Collections.Array<PackedScene> defender_packeds, GArray attacker_datas, GArray defender_datas, string world_path)
    {
        if(_awaitingSettlement || GodotObject.IsInstanceValid(_flowView))return;
        pending_attackers = attacker_packeds; pending_defenders = defender_packeds;
        pending_attacker_data = attacker_datas; pending_defender_data = defender_datas; world_scene_path = world_path;
        ShowBattleBriefing();
    }

    public GArray consume_pending_battle_package()
    {
        var result = new GArray { pending_attackers, pending_defenders, pending_attacker_data, pending_defender_data };
        pending_attackers = []; pending_defenders = []; pending_attacker_data = []; pending_defender_data = [];
        return result;
    }

    public void set_battle_return_context(Dictionary context) => return_context = context.Duplicate(true);

    public void end_team_battle(GArray updated_attackers, GArray updated_defenders)
    {
        PrepareSettlement(updated_attackers,updated_defenders,false);
    }

    public bool has_pending_results() => has_results;
    public GArray consume_results()
    {
        var result = new GArray { result_attackers, result_defenders, saved_entity_data };
        result_attackers = []; result_defenders = []; saved_entity_data = []; has_results = false; saved_world = null;
        return result;
    }

    public Dictionary consume_battle_return_data()
    {
        var result = new Dictionary { ["summary"] = last_summary.Duplicate(true), ["context"] = return_context.Duplicate(true) };
        last_summary.Clear(); return_context.Clear(); return result;
    }

    private void save_entity_data_recursive(Node node)
    {
        if (node.HasMethod("get_snapshot_data"))
        {
            var type = node.Get("entity_type").AsInt32();
            var uid = node.Get("unique_id").AsString();
            var data = node.Get("entity_data").AsGodotObject();
            if ((type == 0 || type == 1) && !string.IsNullOrEmpty(uid) && GodotObject.IsInstanceValid(data)) saved_entity_data[uid] = data!.Call("duplicate");
        }
        foreach (Node child in node.GetChildren()) save_entity_data_recursive(child);
    }

    private static int alive_count(GArray entries)
    {
        var total = 0;
        foreach (var entry in entries) { var data = entry.AsGodotObject(); if (GodotObject.IsInstanceValid(data) && data!.Get("current_health").AsInt32() > 0) total++; }
        return total;
    }
}
