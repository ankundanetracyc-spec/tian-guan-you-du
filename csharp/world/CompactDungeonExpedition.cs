using Godot;
using System;
using System.Linq;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

/// <summary>The first authored short route. Lifecycle and action bookkeeping stay shared.</summary>
public static class CompactDungeonExpedition
{
    public const string TemplateId = "fern_path";
    public const string InstanceId = "fern_path_permanent";
    public const string DisplayName = "蕨林采径";
    public const string EntryNodeId = "fern_00";
    public const string ExitNodeId = "fern_08";
    public const int NodeCount = 9;
    public static bool IsWolfNode(string id) => id is "fern_02" or "fern_05" or "fern_07";

    internal static GDictionary GenerateLayout(string seed)
    {
        var nodes = new GArray();
        var edges = new GArray();
        string[] titles = ["林缘入口", "蕨下根丛", "林径凶狼", "疏林空地", "背阴苔地", "盘根凶狼", "老树根丛", "山口凶狼", "山口出口"];
        string[] descriptions = ["通往东林地林缘的实体出口。", "蕨叶下长着有限的苦苔根。", "林地凶狼活动于狭窄林径，抵达后先查看战前信息。", "短暂开阔的林间地面，可决定继续前进或原路返回。", "背阴湿地附着少量月露苔。", "凶狼伏在盘根附近，仍可自行决定是否开战。", "老树根旁还有一丛苦苔根。", "接近山口时可见一头林地凶狼。", "沿实体出口返回入场地点，不额外发放通关奖。"];
        for (var index = 0; index < NodeCount; index++)
        {
            var resource = index is 1 or 6 ? "common_herb" : index == 4 ? "common_moss" : "";
            nodes.Add(new GDictionary
            {
                ["id"] = NodeId(index), ["title"] = titles[index], ["description"] = descriptions[index],
                ["x"] = index, ["y"] = 2, ["show_title"] = index is 0 or 8,
                ["terrain"] = index == 4 ? 5 : index == 3 ? 1 : 0, ["feature"] = "",
                ["resource_id"] = resource,
                ["resource_category"] = resource.Length > 0 ? (int)EastWoodsResourceCategory.Plant : (int)EastWoodsResourceCategory.None,
            });
            if (index > 0) edges.Add(new GDictionary { ["from"] = NodeId(index - 1), ["to"] = NodeId(index) });
        }
        return new GDictionary
        {
            ["template_id"] = TemplateId, ["seed"] = seed, ["columns"] = NodeCount, ["rows"] = 5,
            ["entry_node_id"] = EntryNodeId, ["exit_node_id"] = ExitNodeId, ["nodes"] = nodes, ["edges"] = edges,
        };
    }

    internal static void ValidateLayout(GDictionary snapshot)
    {
        var nodes = snapshot.GetValueOrDefault("nodes", new GArray()).AsGodotArray();
        var edges = snapshot.GetValueOrDefault("edges", new GArray()).AsGodotArray();
        if (snapshot.GetValueOrDefault("template_id", "").AsString() != TemplateId || nodes.Count != NodeCount || edges.Count != NodeCount - 1 ||
            snapshot.GetValueOrDefault("columns", 0).AsInt32() != NodeCount || snapshot.GetValueOrDefault("rows", 0).AsInt32() != 5 ||
            snapshot.GetValueOrDefault("entry_node_id", "").AsString() != EntryNodeId || snapshot.GetValueOrDefault("exit_node_id", "").AsString() != ExitNodeId)
            throw new InvalidOperationException("蕨林采径模板、节点数量或实体出入口损坏。");
        for (var index = 0; index < NodeCount; index++)
        {
            if (nodes[index].VariantType != Variant.Type.Dictionary) throw new InvalidOperationException("蕨林采径节点无效。");
            var node = nodes[index].AsGodotDictionary();
            var resource = index is 1 or 6 ? "common_herb" : index == 4 ? "common_moss" : "";
            if (node.GetValueOrDefault("id", "").AsString() != NodeId(index) || node.GetValueOrDefault("x", -1).AsInt32() != index ||
                node.GetValueOrDefault("y", -1).AsInt32() != 2 || node.GetValueOrDefault("resource_id", "").AsString() != resource ||
                node.GetValueOrDefault("resource_category", -1).AsInt32() != (resource.Length > 0 ? (int)EastWoodsResourceCategory.Plant : 0) ||
                node.GetValueOrDefault("terrain", -1).AsInt32() is < 0 or > 7)
                throw new InvalidOperationException("蕨林采径的稳定节点或资源配置损坏，拒绝重抽。");
        }
        var expected = Enumerable.Range(1, NodeCount - 1).Select(index => NodeId(index - 1) + "|" + NodeId(index)).ToHashSet(StringComparer.Ordinal);
        foreach (var value in edges)
        {
            if (value.VariantType != Variant.Type.Dictionary) throw new InvalidOperationException("蕨林采径连接无效。");
            var edge = value.AsGodotDictionary();
            var from = edge.GetValueOrDefault("from", "").AsString();
            var to = edge.GetValueOrDefault("to", "").AsString();
            var key = string.CompareOrdinal(from, to) < 0 ? from + "|" + to : to + "|" + from;
            if (!expected.Remove(key)) throw new InvalidOperationException("蕨林采径含有重复或偏离单线的连接。");
        }
        if (expected.Count != 0) throw new InvalidOperationException("蕨林采径路线不连通。");
    }

    private static string NodeId(int index) => "fern_" + index.ToString("D2");
}
