using Godot;
using System.Text.Json;

/// <summary>Native preview-only layout and input checks. Never opens a real world or executes a world action.</summary>
public partial class NpcInteractionPaperUiSmoke : Node
{
    private const string Output = "res://docs/美术规范/NPC交互纸墨木石样稿_20261109/";
    private readonly List<object> _checks = [];
    private readonly List<string> _captures = [];
    private NpcInteractionPaperPreviewScene _preview = null!;
    private int _width;
    private void Check(bool passed, string name)
    {
        _checks.Add(new { name, passed });
        if (!passed) throw new InvalidOperationException(name);
        GD.Print("NPC_PAPER_OK " + name);
    }
    private async Task Frames(int count = 3)
    { for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private T Find<T>(string name) where T : Node => (T)_preview.FindChild(name, true, false);
    private async Task Mouse(Button button, bool down)
    {
        var position = button.GetGlobalTransformWithCanvas() * (button.Size / 2);
        GetViewport().PushInput(new InputEventMouseMotion { Position = position, GlobalPosition = position }, true);
        GetViewport().PushInput(new InputEventMouseButton { Position = position, GlobalPosition = position, ButtonIndex = MouseButton.Left, Pressed = down }, true);
        await Frames(2);
    }
    private async Task Click(string name)
    {
        var button = Find<Button>(name);
        Check(button.IsVisibleInTree() && !button.Disabled, "实际可操作 " + name);
        await Mouse(button, true); await Mouse(button, false); await Frames(2);
    }
    private async Task Capture(string state, bool keepPointer = false)
    {
        if (!keepPointer) GetViewport().PushInput(new InputEventMouseMotion { Position = new(4, 4), GlobalPosition = new(4, 4) }, true);
        await ToSignal(GetTree().CreateTimer(.24), SceneTreeTimer.SignalName.Timeout); await Frames();
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var picture = GetViewport().GetTexture().GetImage();
        Check(picture.GetWidth() == _width && picture.GetHeight() == _width * 9 / 16, "完整原生图尺寸 " + state);
        string file = $"人物交互_{state}_{_width}.png";
        Check(picture.SavePng(Output + file) == Error.Ok, "原生图保存 " + state); _captures.Add(file);
    }
    public override async void _Ready()
    {
        bool passed = false; string failure = "";
        try
        {
            _width = OS.GetCmdlineUserArgs().Contains("--wide") ? 1920 : 1280;
            string project = System.IO.Path.GetFullPath(ProjectSettings.GlobalizePath("res://"));
            string user = System.IO.Path.GetFullPath(ProjectSettings.GlobalizePath("user://"));
            Check(user.StartsWith(System.IO.Path.Combine(project, "output") + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), "用户目录隔离在项目output内");
            Check(GetViewport() is SubViewport, "使用固定GPU子视口");
            var saves = GetNode<GameSaveManager>("/root/GameSaveManager");
            Check(!saves.has_active_slot, "未打开正式旅程或存档槽");
            var clock = GetNode<GameTimeMgr>("/root/GameTimeManager"); int before = clock.get_total_game_hours();
            _preview = GD.Load<PackedScene>("res://tscn/scene/NpcInteractionPaperPreview.tscn").Instantiate<NpcInteractionPaperPreviewScene>();
            _preview.ExitOnClose = false; AddChild(_preview); await Frames(14);
            Check(_preview.People.Count == 3 && _preview.Card.get_displayed_npc_id() == _preview.People[0].npc_id, "初开选择独立样例并复用原人物页");
            var bounds = _preview.Card.get_interaction_rect();
            var roster = Find<Control>("NpcRoster").GetGlobalRect();
            Check(bounds.Position.X > roster.End.X && bounds.Size.X < _width * .6f, "局部页位于名单右侧且保留场景视野");
            Check(_preview.Card.MouseFilter == Control.MouseFilterEnum.Ignore, "人物页根节点不遮挡场景点击");
            Check(Find<PanelContainer>("NpcInteractionPage").GetThemeStylebox("panel") is StyleBoxTexture, "人物纸面使用批准素材");
            Check(Find<Label>("NpcName").Text == "陆清和" && Find<Label>("NpcBasicInformation").Text.Contains("内门弟子"), "公开名头读取原展示口径");
            Check(Find<Label>("NpcIntroduction").Text.Length == 0, "不补模板出身或多余文案");
            Check(!Find<Label>("NpcBasicInformation").Text.Contains("体魄") && !Find<Label>("NpcBasicInformation").Text.Contains("好感"), "普通小页不泄露隐藏属性和关系");
            var back = Find<Button>("NpcReturn"); var date = Find<Label>("NpcDate");
            Check(back.Size.Y == 46 && date.Size.Y == 46 && back.GlobalPosition.Y == date.GlobalPosition.Y && back.GetThemeFontSize("font_size") == date.GetThemeFontSize("font_size"), "日期与返回22字46高同一行");
            Check(!date.Text.Contains(":") && !date.Text.Contains("时") && !date.Text.Contains("昼"), "日期不显示钟点或时辰");
            var actions = Find<GridContainer>("InteractionActions");
            Check(actions.Columns == 2 && actions.GetChildren().OfType<Button>().All(button => button.GetThemeStylebox("normal") is StyleBoxTexture && button.GetThemeStylebox("pressed") is StyleBoxTexture && button.GetThemeFontSize("font_size") == 22), "双列行动统一素材字号与按下材质");
            Check(Find<Button>("NpcAction_attack").Disabled && Find<Label>("InteractionRequirements").Text.Contains("此地禁止私斗"), "白石城攻击不可用且限制常显");
            await Capture("同门人物页");
            var scenePosition = _preview.DesignCanvas.GetGlobalTransformWithCanvas() * new Vector2(1510, 560);
            GetViewport().PushInput(new InputEventMouseButton { Position = scenePosition, GlobalPosition = scenePosition, ButtonIndex = MouseButton.Left, Pressed = true }, true);
            GetViewport().PushInput(new InputEventMouseButton { Position = scenePosition, GlobalPosition = scenePosition, ButtonIndex = MouseButton.Left, Pressed = false }, true); await Frames();
            Check(_preview.SceneClickCount > 0, "纸页外场景实际接收鼠标");
            await Click("NpcRoster_1");
            Check(Find<Label>("NpcName").Text == "陆衡" && _preview.Card.get_displayed_npc_id() == _preview.People[1].npc_id, "名单点击切换真实立绘对象与公开资料");
            await Capture("行脚修士页");
            var stale = Find<Button>("NpcAction_talk"); _preview.SelectNpc(2);
            stale.EmitSignal(BaseButton.SignalName.Pressed);
            Check(_preview.Card.get_displayed_npc_id() == _preview.People[2].npc_id && _preview.LastRequestedAction.Length == 0, "换人后旧行动回调不能操作新目标");
            await Frames(5);
            Check(!Find<Label>("NpcBasicInformation").Text.Contains("身份："), "没有公开名头时省去身份行");
            await Capture("无公开名头");
            var talk = Find<Button>("NpcAction_talk"); await Mouse(talk, true);
            Check(talk.GetThemeStylebox("pressed") is StyleBoxTexture held && held.ExpandMarginTop == -1 && held.ExpandMarginBottom == 1, "实际按住反馈作用于整纸并下沉1单位");
            await Capture("按钮按下", true); await Mouse(talk, false);
            Check(!_preview.Card.Visible && _preview.LastRequestedAction == "talk" && _preview.LastRequestedNpc == _preview.People[2].npc_id, "行动只记预览选择并收起小页");
            await Capture("收起小页");
            await Click("NpcRoster_0"); _preview.ExtendedActions = true; _preview.RefreshPreview(); await Frames(6);
            var scroll = Find<ScrollContainer>("InteractionScroll"); scroll.ScrollVertical = (int)scroll.GetVScrollBar().MaxValue; await Frames();
            Check(scroll.ScrollVertical > 0 && Find<Label>("NpcName").IsVisibleInTree() && Find<Label>("InteractionRequirements").IsVisibleInTree(), "长行动区独立滚动且姓名限制常驻");
            _preview.ExtendedActions = false; _preview.People[0].current_state = "闭关修行"; _preview.RefreshPreview(); await Frames(5);
            Check(!Find<Button>("NpcAction_leave_acquaintance_note").Disabled && Find<Button>("NpcAction_npc_unavailable").Disabled, "闭关夹具保留拜帖与不可用提示");
            await Capture("闭关限制");
            Check(clock.get_total_game_hours() == before && !saves.has_active_slot, "浏览换人及演示点击不推进时间或打开存档");
            await Click("NpcReturn"); Check(!_preview.Visible, "返回只退出独立样稿"); passed = true;
        }
        catch (Exception error) { failure = error.ToString(); GD.PushError(failure); }
        finally
        {
            System.IO.Directory.CreateDirectory(ProjectSettings.GlobalizePath(Output));
            var report = new { status = passed ? "passed" : "failed", width = _width, checks = _checks, captures = _captures, error = failure, scope = "独立NPC菜单；原规则与正式入口保持；百科跳过" };
            System.IO.File.WriteAllText(ProjectSettings.GlobalizePath(Output + $"验收记录_{_width}.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); await Frames(2);
            GD.Print($"NPC_PAPER_{(passed ? "PASS" : "FAIL")} width={_width} checks={_checks.Count} captures={_captures.Count}"); GetTree().Quit(passed ? 0 : 1);
        }
    }
}
