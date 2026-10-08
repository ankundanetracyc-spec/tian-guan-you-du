using Godot;
using Godot.Collections;

/// <summary>通用的横向探索路线。节点始终保留在图上，用明暗表示是否可前往。</summary>
public partial class DungeonExploreUI : Control
{
    [Signal] public delegate void battle_requestedEventHandler(string enemy_id, int enemy_count, bool elite);
    [Signal] public delegate void resource_collectedEventHandler(string resource, int amount);
    [Signal] public delegate void exploration_progressedEventHandler(int amount);
    [Signal] public delegate void exploration_finishedEventHandler();
    [Signal] public delegate void event_requestedEventHandler(string id, string title);

    private DungeonGraph? graph;
    private int current = -1, pending = -1, layer;
    private readonly HashSet<int> complete = [];
    private readonly HashSet<int> available = [];
    private Control? container;
    private Button? action;
    private Label? area_name, area_desc, layer_label, node_title, node_desc;

    public override void _Ready()
    {
        AddToGroup("map_overlay_blocker");
        container = GetNodeOrNull<Control>("%GraphContainer"); action = GetNodeOrNull<Button>("%ActionBtn");
        area_name = GetNodeOrNull<Label>("%AreaNameLabel"); area_desc = GetNodeOrNull<Label>("%AreaDescLabel");
        layer_label = GetNodeOrNull<Label>("%LayerLabel"); node_title = GetNodeOrNull<Label>("%NodeTitleLabel"); node_desc = GetNodeOrNull<Label>("%NodeDescLabel");
        if (action is not null) { action.Pressed += perform; action.Hide(); }
        if (GetNodeOrNull<Button>("%ExitBtn") is { } exit) exit.Pressed += close_exploration;
        if (GetNodeOrNull<Button>("%SkipBtn") is { } leave) leave.Pressed += close_exploration;
        Hide();
    }

    public void load_graph(DungeonGraph value) => load_graph(value, new Godot.Collections.Array());
    public void load_graph(DungeonGraph value, Godot.Collections.Array party)
    {
        graph = value; complete.Clear(); available.Clear(); current = pending = -1;
        var entry = node(value.entry_node_id); if (entry is not null) { complete.Add(entry.id); unlock(entry); layer = 1; }
        if (area_name is not null) area_name.Text = value.area_name;
        if (area_desc is not null) area_desc.Text = value.area_description;
        if (node_title is not null) node_title.Text = "选择前路";
        if (node_desc is not null) node_desc.Text = "从左侧入口出发，选择发亮的节点继续前行。随时可以返回后山。";
        build(); Show(); MoveToFront();
    }

    private DungeonGraph.DungeonNode? node(int id) => graph is not null && graph.nodes.TryGetValue(id, out var value) ? value.AsGodotObject() as DungeonGraph.DungeonNode : null;
    private void build()
    {
        if (container is null || graph is null) return;
        foreach (var child in container.GetChildren()) child.QueueFree();
        foreach (var value in graph.nodes.Values)
        {
            if (value.AsGodotObject() is not DungeonGraph.DungeonNode n) continue;
            var isComplete = complete.Contains(n.id); var isAvailable = available.Contains(n.id);
            var button = new Button { Text = $"{node_marker(n.node_type)}\n{n.title}", Position = new Vector2(70 + n.layer * 190, 80 + n.index * 126), Size = new Vector2(148, 72), Disabled = !isAvailable, TooltipText = n.description };
            button.AddThemeFontSizeOverride("font_size", 16); button.AddThemeColorOverride("font_color", isAvailable ? new Color("f5e5b2") : new Color("9d9685"));
            var normal = new StyleBoxFlat { BgColor = isAvailable ? node_color(n.node_type, .94f) : new Color(.09f, .1f, .09f, .78f), BorderColor = isComplete ? new Color("7f9b77") : isAvailable ? new Color("c9a75c") : new Color("4f514c"), BorderWidthLeft = 2, BorderWidthTop = 2, BorderWidthRight = 2, BorderWidthBottom = 2, CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8 };
            var hover = normal.Duplicate() as StyleBoxFlat; hover!.BgColor = node_color(n.node_type, 1f);
            button.AddThemeStyleboxOverride("normal", normal); button.AddThemeStyleboxOverride("hover", hover);
            if (isComplete) button.Modulate = new Color(.72f, .88f, .7f, .82f);
            if (isAvailable) button.Pressed += () => select(n);
            container.AddChild(button);
        }
        if (layer_label is not null) layer_label.Text = $"行至第 {Mathf.Min(layer, graph.max_layer)} / {graph.max_layer} 段";
    }

    private static string node_marker(int type) => type switch { 0 => "起", 1 => "兽", 3 => "遇", 4 => "采", 5 => "歇", 6 => "出", _ => "险" };
    private static Color node_color(int type, float alpha) => type switch { 1 => new Color(.31f, .17f, .12f, alpha), 3 => new Color(.16f, .24f, .26f, alpha), 4 => new Color(.17f, .29f, .2f, alpha), 5 => new Color(.22f, .25f, .16f, alpha), 6 => new Color(.32f, .25f, .12f, alpha), _ => new Color(.18f, .18f, .15f, alpha) };
    private void select(DungeonGraph.DungeonNode n)
    {
        current = n.id; if (node_title is not null) node_title.Text = n.title; if (node_desc is not null) node_desc.Text = n.description;
        if (action is not null) { action.Text = n.node_type switch { 1 or 2 => "驱赶小兽", 4 => "采集", 5 => "歇脚", 6 => "离开后山", _ => "查看" }; action.Show(); }
    }
    private void perform()
    {
        var n = node(current); if (n is null || !available.Contains(n.id)) return;
        if (n.node_type is 1 or 2) { pending = n.id; EmitSignal(SignalName.battle_requested, n.enemy_id, n.enemy_count, n.node_type == 2); return; }
        if (n.node_type == 3) { EmitSignal(SignalName.event_requested, n.event_id, n.title); finish(n); return; }
        if (n.node_type == 4) EmitSignal(SignalName.resource_collected, n.resource_type, n.resource_amount);
        if (n.node_type == 6) { EmitSignal(SignalName.exploration_finished); close_exploration(); return; }
        finish(n);
    }
    public void resolve_external_battle(bool victory) { var n = node(pending); pending = -1; if (victory && n is not null) finish(n); }
    private void finish(DungeonGraph.DungeonNode n)
    {
        complete.Add(n.id); available.Remove(n.id); layer = Mathf.Max(layer, n.layer + 1); if (n.exploration_progress_amount > 0) EmitSignal(SignalName.exploration_progressed, n.exploration_progress_amount); unlock(n); current = -1;
        if (action is not null) action.Hide(); if (node_title is not null) node_title.Text = "选择前路"; if (node_desc is not null) node_desc.Text = "新的路径已经显现。"; build();
    }
    private void unlock(DungeonGraph.DungeonNode n) { foreach (var id in n.next_node_ids) if (!complete.Contains(id)) available.Add(id); }
    private void close_exploration() { pending = current = -1; Hide(); }
}
