using Godot;

public partial class InfoPanel
{
    private ScrollContainer? _npcProbePage;
    private bool _npcAttributesSelected;

    public void show_npc_attributes(WorldNpcData npc)
    {
        show_npc(npc);
        if (!NpcProbeKnowledge.HasSnapshot(npc)) return;
        _npcAttributesSelected = true;
        render_npc_attribute_snapshot();
    }

    private void render_npc_tab(int page)
    {
        _npcAttributesSelected = page == 1 && _npc_card is { } npc && NpcProbeKnowledge.HasSnapshot(npc);
        render_npc_page();
    }

    private void render_npc_attribute_snapshot()
    {
        if (_npc_card is not { } npc || _characterCanvas is null) return;
        var snapshot = NpcProbeKnowledge.Snapshot(npc);
        if (snapshot.Count == 0) { _npcAttributesSelected = false; render_shared_npc_overview(); return; }
        show_redesigned_npc_shell(npc);
        foreach (var page in _characterPages.Values) page.Hide();
        if (_npcProbePage is null)
        {
            _npcProbePage = new ScrollContainer { Name = "NpcObservedAttributes", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
            _npcProbePage.AddThemeStyleboxOverride("panel", CharacterPaper(padding: 0));
            _characterCanvas.AddChild(_npcProbePage); CharacterRect(_npcProbePage, 232, 76, _characterCanvas.Size.X - 248, _characterCanvas.Size.Y - 96);
        }
        foreach (var child in _npcProbePage.GetChildren()) { _npcProbePage.RemoveChild(child); child.QueueFree(); }
        var margin = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        foreach (var edge in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride("margin_" + edge, 30);
        _npcProbePage.AddChild(margin);
        var box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        box.AddThemeConstantOverride("separation", 24); margin.AddChild(box);
        box.AddChild(CharacterText("探查所得", 32));
        box.AddChild(CharacterText("所见于 " + snapshot.GetValueOrDefault("date", "未详日期").AsString() + "；当前属性可能已经变化。", 20));
        string Number(string key) => snapshot.ContainsKey(key) ? snapshot[key].AsDouble().ToString("0.##") : "未探得";
        box.AddChild(CharacterText("境界  " + snapshot.GetValueOrDefault("realm", "未探得").AsString(), 24));
        box.AddChild(CharacterText($"气血  {Number("health")} / {Number("max_health")}　　寿元上限  {Number("max_age")}", 24));
        box.AddChild(CharacterText($"体魄  {Number("physique")}　　灵慧  {Number("intelligence")}　　神魂  {Number("soul")}", 28));
        box.AddChild(CharacterText("灵根  " + snapshot.GetValueOrDefault("root", "未探得").AsString() + "　　灵力  " + Number("strands") + "缕", 24));
        box.AddChild(CharacterText("福缘、悟性、魅力、交际、治事：未探得", 22));
        box.AddChild(CharacterText("功法、装备、心中所想和私人关系不在本次探查所得之中。", 20));
        _characterTitle!.Text = "人物 · 属性";
        _npcProbePage.Show();
    }
}
