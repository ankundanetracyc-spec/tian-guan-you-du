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
    private Godot.Collections.Array<Dictionary> _artifactReports = [];
    public void set_artifact_reports(Godot.Collections.Array<Dictionary> reports) => _artifactReports = reports.Duplicate(true);
    private bool has_results;
    private bool has_battle_return_data;
    private Dictionary last_summary = [];
    private Dictionary return_context = [];
    private PackedScene? saved_world;
    private Dictionary saved_entity_data = [];
    private PersistentGameSave? _worldProgressSnapshot;
    private Node? _spectatorBattleScene;
    private bool _spectatorBattleActive;
    public bool is_spectator_battle_active() => _spectatorBattleActive;
    public PersistentGameSave? consume_world_progress_snapshot()
    {
        if (!has_battle_return_data) return null;
        var snapshot = _worldProgressSnapshot;
        _worldProgressSnapshot = null;
        return snapshot;
    }

    public void start_team_battle(Godot.Collections.Array<PackedScene> attacker_packeds, Godot.Collections.Array<PackedScene> defender_packeds, GArray attacker_datas, GArray defender_datas, string world_path)
    {
        if(_awaitingSettlement || GodotObject.IsInstanceValid(_flowView))return;
        pending_attackers = attacker_packeds; pending_defenders = defender_packeds;
        pending_attacker_data = attacker_datas; pending_defender_data = defender_datas; world_scene_path = world_path;
        ShowBattleBriefing();
    }

    public bool start_spectator_team_battle(Godot.Collections.Array<PackedScene> attacker_packeds, Godot.Collections.Array<PackedScene> defender_packeds, GArray attacker_datas, GArray defender_datas, string world_path)
    {
        if (_awaitingSettlement || GodotObject.IsInstanceValid(_flowView) || _spectatorBattleActive ||
            attacker_packeds.Count == 0 || defender_packeds.Count == 0 || attacker_datas.Count == 0 || defender_datas.Count == 0)
            return false;

        pending_attackers = attacker_packeds;
        pending_defenders = defender_packeds;
        pending_attacker_data = attacker_datas;
        pending_defender_data = defender_datas;
        world_scene_path = world_path;
        _beforeBattle = Data(attacker_datas).Concat(Data(defender_datas)).Select(data => (BattleEntityData)data.Duplicate(true)).ToList();
        _attackerOrder = Data(attacker_datas).Select(data => data.uid).ToList();
        _defenderOrder = Data(defender_datas).Select(data => data.uid).ToList();
        _battleId = Guid.NewGuid().ToString("N");
        _outcomeNote = "";

        // NPC-versus-NPC results use the same battle manager and rule code, but
        // need no background, camera, HUD, or character presentation scene.
        var scene = new BattleScene();
        var manager = new BattleManager
        {
            Name = "BattleMgr",
            Position = new Vector2(960, 540),
            battle_map = new NodePath("../BattleMap"),
            deploy_layer = new NodePath("../DeployLayer"),
        };
        var map = new BattleMap
        {
            Name = "BattleMap",
            Position = new Vector2(960, 760),
            map_radius = 5,
            hex_size = 120,
            hex_aspect_ratio = 0.8f,
            spatial_combat_enabled = true,
        };
        scene.AddChild(manager);
        scene.AddChild(map);
        scene.AddChild(new Node2D { Name = "DeployLayer" });
        _spectatorBattleActive = true;
        scene.Name = "EastWoodsNpcSpectatorBattle";
        scene.Hide();
        _spectatorBattleScene = scene;
        GetTree().Root.AddChild(scene);
        return true;
    }

    public GArray consume_pending_battle_package()
    {
        _cappedWorldPlayerUid = "";
        _worldStatusBattleHealthCapDelta = 0;
        var playerUid = return_context.GetValueOrDefault("world_player_uid", "").AsString();
        var capRatio = return_context.GetValueOrDefault("player_battle_health_cap_ratio", 1f).AsSingle();
        if (!Training && !string.IsNullOrWhiteSpace(playerUid) && capRatio > 0f && capRatio < 1f)
        {
            foreach (var snapshot in pending_attacker_data.Concat(pending_defender_data)
                         .Select(value => value.AsGodotObject()).OfType<BattleEntityData>())
            {
                if (snapshot.uid != playerUid || snapshot.max_health <= 0) continue;
                _cappedWorldPlayerUid = playerUid;
                var originalMaxHealth = snapshot.max_health;
                snapshot.max_health = Math.Max(1, Mathf.RoundToInt(originalMaxHealth * capRatio));
                _worldStatusBattleHealthCapDelta = originalMaxHealth - snapshot.max_health;
                snapshot.current_health = Math.Min(snapshot.current_health, snapshot.max_health);
                break;
            }
        }
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
    public bool has_pending_battle_return_data() => has_battle_return_data;
    public GArray consume_results()
    {
        var result = new GArray { result_attackers, result_defenders, saved_entity_data, _artifactReports };
        _artifactReports = [];
        result_attackers = []; result_defenders = []; saved_entity_data = []; has_results = false; saved_world = null;
        return result;
    }

    public Dictionary consume_battle_return_data()
    {
        var result = new Dictionary { ["summary"] = last_summary.Duplicate(true), ["context"] = return_context.Duplicate(true) };
        last_summary.Clear(); return_context.Clear(); has_battle_return_data = false; saved_world = null; saved_entity_data.Clear(); return result;
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
