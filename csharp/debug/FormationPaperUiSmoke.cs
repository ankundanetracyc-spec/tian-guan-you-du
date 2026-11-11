using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Environment = System.Environment;
using Cell = FormationGridModel.Cell;
using Kind = FormationGridModel.Kind;
using PortKind = FormationGridModel.PortKind;
using GuardAction = FormationGuardAssaultRuntime.ActionKind;

/// <summary>纸墨阵法内页的隔离原生点击验收；不载入人物旅程，不生成正式器物。</summary>
public partial class FormationPaperUiSmoke : Control
{
    private const string EvidenceRoot = "res://docs/美术规范/阵法纸墨木石界面_20261109/格线与快捷键修订/全链兼容";
    private readonly List<string> _checks = [];
    private readonly List<object> _bounds = [];
    private readonly List<object> _captures = [];
    private BaigongPanel _archive = null!;
    private Character _owner = null!;
    private FormationPaperWorkspace _workspace = null!;
    private FormationWorkbench Workbench => _workspace.Workbench;
    private FormationGuardPanel? _guard;
    private int _width, _height;
    private bool PreviewOnly => OS.GetCmdlineUserArgs().Contains("--formation-paper-preview-only");
    private string Evidence => EvidenceRoot + "/" + _width;
    private string _stage = "initialization";
    private Vector2 _lastPointer;

    public override async void _Ready()
    {
        _width = ReadWidth(); _height = _width == 1920 ? 1080 : 720;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        GetWindow().Mode = Window.ModeEnum.Windowed;
        GetWindow().Size = new(_width, _height);
        GetWindow().ContentScaleSize = new(_width, _height);
        GetWindow().GuiEmbedSubwindows = true;
        GetWindow().Position = new(-30000, -30000);
        if (!OS.GetCmdlineUserArgs().Contains("--formation-paper-ui-smoke")) return;
        try
        {
            await Frames(8);
            Check(GetViewport().GetVisibleRect().Size.IsEqualApprox(new Vector2(_width, _height)), "视口采用本档真实分辨率");
            var profile = Environment.GetEnvironmentVariable("APPDATA") ?? "";
            Check(profile.Contains("formation_paper_ui", StringComparison.OrdinalIgnoreCase), "启动器使用本验收专属APPDATA");
            var background = new ColorRect { Color = new Color("333d37"), MouseFilter = MouseFilterEnum.Ignore };
            background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(background);
            _owner = new Character { Name = "FormationPaperIsolatedOwner" };
            _archive = new BaigongPanel { Name = "FormationPaperBaigong" }; AddChild(_archive);
            _archive.show_for(_owner); await Frames(5);
            Check(!_owner.IsInsideTree(), "百工验收人物未进入世界或载入旅程");
            _stage = "百工真实入口";
            await Capture("01_百工入口.png");
            await Click(Named<Button>(_archive, "OpenFormationWorkspace"));
            await Frames(5);
            _workspace = GetTree().Root.GetNodeOrNull<FormationPaperWorkspace>("FormationPaperWorkspace")
                ?? throw new Exception("百工按钮未打开FormationPaperWorkspace");
            Check(_workspace.IsVisibleInTree() && Workbench.IsVisibleInTree(), "实际点击百工入口打开全屏原生工作台");
            Workbench.DraftPath = "user://formation_paper_ui/" + _width + "/isolated_draft.json";
            Check(ProjectSettings.GlobalizePath(Workbench.DraftPath).StartsWith(OS.GetUserDataDir(), StringComparison.OrdinalIgnoreCase), "草图只写隔离用户目录");
            await MoveOutsideBoard();
            Check(Workbench.Canvas.GridVisible && !Workbench.Canvas.GuidesVisible, "默认保留格线，移动模式不常亮动态落点");
            Check(Workbench.Canvas.Model.IsCircuitConnected && FormationGridModel.Size == 4, "原基础4×4草图及连通状态保持");
            VerifyWorkspaceStyle(); VerifyArt();
            await Capture("02_阵法全屏工作台.png");
            if (PreviewOnly)
            {
                _stage = "首轮画面预览";
                await Click(Find<Button>("OpenGuardTrial")); await Frames(5);
                _guard = GetTree().Root.GetNodeOrNull<FormationGuardPanel>("FormationGuardPanel") ?? throw new Exception("首轮预览未打开守攻页");
                VerifyGuardStyle(_guard);
                await SetNumber(Named<SpinBox>(_guard, "GuardHealthInput"), "20"); await Click(Named<Button>(_guard, "GuardSetHealth"));
                await MoveTo(new Vector2(10, _height - 10)); await Capture("09_守攻低生命开罩.png");
                await Click(Named<Button>(_guard, "GuardReturn")); await Frames(3);
                WriteReport(true, null);
                _archive.QueueFree(); _owner.Free(); await Frames(2);
                GD.Print($"FORMATION_PAPER_UI_PASS mode=preview width={_width} height={_height} checks={_checks.Count}");
                GetTree().Quit(); return;
            }

            _stage = "部件选择与拖放";
            await PlacementAndDragging();
            _stage = "真实铺线与禁止交叉";
            await WiresAndCrossing();
            _stage = "参数与草图读写";
            await CounterAndPersistence();
            _stage = "筛选与信号试运行";
            await FilterTrial();
            _stage = "悬停与拖动抑制";
            await HoverAndSuppression();
            _stage = "守攻同族页面与真实效果";
            await GuardTrial();

            _stage = "返回百工";
            await Click(Find<Button>("ReturnFormationWorkspace")); await Frames(5);
            Check(!GodotObject.IsInstanceValid(_workspace), "返回释放全屏工作台");
            Check(_archive.IsVisibleInTree() && Named<Button>(_archive, "OpenFormationWorkspace").IsVisibleInTree(), "工作台返回后百工入口仍可继续使用");
            Check(GetTree().Root.GetNodeOrNull<FormationGuardPanel>("FormationGuardPanel") is null, "返回后未遗留守攻页");
            await Capture("12_返回百工.png");
            WriteReport(true, null);
            _archive.QueueFree(); _owner.Free();
            GD.Print($"FORMATION_PAPER_UI_PASS width={_width} height={_height} checks={_checks.Count}");
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            try { await Capture("失败现场.png"); } catch { }
            WriteReport(false, exception.ToString());
            GD.PushError("FORMATION_PAPER_UI_FAIL " + _stage + " " + exception);
            GetTree().Quit(1);
        }
    }

