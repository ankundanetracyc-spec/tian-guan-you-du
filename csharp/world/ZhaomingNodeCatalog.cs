using Godot;
using System.Text.RegularExpressions;

/// <summary>从维护中的目录数据填充昭明纪固定 L1-L3 世界节点。</summary>
public partial class ZhaomingNodeCatalog : RefCounted
{
    private const string CatalogPath = "res://resources/world/zhaoming_node_catalog.txt";
    private static readonly Regex EntryPattern = new("\\{\\\"id\\\": \\\"(?<id>[^\\\"]+)\\\", \\\"name\\\": \\\"(?<name>[^\\\"]+)\\\", \\\"x\\\": (?<x>[0-9.]+), \\\"y\\\": (?<y>[0-9.]+), \\\"level\\\": (?<level>[0-9]+), \\\"type\\\": WorldNodeData\\.NodeType\\.(?<type>[A-Z]+)\\}", RegexOptions.Compiled);
    public static void populate(WorldNodeMapData map_data)
    {
        if (map_data is null || map_data.HasMeta("zhaoming_catalog_loaded")) return;
        var ids = new HashSet<string>(map_data.nodes.Select(node => node.node_id)); var names = new HashSet<string>(map_data.nodes.Select(node => node.node_name));
        var file = Godot.FileAccess.Open(CatalogPath, Godot.FileAccess.ModeFlags.Read); var text = file?.GetAsText() ?? ""; file?.Close();
        foreach (Match match in EntryPattern.Matches(text))
        {
            var id=match.Groups["id"].Value; var name=match.Groups["name"].Value; if(ids.Contains(id)||names.Contains(name)) continue;
            if(!Enum.TryParse<WorldNodeData.NodeType>(match.Groups["type"].Value,out var type)) type=WorldNodeData.NodeType.Resource;
            var level=int.Parse(match.Groups["level"].Value); map_data.nodes.Add(new WorldNodeData{node_id=id,node_name=name,node_type=type,display_level=(WorldNodeData.DisplayLevel)level,map_position=new(float.Parse(match.Groups["x"].Value,System.Globalization.CultureInfo.InvariantCulture),float.Parse(match.Groups["y"].Value,System.Globalization.CultureInfo.InvariantCulture)),min_zoom=level==1?0:level==2?1.12f:1.85f,marker_size=level==1?72:level==2?42:22,terrain="待按地貌落位校正",description="昭明纪大世界固定节点；L4 探索事件不会在此常驻显示。"}); ids.Add(id); names.Add(name);
        }
        map_data.SetMeta("zhaoming_catalog_loaded",true);
    }
}
