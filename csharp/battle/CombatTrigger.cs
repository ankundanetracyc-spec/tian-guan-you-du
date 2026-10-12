using Godot;
using Godot.Collections;

/// <summary>Bridges world entities and the battle-scene transition package.</summary>
public partial class CombatTrigger : Node
{
    [Export] public string battle_transition_manager_name = "BattleTransitionManager";
    private const string TempEnemyPrefix = "temp_enemy_";
    public override void _Ready() { AddToGroup("combat_triggers"); GetTree().Root.GetNodeOrNull<Node>("EntityRegistry")?.Call("cleanup"); CallDeferred(nameof(check_and_restore_battle_results)); }
    public void try_trigger_combat(GodotObject attacker, GodotObject defender) => try_trigger_team_combat(new Godot.Collections.Array { attacker }, new Godot.Collections.Array { defender });
    public void try_trigger_team_combat(Godot.Collections.Array attackers, Godot.Collections.Array defenders)
    {
        var manager = TransitionManager(); if (manager is null) return;
        var scenes = new Godot.Collections.Array(); var snapshots = new Godot.Collections.Array(); var enemyScenes = new Godot.Collections.Array(); var enemySnapshots = new Godot.Collections.Array();
        Pack(attackers, scenes, snapshots); Pack(defenders, enemyScenes, enemySnapshots);
        var path = GetTree().CurrentScene?.SceneFilePath ?? "";
        if (scenes.Count > 0 && enemyScenes.Count > 0 && path.Length > 0) manager.Call("start_team_battle", scenes, enemyScenes, snapshots, enemySnapshots, path);
    }
    private static void Pack(Godot.Collections.Array units, Godot.Collections.Array scenes, Godot.Collections.Array snapshots)
    { foreach (var item in units) { var unit = item.AsGodotObject(); if (!GodotObject.IsInstanceValid(unit)) continue; var scene = unit!.Call("pack_for_battle").AsGodotObject() as PackedScene; if (scene is not null) { scenes.Add(scene); snapshots.Add(unit.Call("get_snapshot_data")); } } }
    public void check_and_restore_battle_results()
    {
        var manager = TransitionManager(); if (manager is null) return;
        var hasBattleResults = manager.Call("has_pending_results").AsBool();
        var hasReturnData = manager.Call("has_pending_battle_return_data").AsBool();
        if (!hasBattleResults && !hasReturnData) return;
        Godot.Collections.Array results = [];
        if (hasBattleResults)
        {
            results = manager.Call("consume_results").AsGodotArray(); if (results.Count < 2) return;
            var byId = new Dictionary(); CollectEntities(GetTree().CurrentScene, byId);
            foreach (var group in new[] { results[0].AsGodotArray(), results[1].AsGodotArray() }) foreach (var item in group)
            {
                if (item.AsGodotObject() is not BattleEntityData data || data.uid.StartsWith(TempEnemyPrefix)) continue;
                var entity = byId.TryGetValue(data.uid, out var candidate) ? candidate.AsGodotObject() : null;
                if (!GodotObject.IsInstanceValid(entity)) continue;
                if (data.current_health <= 0 && data.faction != 1) { (entity as Node)?.QueueFree(); continue; }
                if (data.current_health <= 0) { data.current_health = Mathf.Max(1, Mathf.RoundToInt(data.max_health * .1f)); data.current_stamina = Mathf.Max(1, Mathf.RoundToInt(data.max_stamina * .1f)); data.current_mana = Mathf.Max(1, Mathf.RoundToInt(data.max_mana * .1f)); }
                entity!.Call("apply_battle_data", data);
            }
        }

        var returnData = manager.Call("consume_battle_return_data").AsGodotDictionary();
        var context = returnData.GetValueOrDefault("context", new Dictionary()).AsGodotDictionary();
        var summary = returnData.GetValueOrDefault("summary", new Dictionary()).AsGodotDictionary();
        var currentScene = GetTree().CurrentScene;
        if (hasBattleResults)
        {
            (manager as BattleTransitionManager)?.resume_pending_loot();
            (currentScene as MainScene)?.notify_battle_rewards_changed();
            if (currentScene?.HasMethod("handle_persistent_battle_return") == true)
                currentScene.Call("handle_persistent_battle_return", results[0], results[1], summary, context);
        }
        if (context.GetValueOrDefault("kind", "").AsString() == "yuxu_tianwen_practice")
        {
            if (currentScene?.HasMethod("show_yuxu_tianwen_practice_result") == true)
                currentScene.Call("show_yuxu_tianwen_practice_result", summary, context);
        }
        if (context.GetValueOrDefault("kind", "").AsString() == "yuxu_npc_spar" && currentScene is MainScene main)
            main.show_npc_spar_result(summary, context);
    }
    private void CollectEntities(Node? node, Dictionary result)
    { if (node is null) return; var id = node.Get("unique_id").AsString(); if (!string.IsNullOrEmpty(id)) result[id] = node; foreach (var child in node.GetChildren()) CollectEntities(child, result); }
    private Node? TransitionManager() => GetTree().Root.GetNodeOrNull<Node>(battle_transition_manager_name);
}
