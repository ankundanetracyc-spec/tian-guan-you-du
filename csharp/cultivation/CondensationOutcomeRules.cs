/// <summary>2026-11-04 凝核收益：统一五分档，三项基础属性同额；只读取最终结算值。</summary>
public static class CondensationOutcomeRules
{
    public const int SuccessScore = 100;
    public const int ScorePerStep = 5;
    public const float AttributePerStep = .25f;
    public const double SpiritRequirementPerStep = .0125d;
    public const double FoundationCloudPerStep = .025d;
    public static int DifficultySteps(int finalScore) => finalScore <= SuccessScore ? 0 : (finalScore - SuccessScore) / ScorePerStep;
    public static double SpiritRequirementMultiplier(int finalScore) => 1d + DifficultySteps(finalScore) * SpiritRequirementPerStep;
    public static double FoundationCloudBonus(int finalScore) => DifficultySteps(finalScore) * FoundationCloudPerStep;
    public static float FlatAttributeBonus(int finalScore) => DifficultySteps(finalScore) * AttributePerStep;
    public static float AttributeFlatBonus(string key, int finalScore)
    {
        return key is "physique" or "intelligence" or "soul" ? FlatAttributeBonus(finalScore) : 0f;
    }
    public static string Describe(int finalScore)
    {
        if (finalScore < SuccessScore) return "未成核，不获得凝核底蕴奖励。";
        return $"体魄、灵慧、神魂：各定额 ＋{FlatAttributeBonus(finalScore):0.##}\n" +
            $"云息七→八、八→九、九→十：本层灵力需求 ×{SpiritRequirementMultiplier(finalScore):0.####}\n" +
            $"铸基云气补正：＋{FoundationCloudBonus(finalScore) * 100:0.##}%（玄基流程开放后兑现）";
    }
}
