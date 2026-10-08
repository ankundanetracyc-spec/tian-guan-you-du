using Godot;

/// <summary>
/// 三景图录接口。当前保留稳定 ID、归属与观法；新方案确认前不配置战斗属性或效果。
/// 后续可替换为 .tres 图录而不改变角色存档中的稳定 ID。
/// </summary>
public sealed record VisualizationDefinition(
    string Id,
    string Slot,
    string Name,
    string Description,
    float SoulPerRank,
    float IntelligencePerRank,
    string Style);

public sealed record VisualizationPracticeMethod(
    string Id,
    string SceneId,
    string Name,
    string Description,
    int MinRank,
    int MaxRank,
    int DurationHours,
    float RankYield,
    string ConditionText);

public static class VisualizationCatalog
{
    public static readonly VisualizationDefinition[] Defaults =
    [
        new("cold_moon", VisualizationRules.Sky, "一轮寒月", "月色澄澈，重在映照、守持与不散。", 0f, 0f, "圆映"),
        new("snow_mountain", VisualizationRules.Terrain, "远山积雪", "山雪寂然，重在承载、定势与自守。", 0f, 0f, "镇持"),
        new("river_lamp", VisualizationRules.Thing, "江心孤灯", "孤灯临水，重在照见、引念与辨明。", 0f, 0f, "照见")
    ];

    public static readonly VisualizationPracticeMethod[] DefaultMethods =
    [
        new("watch_moon", "cold_moon", "望月观法", "于静室中收视守月，使月影不随杂念而散。", 1, 20, 24, .85f, "夜静无扰"),
        new("keep_moon", "cold_moon", "守月观法", "不逐月光，专守月中澄明之意。", 15, 35, 24, .72f, "月下静室"),
        new("wait_moon", "cold_moon", "候月观法", "候月盈亏，以缺圆相映之理反照灵台。", 30, 50, 24, .60f, "月相相应"),
        new("mountain_stillness", "snow_mountain", "对山观法", "闭目存山，先定其势，再辨雪痕。", 1, 20, 24, .85f, "静室或山地"),
        new("embrace_peak", "snow_mountain", "抱岳观法", "以己念承山势，不令风雪动其根基。", 15, 35, 24, .72f, "山地或无外扰静室"),
        new("snow_clear", "snow_mountain", "雪霁观法", "雪后见山，取其澄寂而不滞。", 30, 50, 24, .60f, "雪景机缘"),
        new("lamp_watch", "river_lamp", "守灯观法", "于明暗之间守一盏灯，令微明长留灵台。", 1, 20, 24, .85f, "灯火与静处"),
        new("guide_lamp", "river_lamp", "引灯观法", "以一灯引万念，辨其来处而不为所惑。", 15, 35, 24, .72f, "夜行或静室灯火"),
        new("night_lamp", "river_lamp", "照夜观法", "灯照深夜，令所见与未见皆能留痕。", 30, 50, 24, .60f, "幽暗异境")
    ];

    public static VisualizationDefinition? Get(string id) => Defaults.FirstOrDefault(item => item.Id == id);
    public static IEnumerable<VisualizationDefinition> BySlot(string slot) => Defaults.Where(item => item.Slot == slot);
    public static VisualizationPracticeMethod? GetMethod(string id) => DefaultMethods.FirstOrDefault(item => item.Id == id);
    public static IEnumerable<VisualizationPracticeMethod> GetMethods(string sceneId, int rank) =>
        DefaultMethods.Where(item => item.SceneId == sceneId && rank >= item.MinRank && rank <= item.MaxRank);
}