    private static int ReadWidth()
    {
        var argument = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--formation-paper-width=", StringComparison.Ordinal));
        return argument is not null && int.TryParse(argument[(argument.IndexOf('=') + 1)..], out var width) && width == 1280 ? 1280 : 1920;
    }

    private void VerifyWorkspaceStyle()
    {
        Check(_workspace.GetGlobalRect().Size.IsEqualApprox(new Vector2(_width, _height)), "宿主覆盖完整视口而非百工旧内嵌小框");
        var design = Named<Control>(_workspace, "FormationDesign");
        Check(design.Size.IsEqualApprox(new Vector2(1672, 941)) && Math.Abs(design.Scale.X - design.Scale.Y) < .001f, "1672×941设计画布等比缩放");
        var desk = Named<TextureRect>(_workspace, "FormationDesk");
        Check(desk.Texture is not null && desk.MouseFilter == MouseFilterEnum.Ignore, "案面使用真实贴图且不阻挡绘图");
        foreach (var name in new[] { "FormationPaperHeader", "FormationPartsPaper", "FormationInspectorPaper", "FormationBoardSummary" })
        {
            var panel = Find<PanelContainer>(name);
            Check(panel.GetThemeStylebox("panel") is StyleBoxTexture { Texture: not null }, name + "采用纸框纹理");
            RecordBounds(panel);
        }
        var header = Find<PanelContainer>("FormationPaperHeader");
        Check(Math.Abs(header.Size.Y - 62) < 1, "顶栏采用62设计单位紧凑行");
        var notice = Find<PanelContainer>("FormationDraftNoticePaper");
        var noticeText = Find<Label>("FormationDraftBoundary");
        Check(notice.GetThemeStylebox("panel") is StyleBoxTexture && notice.GetGlobalRect().Encloses(noticeText.GetGlobalRect().Grow(-1)),
            "草图边界提示完整落在自身浅纸面内");
        RecordBounds(notice);
        Check(Workbench.Canvas.Size.X >= 390 && Workbench.Canvas.Size.Y >= 390, "提示纸面没有挤坏中央画布最小操作面积");
        var headerButtons = new[] { "OpenGuardTrial", "SaveDraft", "LoadDraft", "FormationHelp", "ReturnFormationWorkspace" }.Select(Find<Button>).ToArray();
        foreach (var button in headerButtons)
        {
            Check(button.GetThemeFontSize("font_size") == 22 && Math.Abs(button.Size.Y - 46) < 1, button.Name + "为22字级46高");
            Check(button.GetThemeStylebox("normal") is StyleBoxTexture { Texture: not null }, button.Name + "使用同族纸面按钮");
            RecordBounds(button);
        }
        var centers = headerButtons.Select(b => b.GetGlobalRect().GetCenter().Y).ToArray();
        Check(centers.Max() - centers.Min() <= 2, "顶栏全部按钮实际垂直同心");
        Check(Find<Label>("WorkbenchTitle").GetThemeFontSize("font_size") == 22, "顶栏标题与按钮同字号");
        var back = Find<Button>("ReturnFormationWorkspace");
        Check(back.GetChildren().OfType<Control>().Any(c => c.MouseFilter == MouseFilterEnum.Ignore), "返回带独立且穿透的小三角");
        Check(Workbench.FindChild("FormationDrawingTools", true, false) is null, "盘下工具行已移入标题菜单");
        foreach (var name in new[] { "FormationDrawingArea", "FormationToolMenu", "FormationCounts", "FormationValidation", "LogicInspector" }) RecordBounds(Find<Control>(name));
        var left = Find<Control>("FormationPartsPaper").GetGlobalRect();
        var center = Find<Control>("FormationDrawingArea").GetGlobalRect();
        var right = Find<Control>("FormationInspectorPaper").GetGlobalRect();
        Check(left.End.X <= center.Position.X + 1 && center.End.X <= right.Position.X + 1, "三栏实际边界互不覆盖");
        Check(center.Size.X > left.Size.X && center.Size.X > right.Size.X, "中央阵盘保留三栏中最大宽度");
    }

