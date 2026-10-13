using Godot;
using System;
using System.Linq;
using System.Text;

public partial class EastWoodsDungeonPanel
{
    // Read from existing exploration/action state; never reveal an unexamined resource or beast.
    private string exploration_notes()
    {
        var exitPath = find_shortest_path(_current, "forest_edge");
        var text = new StringBuilder("只显示四格视野内的现场情况；查看不推进时间。\n");
        text.Append(exitPath.Count > 0 || _current == "forest_edge" ? $"返回林缘约需 {format_hours(Math.Max(0, exitPath.Count - 1) * EastWoodsDungeonRules.player_edge_hours(_playerDunSpeed))}。\n" : "尚无完整的已知回程路线，须沿已走过的道路返回。\n");
        var known = EastWoodsDungeonRules.get_nodes().Where(n =>
                is_node_visible(n.Id) &&
                EastWoodsActions.Content(n.Id) is not null &&
                is_explored(n.Id))
            .OrderBy(n => n.Id == _current ? 0 : find_shortest_path(_current, n.Id).Count).ToArray();
        if (known.Length == 0) text.Append("\n视野内暂无可记录的资源或异状，可沿林径继续前行，无需探查。");
        foreach (var node in known.Take(6))
        {
            var availableBeast = available_actions(node.Id).FirstOrDefault(candidate => candidate.Battle);
            var action = availableBeast ?? EastWoodsActions.Content(node.Id)!;
            var location = string.IsNullOrWhiteSpace(node.Title) ? $"林间（{node.GridPosition.X}, {node.GridPosition.Y}）" : node.Title;
            var remaining = action_remaining(node.Id, action);
            var content = action.ItemPath.Length > 0 ? $"{EastWoodsActions.Item(action.ItemPath)?.entity_name}，余 {remaining} 份"
                : action.Battle ? (remaining > 0 ? (string.IsNullOrWhiteSpace(action.EncounterIdentity) ? "凶狼尚在，可绕行" : "凶狼再次活动，可绕行") : "凶狼已被击败")
                : (remaining > 0 ? action.Name + "待办" : action.Name + "已记");
            text.Append($"\n{location}：{content}");
        }
        if (known.Length > 6) text.Append($"\n\n已记 {known.Length} 处要点，此页列离你最近的六处。");
        text.Append("\n\n野外所得归自己；未接取并交付宗门事务，不自动获得贡献。");
        return text.ToString();
    }

    private void preview_duty_target()
    {
        if (_actionBusy || _isTraveling || _travelPawn is not null) return;
        close_action_details();
        var target = _activeDutyReady ? "forest_edge" : "quiet_grove";
        if (_current == target)
            show_action_result("巡查指引", _activeDutyReady ? "从林缘离开后，回和光峰宗门事务处回报。" : "沿路线至少推进四个节点后，在静冠洼地完成复核。巡查不要求探查、采集或杀怪。");
        else
        {
            if (!is_node_discovered(target) || find_shortest_path(_current, target).Count == 0)
            {
                show_action_result("巡查指引", "静冠洼地在林地深处。沿林径继续前行，发现通路后才能预览路线；任务不会直接揭开迷雾。");
                return;
            }
            // A dedicated preview button never confirms a previously selected route.
            _pendingTarget = ""; _pendingPath.Clear();
            on_node_clicked(target);
        }
    }
}
