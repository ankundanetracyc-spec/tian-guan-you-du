using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>One fixed field order for ordinary equipment and forged artifacts.</summary>
public static class EquipmentItemPresentation
{
    public static bool IsEquipment(ItemData item) => item is EquipmentData or SectArtifactOfferData;

    public static HoverInfoManager.HoverInfoContent Build(string name, Texture2D? icon, string grade, string category,
        string benefits = "", string passive = "", string binding = "", string skills = "", string bonds = "", string introduction = "", string sharpness = "")
    {
        static string Value(string text) => string.IsNullOrWhiteSpace(text) ? "无" : text.Trim();
        var bodyProperty = category.StartsWith("剑", System.StringComparison.Ordinal) ? $"\n锋锐：{Value(sharpness)}" : "";
        var bindingText = string.IsNullOrWhiteSpace(binding) ? "无" : "\n" + binding.Trim();
        var effects = string.Join("\n", new[] { benefits.Trim(), passive.Trim() }.Where(value => value.Length > 0));
        var effectText = effects.Length == 0 ? "无" : "\n" + effects;
        return HoverInfoManager.Structured(name, icon, "",
            new("", $"品阶：{Value(grade)}\n器类：{Value(category)}\n器蕴：{Value(bonds)}{bodyProperty}\n\n装备效果：{effectText}\n\n化法：{bindingText}\n\n技能：{Value(skills)}"),
            new("", "简介：" + introduction.Trim())) with { CardWidth = 340f, IconSize = 96f, ShowRules = false };
    }

    public static HoverInfoManager.HoverInfoContent Hover(ItemData item)
    {
        if (item is SectArtifactOfferData offer)
            return ForgedArtifactInventory.Hover(offer.CreateArtifact());
        if (item is not EquipmentData equipment) return Build(item.entity_name, null, "", "") with { Footprint = item.get_inventory_size() };
        var grade = equipment.item_subcategory.Split('·').FirstOrDefault(s => s is "凡器" or "法器" or "法宝" or "灵宝" or "道器")
            ?? (equipment.grade_value > 0 ? equipment.get_grade_string() : "未标注");
        var category = equipment.weapon_type == 1 ? "剑" : equipment.equip_type switch { 0 => "武器", 1 => "防具", _ => "饰品" };
        var benefits = Modifiers(equipment.fixed_bonuses, false).Concat(Modifiers(equipment.mult_bonuses, true));
        var skills = equipment.weapon_skill_paths.Where(path => ResourceLoader.Exists(path)).Select(path => GD.Load<ActiveSkillData>(path)?.skill_name)
            .Where(name => !string.IsNullOrWhiteSpace(name)).Distinct();
        var icon = !string.IsNullOrWhiteSpace(item.icon_path) && ResourceLoader.Exists(item.icon_path) ? GD.Load<Texture2D>(item.icon_path) : null;
        // These definitions contain short descriptions; remove sentences repeating the rendered bonuses.
        var intro = string.Join("。", item.description.Split('。').Where(s => !s.Contains('+') && !s.Contains('＋'))).Trim('。');
        return Build(item.entity_name, icon, grade, category, string.Join("\n", benefits), skills: string.Join("、", skills), introduction: intro)
            with { Footprint = item.get_inventory_size() };
    }

    public static HoverInfoManager.HoverInfoContent Hover(ArtifactInstanceData item)
    {
        if (ForgedArtifactInventory.IsForged(item)) return ForgedArtifactInventory.Hover(item);
        var iconPath = item.form?.icon_path ?? "";
        var icon = !string.IsNullOrWhiteSpace(iconPath) && ResourceLoader.Exists(iconPath) ? GD.Load<Texture2D>(iconPath) : null;
        var body = item.max_durability > 0 ? $"器物耐久{item.current_durability}/{item.max_durability}" : "";
        var inscriptions = item.inscriptions.Where(x => x is not null).Select(x => x.display_name + "：" + string.Join("、", Modifiers(x.modifiers, false)));
        return Build(item.artifact_name, icon, item.artifact_tier, item.form?.form_name ?? "未标注", body,
            string.Join("\n", inscriptions), introduction: "");
    }

    public static HoverInfoManager.HoverInfoContent Hover(ProcessForgingTrial.Artifact artifact)
    {
        var benefits = string.Join("\n", artifact.Effects.Where(x => !x.Special).Select(x => x.Description));
        // The blade is an object's property, not an unconditional bonus to every character attack.
        var category = artifact.Category + (string.IsNullOrWhiteSpace(artifact.Shape) ? "" : $"（{artifact.Shape}）");
        var passive = string.Join("\n", artifact.Effects.Where(x => x.Special && x.CarrierId is not ("binding" or "direct" or "summon"))
            .Select(ConditionalEffect));
        var binding = ForgedArtifactPresentation.BindingDetails(artifact.Effects.Where(x => x.Special && x.CarrierId == "binding"));
        var skillDetails = artifact.Effects.Where(x => x.Special && (x.CarrierId is "direct" or "summon") && x.Value > 0)
            .Select(ForgedArtifactPresentation.EffectDetails).ToList();
        if (artifact.BodyMaximum > 0)
            skillDetails.Add($"御用／御使：器体上限{artifact.BodyMaximum}，在场占灵识{artifact.SpiritRequired}。\n御用：体力1、法力0；御使：体力1、法力1，临时灵识1。");
        return Build(artifact.Name, ForgedArtifactPresentation.Icon(artifact), artifact.Tier, category, benefits, passive, binding,
            string.Join("\n", skillDetails), string.Join("、", artifact.BondContributions), sharpness: artifact.Sharpness.ToString())
            with { Footprint = ForgedArtifactPresentation.InventorySize(artifact) };
    }

    private static string ConditionalEffect(ProcessForgingTrial.Effect effect)
    {
        if (effect.CarrierId == "trigger") return $"{effect.Event}：{effect.Description}\n{effect.Condition}";
        if (effect.Scope == "剑关键词技能中的物理伤害") return $"带“剑”关键词的技能，{effect.Description}";
        return string.IsNullOrWhiteSpace(effect.Scope) ? effect.Description : $"{effect.Scope}：{effect.Description}";
    }

    private static IEnumerable<string> Modifiers(Godot.Collections.Dictionary values, bool percent) => values.Keys.Select(key =>
    {
        var value = values[key].AsDouble();
        return $"{AttributeName(key.AsString())}{(value >= 0 ? "＋" : "")}{(percent ? value * 100 : value):0.##}{(percent ? "%" : "")}";
    });

    private static string AttributeName(string name) => name switch
    {
        "physique" => "体魄", "intelligence" => "灵慧", "soul" => "神魂", "max_health" => "生命上限", "max_mana" => "法力上限",
        "physical_defense" => "物理防御", "physical_damage" => "物理伤害", "precision" => "精准", "dodge" => "身法",
        "critical" => "会心", "crit_resist" => "护心", "crit_damage_mult" => "会心伤害", "initiative_speed" => "速度",
        "sword_proficiency" => "剑道造诣", "body_proficiency" => "体道造诣", "magic_proficiency" => "术法造诣", _ => name
    };
}
