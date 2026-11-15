using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class EastWoodsDungeonPanel
{
    // Same geometric radius convention as the world map, measured in logical cells.
    public const int VisionRadius = 4;
    private readonly HashSet<string> _visibleNodes = new(StringComparer.Ordinal);
    private readonly HashSet<string> _discoveredNodes = new(StringComparer.Ordinal);

    public bool is_node_visible(string id) => _visibleNodes.Contains(id);
    public bool is_node_discovered(string id) => _discoveredNodes.Contains(id);

    private void update_visibility()
    {
        _visibleNodes.Clear();
        if (is_tower_instance)
        {
            foreach (var node in route_nodes)
            {
                _visibleNodes.Add(node.Id);
                _discoveredNodes.Add(node.Id);
            }
            if (!is_node_visible(_hoveredNode)) clear_hover();
            return;
        }
        var center = route_node(_current);
        if (center is null) return;
        foreach (var node in route_nodes)
        {
            var delta = node.GridPosition - center.GridPosition;
            if (delta.X * delta.X + delta.Y * delta.Y > VisionRadius * VisionRadius) continue;
            _visibleNodes.Add(node.Id);
            _discoveredNodes.Add(node.Id);
        }
        if (!is_node_visible(_hoveredNode)) clear_hover();
    }

    private void restore_discovery(Godot.Collections.Dictionary state)
    {
        _discoveredNodes.Clear();
        var saved = state.GetValueOrDefault("discovered", new Godot.Collections.Array<string>());
        if (saved.VariantType == Variant.Type.Array)
            foreach (var value in saved.AsGodotArray())
                if (route_node(value.AsString()) is not null) _discoveredNodes.Add(value.AsString());
        // Migrate only knowledge the old save actually recorded, never reveal the whole map.
        foreach (var id in _visited) _discoveredNodes.Add(id);
        foreach (var node in route_nodes)
            if (progress(node.Id + ":explore") > 0) _discoveredNodes.Add(node.Id);
        update_visibility();
    }
}
