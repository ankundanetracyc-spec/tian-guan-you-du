using Godot;
using Godot.Collections;

public partial class MainScene
{
    /// <summary>Opens a formal battle for the pending road encounter.</summary>
    private bool start_world_encounter_battle(string encounterKind, string encounterId)
    {
        var transition = GetNodeOrNull<BattleTransitionManager>("/root/BattleTransitionManager");
        var playerScene = player?.pack_for_battle();
        if (transition is null || transition.has_open_flow_view() || player is null || playerScene is null ||
            string.IsNullOrWhiteSpace(encounterId)) return false;

        PackedScene? enemyScene;
        BattleEntityData? enemySnapshot;
        var savedSnapshot = _worldEncounterState.GetValueOrDefault("enemy_snapshot", default).AsGodotObject() as BattleEntityData;
        if (encounterKind == "beast")
        {
            var template = GD.Load<BeastTemplate>("res://resources/beast_templates/林地凶狼.tres");
            if (template is null) return false;
            var enemy = new Beast { Name = "RoadBeast", unique_id = "road_encounter_" + encounterId,
                beast_template = template, entity_data = new LivingData(), faction = (int)WorldEntity.Faction.ENEMY,
                Visible = false };
            AddChild(enemy);
            enemyScene = enemy.pack_for_battle();
            enemySnapshot = savedSnapshot is null ? enemy.get_snapshot_data() : (BattleEntityData)savedSnapshot.Duplicate(true);
            RemoveChild(enemy);
            enemy.Free();
        }
        else if (encounterKind == "bandit")
        {
            var data = _worldEncounterState.GetValueOrDefault("enemy_human", default).AsGodotObject() as HumanData;
            if (data is null)
            {
                data = new CharacterGenerator().generate("凡人", (int)WorldEntity.Faction.ENEMY);
                data.entity_name = "劫匪";
                data.unique_id = "road_encounter_" + encounterId;
                _worldEncounterState["enemy_human"] = data;
            }
            var enemy = new Character { Name = "RoadBandit", Visible = false };
            AddChild(enemy);
            enemy.load_from_data(data);
            enemy.unique_id = data.unique_id;
            enemy.entity_type = WorldEntity.EntityType.CHARACTER;
            enemy.faction = (int)WorldEntity.Faction.ENEMY;
            enemyScene = enemy.pack_for_battle();
            enemySnapshot = savedSnapshot is null ? enemy.get_snapshot_data() : (BattleEntityData)savedSnapshot.Duplicate(true);
            RemoveChild(enemy);
            enemy.Free();
        }
        else return false;

        if (enemyScene is null || enemySnapshot is null) return false;
        _worldEncounterState["enemy_snapshot"] = enemySnapshot.Duplicate(true);
        mark_save_dirty();
        transition.set_battle_return_context(new Dictionary
        {
            ["kind"] = "world_road_encounter",
            ["encounter_id"] = encounterId,
            ["encounter_kind"] = encounterKind,
            ["title"] = encounterKind == "beast" ? "旅途遭遇 · 林地凶狼" : "旅途遭遇 · 劫匪",
            ["allow_prebattle_cancel"] = true,
        });
        transition.start_team_battle(new Array<PackedScene> { playerScene }, new Array<PackedScene> { enemyScene },
            new Godot.Collections.Array { player.get_snapshot_data() }, new Godot.Collections.Array { enemySnapshot },
            GetTree().CurrentScene?.SceneFilePath ?? "res://tscn/scene/MainScene.tscn");
        return true;
    }

    private bool handle_world_encounter_battle_return(Dictionary summary, Dictionary context)
    {
        if (context.GetValueOrDefault("kind", "").AsString() != "world_road_encounter") return false;
        finish_world_encounter_battle(summary, context);
        return true;
    }
}
