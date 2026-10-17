using Godot;

public partial class InfoPanel
{
    private void build_character_equipment(Character? human)
    {
        var page = _characterPages[4]; CharacterClear(page); var data = human?.entity_data as HumanData;
        var attire = CharacterPanel(page, 0, 0, 675, 620); var canvas = new Control(); attire.AddChild(canvas);
        var portrait = new PortraitRenderer { MouseFilter = MouseFilterEnum.Ignore }; canvas.AddChild(portrait); CharacterRect(portrait, 150, 48, 340, 444);
        if (human is not null) portrait.show_character(human);
        var title = CharacterText("常规穿戴", 26); canvas.AddChild(title); CharacterRect(title, 2, 0, 220, 38);
        var slots = new[]
        {
            (name: "头冠", path: data?.headwear_path ?? "", slot: "headwear", x: 6f, y: 58f),
            (name: "武器", path: data?.weapon_path ?? "", slot: "weapon", x: 6f, y: 202f),
            (name: "衣甲", path: data?.clothes_path ?? "", slot: "clothes", x: 6f, y: 346f),
            (name: "饰品一", path: data?.ornament_1_path ?? "", slot: "ornament_1", x: 498f, y: 58f),
            (name: "饰品二", path: data?.ornament_2_path ?? "", slot: "ornament_2", x: 498f, y: 202f),
            (name: "足履", path: data?.footwear_path ?? "", slot: "footwear", x: 498f, y: 346f)
        };
        foreach (var entry in slots)
        {
            var item = !string.IsNullOrWhiteSpace(entry.path) && ResourceLoader.Exists(entry.path) ? GD.Load<EquipmentData>(entry.path) : null;
            var button = CharacterButton("", selected: item is not null); canvas.AddChild(button); CharacterRect(button, entry.x, entry.y, 142, 132);
            var box = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center }; box.AddThemeConstantOverride("separation", 4); button.AddChild(box); box.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); box.OffsetLeft = 8; box.OffsetRight = -8; box.OffsetTop = 6; box.OffsetBottom = -6;
            if (item is not null) box.AddChild(CharacterIcon(CharacterLoadIcon(item.icon_path), 52));
            var name = CharacterText(entry.name, 21); name.HorizontalAlignment = HorizontalAlignment.Center; box.AddChild(name);
            var state = CharacterClippedText(item?.entity_name ?? "尚未穿戴", 17, true); state.HorizontalAlignment = HorizontalAlignment.Center; box.AddChild(state);
            if (item is not null) CharacterHover(button, item.entity_name, $"装备 · {entry.name}\n\n[b]属性加成[/b]\n{character_equipment_bonuses(item)}\n\n[b]穿戴位置[/b]\n{entry.name}", CharacterLoadIcon(item.icon_path));
            button.Pressed += () => { if (human is not null) show_character_equipment_picker(human, entry.slot, entry.path); };
        }
        var wardrobe = CharacterButton("衣着 · 更换常服  ›", 23); canvas.AddChild(wardrobe); CharacterRect(wardrobe, 150, 506, 340, 46);
        wardrobe.Pressed += () => { hide_tooltip(); if (human is not null) _wardrobe?.open_for(human); }; wardrobe.Disabled = human is null;
        var artifacts = CharacterBox(CharacterPanel(page, 691, 0, 659, 620), "法宝");
        artifacts.AddChild(CharacterText($"灵识占用    {human?.used_spirit_perception ?? 0} / {human?.max_spirit_perception ?? 0}", 23));
        var bar = WorldHudStyle.Bar(WorldHudStyle.StaminaInk); bar.MaxValue = Math.Max(1, human?.max_spirit_perception ?? 0); bar.Value = human?.used_spirit_perception ?? 0; artifacts.AddChild(bar);
        var artifactRows = CharacterBox(CharacterScroll(artifacts));
        var equipped = human?.get_artifact_items().Where(item => human.is_artifact_equipped(item.instance_id)).ToList() ?? [];
        for (var i = 0; i < Math.Max(3, Math.Max(data?.artifact_slot_count ?? 0, equipped.Count)); i++)
        {
            var index = i; var unlocked = i < (data?.artifact_slot_count ?? 0); var artifact = i < equipped.Count ? equipped[i] : null;
            var button = CharacterButton("", selected: artifact is not null); button.CustomMinimumSize = new Vector2(0, 96); artifactRows.AddChild(button);
            var text = artifact is not null ? artifact.artifact_name : unlocked ? "空闲" : "未解锁";
            var content = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore }; content.AddThemeConstantOverride("separation", 6);
            button.AddChild(content); content.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); content.OffsetLeft = 18; content.OffsetRight = -18; content.OffsetTop = 10; content.OffsetBottom = -10;
            content.AddChild(CharacterClippedText($"法宝位{chinese_volume_number(i + 1)}    {text}", 23));
            content.AddChild(CharacterClippedText(artifact is null ? unlocked ? "＋ 选择法宝" : "随实际槽位开放" : $"{artifact.ritual_mode} · 灵识 {(artifact.ritual_mode == "内炼" ? 0 : artifact.spirit_cost)} · 耐久 {artifact.current_durability}/{artifact.max_durability}", 18, true));
            button.Disabled = !unlocked;
            if (artifact is not null) CharacterHover(button, artifact.artifact_name, character_artifact_detail(artifact), CharacterLoadIcon(artifact.form?.icon_path ?? ""));
            button.Pressed += () => { if (human is not null && unlocked) show_character_artifact_picker(human, artifact); };
        }
        artifacts.AddChild(CharacterText("内炼不占灵识；外祭召出后占用，离场释放。", 18, true));
        var supply = CharacterBox(CharacterPanel(page, 0, 638, 1350, 157), "战备");
        var supplies = new HBoxContainer(); supplies.AddThemeConstantOverride("separation", 48); supply.AddChild(supplies);
        foreach (var category in new[] { "丹药", "符箓" })
        {
            var group = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; group.AddThemeConstantOverride("separation", 16); supplies.AddChild(group);
            group.AddChild(CharacterText(category + "栏", 23));
            var notice = CharacterText("尚未配置", 20, true); group.AddChild(notice);
            CharacterHover(notice, category + "栏", "战备栏配置尚未接入，物品仍在行囊中查看与操作。");
        }
    }

    private void show_character_equipment_picker(Character human, string slot, string current)
    {
        var items = human.pack_items.Where(path => ResourceLoader.Exists(path)).Select(path => GD.Load<ItemData>(path)).OfType<EquipmentData>()
            .Where(item => slot == "weapon" ? item.equip_type == 0 : slot.StartsWith("ornament", StringComparison.Ordinal) ? item.equip_type == 2 : item.equip_type == 1).ToList();
        CharacterChoose("选择穿戴", items.Select(item => item.entity_name).Prepend("卸下当前装备"), value =>
        {
            if (value == "卸下当前装备") human.unequip_inventory_item(current);
            else if (items.FirstOrDefault(item => item.entity_name == value) is { } item) human.equip_inventory_item(item.ResourcePath, slot);
            build_character_equipment(human); hide_tooltip();
        });
    }

    private static string character_equipment_bonuses(EquipmentData item)
    {
        var lines = new List<string>();
        foreach (var key in item.fixed_bonuses.Keys)
        { var value = item.fixed_bonuses[key].AsSingle(); lines.Add($"{battle_attribute_name(key.AsString())} {(value >= 0 ? "+" : "")}{value:0.#}"); }
        foreach (var key in item.mult_bonuses.Keys)
        { var value = item.mult_bonuses[key].AsSingle(); lines.Add($"{battle_attribute_name(key.AsString())} {(value >= 0 ? "+" : "")}{value * 100:0.#}%"); }
        return lines.Count == 0 ? "无直接属性加成" : string.Join("\n", lines);
    }

    private static string character_artifact_detail(ArtifactInstanceData item) =>
        $"{item.artifact_tier} · {item.form?.form_name ?? "法宝"}\n构型：{item.ritual_mode}\n本行：{item.element}　阴阳：{item.polarity}\n功用：{item.utility_id} · {item.utility_grade} 品\n灵识：{(item.ritual_mode == "内炼" ? "内炼不占用" : $"在场占用{item.spirit_cost}")}\n耐久：{item.current_durability}/{item.max_durability}\n器体：{item.outer_body}\n外祭耗费：体力 {item.deploy_energy_cost} · 法力 {item.deploy_mana_cost}\n\n铭文：{(item.inscriptions.Count == 0 ? "无" : string.Join("、", item.inscriptions.Select(value => value.display_name)))}";

    private void show_character_artifact_picker(Character human, ArtifactInstanceData? current)
    {
        var items = human.get_artifact_items().Where(item => !human.is_artifact_equipped(item.instance_id)).ToList();
        var names = items.Select(item => $"{item.artifact_name} · {item.ritual_mode} · 灵识{item.spirit_cost} · {item.instance_id[^Math.Min(4, item.instance_id.Length)..]}").ToList();
        IEnumerable<string> choices = names.Prepend("卸下当前法宝");
        if (current?.ritual_mode == "内炼" && ArtifactSkillFactory.IsConfigured(current)) choices = choices.Prepend("调整化法目标");
        CharacterChoose("选择法宝", choices, value =>
        {
            if (value == "调整化法目标" && current is not null) { show_inner_artifact_binding(human, current); return; }
            ArtifactInstanceData? bindingItem = null;
            if (value == "卸下当前法宝") { if (current is not null) human.unequip_artifact(current.instance_id); }
            else
            {
                var index = names.IndexOf(value); if (index < 0) return;
                if (current is not null) human.unequip_artifact(current.instance_id);
                if (!human.equip_artifact(items[index].instance_id))
                { if (current is not null) human.equip_artifact(current.instance_id); show_character_reading(null, "无法装配", "请检查法器耐久、绑定冲突与可用法器位；战斗中不可换装。"); }
                else if (items[index].ritual_mode == "内炼" && ArtifactSkillFactory.IsConfigured(items[index])) bindingItem = items[index];
            }
            build_character_equipment(human);
            if (bindingItem is not null) show_inner_artifact_binding(human, bindingItem);
        });
    }
    private void show_inner_artifact_binding(Character human, ArtifactInstanceData item)
    {
        var paths = human.get_default_combat_skill_paths().Concat(human.get_available_battle_skills().Select(Character.combat_skill_key))
            .Where(path => !path.StartsWith("artifact:") && human.is_valid_artifact_binding(item, path)).Distinct().ToList();
        var names = paths.Select(path => human.resolve_combat_skill(path)?.skill_name ?? path).ToList();
        CharacterChoose("选择" + item.artifact_name + "化法目标", names, value =>
        {
            var index = names.IndexOf(value);
            if (index >= 0 && !human.bind_inner_artifact(item.instance_id, paths[index]))
                show_character_reading(null, "无法绑定", "该技能已关联其他内炼法器，或来源已失效。");
            build_character_equipment(human);
        });
    }

}
