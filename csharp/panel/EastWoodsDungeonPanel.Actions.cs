using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class EastWoodsDungeonPanel
{
    public event Action<string, string>? ActionRequested;
    public event Action? CompactRouteRequested;
    private Godot.Collections.Dictionary _actionProgress = new();
    private Dictionary<string, List<EastWoodsActions.ActionDef>>? _refreshBeastActions;
    private bool _refreshingMapActions;
    private PanelContainer? _actionBar, _actionCard;
    private VBoxContainer? _actionRows;
    private Control? _actionModal;
    private Label? _actionTitle, _actionBody, _actionCost;
    private TextureRect? _actionIcon;
    private Button? _actionConfirm;
    private string _offeredAction = "";
    private bool _actionBusy;
    public Func<bool>? CanMove { get; set; }

    // Compatibility query for old saves/tests; no paid exploration action remains.
    public bool is_explored(string node) => _visited.Contains(node) || progress(node + ":explore") > 0;
    private int progress(string key) => _actionProgress.GetValueOrDefault(key, 0).AsInt32();
    public int action_remaining(string node, EastWoodsActions.ActionDef action) => action.Stock > 0 ? Math.Max(0, action.Stock - progress(node + ":" + action.Id)) : progress(node + ":" + action.Id) > 0 ? 0 : 1;
    public EastWoodsActions.ActionDef? find_action(string node, string id)
        => _dynamicMode
            ? DynamicDungeonExpedition.AvailableActions(_dynamicInstanceState, node, _actionProgress).FirstOrDefault(action => action.Id == id)
            : EastWoodsEcology.FindAction(node, id, _actionProgress);
    public List<EastWoodsActions.ActionDef> available_actions(string node)
    {
        if (_dynamicMode) return DynamicDungeonExpedition.AvailableActions(_dynamicInstanceState, node, _actionProgress);
        var result = new List<EastWoodsActions.ActionDef>();
        if (EastWoodsActions.Content(node) is { } fixedAction && action_remaining(node, fixedAction) > 0) result.Add(fixedAction);
        if (_refreshingMapActions)
        {
            _refreshBeastActions ??= EastWoodsEcology.IndexAvailableRegeneratedBeasts(_actionProgress);
            if (_refreshBeastActions.TryGetValue(node, out var beasts)) result.AddRange(beasts);
        }
        else result.AddRange(EastWoodsEcology.AvailableRegeneratedBeasts(node, _actionProgress));
        return result;
    }

    public bool can_execute_action(string node, string id)
    {
        if (node != _current || _actionBusy || _isTraveling || _npcEncounterBusy || _travelPawn is not null) return false;
        var action = find_action(node, id);
        return action is not null && action_remaining(node, action) >= Math.Max(1, action.Quantity);
    }
    public void complete_action(string node, string id) => complete_action(node, id, 0);

    /// <summary>Settles a resource action with its final yield, including any selected harvest bonus.</summary>
    public void complete_action(string node, string id, int quantityOverride)
    {
        var action = find_action(node, id);
        if (action is null) return;
        var quantity = quantityOverride > 0 ? quantityOverride : Math.Max(1, action.Quantity);
        _actionProgress[node + ":" + id] = progress(node + ":" + id) + quantity;
        if (!string.IsNullOrWhiteSpace(action.EncounterIdentity))
        {
            var hour = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? 0;
            if (_dynamicMode)
            {
                foreach (var value in _dynamicInstanceState.GetValueOrDefault("beasts", new Godot.Collections.Array()).AsGodotArray())
                {
                    if (value.VariantType != Variant.Type.Dictionary) continue;
                    var record = value.AsGodotDictionary();
                    if (record.GetValueOrDefault("encounter_id", "").AsString() != action.EncounterIdentity) continue;
                    record["status"] = "defeated";
                    record["resolved_hour"] = hour;
                    break;
                }
            }
            else EastWoodsEcology.MarkEncounterDefeated(_actionProgress, action.EncounterIdentity, hour);
        }
        if (_dynamicMode && id.StartsWith("event_", StringComparison.Ordinal))
        {
            var route = get_route_state();
            DynamicDungeonExpedition.ApplyCompletedAction(_dynamicInstanceState, node, id, route);
            restore_route_state(route);
            DynamicDungeonExpedition.StoreRoute(_dynamicInstanceState, route);
        }
        refresh_action_bar();
    }
    public void interrupt_travel(bool stopNpcMotion = true)
    {
        var wasTraveling = _isTraveling;
        if (stopNpcMotion) clear_npc_motion();
        finish_pawn_step();
        _travelGeneration++; _isTraveling = false;
        _pendingPath.Clear(); _pendingTarget = "";
        refresh();
        if (wasTraveling) RouteTravelFinished?.Invoke(_current);
    }
    public void show_action_result(string title, string body)
    {
        open_action_card(title, body, "", "");
    }
    public bool close_action_details()
    {
        if (_actionModal?.Visible != true) return false;
        _actionModal.Hide(); _offeredAction = ""; return true;
    }
    public bool can_leave() => Visible && is_route_exit(_current) && !_isTraveling && !_actionBusy && !_npcEncounterBusy && _travelPawn is null;

    public void return_one_level()
    {
        if (close_action_details()) return;
        interrupt_travel();
        open_action_card("暂歇", _dynamicMode ? $"你仍在{route_display_name}内。查看界面不推进游戏时间；须沿路线走到出口才能离开。" : "你仍在东林地内。查看界面不推进游戏时间；离开须沿林径返回林缘出口。", "", "");
    }

    public void locate_exit()
    {
        if (_actionBusy || _npcEncounterBusy) return;
        close_action_details();
        interrupt_travel();
        if (is_route_exit(_current))
        {
            offer_exit();
            return;
        }
        on_node_clicked(route_exit_node_id);
    }

    private void offer_exit()
    {
        if (!can_leave()) return;
        open_action_card(_dynamicMode ? $"离开{route_display_name}" : "离开林地",
            _dynamicMode ? $"沿实体出口离开{route_display_name}。已探明路线、资源、事件与遭遇状态会保留。" + (is_compact_route ? "再次进入不会重置采集和遭遇。" : "若入口已到期，离开后不能再次进入。") : "沿林缘出口离开东林地。已取得物品与探明、采集、事件进度将保留，再次进入不会重置。",
            "出口已抵达；取消不离场。", "__leave");
        _actionConfirm!.Text = _dynamicMode ? $"离开{route_display_name}" : "离开林地";
    }

    private void build_actions()
    {
        _actionBar = new PanelContainer { Name = "CurrentNodeActions", MouseFilter = MouseFilterEnum.Stop };
        _actionBar.AddThemeStyleboxOverride("panel", WorldHudStyle.Paper());
        AddChild(_actionBar);
        _actionBar.MinimumSizeChanged += () => CallDeferred(nameof(position_current_node_actions));
        _actionRows = new VBoxContainer(); _actionRows.AddThemeConstantOverride("separation", 6); _actionBar.AddChild(_actionRows);
        _actionModal = new Control { Name = "NodeActionDetails", MouseFilter = MouseFilterEnum.Stop, Visible = false };
        _actionModal.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(_actionModal);
        var veil = new ColorRect { Color = new Color(.12f,.15f,.12f,.28f), MouseFilter = MouseFilterEnum.Stop };
        veil.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); _actionModal.AddChild(veil);
        var centered = new CenterContainer { Name = "CenteredCard", MouseFilter = MouseFilterEnum.Ignore };
        centered.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); _actionModal.AddChild(centered);
        _actionCard = new PanelContainer { Name = "PaperCard", CustomMinimumSize = new Vector2(Mathf.Max(320, Size.X * .48f), 0) };
        _actionCard.AddThemeStyleboxOverride("panel", WorldHudStyle.Paper()); centered.AddChild(_actionCard);
        Resized += () => _actionCard.CustomMinimumSize = new Vector2(Mathf.Max(320, Size.X * .48f), 0);
        var margin = new MarginContainer();
        foreach (var side in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride("margin_" + side, 22);
        _actionCard.AddChild(margin);
        var rows = new VBoxContainer(); rows.AddThemeConstantOverride("separation", 16); margin.AddChild(rows);
        var header = new HBoxContainer(); header.AddThemeConstantOverride("separation", 12); rows.AddChild(header);
        header.AddChild(WorldHudStyle.Seal("行"));
        _actionTitle = WorldHudStyle.Text("", 28); _actionTitle.SizeFlagsHorizontal = SizeFlags.ExpandFill; header.AddChild(_actionTitle);
        _actionWorldLabel = WorldHudStyle.Text("东林地", 16, true); header.AddChild(_actionWorldLabel); rows.AddChild(WorldHudStyle.Rule());
        var bodyRow = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill }; bodyRow.AddThemeConstantOverride("separation", 20); rows.AddChild(bodyRow);
        _actionIcon = new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = new Vector2(82,82), SizeFlagsVertical = SizeFlags.ShrinkBegin, MouseFilter = MouseFilterEnum.Ignore, Visible = false };
        bodyRow.AddChild(_actionIcon);
        _actionBody = WorldHudStyle.Text("", 20); _actionBody.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _actionBody.VerticalAlignment = VerticalAlignment.Top; _actionBody.SizeFlagsVertical = SizeFlags.ExpandFill;
        _actionBody.SizeFlagsHorizontal = SizeFlags.ExpandFill; bodyRow.AddChild(_actionBody);
        rows.AddChild(WorldHudStyle.Rule());
        _actionCost = WorldHudStyle.Text("", 17, true); _actionCost.AutowrapMode = TextServer.AutowrapMode.WordSmart; rows.AddChild(_actionCost);
        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End }; buttons.AddThemeConstantOverride("separation", 20); rows.AddChild(buttons);
        var cancel = make_button("返回", 100, 42); cancel.Name = "Cancel"; cancel.Pressed += () => close_action_details(); buttons.AddChild(cancel);
        _actionConfirm = make_button("确认行动", 144, 42); _actionConfirm.Name = "Confirm";
        _actionConfirm.Pressed += () =>
        {
            var id = _offeredAction; var node = _current;
            if (id == "__leave")
            {
                if (can_leave()) close();
                return;
            }
            close_action_details();
            execute_named_action(node, id);
        };
        buttons.AddChild(_actionConfirm);
    }

    private void refresh_action_bar()
    {
        if (_actionRows is null || _actionBar is null) return;
        if (_isTraveling)
        {
            // The panel is hidden for the whole route. Keep its controls intact and
            // rebuild once at arrival instead of replacing them for every crossed cell.
            _actionBar.Hide();
            _actionBarStamp = "";
            return;
        }
        var contentStamp = string.Join("|", available_actions(_current).Select(action => $"{action.Id}:{action_remaining(_current, action)}"));
        var stamp = $"{_current}:{_isTraveling}:{_travelPawn is not null}:{_pendingPath.Count < 2}:{_actionBusy}:{_npcEncounterBusy}:{_activeDutyId}:{_activeDutyStage}:{_activeDutyReady}:{contentStamp}";
        if (_actionBarStamp == stamp) return;
        _actionBarStamp = stamp;

        foreach (var child in _actionRows.GetChildren()) { _actionRows.RemoveChild(child); child.QueueFree(); }
        _actionBar.Visible = !_isTraveling && _travelPawn is null && _pendingPath.Count < 2 && _actionModal?.Visible != true;
        var node = route_node(_current);
        var heading = WorldHudStyle.Text(node?.ShowTitleOnMap == true ? node.Title : _dynamicMode ? route_display_name : "林间驻足", 16);
        if (!_dynamicMode) heading.Text = "东林地 · " + EastWoodsDungeonRules.forest_region(_current);
        heading.HorizontalAlignment = HorizontalAlignment.Center; _actionRows.AddChild(heading);
        if (!_dynamicMode)
        {
            var ambience = WorldHudStyle.Text(forest_ambience(_current), 16, true);
            ambience.Name = "ForestAmbience";
            ambience.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _actionRows.AddChild(ambience);
        }
        if (!_dynamicMode && _activeDutyId == "east_woods_patrol")
        {
            var duty = WorldHudStyle.Text(_activeDutyReady ? "巡查已复核 · 从林缘离开后回和光峰回报" : $"巡查 {Math.Min(_activeDutyStage, YuxuSectPrototypeBalance.EastWoodsPatrolRequiredNodeMoves)}/{YuxuSectPrototypeBalance.EastWoodsPatrolRequiredNodeMoves} · 前往静冠洼地复核", 16, true);
            duty.Name = "DutyProgress"; duty.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            duty.HorizontalAlignment = HorizontalAlignment.Center; _actionRows.AddChild(duty);
        }
        var row = new HFlowContainer(); row.AddThemeConstantOverride("h_separation", 12); row.AddThemeConstantOverride("v_separation", 6); _actionRows.AddChild(row);
        if (!_dynamicMode && _activeDutyId == "east_woods_patrol")
        {
            var target = make_button("查看目标", 100, 32); target.Name = "PreviewDutyTarget";
            target.Pressed += preview_duty_target; row.AddChild(target);
        }
        if (!_dynamicMode && _current == route_entry_node_id)
        {
            var compact = make_button("蕨林采径", 116, 32); compact.Name = "EnterCompactRoute";
            compact.Disabled = _actionBusy || _npcEncounterBusy;
            compact.Pressed += () => { if (!_actionBusy && !_npcEncounterBusy) CompactRouteRequested?.Invoke(); };
            row.AddChild(compact);
        }
        if (is_route_exit(_current))
        {
            var leave = make_button(_dynamicMode ? "离开" + route_display_name : "离开林地", 116, 32); leave.Name = "LeaveDungeon";
            leave.Pressed += offer_exit; row.AddChild(leave);
        }
        var choices = available_actions(_current);
        foreach (var action in choices)
        {
            var actionRow = new HBoxContainer(); actionRow.AddThemeConstantOverride("separation", 10); _actionRows.AddChild(actionRow);
            var button = make_button(local_action_name(action), 0, 36); button.Name = "Action_" + action.Id;
            button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            button.Disabled = _actionBusy || _npcEncounterBusy;
            button.Pressed += () =>
            {
                if (action.ItemPath.Length > 0 || action.Battle) execute_named_action(_current, action.Id);
                else offer_action(action);
            };
            actionRow.AddChild(button);
            var detail = make_button("详情", 54, 36); detail.Name = "Detail_" + action.Id;
            detail.Pressed += () => offer_action(action); actionRow.AddChild(detail);
            var summary = WorldHudStyle.Text(local_action_summary(action), 14, true);
            summary.AutowrapMode = TextServer.AutowrapMode.WordSmart; _actionRows.AddChild(summary);
        }
        var inspect = make_button("此地见闻", 116, 32); inspect.Pressed += () => show_action_result("此地见闻", node?.Description ?? ""); row.AddChild(inspect);
        if (choices.Count == 0)
        {
            var done = WorldHudStyle.Text("此地暂无待办，可继续行进。", 16, true); done.HorizontalAlignment = HorizontalAlignment.Center; _actionRows.AddChild(done);
        }
        _actionBar.ResetSize();
        CallDeferred(nameof(position_current_node_actions));
    }
    private void offer_action(EastWoodsActions.ActionDef action)
    {
        var remaining = action.Stock > 0 ? $"　·　剩余 {action_remaining(_current, action)} 份" : "";
        var item = action.ItemPath.Length > 0 ? EastWoodsActions.Item(action.ItemPath) : null;
        open_action_card(item is null ? action.Name : action.Name + " · " + item.entity_name, action.Description,
            $"耗时 {action.Hours:0.#} 小时　·　精力 {action.Stamina:0.#}{remaining}\n查看和取消不消耗；耗时行动期间其他修行者仍会行进。", action.Id);
        if (item is not null && ResourceLoader.Exists(item.icon_path)) { _actionIcon!.Texture = GD.Load<Texture2D>(item.icon_path); _actionIcon.Show(); }
    }
    private void open_action_card(string title, string body, string cost, string action)
    {
        clear_hover();
        _offeredAction = action;
        _actionIcon!.Hide();
        _actionTitle!.Text = title; _actionBody!.Text = body; _actionCost!.Text = cost;
        _actionConfirm!.Visible = action.Length > 0;
        _actionConfirm.Text = "确认行动";
        _actionModal!.Show(); _actionModal.MoveToFront();
    }
}
