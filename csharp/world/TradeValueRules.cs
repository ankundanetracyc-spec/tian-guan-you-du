using Godot;
using System;

/// <summary>用于现有交易接受度与公平交易关系证据的统一估值，不直接作为交易价格。</summary>
public static class TradeValueRules
{
    public static int Estimate(ItemData? item, int quantity)
    {
        if (item is null) return 0;
        var categoryBonus = item.item_category switch
        {
            "秘籍" => 32,
            "装备" => 26,
            "丹药" => 20,
            "符箓" => 16,
            "器具" or "阵盘" => 18,
            _ => 8,
        };
        return Math.Max(8, 18 + item.grade_value * 8 + categoryBonus) * Math.Max(1, quantity);
    }
}
