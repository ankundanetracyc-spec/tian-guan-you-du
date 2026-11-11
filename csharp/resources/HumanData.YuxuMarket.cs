using Godot;
using Godot.Collections;

public partial class HumanData
{
    /// <summary>坊市租约及资产随正式人物存档保存；旧档默认无租约和托管。</summary>
    [Export] public long yuxu_market_lease_start_hour = -1;
    [Export] public long yuxu_market_lease_end_hour = -1;
    [Export] public int yuxu_market_pending_stones;
    [Export] public Array<Dictionary> yuxu_market_listings = [];
    [Export] public Array<Dictionary> yuxu_market_receipts = [];
}
