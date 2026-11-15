using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

/// <summary>Exercises real native trade rows and commit callbacks without loading or writing player saves.</summary>
public partial class TradeNativeIntegrationSmoke : Control
{
    private const string Stone = "res://resources/entities/items/灵石.tres";
    private const string Bone = "res://resources/entities/items/兽骨.tres";
    private int _checks;
    private TradePanel _panel = null!;
    private void Check(bool result, string reason) { _checks++; if (!result) throw new InvalidOperationException(reason); }
    private async Task Frames(int count = 4) { for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private Character Player(int stones)
    {
        var player = new Character { entity_data = new HumanData { entity_name = "交易验证", current_world_stamina = 100, max_world_stamina = 100 } };
        player.SetMeta("artifact_battle_clone", true); AddChild(player); player.Hide();
        Check(player.add_item_to_inventory(Stone, stones), "seed inventory rejected"); return player;
    }
    private static WorldNpcData Npc() => new() { npc_id = Guid.NewGuid().ToString("N"), npc_name = "交易同修", trade_inventory = new() { [Bone] = 1 } };
    private Button Source(string grid, string name) => _panel.FindChild(grid, true, false)!.GetChildren().OfType<Button>()
        .First(b => b.FindChild("ItemName", true, false) is Label l && l.Text == name);
    private Button Commit => _panel.FindChild("CommitTrade", true, false) as Button ?? throw new InvalidOperationException("Missing commit button");
    private int OfferRows(string name) => _panel.FindChild(name, true, false)!.GetChildren().OfType<Button>().Count();
    private string Placement(Character player) => string.Join("|", player.get_spatial_inventory().Select(i => $"{i.InstanceId}:{i.ItemPath}:{i.RegionId}:{i.X}:{i.Y}:{i.Width}:{i.Height}:{i.Rotation}"));
    private void Choose(string grid, string name, int quantity) => Source(grid, name).FindChildren("*", "SpinBox", true, false).OfType<SpinBox>().Single().Value = quantity;
    public override void _Ready() => CallDeferred(nameof(Run));
    private async void Run()
    {
        try
        {
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            _panel = new TradePanel(); AddChild(_panel);
            var player = Player(3); var npc = Npc(); int completed = 0; _panel.TradeCompleted += () => completed++;
            _panel.show_for(player, npc); await Frames();
            Check(_panel.FindChild("TradeColumns", true, false)!.GetChildCount() == 4, "trade must show source and offer lists separately");
            var row = Source("PlayerTradeList", "灵石");
            Check(_panel.FindChild("PlayerTradeList", true, false)!.GetChildren().OfType<Button>().Count() == 1, "independent instances not grouped by item definition");
            Check(row.FindChildren("*", "TextureRect", true, false).Count == 0, "source row contains item icon");
            row.EmitSignal(BaseButton.SignalName.Pressed); Source("NpcTradeList", "兽骨").EmitSignal(BaseButton.SignalName.Pressed);
            Check(OfferRows("OutgoingTradeList") == 1 && OfferRows("IncomingTradeList") == 1, "clicking sources must populate both offer lists");
            Choose("PlayerTradeList", "灵石", 2);
            var outgoing = _panel.FindChild("OutgoingTradeList", true, false)!.GetChildren().OfType<Button>().Single();
            Check(outgoing.FindChild("SelectedCount", true, false) is Label { Text: "2" }, "quantity picker did not update offer count");
            outgoing.EmitSignal(BaseButton.SignalName.Pressed);
            Check(_panel.FindChild("OutgoingTradeList", true, false)!.GetChildren().OfType<Button>().Single().FindChild("SelectedCount", true, false) is Label { Text: "1" }, "offer row must decrease selected quantity");
            Check(!Commit.Disabled, "fair exchange rejected");
            if (DisplayServer.GetName() != "headless")
            {
                await Frames(); await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://outputs/native_inventory_20261031"));
                using var image = GetViewport().GetTexture().GetImage();
                image.SavePng("res://outputs/native_inventory_20261031/交易_文字目录与双侧清单.png");
            }
            Commit.EmitSignal(BaseButton.SignalName.Pressed); await Frames();
            Check(player.get_inventory_total_count(Stone) == 2 && player.get_inventory_total_count(Bone) == 1, "player exchange quantity mismatch");
            Check(npc.trade_inventory[Stone].AsInt32() == 1 && !npc.trade_inventory.ContainsKey(Bone), "NPC exchange quantity mismatch");
            Check(player.current_world_stamina == 99 && completed == 1, "successful exchange must charge once and emit once");
            Check(npc.action_history.Count == 1 && npc.action_history[0].action_id == "player_trade_completed", "successful trade record missing");
            Check(CharacterRelationshipRules.Get((HumanData)player.entity_data!, npc.npc_id)?.pending_evidence.Count == 1, "fair trade relationship evidence missing");

            var tired = Player(3); var tiredNpc = Npc(); _panel.show_for(tired, tiredNpc); await Frames();
            Choose("PlayerTradeList", "灵石", 1); Choose("NpcTradeList", "兽骨", 1); tired.current_world_stamina = 0;
            var before = Placement(tired); Commit.EmitSignal(BaseButton.SignalName.Pressed);
            Check(Placement(tired) == before && tiredNpc.trade_inventory[Bone].AsInt32() == 1 && completed == 1, "stamina failure mutated inventory or event count");

            var stale = Player(3); var staleNpc = Npc(); _panel.show_for(stale, staleNpc); await Frames();
            Choose("PlayerTradeList", "灵石", 1); Choose("NpcTradeList", "兽骨", 1); staleNpc.trade_inventory.Remove(Bone);
            before = Placement(stale); Commit.EmitSignal(BaseButton.SignalName.Pressed);
            Check(Placement(stale) == before && stale.current_world_stamina == 100 && completed == 1, "stale NPC offer duplicated items or charged stamina");

            var full = Player(Character.InventorySlotCapacity); var fullNpc = Npc(); _panel.show_for(full, fullNpc); await Frames();
            Choose("PlayerTradeList", "灵石", 1); Choose("NpcTradeList", "兽骨", 1); before = Placement(full);
            Commit.EmitSignal(BaseButton.SignalName.Pressed);
            Check(Placement(full) == before && full.get_inventory_total_count(Stone) == Character.InventorySlotCapacity && full.current_world_stamina == 100 && fullNpc.trade_inventory[Bone].AsInt32() == 1,
                "capacity rejection must preserve exact instance positions, both inventories and stamina");
            Choose("PlayerTradeList", "灵石", 12); Commit.EmitSignal(BaseButton.SignalName.Pressed); await Frames();
            Check(Placement(full) == before && full.get_inventory_total_count(Stone) == Character.InventorySlotCapacity && full.current_world_stamina == 100 && completed == 1,
                "twelve freed cells in an 8-wide region do not fit a 5-by-2 bone; area-only acceptance must not occur");
            Choose("PlayerTradeList", "灵石", 16); Commit.EmitSignal(BaseButton.SignalName.Pressed); await Frames();
            Check(full.get_inventory_total_count(Stone) == Character.InventorySlotCapacity - 16 && full.get_inventory_total_count(Bone) == 1 && full.current_world_stamina == 99 && completed == 2,
                "exchange must release offered space before fitting incoming object");
            Check(full.get_spatial_inventory().All(i => i.IsPlaced), "successful trade left unplaced item");

            var over = Player(1); var overNpc = Npc(); overNpc.trade_inventory[Stone] = int.MaxValue;
            _panel.show_for(over, overNpc); await Frames(); Choose("PlayerTradeList", "灵石", 1); before = Placement(over); Commit.EmitSignal(BaseButton.SignalName.Pressed);
            Check(Placement(over) == before && overNpc.trade_inventory[Stone].AsInt32() == int.MaxValue && over.current_world_stamina == 100, "NPC overflow must be rejected atomically");
            _panel.close(); Check(!_panel.Visible, "close must hide native panel");
            GD.Print($"NATIVE_TRADE_INTEGRATION_OK: {_checks} checks; native source/offer rows, quantity, actual transaction, stamina, stale NPC, capacity and exact position conservation");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