    private void VerifyArt()
    {
        Check(FormationBoardCanvas.BoardArtPath.Contains("守攻阵铜纹素材_20261109/board_base.png", StringComparison.Ordinal), "基础盘真正使用已选B空盘底路径");
        Check(FormationBoardCanvas.LoadArt(FormationBoardCanvas.BoardArtPath) is not null, "B盘底实际载入纹理");
        foreach (var kind in new[] { Kind.Sensor, Kind.Source, Kind.Ward })
        {
            var path = FormationBoardCanvas.ArtPath(kind);
            Check(path.Contains("铜纹阵盘基础元件_20261108", StringComparison.Ordinal) && FormationBoardCanvas.LoadArt(path) is not null,
                "基础" + kind + "实际使用B同族素材");
        }
        Check(!Workbench.Canvas.GetChildren().OfType<Label>().Any(), "画布未叠加常驻Label控件；绘制文字另由截图目视核对");
    }

    private async Task PlacementAndDragging()
    {
        Workbench.Canvas.SetModel(new FormationGridModel()); Workbench.Refresh("验收准备：空盘。"); await Frames(3);
        await Click(Find<Button>("PartSensor"));
        Check(Workbench.Canvas.Tool == FormationBoardCanvas.EditTool.Component && Find<Button>("PartSensor").ButtonPressed, "实际命中感应件选择按钮");
        CheckSelectedPaper(Find<Button>("PartSensor"));
        await MoveCell(new Cell(0, 0));
        Check(Workbench.Canvas.GuidesVisible, "选择部件并移入盘内时显示真实落点格");
        await ClickCell(new Cell(0, 0));
        var sensor = Workbench.Canvas.Model.Components.Values.Single(p => p.Kind == Kind.Sensor);
        Check(sensor.Origin == new Cell(0, 0), "实际点击画布放置完整感应件");
        await SelectDrawingTool("移动");
        await Press(CellPoint(sensor.Origin));
        Check(Workbench.Canvas.GuidesVisible && HoverInfoManager.IsDragInProgress(), "按住真实部件时格位与全局拖拽抑制同时生效");
        await MoveCell(new Cell(0, 1), true); await Release(CellPoint(new Cell(0, 1)));
        Check(Workbench.Canvas.Model.Components[sensor.Id].Origin == new Cell(0, 1), "真实拖放改变部件原点");
        await Press(CellPoint(new Cell(0, 1))); await MoveCell(new Cell(1, 1), true); await Release(CellPoint(new Cell(1, 1)));
        Check(Workbench.Canvas.Model.Components[sensor.Id].Origin == new Cell(1, 1), "盘心可正常放置与移动部件");
        await MoveOutsideBoard();
        Check(!Workbench.Canvas.GuidesVisible && !HoverInfoManager.IsDragInProgress(), "拖放结束离开盘面后格位与拖动状态收起");
    }

