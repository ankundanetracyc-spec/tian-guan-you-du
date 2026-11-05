using Godot;

public partial class InfoPanel
{
    public sealed record ProficiencyEntry(string Key, string Name, string Group, string Description);
    private static readonly List<ProficiencyEntry> CharacterProficiencies =
    [
        new("sword_proficiency", "剑道", "武艺", "影响剑道技艺与相关战斗手段。"),
        new("blade_proficiency", "刀道", "武艺", "影响刀道技艺与相关战斗手段。"),
        new("spear_proficiency", "枪道", "武艺", "影响枪道技艺与相关战斗手段。"),
        new("body_proficiency", "体道", "武艺", "影响体道技艺与相关战斗手段。"),
        new("magic_proficiency", "术法", "术法", "影响术法与相关技艺的掌握。"),
        new("forging_proficiency", "器道", "百工", "影响炼器与相关技艺的掌握。"),
        new("talisman_proficiency", "符道", "百工", "影响符箓与相关技艺的掌握。"),
        new("formation_proficiency", "阵道", "百工", "影响阵法与相关技艺的掌握。"),
        new("alchemy_proficiency", "丹道", "百工", "影响丹药与相关技艺的掌握。")
    ];
    public static void RegisterProficiency(ProficiencyEntry entry)
    {
        var index = CharacterProficiencies.FindIndex(value => value.Key == entry.Key);
        if (index >= 0) CharacterProficiencies[index] = entry; else CharacterProficiencies.Add(entry);
    }
    private string _proficiencyQuery = "", _proficiencyGroup = "全部";

