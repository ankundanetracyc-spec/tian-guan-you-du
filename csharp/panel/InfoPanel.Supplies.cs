using Godot;

public partial class InfoPanel
{
    private static void EquipmentTileContent(Button tile, string iconPath, string title, string subtitle, int iconSize)
    {
        var box = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        box.AddThemeConstantOverride("separation", 3); tile.AddChild(box);
        box.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); box.OffsetLeft = 8; box.OffsetRight = -8; box.OffsetTop = 8; box.OffsetBottom = -8;
        if (CharacterLoadIcon(iconPath) is { } icon) box.AddChild(CharacterIcon(icon, iconSize));
        else { var mark = CharacterText("＋", iconSize - 8, true); mark.HorizontalAlignment = HorizontalAlignment.Center; box.AddChild(mark); }
        var name = CharacterClippedText(title, 20); name.HorizontalAlignment = HorizontalAlignment.Center; box.AddChild(name);
        var note = CharacterClippedText(subtitle, 16, true); note.HorizontalAlignment = HorizontalAlignment.Center; box.AddChild(note);
    }

    private void build_equipment_supplies(Control page, Character? human)
    {
        var panel = CharacterPanel(page, 788, 600, 594, 195); panel.Name = "EquipmentSuppliesPanel";
        var canvas = new Control(); panel.AddChild(canvas);
        var title = CharacterText("随身战备", 26); canvas.AddChild(title); CharacterRect(title, 0, 0, 300, 34);
        var locked = human?.HasMeta("artifact_battle_clone") == true;
        if (human is not null)
        {
            human.get_inventory_snapshot();
            BattleSupplyRules.Normalize(human.battle_supplies, human.pack_items, human.pack_counts);
        }
        for (var i = 0; i < BattleSupplyRules.SlotCount; i++)
        {
            var index = i;
            var slot = human is not null ? human.battle_supplies[i] : null;
            var pill = BattleSupplyRules.Load(slot?.item_path ?? "");
            var tile = CharacterButton(""); tile.Name = $"EquipmentSupplySlot{i}"; canvas.AddChild(tile); CharacterRect(tile, i * 180, 44, 168, 116);
            EquipmentTileContent(tile, pill?.icon_path ?? "", pill is null ? $"战备位 {i + 1}" : BattleSupplyRules.Name(pill),
                pill is null ? "未编入" : BattleSupplyRules.QualityNames[pill.efficacy_tier], 42);
            if (pill is not null)
            {
                var icon = tile.FindChildren("*", "TextureRect", true, false).OfType<TextureRect>().FirstOrDefault();
                if (icon is not null) AlchemyPillVisual.Apply(icon, pill.efficacy_tier, pill.pill_id);
                var count = CharacterText($"×{slot!.count}", 18); count.MouseFilter = MouseFilterEnum.Ignore;
                tile.AddChild(count); CharacterRect(count, 118, 6, 42, 24); count.HorizontalAlignment = HorizontalAlignment.Right;
                var info = AlchemyPillVisual.Hover(pill, user: human);
                HoverInfoManager.SetInfo(tile, info with
                {
                    Body = info.Body + $"\n\n已编入{slot.count}颗 · 本人使用。\n每次占1行动位，成功生效才消耗。",
                    Sections = null,
                });
            }
            else CharacterHover(tile, $"战备位 {i + 1}", "丹药与消耗型符箓共用三个战备位，每位最多5件同种同档物品。当前支持回春丹、通脉丹，从本人行囊配置。");
            tile.Disabled = human is null; // 战斗中仍可悬停，点击不打开编辑器。
            tile.Pressed += () => { if (human is not null && !locked) show_equipment_supply_actions(human, index); };
        }
    }

    private void show_equipment_supply_actions(Character human, int index)
    {
        if (human.HasMeta("artifact_battle_clone")) return;
        BattleSupplyRules.Normalize(human.battle_supplies, human.pack_items, human.pack_counts);
        var slot = human.battle_supplies[index];
        if (BattleSupplyRules.Load(slot.item_path) is null) { show_equipment_supply_picker(human, index); return; }
        var actions = new List<string> { "更换物品" };
        if (slot.count < 5 && BattleSupplyRules.PackCount(human.pack_items, human.pack_counts, slot.item_path) > BattleSupplyRules.Reserved(human.battle_supplies, slot.item_path)) actions.Add("增加1颗");
        if (slot.count > 1) actions.Add("减少1颗");
        actions.Add("移除此位物品");
        CharacterChoose($"战备位 {index + 1}", actions, action =>
        {
            if (action == "更换物品") { show_equipment_supply_picker(human, index); return; }
            human.set_battle_supply(index, slot.item_path, action == "移除此位物品" ? 0 : slot.count + (action == "增加1颗" ? 1 : -1));
            build_character_equipment(human);
        });
    }

    private void show_equipment_supply_picker(Character human, int index)
    {
        if (human.HasMeta("artifact_battle_clone")) return;
        human.get_inventory_snapshot();
        var candidates = human.pack_items.Distinct().Where(path => BattleSupplyRules.Load(path) is not null)
            .Select(path => (Path: path, Pill: BattleSupplyRules.Load(path)!, Available: BattleSupplyRules.PackCount(human.pack_items, human.pack_counts, path) - BattleSupplyRules.Reserved(human.battle_supplies, path, index)))
            .Where(entry => entry.Available > 0).ToList();
        var options = candidates.Select(entry => $"{BattleSupplyRules.Name(entry.Pill)} · {BattleSupplyRules.QualityNames[entry.Pill.efficacy_tier]}　可编入{Math.Min(5, entry.Available)}颗").ToList();
        if (options.Count == 0) options.Add("暂无可编入的丹药");
        CharacterChoose($"选择战备位 {index + 1} 的物品", options, value =>
        {
            var choice = options.IndexOf(value);
            if (choice >= 0 && choice < candidates.Count && human.set_battle_supply(index, candidates[choice].Path, 5)) build_character_equipment(human);
        });
        var buttons = _gongfaModalChoices!.FindChildren("*", "Button", true, false).OfType<Button>().ToList();
        foreach (var button in buttons)
        {
            var choice = options.IndexOf(button.Text);
            if (choice < 0 || choice >= candidates.Count) { button.Disabled = true; continue; }
            var entry = candidates[choice];
            HoverInfoManager.SetInfo(button, AlchemyPillVisual.Hover(entry.Pill, user: human));
        }
    }
}
