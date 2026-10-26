using Godot;

public partial class InfoPanel
{
    private void build_character_equipment(Character? human)
    {
        var page = _characterPages[4]; CharacterClear(page); var data = human?.entity_data as HumanData;
        var forgedSword = human?.get_equipped_forged_sword();
        var attire = CharacterPanel(page, 0, 0, 770, 795); var canvas = new Control(); attire.AddChild(canvas);
        var portrait = new PortraitRenderer { MouseFilter = MouseFilterEnum.Ignore }; canvas.AddChild(portrait); CharacterRect(portrait, 176, 8, 386, 740);
        if (human is not null) portrait.show_character(human);
        var slots = new[]
        {
            (name: "武器", path: data?.weapon_path ?? "", slot: "weapon", mark: "兵", x: 8f, y: 110f),
            (name: "护具", path: data?.clothes_path ?? "", slot: "clothes", mark: "甲", x: 8f, y: 320f),
            (name: "器物一", path: data?.ornament_1_path ?? "", slot: "ornament_1", mark: "器", x: 576f, y: 110f),
            (name: "器物二", path: data?.ornament_2_path ?? "", slot: "ornament_2", mark: "器", x: 576f, y: 320f)
        };
        for (var i = 0; i < slots.Length; i++)
        {
            var entry = slots[i];
            var item = !string.IsNullOrWhiteSpace(entry.path) && ResourceLoader.Exists(entry.path) ? GD.Load<EquipmentData>(entry.path) : null;
            if (i == 0 && forgedSword is not null) item = new EquipmentData { entity_name = forgedSword.artifact_name, icon_path = ForgedArtifactInventory.IconPath(forgedSword) };
            var button = CharacterButton(""); button.Name = "EquipmentSlot" + i; canvas.AddChild(button); CharacterRect(button, entry.x, entry.y, 154, 176);
            var box = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center }; box.AddThemeConstantOverride("separation", 4); button.AddChild(box); box.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); box.OffsetLeft = 8; box.OffsetRight = -8; box.OffsetTop = 6; box.OffsetBottom = -6;
            if (item is not null && CharacterLoadIcon(item.icon_path) is { } icon) box.AddChild(CharacterIcon(icon, 56));
            else { var mark = CharacterText(entry.mark, 38, true); mark.HorizontalAlignment = HorizontalAlignment.Center; mark.Modulate = new Color(1, 1, 1, .45f); box.AddChild(mark); }
            var name = CharacterText(entry.name, 21); name.HorizontalAlignment = HorizontalAlignment.Center; box.AddChild(name);
            var state = CharacterClippedText(item?.entity_name ?? "空位", 17, true); state.HorizontalAlignment = HorizontalAlignment.Center; box.AddChild(state);
            if (item is not null) CharacterHover(button, item.entity_name, $"装备 · {entry.name}\n\n[b]属性加成[/b]\n{character_equipment_bonuses(item)}", CharacterLoadIcon(item.icon_path));
            if (i == 0 && forgedSword is not null) HoverInfoManager.SetInfo(button, ForgedArtifactInventory.Hover(forgedSword));
            button.Disabled = human is null;
            button.Pressed += () => { hide_tooltip(); if (human is not null) show_character_equipment_picker(human, entry.slot, entry.path); };
        }
        var clothing = !string.IsNullOrWhiteSpace(data?.portrait_clothing_item_path) && ResourceLoader.Exists(data.portrait_clothing_item_path)
            ? GD.Load<PortraitClothingData>(data.portrait_clothing_item_path) : null;
        var wardrobe = CharacterButton(""); wardrobe.Name = "EquipmentClothingSlot"; canvas.AddChild(wardrobe); CharacterRect(wardrobe, 8, 530, 154, 154);
        EquipmentTileContent(wardrobe, clothing?.icon_path ?? "", "衣着", clothing?.entity_name ?? "空位", 58);
        if (clothing is not null) CharacterHover(wardrobe, clothing.entity_name, "衣着", CharacterLoadIcon(clothing.icon_path));
        wardrobe.Pressed += () => { hide_tooltip(); if (human is not null) _wardrobe?.open_for(human); }; wardrobe.Disabled = human is null;
        build_equipment_totals(page, human);
        build_equipment_bonds(page, human);
        build_equipment_supplies(page, human);
    }

    private void show_character_equipment_picker(Character human, string slot, string current)
    {
        if (slot == "weapon") { show_character_weapon_picker(human, current); return; }
        var items = human.pack_items.Where(path => ResourceLoader.Exists(path)).Select(path => GD.Load<ItemData>(path)).OfType<EquipmentData>()
            .Where(item => slot == "weapon" ? item.equip_type == 0 : slot.StartsWith("ornament", StringComparison.Ordinal) ? item.equip_type == 2 : item.equip_type == 1).ToList();
        if (items.Count == 0 && string.IsNullOrEmpty(current))
        { show_character_reading(null, "暂无可用装备", "行囊中没有适用于此槽位的装备。"); return; }
        var choices = items.Select(item => item.entity_name);
        CharacterChoose("选择装备", string.IsNullOrEmpty(current) ? choices : choices.Prepend("卸下当前装备"), value =>
        {
            if (value == "卸下当前装备") human.unequip_inventory_item(current);
            else if (items.FirstOrDefault(item => item.entity_name == value) is { } item) human.equip_inventory_item(item.ResourcePath, slot);
            build_character_equipment(human); hide_tooltip();
        });
    }
    private void show_character_weapon_picker(Character human, string current)
    {
        var choices = new List<(string Name, Action Equip)>();
        var forged = human.get_equipped_forged_sword();
        if (forged is not null || current.Length > 0)
            choices.Add(("卸下当前装备", () => { if (forged is not null) human.unequip_artifact(forged.instance_id); else human.unequip_inventory_item(current); }));
        foreach (var path in human.pack_items.Distinct())
            if (ResourceLoader.Exists(path) && GD.Load<ItemData>(path) is EquipmentData { equip_type: 0 } item)
                choices.Add((item.entity_name, () => human.equip_inventory_item(path, "weapon")));
        foreach (var item in human.get_artifact_items().Where(ForgedSwordBattleAdapter.IsSword))
            choices.Add(($"{item.artifact_name} · {item.artifact_tier} · {item.instance_id[^Math.Min(6, item.instance_id.Length)..]}", () =>
            { if (!human.is_artifact_equipped(item.instance_id)) human.equip_artifact(item.instance_id); }));
        if (choices.Count == 0) { show_character_reading(null, "暂无可用装备", "行囊中没有剑器或其他武器。"); return; }
        CharacterChoose("选择武器", choices.Select(entry => entry.Name), value =>
        { choices.First(entry => entry.Name == value).Equip(); build_character_equipment(human); hide_tooltip(); });
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
        var items = human.get_artifact_items().Where(item => !ForgedArtifactInventory.IsForged(item) && !human.is_artifact_equipped(item.instance_id)).ToList();
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
