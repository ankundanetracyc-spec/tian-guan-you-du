using Godot;

/// <summary>Fresh journeys begin in the open world while retaining the character's normal mortal start state.</summary>
public partial class MainScene
{
    private const string FreeCultivationStartNodeId = "white_stone_town";
    private static readonly Vector2I FreeCultivationStartCell = new(17, 142);
    private bool _story_free_start;

    private void initialize_story_free_start()
    {
        if (_loaded_game_save is not null)
            return;

        _story_free_start = true;
        _player_world_node_id = FreeCultivationStartNodeId;

        var profile = GetNodeOrNull<GameStartProfile>("/root/GameStartProfile");
        var gongfaPath = string.IsNullOrWhiteSpace(profile?.selected_initial_gongfa_path)
            ? GongfaAvailability.DefaultBookPath
            : profile.selected_initial_gongfa_path;
        if (player?.entity_data is HumanData human)
        {
            // Keep the creation-screen selection as the character's starting study reference.
            // Do not mark a chapter learned, grant study progress, or advance cultivation.
            human.intro_gongfa_path = gongfaPath;
            human.intro_gongfa_study_progress = 0f;
        }
        if (player is not null && !player.grant_new_journey_yunxi_breakthrough_pills())
            GD.PushError("新旅程未能发放云息突破丹药；请检查行囊容量与丹药物品定义。");

        var startCell = FreeCultivationStartCell;
        if (tile_world_map is not null)
            startCell = tile_world_map.Call("get_location_anchor_cell", FreeCultivationStartNodeId).AsVector2I();
        if (startCell.X < 0 || startCell.Y < 0)
        {
            GD.PushError("白石镇未配置有效的世界地图锚点；暂沿用其固定地图坐标。");
            startCell = FreeCultivationStartCell;
        }
        _player_world_grid_cell = startCell;
        hud?.set_world_map_available(true);
        set_prologue_skip_available(false);
        mark_save_dirty();
    }

    private bool has_open_world_access()
        => _story_free_start || _qingxi_prologue == QingxiPrologueStep.Departed;

    private void show_story_free_start()
    {
        if (!_story_free_start)
            return;

        // The fresh-run landing page is the world map itself; no Qingxi scene or dialogue is opened.
        open_world();
    }
}
