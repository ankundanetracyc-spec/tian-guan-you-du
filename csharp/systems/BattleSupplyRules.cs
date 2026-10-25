using Godot;
using Godot.Collections;

public static class BattleSupplyRules
{
    public const int SlotCount = 3, StackCapacity = 5;
    public static readonly string[] QualityNames = ["薄效", "良效", "优效", "极效", "满效"];
    private static readonly System.Collections.Generic.Dictionary<string, PillItemData> Pills = [];

    // 首批只开放已有正式丹种的战斗用途。静心丹与未定稿消耗型符箓不开放。
    public static PillItemData? Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        if (Pills.TryGetValue(path, out var cached)) return cached;
        if (!ResourceLoader.Exists(path) || GD.Load<Resource>(path) is not PillItemData pill ||
            pill.pill_id is not ("huichun" or "tongmai") || pill.pill_rank != 9 || pill.efficacy_tier is < 0 or > 4) return null;
        return Pills[path] = pill;
    }

    public static int PackCount(Array<string> paths, Array<int> counts, string path)
    {
        long total = 0;
        for (var i = 0; i < Math.Min(paths.Count, counts.Count); i++)
            if (paths[i] == path) total += Math.Max(0, counts[i]);
        return (int)Math.Min(int.MaxValue, total);
    }

    public static int Reserved(Array<BattleSupplySlotData> slots, string path, int except = -1) =>
        slots.Where((slot, index) => index != except && slot is not null && slot.item_path == path)
            .Sum(slot => Math.Clamp(slot.count, 0, StackCapacity));

    public static void Normalize(Array<BattleSupplySlotData> slots, Array<string> paths, Array<int> counts)
    {
        while (slots.Count > SlotCount) slots.RemoveAt(slots.Count - 1);
        while (slots.Count < SlotCount) slots.Add(new());
        var reserved = new System.Collections.Generic.Dictionary<string, int>();
        for (var i = 0; i < SlotCount; i++)
        {
            slots[i] ??= new();
            var slot = slots[i];
            var available = Math.Max(0, PackCount(paths, counts, slot.item_path) - reserved.GetValueOrDefault(slot.item_path));
            slot.count = Load(slot.item_path) is null ? 0 : Math.Clamp(slot.count, 0, Math.Min(StackCapacity, available));
            if (slot.count == 0) slot.item_path = "";
            else reserved[slot.item_path] = reserved.GetValueOrDefault(slot.item_path) + slot.count;
        }
    }

    public static bool Set(BattleEntityData owner, int index, string path, int count)
    {
        Normalize(owner.battle_supplies, owner.pack_item_paths, owner.pack_item_counts);
        if (index is < 0 or >= SlotCount) return false;
        if (count <= 0) { owner.battle_supplies[index] = new(); return true; }
        if (Load(path) is null) return false;
        var available = PackCount(owner.pack_item_paths, owner.pack_item_counts, path) - Reserved(owner.battle_supplies, path, index);
        if (available <= 0) return false;
        owner.battle_supplies[index] = new() { item_path = path, count = Math.Min(Math.Clamp(count, 1, StackCapacity), available) };
        return true;
    }

    public static int Amount(PillItemData pill, int manaCapacity, double toxicity)
    {
        if (pill.pill_id == "huichun") return PillToxicityRules.Absorb(AlchemyProductCatalog.Healing[pill.efficacy_tier], toxicity);
        // 沿现行丹效与低法力口径；不恢复已燃烧的法力上限。
        var nominal = (long)Math.Max(0, manaCapacity) * AlchemyProductCatalog.ManaPercent[pill.efficacy_tier] / 100;
        return nominal <= 0 ? 0 : Math.Max(1, (int)Math.Floor((double)Math.Max(0, manaCapacity) *
            AlchemyProductCatalog.ManaPercent[pill.efficacy_tier] / 100 * PillToxicityRules.Efficiency(toxicity) + 1e-9));
    }

    public static string Name(PillItemData pill) => pill.pill_id == "huichun" ? "回春丹" : "通脉丹";
}
