using Godot;

public partial class EastWoodsDungeonPanel
{
    private string forest_ambience(string nodeId)
    {
        if (nodeId == "outer_08_02") return "越过林口，天光被层层枝叶遮住。身后的林径仍可原路返回。";
        if (EastWoodsDungeonRules.forest_region(nodeId) == "深林")
            return terrain_kind(nodeId).Index switch
            {
                5 => "树影落进湿洼，脚步声被潮软的泥地吞没。",
                7 => "岩隙里透出凉意，抬头已难看清林外的天色。",
                6 => "密竹掩住天光，风过时只有细碎的叶声。",
                _ => "古木交错，林径隐入深处。来时的路渐渐没入树影。",
            };
        return terrain_kind(nodeId).Index switch
        {
            3 => "溪声从疏林间传来，水面映着零碎天光。",
            5 => "浅洼间留着水痕，日光从枝叶缝隙落下。",
            _ => "日光穿过疏林，脚下的旧径仍清晰可辨。",
        };
    }

    private void offer_arrival_narration(EastWoodsActions.ActionDef action)
    {
        var region = EastWoodsDungeonRules.forest_region(_current);
        var item = action.ItemPath.Length > 0 ? EastWoodsActions.Item(action.ItemPath) : null;
        var opening = action.Battle ? "枝叶忽然一晃，一头凶狼从近处的树影中现出身形。"
            : item is not null ? $"你停下脚步，在近旁发现了{item.entity_name}。"
            : "走近后，你发现这里留下了一些异样的痕迹。";
        var scene = action.Battle ? "它伏低身子，目光紧盯着你。这头林地凶狼已有通智初期修为，是否应战，还须掂量自身实力。"
            : route_node(_current)?.ResourceNodeId switch
            {
                "common_herb" => "拂开低垂的蕨叶，几丛根茎露在湿土间。你可以俯身采下，也可以任它们留在林中。",
                "common_moss" => "背阴处覆着一层细苔，叶尖凝着水珠。走近细看，正是月露苔。",
                "mistbell_slow" => "水痕旁垂着几朵小花，在枝叶间轻轻摇曳。是雾铃花，生得并不多。",
                "spirit_stone_vein" => "岩缝中透出微弱灵光。拨开浮土，石层里显出一段细小的灵石矿脉。",
                _ => action.Description,
            };
        open_action_card($"{region} · {(action.Battle ? "林中遭遇" : item is not null ? "途中发现" : "林间异状")}",
            opening + "\n\n" + scene,
            local_action_summary(action), action.Id);
        _actionConfirm!.Text = action.Battle ? "查看战前情报" : local_action_name(action);
    }
}
