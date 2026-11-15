using Godot;

/// <summary>用当前物品图标规格打包七张符工图。</summary>
public partial class TalismanIconPack : Node
{
    public override void _Ready()
    {
        try
        {
            InventoryIconDensityPack.Pack(InventoryIconDensityPack.TalismanSpecs);
            GD.Print($"TALISMAN_ICON_PACK_OK icons=7 size={InventoryIconDensityPack.IconPixels}");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }
}
