using Godot;

public partial class StartMenu
{
    private static PackedScene CreateForestBeiScene()
    {
        var bei = new Beast { beast_template = GD.Load<BeastTemplate>("res://resources/beast_templates/林地妖狈.tres") };
        var scene = new PackedScene();
        if (scene.Pack(bei) != Error.Ok) GD.PushError("无法创建林地妖狈测试场景");
        bei.QueueFree();
        return scene;
    }
    private static BattleEntityData CreateForestBeiTestUnit(int index)
    {
        var template = ResourceLoader.Load<BeastTemplate>("res://resources/beast_templates/林地妖狈.tres");
        const float physique = 7, intelligence = 8, soul = 9;
        var coordination = Mathf.Min(physique, soul);
        var health = Mathf.RoundToInt(60 + physique * 11 + soul * 3 + intelligence * 1.5f);
        var mind = Mathf.RoundToInt(30 + soul * 4 + intelligence * 1.5f + physique * .5f);
        return new BattleEntityData {
            uid = $"battle_test_forest_bei_{index}", entity_name = "林地妖狈", entity_type = (int)WorldEntity.EntityType.BEAST,
            faction = (int)WorldEntity.Faction.ENEMY, cultivation_realm = "通智", physique = physique, intelligence = intelligence, soul = soul,
            max_health = health, current_health = health, max_mind = mind, current_mind = mind, max_mana = 0, current_mana = 0,
            physical_defense = 0, precision = Mathf.RoundToInt(intelligence * 7 + soul + physique * .5f),
            dodge = Mathf.RoundToInt(coordination * 4 + soul * 2 + intelligence + physique * .5f),
            critical = Mathf.RoundToInt(physique * .5f + soul * 2 + intelligence * .3f),
            crit_resist = Mathf.RoundToInt(physique * 3 + intelligence * 2 + coordination * 2 + soul * 1.5f), crit_damage_mult = 1.5f,
            initiative_speed = Creature.calculate_base_initiative_speed(physique, intelligence, soul), initial_hand_size = 5, cards_per_turn = 3,
            battle_portrait_path = BattlePaperDoll.ForestBeiPortraitPath,
            active_skill_paths = template?.deck?.get_deck_skills() ?? [],
            combat_board_layout = template?.combat_board_layout?.CopyConfiguration()
        };
    }
}
