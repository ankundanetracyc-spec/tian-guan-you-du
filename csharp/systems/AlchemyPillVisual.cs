using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>成丹展示与行囊共用的瓷瓶品阶刻点与物品说明；不生成独立评分物品。</summary>
public static class AlchemyPillVisual
{
    private static readonly string[] Tiers = ["薄效", "良效", "优效", "极效", "满效"];
    public static void Apply(TextureRect icon, int tier, string pillId = "")
    {
        var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/alchemy_container_grade.gdshader") };
        material.SetShaderParameter("tier", System.Math.Clamp(tier, 0, 4));
        var band = pillId switch
        {
            "huichun" => new Color("82916d"),
            "tongmai" => new Color("71869a"),
            "jingxin" => new Color("9b91a2"),
            "dingmai" => new Color("9a7a55"),
            "guiyuan_dingxi" => new Color("7b7799"),
            "ninggang" => new Color("9b8059"), "jinji" => new Color("a69568"),
            "qingshen" => new Color("80a28a"), "xusheng" => new Color("749076"),
            "jiegu" => new Color("ac805b"), "hezhong" => new Color("829da0"),
            "naling" => new Color("6f9a9c"), "fanyuan" => new Color("a38462"),
            _ => new Color("71869a")
        };
        material.SetShaderParameter("band_tint", band);
        icon.Material = material;
    }
    public static HoverInfoManager.HoverInfoContent Hover(PillItemData item, int? score = null,
        int? contentsCount = null, IReadOnlyList<int>? scores = null, bool singleBottle = false, Character? user = null,
        string? effectOverride = null)
    {
        int tier = System.Math.Clamp(item.efficacy_tier, 0, 4);
        string quality = Tiers[tier];
        var effect = item.pill_id switch
        {
            "huichun" => $"恢复{AlchemyProductCatalog.Healing[tier]}点生命值",
            "tongmai" => $"恢复最大法力的{AlchemyProductCatalog.ManaPercent[tier]}%",
            "jingxin" => $"恢复{AlchemyProductCatalog.Calm[tier]}点心境",
            _ => item.EffectText
        };
        var introduction = CleanIntroduction(item.description);
        if (item.pill_id == "dingmai") introduction = "只用于云息三层晋升四层的闭关突破，不可直接服用或编入战备。" + introduction;
        else if (item.pill_id == "guiyuan_dingxi") introduction = "只用于云息九层晋升十层的闭关突破，不可直接服用或编入战备。" + introduction;
        else if (!AlchemyProductCatalog.IsEnabled(item.pill_id)) introduction = "暂未开放使用。" + introduction;
        var condition = item.pill_id switch
        {
            "jingxin" => "心境低于0时可用，最高恢复至0，不能获得正向心境。",
            "tongmai" => "不超过法力上限，不恢复大世界精力。",
            "ninggang" or "jinji" or "qingshen" or "xusheng" => "只在战斗中由本人服用。",
            "jiegu" => "只在战斗中由本人服用，存在外伤时才可用。",
            "hezhong" => "只在战斗中由本人服用，存在内伤时才可用。",
            "naling" => "只用于云息一至九层，不自动突破，也不增加归元息。",
            "fanyuan" => "只在大世界精力有缺口时可用，不恢复战斗体力。",
            _ => ""
        };
        if (condition.Length > 0) introduction = condition + introduction;
        if (user is not null && user.PillAbsorption < 1)
        {
            int actual = user.AlchemyPillAmount(item.pill_id, tier);
            string absorption = $"{user.PillAbsorption * 100:0.##}%";
            effect = item.pill_id switch
            {
                "huichun" => $"恢复{actual}点生命值，基础{AlchemyProductCatalog.Healing[tier]}点 计算公式：取较大值(1,向下取整({AlchemyProductCatalog.Healing[tier]}×{absorption}))＝{actual}",
                "tongmai" => $"恢复{actual}点法力，基础为最大法力的{AlchemyProductCatalog.ManaPercent[tier]}% 计算公式：最大法力[{user.max_mana}]×{AlchemyProductCatalog.ManaPercent[tier]}%×{absorption}，向下取整且基础恢复大于0时至少1点＝{actual}",
                "jingxin" => $"恢复{actual}点心境，基础{AlchemyProductCatalog.Calm[tier]}点 计算公式：取较大值(1,向下取整({AlchemyProductCatalog.Calm[tier]}×{absorption}))＝{actual}",
                _ => ActualEffectText(item, user.PillToxicity)
            };
        }
        else if (item.pill_id == "tongmai") effect += " 计算公式：向下取整(最大法力×" + AlchemyProductCatalog.ManaPercent[tier] + "%)";
        if (effectOverride is not null) effect = effectOverride;
        var toxicity = PillToxicityRules.Load(item.pill_id, item.efficacy_tier);
        var factor = new[] { 1.5, 1.2, .9, .6, .3 }[tier];
        var toxicityText = $"＋{toxicity}";
        if (PillToxicityRules.BaseLoads.TryGetValue(item.pill_id, out var basis))
            toxicityText += $" 计算公式：向上取整({basis:0.##}×{factor:0.##})＝{toxicity}";
        var name = Regex.Replace(item.entity_name, "[·・]" + quality + "$", "");
        return Build(item, name, $"丹阶：{RankName(item.pill_rank)}\n丹效：{quality}\n药效：{effect}\n丹毒：{toxicityText}", introduction);
    }

