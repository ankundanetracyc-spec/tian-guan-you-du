using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class InfoPanel
{
    private string _relationQuery = "", _relationFilter = "全部", _relationSelected = "";

    public void refresh_relationships()
    {
        if (!Visible || _characterCanvas is null || current_card is not Character character || character.entity_data is not HumanData)
            return;
        build_character_relations();
    }

    private void build_character_relations()
    {
        var page = _characterPages[6]; CharacterClear(page);
        var list = CharacterBox(CharacterPanel(page, 0, 0, 450, 795), "关系名录");
        var search = CharacterSearch("搜索已记录人物"); search.Text = _relationQuery; list.AddChild(search);
        var filters = new HBoxContainer(); filters.AddThemeConstantOverride("separation", 10); list.AddChild(filters);
        var people = CharacterBox(CharacterScroll(list));
        var details = CharacterBox(CharacterPanel(page, 468, 0, 914, 795), "关系详情");
        var playerData = current_card is Character playerCharacter ? playerCharacter.entity_data as HumanData : null;
        var knownIds = playerData?.directed_relationships.Keys.Select(key => key.AsString()).ToHashSet(StringComparer.Ordinal) ?? new HashSet<string>(StringComparer.Ordinal);
        var all = GetNodeOrNull<NpcWorldManager>("/root/NpcWorldManager")?.npcs
            .Where(npc => npc.player_known_npc_info.ContainsKey("name") || knownIds.Contains(npc.npc_id)).ToList() ?? [];
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
            var ownRelation = playerData is null ? null : CharacterRelationshipRules.Get(playerData, npc.npc_id);
            facts.AddChild(CharacterParagraph($"你对其的私交\n亲近：{CharacterRelationshipRules.StageLabel(CharacterRelationshipRules.Closeness, ownRelation?.closeness ?? 0)}\n信任：{CharacterRelationshipRules.StageLabel(CharacterRelationshipRules.Trust, ownRelation?.trust ?? 0)}\n敬重：{CharacterRelationshipRules.StageLabel(CharacterRelationshipRules.Respect, ownRelation?.respect ?? 0)}\n怨结：{CharacterRelationshipRules.StageLabel(CharacterRelationshipRules.Resentment, ownRelation?.resentment ?? 0)}\n状态：{(npc.is_alive ? "在世" : "已故")}", 22));
            facts.AddChild(CharacterParagraph("关系阶段记录你这一方的感受；下方仅列对方实际见闻留下的线索。", 18, true));
            var impression = NpcPlayerImpressionRules.DescribeForPlayer(npc);
            details.AddChild(new HSeparator()); details.AddChild(CharacterText("对方对你的大致印象", 24));
            details.AddChild(CharacterParagraph($"目前倾向：{impression.Summary}", 20, true));
            if (impression.Clues.Count > 0)
                details.AddChild(CharacterParagraph(string.Join("\n", impression.Clues.Take(3).Select(clue => "· " + clue)), 18, true));
            if (!string.IsNullOrWhiteSpace(impression.RecentSource))
                details.AddChild(CharacterParagraph($"最近依据：{impression.RecentSource} · {impression.RecentDate}\n{impression.RecentClue}", 18, true));
            details.AddChild(new HSeparator()); details.AddChild(CharacterText("待月结记事", 24));
            var pendingScroll = new ScrollContainer
            {
                CustomMinimumSize = new Vector2(0, 90), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
                VerticalScrollMode = ScrollContainer.ScrollMode.Auto
            };
            details.AddChild(pendingScroll);
            var pendingList = CharacterBox(pendingScroll);
            var pending = ownRelation?.pending_evidence.Where(evidence => evidence is not null)
                .OrderByDescending(evidence => evidence.month_index).ThenByDescending(evidence => evidence.event_id, StringComparer.Ordinal).ToList() ?? [];
            if (pending.Count == 0)
                pendingList.AddChild(CharacterText("暂无待归纳记录", 19, true));
            else
                foreach (var evidence in pending)
                {
                    var when = evidence.month_index < 0 ? "待定日期" : $"{evidence.month_index / 12 + 1}年{evidence.month_index % 12 + 1}月";
                    pendingList.AddChild(CharacterParagraph($"{when}　{CharacterRelationshipRules.DescribePendingEvidence(ownRelation!, evidence)}", 19, true));
                }
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
                .Where(npc => _relationFilter != "亲近" || (playerData is not null && CharacterRelationshipRules.Value(playerData, npc.npc_id, CharacterRelationshipRules.Closeness) >= 30)).ToList();
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
        foreach (var filter in new[] { "全部", "亲近" })
        {
            var button = CharacterButton(filter, selected: filter == _relationFilter); button.SizeFlagsHorizontal = SizeFlags.ExpandFill; filters.AddChild(button);
            button.Pressed += () => { _relationFilter = filter; foreach (var sibling in filters.GetChildren().OfType<Button>()) PaperButtonFeedback.Apply(sibling, fontSize: sibling.GetThemeFontSize("font_size"), selected: sibling == button); Refresh(); };
        }
        search.TextChanged += value => { _relationQuery = value; Refresh(); }; Refresh();
    }

    private void show_redesigned_npc_shell(WorldNpcData npc)
    {
        if (_characterCanvas is null) return;
        foreach (var page in _characterPages) page.Value.Visible = page.Key == 0;
        _characterTitle!.Text = "人物 · " + (_npcAttributesSelected ? "属性" : "概览"); _characterName!.Text = npc.npc_name;
        _characterRealm!.Text = "人物档案"; _characterRoot!.Text = npc.species;
        _characterName.Visible = _characterRealm.Visible = _characterRoot.Visible = _npcAttributesSelected;
        if (_paperInventory is not null) _paperInventory.Hide();
        update_character_paper_navigation(_npcAttributesSelected ? 1 : 0, true);
    }
}
