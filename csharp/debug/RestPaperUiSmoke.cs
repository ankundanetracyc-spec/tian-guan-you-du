using Godot;
using System.Reflection;
using System.Text.Json;

/// <summary>隔离内存人物的休息界面验收；原休息结算继续由调用方负责。</summary>
public partial class RestPaperUiSmoke : Node
{
    private const string Folder = "res://docs/美术规范/休息纸墨木石正式接入_20261108/";
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<object> _checks = [];
    private readonly List<string> _captures = [];
    private RestPanel _panel = null!;
    private Character _actor = null!;
    private GameTimeMgr _clock = null!;
    private int _width = 1280;
    private bool _headless;
    private string _failure = "";
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, Hidden)!.GetValue(owner)!;
    private int Hours() => (int)typeof(RestPanel).GetMethod("Hours", Hidden)!.Invoke(_panel, null)!;
    private void Check(bool passed, string why)
    {
        _checks.Add(new { name = why, passed });
        if (!passed) throw new InvalidOperationException(why);
        GD.Print("REST_PAPER_UI_OK " + why);
    }
    private static IEnumerable<T> Nodes<T>(Node parent) where T : Node
    {
        foreach (var child in parent.GetChildren())
        {
            if (child is T match) yield return match;
            foreach (var descendant in Nodes<T>(child)) yield return descendant;
        }
    }
    private async Task Frames(int count = 3)
    { for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private async Task Click(Button button)
    {
        await Frames();
        Check(button.IsVisibleInTree() && !button.Disabled && button.MouseFilter != Control.MouseFilterEnum.Ignore
            && button.Size.X > 1 && button.Size.Y > 1, "目标控件可命中 " + button.Name);
        if (_headless) button.EmitSignal(BaseButton.SignalName.Pressed);
        else
        {
            var point = button.GetGlobalTransformWithCanvas() * (button.Size * .5f);
            GetViewport().PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point }, true);
            GetViewport().PushInput(new InputEventMouseButton
                { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = true }, true);
            GetViewport().PushInput(new InputEventMouseButton
                { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = false }, true);
        }
        await Frames();
    }
    private void Press(string field) => Field<Button>(_panel, field).EmitSignal(BaseButton.SignalName.Pressed);
    private void Open(Action<int>? confirm = null, int? fixedHours = null, string note = "")
        => _panel.Open(_actor, _clock, "观复峰居所", confirm ?? (_ => { }), fixedHours, note);
    private (float Stamina, float Health, int Time) Stamp()
        => (_actor.current_world_stamina, _actor.current_health, _clock.get_total_game_hours());
    private Rect2 Bounds(Control control)
    {
        var transform = control.GetGlobalTransformWithCanvas();
        var result = new Rect2(transform * Vector2.Zero, Vector2.Zero);
        foreach (var point in new[] { new Vector2(control.Size.X, 0), control.Size, new Vector2(0, control.Size.Y) })
            result = result.Expand(transform * point);
        return result;
    }
    private async Task Capture(string name)
    {
        await Frames();
        if (_headless) return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage();
        string file = $"休息_{name}_{_width}.png";
        Check(image.GetWidth() == _width, "真实截图尺寸 " + name);
        Check(image.SavePng(Folder + file) == Error.Ok, "保存原生截图 " + name);
        _captures.Add(file);
    }
    public override async void _Ready()
    {
        try
        {
            Check(OS.GetEnvironment("APPDATA").Contains("rest_paper", StringComparison.OrdinalIgnoreCase), "使用独立APPDATA");
            _width = OS.GetCmdlineUserArgs().Contains("--wide") ? 1920 : 1280;
            int height = _width * 9 / 16;
            _headless = DisplayServer.GetName() == "headless";
            GetWindow().Unfocusable = true;
            GetWindow().Borderless = true;
            GetWindow().Mode = Window.ModeEnum.Windowed;
            GetWindow().ContentScaleSize = new Vector2I(_width, height);
            GetWindow().Size = new Vector2I(_width, height);
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(Folder));
            await Frames();
            _clock = GetNode<GameTimeMgr>("/root/GameTimeManager");
            _clock.year = 1; _clock.month = 3; _clock.day = 15; _clock.hour = 10;
            _actor = new Character { entity_data = new HumanData { entity_name = "休息隔离验收", current_world_stamina = 40, max_world_stamina = 100 } };
            AddChild(_actor); _actor.Hide(); _actor.current_health = _actor.max_health / 2;
            _panel = new RestPanel(); AddChild(_panel);
            int confirmed = 0, calls = 0;
            Open(count => { confirmed = count; calls++; });
            var baseline = Stamp();
            Check(Hours() == 8 && !Field<Button>(_panel, "_start").Disabled, "手动默认8小时");
            Check(Field<Label>(_panel, "_time").Text == "1年3月15日", "日期仅年月日");
            var headerDate = Field<Label>(_panel, "_time");
            var headerReturn = _panel.GetNode<Button>("MaterialCanvas/RestReturn");
            Check(Mathf.IsEqualApprox(headerDate.Position.Y, headerReturn.Position.Y) && Mathf.IsEqualApprox(headerDate.Size.Y, headerReturn.Size.Y), "右上日期与返回同一行同高");
            Check(headerDate.GetThemeFontSize("font_size") == 22 && headerDate.GetThemeFontSize("font_size") == headerReturn.GetThemeFontSize("font_size")
                && headerDate.VerticalAlignment == VerticalAlignment.Center, "右上日期与返回同字号且垂直居中");
            Check(Field<TextureRect>(_panel, "_background").Texture.ResourcePath.EndsWith("rest_day.png"), "保留原白昼卧房");
            Check(Field<HBoxContainer>(_panel, "_modes").GetChildCount() == 3, "三种自由休息方式");
            Check(Field<Button>(_panel, "_byFull").GetThemeStylebox("normal") is StyleBoxTexture, "方式按钮使用PNG材质");
            var modes = new[] { Field<Button>(_panel, "_byHours"), Field<Button>(_panel, "_byDate"), Field<Button>(_panel, "_byFull") };
            Check(modes.All(button => button.GetThemeStylebox("normal") is StyleBoxTexture face
                && face.Texture.ResourceName.StartsWith("CraftingPaper/button_regular.png/", StringComparison.Ordinal)), "三种方式使用Regular原纸纹而非折角页签");
            Check(modes.All(button => new[] { "normal", "hover", "pressed", "hover_pressed", "disabled" }
                .All(state => button.GetThemeStylebox(state) is StyleBoxTexture face
                    && face.Texture.ResourceName.EndsWith("/selectedFalse", StringComparison.Ordinal))), "选择与各状态不使用selectedTrue中央染色纹理");
            Check(Nodes<RestPaperNumber>(_panel).Count() == 4 && Nodes<RestPaperNumber>(_panel).All(n => n.GetNode<Panel>("NumberWell").GetThemeStylebox("panel") is StyleBoxTexture), "四个数字纸槽使用PNG材质");
            Check(Nodes<SpinBox>(_panel).All(n => !n.IsVisibleInTree()), "不露出旧数字箭头外框");
            Check(_panel.FindChild("ReturnTriangle", true, false) is RestPaperTriangle { Right: true }, "右上返回小实心三角");
            Check(Nodes<Label>(_panel).Where(n => n.IsVisibleInTree()).All(n => !n.Text.Contains("时辰") && !n.Text.Contains(" · 昼") && !n.Text.Contains(" · 夜")), "无时辰昼夜文本");
            await Capture("按时长");
            var hours = Field<SpinBox>(_panel, "_hours");
            hours.Value = 0; Check(Hours() == 1, "手动下限1小时");
            hours.Value = 900; Check(Hours() == 720, "手动上限720小时");
            var number = Nodes<RestPaperNumber>(_panel).First();
            var input = number.GetNode<LineEdit>("NumberInput");
            input.Text = "23"; input.EmitSignal(LineEdit.SignalName.TextSubmitted, input.Text);
            Check(Hours() == 23, "文字输入实际提交");
            number.GetNode<Button>("NumberPlus").EmitSignal(BaseButton.SignalName.Pressed);
            Check(Hours() == 24, "裸三角实际增量");
            Press("_byFull");
            Check(Hours() == 60 && Field<Label>(_panel, "_fullDuration").Text == "60", "40到100精力需60小时");
            Check(Field<Label>(_panel, "_afterStamina").Text == "100 / 100" && Field<Label>(_panel, "_recovered").Text == "60 点", "回满预览遵守上限和恢复率");
            Check(Stamp() == baseline && confirmed == 0, "切换与安排不修改人物时间");
            await Capture("精力回满");
            _actor.current_world_stamina = 99.75f; _panel.Refresh();
            Check(Hours() == 1, "不足1点缺口向上取整");
            _actor.max_world_stamina = 1000; _actor.current_world_stamina = 0; _panel.Refresh();
            Check(Hours() == 1000 && !Field<Button>(_panel, "_start").Disabled, "回满不受手动720小时限制");
            _actor.current_world_stamina = 1000; _panel.Refresh();
            Check(Hours() == 0 && Field<Button>(_panel, "_start").Disabled, "已满零小时禁止开始");
            Press("_start"); Check(calls == 0 && _panel.Visible, "已满无法绕过开始禁用");
            await Capture("精力已满");
            _actor.max_world_stamina = 100; _actor.current_world_stamina = 40; _panel.Refresh();
            baseline = Stamp(); Press("_start");
            Check(confirmed == 60 && calls == 1 && !_panel.Visible, "确认仅传回60小时并关闭");
            Check(Stamp() == baseline, "面板不代替正式调用方结算");
            Press("_start"); Check(calls == 1, "重复信号不会重复结算");
            _clock.month = 12; _clock.day = 30; _clock.hour = 22;
            Open(); Press("_byDate");
            Check(Hours() == 24 && Field<Label>(_panel, "_wakeDate").Text == "2年1月1日", "年末日期保留隐含小时");
            await Capture("指定日期");
            Field<SpinBox>(_panel, "_year").Value = 1; Field<SpinBox>(_panel, "_month").Value = 12; Field<SpinBox>(_panel, "_date").Value = 30;
            Check(Hours() == 0 && Field<Button>(_panel, "_start").Disabled, "同日不可安排");
            Field<SpinBox>(_panel, "_date").Value = 29;
            Check(Hours() < 0 && Field<Button>(_panel, "_start").Disabled, "过去日期不可安排");
            Field<SpinBox>(_panel, "_year").Value = 2; Field<SpinBox>(_panel, "_month").Value = 3;
            Check(Hours() > 720 && !Field<Button>(_panel, "_start").Disabled, "指定日期不套手动720上限");
            Open(fixedHours: 8, note: "剧情限定休息至次日清晨。");
            Check(Hours() == 8 && !Field<HBoxContainer>(_panel, "_modes").Visible && !Field<HBoxContainer>(_panel, "_hoursRow").Visible
                && !Field<VBoxContainer>(_panel, "_dateRow").Visible && !Field<HBoxContainer>(_panel, "_fullRow").Visible, "剧情固定隐藏全部自由选项");
            Check(Field<Label>(_panel, "_note").Text == "剧情限定休息至次日清晨。", "保留真实剧情备注");
            await Frames();
            var fixedScroll = Field<ScrollContainer>(_panel, "_scroll");
            var shortNote = Bounds(Field<Label>(_panel, "_note"));
            var fixedViewport = Bounds(fixedScroll);
            Check(shortNote.Position.Y >= fixedViewport.Position.Y && shortNote.End.Y <= fixedViewport.End.Y + 1, "短剧情备注首屏完整可见");
            Check(fixedScroll.GetVScrollBar().MaxValue <= fixedScroll.Size.Y + 1, "短剧情备注无需滚动");
            Check(Field<Label>(_panel, "_selectionHeading").Position.Y - Field<ProgressBar>(_panel, "_before").GetRect().End.Y < 60, "隐藏方式后收紧固定剧情留白");
            await Capture("剧情限定");
            Open(fixedHours: 800); Check(Hours() == 800, "固定剧情时长不经过手动输入截断");
            _clock.month = 3; _clock.day = 15; _clock.hour = 10; Open(); Press("_byFull");
            var dayTexture = Field<TextureRect>(_panel, "_background").Texture;
            Check(Field<TextureRect>(_panel, "_background").Texture == dayTexture, "预测跨日不切当前背景");
            _clock.hour = 22; _clock.EmitSignal(GameTimeMgr.SignalName.time_changed);
            Check(Field<TextureRect>(_panel, "_background").Texture.ResourcePath.EndsWith("rest_night.png"), "真实时钟切换原夜景");
            await Capture("入夜");
            Open(note: string.Join("\n", Enumerable.Repeat("休息结算沿用当前实际行动。", 12)));
            await Frames();
            var start = Bounds(Field<Button>(_panel, "_start"));
            Check(start.Position.Y >= 0 && start.End.Y <= GetViewport().GetVisibleRect().End.Y + 1, "长剧情备注不挤出开始按钮");
            Check(_panel.FindChild("RestNoteScroll", true, false) is ScrollContainer scroll && scroll.GetVScrollBar().MaxValue > scroll.Size.Y, "完整备注可滚动");
            var previousScroll = Field<ScrollContainer>(_panel, "_scroll");
            previousScroll.ScrollVertical = (int)previousScroll.GetVScrollBar().MaxValue;
            Open(fixedHours: 8, note: "剧情限定休息至次日清晨。");
            await Frames();
            Check(previousScroll.ScrollVertical == 0 && Bounds(Field<Label>(_panel, "_note")).End.Y <= Bounds(previousScroll).End.Y + 1, "重新打开固定休息回到首屏完整备注");
            _panel.GetNode<Button>("MaterialCanvas/RestReturn").EmitSignal(BaseButton.SignalName.Pressed);
            Check(!_panel.Visible, "右上返回收起面板");
            _panel.Open(null!, _clock, "", _ => calls++);
            Check(Hours() == 0 && Field<Button>(_panel, "_start").Disabled && Field<Label>(_panel, "_stamina").Text == "—", "未知人物不使用假数据");
            Check(Field<Label>(_panel, "_healthResult").Text == "—", "未知人物不宣称生命恢复结果");
            await Capture("未绑定");
            _panel.Open(_actor, null!, "观复峰居所", _ => calls++);
            Check(Hours() == 0 && Field<Label>(_panel, "_time").Text == "—" && Field<Button>(_panel, "_start").Disabled, "未知日期不可开始");
            int clicksBefore = calls;
            Open(_ => calls++);
            baseline = Stamp();
            await Click(Field<Button>(_panel, "_byFull"));
            Check(Hours() == 60 && Field<HBoxContainer>(_panel, "_fullRow").IsVisibleInTree(), "指针点击实际选中精力回满");
            await Click(Field<Button>(_panel, "_byHours"));
            int beforePlus = Hours();
            await Click(Nodes<RestPaperNumber>(_panel).First(n => n.IsVisibleInTree()).GetNode<Button>("NumberPlus"));
            Check(Hours() == beforePlus + 1, "指针点击裸三角实际增加1小时");
            await Click(_panel.GetNode<Button>("MaterialCanvas/RestReturn"));
            Check(!_panel.Visible && calls == clicksBefore && Stamp() == baseline, "指针点击返回关闭且未结算");
            GD.Print($"REST_PAPER_UI_PASS {_checks.Count} checks");
        }
        catch (Exception error) { _failure = error.ToString(); GD.PushError("REST_PAPER_UI_FAIL " + error); }
        finally
        {
            var report = new { kind = "原生休息隔离验收", width = _width, height = _width * 9 / 16, headless = _headless,
                scope = "只创建内存人物；不读取玩家存档；不代替统一休息结算",
                pointerInput = _headless ? "headless仅信号" : "图形模式使用Viewport.PushInput命中按钮与裸三角",
                checks = _checks, captures = _captures, failure = _failure };
            string output = ProjectSettings.GlobalizePath(Folder + $"验证记录_{_width}.json");
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(output)!);
            System.IO.File.WriteAllText(output, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            GetTree().Quit(_failure.Length == 0 ? 0 : 1);
        }
    }
}
