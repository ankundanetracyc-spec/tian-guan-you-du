using Godot;

/// <summary>Persistence and common action feedback for the shared world-stamina pool.</summary>
public partial class MainScene
{
    public void notify_world_stamina_changed(Character _)
    {
        mark_save_dirty();
    }

    public bool try_spend_player_world_stamina(float amount, string actionName)
    {
        if (player is null || !player.try_spend_world_stamina(amount))
        {
            show_message("体力不足", $"体力不足，无法{actionName}。休息可以恢复体力。查看、取消和无效操作不消耗体力。");
            return false;
        }
        mark_save_dirty();
        return true;
    }
}
