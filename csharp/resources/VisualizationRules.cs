using Godot;

/// <summary>蕴神观想的稳定槽位、五十重进度与节点规则。</summary>
public static class VisualizationRules
{
    public const string Sky = "sky";
    public const string Terrain = "terrain";
    public const string Thing = "thing";
    public const int MaxRank = 50;
    public const int NodeInterval = 5;

    public static readonly string[] Slots = [Sky, Terrain, Thing];

    private static readonly Dictionary<string, string> SlotNames = new()
    {
        [Sky] = "天象",
        [Terrain] = "地文",
        [Thing] = "物华"
    };

    public static string GetSlotName(string slot) => SlotNames.GetValueOrDefault(slot, slot);
    public static int GetRank(float progress) => Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(1f, progress)), 1, MaxRank);
    public static float GetRankRatio(float progress) => GetRank(progress) >= MaxRank ? 1f : Mathf.Clamp(progress - Mathf.Floor(progress), 0f, 1f);
    public static int GetCompletedNodeCount(int rank) => Mathf.Clamp(rank / NodeInterval, 0, MaxRank / NodeInterval);
    public static bool IsNode(int rank) => rank > 0 && rank % NodeInterval == 0;
    /// <summary>每五重的固定局外收益：只强化后续观法，不以未定的战斗效果冒充节点奖励。</summary>
    public static float GetPracticeMultiplier(int rank) => 1f + GetCompletedNodeCount(rank) * .05f;
}
