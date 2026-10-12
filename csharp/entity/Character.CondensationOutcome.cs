public partial class Character
{
    public int condensation_final_score => has_spirit_core ? Math.Max(0, human?.condensation_final_score ?? 0) : 0;
    public int get_condensation_flat_bonus(string key) => CondensationOutcomeRules.ReceivesFlatBonus(key)
        ? CondensationOutcomeRules.FlatAttributeBonus(condensation_final_score) : 0;
    public double get_condensation_spirit_requirement_multiplier() => CondensationOutcomeRules.SpiritRequirementMultiplier(condensation_final_score);
    /// <summary>铸玄基正式转化未来读取此补正；不生成日常云气，不增加归元息数量。</summary>
    public double get_foundation_cloud_bonus() => CondensationOutcomeRules.FoundationCloudBonus(condensation_final_score);
}
