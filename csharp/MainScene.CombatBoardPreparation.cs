public partial class MainScene
{
    public void notify_combat_board_configuration_changed(Character actor)
    {
        if(actor!=player)return;
        mark_save_dirty();save_progress_now(false);
    }
}
