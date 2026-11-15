using System;

/// <summary>连续跨档需求曲线；输入为各实际过程轴的完成度，输出用于同档固定效果的判级。</summary>
public static class AlchemyQualityRules
{
    // 交界前的一个分数需要跨过较长过程区间，档内仍线性。均为首轮可调锚点。
    private static readonly double[] Raw = [0, 20, 44, 51, 65, 76, 84, 91, 97, 100];
    private static readonly double[] Score = [0, 20, 50, 51, 75, 76, 90, 91, 99, 100];
    private static readonly double[] Demand = [0, 1, 1.25, 1.5, 1.8, 2.2, 2.55, 3.2, 3.6, 4];
    public static double RawForDemand(double ratio)
    {
        ratio = Math.Max(0, ratio);
        for (int i = 1; i < Demand.Length; i++)
            if (ratio <= Demand[i]) return Raw[i - 1] + (Raw[i] - Raw[i - 1]) *
                (ratio - Demand[i - 1]) / (Demand[i] - Demand[i - 1]);
        return 100;
    }
    public static double FromRaw(double raw)
    {
        raw = Math.Clamp(raw, 0, 100);
        for (int i = 1; i < Raw.Length; i++)
            if (raw <= Raw[i]) return Score[i - 1] + (Score[i] - Score[i - 1]) *
                (raw - Raw[i - 1]) / (Raw[i] - Raw[i - 1]);
        return 100;
    }
    public static int Tier(int score) => score < 20 ? -1 : score <= 50 ? 0 : score <= 75 ? 1 : score <= 90 ? 2 : score <= 99 ? 3 : 4;
    public static string Name(int score) => Tier(score) switch { 0 => "薄效", 1 => "良效", 2 => "优效", 3 => "极效", 4 => "满效", _ => "未成丹" };
    public static double Polarity(double deviation, double at20, double at100) => deviation <= at100 ? 100 :
        deviation <= at20 ? 20 + 80 * (at20 - deviation) / (at20 - at100) :
        20 * Math.Clamp(1 - (deviation - at20) / .12, 0, 1);
    public static double Purity(double fraction) => fraction <= .06
        ? 20 + 80 * Math.Clamp((.06 - fraction) / .057, 0, 1)
        : 20 * Math.Clamp((.10 - fraction) / .04, 0, 1);
}
