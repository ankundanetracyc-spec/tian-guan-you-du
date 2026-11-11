using Godot;
using Kind = FormationGridModel.Kind;

public partial class FormationWorkbench
{
    public event Action? Closed;
    private readonly System.Collections.Generic.List<Button> _paperInspectorTabs = [];
    private Control? _formationHelp;

    private void BuildPaperWorkbench()
    {
        Theme = CraftingPaperStyle.MakeTheme();
        AddThemeConstantOverride("separation", 12);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;
        MouseFilter = MouseFilterEnum.Stop;

        BuildPaperHeader();
        var content = new HBoxContainer { Name = "FormationPaperColumns", SizeFlagsVertical = SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 16); AddChild(content);
        BuildPaperParts(content);
        BuildPaperBoard(content);
        BuildPaperInspector(content);

    }

    private void BuildPaperHeader()
    {
        var headerRow = new HBoxContainer { Name = "FormationHeaderRow", CustomMinimumSize = new Vector2(0, 62) };
        headerRow.AddThemeConstantOverride("separation", 16); AddChild(headerRow);
        var header = new PanelContainer { Name = "FormationPaperHeader", CustomMinimumSize = new Vector2(0, 62), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        header.AddThemeStyleboxOverride("panel", CraftingPaperHeader.Paper(0, true, 1f)); headerRow.AddChild(header);
        var inset = new MarginContainer(); inset.AddThemeConstantOverride("margin_left", 14); inset.AddThemeConstantOverride("margin_right", 14); inset.AddThemeConstantOverride("margin_top", 8); inset.AddThemeConstantOverride("margin_bottom", 8); header.AddChild(inset);
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 12); inset.AddChild(row);
        var help = HeaderButton("阵法说明", "FormationHelp", OpenFormationHelp); help.CustomMinimumSize = new Vector2(136, 46); row.AddChild(help);
        BuildBoardSize(row);
        row.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        row.AddChild(HeaderButton("保存草图", "SaveDraft", OpenSaveDraftDialog));
        row.AddChild(HeaderButton("读取草图", "LoadDraft", OpenLoadDraftDialog));
        var back = FormationPaperChrome.Return("ReturnFormationWorkspace", () => Closed?.Invoke());
        back.SizeFlagsVertical = SizeFlags.ShrinkCenter; headerRow.AddChild(back);
    }

    private static Button HeaderButton(string caption, string name, Action action)
    {
        var button = Button(caption, action); button.Name = name;
        button.CustomMinimumSize = new Vector2(0, 46); CraftingPaperStyle.ApplyButton(button, fontSize: 22); return button;
    }

    private void BuildPaperParts(HBoxContainer content)
    {
        var paper = new PanelContainer { Name = "FormationPartsPaper", CustomMinimumSize = new Vector2(260, 0) };
        paper.AddThemeStyleboxOverride("panel", CraftingPaperHeader.Paper(16, true, 1f)); content.AddChild(paper);
        var body = new VBoxContainer(); body.AddThemeConstantOverride("separation", 12); paper.AddChild(body);
        BuildPartCategories(body);
        _examples = new OptionButton { Name = "LogicExamples", Visible = false };
        foreach (var caption in new[] { "基础直连", "筛选器", "计数器", "计数器·先发一次", "分流与合流" }) _examples.AddItem("示例：" + caption);
        _examples.AddItem("守攻阵（5×5）");
        StylePaperOption(_examples, 18);
        _examples.ItemSelected += index => LoadExample((int)index); body.AddChild(_examples);
    }

