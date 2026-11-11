using Godot;

/// <summary>首轮候选符效唯一数值入口。战斗只读固定符种和符效档，不读制造者或纸墨原料。</summary>
public static class TalismanCombatCatalog
{
    public const string CandidateVersion = "talisman-combat-candidate-20261109-v1";
    public const int EnergyCost = 1;
    public static readonly string[] TierNames = ["薄效", "良效", "优效", "极效", "满效"];
    private static readonly int[] ArrowDamage = [8, 12, 17, 23, 30];
    private static readonly int[] GuardShield = [6, 9, 13, 18, 24];

    public static bool IsDefined(string recipeId, int tier) => recipeId is "hushen" or "yanshi" && tier is >= 0 and <= 4;
    public static string Name(string recipeId) => recipeId switch { "hushen" => "护身符", "yanshi" => "炎矢符", _ => "符箓" };
    public static string TierName(int tier) => tier is >= 0 and <= 4 ? TierNames[tier] : "未定";
    public static int Amount(string recipeId, int tier) => !IsDefined(recipeId, tier) ? 0 : (recipeId == "hushen" ? GuardShield : ArrowDamage)[tier];
    public static string EffectText(string recipeId, int tier) => !IsDefined(recipeId, tier) ? "此符暂不能施用。"
        : recipeId == "hushen" ? $"自身获得{Amount(recipeId, tier)}点护盾。"
        : $"对一名随机敌人造成{Amount(recipeId, tier)}点远程物理伤害。";
    public static string SkillPath(string recipeId, int tier) => IsDefined(recipeId, tier)
        ? $"res://resources/activeskills/符箓成品/{recipeId}_{tier}.tres" : "";
    public static string IconPath(string recipeId) => TalismanArtCatalog.TalismanIconPath(recipeId);
    public static bool IsSkill(ActiveSkillData? skill) => skill?.combat_source_key.StartsWith("talisman:", StringComparison.Ordinal) == true;

    public static TalismanProductItemData? Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !ResourceLoader.Exists(path)) return null;
        return ResourceLoader.Load(path) is TalismanProductItemData item && IsBattleProduct(item) ? item : null;
    }
    public static bool IsBattleProduct(ItemData item) => item is TalismanProductItemData product
        && product.Snapshot is { Kind: "talisman" } snapshot && TalismanCraftingService.ValidateSnapshot(snapshot)
        && IsDefined(snapshot.RecipeId, snapshot.Tier);
    public static ActiveSkillData? Skill(TalismanProductItemData item) => item.Snapshot is { } snapshot
        && IsDefined(snapshot.RecipeId, snapshot.Tier) ? ResourceLoader.Load<ActiveSkillData>(SkillPath(snapshot.RecipeId, snapshot.Tier)) : null;

    public static HoverInfoManager.HoverInfoContent Hover(TalismanProductItemData item, bool expended = false)
    {
        var snapshot = item.Snapshot!;
        var cells = new[] { Vector2I.Zero, Vector2I.Down };
        var info = HoverInfoManager.Structured(Name(snapshot.RecipeId), BattleBoardView.Icon(IconPath(snapshot.RecipeId)),
            $"符箓 · {TierName(snapshot.Tier)}", new("", "消耗：体力1、法力0；符箓1张。\n目标："
                + (snapshot.RecipeId == "hushen" ? "自身" : "随机一名敌人\n出手方式：远程")),
            new("效果", EffectText(snapshot.RecipeId, snapshot.Tier)),
            new("", expended ? "本张已使用，符痕仍占格；整理时可移出或补符。" : "首次读到时检查一次；未能施用则保留。"));
        return ItemLayoutPresentation.AttachStorageVolume(info with
        {
            Subtitle = $"符箓 · {TierName(snapshot.Tier)}",
            CardWidth = 340f, IconSize = 96f, Footprint = new Vector2I(item.get_storage_volume(), 1),
            InventoryFootprintBelowTitle = false, CompactGridFooter = true, SingleColumn = true,
            GridDiagrams = [new("战斗占格", HoverGridKind.Combat, cells, RotationAllowed: true)],
            RulesText = "一块预留一张本人行囊中的真实符箓。\n首次读到检查一次，条件不足不扣符；本轮不重试。\n施符后命中、闪避、防护沿公共规则结算，不退符。\n符痕继续占格，不回复体力；共同回合结束后可移出或补入。"
        }, item.get_storage_volume());
    }
}
