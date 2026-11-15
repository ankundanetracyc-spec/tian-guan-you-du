using Godot;

/// <summary>Fresh journeys enter the Baishi opening without preselecting a cultivation book.</summary>
public partial class MainScene
{
    private const string FreeCultivationStartNodeId = "white_stone_town";
    private bool _story_free_start;

    private void initialize_story_free_start()
    {
        if (_loaded_game_save is not null)
            return;

        _story_free_start = true;
        _baishiLecture = new BaishiLectureProgress();
        _baishiLecture.companion.ensure_appearance_identity();
        _player_world_node_id = FreeCultivationStartNodeId;

        if (player?.entity_data is HumanData human)
        {
            human.intro_gongfa_path = "";
            human.intro_gongfa_study_progress = 0f;
        }
        if (player is not null && _save_manager?.active_game_mode != "roguelike" && !player.grant_new_journey_yunxi_breakthrough_pills())
            GD.PushError("新旅程未能发放云息突破丹药；请检查行囊容量与丹药物品定义。");

        // World position is resolved from the reviewed node graph when the map opens.
        hud?.set_world_map_available(false);
        mark_save_dirty();
    }

    private bool has_open_world_access()
        => (_baishiLecture is null || _baishiLecture.departed_qingxi) &&
           _story_free_start;

    private void show_story_free_start()
    {
        if (_save_manager?.active_game_mode == "roguelike")
        {
            start_roguelike_run();
            return;
        }
        if (!_story_free_start)
            return;

        // Resume the new opening; pre-existing free-start saves have no lecture state.
        if (_baishiLecture is { departed_qingxi: false }) show_baishi_opening();
        else open_world();
    }

    private void start_roguelike_run()
    {
        if (player is null) return;
        var instances = _dynamic_dungeon_state.GetValueOrDefault("instances", new Godot.Collections.Array()).AsGodotArray();
        var run = instances.FirstOrDefault(value => value.VariantType == Variant.Type.Dictionary &&
            DynamicDungeonExpedition.IsRoguelikeInstance(value.AsGodotDictionary()));
        if (run.VariantType != Variant.Type.Dictionary)
        {
            var profile = GetNodeOrNull<GameStartProfile>("/root/GameStartProfile");
            var archetype = profile?.roguelike_archetype ?? "sword";
            var id = "rogue_" + System.Guid.NewGuid().ToString("N");
            var seed = System.Guid.NewGuid().ToString("N");
            var instance = DynamicDungeonExpedition.CreateRoguelikeInstance(id, seed, current_game_hour(), archetype);
            instances.Add(instance);
            _dynamic_dungeon_state["instances"] = instances;
            _dynamic_dungeon_state["roguelike_run_id"] = id;
            mark_save_dirty();
            run = instance;
        }
        var activeRun = run.AsGodotDictionary();
        open_dynamic_dungeon(activeRun.GetValueOrDefault("instance_id", "").AsString());
    }
}
