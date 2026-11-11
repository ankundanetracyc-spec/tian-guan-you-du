using Godot;
using System.Text.Json;

/// <summary>Native standalone mailbox checks; only ephemeral letters and isolated APPDATA.</summary>
public partial class LetterPaperUiSmoke : Node
{
    private const string Output = "res://docs/美术规范/信件邀约纸墨木石样稿_20261109/";
    private readonly List<object> _checks = [];
    private readonly List<string> _captures = [];
    private LetterPaperPreviewScene _preview = null!;
    private int _width;
    private void Check(bool pass, string name)
    {
        _checks.Add(new { name, passed = pass });
        if (!pass) throw new InvalidOperationException(name);
        GD.Print("LETTER_PAPER_OK " + name);
    }
    private async Task Frames(int count = 3)
    { for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private T Find<T>(string name) where T : Node => (T)_preview.FindChild(name, true, false);
    private async Task Click(string name)
    {
        var button = Find<BaseButton>(name);
        for (var parent = button.GetParent(); parent is not null; parent = parent.GetParent())
            if (parent is ScrollContainer scroll) { scroll.EnsureControlVisible(button); await Frames(); }
        Check(button.IsVisibleInTree() && !button.Disabled, "实际可操作 " + name);
        var position = button.GetGlobalTransformWithCanvas() * (button.Size / 2);
        GetViewport().PushInput(new InputEventMouseMotion { Position = position, GlobalPosition = position }, true);
        await Frames(1);
        GetViewport().PushInput(new InputEventMouseButton { Position = position, GlobalPosition = position, ButtonIndex = MouseButton.Left, Pressed = true }, true);
        await Frames(1);
        GetViewport().PushInput(new InputEventMouseButton { Position = position, GlobalPosition = position, ButtonIndex = MouseButton.Left, Pressed = false }, true);
        await Frames(4);
    }
    private async Task Capture(string name)
    {
        GetViewport().PushInput(new InputEventMouseMotion { Position = new(4, 4), GlobalPosition = new(4, 4) }, true);
        await ToSignal(GetTree().CreateTimer(.20), SceneTreeTimer.SignalName.Timeout); await Frames();
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage();
        Check(image.GetWidth() == _width && image.GetHeight() == _width * 9 / 16, "原生完整图尺寸 " + name);
        string file = $"信件_{name}_{_width}.png";
        Check(image.SavePng(Output + file) == Error.Ok, "原生完整图保存 " + name); _captures.Add(file);
    }
    private async Task FillQuery(string value)
    {
        var field = Find<LineEdit>("LetterQuery"); field.GrabFocus(); field.SelectAll();
        GetViewport().PushInput(new InputEventKey { Keycode = Key.Backspace, Pressed = true }, true);
        GetViewport().PushInput(new InputEventKey { Keycode = Key.Backspace, Pressed = false }, true);
        foreach (char letter in value)
        {
            GetViewport().PushInput(new InputEventKey { Unicode = letter, Pressed = true }, true);
            GetViewport().PushInput(new InputEventKey { Unicode = letter, Pressed = false }, true);
        }
        await Frames();
    }
    public override async void _Ready()
    {
        bool passed = false; string failure = "";
        try
        {
            _width = OS.GetCmdlineUserArgs().Contains("--wide") ? 1920 : 1280;
            string project = System.IO.Path.GetFullPath(ProjectSettings.GlobalizePath("res://"));
            string userdata = System.IO.Path.GetFullPath(ProjectSettings.GlobalizePath("user://"));
            Check(userdata.StartsWith(System.IO.Path.Combine(project, "output") + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), "APPDATA隔离在项目output内");
            Check(GetViewport() is SubViewport, "使用固定GPU子视口而非桌面截图");
            System.IO.Directory.CreateDirectory(ProjectSettings.GlobalizePath(Output));
            var clock = GetNode<GameTimeMgr>("/root/GameTimeManager"); clock.pause_time(); int before = clock.get_total_game_hours();
            _preview = new LetterPaperPreviewScene { ExitOnClose = false }; AddChild(_preview); await Frames(5);
            var mailbox = _preview.PreviewMailbox;
            Check(mailbox.letters.Count == 4 && mailbox.UnreadCount == 4, "仅创建四封独立示例信");
            Check(_preview.SelectedLetterId == "" && Find<Control>("LetterEmpty").Visible, "初开不自动拆信或标已读");
            var date = Find<Label>("LetterDate"); var back = Find<Button>("LetterReturn");
            Check(date.Position.Y == back.Position.Y && date.Size.Y == back.Size.Y && date.Size.Y == 46 && date.GetThemeFontSize("font_size") == back.GetThemeFontSize("font_size"), "日期返回同一行22字46高");
            Check(!date.Text.Contains("时") && date.Text.EndsWith("日"), "日期只显示年月日");
            await Capture("信匣初开");
            await Click("LetterRow_preview_banquet");
            var selected = mailbox.Find(_preview.SelectedLetterId)!;
            Check(selected.is_read && mailbox.UnreadCount == 3, "实际点信才标已读");
            Check(Find<RichTextLabel>("LetterBody").Text == selected.body, "全文取原信件快照");
            Check(Find<Button>("LetterAccept").IsVisibleInTree() && Find<Button>("LetterAttend").Disabled, "待答邀约可应答且未注册活动不开放赴约");
            foreach (var caption in new[] { "LetterAccept", "LetterDecline", "LetterAttend", "LetterReadState", "LetterSort", "LetterReturn" })
            {
                var button = Find<Button>(caption);
                Check(button.GetThemeStylebox("normal") is StyleBoxTexture && button.GetMeta("paper_feedback_kind").AsInt32() == (int)CraftingPaperStyle.ButtonKind.Regular, "普通操作使用同族整纸材质 " + caption);
            }
            await Capture("邀约展信");
            await Click("LetterAccept");
            Check(selected.invitation?.response == "accepted" && !Find<Button>("LetterAccept").IsVisibleInTree(), "应邀沿原Reply且答复后收起重复操作");
            await Capture("已应邀");
            await Click("LetterTab_invites"); Check(_preview.VisibleLetters.Count == 2, "邀约分类按原真实字段筛选");
            await Click("LetterRow_preview_expedition");
            Check(!Find<Button>("LetterAccept").IsVisibleInTree() && !Find<Button>("LetterDecline").IsVisibleInTree() && !Find<Button>("LetterAttend").IsVisibleInTree(), "过期邀约没有应答或赴约操作");
            await Capture("过期邀约");
            await Click("LetterTab_all"); await Click("LetterRow_preview_greeting");
            Check(!Find<Control>("LetterInvitation").Visible, "普通信收起邀约笺并扩大正文");
            await Capture("普通来信");
            await FillQuery("陈砚");
            Check(_preview.VisibleLetters.Count == 2 && _preview.VisibleLetters.All(letter => letter.sender_name == "陈砚"), "检索署名保持原快照语义");
            await Capture("检索");
            await FillQuery(""); await Click("LetterSort");
            Check(_preview.VisibleLetters.First().source_event_id == "preview_expedition", "实际排序点击改为旧信在前");
            await Capture("检索排序");
            await Click("LetterTab_unread");
            Check(_preview.VisibleLetters.All(letter => !letter.is_read), "未读页只显示未读");
            var other = LetterPaperPreviewScene.CreateSampleMailbox(); _preview.Bind(other); await Frames();
            await Click("LetterRow_preview_banquet"); await Click("LetterDecline");
            Check(other.Find(_preview.SelectedLetterId)?.invitation?.response == "declined", "婉拒独立调用原Reply");
            var longMailbox = LetterPaperPreviewScene.CreateSampleMailbox();
            foreach (int i in Enumerable.Range(1, 10))
                longMailbox.Deliver(new LetterData { source_event_id = "scroll_" + i, sender_name = "顾远", subject = "静室门前留书", sent_at_hour = i, body = string.Join("\n\n", Enumerable.Repeat(longMailbox.letters[2].body, 6)) });
            _preview.Bind(longMailbox); await Frames(); await Click("LetterRow_scroll_10");
            Check(Find<ScrollContainer>("LetterListScroll").ScrollVertical > 0, "长信匣独立滚动可选择下方来信");
            var bodyScroll = Find<ScrollContainer>("LetterBodyScroll"); bodyScroll.ScrollVertical = (int)bodyScroll.GetVScrollBar().MaxValue; await Frames();
            Check(bodyScroll.ScrollVertical > 0 && Find<Button>("LetterReadState").Position.Y == 738, "长正文滚动而底部操作固定");
            await Capture("长信末尾");
            _preview.Bind(new LetterMailbox()); await Frames();
            Check(_preview.PreviewMailbox.letters.Count == 0 && Find<Control>("LetterEmpty").Visible, "空信匣不补入示例");
            await Capture("空信匣");
            Check(clock.get_total_game_hours() == before, "检索阅读及应答不推进游戏时间");
            await Click("LetterReturn"); Check(!_preview.Visible, "返回只收起独立样稿");
            passed = true;
        }
        catch (Exception error) { failure = error.ToString(); GD.PushError(failure); }
        finally
        {
            var report = new { status = passed ? "passed" : "failed", width = _width, checks = _checks, captures = _captures, error = failure, formalLetterPanelChanged = false, formalMailboxReadOrWritten = false };
            System.IO.Directory.CreateDirectory(ProjectSettings.GlobalizePath(Output));
            System.IO.File.WriteAllText(ProjectSettings.GlobalizePath(Output + $"验收记录_{_width}.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            GD.Print($"LETTER_PAPER_{(passed ? "PASS" : "FAIL")} width={_width} checks={_checks.Count} captures={_captures.Count}");
            GetTree().Quit(passed ? 0 : 1);
        }
    }
}
