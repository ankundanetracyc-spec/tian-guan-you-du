using Godot;

public partial class BattleFlowView
{
    private void Supplies(BattleEntityData owner, Action back, int selected = 0)
    {
        BattleSupplyRules.Normalize(owner.battle_supplies, owner.pack_item_paths, owner.pack_item_counts);
        Clear(owner.entity_name + " · 战备准备");
        _hint.Text = "从本人行囊编入；每位最多5颗同种同档丹药，战斗中实际生效才消耗。";
        for (var i = 0; i < BattleSupplyRules.SlotCount; i++)
        {
            var index = i;
            var slot = owner.battle_supplies[i];
            var pill = BattleSupplyRules.Load(slot.item_path);
            var card = Panel(_body, new(12 + i * 460, 12), new(444, 194));
            if (i == selected) card.AddThemeStyleboxOverride("panel", new StyleBoxFlat {
                BgColor = new Color("29413e"), BorderColor = BattleUiArt.Accent,
                BorderWidthLeft = 2, BorderWidthRight = 2, BorderWidthTop = 2, BorderWidthBottom = 2 });
            Text(card, $"战备位 {i + 1}" + (i == selected ? " · 当前选择" : ""), new(14, 8), new(416, 28), 19);
            Text(card, pill is null ? "未编入" : $"{BattleSupplyRules.Name(pill)} · {BattleSupplyRules.QualityNames[pill.efficacy_tier]}   ×{slot.count}", new(90, 52), new(338, 44), 21);
            if (pill is not null)
            {
                var icon = new TextureRect { Position = new(16, 44), Size = new(64, 64), Texture = GD.Load<Texture2D>(pill.icon_path),
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                    MouseFilter = Control.MouseFilterEnum.Ignore };
                card.AddChild(icon); AlchemyPillVisual.Apply(icon, pill.efficacy_tier, pill.pill_id);
            }
            var controls = new HBoxContainer { Position = new(14, 132), Size = new(416, 44) };
            controls.AddThemeConstantOverride("separation", 8); card.AddChild(controls);
            var choose = Button(controls, "选择此位", () => Supplies(owner, back, index)); choose.CustomMinimumSize = new(122, 40);
            var less = Button(controls, "－", () => { BattleSupplyRules.Set(owner, index, slot.item_path, slot.count - 1); Supplies(owner, back, index); }); less.CustomMinimumSize = new(58, 40); less.Disabled = pill is null;
            var more = Button(controls, "＋", () => { BattleSupplyRules.Set(owner, index, slot.item_path, slot.count + 1); Supplies(owner, back, index); }); more.CustomMinimumSize = new(58, 40);
            more.Disabled = pill is null || slot.count >= 5 || BattleSupplyRules.PackCount(owner.pack_item_paths, owner.pack_item_counts, slot.item_path) <= BattleSupplyRules.Reserved(owner.battle_supplies, slot.item_path);
            var clear = Button(controls, "清空", () => { BattleSupplyRules.Set(owner, index, "", 0); Supplies(owner, back, index); }); clear.CustomMinimumSize = new(98, 40); clear.Disabled = pill is null;
        }
        Text(_body, $"本人的行囊 · 点击丹药编入战备位 {selected + 1}", new(20, 220), new(1340, 32), 21);
        var scroll = Scroll(_body, new(12, 260), new(1368, 364));
        var list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", 10); scroll.AddChild(list);
        var candidates = owner.pack_item_paths.Distinct().Where(path => BattleSupplyRules.Load(path) is not null &&
            BattleSupplyRules.PackCount(owner.pack_item_paths, owner.pack_item_counts, path) > 0).ToList();
        foreach (var path in candidates)
        {
            var pill = BattleSupplyRules.Load(path)!;
            var total = BattleSupplyRules.PackCount(owner.pack_item_paths, owner.pack_item_counts, path);
            var available = Math.Max(0, total - BattleSupplyRules.Reserved(owner.battle_supplies, path, selected));
            var item = Button(list, $"{BattleSupplyRules.Name(pill)} · {BattleSupplyRules.QualityNames[pill.efficacy_tier]}    持有 {total} 颗    此位可编入 {Math.Min(5, available)} 颗",
                () => { BattleSupplyRules.Set(owner, selected, path, 5); Supplies(owner, back, selected); });
            item.CustomMinimumSize = new(1330, 62); item.Disabled = available <= 0;
            var info = AlchemyPillVisual.Hover(pill);
            HoverInfoManager.SetInfo(item, info with
            {
                Body = info.Body + "\n\n只从本人真实行囊配置，每位最多5颗同种同档丹药。",
                Sections = null,
            });
        }
        if (candidates.Count == 0) Text(list, "本人的行囊中没有可用的战斗丹药。", Vector2.Zero, new(1300, 64), 20);
        Button(_buttons, "完成战备配置", back);
    }
}
