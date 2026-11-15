using Godot;

public partial class FormationGuardPanel
{
    private void BuildPaperUi()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); ZIndex = 200; MouseFilter = MouseFilterEnum.Stop;
        var design = FormationPaperChrome.Mount(this);
        var margin = new MarginContainer(); margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 24); margin.AddThemeConstantOverride("margin_right", 24);
        margin.AddThemeConstantOverride("margin_top", 10); margin.AddThemeConstantOverride("margin_bottom", 18);
        design.AddChild(margin);
        var layout = new VBoxContainer(); layout.AddThemeConstantOverride("separation", 12); margin.AddChild(layout);

        var header = FormationPaperChrome.Paper("GuardPaperHeader", 8); layout.AddChild(header);
        var heading = new HBoxContainer(); heading.AddThemeConstantOverride("separation", 16); header.AddChild(heading);
        var title = Label("守攻阵 · 试演", 22); title.AutowrapMode = TextServer.AutowrapMode.Off; title.SizeFlagsVertical = SizeFlags.ShrinkCenter; heading.AddChild(title);
        heading.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        var hint = Label("本页演练不影响人物与物品", 18, "786c56"); hint.AutowrapMode = TextServer.AutowrapMode.Off; hint.SizeFlagsVertical = SizeFlags.ShrinkCenter; heading.AddChild(hint);
        heading.AddChild(FormationPaperChrome.Return("GuardReturn", Close));

        var states = FormationPaperChrome.Paper("GuardPaperStates", 12, false); layout.AddChild(states);
        _stateGrid = new GridContainer { Columns = 7 }; _stateGrid.AddThemeConstantOverride("h_separation", 18); states.AddChild(_stateGrid);
        foreach (var (key, caption) in new[] { ("health", "本人生命"), ("mana", "本人法力"), ("enemy", "敌方生命"), ("shield", "公共护盾"), ("guard", "本器护罩"), ("source", "阵源"), ("mode", "守攻许可") })
        {
            var item = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; _stateGrid.AddChild(item);
            item.AddChild(Label(caption, 18, "786c56")); var value = Label("—", 22); value.Name = "GuardState_" + key; item.AddChild(value); _readouts[key] = value;
        }

        var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill }; body.AddThemeConstantOverride("separation", 22); layout.AddChild(body);
        var graph = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill }; graph.AddThemeConstantOverride("separation", 8); body.AddChild(graph);
        var tools = FormationPaperChrome.Paper("GuardPaperTools", 8, false); graph.AddChild(tools);
        var toolRow = new HBoxContainer(); toolRow.AddThemeConstantOverride("separation", 8); tools.AddChild(toolRow);
        var graphTitle = Label("阵盘", 22); graphTitle.AutowrapMode = TextServer.AutowrapMode.Off; graphTitle.SizeFlagsVertical = SizeFlags.ShrinkCenter; toolRow.AddChild(graphTitle);
        toolRow.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _viewMode = new OptionButton { Name = "GuardViewMode", CustomMinimumSize = new Vector2(128, 46) };
        _viewMode.AddItem("阵盘"); _viewMode.AddItem("流程"); _viewMode.ItemSelected += _ => ApplyView(); toolRow.AddChild(_viewMode);
        _enlarge = Button("放大阵盘", "GuardEnlargeBoard", OpenBoardCloseup); toolRow.AddChild(_enlarge);
        var visual = new Control { Name = "GuardVisual", CustomMinimumSize = new Vector2(570, 350), SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill }; graph.AddChild(visual);
        _board = new FormationGuardBoardCanvas { Name = "GuardBoardCanvas", ShowLabels = false }; _board.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); visual.AddChild(_board);
        _flow = new GuardFlowCanvas { Name = "GuardLiveGraph", Visible = false }; _flow.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); visual.AddChild(_flow);
        var summary = FormationPaperChrome.Paper("GuardPaperSummary", 10, false); graph.AddChild(summary);
        var summaryLines = new VBoxContainer(); summary.AddChild(summaryLines);
        _viewHint = Label("朱砂传信号，赭金供能。悬停查看部件。", 18, "786c56"); summaryLines.AddChild(_viewHint);
        _totals = Label("", 18); _totals.Name = "GuardTotals"; summaryLines.AddChild(_totals);

        var tabs = new TabContainer { Name = "GuardSidebar", CustomMinimumSize = new Vector2(390, 0), SizeFlagsVertical = SizeFlags.ExpandFill }; body.AddChild(tabs);
        tabs.AddThemeStyleboxOverride("panel", CraftingPaperHeader.Paper(14));
        tabs.AddThemeFontSizeOverride("font_size", 22);
        tabs.AddThemeStyleboxOverride("tab_selected", CraftingPaperStyle.ButtonStyle(CraftingPaperStyle.ButtonKind.Regular, CraftingPaperStyle.ButtonState.Selected));
        tabs.AddThemeStyleboxOverride("tab_unselected", CraftingPaperStyle.ButtonStyle(CraftingPaperStyle.ButtonKind.Regular));
        tabs.AddThemeStyleboxOverride("tab_hovered", CraftingPaperStyle.ButtonStyle(CraftingPaperStyle.ButtonKind.Regular, CraftingPaperStyle.ButtonState.Hover));
        tabs.AddThemeColorOverride("font_selected_color", CraftingPaperStyle.Ink); tabs.AddThemeColorOverride("font_unselected_color", CraftingPaperStyle.Ink);
        var bar = tabs.GetTabBar(); bar.AddThemeFontSizeOverride("font_size", 22);
        bar.AddThemeStyleboxOverride("tab_selected", CraftingPaperStyle.ButtonStyle(CraftingPaperStyle.ButtonKind.Regular, CraftingPaperStyle.ButtonState.Selected));
        bar.AddThemeStyleboxOverride("tab_unselected", CraftingPaperStyle.ButtonStyle(CraftingPaperStyle.ButtonKind.Regular));
        bar.AddThemeStyleboxOverride("tab_hovered", CraftingPaperStyle.ButtonStyle(CraftingPaperStyle.ButtonKind.Regular, CraftingPaperStyle.ButtonState.Hover));
        bar.AddThemeColorOverride("font_selected_color", CraftingPaperStyle.Ink); bar.AddThemeColorOverride("font_unselected_color", CraftingPaperStyle.Ink);
        BuildOperations(Page(tabs, "操作")); BuildParameters(Page(tabs, "试验参数"));
        tabs.SetTabTitle(1, "试演设置");

        var history = FormationPaperChrome.Paper("GuardPaperHistory", 10, false); layout.AddChild(history);
        _log = new RichTextLabel { Name = "GuardHistory", BbcodeEnabled = false, FitContent = false, ScrollActive = true, CustomMinimumSize = new Vector2(0, 70), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _log.AddThemeFontSizeOverride("normal_font_size", 18); history.AddChild(_log);
        RestartTrial();
    }
}
