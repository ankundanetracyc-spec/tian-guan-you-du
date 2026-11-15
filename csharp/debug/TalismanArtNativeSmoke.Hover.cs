using Godot;
using System;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Service = TalismanCraftingService;

public partial class TalismanArtNativeSmoke
{
    private static Rect2 HoverBounds(Control control)
    {
        var transform = control.GetGlobalTransformWithCanvas();
        var bounds = new Rect2(transform * Vector2.Zero, Vector2.Zero);
        foreach (var point in new[] { new Vector2(control.Size.X, 0), control.Size, new Vector2(0, control.Size.Y) })
            bounds = bounds.Expand(transform * point);
        return bounds;
    }

    private string HoverWorldState()
    {
        var data = (HumanData)_owner.entity_data!;
        return JsonSerializer.Serialize(new
        {
            Inventory = Fingerprint(_owner), Energy = _owner.current_world_stamina,
            Proficiency = data.talisman_proficiency,
            Hours = GetNode<GameTimeMgr>("/root/GameTimeManager").get_precise_game_hours(),
            Records = data.talisman_records.ToArray(), Methods = data.talisman_method_ids.ToArray(),
            Definitions = data.talisman_product_definitions.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray(),
            Frozen = _legacy.Select(product => new { product.Key, product.Item.snapshot_json, product.Item.description }).ToArray()
        });
    }

    private static string HoverPlain(string text) => Regex.Replace(text, @"\s+", "");

    private void CheckCraftHoverContent(TalismanProductItemData item, string name)
    {
        var snapshot = item.Snapshot!;
        var expected = Service.PlayerFields(snapshot);
        var content = StoragePanel.ItemHover(item, 99, "res://private/test-only-item.tres", _owner);
        string body = content.Body;
        Check(expected.Count == 4 && expected.All(field => body.Contains(field.Key + "：" + field.Value)
            && Regex.Matches(body, Regex.Escape(field.Key) + "[：:]").Count == 1),
            name + "共用悬停逐行展示四个真实冻结字段", body);
        string all = content.Title + content.Subtitle + body;
        Check(!Regex.IsMatch(all, @"\b(?:paper|ink|brush)\b") && !all.Contains("res://") && !all.Contains("user://")
            && !all.Contains("未配置") && !all.Contains("来源预算") && !all.Contains("快照") && !all.Contains("数量"),
            name + "正文不泄露内部分类路径预算或旧占位说明", all);
        Check(content.CardWidth == 340 && content.IconSize == 96 && content.InventoryFootprintBelowTitle
            && content.Footprint == item.get_inventory_size() && content.GridDiagrams?.Any(diagram => diagram.Kind == HoverGridKind.Combat) != true,
            name + "使用常规物品栏宽图标和名称下真实行囊格图，不生成战斗底栏");
        Check(content.Icon?.ResourcePath == item.icon_path, name + "悬停使用已有实际物品PNG", content.Icon?.ResourcePath);
    }

