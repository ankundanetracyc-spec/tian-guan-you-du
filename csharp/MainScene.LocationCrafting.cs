using Godot;

public partial class MainScene
{
    private LocationCraftingPanel? _locationCrafting;

    private void open_yuxu_crafting(string actionId)
    {
        var overview = GetNodeOrNull<Control>("SceneLayer/YuxuXianzongOverview");
        if (player is null || _player_current_yuxu_peak_id != "tiangong" || overview?.Visible != true ||
            overview.GetNodeOrNull<Control>("PeakInterior")?.Visible != true) return;
        if (actionId == "tiangong_talisman") _locationCrafting?.ShowTalismanFor(player);
        else _locationCrafting?.ShowFor(player, actionId == "tiangong_alchemy");
    }
}
