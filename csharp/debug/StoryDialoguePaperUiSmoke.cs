using Godot;
using System.Text.Json;

/// <summary>Native dialogue presentation checks without a world, live probe state, cost or save.</summary>
public partial class StoryDialoguePaperUiSmoke : Node
{
    private const string Output = "res://docs/美术规范/剧情对白纸墨木石样稿_20261109/";
    private readonly List<object> _checks = [];
    private readonly List<string> _captures = [];
    private StoryDialoguePaperPreviewScene _preview = null!;
    private int _width;
    private void Check(bool passed, string name)
    {
        _checks.Add(new { name, passed });
        if (!passed) throw new InvalidOperationException(name);
        GD.Print("DIALOGUE_PAPER_OK " + name);
    }
    private async Task Frames(int count = 3)
    { for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private T Find<T>(string name) where T : Node => (T)_preview.FindChild(name, true, false);
    private async Task Mouse(Control control, bool pressed)
    {
        var position = control.GetGlobalTransformWithCanvas() * (control.Size / 2);
        GetViewport().PushInput(new InputEventMouseMotion { Position = position, GlobalPosition = position }, true);
        GetViewport().PushInput(new InputEventMouseButton { Position = position, GlobalPosition = position, ButtonIndex = MouseButton.Left, Pressed = pressed }, true);
        await Frames(2);
    }
    private async Task Click(Control control) { await Mouse(control, true); await Mouse(control, false); await Frames(2); }
    private async Task Capture(string state, bool keepPointer = false)
    {
        if (!keepPointer) GetViewport().PushInput(new InputEventMouseMotion { Position = new(4, 4), GlobalPosition = new(4, 4) }, true);
        await Frames(3); await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var picture = GetViewport().GetTexture().GetImage();
        Check(picture.GetWidth() == _width && picture.GetHeight() == _width * 9 / 16, "原生完整图尺寸 " + state);
        string file = $"剧情对白_{state}_{_width}.png";
        Check(picture.SavePng(Output + file) == Error.Ok, "原生完整图保存 " + state); _captures.Add(file);
    }
    private void CheckChoicePlacement()
    {
        var dialogue = _preview.Dialogue.get_dialogue_rect(); var responses = _preview.Dialogue.get_response_rect();
        Check(Math.Abs(responses.End.Y - dialogue.Position.Y) < 1.1f && responses.Position.X > _width * .5f,
            "回应在右侧竖排且底边贴合实际对白上沿");
        Check(Find<ScrollContainer>("DialogueResponses").ZIndex == 260, "独立回应保持原立绘前方层级");
    }
    private void CheckPortraitEdge(string state)
    {
        var portrait = Find<Control>("SpeakerPortrait");
        Check(portrait.IsVisibleInTree() && portrait.ClipContents &&
            Math.Abs(portrait.GetGlobalRect().End.Y - _preview.Dialogue.get_dialogue_rect().Position.Y) < .1f,
            "立绘可见下沿严格贴合对白上沿 " + state);
    }
    public override async void _Ready()
    {
        bool passed = false; string failure = "";
        try
        {
            _width = OS.GetCmdlineUserArgs().Contains("--wide") ? 1920 : 1280;
            string project = System.IO.Path.GetFullPath(ProjectSettings.GlobalizePath("res://"));
            string user = System.IO.Path.GetFullPath(ProjectSettings.GlobalizePath("user://"));
            Check(user.StartsWith(System.IO.Path.Combine(project, "output") + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), "用户数据目录隔离在output内");
            Check(GetViewport() is SubViewport, "完整图使用固定GPU子视口");
            var clock = GetNode<GameTimeMgr>("/root/GameTimeManager"); int hour = clock.get_total_game_hours();
            var saves = GetNode<GameSaveManager>("/root/GameSaveManager"); Check(!saves.has_active_slot, "未读取正式旅程存档");
            _preview = GD.Load<PackedScene>("res://tscn/scene/StoryDialoguePaperPreview.tscn").Instantiate<StoryDialoguePaperPreviewScene>();
            _preview.ExitOnClose = false; AddChild(_preview); await Frames(12);
            var panel = _preview.Dialogue; var bounds = panel.get_dialogue_rect();
            Check(Math.Abs(bounds.GetCenter().X - _width / 2f) < 1 && bounds.Size.X <= _width * .77f && bounds.End.Y < _width * 9 / 16 - 15,
                "对白保持水平居中有限宽度及底部留白");
            Check(panel.PaperFrame.GetThemeStylebox("panel") is StyleBoxTexture, "对白承托使用批准纸面素材");
            Check(panel.SpeakerLabel.Text == "陆衡" && panel.BodyLabel.Text == StoryDialoguePaperPreviewScene.NpcSpeech,
                "NPC发言保留原真实姓名与台词口径");
            CheckChoicePlacement();
            CheckPortraitEdge("NPC回应");
            var surface = (SubViewport)GetViewport(); var originalSize = surface.Size;
            surface.Size = new Vector2I(_width - 96, (_width - 96) * 9 / 16); await Frames(6);
            CheckPortraitEdge("窗口变化");
            surface.Size = originalSize; await Frames(6);
            var buttons = Find<VBoxContainer>("DialogueResponseList").GetChildren().OfType<Button>().ToArray();
            Check(buttons.Length == 4 && buttons.Select(button => button.Text).SequenceEqual(StoryDialoguePaperPreviewScene.ReplyLabels), "保留原四项回应与次序");
            Check(buttons.All(button => button.GetThemeStylebox("normal") is StyleBoxTexture && button.GetThemeStylebox("pressed") is StyleBoxTexture && button.GetThemeFontSize("font_size") == 22),
                "回应使用同族素材与统一22字号");
            Check(buttons.All(button => button.GetChildren().OfType<ColorRect>().All(backing => !backing.Visible)), "清除每个选项文字后的独立色块");
            var date = Find<Label>("DialogueDate"); var back = Find<Button>("DialogueReturn");
            Check(date.Size.Y == 46 && back.Size.Y == 46 && date.GlobalPosition.Y == back.GlobalPosition.Y && date.GetThemeFontSize("font_size") == back.GetThemeFontSize("font_size"), "日期与返回同一行同22字46高");
            Check(!date.Text.Contains(":") && !date.Text.Contains("时") && !date.Text.Contains("昼"), "日期不显示钟点或时辰");
            await Capture("独立回应");
            var apologize = Find<Button>("PaperResponse_0"); await Mouse(apologize, true);
            Check(apologize.GetThemeStylebox("pressed") is StyleBoxTexture pressed && pressed.ExpandMarginTop == -1 && pressed.ExpandMarginBottom == 1, "实际按住时整纸下沉且反馈不另铺色块");
            await Capture("按钮按下", true); await Mouse(apologize, false); await Frames(4);
            Check(_preview.LastChoice == 0 && panel.SpeakerLabel.Text == "沈照微" && panel.BodyLabel.Text == "是我唐突，抱歉。", "回应实际点击只切换玩家原台词");
            CheckPortraitEdge("切换玩家");
            Check(panel.get_response_rect().Size == Vector2.Zero, "回应后旧选项收起");
            await Capture("玩家发言"); int beforeContinue = _preview.ContinueCount;
            await Click(panel.BodyLabel); Check(!panel.Visible && _preview.ContinueCount == beforeContinue + 1, "点击正文仅执行一次原继续回调");
            _preview.ShowCase(0); await Frames(5); CheckPortraitEdge("切回NPC"); await Capture("普通对白");
            Check(panel.SpeakerLabel.Text == "陆衡" && panel.get_response_rect().Size == Vector2.Zero, "普通人物对白不添加回应或多余按钮");
            _preview.ShowCase(2); await Frames(5);
            Check(panel.SpeakerLabel.Text == "旁白" && panel.BodyLabel.Text == StoryDialoguePaperPreviewScene.Narration, "旁白不伪装为人物或事项名称");
            await Capture("旁白");
            _preview.ShowCase(3); await Frames(8); panel.BodyScroll.ScrollVertical = (int)panel.BodyScroll.GetVScrollBar().MaxValue; await Frames();
            Check(panel.BodyScroll.ScrollVertical > 0 && panel.get_dialogue_rect().Size.Y <= 311 && panel.SpeakerLabel.IsVisibleInTree(), "长文内部滚动且不扩大为通栏或遮住姓名");
            await Capture("长文滚动");
            _preview.ShowCase(4); await Frames(8); var scroll = Find<ScrollContainer>("DialogueResponses");
            scroll.ScrollVertical = (int)scroll.GetVScrollBar().MaxValue; await Frames();
            Check(scroll.ScrollVertical > 0 && panel.BodyLabel.Text == StoryDialoguePaperPreviewScene.NpcSpeech, "长回应列表独立滚动而正文保持");
            CheckChoicePlacement(); await Capture("长选项滚动");
            var old = Find<Button>("PaperResponse_0"); _preview.ShowCase(1); old.EmitSignal(BaseButton.SignalName.Pressed);
            Check(_preview.LastChoice == -1 && panel.SpeakerLabel.Text == "陆衡", "旧代次回应回调不能污染新对白");
            await Frames(); int cancel = _preview.CancelCount; panel.close(); panel.close();
            Check(!panel.Visible && _preview.CancelCount == cancel + 1, "取消只回调一次且关闭独立选项");
            Check(clock.get_total_game_hours() == hour && !saves.has_active_slot, "浏览回应不推进时间或打开存档");
            _preview.ShowCase(0); await Frames(); await Click(back);
            Check(!_preview.Visible, "右上返回实际退出独立样稿"); passed = true;
        }
        catch (Exception error) { failure = error.ToString(); GD.PushError(failure); }
        finally
        {
            System.IO.Directory.CreateDirectory(ProjectSettings.GlobalizePath(Output));
            var report = new { status = passed ? "passed" : "failed", width = _width, checks = _checks, captures = _captures, error = failure, scope = "独立剧情对白与回应；原剧情入口、探查和世界行为未接入" };
            System.IO.File.WriteAllText(ProjectSettings.GlobalizePath(Output + $"验收记录_{_width}.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); await Frames(2);
            GD.Print($"DIALOGUE_PAPER_{(passed ? "PASS" : "FAIL")} width={_width} checks={_checks.Count} captures={_captures.Count}"); GetTree().Quit(passed ? 0 : 1);
        }
    }
}