    private async Task WiresAndCrossing()
    {
        Workbench.Canvas.SetModel(new FormationGridModel()); Workbench.Refresh("验收准备：空盘接线。"); await Frames(2);
        await SelectDrawingTool("信号线"); Check(Workbench.Canvas.Tool == FormationBoardCanvas.EditTool.Signal, "菜单选中信号线工具");
        await Press(CellPoint(new Cell(2, 1))); await MoveCell(new Cell(2, 2), true); await MoveCell(new Cell(2, 3), true);
        Check(Workbench.Canvas.GuidesVisible && Workbench.Canvas.Model.Wires.Count == 3, "按住拖线真实形成三个朱砂格，编辑格位可见");
        Check(Workbench.Canvas.Model.Wires.Values.All(k => k == PortKind.Signal), "实际信号工具没有误铺供能");
        await Capture("03_真实铺线与格位.png"); await Release(CellPoint(new Cell(2, 3)));
        await SelectDrawingTool("擦除"); await ClickCell(new Cell(2, 2));
        Check(!Workbench.Canvas.Model.Wires.ContainsKey(new Cell(2, 2)), "实际擦线按钮与画布命中删除中间线格");

        Workbench.Canvas.SetModel(new FormationGridModel()); Workbench.Refresh("验收准备：供能T与交叉拒绝。");
        await SelectDrawingTool("供能线");
        await Press(CellPoint(new Cell(2, 1))); await MoveCell(new Cell(2, 2), true); await MoveCell(new Cell(2, 3), true); await Release(CellPoint(new Cell(2, 3)));
        await ClickCell(new Cell(1, 2));
        Check(Workbench.Canvas.Model.Wires.Count == 4, "同源供能第三向T接点实际允许");
        var before = Workbench.Canvas.Model.Serialize();
        await ClickCell(new Cell(3, 2));
        Check(Workbench.Canvas.Model.Serialize() == before && !Workbench.Canvas.Model.Wires.ContainsKey(new Cell(3, 2)), "真实第四向交叉被拒绝且保留原盘");
        Check(Workbench.Canvas.Model.LastError.Contains("四", StringComparison.Ordinal), "拒绝原因明确指出四向交叉");
        await Capture("04_单层交叉拒绝.png");
        await SelectDrawingTool("移动"); await MoveOutsideBoard();
        Check(Workbench.Canvas.GridVisible && !Workbench.Canvas.GuidesVisible, "切回移动并离开后保留格线而收起动态辅助");
    }

    private async Task CounterAndPersistence()
    {
        Workbench.LoadExample(2); await Frames(3); await SelectPart(Kind.Counter);
        await Inspector(0); await SetNumber(Find<SpinBox>("PartInterval"), "2"); await Click(Find<Button>("ApplyPartParameters"));
        var counter = Workbench.Canvas.Model.Components.Values.Single(p => p.Kind == Kind.Counter);
        Check(counter.Settings.Interval == 2 && !counter.Settings.FirstImmediate, "原生数字键入与应用按钮真实保存数满再发N2");
        await Capture("05_计数器参数纸栏.png");
        await Inspector(1); await Click(Find<Button>("SendTrial"));
        Check(Workbench.LastTrialResult?.OutputNodeIds.Count == 0 && Find<Label>("TrialPhases").Text.Contains("1/2", StringComparison.Ordinal), "第一枚新信号仅计一次并显示真实进度");
        var originalResult = Workbench.LastTrialResult;
        await Click(Find<Button>("SaveDraft"));
        Check(System.IO.File.Exists(ProjectSettings.GlobalizePath(Workbench.DraftPath)) && ReferenceEquals(originalResult, Workbench.LastTrialResult), "实际保存写隔离草图且不清当前计数");
        var before = Workbench.Canvas.Model.Serialize();
        await SelectDrawingTool("转向");
        Check(Workbench.Canvas.Model.Serialize() == before && ReferenceEquals(originalResult, Workbench.LastTrialResult), "越界旋转被拒绝且试运行进度保留");
        await Click(Find<Button>("RepeatTrial"));
        Check(Workbench.LastTrialResult?.Duplicate == true && Workbench.TotalControlRequests == 0 && Find<Label>("TrialPhases").Text.Contains("1/2", StringComparison.Ordinal), "重发按钮不重复计次或产生控制请求");
        await Click(Find<Button>("SendTrial"));
        Check(Workbench.LastTrialResult?.OutputNodeIds.Count == 1 && Workbench.TotalControlRequests == 1, "第二个不同发生抵达护持一次");
        await Capture("06_信号实际试运行.png");
        System.IO.File.WriteAllText(ProjectSettings.GlobalizePath(Workbench.DraftPath), "{\"version\":1,\"broken\":true}");
        await Click(Find<Button>("LoadDraft"));
        Check(Workbench.Canvas.Model.Serialize() == before, "原生读取坏档不覆盖当前盘面");
        await Click(Find<Button>("SaveDraft"));
        await Inspector(0); await SetNumber(Find<SpinBox>("PartInterval"), "3"); await Click(Find<Button>("ApplyPartParameters"));
        Check(Workbench.LastTrialResult is null, "成功修改参数重建试运行");
        await Click(Find<Button>("LoadDraft"));
        Check(Workbench.Canvas.Model.Components.Values.Single(p => p.Kind == Kind.Counter).Settings.Interval == 2 && Workbench.Canvas.Model.IsCircuitConnected,
            "原生读取恢复实际保存的参数与接线");
    }