    private void build_character_attributes(Creature unit)
    {
        var page = _characterPages[1]; CharacterClear(page); var human = unit as Character;
        populate_static_attribute_page(unit, human);
        var basic = CharacterBox(CharacterPanel(page, 0, 0, 426, 594), "基础属性");
        var battle = CharacterBox(CharacterPanel(page, 442, 0, 466, 594), "战斗属性");
        var cultivation = CharacterBox(CharacterPanel(page, 924, 0, 426, 594), "修行属性");
        void Stat(Control parent, string name, string value, string rowName, bool large = false)
        {
            var row = new HBoxContainer { CustomMinimumSize = new Vector2(0, large ? 82 : 36), SizeFlagsHorizontal = SizeFlags.ExpandFill };
            row.AddThemeConstantOverride("separation", 8); parent.AddChild(row);
            if (large)
            {
                var index = Array.IndexOf(new[] { "PhysiqueRow", "IntelligenceRow", "SoulRow", "FortuneRow", "ComprehensionRow" }, rowName);
                if (index >= 0)
                {
                    var sheet = GD.Load<Texture2D>("res://textures/ui/infopanel_attribute_icons_ink_v2.png");
                    row.AddChild(CharacterIcon(new AtlasTexture { Atlas = sheet, Region = new Rect2(index % 3 * 512, index / 3 * 512, 512, 512) }, 48));
                }
                var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter }; column.AddThemeConstantOverride("separation", 2); row.AddChild(column);
                var caption = CharacterText(name, 22); caption.MouseFilter = MouseFilterEnum.Stop; column.AddChild(caption);
                var figure = CharacterText(value, 29); figure.MouseFilter = MouseFilterEnum.Stop; column.AddChild(figure);
                CharacterAttributePresentation.Bind(caption, unit, rowName, name); CharacterAttributePresentation.Bind(figure, unit, rowName, name); return;
            }
            var title = CharacterText(name, large ? 23 : 20); title.SizeFlagsHorizontal = SizeFlags.ExpandFill; title.MouseFilter = MouseFilterEnum.Stop; row.AddChild(title);
            var number = CharacterText(value, large ? 29 : 21); number.MouseFilter = MouseFilterEnum.Stop; number.CustomMinimumSize = new Vector2(55, 0); number.HorizontalAlignment = HorizontalAlignment.Right; row.AddChild(number);
            CharacterAttributePresentation.Bind(title, unit, rowName, name); CharacterAttributePresentation.Bind(number, unit, rowName, name);
        }
        var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill }; grid.AddThemeConstantOverride("h_separation", 24); basic.AddChild(grid);
        foreach (var stat in new[] { ("体魄", unit.physique, "PhysiqueRow"), ("灵慧", unit.intelligence, "IntelligenceRow"), ("神魂", unit.soul, "SoulRow"), ("福缘", unit.fortune, "FortuneRow"), ("悟性", unit.comprehension, "ComprehensionRow") })
            Stat(grid, stat.Item1, stat.Item2.ToString("0.0"), stat.Item3, true);
        basic.AddChild(new HSeparator()); basic.AddChild(CharacterText("社交属性", 23));
        foreach (var stat in new[] { ("魅力", "charisma", "CharismaRow"), ("交际", "social", "SocialRow"), ("治事", "management", "ManagementRow") }) Stat(basic, stat.Item1, data_number(unit, stat.Item2).ToString("0.0"), stat.Item3);
        foreach (var resource in new[] { ("生命", unit.current_health, unit.max_health, "HealthRow"), ("体力", unit.current_stamina, unit.max_stamina, "StaminaRow"), ("法力", unit.current_mana, unit.max_mana, "ManaRow") })
        {
            var row = new HBoxContainer { CustomMinimumSize = new Vector2(0, 48) }; row.AddThemeConstantOverride("separation", 12); battle.AddChild(row);
            var name = CharacterText(resource.Item1, 22); name.CustomMinimumSize = new Vector2(62, 0); name.MouseFilter = MouseFilterEnum.Stop; row.AddChild(name);
            var bar = WorldHudStyle.Bar(WorldHudStyle.StaminaInk); bar.MaxValue = Math.Max(1, resource.Item3); bar.Value = resource.Item2; row.AddChild(bar);
            var value = CharacterText($"{resource.Item2}/{resource.Item3}", 21); value.CustomMinimumSize = new Vector2(100, 0); value.HorizontalAlignment = HorizontalAlignment.Right; value.MouseFilter = MouseFilterEnum.Stop; row.AddChild(value);
            CharacterAttributePresentation.Bind(name, unit, resource.Item4, resource.Item1); CharacterAttributePresentation.Bind(value, unit, resource.Item4, resource.Item1);
            CharacterAttributePresentation.Bind(bar, unit, resource.Item4, resource.Item1);
        }
        battle.AddChild(new HSeparator()); var combatGrid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill }; combatGrid.AddThemeConstantOverride("h_separation", 16); battle.AddChild(combatGrid);
        foreach (var stat in new[] { ("速度", unit.initiative_speed.ToString("0.0"), "MindRow"), ("灵识", $"{unit.used_spirit_perception}/{unit.max_spirit_perception}", "SpiritPerceptionRow"), ("物理防御", unit.physical_defense.ToString(), "PhysicalDefenseRow"), ("精准", unit.precision.ToString(), "PrecisionRow"), ("身法", unit.dodge.ToString(), "DodgeRow"), ("会心", unit.critical.ToString(), "CriticalRow"), ("护心", unit.crit_resist.ToString(), "CritResistRow"), ("会心伤害", $"{unit.crit_damage_mult:0.00}倍", "CritDamageRow"), ("物理减伤", $"{unit.physical_damage_reduction * 100:0.#}%", "PhysicalReductionRow"), ("遁速", human?.dun_speed.ToString("0.#") ?? "未详", "TravelSpeedRow") }) Stat(combatGrid, stat.Item1, stat.Item2, stat.Item3);
        foreach (var stat in new[] { ("境界", unit.cultivation_realm, "RealmRow"), ("灵根", human?.spirit_root_name ?? "未详", "SpiritRootRow"), ("灵力", $"{unit.spirit_strands} 缕", "StrandsRow"), ("学习效率", human is null ? "—" : $"{human.get_learning_rate():0.00}/小时", "LearningRow"), ("修炼效率", human is null ? "—" : $"{human.get_cultivation_rate():0.000}/小时", "CultivationRow") }) Stat(cultivation, stat.Item1, stat.Item2, stat.Item3);
        cultivation.AddChild(new HSeparator()); cultivation.AddChild(CharacterText("灵根权重", 23));
        var data = human?.entity_data as HumanData; var roots = new[] { "金", "木", "水", "火", "土" };
        for (var i = 0; i < roots.Length; i++)
        {
            var value = data is not null && data.root_weights.Count > i ? data.root_weights[i].AsInt32() : 0;
            var row = new HBoxContainer { CustomMinimumSize = new Vector2(0, 34) }; row.AddThemeConstantOverride("separation", 18); cultivation.AddChild(row);
            var rootName = CharacterText(roots[i], 20); row.AddChild(rootName); var number = CharacterText(value.ToString(), 20); number.CustomMinimumSize = new Vector2(44, 0); row.AddChild(number);
            rootName.MouseFilter = MouseFilterEnum.Pass; number.MouseFilter = MouseFilterEnum.Pass;
            CharacterAttributePresentation.Bind(rootName, unit, "RootWeight:" + i, roots[i] + "灵根权重"); CharacterAttributePresentation.Bind(number, unit, "RootWeight:" + i, roots[i] + "灵根权重");
            var bar = WorldHudStyle.Bar(i == 3 ? new Color("a36150") : WorldHudStyle.StaminaInk); bar.Value = value; row.AddChild(bar);
        }
        var prof = CharacterBox(CharacterPanel(page, 0, 610, 1350, 185, 12)); prof.AddThemeConstantOverride("separation", 8);
        var heading = new HBoxContainer(); heading.AddThemeConstantOverride("separation", 12); prof.AddChild(heading);
        heading.AddChild(CharacterText("造诣", 27));
        var rows = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill }; rows.AddThemeConstantOverride("h_separation", 46); rows.AddThemeConstantOverride("v_separation", 4);
        void Refresh()
        {
            CharacterClear(rows);
            foreach (var entry in CharacterProficiencies.Where(entry => _proficiencyGroup == "全部" || entry.Group == _proficiencyGroup).Where(entry => CharacterMatches(entry.Name, _proficiencyQuery)))
            {
                var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 28) }; rows.AddChild(row);
                var label = CharacterText(entry.Name, 21); label.SizeFlagsHorizontal = SizeFlags.ExpandFill; label.MouseFilter = MouseFilterEnum.Stop; row.AddChild(label);
                var value = CharacterText(data_number(unit, entry.Key).ToString("0.0"), 21); value.MouseFilter = MouseFilterEnum.Stop; row.AddChild(value);
                CharacterAttributePresentation.Bind(label, unit, entry.Key, entry.Name); CharacterAttributePresentation.Bind(value, unit, entry.Key, entry.Name);
            }
            if (rows.GetChildCount() == 0) rows.AddChild(CharacterText("没有符合条件的造诣", 20, true));
        }
        var buttons = new List<Button>();
        foreach (var group in new[] { "全部", "武艺", "术法", "百工" })
        { var button = CharacterButton(group, 18, _proficiencyGroup == group, 6); button.CustomMinimumSize = new Vector2(64, 36); heading.AddChild(button); buttons.Add(button); button.Pressed += () => { _proficiencyGroup = group; foreach (var sibling in buttons) sibling.AddThemeStyleboxOverride("normal", CharacterButtonStyle(sibling == button, 6)); Refresh(); }; }
        var spacer = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill }; heading.AddChild(spacer);
        var search = CharacterSearch("搜索造诣"); search.SizeFlagsHorizontal = SizeFlags.ShrinkEnd; search.CustomMinimumSize = new Vector2(260, 38); heading.AddChild(search); search.Text = _proficiencyQuery;
        search.TextChanged += value => { _proficiencyQuery = value; Refresh(); }; CharacterScroll(prof).AddChild(rows); Refresh();
    }
}
