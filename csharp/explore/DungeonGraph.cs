using Godot;
using Godot.Collections;

public partial class DungeonGraph : Resource
{
    public enum NodeType { ENTRY, BATTLE, ELITE, EVENT, RESOURCE, REST, EXIT }
    public partial class DungeonNode : RefCounted { public int id, node_type, layer, index, exploration_progress_amount, enemy_count = 1, resource_amount; public string title = "", description = "", enemy_id = "", event_id = "", resource_type = ""; public float restore_percent = .3f, time_cost = 1; public Array<int> next_node_ids = []; public DungeonNode(int id, int type, int layer, int index) { this.id = id; node_type = type; this.layer = layer; this.index = index; } }
    public Dictionary nodes = []; public int entry_node_id = -1, exit_node_id = -1, max_layer; public string area_name = "", area_description = "";
    public void add_node(DungeonNode node) { nodes[node.id] = node; if (node.node_type == (int)NodeType.ENTRY) entry_node_id = node.id; if (node.node_type == (int)NodeType.EXIT) exit_node_id = node.id; max_layer = Mathf.Max(max_layer, node.layer); }
    public void connect_nodes(int from, int to) { if (nodes.TryGetValue(from, out var value) && value.AsGodotObject() is DungeonNode node && !node.next_node_ids.Contains(to)) node.next_node_ids.Add(to); }
    public Array<DungeonNode> get_layer_nodes(int layer) { var result = new List<DungeonNode>(); foreach (var value in nodes.Values) if (value.AsGodotObject() is DungeonNode node && node.layer == layer) result.Add(node); result.Sort((a,b) => a.index.CompareTo(b.index)); return new Array<DungeonNode>(result); }
    public Array<DungeonNode> get_next_nodes(int id) { var result = new Array<DungeonNode>(); if (!nodes.TryGetValue(id, out var value) || value.AsGodotObject() is not DungeonNode node) return result; foreach (var next in node.next_node_ids) if (nodes.TryGetValue(next, out var child) && child.AsGodotObject() is DungeonNode childNode) result.Add(childNode); return result; }
    public Array<int> get_visible_layers(int layer) { var result = new Array<int>(); for (var i = layer; i < Mathf.Min(layer + 2, max_layer + 1); i++) result.Add(i); return result; }
}
