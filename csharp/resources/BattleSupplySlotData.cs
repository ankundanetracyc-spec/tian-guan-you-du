using Godot;

/// <summary>本人战备中的一堆真实消耗品；库存仍保存在本人行囊。</summary>
public partial class BattleSupplySlotData : Resource
{
    [Export] public string item_path = "";
    [Export] public int count;
}
