using Godot;
using System.Collections.Generic;

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
            _ => new Color("71869a")
        };
        material.SetShaderParameter("band_tint", band);
        icon.Material = material;
    }
    public static HoverInfoManager.HoverInfoContent Hover(PillItemData item, int? score = null,
        int? contentsCount = null, IReadOnlyList<int>? scores = null, bool singleBottle = false, Character? user = null)
    {
        int tier = System.Math.Clamp(item.efficacy_tier, 0, 4);
        string quality = Tiers[tier];
        var countText = contentsCount is > 0 ? $" · 数量 {contentsCount.Value}颗" : "";
        var scoreText = scores is { Count: > 0 }
            ? "\n本炉装入丹药的丹效分：" + string.Join("、", scores) + "。"
            : "";
        var effect = item.EffectText;
        if (user is not null && user.PillAbsorption < 1)
        {
            int actual = user.AlchemyPillAmount(item.pill_id, tier);
            effect = item.pill_id switch
            {
                "huichun" => $"恢复{actual}点生命值（受丹毒影响，基础{AlchemyProductCatalog.Healing[tier]}）。",
                "tongmai" => $"恢复{actual}点法力（受丹毒影响，基础为最大法力的{AlchemyProductCatalog.ManaPercent[tier]}%）。",
                "jingxin" => $"恢复{actual}点负心境，最高至0（受丹毒影响）。",
                _ => effect
            };
        }
        var toxicity = PillToxicityRules.Load(item.pill_id, item.efficacy_tier);
        return HoverInfoManager.Structured(item.entity_name, GD.Load<Texture2D>(item.icon_path),
            $"{item.pill_rank}品丹药 · {quality}" + (score.HasValue ? $" · 丹效 {score.Value}/100" : "") + countText,
            new("使用效果", effect), new("服用代价", $"丹毒＋{toxicity}。"), new("本炉丹效", scoreText.Trim()));
    }
}
