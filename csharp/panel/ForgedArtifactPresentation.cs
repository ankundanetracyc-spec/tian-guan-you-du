using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>成器物品的共用展示。读取实例快照，不推演、不装备、不触发结算。</summary>
public static class ForgedArtifactPresentation
{
    public readonly record struct Section(string Title, string Body);

    public static string IconPath(ProcessForgingTrial.Artifact artifact)
    {
        return $"res://textures/ui/forged_swords_icons_20261025_v1/{SpriteName(artifact)}_sword.png";
    }

    // 战斗道具保留原有横图与握点；物品图标使用独立斜放构图。
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
                sections.Add(new("化法", string.Join("\n\n", group.Select(x =>
                    $"适用范围：{ForgedEquipmentLoadout.BindingScope(x)}\n化法追加：{x.Description}"))));
            }
            else sections.Add(new(group.First().Route, string.Join("\n\n", group.Select(EffectDetails))));
        }
        return sections;
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
        HoverInfoManager.Structured(artifact.Name, Icon(artifact), Subtitle(artifact),
            Sections(artifact).Select(x => new HoverInfoManager.HoverInfoSection(x.Title, x.Body)).ToArray());
}