    private void BuildPaperBoard(HBoxContainer content)
    {
        var center = new VBoxContainer { Name = "FormationDrawingArea", SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        center.AddThemeConstantOverride("separation", 8); content.AddChild(center);
        Canvas = new FormationBoardCanvas { Name = "FormationBoardCanvas", SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill }; center.AddChild(Canvas);
    }

    private void BuildPaperInspector(HBoxContainer content)
    {
        var paper = new PanelContainer { Name = "FormationInspectorPaper", CustomMinimumSize = new Vector2(370, 0) };
        paper.AddThemeStyleboxOverride("panel", CraftingPaperHeader.Paper(16, true, 1f)); content.AddChild(paper);
        var body = new VBoxContainer(); body.AddThemeConstantOverride("separation", 10); paper.AddChild(body);
        var tabs = new HBoxContainer(); tabs.AddThemeConstantOverride("separation", 3); body.AddChild(tabs);
        foreach (var (caption, index) in new[] { ("部件", 0), ("调试", 1), ("信息", 2) })
        {
            var button = Button(caption, () => { _inspector.CurrentTab = index; RefreshPaperInspectorTabs(); });
            button.Name = index == 0 ? "FormationParametersTab" : index == 1 ? "FormationSignalTrialTab" : "FormationControlTrialTab";
            button.ToggleMode = true; button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            CraftingPaperStyle.ApplyButton(button, CraftingPaperStyle.ButtonKind.Regular, 20); tabs.AddChild(button); _paperInspectorTabs.Add(button);
        }
        _inspector = new TabContainer { Name = "LogicInspector", TabsVisible = false, SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _inspector.AddThemeStyleboxOverride("panel", new StyleBoxEmpty()); body.AddChild(_inspector);
        BuildParameters(); BuildEditableTrial();
        var info = InspectorPage("阵盘信息");
        info.AddChild(Text("阵盘信息", 24, "ded6be"));
        _counts = Text("", 18, "d4c8ab"); _counts.Name = "FormationCounts"; info.AddChild(_counts);
        var coordinates = Text("坐标先列后行，左上(1,1)，向右数列、向下数行。", 18, "aeb8aa"); coordinates.Name = "FormationCoordinateHelp"; info.AddChild(coordinates);
        _validation = Text("", 18, "b8c6b3"); _validation.Name = "FormationValidation"; info.AddChild(_validation);
        _status = Text("", 18, "d8c39e"); _status.Name = "FormationStatus"; info.AddChild(_status);
        BuildTrial();
        _inspector.TabChanged += _ => RefreshPaperInspectorTabs();
        RefreshPaperInspectorTabs();
        _selection = Text("", 18, "aeb8aa");
        _selection.Name = "SelectionInfo"; _selection.Visible = false; body.AddChild(_selection);
    }

    private void RefreshPaperInspectorTabs()
    {
        for (var index = 0; index < _paperInspectorTabs.Count; index++)
        {
            var selected = index == _inspector.CurrentTab; var button = _paperInspectorTabs[index];
            SetPaperSelected(button, selected, true);
            button.AddThemeFontSizeOverride("font_size", 17);
            foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled" })
                button.AddThemeStyleboxOverride(state, CraftingPaperStyle.ButtonStyle(CraftingPaperStyle.ButtonKind.Regular, selected ? CraftingPaperStyle.ButtonState.Selected : CraftingPaperStyle.ButtonState.Normal, 3, 7));
        }
    }

    private static void StylePaperOption(OptionButton option, int fontSize = 20)
    {
        CraftingPaperStyle.ApplyButton(option, CraftingPaperStyle.ButtonKind.Input, fontSize);
        option.CustomMinimumSize = new Vector2(option.CustomMinimumSize.X, 44);
    }

    private static void SetPaperSelected(Button button, bool selected, bool tab = false)
    {
        button.SetPressedNoSignal(selected);
        CraftingPaperStyle.ApplyButton(button, tab ? CraftingPaperStyle.ButtonKind.Regular : CraftingPaperStyle.ButtonKind.Regular, 20, selected);
    }

    private void OpenFormationHelp()
    {
        if (GodotObject.IsInstanceValid(_formationHelp)) return;
        var overlay = new Control { Name = "FormationHelpOverlay", MouseFilter = MouseFilterEnum.Stop, ZIndex = 20 };
        // 弹页挂到同一设计画布，避免作为VBox子项挤占阵盘，且继续继承宿主的等比缩放。
        var host = GetParent();
        while (host is Container && host.GetParent() is Control ancestor) host = ancestor;
        host.AddChild(overlay); overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); _formationHelp = overlay;
        var shade = new ColorRect { Color = new Color(0, 0, 0, .36f), MouseFilter = MouseFilterEnum.Stop };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); overlay.AddChild(shade);
        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore }; center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); overlay.AddChild(center);
        var paper = new PanelContainer { CustomMinimumSize = new Vector2(780, 660) };
        paper.AddThemeStyleboxOverride("panel", CraftingPaperHeader.Paper(26, true, 1f)); center.AddChild(paper);
        var body = new VBoxContainer(); body.AddThemeConstantOverride("separation", 16); paper.AddChild(body);
        var heading = new HBoxContainer(); body.AddChild(heading);
        heading.AddChild(Text("阵法说明", 26, "ded6be")); heading.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        var close = Button("返回", () => { overlay.QueueFree(); _formationHelp = null; }); close.Name = "CloseFormationHelp"; heading.AddChild(close);
        body.AddChild(CraftingPaperStyle.Divider());
        var explanation = new RichTextLabel
        {
            Name = "FormationHelpText", BbcodeEnabled = false, ScrollActive = true, FitContent = false,
            CustomMinimumSize = new Vector2(710, 510), SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill,
            Text = "绘制阵图\n顶栏可选4×4或5×5；扩缩盘保留部件与线路，放不下时保持原图。左侧直接拖部件到盘上；绿色能放，红色不能放。已有部件可按住拖动。R转向，右键取下部件或擦除线路。\n\n接线与坐标\n朱砂细线传信号，浅赭金粗线供能。按Q铺信号线，按E铺供能线；盘上只亮当前线种可接的接口和线路，从亮起处按住左键拖到目标，松开完成。Q与E可随时切换，Esc退出，右键擦除。未接通的悬空线松手时会清除。信号分事件、开关和数值，接口类型与数值单位都要相同。单层不能交叉，分流、合流须用对应部件。\n坐标先列后行：左上角为(1,1)，向右数列，向下数行。短报错后的坐标指出需要检查的格子。\n\n感应与设定\n左侧选择气血、护盾、法力或阵能感应件；感应件固定读取对应的真实状态。术法感应接收成功施法。感应来源、事件和强度由战斗事实带出，不需要玩家填写。比较器设置阈值，开关设置放行条件，计数器设置间隔；这些定义的是阵法如何响应。\n\n阵法运行\n在“调试”页施展术法、调整试演人物状态或推进回合，观察当前阵图如何响应。试演按钮只提供具体战斗事实，不伪造事件名、来源标签或信号幅值。\n\n观看传导\n每次运行后，朱砂脉点沿信号线传递，赭金脉点沿本次实际供能的线路流向输出。空心脉点表示开关为关，未知没有脉点；被拦的事件不会继续前进。点“回放传导”可重看，不重复发动或扣能。\n\n运行与保存\n改图后重开；保存和切页不重置账本，同一战斗事实不会重复结算。草图未接完也可保存，坏档不覆盖当前图；保存不消耗材料、不授予实物。"
        };
        explanation.AddThemeFontSizeOverride("normal_font_size", 20); explanation.AddThemeColorOverride("default_color", CraftingPaperStyle.Ink); body.AddChild(explanation);
    }

    public override void _ExitTree()
    {
        ClearFlowPlayback();
        ReleaseEditableTrial();
        if (GodotObject.IsInstanceValid(_draftOverlay) && !_draftOverlay!.IsQueuedForDeletion()) _draftOverlay.QueueFree();
        _draftOverlay = null;
        if (GodotObject.IsInstanceValid(_formationHelp) && !_formationHelp!.IsQueuedForDeletion()) _formationHelp.QueueFree();
        _formationHelp = null;
    }
}
