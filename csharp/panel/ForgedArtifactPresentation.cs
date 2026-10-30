using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>成器物品的共用展示。读取实例快照，不推演、不装备、不触发结算。</summary>
public static class ForgedArtifactPresentation
{
    public readonly record struct Section(string Title, string Body);

    public static string IconPath(ProcessForgingTrial.Artifact artifact)
    {
        var name = SpriteName(artifact);
        return InventoryIconDensityPack.Root + name + "_sword_" + (name == "slender" ? "2x6.png" : "3x6.png");
    }

    public static Vector2I InventorySize(ProcessForgingTrial.Artifact artifact) => new(SpriteName(artifact) == "slender" ? 2 : 3, 6);
    public static bool InventoryCanRotate(ProcessForgingTrial.Artifact artifact) => true;
    public static string InventoryFootprintShape(ProcessForgingTrial.Artifact artifact) => "rectangle";

    // 战斗道具保留原有横图与握点；物品图标独立于战斗构图。
    public static string BodySpritePath(ProcessForgingTrial.Artifact artifact) =>
        $"res://textures/ui/forged_swords_20261023_v1/{SpriteName(artifact)}_sword.png";

    private static string SpriteName(ProcessForgingTrial.Artifact artifact)
    {
        string shape = artifact.Shape ?? "";
        return shape.Contains("厚脊") ? "spined" : shape.Contains("宽展") ? "broad" : "slender";
    }

    public static Texture2D Icon(ProcessForgingTrial.Artifact artifact) => GD.Load<Texture2D>(IconPath(artifact));

    public static string Subtitle(ProcessForgingTrial.Artifact artifact) =>
        $"{artifact.Tier} · {artifact.Category} · {ProcessForgingTrial.PolarityLabel(artifact.Polarity)}";

    public static string EffectDetails(ProcessForgingTrial.Effect effect) =>
        $"【{effect.Route}】{effect.Description}\n范围：{effect.Scope}\n时点：{effect.Event}\n" +
        (effect.CarrierId is "direct" or "summon" ? effect.Value > 0 ? "装备后手动放入技能槽；体力1、法力1，施法临时占灵识1。" : "此项数值为0，不单独形成可用技能。" : effect.Condition);

    public static IReadOnlyList<Section> AbilitySections(ProcessForgingTrial.Artifact artifact)
    {
        var sections = new List<Section>();
        foreach (var group in artifact.Effects.Where(x => x.Special).GroupBy(x => x.CarrierId))
        {
            if (group.Key == "binding")
            {
                sections.Add(new("化法", BindingDetails(group)));
            }
            else sections.Add(new(group.First().Route, string.Join("\n\n", group.Select(EffectDetails))));
        }
        return sections;
    }

    public static string BindingDetails(IEnumerable<ProcessForgingTrial.Effect> source)
    {
        var effects = source.ToArray();
        if (effects.Length == 0) return "";
        var scopes = effects.Select(ForgedEquipmentLoadout.BindingScope).Distinct().ToArray();
        var scope = scopes.Length == 1 ? scopes[0] : "同时满足以下条件（" + string.Join("；", scopes) + "）";
        if (effects.Length == 2 && effects.Any(x => x.AtomId == "defuse_flat") && effects.Any(x => x.AtomId == "physical_defense_ignore_ratio"))
            scope = "能应对并施加化解，且带“气血”关键词的单体物理伤害技能";
        return "适用范围：" + scope + "\n追加效果：\n" + string.Join("\n", effects.Select(x => "· " + x.Description));
    }

    public static IReadOnlyList<Section> Sections(ProcessForgingTrial.Artifact artifact)
    {
        var sections = new List<Section>
        {
            new("本体属性", $"锋锐 {artifact.Sharpness}\n形态：{artifact.Shape}"),
            new("基础加成", string.Join("\n", artifact.Effects.Where(x => !x.Special).Select(x => "· " + x.Description).DefaultIfEmpty("无额外基础加成")))
        };
        if (artifact.Tier != "凡器")
        {
            sections.Add(new("器物效能", string.Join("\n\n", AbilitySections(artifact).Select(x => x.Title + "\n" + x.Body).DefaultIfEmpty("未形成特殊效能"))));
            if (artifact.BodyMaximum > 0)
                sections.Add(new("御器信息", $"器体上限 {artifact.BodyMaximum} · 在场灵识 {artifact.SpiritRequired}\n御用：体力1、法力0；御使：体力1、法力1，临时灵识1。"));
            sections.Add(new("器蕴贡献", (artifact.BondContributions.Count > 0 ? string.Join("\n", artifact.BondContributions.Select(value => "本件贡献：" + value)) : "未形成器蕴贡献") +
                "\n惊鸿：带有“剑”关键词的技能物理伤害；抱朴：角色物防。\n2点总计+10%，4点总计+20%。激活情况请查看人物装备页。"));
        }
        return sections;
    }

    public static HoverInfoManager.HoverInfoContent Hover(ProcessForgingTrial.Artifact artifact) =>
        EquipmentItemPresentation.Hover(artifact);
}
