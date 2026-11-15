using Godot;

/// <summary>验证善功堂目录、分类分页、数量及兑换反馈，不写玩家存档。</summary>
public partial class YuxuExchangeNativeSmoke : Control
{
    private int _checks;
    private void Check(bool success, string reason) { _checks++; if (!success) throw new InvalidOperationException(reason); }
    public override void _Ready() => CallDeferred(nameof(Run));
    private async Task Settle(int count = 6)
    {
        for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
    private static T Field<T>(object owner, string name) =>
        (T)owner.GetType().GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(owner)!;
    private async Task Click(Control control)
    {
        var point = control.GetGlobalTransformWithCanvas() * (control.Size * .5f);
        GetViewport().PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point }, true);
        foreach (bool pressed in new[] { true, false })
            GetViewport().PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = pressed, Position = point, GlobalPosition = point }, true);
        await Settle();
    }
    private async Task Wheel(Control control, MouseButton direction, bool shift = false)
    {
        var point = control.GetGlobalTransformWithCanvas() * (control.Size * .5f);
        GetViewport().PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point }, true);
        foreach (bool pressed in new[] { true, false })
            GetViewport().PushInput(new InputEventMouseButton { ButtonIndex = direction, Pressed = pressed, ShiftPressed = shift, Position = point, GlobalPosition = point }, true);
        await Settle();
    }

    private async void Run()
    {
        try
        {
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            GetWindow().Size = new(1920, 1080);
            var items = YuxuExchangeCatalog.LoadItems();
            Check(items.Count > 50, "现行目录未载入");
            Check(items.Count(i => i is PillItemData) == 5, "回春丹五档缺失或恢复暂停丹药");
            Check(!YuxuExchangeCatalog.Categories.Contains("装备") && items.All(i => i is not PortraitClothingData && YuxuExchangeCatalog.Category(i) != "装备"),
                "装备分类或衣着仍在善功堂");
            Check(items.All(i => i is not PillContainerItemData && YuxuExchangeCatalog.Price(i) >= 0), "模板或无价品进入兑换目录");
            Check(YuxuExchangeCatalog.Categories.Length == 7 && !YuxuExchangeCatalog.Categories.Contains("秘籍") && items.All(i => i.gongfa is null),
                "分类不一致或秘籍仍在善功堂");
            var pill = items.Single(i => i.definition_id.ToString() == "alchemy_huichun_9_1");
            Check(YuxuExchangeCatalog.Price(pill) == 8, "贡献表良效回春价不一致");
            var material = items.First(i => YuxuExchangeCatalog.Category(i) == "材料");
            var artifact = items.First(i => i is SectArtifactOfferData);
            var human = new HumanData { entity_name = "批量兑换验证", sect_affiliation_id = "yuxu_xianzong" };
            Check(human.record_yuxu_contribution(new YuxuSectContributionRecord
            {
                record_id = "fixture", source_transaction_id = "fixture",
                transaction_kind = YuxuSectContributionRecord.KindOfficialDutyReward, source_title = "验证",
                occurred_at = "1年1月1日", contribution_delta = 100, cost_summary = "验证", result_summary = "验证"
            }), "测试贡献准备失败");
            var actor = new Character { entity_data = human };
            AddChild(actor);
            Godot.Collections.Dictionary Quote(ItemData item, int count, int? price = null) => new()
            {
                ["item_path"] = item.ResourcePath, ["count"] = count, ["unit_price"] = price ?? YuxuExchangeCatalog.Price(item)
            };
            int initialPills = actor.get_inventory_total_count(pill.ResourcePath);
            int initialArtifacts = actor.get_artifact_items().Count;
            var invalid = new Godot.Collections.Array { Quote(pill, 2, -99), Quote(artifact, 1) };
            Check(!YuxuExchangeBatchPurchase.Purchase(actor, invalid, "1年1月1日").Success &&
                human.get_yuxu_contribution_total() == 100 && actor.get_inventory_total_count(pill.ResourcePath) == initialPills &&
                actor.get_artifact_items().Count == initialArtifacts, "过期报价造成部分扣费或入包");
            var transaction = new Godot.Collections.Array { Quote(pill, 2), Quote(artifact, 1) };
            var purchased = YuxuExchangeBatchPurchase.Purchase(actor, transaction, "1年1月1日");
            int batchCost = YuxuExchangeCatalog.Price(pill) * 2 + YuxuExchangeCatalog.Price(artifact);
            Check(purchased.Success && purchased.Balance == 100 - batchCost &&
                actor.get_inventory_total_count(pill.ResourcePath) == initialPills + 2 &&
                actor.get_artifact_items().Count == initialArtifacts + 1, "批量兑换未正确登记贡献与物品");
            Check(actor.add_item_to_inventory("res://resources/entities/items/灵石.tres", actor.get_inventory_free_capacity()), "满包准备失败");
            string original = Character.claim_inventory_stamp(actor.get_spatial_inventory());
            long balance = human.get_yuxu_contribution_total();
            Check(!YuxuExchangeBatchPurchase.Purchase(actor, transaction, "1年1月1日").Success &&
                human.get_yuxu_contribution_total() == balance &&
                Character.claim_inventory_stamp(actor.get_spatial_inventory()) == original, "容量不足造成部分扣费或入包");

            var panel = new YuxuSectExchangePanel();
            AddChild(panel);
            panel.open_panel(100);
            await Settle();
            Check(panel.IsVisibleInTree(), "目录未显示");
            var columns = panel.FindChild("CategoryColumns", true, false) as Control;
            Check(columns?.GetChildCount() == 3 && columns.GetChildren().OfType<Control>().All(c => Mathf.IsEqualApprox(c.Size.X, 500)),
                "未同屏显示三列");
            Check(panel.FindChildren("CategoryHeading", "Panel", true, false).Count == 3, "分类题签缺失");
            Check(panel.FindChildren("ItemIcon", "TextureRect", true, false).Count == 0, "文字行仍有图标");
            Check(panel.FindChildren("ItemScroll_*", "ScrollContainer", true, false).Count == 3, "列内独立滚动缺失");
            Check(panel.FindChild("PavilionFrame", true, false) is TextureRect { Texture: not null }, "室内木框未载入");
            var previous = Field<Button>(panel, "_previous");
            var next = Field<Button>(panel, "_next");
            var page = Field<Label>(panel, "_pageLabel");
            Check(previous.Disabled && !next.Disabled && page.Text == "1 / 3", "首页翻页状态错误");

            var materialScroll = panel.FindChild("ItemScroll_材料", true, false) as ScrollContainer;
            var pillScroll = panel.FindChild("ItemScroll_丹药", true, false) as ScrollContainer;
            Check(materialScroll is not null && pillScroll is not null, "目录滚动区域缺失");
            var materialRow = panel.FindChild("Item_" + material.definition_id, true, false) as Button;
            Check(materialRow is not null, "材料行缺失");
            await Wheel(materialRow!, MouseButton.WheelDown);
            Check(materialScroll!.ScrollVertical > 0 && pillScroll!.ScrollVertical == 0 && page.Text == "1 / 3", "普通滚轮没有只滚动当前分类");
            await Wheel(materialRow!, MouseButton.WheelRight);
            Check(page.Text == "1 / 3", "横向滚轮触发了翻页");
            materialScroll.ScrollVertical = 0;
            await Settle();
            await Click(materialRow!);
            var quantity = materialRow!.GetParent().GetChildren().OfType<Control>().Single(c => c.FindChild("SelectedQuantity", true, false) is not null);
            var plus = quantity.GetChildren().OfType<Button>().Single(b => b.Text == "+");
            var minus = quantity.GetChildren().OfType<Button>().Single(b => b.Text == "−");
            await Click(plus);
            Check(Field<Label>(panel, "_totalCount").Text == "共计 2 件", "增加数量没有更新合计");
            await Click(minus);
            var input = (LineEdit)quantity.FindChild("SelectedQuantity", true, false);
            input.Text = "2";
            input.EmitSignal(LineEdit.SignalName.TextChanged, "2");
            await Settle();
            var pillRow = panel.FindChild("Item_" + pill.definition_id, true, false) as Button;
            Check(pillRow is not null, "丹药原生行缺失");
            await Click(pillRow!);
            int expected = 2 * YuxuExchangeCatalog.Price(material) + YuxuExchangeCatalog.Price(pill);
            Check(Field<Label>(panel, "_totalCount").Text == "共计 3 件" &&
                Field<Label>(panel, "_totalCost").Text == $"共计 {expected} 贡献" &&
                Field<Label>(panel, "_remaining").Text == $"预计剩余贡献 {100 - expected}", "选中数量、报价或剩余贡献不同步");

            await Click(next);
            Check(page.Text == "2 / 3" && panel.FindChildren("CategoryHeading", "Panel", true, false).Count == 3,
                "翻页没有整页切换或分类题签缺失");
            await Click(next);
            Check(page.Text == "3 / 3" && next.Disabled, "末页没有正确禁用向后翻页");
            await Click(previous);
            await Click(previous);
            Check(page.Text == "1 / 3" && previous.Disabled && Field<Label>(panel, "_totalCount").Text == "共计 3 件", "返回首页或跨页选择错误");
            var search = Field<LineEdit>(panel, "_search");
            search.Text = "回春丹";
            search.EmitSignal(LineEdit.SignalName.TextChanged, search.Text);
            await Settle();
            Check(panel.FindChildren("Item_*", "Button", true, false).Count == 5 && page.Text == "1 / 3", "搜索结果或页码错误");
            search.Text = "";
            search.EmitSignal(LineEdit.SignalName.TextChanged, search.Text);
            await Settle();

            int requested = 0;
            panel.batch_purchase_requested += selection =>
            {
                requested++;
                var quoted = selection.Select(value => value.AsGodotDictionary()).ToList();
                Check(quoted.Count == 2 && quoted.Any(entry => entry["item_path"].AsString() == pill.ResourcePath && entry["count"].AsInt32() == 1 && entry["unit_price"].AsInt32() == 8) &&
                    quoted.Any(entry => entry["item_path"].AsString() == material.ResourcePath && entry["count"].AsInt32() == 2), "批量兑换事件与选中商品不一致");
                panel.resolve_purchase(true, 100 - expected, "兑换成功");
            };
            var confirm = Field<Button>(panel, "_confirm");
            await Click(confirm);
            Check(requested == 1 && confirm.Disabled && Field<Label>(panel, "_balanceLabel").Text.Contains((100 - expected).ToString()), "兑换结果或余额未同步");
            confirm.EmitSignal(BaseButton.SignalName.Pressed);
            Check(requested == 1, "重复信号触发二次购买");

            panel.set_contribution_balance(0);
            materialRow = panel.FindChild("Item_" + material.definition_id, true, false) as Button;
            await Click(materialRow!);
            Check(confirm.Disabled && Field<Label>(panel, "_message").Text == "贡献不足", "贡献不足仍可确认或没有提示");
            await Click(Field<Button>(panel, "_cancel"));
            Check(Field<Label>(panel, "_totalCount").Text == "共计 0 件" && Field<Label>(panel, "_balanceLabel").Text.Contains("0"), "取消未清空选择或隐藏可用贡献");
            GetWindow().Size = new(1280, 720);
            await Settle();
            Check(panel.FindChild("PavilionCanvas", true, false) is Control canvas && canvas.GetGlobalRect().Size.X <= GetViewport().GetVisibleRect().Size.X + 1,
                "窄窗口框架溢出");
            int returns = 0;
            panel.back_requested += () => returns++;
            panel.handle_back();
            Check(returns == 1, "返回没有发出请求");
            GD.Print($"YUXU_EXCHANGE_NATIVE_OK: {_checks} checks; catalog={items.Count}; three-column pages, independent scrolling, multi-selection, prices and confirmation");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}

