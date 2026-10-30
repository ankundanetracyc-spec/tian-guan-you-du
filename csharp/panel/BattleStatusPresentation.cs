using Godot;
using System;
using System.Linq;

/// <summary>Presentation only; reads existing status instances and combat resources.</summary>
public static class BattleStatusPresentation
{
    public static HoverInfoManager.HoverInfoContent Instance(StatusInstance status, string id)
    {
        var effect = status.effect!;
        var name = string.IsNullOrWhiteSpace(effect.name) ? effect.effect_id : effect.name;
        var isHeart = id.StartsWith("心法:", StringComparison.Ordinal);
        var layers = isHeart && effect.max_stacks <= 1 ? "" : Count(status.stacks, effect.max_stacks);
        var description = effect.description.Trim();
        if (id.StartsWith("心法:", StringComparison.Ordinal))
            description = string.Join("\n", description.Split('\n').Select(s => s.Trim())
                .Where(s => !s.StartsWith("类型：") && !s.StartsWith("关键词：") && !s.StartsWith("心法被动："))
                .Select(s => s.StartsWith("效果：") ? s[3..] : s));
        if (effect.effect_id is "外伤" or "内伤")
        {
            var sentences = description.Split('。');
            if (sentences.Length > 1)
            {
                description = sentences[1].Split('；')[0] + "。";
                if (effect.description.Contains("同类新增层刷新")) description += "新增层数会刷新持续时间。";
            }
        }
        else if (effect.frame_counted_layers)
            description = string.Join("。", effect.description.Split('。').Where(s => s.Contains("行动条停止") || s.Contains("再次施加"))) + "。";
        var fields = isHeart ? layers : (layers.Length > 0 ? layers + "\n" : "") + "持续时间：" + Duration(status);
        return Build(name, BattleUnitOverlay.GetStatusIcon(id, status), fields, description);
    }

    public static string Duration(StatusInstance status)
    {
        var effect = status.effect!;
        if (effect.frame_counted_layers) return $"剩余{status.stacks}战斗帧";
        return effect.duration_type switch
        {
            StatusEffect.DurationType.CombatTime => $"剩余{Mathf.Max(0, status.remaining_combat_seconds):0.0}秒",
            StatusEffect.DurationType.Frames => $"剩余{Mathf.Max(0, Mathf.CeilToInt(status.remaining_frames))}帧",
            StatusEffect.DurationType.Hits => $"剩余{status.remaining_hits}次受击",
            StatusEffect.DurationType.Trigger when status.source_id.StartsWith("power:") || effect.effect_id == "剑意" => "本场战斗",
            StatusEffect.DurationType.Trigger => "持续至触发或消耗完毕",
            StatusEffect.DurationType.Turns when status.remaining_duration > 0 => $"剩余{status.remaining_duration}回合",
            _ => "本场战斗"
        };
    }

    public static HoverInfoManager.HoverInfoContent Resource(Creature unit, string id, int count, int manaCapacity, string fallback)
    {
        var amount = id switch
        {
            "气血" => Count(count, unit.max_qi_blood),
            "血炉" => Count(count, unit.blood_furnace_value_cap),
            _ => Count(count, 0)
        };
        var effect = id switch
        {
            "护盾" => "优先吸收伤害，按实际吸收量扣除。",
            "气血" => "每下降1层，按当前回落回复率恢复生命。基础回复率为最大生命的5%，可由明确效果调整。",
            "炁" when manaCapacity > 0 => $"达到{2L * manaCapacity}炁时完成周天 计算公式：当前法力上限[{manaCapacity}]×2＝{2L * manaCapacity}。清空炁，法力恢复至上限；超过阈值的炁不保留。",
            "炁" => "当前法力上限为0，不触发周天。",
            "血炉" => $"物理伤害＋{count / 2}% 计算公式：向下取整({count}÷2)＝{count / 2}。可被相关招式消耗。",
            _ => fallback
        };
        return Build(id, BattleUnitOverlay.GetStatusIcon(id, null), amount + "\n持续时间：本场战斗", effect);
    }

    private static string Count(int count, int maximum) => "当前层数：" + count + (maximum > 0 ? "/" + maximum : "");

    private static HoverInfoManager.HoverInfoContent Build(string name, Texture2D? icon, string fields, string effect) =>
        HoverInfoManager.Structured(name, icon, "", new("", fields), new("", "效果：" + effect))
            with { CardWidth = 340f, IconSize = 96f, ShowRules = false, LiveUpdate = true };
}
