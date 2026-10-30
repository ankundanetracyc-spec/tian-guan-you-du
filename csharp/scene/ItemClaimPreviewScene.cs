using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>领取页美术预览：使用正式奖励面板与物品资源，不接触玩家存档。</summary>
public partial class ItemClaimPreviewScene : Control
{
    private const string CaptureDirectory = "res://docs/美术规范/交易列表与物品占格悬停_20261029";
    private int _checks;

    public override async void _Ready()
    {
        try { await run_preview(); }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Paused = false;
            GetTree().Quit(1);
        }
    }

    private async System.Threading.Tasks.Task run_preview()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        add_backdrop();

        var paths = collect_item_paths();
        if (paths.Count < 32)
        {
            GD.PushError($"领取页预览资源不足：只找到 {paths.Count} 项启用物品。");
            GetTree().Quit(1);
            return;
        }

        var pillRewards = new GArray
        {
            new GDictionary { ["item_path"] = "res://resources/entities/items/alchemy/huichun_2.tres", ["count"] = 10000, ["source"] = "丹房所得" },
            new GDictionary { ["item_path"] = "res://resources/entities/items/alchemy/huichun_2.tres", ["count"] = 2345, ["source"] = "游历所得" },
            new GDictionary { ["item_path"] = "res://resources/entities/items/alchemy/huichun_1.tres", ["count"] = 37, ["source"] = "丹房所得" }
        };
        var pillRecipient = make_recipient(paths.Take(0));
        var claimedEntries = new GArray();
        var abandonedEntries = new GArray();
        var pillPanel = present(pillRecipient, pillRewards, entries => claimedEntries = entries, entries => abandonedEntries = entries);
        await capture("领取列表_数量与来源");
        var pillGrid = pillPanel.GetNode<GridContainer>("ReceiptCanvas/ReceiptPage/ClaimItemsScroll/ClaimItemGrid");
        check(pillGrid.Columns == 1 && pillGrid.GetChildCount() == 2, "重复物品按现有规则归组为单列列表");
        var pillCard = pillGrid.GetChild<Control>(0);
        var caption = pillCard.GetNode<Label>("ItemCaption");
        check(!caption.Text.Contains("12345", StringComparison.Ordinal) && caption.Text.Contains("回春丹", StringComparison.Ordinal), "名称与数量分列");
        check(pillCard.GetNode<Label>("ItemQuantity").Text == "× 12,345", "大量物品数量完整显示");
        check(pillCard.GetNode<Label>("ItemIdentity").Text.Contains("丹房所得", StringComparison.Ordinal)
            && pillCard.GetNode<Label>("ItemIdentity").Text.Contains("游历所得", StringComparison.Ordinal), "归组保留全部来源");
        check(pillCard.GetNode<TextureRect>("ItemIcon").Material is null, "瓷瓶使用本色而非药丸着色");
        pillCard.GetNode<Button>("ItemHitTarget").EmitSignal(Button.SignalName.Pressed);
        check(pillRecipient.get_inventory_slot_count() == 0 && claim_list(pillPanel).GetChild<Control>(0).GetNode<Label>("ItemSelectionMark").Text == "", "取消勾选不写库存");
        pillPanel.GetNode<Button>("ReceiptCanvas/ReceiptPage/ClaimSelectAll").EmitSignal(Button.SignalName.Pressed);
        check(claim_list(pillPanel).GetChildren().OfType<Control>().All(row => row.GetNode<Label>("ItemSelectionMark").Text == "✓"), "全选恢复各行勾选");
        pillPanel.GetNode<Button>("ReceiptCanvas/ReceiptPage/ClaimSelectAll").EmitSignal(Button.SignalName.Pressed);
        check(pillPanel.GetNode<Button>("ReceiptCanvas/ReceiptPage/ClaimFooterPrimary").Disabled, "取消全选后禁止空领取");
        pillPanel.GetNode<Button>("ReceiptCanvas/ReceiptPage/ClaimSelectAll").EmitSignal(Button.SignalName.Pressed);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        pillCard = claim_list(pillPanel).GetChild<Control>(0);
        var pointer = pillCard.GetNode<Button>("ItemHitTarget").GetGlobalRect().GetCenter();
        Input.WarpMouse(pointer);
        GetViewport().PushInput(new InputEventMouseMotion { Position = pointer, GlobalPosition = pointer }, true);
        await ToSignal(GetTree().CreateTimer(0.35), SceneTreeTimer.SignalName.Timeout);
        GD.Print($"ITEM_CLAIM_HOVER pointer={pointer} hovered={GetViewport().GuiGetHoveredControl()?.GetPath()}");
        await capture("领取列表_物品悬停");
        check(GetNode<Control>("/root/HoverInfoManager/SharedHoverInfoCard").Visible, "真实鼠标悬停显示共用详情");
        pillPanel.GetNode<Button>("ReceiptCanvas/ReceiptPage/ClaimFooterPrimary").EmitSignal(Button.SignalName.Pressed);
        check(pillRecipient.get_inventory_total_count("res://resources/entities/items/alchemy/huichun_2.tres") == 12345
            && pillRecipient.get_inventory_total_count("res://resources/entities/items/alchemy/huichun_1.tres") == 37, "确认领取后数量守恒");
        check(claimedEntries.Count == 3 && abandonedEntries.Count == 0
            && claimedEntries.Select(entry => entry.AsGodotDictionary()).Sum(entry => entry["count"].AsInt32()) == 12382, "领取回调保留原始三份来源及数量");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        var firstRecipient = make_recipient(paths.Take(0));
        var compactPaths = new[]
        {
            "res://resources/entities/items/灵石.tres", "res://resources/entities/items/兽骨.tres",
            "res://resources/entities/items/兽皮.tres", "res://resources/entities/items/云雾菌.tres",
            "res://resources/entities/items/铜髓矿.tres", "res://resources/entities/items/凝霜叶.tres",
            "res://resources/entities/items/雾心菌核.tres", "res://resources/entities/items/石蜂蜡.tres",
            "res://resources/entities/items/月露苔.tres", "res://resources/entities/items/回泉珠.tres",
            "res://resources/entities/items/玄铁.tres", "res://resources/entities/items/赤纹燧石.tres",
            "res://resources/entities/items/砂脉石片.tres", "res://resources/entities/items/松心炭.tres",
            "res://resources/entities/items/灵泉露.tres", "res://resources/entities/items/山蚕长丝.tres",
            "res://resources/entities/items/赤鳞蜕.tres", "res://resources/entities/items/玄甲薄片.tres",
            "res://resources/entities/items/定脉丹.tres", "res://resources/entities/items/归元定息丹.tres",
            "res://resources/entities/items/alchemy/huichun_3.tres", "res://resources/entities/items/alchemy/jingxin_2.tres",
            "res://resources/entities/items/alchemy/tongmai_1.tres", "res://resources/entities/equipment/布甲.tres"
        }.Where(paths.Contains).ToList();
        var compactRewards = rewards_for(compactPaths, includeLargeStoneStack: true);
        var compactPanel = present(firstRecipient, compactRewards);
        await capture("领取列表_混合物品");
        compactPanel.QueueFree();
        GetTree().Paused = false;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        var scrollRecipient = make_recipient(paths.Take(0));
        var scrollPanel = present(scrollRecipient, rewards_for(paths, includeLargeStoneStack: true));
        await capture("领取列表_长列表");
        var longScroll = scrollPanel.GetNode<ScrollContainer>("ReceiptCanvas/ReceiptPage/ClaimItemsScroll");
        check(longScroll.GetVScrollBar().Visible && longScroll.GetVScrollBar().MaxValue > longScroll.Size.Y, "长列表产生实际纵向溢出");
        longScroll.ScrollVertical = 180;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        claim_list(scrollPanel).GetChild<Control>(2).GetNode<Button>("ItemHitTarget").EmitSignal(Button.SignalName.Pressed);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        check(scrollPanel.GetNode<ScrollContainer>("ReceiptCanvas/ReceiptPage/ClaimItemsScroll").ScrollVertical == 180, "勾选重建列表后保留滚动位置");
        scrollPanel.QueueFree();
        GetTree().Paused = false;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        var fullRecipient = make_recipient(paths.Take(Character.InventorySlotCapacity));
        var overflowRewards = rewards_for(paths.Skip(Character.InventorySlotCapacity).Take(16), includeLargeStoneStack: false);
        var organizerPanel = present(fullRecipient, overflowRewards);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        organizerPanel.GetNode<Button>("ReceiptCanvas/ReceiptPage/ClaimFooterPrimary").EmitSignal(Button.SignalName.Pressed);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var originalSlots = fullRecipient.get_inventory_slot_count();
        var originalRows = organize_list(organizerPanel, false).GetChildCount();
        check(organize_list(organizerPanel, false).Columns == 1 && organize_list(organizerPanel, true).Columns == 1, "整理两侧均为单列列表");
        move_first_inventory_item_to_discard(organizerPanel);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        move_first_inventory_item_to_discard(organizerPanel);
        check(organize_list(organizerPanel, false).GetChildCount() == originalRows - 2 && organize_list(organizerPanel, true).GetChildCount() == 2, "移入待弃保持行数守恒");
        check(fullRecipient.get_inventory_slot_count() == originalSlots, "暂存待弃不提前写库存");
        organize_list(organizerPanel, true).GetChild<Control>(0).GetNode<Button>("ItemHitTarget").EmitSignal(Button.SignalName.Pressed);
        check(organize_list(organizerPanel, false).GetChildCount() == originalRows - 1 && organize_list(organizerPanel, true).GetChildCount() == 1, "待弃物品可原样移回");
        await capture("整理行囊_双侧列表");
        organizerPanel.GetNode<Button>("ReceiptCanvas/ReceiptPage/ClaimDiscardConfirm").EmitSignal(Button.SignalName.Pressed);
        check(fullRecipient.get_inventory_slot_count() == originalSlots - 1, "确认后仅移除实际待弃一行");
        check(organizerPanel.GetNodeOrNull<ScrollContainer>("ReceiptCanvas/ReceiptPage/ClaimItemsScroll") is not null, "确认整理返回领取列表");

        GD.Print($"ITEM_CLAIM_LIST_OK checks={_checks} resources={paths.Count}");
        GetTree().Paused = false;
        GetTree().Quit();
    }

    private void add_backdrop()
    {
        var backdrop = new TextureRect
        {
            Name = "InventoryBackdrop",
            Texture = GD.Load<Texture2D>("res://textures/ui/inventory_20261012/backdrop_v1.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(backdrop);
    }

    private ItemClaimPanel present(Character recipient, GArray rewards, Action<GArray>? claimed = null, Action<GArray>? abandoned = null)
    {
        var panel = new ItemClaimPanel { Name = "ItemClaimPreviewPanel" };
        AddChild(panel);
        if (!panel.Present(recipient, rewards, claimed: claimed, abandoned: abandoned))
            throw new System.InvalidOperationException("领取页预览无法打开。");
        return panel;
    }

    private void check(bool valid, string message)
    {
        if (!valid) throw new InvalidOperationException("领取列表检查失败：" + message);
        _checks++;
    }

    private static GridContainer claim_list(ItemClaimPanel panel) => panel.GetNode<GridContainer>("ReceiptCanvas/ReceiptPage/ClaimItemsScroll/ClaimItemGrid");
    private static GridContainer organize_list(ItemClaimPanel panel, bool discard) => panel.GetNode<GridContainer>(
        discard ? "ReceiptCanvas/ReceiptPage/PendingDiscardItemsScroll/PendingDiscardList" : "ReceiptCanvas/ReceiptPage/PackDiscardSourceScroll/PackSourceList");

    private static Character make_recipient(IEnumerable<string> inventoryPaths)
    {
        var data = new HumanData { entity_name = "预览角色" };
        foreach (var path in inventoryPaths)
        {
            data.inventory_item_paths.Add(path);
            data.inventory_item_counts.Add(1);
            data.inventory_item_positions.Add(data.inventory_item_positions.Count);
        }
        return new Character { Name = "PreviewRecipient", entity_data = data };
    }

    private static GArray rewards_for(IEnumerable<string> paths, bool includeLargeStoneStack)
    {
        var rewards = new GArray();
        var selectedPaths = paths.Distinct(StringComparer.Ordinal).ToList();
        const string stonePath = "res://resources/entities/items/灵石.tres";
        if (includeLargeStoneStack && ResourceLoader.Exists(stonePath) && !selectedPaths.Contains(stonePath, StringComparer.Ordinal))
            selectedPaths.Add(stonePath);
        foreach (var path in selectedPaths)
        {
            var item = ResourceLoader.Load<ItemData>(path);
            var count = item is { can_stack: false } ? 1 :
                includeLargeStoneStack && path.EndsWith("/灵石.tres", System.StringComparison.Ordinal) ? 1200 : 1 + rewards.Count % 27;
            rewards.Add(new GDictionary { ["item_path"] = path, ["count"] = count, ["source"] = "游历所得" });
        }
        return rewards;
    }

    private static List<string> collect_item_paths()
    {
        var result = new List<string>();
        collect_item_paths("res://resources/entities/items", result);
        collect_item_paths("res://resources/entities/equipment", result);
        return result.Where(path => ResourceLoader.Load<ItemData>(path) is { } item && GongfaAvailability.IsItemEnabled(item) && AlchemyProductCatalog.IsItemEnabled(item))
            .Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal).ToList();
    }

    private static void collect_item_paths(string directory, List<string> result)
    {
        using var access = DirAccess.Open(directory);
        if (access is null) return;
        foreach (var file in access.GetFiles().Where(file => file.EndsWith(".tres", System.StringComparison.OrdinalIgnoreCase)))
            result.Add($"{directory.TrimEnd('/')}/{file}");
        foreach (var child in access.GetDirectories().OrderBy(value => value, StringComparer.Ordinal))
            collect_item_paths($"{directory.TrimEnd('/')}/{child}", result);
    }

    private static void move_first_inventory_item_to_discard(ItemClaimPanel panel)
    {
        var source = panel.GetNode<ScrollContainer>("ReceiptCanvas/ReceiptPage/PackDiscardSourceScroll");
        var button = find_button(source);
        button?.EmitSignal(Button.SignalName.Pressed);
    }

    private static Button? find_button(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is Button button) return button;
            if (find_button(child) is { } nested) return nested;
        }
        return null;
    }

    private async System.Threading.Tasks.Task capture(string name)
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(CaptureDirectory));
        var path = $"{CaptureDirectory}/{name}.png";
        var error = GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath(path));
        if (error != Error.Ok) GD.PushError($"领取页截图保存失败：{path} ({error})");
        else GD.Print($"ITEM_CLAIM_CAPTURE {ProjectSettings.GlobalizePath(path)}");
    }
}