    private void CheckCraftHoverModels()
    {
        string before = HoverWorldState();
        foreach (var product in _legacy.Where(product => product.Item.Snapshot?.Kind is "paper" or "ink" or "brush"))
        {
            Check(product.Item.description.Length == 0, "旧物夹具确实没有可借用的description " + product.Key);
            CheckCraftHoverContent(product.Item, product.Key);
        }

        var paper = _legacy.First(product => product.Key == "paper").Item;
        using var stale = new TalismanProductItemData
        {
            entity_name = "旧说明符纸", item_category = "器具", item_subcategory = "paper", icon_path = paper.icon_path,
            snapshot_json = paper.snapshot_json, description = "旧案：承墨量999，过时说明。", inventory_width = 1, inventory_height = 2
        };
        using var legacyLayout = new ItemLayoutPreviewData
        {
            definition_key = "isolated_legacy_paper_layout", board_cells = [Vector2I.Zero]
        };
        stale.layout_preview = legacyLayout;
        CheckCraftHoverContent(stale, "旧描述成品");
        Check(!StoragePanel.ItemHover(stale).Body.Contains("999") && stale.description.Contains("999")
            && stale.snapshot_json == paper.snapshot_json, "显示不借旧description也不改写旧物资料");
        var variation = JsonSerializer.Deserialize<Service.ProductSnapshot>(paper.snapshot_json)!;
        variation.Capacity = .8;
        variation.ElementShares = [new("木", 1), new("水", 1)];
        variation.Elements = ["木", "水"];
        using var other = new TalismanProductItemData
        {
            entity_name = "另一张符纸", item_category = "器具", item_subcategory = "paper", icon_path = paper.icon_path,
            snapshot_json = JsonSerializer.Serialize(variation), inventory_width = 1, inventory_height = 2
        };
        Check(Service.ValidateSnapshot(variation), "不同纸性与实际保留五行的隔离变体仍是合法冻结成品");
        var changed = StoragePanel.ItemHover(other);
        Check(changed.Body.Contains("承墨量：少") && StoragePanel.ItemHover(paper).Body.Contains("承墨量：中")
            && changed.Subtitle.Contains("木") && changed.Subtitle.Contains("水"), "不同成品各读自己的纸性和已保留五行");

        var human = (HumanData)_owner.entity_data!;
        float proficiency = human.talisman_proficiency;
        string initial = StoragePanel.ItemHover(paper, user: _owner).Body;
        human.talisman_proficiency = 900;
        Check(StoragePanel.ItemHover(paper, user: _owner).Body == initial && paper.snapshot_json == _legacy.First(p => p.Key == "paper").SnapshotJson,
            "当前造诣改变不会重算旧纸的冻结属性");
        human.talisman_proficiency = proficiency;
        var mode = PlayerInfoText.Mode;
        try
        {
            PlayerInfoText.SetMode(PlayerInfoText.InformationMode.Simple, persist: false);
            string simple = PlayerInfoText.Display(StoragePanel.ItemHover(paper).Body);
            PlayerInfoText.SetMode(PlayerInfoText.InformationMode.Detailed, persist: false);
            Check(PlayerInfoText.Display(StoragePanel.ItemHover(paper).Body) == simple && !simple.Contains("计算来源"),
                "简略和详细都显示相同成品字段，不追加来源账或计算占位");
        }
        finally { PlayerInfoText.SetMode(mode, persist: false); }

        foreach (string invalid in new[] { "{", "", "{\"Kind\":\"paper\",\"Version\":-1}" })
        {
            using var broken = new TalismanProductItemData { entity_name = "失效成品", item_category = "器具", item_subcategory = "paper", snapshot_json = invalid };
            var info = StoragePanel.ItemHover(broken);
            Check(info.Body.Contains("失效") && !info.Body.Contains("未配置") && broken.snapshot_json == invalid,
                "失效成品明确提示且不退回通用世界使用说明 " + invalid.Length);
        }
        foreach (var product in _legacy.Where(product => product.Item.Snapshot?.Kind == "talisman"))
        {
            var content = StoragePanel.ItemHover(product.Item);
            var combat = TalismanCombatCatalog.Hover(product.Item);
            Check(content.Body == combat.Body && content.Subtitle == combat.Subtitle
                && content.GridDiagrams?.Single().Kind == HoverGridKind.Combat,
                "固定战斗符继续使用原有效果费用和战斗占格 " + product.Key);
        }
        var material = TalismanCraftingCatalog.DiscoverMaterials().First().Item;
        Check(StoragePanel.ItemHover(material).Body == MaterialItemPresentation.Hover(material).Body,
            "普通原料继续使用既有材性悬停分派");
        Check(HoverWorldState() == before, "成品信息生成及模式切换不改变物品能力库存造诣用度和历法");
    }

