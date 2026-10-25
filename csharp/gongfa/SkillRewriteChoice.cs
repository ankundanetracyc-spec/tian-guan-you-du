using System;

/// <summary>秘籍及器物化法共用的选择项；效果仍由各来源自己的实现结算。</summary>
public sealed class SkillRewriteChoice
{
    public required string Id { get; init; }
    public required string Source { get; init; }
    public required string Title { get; init; }
    public required string Condition { get; init; }
    public required string Description { get; init; }
    public bool Available { get; init; }
    public string UnavailableReason { get; init; } = "";
    public ArtifactInstanceData? LegacyArtifact { get; init; }
    public required Func<ActiveSkillData, bool> Matches { get; init; }
    public required Func<ActiveSkillData, ActiveSkillData> Apply { get; init; }
}
