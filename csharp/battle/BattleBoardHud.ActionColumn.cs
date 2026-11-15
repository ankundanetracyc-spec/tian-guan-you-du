using Godot;

public partial class BattleBoardHud
{
    private sealed record ActionColumnItem(string PlacementId, int Column, string Name, string IconPath,
        float? Speed, string State, bool IsCurrent, bool IsEmpty = false);

    private Control? _actionColumn;
    private BattleBoardSkinPanel? _actionColumnFrame;
    private Label? _actionColumnTitle, _actionColumnHint;
    private VBoxContainer? _actionColumnRows;
    private string _actionColumnSignature = "";

    private void CreateActionColumn()
    {
        _actionColumn = new Control
        {
            Name = "PlayerActionColumn",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false,
            ZIndex = 32
        };
        _actionColumnFrame = new BattleBoardSkinPanel
        {
            Name = "ActionColumnFrame",
            Part = "tooltip_panel",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _actionColumn.AddChild(_actionColumnFrame);

        _actionColumnTitle = new Label
        {
            Name = "ActionColumnTitle",
            Position = new(14, 10),
            Size = new(276, 26),
            Text = "本回合行动",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _actionColumnTitle.AddThemeFontSizeOverride("font_size", 19);
        _actionColumnTitle.AddThemeColorOverride("font_color", BattleBoardSkin.PaperText);
        _actionColumn.AddChild(_actionColumnTitle);

        _actionColumnHint = new Label
        {
            Name = "ActionColumnHint",
            Position = new(14, 36),
            Size = new(276, 22),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Text = "左侧在前，依次向下"
        };
        _actionColumnHint.AddThemeFontSizeOverride("font_size", 14);
        _actionColumnHint.AddThemeColorOverride("font_color", new Color("d5c8a9"));
        _actionColumn.AddChild(_actionColumnHint);

        _actionColumnRows = new VBoxContainer
        {
            Name = "ActionColumnRows",
            Position = new(12, 62),
            Size = new(280, 0),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _actionColumnRows.AddThemeConstantOverride("separation", 4);
        _actionColumn.AddChild(_actionColumnRows);
        AddChild(_actionColumn);
    }

    private void UpdateActionColumn(CombatBoardRoundState state)
    {
        if (_actionColumn is null || _actionColumnRows is null || _manager is null || _owner is null) return;
        _actionColumn.Visible = !_editor.Visible && _manager.battle_running;
        if (!_actionColumn.Visible) return;

        var row = state.SelectedRow;
        var activeId = _manager.get_board_active_placement_id(_owner);
        List<ActionColumnItem> items = row >= 0 ? BuildActionColumnItems(state, row, activeId) : new();
        _actionColumnTitle!.Text = row >= 0 ? $"第 {row + 1} 行行动" : "本回合行动";
        _actionColumnHint!.Text = row >= 0
            ? "从左至右展开 · 队首显示出手值"
            : "选择一行后，横向顺序竖直展开";

        var height = 62f + items.Count * 47f + Math.Max(0, items.Count - 1) * 4f;
        _actionColumn.Size = new Vector2(304, Math.Max(104, height));
        _actionColumn.Position = new Vector2(292, BattleUiArt.Canvas.Y - 26 - _actionColumn.Size.Y);
        _actionColumnFrame!.Size = _actionColumn.Size;
        _actionColumnRows.Size = new Vector2(280, items.Count * 47f + Math.Max(0, items.Count - 1) * 4f);

        var signature = $"{state.Round}/{row}/{activeId}/" + string.Join("|", items.Select(item =>
            $"{item.PlacementId}:{item.Column}:{item.Name}:{item.IconPath}:{item.Speed:0.0}:{item.State}:{item.IsCurrent}:{item.IsEmpty}"));
        if (signature == _actionColumnSignature) return;
        _actionColumnSignature = signature;
        foreach (var child in _actionColumnRows.GetChildren())
        {
            _actionColumnRows.RemoveChild(child);
            child.QueueFree();
        }

        for (var index = 0; index < items.Count; index++)
            _actionColumnRows.AddChild(CreateActionColumnRow(items[index], index));
    }

    private List<ActionColumnItem> BuildActionColumnItems(CombatBoardRoundState state, int row, string activeId)
    {
        var result = new List<ActionColumnItem>();
        var layout = state.Layout;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var column = 0; column < layout.size.X; column++)
        {
            var cell = new Vector2I(column, row);
            if (layout.unlocked_cells.Count > 0 && !layout.unlocked_cells.Contains(cell)) continue;
            var placement = CombatBoardGeometry.At(layout, cell, _catalog.FindSkill);
            if (placement is null)
            {
                result.Add(new("", column, $"空格 · 回复体力 1", "", null, "经过时生效", false, true));
                continue;
            }
            if (!seen.Add(placement.placement_id)) continue;

            string name, icon = "";
            switch (placement.kind)
            {
                case CombatBoardPlacement.ObjectKind.Skill:
                    if (_manager.get_board_placement_skill(_owner, placement) is not { } boardSkill) continue;
                    name = boardSkill.skill_name;
                    icon = boardSkill.icon_path;
                    break;
                case CombatBoardPlacement.ObjectKind.Pill:
                    if (BattleSupplyRules.Load(placement.source_key) is not { } pill) continue;
                    name = placement.expended ? "空瓶 · 不行动" : pill.entity_name;
                    icon = pill.icon_path;
                    break;
                case CombatBoardPlacement.ObjectKind.Talisman:
                    if (TalismanCombatCatalog.Load(placement.source_key) is not { } talisman) continue;
                    name = placement.expended ? "符痕 · 不行动" : talisman.entity_name;
                    icon = talisman.icon_path;
                    break;
                case CombatBoardPlacement.ObjectKind.Rewrite:
                    name = "化法 · 不单独行动";
                    break;
                case CombatBoardPlacement.ObjectKind.ZhouTianIndicator:
                    name = "周天指示 · 不单独行动";
                    break;
                default:
                    continue;
            }

            var current = placement.placement_id == activeId;
            var status = ActionColumnStatus(state, placement, current);
            var isAction = !placement.expended && (placement.kind == CombatBoardPlacement.ObjectKind.Skill
                || placement.kind == CombatBoardPlacement.ObjectKind.Talisman
                || placement.kind == CombatBoardPlacement.ObjectKind.Pill);
            var speed = isAction ? _manager.get_board_placement_speed(_owner, placement) : 0f;
            result.Add(new(placement.placement_id, column, name, icon, isAction ? speed : null,
                status, current));
        }
        return result;
    }

    private string ActionColumnStatus(CombatBoardRoundState state, CombatBoardPlacement placement, bool current)
    {
        if (placement.expended) return "已使用";
        if (placement.kind is CombatBoardPlacement.ObjectKind.Rewrite or CombatBoardPlacement.ObjectKind.ZhouTianIndicator)
            return "不参与出手排序";
        if (state.IsArmed(placement.placement_id)) return "待命中";
        if (current) return "当前队首";
        if (state.HasSucceeded(placement.placement_id)) return "已施展";
        if (_manager.is_board_dazed(_owner)) return "失神 · 跳过";
        if (state.HasBeenChecked(placement.placement_id)) return "已跳过";
        return "待行动";
    }

    private Control CreateActionColumnRow(ActionColumnItem item, int index)
    {
        var row = new PanelContainer
        {
            Name = $"ActionColumnItem{index + 1}",
            CustomMinimumSize = new Vector2(280, 47),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        var background = new StyleBoxFlat
        {
            BgColor = item.IsCurrent ? new Color("665735") : item.IsEmpty ? new Color(0.15f, 0.18f, 0.16f, 0.82f) : new Color(0.20f, 0.23f, 0.20f, 0.92f),
            BorderColor = item.IsCurrent ? new Color("e0bd66") : new Color(0.47f, 0.44f, 0.37f, 0.66f),
            BorderWidthLeft = item.IsCurrent ? 2 : 1,
            BorderWidthTop = item.IsCurrent ? 2 : 1,
            BorderWidthRight = item.IsCurrent ? 2 : 1,
            BorderWidthBottom = item.IsCurrent ? 2 : 1,
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            CornerRadiusBottomRight = 3,
            CornerRadiusBottomLeft = 3,
            ContentMarginLeft = 8,
            ContentMarginRight = 8,
            ContentMarginTop = 3,
            ContentMarginBottom = 3
        };
        row.AddThemeStyleboxOverride("panel", background);

        var line = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        line.AddThemeConstantOverride("separation", 7);
        row.AddChild(line);
        var number = new Label
        {
            Text = $"{index + 1}",
            CustomMinimumSize = new Vector2(18, 0),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        number.AddThemeFontSizeOverride("font_size", 15);
        number.AddThemeColorOverride("font_color", item.IsCurrent ? new Color("f3d982") : new Color("c3b89d"));
        line.AddChild(number);

        if (!string.IsNullOrWhiteSpace(item.IconPath) && BattleBoardView.Icon(item.IconPath) is { } texture)
        {
            line.AddChild(new TextureRect
            {
                Texture = texture,
                CustomMinimumSize = new Vector2(30, 30),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Modulate = item.State == "已施展" ? new Color(0.78f, 0.82f, 0.76f, 0.62f) : Colors.White
            });
        }

        var words = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            Alignment = BoxContainer.AlignmentMode.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        line.AddChild(words);
        var title = new Label
        {
            Text = item.Name,
            ClipText = true,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        title.AddThemeFontSizeOverride("font_size", 16);
        title.AddThemeColorOverride("font_color", item.IsCurrent ? Colors.White : BattleBoardSkin.PaperText);
        words.AddChild(title);
        var detail = new Label
        {
            Text = item.Speed is { } speed
                ? $"{(item.IsCurrent ? "出手值" : "预估出手值")} {speed:0.0} · {item.State}"
                : item.State,
            ClipText = true,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        detail.AddThemeFontSizeOverride("font_size", 12);
        detail.AddThemeColorOverride("font_color", item.IsCurrent ? new Color("f3d982") : new Color("d1c7ae"));
        words.AddChild(detail);
        return row;
    }
}
