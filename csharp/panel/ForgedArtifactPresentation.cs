using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>成器物品的共用展示。读取实例快照，不推演、不装备、不触发结算。</summary>
public static class ForgedArtifactPresentation
{
    public readonly record struct Section(string Title, string Body);

    public static string IconPath(ProcessForgingTrial.Artifact artifact)
    {
        // 形态只决定外观，不反向赋予属性或匹配配方。
        string shape = artifact.Shape ?? "";
        string sprite = shape.Contains("厚脊") ? "spined" : shape.Contains("宽展") ? "broad" : "slender";
        return $"res://textures/ui/forging_20261021/{sprite}_sword_ink_v2.png";
    }

    public static Texture2D Icon(ProcessForgingTrial.Artifact artifact) => GD.Load<Texture2D>(IconPath(artifact));

    public static string Subtitle(ProcessForgingTrial.Artifact artifact) =>
        $"{artifact.Tier} · {artifact.Category} · {ProcessForgingTrial.PolarityLabel(artifact.Polarity)}";

    public static string EffectDetails(ProcessForgingTrial.Effect effect) =>
        $"【{effect.Route}】{effect.Description}\n范围：{effect.Scope}\n时点：{effect.Event}\n{effect.Condition}";

    public static IReadOnlyList<Section> AbilitySections(ProcessForgingTrial.Artifact artifact)
    {
        var sections = new List<Section>();
        foreach (var group in artifact.Effects.Where(x => x.Special).GroupBy(x => x.CarrierId))
        {
            if (group.Key == "binding")
            {
                const string repeated = "选择一个类别、关键词与实际效果均符合要求的技能绑定；";
                sections.Add(new("化法 · 仅绑定一个技能", "选择一个符合条件的技能；各项效果按对应条件生效。\n" +
                    string.Join("\n\n", group.Select(x => "· " + x.Description + "\n条件：" + x.Condition.Replace(repeated, "") + "\n时点：" + x.Event))));
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
                sections.Add(new("御器信息", $"器体上限 {artifact.BodyMaximum} · 在场灵识 {artifact.SpiritRequired}\n召出与御使尚未开放。"));
            sections.Add(new("器蕴", artifact.BondContributions.Count > 0 ? string.Join("\n", artifact.BondContributions) : "未配置器蕴贡献"));
        }
        return sections;
    }

    public static HoverInfoManager.HoverInfoContent Hover(ProcessForgingTrial.Artifact artifact) =>
        new(artifact.Name, string.Join("\n\n", Sections(artifact).Select(x => x.Title + "\n" + x.Body)) +
            "\n\n试炼成器 · 仅供查看，装备功能暂未开放。", Icon(artifact), Subtitle(artifact));
}