    public static bool IsPill(ItemData item) => item is PillItemData || item.item_category == "丹药";

    public static string ActualEffectText(PillItemData pill, double toxicity)
    {
        var tier = System.Math.Clamp(pill.efficacy_tier, 0, 4);
        int amount = PillToxicityRules.Absorb(AlchemyProductCatalog.PrimaryAmount(pill.pill_id, tier), toxicity);
        int healing = PillToxicityRules.Absorb(AlchemyProductCatalog.WoundHealing[tier], toxicity);
        var text = pill.pill_id switch
        {
            "huichun" => $"恢复{amount}点生命值。",
            "ninggang" => $"获得{amount}点护盾。",
            "jinji" => $"物理防御提高{amount}点，持续2回合。",
            "qingshen" => $"获得{amount}层身法。",
            "xusheng" => $"后续两个自身回合开始时，各恢复{amount}点生命值。",
            "jiegu" => $"移除{amount}层外伤，并恢复{healing}点生命值。",
            "hezhong" => $"移除{amount}层内伤，并恢复{healing}点生命值。",
            "naling" => $"补入当前层{amount}缕灵力，不超过本层门槛。",
            "fanyuan" => $"恢复{amount}点大世界精力。",
            _ => pill.EffectText
        };
        return PillToxicityRules.Efficiency(toxicity) < 1
            ? text + $" 基础药效：{pill.EffectText} 计算公式：各正整数药效＝取较大值(1,向下取整(基础值×{PillToxicityRules.Efficiency(toxicity):0.##%}))；时长与次数不变。"
            : text;
    }

    public static HoverInfoManager.HoverInfoContent Hover(ItemData item, Character? user = null)
    {
        if (item is PillItemData pill) return Hover(pill, user: user);
        var id = item.definition_id.ToString();
        var toxicityId = id == "pill_yunxi_meridian_stabilizing" ? "dingmai" : id == "pill_yunxi_return_origin" ? "guiyuan_dingxi" : "";
        var effect = toxicityId == "dingmai" ? "用于云息三层晋升四层" : toxicityId == "guiyuan_dingxi" ? "用于云息九层晋升十层" : "见简介";
        var grade = item.grade_value > 0 ? item.get_grade_string() : "未标注";
        var fields = $"丹阶：{grade}\n药效：{effect}";
        if (PillToxicityRules.BaseLoads.TryGetValue(toxicityId, out var load)) fields += $"\n丹毒：＋{load:0.##}";
        return Build(item, item.entity_name, fields, CleanIntroduction(item.description));
    }

    private static string RankName(int rank) => rank is >= 1 and <= 9 ? "一二三四五六七八九"[rank - 1] + "品" : rank + "品";

    private static string CleanIntroduction(string description) => string.Join("。", description.Split('。')
        .Select(s => s.Trim()).Where(s => s.Length > 0 && !Regex.IsMatch(s, @"^九品.+丹，丹效") &&
            !s.Contains("丹药按颗计数") && !s.Contains("瓷瓶仅作") && !s.Contains("同类丹药在行囊合并")));

    private static HoverInfoManager.HoverInfoContent Build(ItemData item, string name, string fields, string introduction)
    {
        var icon = !string.IsNullOrWhiteSpace(item.icon_path) && ResourceLoader.Exists(item.icon_path) ? GD.Load<Texture2D>(item.icon_path) : null;
        return HoverInfoManager.Structured(name, icon, "", new("", fields), new("", "简介：" + introduction))
            with { CardWidth = 340f, IconSize = 96f, ShowRules = false, Footprint = item.get_inventory_size() };
    }
}
