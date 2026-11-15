using System;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public static partial class ForestWolfEvent
{
    public static string PreviousContact(GDictionary actions, string node)
    {
        var last = actions.GetValueOrDefault("__forest_wolf_last_contact", new GDictionary()).AsGodotDictionary();
        if (last.Count == 0 || last["node"].AsString() == node) return "";
        return last["stage"].AsString() switch
        {
            "drive_success" => "先前的凶狼已经被你逼退，林中的动静却没有就此停歇。",
            "battle_win" => "先前那场交锋的余息尚在，前方又传来新的动静。",
            "retreated" => "你避开了先前的凶狼，换一处林径探行，却又察觉到异样。",
            "battle_escape" => "你刚从先前的纠缠中脱身，走到另一处林径，警觉仍未放松。",
            "battle_draw" or "battle_loss" => "先前的交锋使你更为谨慎，眼前又有新的动静。",
            _ => "",
        };
    }

    public static Page Describe(GDictionary record, BattleEntityData player)
    {
        var page = DescribeBase(record, player);
        var node = record.GetValueOrDefault("node", NodeId).AsString();
        var stage = record.GetValueOrDefault("stage", "opening").AsString();
        if (node == "outer_08_08") page = stage switch
        {
            "observe_success" => page with { Title = "辨清来向", Body = "你放慢呼吸，分辨脚步与落叶声的间隔。每当你停下，身后的动静也会迟疑片刻。\n\n那头凶狼仍在试探，并未下定扑击的决心。趁它迟疑时反身喝止，也许能让它退去。" },
            "observe_failure" => page with { Title = "脚步乱了", Body = "你刻意放轻脚步，却踩断了一截枯枝。身后的动静骤然停住，随后响起一声低吼。\n\n它看出了你的迟疑，脚步比先前更近。再想喝退它，已不如刚才容易。" },
            "drive_success" => page with { Title = "尾随止息", Body = "你稳住身形，忽然转身喝止。草间的凶狼收住前爪，与你相持片刻，终于退开。\n\n你再度迈步时，身后只剩风过落叶的声音。" },
            "drive_failure" => page with { Title = "仍在逼近", Body = "你反身喝出一声，凶狼却只是侧了侧头。它从草影里走出半步，仍盯着你的动作。\n\n喝止没有奏效。这场接触只能另作决断。" },
            "retreated" => page with { Body = "你始终留意身后的动静，缓缓拉开距离。凶狼停在草影边缘，没有继续紧逼。\n\n接触暂告一段落。这里仍有凶狼活动，你可以另择道路。" },
            "battle_win" => page with { Body = "尾随的凶狼终于倒下。你平复气息，再回望来路，林间已没有紧跟不舍的脚步声。" },
            "battle_escape" => page with { Body = "你摆脱了凶狼的纠缠，重新站稳脚步。远处仍有它压低的吼声，这一处并未平息。" },
            "battle_draw" => page with { Body = "交锋未能分出胜负。你暂且站稳，凶狼仍在附近活动；先前的试探与消耗都没有消失。" },
            "battle_loss" => page,
            _ => page with { Title = "林径尾随", Body = "你穿过一段落叶覆盖的林径，身后也响起细碎的脚步。你停，它便停；你走，那声音又跟上来。\n\n回头望去，一双眼睛正从草影里盯着你。一头凶狼保持着距离，似乎还在试探。" },
        };
        else if (node == "outer_13_06") page = stage switch
        {
            "observe_success" => page with { Title = "寻到迟疑", Body = "你留心草叶摆动与它的呼吸。凶狼每次向前探身，前爪却总会收回一点。\n\n它同样有所顾忌。稳住身形，踏前逼近，也许能让它先退。" },
            "observe_failure" => page with { Title = "惊动草丛", Body = "你俯身看向草间，却被近处一阵响动扰乱了判断。凶狼随即压低身子，喉间的低吼越发清楚。\n\n你的试探已经被察觉。此时再想逼退它，恐怕需要更大的威势。" },
            "drive_success" => page with { Title = "草间归静", Body = "你踩稳脚下的泥土，迎着那道视线踏前一步。凶狼顿住，终于向草丛深处退去。\n\n高草缓缓合拢，眼前的对峙结束了。" },
            "drive_failure" => page with { Title = "寸步不让", Body = "你踏前逼近，凶狼却没有退让。草叶被它的前爪拨开，低吼声近在眼前。\n\n它不肯先退，继续施压已无意义。你仍可迎战，也可结束这次接触。" },
            "retreated" => page with { Body = "你放缓动作，结束与那双眼睛的对视。凶狼仍伏在草间，没有趁势扑来。\n\n这里仍未平息。是否回来应对，可以稍后再作决定。" },
            "battle_win" => page with { Body = "草间的凶狼终于倒下。你收住架势，风吹过高草，近处再没有回应的低吼。" },
            "battle_escape" => page with { Body = "你脱离了凶狼的纠缠，重新站稳脚步。草间仍有它的动静，这场遭遇还没有结束。" },
            "battle_draw" => page with { Body = "这场交锋未分胜负。你重整呼吸，草间仍有凶狼活动；伤势和消耗需自行料理。" },
            "battle_loss" => page,
            _ => page with { Title = "草间对峙", Body = "前方高草忽然向两侧分开，一双眼睛在阴影中抬起。凶狼伏着不动，目光随着你的脚步缓缓偏转。\n\n你停在草丛外。双方都没有先动，风声里夹着低低的鼻息。" },
        };
        var lead = record.GetValueOrDefault("lead_in", "").AsString();
        return stage == "opening" && lead.Length > 0 ? page with { Body = lead + "\n\n" + page.Body } : page;
    }
}
