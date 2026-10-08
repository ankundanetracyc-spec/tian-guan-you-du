using System;

/// <summary>
/// 云息六层至七层凝核的纯规则层。
///
/// 这里不持有卡牌、材料或 UI 节点；牌局结算器只通过本类改变三项结果数值，
/// 从而保证压缩、扩张与杂质失败在试玩页和正式界面使用同一套公式。
/// </summary>
public sealed class CondensationCoreState
{
    public const float MinimumVolume = 1f;
    public const float MinimumDensity = 1f;
    public const float FailureImpurity = 100f;

    public float Volume { get; private set; } = MinimumVolume;
    public float Density { get; private set; } = MinimumDensity;
    public float Impurity { get; private set; }
    public bool Failed { get; private set; }
    public float TotalMass => Volume * Density;

    public CondensationCoreState(float volume = MinimumVolume, float density = MinimumDensity, float impurity = 0f)
    {
        Volume = MathF.Max(MinimumVolume, volume);
        Density = MathF.Max(MinimumDensity, density);
        Impurity = MathF.Max(0f, impurity);
        Failed = Impurity >= FailureImpurity;
        if (Failed) Impurity = FailureImpurity;
    }

    /// <summary>直接牌面增加体积，会增加灵核总量。</summary>
    public CondensationChange AddVolume(float amount) => Apply(amount, 0f, 0f);

    /// <summary>直接牌面增加密度，会增加灵核总量。</summary>
    public CondensationChange AddDensity(float amount) => Apply(0f, amount, 0f);

    /// <summary>杂质不能低于零；达到 100 时立刻标记失败。</summary>
    public CondensationChange AddImpurity(float amount) => Apply(0f, 0f, amount);

    /// <summary>
    /// 标准压缩不改变总量。实际压缩量受当前密度与 1.00 体积下限共同限制。
    /// </summary>
    public CondensationChange Compress(float standardAmount)
    {
        if (Failed || standardAmount <= 0f) return CondensationChange.None;
        var beforeVolume = Volume;
        var beforeDensity = Density;
        var beforeImpurity = Impurity;
        var total = TotalMass;
        var actual = MathF.Min(standardAmount / Density, Volume - MinimumVolume);
        Volume = MathF.Max(MinimumVolume, Volume - MathF.Max(0f, actual));
        Density = MathF.Max(MinimumDensity, total / Volume);
        return new CondensationChange(Volume - beforeVolume, Density - beforeDensity, Impurity - beforeImpurity, Failed);
    }

    /// <summary>
    /// 标准扩张不改变总量。高密度、低体积时更容易舒展；扩张不会制造溃散。
    /// </summary>
    public CondensationChange Expand(float standardAmount)
    {
        if (Failed || standardAmount <= 0f) return CondensationChange.None;
        var beforeVolume = Volume;
        var beforeDensity = Density;
        var beforeImpurity = Impurity;
        var total = TotalMass;
        var expected = standardAmount * Density / Volume;
        var actual = MathF.Min(expected, total - Volume);
        Volume += MathF.Max(0f, actual);
        Density = MathF.Max(MinimumDensity, total / Volume);
        return new CondensationChange(Volume - beforeVolume, Density - beforeDensity, Impurity - beforeImpurity, Failed);
    }

    /// <summary>
    /// 对一张后方灵根牌开始结算时适用邻位关系。前方、后方由当前云候的普通结算方向决定。
    /// </summary>
    public CondensationChange ApplyRelation(CondensationElement front, CondensationElement back, CondensationRelationTuning? tuning = null)
    {
        if (Failed || front == CondensationElement.None || back == CondensationElement.None) return CondensationChange.None;
        tuning ??= CondensationRelationTuning.Default;
        if (front == back) return Apply(tuning.SameDensity, tuning.SameVolume, 0f);
        if (Produces(front, back)) return Apply(tuning.GenerateDensity, 0f, tuning.GenerateImpurity);
        if (Controls(front, back)) return Apply(tuning.ControlVolume, 0f, tuning.ControlImpurity);
        return CondensationChange.None;
    }

    private CondensationChange Apply(float volume, float density, float impurity)
    {
        if (Failed) return CondensationChange.None;
        var beforeVolume = Volume;
        var beforeDensity = Density;
        var beforeImpurity = Impurity;
        Volume = MathF.Max(MinimumVolume, Volume + volume);
        Density = MathF.Max(MinimumDensity, Density + density);
        Impurity = MathF.Max(0f, Impurity + impurity);
        if (Impurity >= FailureImpurity)
        {
            Impurity = FailureImpurity;
            Failed = true;
        }
        return new CondensationChange(Volume - beforeVolume, Density - beforeDensity, Impurity - beforeImpurity, Failed);
    }

    public static bool Produces(CondensationElement front, CondensationElement back) =>
        (front, back) is
            (CondensationElement.Metal, CondensationElement.Water) or
            (CondensationElement.Water, CondensationElement.Wood) or
            (CondensationElement.Wood, CondensationElement.Fire) or
            (CondensationElement.Fire, CondensationElement.Earth) or
            (CondensationElement.Earth, CondensationElement.Metal);

    public static bool Controls(CondensationElement front, CondensationElement back) =>
        (front, back) is
            (CondensationElement.Metal, CondensationElement.Wood) or
            (CondensationElement.Wood, CondensationElement.Earth) or
            (CondensationElement.Earth, CondensationElement.Water) or
            (CondensationElement.Water, CondensationElement.Fire) or
            (CondensationElement.Fire, CondensationElement.Metal);
}

public enum CondensationElement { None, Metal, Wood, Water, Fire, Earth }

/// <summary>云候或牌面临时改写的关系数值。未传入时使用策划案标准值。</summary>
public sealed record CondensationRelationTuning(
    float SameDensity,
    float SameVolume,
    float GenerateDensity,
    float GenerateImpurity,
    float ControlVolume,
    float ControlImpurity)
{
    public static readonly CondensationRelationTuning Default = new(.02f, .02f, .04f, 1f, .06f, 2f);
}

/// <summary>单次效果造成的实际变化，供日志和动画直接使用。</summary>
public readonly record struct CondensationChange(float Volume, float Density, float Impurity, bool CausedFailure)
{
    public static readonly CondensationChange None = new(0f, 0f, 0f, false);
}
