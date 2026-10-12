using Godot;

public partial class InfoPanel
{
    private string _relationQuery = "", _relationFilter = "全部", _relationSelected = "";

    private void build_character_relations()
    {
        var page = _characterPages[5]; CharacterClear(page);
        var list = CharacterBox(CharacterPanel(page, 0, 0, 450, 795), "关系名录");
        var search = CharacterSearch("搜索已记录人物"); search.Text = _relationQuery; list.AddChild(search);
        var filters = new HBoxContainer(); filters.AddThemeConstantOverride("separation", 10); list.AddChild(filters);
        var people = CharacterBox(CharacterScroll(list));
        var details = CharacterBox(CharacterPanel(page, 468, 0, 882, 795), "关系详情");
        var all = GetNodeOrNull<NpcWorldManager>("/root/NpcWorldManager")?.npcs.Where(NpcSocialRules.KnowsName).ToList() ?? [];
        void Select(WorldNpcData? npc)
        {
            foreach (var child in details.GetChildren().Skip(2).ToList()) { details.RemoveChild(child); child.QueueFree(); }
            if (npc is null)
            {
                var empty = CharacterText("尚无往来记录", 32); empty.CustomMinimumSize = new Vector2(0, 150); empty.HorizontalAlignment = HorizontalAlignment.Center; details.AddChild(empty);
                var hint = CharacterText("结识人物后，可在此查看关系与往来。", 23, true); hint.HorizontalAlignment = HorizontalAlignment.Center; details.AddChild(hint);
                var bridge = new TextureRect { Texture = GD.Load<Texture2D>("res://textures/ui/character_20261012/relations_bridge_v1.png"),
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                    CustomMinimumSize = new Vector2(0, 270), Modulate = new Color(1, 1, 1, .52f), MouseFilter = MouseFilterEnum.Ignore };
                details.AddChild(bridge);
                details.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });
                details.AddChild(CharacterText("往来纪事", 26)); details.AddChild(new HSeparator()); details.AddChild(CharacterText("暂无记录", 21, true)); return;
            }
            _relationSelected = npc.npc_id;
            var profile = new HBoxContainer(); profile.AddThemeConstantOverride("separation", 24); details.AddChild(profile);
            var portrait = new PortraitRenderer { CustomMinimumSize = new Vector2(210, 270), MouseFilter = MouseFilterEnum.Ignore }; profile.AddChild(portrait); portrait.show_world_npc(npc);
            var facts = CharacterBox(profile); facts.AddChild(CharacterText(npc.npc_name, 30));
            facts.AddChild(CharacterParagraph($"关系：{npc.player_relationship}\n状态：{(npc.is_alive ? "在世" : "已故")}\n{(npc.relation_tags.Count > 0 ? string.Join("、", npc.relation_tags) : "")}", 22));
            facts.AddChild(CharacterText("仅展示已有交往记录。", 18, true));
            details.AddChild(new HSeparator()); details.AddChild(CharacterText("往来纪事", 26));
            var history = CharacterBox(CharacterScroll(details));
            var entries = npc.action_history.Where(record => record.target_npc_id == "player" || record.action_id.StartsWith("player_social_", StringComparison.Ordinal)).Reverse().ToList();
            foreach (var record in entries)
            {
                var panel = new PanelContainer(); panel.AddThemeStyleboxOverride("panel", CharacterStyle(false, 12)); history.AddChild(panel);
                var box = CharacterBox(panel); box.AddChild(CharacterParagraph($"{record.date_label}  {record.action_name}", 22)); box.AddChild(CharacterParagraph(record.outcome, 20, true));
            }
            if (entries.Count == 0) history.AddChild(CharacterText("暂无往来纪事", 22, true));
        }
        void Refresh()
        {
            CharacterClear(people);
            var shown = all.Where(npc => CharacterMatches(npc.npc_name, _relationQuery))
                .Where(npc => _relationFilter != "亲缘" || npc.relation_tags.Contains("亲缘")).ToList();
            foreach (var npc in shown)
            {
                var button = CharacterButton(npc.npc_name + "   ›", selected: npc.npc_id == _relationSelected); button.CustomMinimumSize = new Vector2(0, 64); people.AddChild(button);
                button.Pressed += () => { _relationSelected = npc.npc_id; Refresh(); };
            }
            if (shown.Count == 0)
            {
                people.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });
                var sheet = GD.Load<Texture2D>("res://textures/ui/infopanel_nav_icons_ink_v1.png");
                var icon = CharacterIcon(new AtlasTexture { Atlas = sheet, Region = new Rect2(1024, 512, 512, 512) }, 120); icon.Modulate = new Color(1, 1, 1, .38f); people.AddChild(icon);
                var empty = CharacterText(all.Count == 0 ? "暂无已记录人物" : "没有符合条件的人物", 23, true); empty.HorizontalAlignment = HorizontalAlignment.Center; people.AddChild(empty);
                people.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });
            }
            Select(shown.FirstOrDefault(npc => npc.npc_id == _relationSelected) ?? shown.FirstOrDefault());
        }
        foreach (var filter in new[] { "全部", "亲缘", "往来" })
        {
            var button = CharacterButton(filter, selected: filter == _relationFilter); button.SizeFlagsHorizontal = SizeFlags.ExpandFill; filters.AddChild(button);
            button.Pressed += () => { _relationFilter = filter; foreach (var sibling in filters.GetChildren().OfType<Button>()) sibling.AddThemeStyleboxOverride("normal", CharacterStyle(sibling == button, 10)); Refresh(); };
        }
        search.TextChanged += value => { _relationQuery = value; Refresh(); }; Refresh();
    }

    private void show_redesigned_npc_shell(WorldNpcData npc)
    {
        if (_characterCanvas is null) return;
        foreach (var page in _characterPages) page.Value.Visible = page.Key == 0;
        _characterTitle!.Text = "概览"; _characterName!.Text = npc.npc_name;
        _characterRealm!.Text = "人物档案"; _characterRoot!.Text = npc.species;
        for (var i = 0; i < _characterTabs.Count; i++) _characterTabs[i].Visible = i == 0;
    }
}
