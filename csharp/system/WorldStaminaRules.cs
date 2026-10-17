/// <summary>首轮可调的大世界精力数值；战斗内体力仍由战斗规则独立处理。</summary>
public static class WorldStaminaRules
{
    public const float InitialCapacity = 100.0f;
    public const float ConversationCost = 1.0f;
    public const float InteractionCost = 1.0f;
    // 2026-10-10 用户确认：吐纳与归元养息只推进时间，不消耗大世界精力。
    public const float CultivationCostPerGameHour = 0.0f;
    public const float StudyCostPerGameHour = 1.0f;
    public const float RestRecoveryPerGameHour = 1.0f;
}