    private async Task FilterTrial()
    {
        Workbench.LoadExample(1); await Frames(3); await SelectPart(Kind.Filter); await Inspector(0);
        await SetNumber(Find<SpinBox>("FilterMinimum"), "3"); await TypeText(Find<LineEdit>("PartSourceTag"), "spell"); await Click(Find<Button>("ApplyPartParameters"));
        await Inspector(1); await TypeText(Find<LineEdit>("TrialSourceTag"), "spell"); await SetNumber(Find<SpinBox>("TrialAmplitude"), "0");
        await Click(Find<Button>("SendTrial"));
        Check(Workbench.LastTrialResult?.Signal.Amplitude == 0 && Workbench.LastTrialResult.OutputNodeIds.Count == 0, "真实试运行保留零幅值并由筛选阻断");
        await SetNumber(Find<SpinBox>("TrialAmplitude"), "3"); await Click(Find<Button>("SendTrial"));
        Check(Workbench.LastTrialResult?.OutputNodeIds.Count == 1, "通过实际设置的来源与阈值后放行");
        await TypeText(Find<LineEdit>("TrialSourceTag"), "other"); await Click(Find<Button>("SendTrial"));
        Check(Workbench.LastTrialResult?.OutputNodeIds.Count == 0, "来源不符时同一原生试线入口实际拦下");
    }

    private async Task HoverAndSuppression()
    {
        Workbench.LoadExample(0); await Frames(3); await MoveOutsideBoard();
        await MoveCell(new Cell(0, 0)); await Wait(.28);
        var card = HoverCard();
        Check(card.Visible && card.MouseFilter == MouseFilterEnum.Ignore, "盘内悬停使用全局深墨只读卡且穿透鼠标");
        Check(card.FindChildren("*", "Control", true, false).OfType<Control>().All(c => c.MouseFilter == MouseFilterEnum.Ignore), "只读悬停的全部子控件不拦截绘图");
        RecordBounds(card); await Capture("07_部件悬停.png");
        await Press(CellPoint(new Cell(0, 0))); await Frames(2);
        Check(HoverInfoManager.IsDragInProgress() && !card.Visible, "开始拖部件立即隐藏悬停");
        await Release(CellPoint(new Cell(0, 0))); await MoveOutsideBoard(); await Wait(.25);
        Check(!card.Visible && !Workbench.Canvas.GuidesVisible, "离开真实来源后悬停与格位均收起");
        await Capture("08_常显格线无纸签.png");
    }

