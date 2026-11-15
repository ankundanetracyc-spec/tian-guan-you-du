using Godot;

public partial class InfoPanel
{
    // 只读取已接入的收益来源，不用人物总属性冒充装备加成，也不把器体能力算到人物身上。
    private static List<(string Name, string Value)> EquipmentTotals(Character? human)
    {
        var flat = new Dictionary<string, float>();
        var percent = new Dictionary<string, float>();
        void Add(Dictionary<string, float> target, string key, float value) => target[key] = target.GetValueOrDefault(key) + value;
        if (human?.entity_data is HumanData data)
        {
            if (!string.IsNullOrWhiteSpace(data.weapon_path) && GD.Load<EquipmentData>(data.weapon_path) is { } weapon &&
                weapon.definition_id.ToString().StartsWith("sect_sword_", StringComparison.Ordinal))
                foreach (Godot.Variant key in weapon.fixed_bonuses.Keys)
                    Add(flat, key.AsString(), weapon.fixed_bonuses[key].AsSingle());
            if (human.get_equipped_forged_sword() is { } sword && ForgedArtifactInventory.TryRead(sword, out var snapshot))
            {
                foreach (var effect in snapshot.Effects.Where(effect => effect.CarrierId == "base" && effect.AtomId == "physical_defense"))
                    Add(flat, "physical_defense", Mathf.RoundToInt((float)effect.Value));
                foreach (var effect in snapshot.Effects.Where(effect => effect.AtomId == "physical_damage_percent" && effect.CarrierId is "base" or "keyword"))
                    Add(percent, effect.CarrierId == "base" ? "物理伤害" : "剑技物伤", (float)effect.Value);
            }
            var bonds = human.get_artifact_bonds();
            Add(percent, "剑技物伤", bonds.SwordDamageBonus * 100);
            Add(percent, "physical_defense", bonds.DefenseBonus * 100);
        }
        var common = new[] { "物理伤害", "physical_defense", "剑技物伤", "precision", "dodge", "critical", "crit_resist", "crit_damage_mult" };
        return common.Concat(flat.Keys).Concat(percent.Keys).Distinct()
            .Select(key =>
            {
                var values = new List<string>();
                if (!Mathf.IsZeroApprox(flat.GetValueOrDefault(key))) values.Add($"{flat[key]:+0.##;-0.##;0}");
                if (!Mathf.IsZeroApprox(percent.GetValueOrDefault(key))) values.Add($"{percent[key]:+0.##;-0.##;0}%");
                if (values.Count == 0) values.Add(key is "物理伤害" or "剑技物伤" ? "0%" : "0");
                return (key == "max_spirit_perception" ? "灵识上限" : battle_attribute_name(key), string.Join(" / ", values));
            }).ToList();
    }

    private void build_equipment_totals(Control page, Character? human)
    {
        var panel = CharacterPanel(page, 788, 0, 594, 370); panel.Name = "EquipmentTotalsPanel";
        var canvas = new Control(); panel.AddChild(canvas);
        var title = CharacterText("穿戴汇总", 28); canvas.AddChild(title); CharacterRect(title, 0, 0, 530, 38);
        var divider = new HSeparator(); canvas.AddChild(divider); CharacterRect(divider, 0, 48, 530, 4);
        var totals = EquipmentTotals(human);
        if (totals.Count == 0)
        { var empty = CharacterText("暂无加成", 22, true); canvas.AddChild(empty); CharacterRect(empty, 0, 72, 530, 34); }
        var rowHeight = Math.Min(40, 260 / Math.Max(1, (totals.Count + 1) / 2));
        for (var i = 0; i < totals.Count; i++)
        {
            var column = i % 2; var row = i / 2;
            var name = CharacterClippedText(totals[i].Name, 19); canvas.AddChild(name); CharacterRect(name, column * 274, 66 + row * rowHeight, 140, rowHeight);
            var value = CharacterText(totals[i].Value, 19); value.HorizontalAlignment = HorizontalAlignment.Right;
            canvas.AddChild(value); CharacterRect(value, column * 274 + 144, 66 + row * rowHeight, 108, rowHeight);
        }
    }

    private void build_equipment_bonds(Control page, Character? human)
    {
        var panel = CharacterPanel(page, 788, 388, 594, 194); panel.Name = "EquipmentBondsPanel";
        var canvas = new Control(); panel.AddChild(canvas);
        var title = CharacterText("器蕴", 28); canvas.AddChild(title); CharacterRect(title, 0, 0, 180, 38);
        var spirit = CharacterText($"灵识 {human?.used_spirit_perception ?? 0}/{human?.max_spirit_perception ?? 0}", 19, true);
        spirit.HorizontalAlignment = HorizontalAlignment.Right; canvas.AddChild(spirit); CharacterRect(spirit, 290, 4, 240, 30);
        CharacterHover(spirit, "灵识", "御使器物持续占用灵识，跨回合保持；收回或停用时释放。技能自身的体力、法力费用另算。");
        var totals = human?.get_artifact_bonds() ?? new ArtifactBondRuntime.Totals(0, 0);
        var bonds = new[] { (Name: "惊鸿", Points: totals.Jinghong, Effect: "剑技物伤", Bonus: totals.SwordDamageBonus),
            (Name: "抱朴", Points: totals.Baopu, Effect: "物理防御", Bonus: totals.DefenseBonus) };
        for (var i = 0; i < bonds.Length; i++)
        {
            var bond = bonds[i]; var y = 54 + i * 54;
            var name = CharacterText($"{bond.Name}  {bond.Points}/{(bond.Points < 2 ? 2 : 4)}", 22); canvas.AddChild(name); CharacterRect(name, 0, y, 230, 32);
            var effect = CharacterText(bond.Bonus > 0 ? $"{bond.Effect} +{bond.Bonus * 100:0}%" : "未激活", 20, bond.Bonus <= 0);
            effect.HorizontalAlignment = HorizontalAlignment.Right; canvas.AddChild(effect); CharacterRect(effect, 240, y, 290, 32);
            CharacterHover(name, bond.Name, ArtifactBondRuntime.Describe(bond.Name, bond.Points, bond.Effect));
        }
    }
}
