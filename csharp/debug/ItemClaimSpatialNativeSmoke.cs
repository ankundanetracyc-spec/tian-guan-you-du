using Godot;
using System.Reflection;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

/// <summary>Actual receipt handlers and spatial inventory, isolated from journey saves.</summary>
public partial class ItemClaimSpatialNativeSmoke : Node
{
    private const string Stone = "res://resources/entities/items/灵石.tres";
    private const string Book = "res://resources/entities/items/玄枢含章经秘籍.tres";
    private const string Iron = "res://resources/entities/items/玄铁.tres";
    private int _checks;
    private readonly List<Character> _players = [];
    private void Check(bool ok, string message) { _checks++; if (!ok) throw new InvalidOperationException(message); }
    private Character Player() { var p = new Character { entity_data = new HumanData { entity_name = "领取空间验证" } }; _players.Add(p); return p; }
    private static string Stamp(Character p) => string.Join("|", p.get_spatial_inventory().OrderBy(e => e.InstanceId).Select(e => $"{e.InstanceId}:{e.ItemPath}:{e.RegionId}:{e.X},{e.Y}:{e.Rotation}"));
    private static object? Invoke(ItemClaimPanel p, string name, params object?[] args) => typeof(ItemClaimPanel).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(p, args);
    private static T Field<T>(ItemClaimPanel p, string name) => (T)typeof(ItemClaimPanel).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(p)!;
    private static GArray Rewards(params (string Path, int Count)[] entries)
    { var array = new GArray(); foreach (var e in entries) array.Add(new GDictionary { ["item_path"] = e.Path, ["count"] = e.Count }); return array; }
    private ItemClaimPanel Present(Character p, GArray entries, Action<GArray>? claimed = null)
    { var panel = new ItemClaimPanel(); AddChild(panel); Check(panel.Present(p, entries, claimed: claimed), "领取队列无法打开"); return panel; }
    private async Task Clear(ItemClaimPanel p)
    {
        if (GodotObject.IsInstanceValid(p) && !p.IsQueuedForDeletion())
            p.FindChildren("ClaimFooterClose", "Button", true, false).OfType<Button>().First().EmitSignal(BaseButton.SignalName.Pressed);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Check(!GetTree().Paused, "关闭领取后暂停状态未恢复");
    }
    public override async void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        try
        {
            var full = Player(); Check(full.add_item_to_inventory(Stone, 120), "满包样本创建失败");
            int claimed = 0; var panel = Present(full, Rewards((Stone, 1)), _ => claimed++); var before = Stamp(full);
            Invoke(panel, "claim_selected_items");
            Check(Stamp(full) == before && claimed == 0 && Field<bool>(panel, "_presented"), "满包领取改变库存或消耗队列");
            Check(panel.FindChildren("ClaimFooterStatus", "Label", true, false).OfType<Label>().Last().Text.Contains("空间不足"), "满包反馈缺失");
            await Clear(panel);

            var partial = Player(); Check(partial.add_item_to_inventory(Stone, 119), "批量失败样本创建失败");
            panel = Present(partial, Rewards((Stone, 1), (Book, 1)), _ => claimed++); before = Stamp(partial);
            Invoke(panel, "claim_selected_items"); Invoke(panel, "claim_selected_items");
            Check(Stamp(partial) == before && partial.get_inventory_total_count(Book) == 0 && claimed == 0, "批量失败曾部分入包或重复领取");
            await Clear(panel);

            var success = Player(); int notices = 0, changed = 0, callbackEntries = 0; ItemClaimPanel? activeReceipt = null;
            success.ItemAcquired += (_, _, count) => notices += count; success.InventoryChanged += _ => { changed++; if (activeReceipt is not null) Invoke(activeReceipt, "claim_selected_items"); };
            panel = Present(success, Rewards((Stone, 2), (Book, 2)), reward => callbackEntries = reward.Count);
            activeReceipt = panel;
            Invoke(panel, "claim_selected_items");
            Check(success.get_inventory_total_count(Stone) == 2 && success.get_inventory_total_count(Book) == 2, "成功领取数量不守恒");
            Check(success.get_inventory_occupied_cell_count() == 14 && success.get_spatial_inventory().Count == 4, "不堆叠占格错误");
            Check(changed == 1 && notices == 4 && callbackEntries == 2, "原子领取通知或回调数量错误");
            Invoke(panel, "claim_selected_items");
            Check(changed == 1 && success.get_inventory_total_count(Book) == 2, "提交重入或关闭后重复点击导致重复领取");
            await Clear(panel);

            var stale = Player(); panel = Present(stale, Rewards((Book, 1)), _ => claimed++);
            Check(stale.add_item_to_inventory(Stone, 120), "动态变化样本失败"); before = Stamp(stale); Invoke(panel, "claim_selected_items");
            Check(Stamp(stale) == before && stale.get_inventory_total_count(Book) == 0 && claimed == 0, "选择后容量变化未重新检查");
            await Clear(panel);

            var fragmented = Player(); Check(fragmented.add_item_to_inventory(Stone, 120) && fragmented.remove_item_from_inventory(Stone, 6), "碎片样本创建失败");
            var occupied = fragmented.get_spatial_inventory();
            var cells = (from r in Character.InventoryRegions from y in Enumerable.Range(0, r.Height) from x in Enumerable.Range(0, r.Width) select (r.Id, X: x, Y: y));
            var free = cells.Where(c => !occupied.Any(e => e.RegionId == c.Id && e.X == c.X && e.Y == c.Y)).ToArray();
            var holes = new[] { ("chest", 0, 0), ("chest", 3, 2), ("sleeve-left", 0, 0), ("sleeve-right", 0, 0), ("waist", 0, 0), ("back", 0, 0) };
            for (var i = 0; i < holes.Length; i++)
            {
                var e = fragmented.get_spatial_inventory().First(e => e.RegionId == holes[i].Item1 && e.X == holes[i].Item2 && e.Y == holes[i].Item3);
                Check(fragmented.move_spatial_item(e.InstanceId, free[i].Id, free[i].X, free[i].Y, 0), "无法构造分散空位");
            }
            Check(fragmented.get_inventory_free_slot_count() == 6, "碎片空位面积错误");
            panel = Present(fragmented, Rewards((Book, 1))); before = Stamp(fragmented); Invoke(panel, "claim_selected_items");
            Check(Stamp(fragmented) == before && fragmented.get_inventory_total_count(Book) == 0, "面积足够却形状不符时错误领取");
            Check(panel.FindChildren("ClaimFooterStatus", "Label", true, false).OfType<Label>().Last().Text.Contains("空位无法放下"), "碎片空位反馈错误");
            await Clear(panel);

            var organizer = Player(); Check(organizer.add_item_to_inventory(Iron) && organizer.add_item_to_inventory(Stone, 111), "整理样本创建失败");
            panel = Present(organizer, Rewards((Book, 1))); Invoke(panel, "open_organizer"); before = Stamp(organizer);
            var source = panel.FindChild("PackSourceList", true, false)!;
            var ironCard = source.GetChildren().OfType<Control>().First(c => c.GetNodeOrNull<Label>("ItemCaption")?.Text == "玄铁");
            ironCard.GetNode<Button>("ItemHitTarget").EmitSignal(BaseButton.SignalName.Pressed);
            Check(Stamp(organizer) == before, "待丢弃预览提前移除了物品");
            Check(panel.FindChildren("*", "Label", true, false).OfType<Label>().Any(l => l.Text.Contains("待丢弃物品") && l.Text.Contains("9 格")), "整理未显示实际9格");
            Check((bool)Invoke(panel, "can_claim_after_discard", Field<object>(panel, "_items"))!, "丢弃9格后应可摆入书册");
            Check(Stamp(organizer) == before, "假想整理预检修改了实际人物");
            Invoke(panel, "discard_and_return_to_claim"); Invoke(panel, "claim_selected_items");
            Check(organizer.get_inventory_total_count(Iron) == 0 && organizer.get_inventory_total_count(Stone) == 111 && organizer.get_inventory_total_count(Book) == 1, "确认整理和领取数量不守恒");
            Check(organizer.get_inventory_occupied_cell_count() == 117, "确认整理后占格错误");
            await Clear(panel);
            foreach (var p in _players) p.Free();
            GD.Print($"ITEM_CLAIM_SPATIAL_NATIVE_OK {_checks}"); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Paused = false; foreach (var p in _players) p.Free(); GetTree().Quit(1); }
    }
}
