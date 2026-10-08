using Godot;

/// <summary>
/// 行炁灵感的九品规则。品级显示遵循修仙品级：九品最低，一品最高。
/// 内部 grade 采用 1—9，便于与项目现有的品级数值约定保持一致。
/// </summary>
public static class QiInspirationCatalog
{
    public static readonly IReadOnlyList<string> GradeNames =
        new[] { "九品", "八品", "七品", "六品", "五品", "四品", "三品", "二品", "一品" };

    // 参悟收益和窗口都按九品到一品递增；灵感被参悟时即时结算，不消耗游戏时间。
    public static readonly IReadOnlyList<float> InsightRewards =
        new[] { 0.25f, 0.45f, 0.75f, 1.20f, 1.90f, 3.00f, 4.80f, 7.50f, 12.00f };

    public static readonly IReadOnlyList<int> DurationDays =
        new[] { 30, 60, 90, 150, 240, 360, 540, 720, 1080 };

    // 所有来源都从九品起步；来源质量只改变向高品级上探的概率。
    private static readonly IReadOnlyDictionary<string, int[]> SourceWeights = new Dictionary<string, int[]>
    {
        ["修炼"] = new[] { 70, 20, 7, 2, 1, 0, 0, 0, 0 },
        ["切磋"] = new[] { 40, 28, 16, 8, 4, 2, 1, 1, 0 },
        ["论道"] = new[] { 26, 20, 15, 12, 9, 7, 5, 4, 2 },
        ["特殊探索"] = new[] { 20, 17, 14, 12, 10, 9, 7, 6, 5 },
        ["奇遇"] = new[] { 20, 17, 14, 12, 10, 9, 7, 6, 5 },
        ["稀有事件"] = new[] { 20, 17, 14, 12, 10, 9, 7, 6, 5 }
    };

    public static int ClampGrade(int grade) => Mathf.Clamp(grade, 1, GradeNames.Count);

    public static string GradeName(int grade) => GradeNames[ClampGrade(grade) - 1];

    public static float InsightReward(int grade) => InsightRewards[ClampGrade(grade) - 1];

    public static int DurationHours(int grade) => DurationDays[ClampGrade(grade) - 1] * 24;

    public static string SourceName(string source) => source switch
    {
        "探索" => "特殊探索",
        "事件" => "稀有事件",
        _ => string.IsNullOrWhiteSpace(source) ? "未知活动" : source
    };

    private static int[] weights_for_source(string source) => source switch
    {
        "切磋" => SourceWeights["切磋"],
        "论道" => SourceWeights["论道"],
        "特殊探索" or "探索" => SourceWeights["特殊探索"],
        "奇遇" => SourceWeights["奇遇"],
        "稀有事件" or "事件" => SourceWeights["稀有事件"],
        _ => SourceWeights["修炼"]
    };

    public static int RollGrade(string source, RandomNumberGenerator rng)
    {
        var weights = weights_for_source(source);
        var total = weights.Sum();
        if (total <= 0) return 1;
        var roll = rng.RandiRange(1, total);
        var cursor = 0;
        for (var index = 0; index < weights.Length; index++)
        {
            cursor += weights[index];
            if (roll <= cursor) return index + 1;
        }
        return 1;
    }
}