    private async Task GuardTrial()
    {
        var draft = Workbench.Canvas.Model.Serialize();
        await Click(Find<Button>("OpenGuardTrial")); await Frames(5);
        _guard = GetTree().Root.GetNodeOrNull<FormationGuardPanel>("FormationGuardPanel") ?? throw new Exception("守攻按钮没有打开原生页");
        VerifyGuardStyle(_guard);
        var actor = _guard.TrialOwner; var enemy = _guard.Enemy;
        Check(!actor.IsInsideTree() && !enemy.IsInsideTree(), "守攻使用隔离真实Character而非正式旅程人物");
        Check(_guard.FindChild("FormationDesk", true, false) is TextureRect { Texture: not null }, "守攻页与工作台复用同族案面贴图");
        var back = Named<Button>(_guard, "GuardReturn");
        Check(back.GetThemeStylebox("normal") is StyleBoxTexture && back.GetThemeFontSize("font_size") == 22, "守攻返回采用同族纸按钮与22字级");
        RecordBounds(back);
        var board = Named<FormationGuardBoardCanvas>(_guard, "GuardBoardCanvas");
        Check(!board.HasPersistentLabels, "守攻盘不绘制常驻名称或状态纸签");
        await SetNumber(Named<SpinBox>(_guard, "GuardHealthInput"), "20"); await Click(Named<Button>(_guard, "GuardSetHealth"));
        var low = _guard.Runtime.GetSnapshot(); var config = _guard.Runtime.Configuration;
        Check(low.Health == 20 && low.Mode == FormationTruth.True && low.GuardRunning && low.GuardRemaining == config.GuardCapacity,
            "实际设为20生命后进入守备并建立本器罩");
        Check(low.EnergySpent == config.GuardCost && actor.shield == low.Shield, "护罩已真实付款并与Character公共盾一致");
        await Click(Named<Button>(_guard, "GuardSpell"));
        Check(_guard.Runtime.GetSnapshot().Attacks == 0, "守态真实施法事件被开关阻断");
        await MoveTo(new Vector2(10, _height - 10)); await Capture("09_守攻低生命开罩.png");
        await SetNumber(Named<SpinBox>(_guard, "GuardHealthInput"), "80"); await Click(Named<Button>(_guard, "GuardSetHealth"));
        var before = _guard.Runtime.GetSnapshot(); await Click(Named<Button>(_guard, "GuardSpell"));
        var after = _guard.Runtime.GetSnapshot();
        Check(after.Mode == FormationTruth.False && !after.GuardRunning && after.EnemyHealth < before.EnemyHealth,
            "实际设为80生命后撤本器罩并以新施法伤敌");
        Check(after.SourceEnergy == before.SourceEnergy - config.AttackCost && enemy.current_health == after.EnemyHealth, "攻击实际扣阵能与敌方Character生命");
        await Click(Named<Button>(_guard, "GuardRepeat"));
        Check(_guard.LastResult?.Duplicate == true && _guard.Runtime.GetSnapshot().EnemyHealth == after.EnemyHealth, "原生重发不再次伤敌或扣费");
        await MoveTo(new Vector2(10, _height - 10)); await Capture("10_守攻高生命施法.png");
        foreach (var name in new[] { "GuardVisual", "GuardSidebar", "GuardHistory" })
            if (_guard.FindChild(name, true, false) is Control control && control.IsVisibleInTree()) RecordBounds(control);
        await Click(back); await Frames(5);
        Check(!GodotObject.IsInstanceValid(_guard) && !GodotObject.IsInstanceValid(actor) && !GodotObject.IsInstanceValid(enemy), "守攻返回释放页面及两名试验人物");
        Check(Workbench.IsVisibleInTree() && Workbench.Canvas.Model.Serialize() == draft, "守攻返回原工作台且基础草图保持");
        _guard = null; await Capture("11_返回阵盘工作台.png");
    }

