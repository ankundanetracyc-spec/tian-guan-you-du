using Godot;
using System;
using System.Linq;
using System.Collections.Generic;

public partial class YuxuMarketCurrentCheck : Node
{
    private const string Root = "res://resources/entities/items/";
    private const string Bone = Root + "兽骨.tres", Herb = Root + "雾铃花.tres", Iron = Root + "玄铁.tres", Stone = YuxuMarketRules.StonePath;
    private const string Bag = "res://tools/fixtures/storage_bag_large.tres";
    private const string Output = "E:/新建文件夹 (2)/2026-11-15/new-chat/";
    private static void Check(bool ok, string reason) { if (!ok) throw new InvalidOperationException(reason); }
    private static WorldNpcData Seller(string id, string path, int amount) => new()
    {
        npc_id = id, npc_name = id, faction_id = "yuxu_xianzong", is_alive = true, current_state = "空闲",
        current_node_id = YuxuMarketRules.NodeId, current_subscene_id = YuxuMarketRules.PeakId,
        trade_inventory = new Godot.Collections.Dictionary { [path] = amount }
    };
    private Character Player(int stones)
    {
        var player = new Character { entity_data = new HumanData { entity_name = "坊市查看", sect_affiliation_id = "yuxu_xianzong", current_world_stamina = 0 } };
        AddChild(player);
        if (stones > 0) Check(player.add_item_to_inventory(Stone, stones), "fixture stones");
        return player;
    }
    public override async void _Ready()
    {
        try
        {
            var world = new NpcWorldManager();
            AddChild(world);
            var a = Seller("seller_a", Bone, 3); var b = Seller("seller_b", Herb, 4);
            var c = Seller("seller_c", Iron, 3); var d = Seller("seller_d", Bone, 2);
            world.npcs.Add(a); world.npcs.Add(b); world.npcs.Add(c); world.npcs.Add(d);
            var player = Player(60);
            Check(player.add_item_to_inventory(Herb, 4), "fixture herb");
            var panel = new YuxuMarketPanel(); AddChild(panel);
            panel.Open(player, world, () => true);
            await Frames();
            Check(a.yuxu_market_stall_number != b.yuxu_market_stall_number && a.yuxu_market_stall_number > 0, "stall number");
            var originalNumber = a.yuxu_market_stall_number;
            var offers = panel.FindChildren("*", "Button", true, false).OfType<Button>().Where(node => node.Name.ToString().StartsWith("MarketOffer")).ToArray();
            Check(offers.Length >= 3, "offer columns");
            Click(offers[0]); await Frames();
            offers = panel.FindChildren("*", "Button", true, false).OfType<Button>().Where(node => node.Name.ToString().StartsWith("MarketOffer")).ToArray();
            Click(offers[1]); await Frames();
            var visibleInputs = panel.FindChildren("SelectedQuantity", "LineEdit", true, false).OfType<LineEdit>().Where(input => input.IsVisibleInTree()).ToArray();
            Check(visibleInputs.Length == 2, "multi selection");
            var selected = visibleInputs[0];
            selected.GrabFocus(); selected.Text = "2";
            var plus = selected.GetParent().GetChildren().OfType<YuxuMarketQuantityStep>().Single(step => step.Increase);
            var minus = selected.GetParent().GetChildren().OfType<YuxuMarketQuantityStep>().Single(step => !step.Increase);
            Check(Mathf.IsEqualApprox(plus.Position.Y + plus.Size.Y / 2, selected.Position.Y + selected.Size.Y / 2)
                && Mathf.IsEqualApprox(minus.Position.Y + minus.Size.Y / 2, selected.Position.Y + selected.Size.Y / 2), "quantity alignment");
            Click(plus); await Frames(); Check(selected.Text == "3", "input then plus");
            selected.GrabFocus(); Click(minus); await Frames(); Check(selected.Text == "2", "input then minus");
            GetWindow().Mode = Window.ModeEnum.Windowed; GetWindow().Size = new Vector2I(1280, 720);
            await Frames(); await Capture("坊市_多选_1280.png");
            GetWindow().Size = new Vector2I(1920, 1080);
            await Frames(); await Capture("坊市_多选_1920.png");
            var search = panel.FindChild("MarketSearch", true, false) as LineEdit ?? throw new Exception("search");
            search.Text = "甲子"; await Frames();
            Check(a.yuxu_market_stall_number == originalNumber, "search renumber");
            search.Text = ""; await Frames();
            var buy = panel.FindChild("BuyItem", true, false) as Button ?? throw new Exception("buy");
            selected = panel.FindChildren("SelectedQuantity", "LineEdit", true, false).OfType<LineEdit>().First(input => input.IsVisibleInTree());
            selected.GrabFocus();
            Click(buy); await Frames();
            Check(player.current_world_stamina == 0 && player.get_inventory_total_count(Bone) == 2 && player.get_inventory_total_count(Herb) == 5,
                "batch purchase or stamina");
            Check(player.get_inventory_total_count(Stone) == 52 && a.trade_inventory[Bone].AsInt32() == 1 && b.trade_inventory[Herb].AsInt32() == 3, "batch costs");
            Click(buy); await Frames();
            Check(player.get_inventory_total_count(Stone) == 52 && a.trade_inventory[Bone].AsInt32() == 1 &&
                b.trade_inventory[Herb].AsInt32() == 3, "repeat click charged twice");
            var stallTab = panel.FindChildren("*", "Button", true, false).OfType<Button>().Single(node => node.Text == "我的摊位");
            Click(stallTab); await Frames();
            var rent = panel.FindChild("RentMarketStall", true, false) as Button ?? throw new Exception("rent");
            Click(rent); await Frames();
            var confirm = panel.FindChild("ConfirmMarketAction", true, false) as Button ?? throw new Exception("confirm rent");
            Click(confirm); await Frames();
            var human = (HumanData)player.entity_data!;
            Check(human.yuxu_market_stall_number > 0 && human.yuxu_market_stall_number != a.yuxu_market_stall_number, "own stall");
            await Capture("坊市_我的摊位_实机.png");
            var source = panel.FindChildren("*", "Button", true, false).OfType<YuxuMarketDragRow>()
                .First(node => node.Payload.GetValueOrDefault("kind", "").AsString() == "inventory" && node.ItemName == "雾铃花");
            var dest = panel.FindChild("MarketListingsScroll", true, false) as Control ?? throw new Exception("drop destination");
            await Drag(source, dest);
            Check(human.yuxu_market_listings.Count == 1 && player.get_inventory_total_count(Herb) == 4, "drag to list");
            var price = panel.FindChild("ListingPrice", true, false) as LineEdit ?? throw new Exception("price input");
            Click(price); price.Text = "7"; price.EmitSignal(LineEdit.SignalName.TextSubmitted, "7"); await Frames();
            Check(human.yuxu_market_listings.Single()["unit_price"].AsInt32() == 7, "change listing price");
            GetWindow().Size = new Vector2I(1280, 720);
            await Frames(); await Capture("坊市_我的摊位_1280.png");
            GetWindow().Size = new Vector2I(1920, 1080);
            await Frames(); await Capture("坊市_我的摊位_1920.png");
            var listing = panel.FindChildren("*", "Button", true, false).OfType<YuxuMarketDragRow>()
                .First(node => node.Payload.GetValueOrDefault("kind", "").AsString() == "listing");
            var sourceArea = panel.FindChild("MarketInventoryScroll", true, false) as Control ?? throw new Exception("source destination");
            await Drag(listing, sourceArea);
            GD.Print($"AFTER_WITHDRAW count={human.yuxu_market_listings.Count} herb={player.get_inventory_total_count(Herb)} status={panel.FindChildren("*", "Label", true, false).OfType<Label>().FirstOrDefault(l => l.Text.Contains("行囊容量"))?.Text}");
            Check(human.yuxu_market_listings.Count == 0 && player.get_inventory_total_count(Herb) == 5, "drag back");

            // Each failure must preserve all seller inventories and the buyer's funds.
            var rejectSeller = Seller("reject", Bone, 2);
            var rejectOther = Seller("reject_other", Herb, 2);
            var poor = Player(1);
            var rejected = new YuxuMarketRules.Purchase(new YuxuMarketRules.Offer(rejectSeller, Bone, 2, 3), 1);
            var validOther = new YuxuMarketRules.Purchase(new YuxuMarketRules.Offer(rejectOther, Herb, 2, 2), 1);
            Check(!YuxuMarketRules.TryBuyBatch(poor, [rejected, validOther], 0, out _) && poor.get_inventory_total_count(Stone) == 1
                && rejectSeller.trade_inventory[Bone].AsInt32() == 2 && rejectOther.trade_inventory[Herb].AsInt32() == 2, "insufficient funds rollback");
            var stale = new YuxuMarketRules.Purchase(new YuxuMarketRules.Offer(rejectOther, Herb, 2, 4), 1);
            Check(!YuxuMarketRules.TryBuyBatch(player, [rejected, stale], 0, out _) && rejectSeller.trade_inventory[Bone].AsInt32() == 2
                && rejectOther.trade_inventory[Herb].AsInt32() == 2, "stale price rollback");
            var stock = new YuxuMarketRules.Purchase(new YuxuMarketRules.Offer(rejectOther, Herb, 2, 2), 3);
            Check(!YuxuMarketRules.TryBuyBatch(player, [rejected, stock], 0, out _) && rejectSeller.trade_inventory[Bone].AsInt32() == 2
                && rejectOther.trade_inventory[Herb].AsInt32() == 2, "stale stock rollback");
            string bulkyPath = "user://market_capacity_fixture.tres";
            var bulky = (ItemData)GD.Load<ItemData>(Iron).Duplicate(true);
            bulky.storage_volume = 5;
            Check(ResourceSaver.Save(bulky, bulkyPath) == Error.Ok, "bulky fixture");
            var bulkySeller = Seller("bulky", bulkyPath, 1);
            var bulkyOffer = new YuxuMarketRules.Purchase(new YuxuMarketRules.Offer(bulkySeller, bulkyPath, 1, 3), 1);
            int remaining = player.get_inventory_free_capacity();
            Check(remaining > 0 && player.add_item_to_inventory(Stone, remaining), "fill capacity");
            int balance = player.get_inventory_total_count(Stone);
            Check(!YuxuMarketRules.TryBuyBatch(player, [bulkyOffer, validOther], 0, out _) && player.get_inventory_total_count(Stone) == balance
                && bulkySeller.trade_inventory[bulkyPath].AsInt32() == 1 && rejectOther.trade_inventory[Herb].AsInt32() == 2, "capacity rollback");
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(bulkyPath));
            var bagOwner = Player(2);
            var bagHuman = (HumanData)bagOwner.entity_data!;
            Check(bagOwner.add_item_to_inventory(Bag) && bagOwner.add_item_to_inventory(Herb, 3) && YuxuMarketRules.TryRent(bagOwner, 0, out _), "storage fixture");
            var bag = bagOwner.get_storage_bags().First();
            var herbs = bagOwner.get_spatial_inventory().First(entry => entry.ItemPath == Herb);
            Check(bagOwner.try_transfer_storage_item(herbs.InstanceId, bag.InstanceId, out _), "transfer to bag");
            Check(YuxuMarketRules.TryListStorageItem(bagOwner, bag.InstanceId, herbs.InstanceId, Herb, 1, out _), "list bag item");
            Check(bagOwner.get_storage_bag_inventory(bag.InstanceId).Single().Count == 2
                && bagHuman.yuxu_market_listings.Single()["quantity"].AsInt32() == 1, "bag stack conservation");
            Check(YuxuMarketRules.TryWithdrawOne(bagOwner, bagHuman.yuxu_market_listings.Single()["id"].AsString(), out _)
                && bagOwner.get_inventory_total_count(Herb) == 1, "withdraw bag item");
            GD.Print("MARKET_CURRENT_CHECK_OK");
            GetTree().Quit();
        }
        catch (Exception exception) { GD.PushError(exception.ToString()); GetTree().Quit(1); }
    }
    private async Task Frames() { for (int i = 0; i < 5; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private async Task Capture(string name)
    {
        await Frames(); HoverInfoManager.HideAll(); await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var shot = GetViewport().GetTexture().GetImage(); Check(shot.SavePng(Output + name) == Error.Ok, "capture");
    }
    private void Click(Control control)
    {
        var p = control.GetGlobalTransformWithCanvas() * (control.Size * .5f);
        GetViewport().PushInput(new InputEventMouseMotion { Position = p, GlobalPosition = p }, true);
        foreach (bool pressed in new[] { true, false })
            GetViewport().PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = pressed, Position = p, GlobalPosition = p }, true);
    }
    private async Task Drag(Control source, Control destination)
    {
        Vector2 a = source.GetGlobalTransformWithCanvas() * new Vector2(90, source.Size.Y / 2);
        Vector2 b = destination.GetGlobalTransformWithCanvas() * new Vector2(destination.Size.X / 2, destination.Size.Y - 60);
        GetViewport().PushInput(new InputEventMouseMotion { Position = a, GlobalPosition = a }, true);
        await Frames();
        GD.Print($"DRAG_SOURCE {source.Name} visible={source.IsVisibleInTree()} pos={a} hovered={GetViewport().GuiGetHoveredControl()?.Name}");
        GetViewport().PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, ButtonMask = MouseButtonMask.Left,
            Pressed = true, Position = a, GlobalPosition = a }, true);
        GetViewport().PushInput(new InputEventMouseMotion { Position = a + new Vector2(15, 0), GlobalPosition = a + new Vector2(15, 0),
            Relative = new Vector2(15, 0), ButtonMask = MouseButtonMask.Left }, true);
        await Frames();
        Check(GetViewport().GuiIsDragging(), "drag did not start");
        GetViewport().PushInput(new InputEventMouseMotion { Position = b, GlobalPosition = b, Relative = b - a,
            ButtonMask = MouseButtonMask.Left }, true);
        await Frames();
        GD.Print($"DRAG_TARGET {destination.Name} pos={b} hovered={GetViewport().GuiGetHoveredControl()?.Name} data={GetViewport().GuiGetDragData()}");
        GetViewport().PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = b, GlobalPosition = b }, true);
        await Frames();
        GD.Print($"DRAG_RESULT {GetViewport().GuiIsDragSuccessful()}");
    }
}

