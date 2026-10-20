/// <summary>2026-10-11 已确认的凝核最终值收益；只读取全部回弹和紊乱结算后的值。</summary>
public static class CondensationOutcomeRules
{
    public const int SuccessScore = 100;
    public static int DifficultySteps(int finalScore) => Math.Max(0, finalScore - SuccessScore) / 5;
    public static double SpiritRequirementMultiplier(int finalScore) => 1d + DifficultySteps(finalScore) * .0125d;
    public static double FoundationCloudBonus(int finalScore) => DifficultySteps(finalScore) * .025d;
    public static int FlatAttributeBonus(int finalScore) => Math.Max(0, finalScore - SuccessScore) / 25;
    public static float AttributeFlatBonus(string key, int finalScore)
    {
        var perTier = FlatAttributeBonus(finalScore);
        return key switch { "physique" or "soul" => perTier * 1.5f, "intelligence" => perTier, _ => 0f };
    }
    public static string Describe(int finalScore)
    {
        if (finalScore < SuccessScore) return "未成核，不获得凝核底蕴奖励。";
        return $"后续本层灵力门槛 ×{SpiritRequirementMultiplier(finalScore):0.####}；铸基云气较基准多{FoundationCloudBonus(finalScore) * 100:0.##}%\n体魄、神魂：各定额 +{AttributeFlatBonus("physique",finalScore):0.#}；灵慧：定额 +{AttributeFlatBonus("intelligence",finalScore):0.#}";
    }
}
