using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

/// <summary>现行藏书目录与领取提交时结算。选书不授课，只有实物入包才消费免费机会或贡献。</summary>
public static class YuxuManualCopy
{
    public const int SubsequentCopyTestCost = 1;
    public sealed record Entry(GongfaResource Book, ItemData Item);
    private static readonly string[] Preferred = ["玄枢含章经", "太和感物养元宝册", "舒卷天章", "百骸衍枝化生宝册", "斫基损契玄经"];
    public static List<Entry> Catalog()
    {
        var entries = new List<Entry>();
        foreach (var book in GongfaAvailability.EnabledBooks())
        {
            var path = $"res://resources/entities/items/{book.gongfa_name}秘籍.tres";
            if (!ResourceLoader.Exists(path) || GD.Load<ItemData>(path) is not { } item ||
                item.gongfa?.ResourcePath != book.ResourcePath || !GongfaAvailability.IsItemEnabled(item)) continue;
            entries.Add(new(book, item));
        }
        return entries.OrderBy(e => Array.IndexOf(Preferred, e.Book.gongfa_name) is var i && i >= 0 ? i : int.MaxValue)
            .ThenBy(e => e.Book.ResourcePath, StringComparer.Ordinal).ToList();
    }
    public static int Cost(HumanData human) => string.IsNullOrWhiteSpace(human.sect_library_first_manual_path) ? 0 : SubsequentCopyTestCost;
    public sealed class Request
    {
        private readonly Character _reader;
        private readonly string _when, _transactionId = $"yuxu:manual-copy:{Guid.NewGuid():N}";
        public Entry Manual { get; }
        public int QuotedCost { get; }
        public bool Settled { get; private set; }
        internal Request(Character reader, Entry manual, string when) { _reader = reader; Manual = manual; QuotedCost = Cost((HumanData)reader.entity_data!); _when = when; }
        public GArray Rewards() => new() { new GDictionary { ["item_path"] = Manual.Item.ResourcePath, ["count"] = 1, ["source"] = "玄览峰拓印" } };
        /// <summary>只能从已校验好库存布局的提交钩子调用；返回错误时不改费用、首本和库存。</summary>
        public string? BeforeCommit(GArray claimed)
        {
            if (Settled) return "本次拓印已经结算。";
            var count = 0;
            foreach (var value in claimed)
            {
                if (value.VariantType != Variant.Type.Dictionary) continue;
                var entry = value.AsGodotDictionary();
                if (entry.GetValueOrDefault("item_path", "").AsString() != Manual.Item.ResourcePath) return "领取物品与本次拓印不符。";
                count += entry.GetValueOrDefault("count", 0).AsInt32();
            }
            if (count == 0) { Settled = true; return null; }
            if (count != 1 || _reader.entity_data is not HumanData human || human.sect_affiliation_id != "yuxu_xianzong")
                return "当前无法完成这次拓印，请放弃本册后重新办理。";
            if (!GongfaAvailability.IsEnabled(Manual.Book) || !GongfaAvailability.IsItemEnabled(Manual.Item))
                return "这部秘籍当前未开放，物品与贡献均未提交。";
            if (Cost(human) != QuotedCost) return "首本免费状态已变化，请放弃本册后重新确认价格。";
            if (human.get_yuxu_contribution_total() < QuotedCost) return $"贡献不足；需要 {QuotedCost} 点，当前 {Math.Max(0, human.get_yuxu_contribution_total())} 点。";
            if (QuotedCost > 0)
            {
                var record = new YuxuSectContributionRecord
                {
                    record_id = _transactionId, source_transaction_id = _transactionId,
                    transaction_kind = YuxuSectContributionRecord.KindExchangeSpend,
                    source_title = $"玄览峰拓印·{Manual.Book.gongfa_name}", occurred_at = _when,
                    contribution_delta = -QuotedCost, cost_summary = $"支出 {QuotedCost} 点贡献。",
                    result_summary = $"{Manual.Item.entity_name} ×1 已放入行囊。"
                };
                if (!human.record_yuxu_contribution(record)) return "拓印贡献流水未能登记，本次领取未提交。";
            }
            else human.sect_library_first_manual_path = Manual.Book.ResourcePath;
            Settled = true;
            return null;
        }
    }
    public static bool TryPrepare(Character? reader, string bookPath, string when, out Request? request, out string error)
    {
        request = null; error = "";
        if (reader?.entity_data is not HumanData human || human.sect_affiliation_id != "yuxu_xianzong")
        { error = "只有玉虚仙宗弟子可以在此拓印。"; return false; }
        var entry = Catalog().FirstOrDefault(e => e.Book.ResourcePath == bookPath);
        if (entry is null) { error = "这部秘籍当前不在藏经阁目录。"; return false; }
        var cost = Cost(human);
        if (human.get_yuxu_contribution_total() < cost) { error = $"贡献不足；需要 {cost} 点，当前 {Math.Max(0, human.get_yuxu_contribution_total())} 点。"; return false; }
        request = new(reader, entry, string.IsNullOrWhiteSpace(when) ? "游戏时间未载" : when);
        return true;
    }
}
