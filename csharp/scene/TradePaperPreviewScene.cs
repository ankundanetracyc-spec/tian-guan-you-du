using Godot;

/// <summary>Independent trade proposal. All stock, costs and trade records belong only to disposable fixture actors.</summary>
public partial class TradePaperPreviewScene : Control
{
    public const string Items = "res://resources/entities/items/";
    public const int SampleHour = (2 * 30 + 11) * 24 + 10;
    private Character _actor = null!;
    private WorldNpcData _merchant = null!;
    private TradePaperPanel _panel = null!;
    public bool ExitOnClose { get; set; } = true;
    public TradePaperPanel Panel => _panel;
    public Character Actor => _actor;
    public WorldNpcData Merchant => _merchant;
    public int CompletedCount { get; private set; }
    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _panel = new TradePaperPanel { Name = "TradePaperPreviewPanel" }; AddChild(_panel);
        _panel.Closed += () =>
        {
            if (ExitOnClose) { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); GetTree().Quit(); }
            else Hide();
        };
        _panel.TradeCompleted += () => CompletedCount++;
        ResetFixture();
    }
    public void ResetFixture(bool empty = false)
    {
        var previous = _actor;
        _actor = new Character { entity_name = "苏醒者", entity_data = new HumanData { entity_name = "苏醒者", current_world_stamina = 100, max_world_stamina = 100 } };
        _actor.SetMeta("artifact_battle_clone", true); AddChild(_actor); _actor.Hide();
        _merchant = new WorldNpcData
        {
            npc_id = "paper_trade_zhou", npc_name = "周满仓", formal_identity = "", cultivation_realm = "凡人",
            faction_id = empty ? YuxuMarketRules.SectId : "", current_node_id = "white_stone_town", current_subscene_id = "inn"
        };
        if (!empty)
        {
            foreach (var (name, count) in new[] { ("灵石", 12), ("兽皮", 3), ("雾铃花", 4), ("苦苔根", 4), ("玄铁", 1) })
                if (!_actor.add_item_to_inventory(Items + name + ".tres", count)) throw new InvalidOperationException("Trade preview fixture does not fit: " + name);
            foreach (var (name, count) in new[] { ("灵石", 8), ("兽骨", 4), ("松心炭", 3), ("玄铁", 2), ("雾铃花", 2) })
                _merchant.trade_inventory[Items + name + ".tres"] = count;
        }
        _panel.BindPreview(_actor, _merchant);
        if (previous is not null && GodotObject.IsInstanceValid(previous)) previous.QueueFree();
    }
    public void ClearSelection() => _panel.ClearQuantities();
}