    private void HoverPointer(Vector2 point, bool click = false)
    {
        GetViewport().PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point }, true);
        if (!click) return;
        foreach (bool pressed in new[] { true, false }) GetViewport().PushInput(new InputEventMouseButton
            { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = pressed }, true);
    }

    private async Task<PanelContainer> HoverActual(Control anchor, TalismanProductItemData item, string name)
    {
        HoverInfoManager.HideAll(); HoverPointer(new Vector2(3, 3)); await Frames(2);
        for (Node? parent = anchor.GetParent(); parent is not null; parent = parent.GetParent())
            if (parent is ScrollContainer scroll) { scroll.EnsureControlVisible(anchor); await Frames(2); }
        var point = HoverBounds(anchor).GetCenter();
        Check(anchor.IsVisibleInTree() && GetViewport().GetVisibleRect().Grow(1).Encloses(HoverBounds(anchor)), name + "真实悬停来源在窗口内");
        var card = GetTree().Root.GetChildren().OfType<HoverInfoManager>().Single().GetNode<PanelContainer>("SharedHoverInfoCard");
        ulong deadline = Time.GetTicksMsec() + 3000;
        while (Time.GetTicksMsec() < deadline)
        {
            HoverPointer(point); await Frames(2);
            if (card.IsVisibleInTree() && Descendants(card).OfType<Label>().Any(label => label.IsVisibleInTree() && label.Text == item.entity_name)) break;
        }
        Check(card.IsVisibleInTree() && Descendants(card).OfType<Label>().Any(label => label.IsVisibleInTree() && label.Text == item.entity_name),
            name + "真实指针触发正确成品信息栏");
        string body = string.Join("\n", Descendants(card).OfType<RichTextLabel>().Where(label => label.IsVisibleInTree()).Select(label => label.GetParsedText()));
        Check(HoverPlain(body) == HoverPlain(StoragePanel.ItemHover(item).Body), name + "屏幕正文完整呈现共用冻结成品信息一次", body);
        Check(GetViewport().GetVisibleRect().Grow(1).Encloses(HoverBounds(card)), name + "完整信息栏位于窗口内", HoverBounds(card));
        Check(card.MouseFilter == Control.MouseFilterEnum.Ignore && Descendants(card).OfType<Control>().All(control => control.MouseFilter == Control.MouseFilterEnum.Ignore),
            name + "信息栏和全部子控件透传鼠标");
        var header = card.FindChild("HeaderInventoryFootprint", true, false) as InventoryFootprintView;
        var title = Descendants(card).OfType<Label>().Single(label => label.IsVisibleInTree() && label.Text == item.entity_name);
        Check(header is { Visible: true } && header.Footprint == item.get_inventory_size()
            && HoverBounds(header).Position.Y >= HoverBounds(title).End.Y,
            name + "实际行囊格图位于名称下方");
        Check(!Descendants(card).OfType<HoverFootprintFooter>().Any(footer => footer.IsVisibleInTree())
            && !Descendants(card).OfType<Label>().Any(label => label.IsVisibleInTree() && label.Text == "战斗占格："),
            name + "纸墨笔不留下战斗底栏或空标签");
        Check(Descendants(card).OfType<TextureRect>().Any(icon => icon.IsVisibleInTree()
            && icon.Texture?.ResourcePath == item.icon_path && icon.Size.IsEqualApprox(new Vector2(96, 96))),
            name + "实际信息栏保持96像素PNG主图");
        return card;
    }

    private async Task CaptureHover(string state)
    {
        if (_headless) return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage();
        string name = $"talisman_art_{state}_{_width}.png";
        Check(image.SavePng(System.IO.Path.Combine(_output, name)) == Error.Ok, "保存实际悬停图 " + state);
        _captures.Add(name);
    }

    private async Task CheckInventoryCraftHovers()
    {
        CheckCraftHoverModels();
        string before = HoverWorldState();
        foreach (string key in new[] { "paper", "ink", "qingzhu", "jinghao", "cangfeng" })
        {
            var product = _legacy.Single(product => product.Key == key);
            var slot = Descendants(_inventory).OfType<StorageItemSlot>().First(slot => slot.item_path == product.Path);
            await HoverActual(slot, product.Item, "行囊 " + key);
            await CaptureHover("inventory_hover_" + key);
        }
        HoverInfoManager.HideAll();
        Check(HoverWorldState() == before, "实际行囊连续悬停不改变库存身份冻结数据造诣精力和历法");
    }

    private async Task ClickHoverChoice(Button button)
    {
        Check(button.IsVisibleInTree() && !button.Disabled, "实际选择按钮可用 " + button.Name);
        HoverPointer(HoverBounds(button).GetCenter(), click: true); await Frames();
    }

    private async Task CheckCraftPickerHovers()
    {
        string before = HoverWorldState();
        await ClickHoverChoice((Button)_room.FindChild("CraftTab_drawing", true, false));
        foreach (string kind in new[] { "paper", "ink", "brush" })
        {
            var product = _legacy.First(product => product.Item.Snapshot?.Kind == kind);
            await ClickHoverChoice((Button)_room.FindChild("ChooseProduct_" + kind, true, false));
            var modal = _room.FindChild("CraftingModal", true, false)!;
            var source = (Control)modal.FindChild("ProductInfo_" + product.Item.definition_id, true, false);
            Check(source is not null, "选择卡确实注册成品信息来源 " + kind);
            await HoverActual(source!, product.Item, "制符选择 " + kind);
            await CaptureHover("picker_hover_" + kind);
            await ClickHoverChoice((Button)modal.FindChild("PickProduct_" + product.Item.definition_id, true, false));
            var request = _room.CurrentRequest();
            Check(kind switch { "paper" => request.PaperPath == product.Path, "ink" => request.InkSelections.Any(ink => ink.Path == product.Path),
                _ => request.BrushPath == product.Path }, "信息栏显示期间真实选择点击仍生效 " + kind);
            if (kind == "ink") await ClickHoverChoice((Button)_room.FindChild("FinishProductSelection", true, false));
        }
        HoverInfoManager.HideAll();
        Check(HoverWorldState() == before, "查看和选择纸墨笔只改界面草稿，不扣料不推进历法或改写冻结能力");
    }
}
