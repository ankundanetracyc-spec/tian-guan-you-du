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
        if (player is not null && !player.grant_new_journey_yunxi_breakthrough_pills())
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
        if (!_story_free_start)
            return;

        // Resume the new opening; pre-existing free-start saves have no lecture state.
        if (_baishiLecture is { departed_qingxi: false }) show_baishi_opening();
        else open_world();
    }
}
