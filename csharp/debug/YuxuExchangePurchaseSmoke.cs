using Godot;

/// <summary>Calls the same redemption service used by MainScene; does not load or save a journey.</summary>
public partial class YuxuExchangePurchaseSmoke : Node
{
    private int _checks;
    private const string Pill = "res://resources/entities/items/alchemy/huichun_1.tres";
    private const string Stone = "res://resources/entities/items/灵石.tres";
    private const string Qinghe = "res://resources/entities/equipment/清和剑.tres";
    private const string Date = "1年1月1日";
    private void Check(bool value, string reason) { _checks++; if (!value) throw new InvalidOperationException(reason); }
    private static Character Member(int balance)
    {
        var human = new HumanData { entity_name = "善功堂实际兑换测试", sect_affiliation_id = "yuxu_xianzong" };
        if (balance > 0) human.record_yuxu_contribution(new YuxuSectContributionRecord
        {
            record_id = Guid.NewGuid().ToString("N"), source_transaction_id = "test:" + Guid.NewGuid().ToString("N"),
            transaction_kind = YuxuSectContributionRecord.KindOfficialDutyReward, source_title = "测试初始贡献",
            occurred_at = Date, contribution_delta = balance, cost_summary = "隔离测试", result_summary = "隔离测试"
        });
        return new Character { entity_data = human };
    }
    private static string Fingerprint(Character p) => string.Join("|", p.get_spatial_inventory().OrderBy(e => e.InstanceId)
        .Select(e => $"{e.InstanceId}:{e.ItemPath}:{e.ArtifactId}:{e.RegionId}:{e.X},{e.Y}:{e.Width},{e.Height}:{e.Rotation}"));
    public override void _Ready()
    {
        try
        {
            using var paid = Member(100); var human = (HumanData)paid.entity_data!;
            int changes = 0, acquisitions = 0; bool coherent = true;
            paid.InventoryChanged += _ => { changes++; coherent &= human.get_yuxu_contribution_total() == 92 && paid.get_inventory_total_count(Pill) == 1; };
            paid.ItemAcquired += (_, item, count) => { acquisitions += count; coherent &= item.ResourcePath == Pill && human.get_yuxu_contribution_total() == 92; };
            var result = YuxuExchangePurchase.Purchase(paid, Pill, 8, Date);
            Check(result.Success && result.Balance == 92 && paid.get_inventory_total_count(Pill) == 1, "实际回春丹兑换没有扣8得1");
            Check(changes == 1 && acquisitions == 1 && coherent, "兑换通知存在半提交或重复发出");
            Check(human.sect_contribution_records.Count == 2 && human.sect_contribution_records.Last().contribution_delta == -8, "贡献流水未准确登记");
            Check(human.get_yuxu_official_service_total() == 100, "兑换改变累计履职贡献");
            var original = Fingerprint(paid);
            Check(!YuxuExchangePurchase.Purchase(paid, Pill, 7, Date).Success, "过时报价没有拒绝");
            Check(human.get_yuxu_contribution_total() == 92 && Fingerprint(paid) == original && acquisitions == 1, "过时报价改变余额或物品位置");
            Check(!YuxuExchangePurchase.Purchase(paid, "res://resources/entities/items/alchemy/jingxin_1.tres", 8, Date).Success, "暂停丹药重新开放了兑换");
            Check(!YuxuExchangePurchase.Purchase(paid, "res://resources/entities/items/不存在的兑换物品.tres", 8, Date).Success, "不存在商品未拒绝");
            Check(human.get_yuxu_contribution_total() == 92 && Fingerprint(paid) == original && changes == 1, "无效商品产生副作用");

            using var poor = Member(7); string poorBefore = Fingerprint(poor);
            Check(!YuxuExchangePurchase.Purchase(poor, Pill, 8, Date).Success, "贡献不足仍可兑换");
            Check(((HumanData)poor.entity_data!).get_yuxu_contribution_total() == 7 && Fingerprint(poor) == poorBefore, "贡献不足改变库存或余额");
            using var outsider = Member(100); var outsiderData = (HumanData)outsider.entity_data!; outsiderData.sect_affiliation_id = "";
            Check(!YuxuExchangePurchase.Purchase(outsider, Pill, 8, Date).Success && outsiderData.get_yuxu_contribution_total() == 100 && outsider.get_inventory_total_count(Pill) == 0, "非宗门成员仍能兑换");

            using var full = Member(100); var fullData = (HumanData)full.entity_data!;
            Check(full.add_item_to_inventory(Stone, 120), "满包测试准备失败");
            string fullBefore = Fingerprint(full); int fullChanges = 0; full.InventoryChanged += _ => fullChanges++;
            Check(!YuxuExchangePurchase.Purchase(full, Pill, 8, Date).Success, "满120格仍兑换丹药");
            Check(fullData.get_yuxu_contribution_total() == 100 && fullData.sect_contribution_records.Count == 1 && Fingerprint(full) == fullBefore && fullChanges == 0, "满包失败扣费、记流水或改变身份摆位");
            Check(!YuxuExchangePurchase.Purchase(full, Qinghe, 0, Date).Success && Fingerprint(full) == fullBefore && full.get_artifact_items().Count == 0, "满包0价法器被放入或移动原物品");

            using var free = Member(0); var freeData = (HumanData)free.entity_data!;
            result = YuxuExchangePurchase.Purchase(free, Qinghe, 0, Date);
            Check(result.Success && result.Balance == 0 && free.get_artifact_items().Count == 1 && freeData.sect_contribution_records.Count == 0, "0贡献清和剑实际兑换错误");
            var first = free.get_spatial_inventory().Single();
            Check(first.Artifact is not null && ForgedArtifactInventory.TryRead(first.Artifact, out _) && first.Width * first.Height == 12, "清和剑丢失炼器快照或正确占地");
            Check(YuxuExchangePurchase.Purchase(free, Qinghe, 0, Date).Success && free.get_artifact_items().Select(a => a.instance_id).Distinct().Count() == 2, "法器实例没有各自身份");
            Check(free.get_spatial_inventory().First(e => e.InstanceId == first.InstanceId) == first, "第二件0价法器移动了第一件");

            using var aborted = Member(0); int callbackCalls = 0;
            var artifact = GD.Load<SectArtifactOfferData>(Qinghe).CreateArtifact();
            Check(!aborted.try_add_artifact_to_inventory(artifact, () => { callbackCalls++; return false; }), "法器扣费回调拒绝仍然提交");
            Check(callbackCalls == 1 && aborted.get_artifact_items().Count == 0 && aborted.get_inventory_occupied_cell_count() == 0, "法器失败回调留下实例或占格");

            using var clothes = Member(100); var clothing = YuxuExchangeCatalog.LoadItems().OfType<PortraitClothingData>().First();
            result = YuxuExchangePurchase.Purchase(clothes, clothing.ResourcePath, YuxuExchangeCatalog.Price(clothing), Date);
            Check(result.Success && result.Balance == 80 && clothes.get_inventory_total_count(clothing.ResourcePath) == 1 && clothes.get_inventory_occupied_cell_count() == 12, "衣着新增兑换没有使用临时价和真实占格");
            foreach (var character in new[] { paid, poor, outsider, full, free, aborted, clothes }) character.Free();
            GD.Print($"YUXU_EXCHANGE_PURCHASE_OK: {_checks} checks; real MainScene service, contribution ledger, capacity, exact identity preservation, 0-price artifact and clothing");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
