using Godot;
using Godot.Collections;

public partial class Character
{
    private static SkillNodeNetworkConfig? _skillNodeNetwork;
    private static SkillNodeNetworkConfig? skill_node_network => _skillNodeNetwork ??= SkillNodeNetworkConfig.load_default();

    public Array<SkillNodeConfig> get_skill_node_configs() =>
        new(skill_node_network?.get_nodes() ?? Enumerable.Empty<SkillNodeConfig>());

    public Array<SkillNodeFaceConfig> get_skill_node_face_configs() =>
        new(skill_node_network?.get_faces() ?? Enumerable.Empty<SkillNodeFaceConfig>());

    public bool has_unlocked_skill_node(string nodeId) =>
        human?.unlocked_skill_node_ids.Contains(nodeId) == true;

    public bool can_unlock_skill_node(string nodeId, out string reason)
    {
        reason = "";
        if (human is null) { reason = "当前对象没有可修习的人物数据。"; return false; }
        var node = skill_node_network?.get_node(nodeId);
        if (node is null) { reason = "未找到对应节点。"; return false; }
        if (has_unlocked_skill_node(nodeId)) { reason = "已经参悟。"; return false; }

        var missingNodes = node.prerequisite_node_ids.Where(id => !has_unlocked_skill_node(id)).ToArray();
        if (missingNodes.Length > 0)
        {
            var names = missingNodes.Select(id => skill_node_network?.get_node(id)?.node_name ?? id);
            reason = "尚需前置节点：" + string.Join("、", names);
            return false;
        }

        var unmet = new List<string>();
        foreach (Variant key in node.required_attributes.Keys)
        {
            var attribute = key.AsString();
            if (CardRules.Number(this, attribute) + 0.0001f < node.required_attributes[attribute].AsSingle()) unmet.Add(attribute);
        }
        if (unmet.Count > 0)
        {
            reason = "尚未达到：" + string.Join("、", unmet.Select(attribute =>
                $"{skill_node_attribute_name(attribute)} {CardRules.Number(this, attribute):0.#}/{node.required_attributes[attribute].AsSingle():0.#}"));
            return false;
        }

        return true;
    }

    public bool unlock_skill_node(string nodeId)
    {
        if (!can_unlock_skill_node(nodeId, out _) || human is null) return false;
        human.unlocked_skill_node_ids.Add(nodeId);
        return true;
    }

    public Array<string> get_unlocked_skill_node_paths()
    {
        var result = new Array<string>();
        if (human is null || skill_node_network is null) return result;
        foreach (var node in skill_node_network.get_nodes())
            if (has_unlocked_skill_node(node.node_id))
                foreach (var path in node.get_skill_paths())
                    if (!result.Contains(path)) result.Add(path);
        return result;
    }

    private static string skill_node_attribute_name(string id) => id switch
    {
        "physique" => "体魄",
        "intelligence" => "灵慧",
        "soul" => "神魂",
        "body_proficiency" => "体道造诣",
        "magic_proficiency" => "术法造诣",
        "sword_proficiency" => "剑道造诣",
        _ => id
    };
}
