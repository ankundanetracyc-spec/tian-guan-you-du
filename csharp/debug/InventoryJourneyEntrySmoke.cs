using Godot;
using System.Reflection;

/// <summary>Loads the production journey and its real UI handoffs without opening a save slot.</summary>
public partial class InventoryJourneyEntrySmoke : Node
{
    private int _checks;
    private MainScene? _journey;
    private void Check(bool ok, string reason) { _checks++; if (!ok) throw new InvalidOperationException(reason); }
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
    private static object? Invoke(object owner, string name, params object[] args) => owner.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(owner, args);
    private async Task Frames(int count = 4) { for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    public override void _Ready() => CallDeferred(nameof(Run));
    private async void Run()
    {
        try
        {
            var saves = GetNode<GameSaveManager>("/root/GameSaveManager");
            Check(!saves.has_active_slot, "入口检查必须没有活动存档");
            _journey = GD.Load<PackedScene>("res://tscn/scene/MainScene.tscn").Instantiate<MainScene>();
            AddChild(_journey);
            var deadline = Time.GetTicksMsec() + 90000UL;
            while (!_journey.StartupComplete && Time.GetTicksMsec() < deadline) await Frames(1);
            Check(_journey.StartupComplete && _journey.StartupError is null, "真实旅程初始化失败：" + _journey.StartupError);
            await Frames(10);
            Check(!saves.has_active_slot, "真实旅程自行创建了存档");
            var player = Field<Character>(_journey, "player");
            var human = (HumanData)player.entity_data!;
            var inventory = _journey.GetNode<StoragePanel>("CanvasLayer/StoragePanel");
            Check(ReferenceEquals(Field<Character>(inventory, "_player"), player), "行囊未绑定实际玩家");
            Check(player.get_spatial_inventory().All(e => e.IsPlaced), "新旅程存在未安置物品");
            Check(player.get_inventory_occupied_cell_count() <= 120, "新旅程初始物品超出空间");
            var changed = (Delegate?)typeof(Character).GetField("InventoryChanged", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(player);
            Check(changed?.GetInvocationList().Any(d => ReferenceEquals(d.Target, _journey) && d.Method.Name == "on_player_inventory_save_changed") == true,
                "实际玩家摆放变更未连接存档标记");
            Invoke(_journey, "open_storage"); await Frames();
            Check(inventory.IsVisibleInTree() && inventory.FindChild("BodyStorage", true, false) is Control, "HUD行囊入口未显示五区空间");
            Check(inventory.FindChild("ItemIndex", true, false) is ScrollContainer, "真实行囊缺少汇总清册");
            Check(_journey.try_return_to_previous_page() && !inventory.Visible, "真实返回入口不能关闭行囊");

            // The fixture stands in the same indoor location as the player; use the production contact and trade entry.
            Field<Control>(_journey, "tile_world_map").Hide();
            var npc = new WorldNpcData { npc_id = "entry_smoke_trade", npc_name = "入口验证同修", current_node_id = Field<string>(_journey, "_player_world_node_id"),
                trade_inventory = new() { ["res://resources/entities/items/兽骨.tres"] = 1 } };
            _journey.open_world_npc_trade(npc); await Frames();
            var trade = Field<TradePanel>(_journey, "trade_panel");
            Check(trade.IsVisibleInTree(), "实际NPC交易入口未打开");
            Check(trade.FindChild("TradeColumns", true, false)?.GetChildCount() == 4, "实际交易没有四份文字目录");
            Check(trade.FindChild("NpcTradeList", true, false)!.GetChildren().OfType<Button>().Any(), "实际对方商品未绑定");
            Check(trade.FindChild("NpcTradeList", true, false)!.FindChildren("*", "TextureRect", true, false).Count == 0, "实际交易目录仍有图标");
            Check(_journey.try_return_to_previous_page() && !trade.Visible, "实际返回入口不能关闭交易");

            human.sect_affiliation_id = "yuxu_xianzong"; human.sect_current_rank = "外门弟子";
            Check(human.record_yuxu_contribution(new YuxuSectContributionRecord { record_id = "entry_smoke_grant", source_transaction_id = "entry_smoke_grant",
                transaction_kind = YuxuSectContributionRecord.KindOfficialDutyReward, source_title = "隔离入口验证", occurred_at = "1年1月1日", contribution_delta = 100,
                cost_summary = "隔离入口验证", result_summary = "隔离入口验证" }), "贡献夹具失败");
            Invoke(_journey, "refresh_yuxu_life_context");
            var overview = _journey.GetNode<Control>("SceneLayer/YuxuXianzongOverview");
            overview.Call("open_overview"); overview.Call("_on_peak_pressed", "heguang");
            var peak = overview.GetNode<Control>("PeakInterior");
            peak.Call("_open_exchange_board"); await Frames();
            var shop = peak.FindChildren("*", "Control", true, false).OfType<YuxuSectExchangePanel>().Single();
            Check(shop.IsVisibleInTree(), "实际和光峰善功堂入口未打开");
            Check(shop.FindChild("CategoryColumns", true, false)?.GetChildCount() == 9, "实际善功堂分类缺失");
            Check(Field<Label>(shop, "_balanceLabel").Text.Contains("100"), "实际贡献未传入界面");
            var pill = "res://resources/entities/items/alchemy/huichun_1.tres";
            var previous = player.get_inventory_total_count(pill);
            var row = shop.FindChild("Item_alchemy_huichun_9_1", true, false) as Button;
            Check(row is not null, "实际善功堂缺少良效回春丹"); row!.EmitSignal(BaseButton.SignalName.Pressed);
            Field<Button>(shop, "_exchangeButton").EmitSignal(BaseButton.SignalName.Pressed);
            Check(shop.FindChild("PurchaseConfirmation", true, false) is Control { Visible: true }, "实际兑换确认未显示");
            Field<Button>(shop, "_purchaseConfirm").EmitSignal(BaseButton.SignalName.Pressed); await Frames();
            Check(player.get_inventory_total_count(pill) == previous + 1 && human.get_yuxu_contribution_total() == 92, "善功堂→峰内→主场景实际结算未完成");
            Check(Field<Label>(shop, "_balanceLabel").Text.Contains("92"), "实际结算余额没有回传界面");
            var snapshot = _journey.capture_battle_world_snapshot();
            Check(snapshot?.player_data?.inventory_placements.Count == human.inventory_placements.Count && snapshot?.player_data != human,
                "实际持久化快照未包含独立空间摆放");
            Check(!saves.has_active_slot && !saves.IsBackgroundSaveBusy, "隔离检查触发真实存档写入");
            _journey.prepare_return_to_title_from_menu(false); _journey.QueueFree(); await Frames(3);
            GD.Print($"InventoryJourneyEntrySmoke: {_checks} checks passed"); GetTree().Quit();
        }
        catch (Exception exception)
        {
            _journey?.prepare_return_to_title_from_menu(false);
            _journey?.QueueFree(); await Frames(3);
            GD.PushError("InventoryJourneyEntrySmoke: " + exception); GetTree().Quit(1);
        }
    }
}