    private async Task SelectPart(Kind kind)
    {
        await SelectDrawingTool("移动");
        var part = Workbench.Canvas.Model.Components.Values.First(p => p.Kind == kind);
        await ClickCell(part.Origin);
        Check(Workbench.Canvas.SelectedId == part.Id, "真实画布命中选中" + FormationBoardCanvas.PartName(kind));
    }
    private void VerifyGuardStyle(FormationGuardPanel panel)
    {
        var header = Named<PanelContainer>(panel, "GuardPaperHeader");
        var tools = Named<PanelContainer>(panel, "GuardPaperTools");
        Check(Math.Abs(header.Size.Y - 62) < 1 && Math.Abs(tools.Size.Y - 62) < 2, "守攻标题62高且工具条约62高，没有被竖排文字撑高");
        var back = Named<Button>(panel, "GuardReturn");
        Check(Math.Abs(back.Size.Y - 46) < 1 && back.GetThemeFontSize("font_size") == 22, "守攻返回保持46高22字级而不被父行拉伸");
        foreach (var name in new[] { "GuardPaperHeader", "GuardPaperTools", "GuardPaperStates", "GuardPaperSummary", "GuardPaperHistory", "GuardVisual", "GuardSidebar" })
            RecordBounds(Named<Control>(panel, name));
        foreach (var text in header.FindChildren("*", "Label", true, false).OfType<Label>())
            Check(text.Size.X >= text.GetThemeFont("font").GetStringSize(text.Text, fontSize: text.GetThemeFontSize("font_size")).X - 2,
                "守攻顶栏文字完整单行 " + text.Text);
    }
    private async Task Inspector(int index)
    {
        await Click(Find<Button>(index == 0 ? "FormationParametersTab" : "FormationSignalTrialTab"));
        Check(Find<TabContainer>("LogicInspector").CurrentTab == index, "原生纸页签实际切换检查栏" + index);
        CheckSelectedPaper(Find<Button>(index == 0 ? "FormationParametersTab" : "FormationSignalTrialTab"));
    }
    private void CheckSelectedPaper(Button button)
    {
        var style = button.GetThemeStylebox("normal") as StyleBoxTexture;
        Check(button.ButtonPressed && style?.Texture is not null && style.Texture.ResourceName.Contains("selectedTrue", StringComparison.OrdinalIgnoreCase),
            button.Name + "的选中使用整份纸纹染色而非中央色块");
        Check(button.GetThemeColor("font_color").IsEqualApprox(CraftingPaperStyle.Ink), button.Name + "选中保持深墨文字");
    }
    private async Task SetNumber(SpinBox spin, string value)
    {
        await EnsureVisible(spin); await TypeText(spin.GetLineEdit(), value);
        Check(Math.Abs(spin.Value - double.Parse(value, System.Globalization.CultureInfo.InvariantCulture)) < .01, spin.Name + "真实键入数值");
    }
    private async Task TypeText(LineEdit input, string value)
    {
        await Click(input); await KeyPress(Key.A, control: true);
        foreach (var character in value)
        {
            GetViewport().PushInput(new InputEventKey { Pressed = true, Unicode = character }, true);
            GetViewport().PushInput(new InputEventKey { Pressed = false, Unicode = character }, true);
        }
        await KeyPress(Key.Enter); await Frames(2);
    }
    private async Task SelectDrawingTool(string label)
    {
        var menu = Find<MenuButton>("FormationToolMenu"); var popup = menu.GetPopup();
        await Click(menu); await Frames(1);
        if (!popup.Visible) throw new Exception("菜单未打开：" + label);
        var index = Enumerable.Range(0, popup.ItemCount).First(i => popup.GetItemText(i).StartsWith(label, StringComparison.Ordinal));
        for (var y = 4f; y < popup.Size.Y - 4; y += Math.Max(3f, popup.Size.Y / (popup.ItemCount * 4f)))
        {
            var point = new Vector2(popup.Position.X + popup.Size.X * .55f, popup.Position.Y + y);
            await MoveTo(point);
            if (popup.GetFocusedItem() != index) continue;
            await Press(point); await Release(point); await Frames(2);
            if (popup.Visible) throw new Exception("原生鼠标未激活工具项：" + label);
            return;
        }
        throw new Exception("鼠标未命中菜单项：" + label);
    }
    private async Task KeyPress(Key key, bool control = false)
    {
        var target = GetViewport();
        target.PushInput(new InputEventKey { Keycode = key, Pressed = true, CtrlPressed = control }, true);
        await Frames(1); target.PushInput(new InputEventKey { Keycode = key, Pressed = false, CtrlPressed = control }, true); await Frames(1);
    }
    private async Task EnsureVisible(Control control)
    {
        for (var parent = control.GetParent(); parent is not null; parent = parent.GetParent())
            if (parent is ScrollContainer scroll) { scroll.EnsureControlVisible(control); await Frames(2); }
    }
    private async Task Click(Control control)
    {
        await EnsureVisible(control);
        Check(control.IsVisibleInTree(), "原生目标可见 " + control.Name);
        Check(GetViewport().GetVisibleRect().HasPoint(control.GetGlobalRect().GetCenter()), "原生命中点在视口内 " + control.Name);
        if (control is BaseButton button) Check(!button.Disabled, "原生按钮当前可操作 " + control.Name);
        await MoveTo(control.GetGlobalRect().GetCenter()); await Press(control.GetGlobalRect().GetCenter()); await Release(control.GetGlobalRect().GetCenter());
    }
    private Vector2 CellPoint(Cell cell) => Workbench.Canvas.GetGlobalTransformWithCanvas() * Workbench.Canvas.CellPosition(cell);
    private async Task ClickCell(Cell cell) { await MoveCell(cell); await Press(CellPoint(cell)); await Release(CellPoint(cell)); }
    private Task MoveCell(Cell cell, bool leftHeld = false) => MoveTo(CellPoint(cell), leftHeld);
    private Task MoveOutsideBoard() => MoveTo(Find<Label>("WorkbenchTitle").GetGlobalRect().GetCenter());
    private async Task MoveTo(Vector2 point, bool leftHeld = false)
    {
        var relative = point - _lastPointer; _lastPointer = point;
        GetViewport().PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point, Relative = relative, Velocity = relative * 60,
            ButtonMask = leftHeld ? MouseButtonMask.Left : 0 }, true);
        await Frames(2);
    }
    private async Task Press(Vector2 point)
    {
        GetViewport().PushInput(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = true, ButtonMask = MouseButtonMask.Left }, true);
        await Frames(2);
    }
    private async Task Release(Vector2 point)
    {
        GetViewport().PushInput(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = false }, true);
        await Frames(3);
    }
    private T Find<T>(string name) where T : Node => Named<T>(Workbench, name);
    private static T Named<T>(Node root, string name) where T : Node => root.FindChild(name, true, false) as T ?? throw new Exception("缺少原生控件 " + name);
    private PanelContainer HoverCard() => GetTree().Root.GetNode<HoverInfoManager>("HoverInfoManager").GetNode<PanelContainer>("SharedHoverInfoCard");
    private async Task Frames(int count) { for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), Godot.Timer.SignalName.Timeout);
    private void Check(bool passed, string message)
    {
        if (!passed) throw new Exception(message);
        _checks.Add(message); GD.Print("FORMATION_PAPER_UI_CHECK " + message);
    }
    private void RecordBounds(Control control)
    {
        var rect = control.GetGlobalRect();
        Check(GetViewport().GetVisibleRect().Encloses(rect.Grow(-1)), "界面边界完整 " + control.Name);
        _bounds.Add(new { name = control.Name.ToString(), type = control.GetClass(), x = rect.Position.X, y = rect.Position.Y, width = rect.Size.X, height = rect.Size.Y });
    }
    private async Task Capture(string filename)
    {
        await Frames(3); await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(Evidence));
        using var image = GetViewport().GetTexture().GetImage();
        Check(image is not null && !image.IsEmpty() && image.GetWidth() == _width && image.GetHeight() == _height, "截图真实尺寸 " + filename);
        Check(image!.SavePng(Evidence + "/" + filename) == Error.Ok, "保存原生画面 " + filename);
        _captures.Add(new { file = filename, width = image.GetWidth(), height = image.GetHeight(), view = "actual Godot root Viewport; no repaint" });
    }
    private void WriteReport(bool passed, string? error)
    {
        var directory = ProjectSettings.GlobalizePath(Evidence); System.IO.Directory.CreateDirectory(directory);
        var report = new
        {
            date = "2026-11-09", passed, run_mode = PreviewOnly ? "preview_only" : "full_interaction", stage = _stage, error, resolution = new[] { _width, _height },
            input = "Viewport.PushInput mouse hit testing and keyboard text entry; public examples used only as fixture preparation",
            isolated_profile = true, appdata = Environment.GetEnvironmentVariable("APPDATA"), user_data_dir = OS.GetUserDataDir(),
            character_save_opened = false, real_journey_loaded = false, artifacts_or_knowledge_granted = false,
            draft_path = GodotObject.IsInstanceValid(_workspace) ? Workbench.DraftPath : "user://formation_paper_ui/" + _width + "/isolated_draft.json",
            scope = PreviewOnly ? "First native workspace and guard screenshots only; full interaction is not yet checked."
                : "This run checks new native paper workspace and its actual operations. Pure-core historical assertion counts are not reused.",
            visual_review = "pending screenshot inspection by agent", checks = _checks, bounds = _bounds, screenshots = _captures
        };
        System.IO.File.WriteAllText(System.IO.Path.Combine(directory, passed ? "原生验收.json" : "原生验收失败.json"),
            System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
            { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
    }
}
