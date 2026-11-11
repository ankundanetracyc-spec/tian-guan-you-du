using Godot;
using System.Reflection;
using System.Text.Json;

/// <summary>真实Character的静室三页与行动回归；只用项目内隔离配置，不开旅程存档。</summary>
public partial class QuietRoomPaperUiSmoke : Node
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<object> _checks = [];
    private readonly List<string> _captures = [];
    private CultivationUI _ui = null!;
    private GameTimeMgr _clock = null!;
    private string _output = "";
    private int _width = 1280, _height = 720;
    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Hidden)!.GetValue(target)!;
    private static object? Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, Hidden)!.Invoke(target, args);
    private static T Find<T>(Node node, string name) where T : Node => node.FindChild(name, true, false) as T ?? throw new InvalidOperationException("Missing " + name);
    private void Check(bool value, string label)
    {
        _checks.Add(new { label, passed = value });
        if (!value) throw new InvalidOperationException(label);
        GD.Print("QUIET_PAPER_UI_OK " + label);
    }
    private async Task Frames(int count = 3) { for (int index = 0; index < count; index++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private async Task Click(Button button)
    {
        Check(button.IsVisibleInTree() && !button.Disabled, "控件可操作 " + button.Name);
        if (DisplayServer.GetName() == "headless") button.EmitSignal(BaseButton.SignalName.Pressed);
        else
        {
            var point = button.GetGlobalTransformWithCanvas() * (button.Size * .5f);
            GetViewport().PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point }, true);
            foreach (bool down in new[] { true, false }) GetViewport().PushInput(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = down }, true);
        }
        await Frames();
    }
    private void PointerButton(Button button, bool down)
    {
        if (DisplayServer.GetName() == "headless") { button.EmitSignal(down ? BaseButton.SignalName.ButtonDown : BaseButton.SignalName.ButtonUp); return; }
        var point = button.GetGlobalTransformWithCanvas() * (button.Size * .5f);
        GetViewport().PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point }, true);
        GetViewport().PushInput(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = down }, true);
    }
    private static bool OriginalPaper(StyleBoxTexture style, string file) => style.Texture is not null
        && style.Texture.ResourceName.Contains(file, StringComparison.Ordinal)
        && style.Texture.ResourceName.EndsWith("/selectedFalse", StringComparison.Ordinal);
    private static bool RegularPaper(Button button) => new[] { "normal", "hover", "pressed", "hover_pressed", "disabled" }
        .All(state => button.GetThemeStylebox(state) is StyleBoxTexture style && OriginalPaper(style, "button_regular.png"));
    private async Task CheckPaperFeedback(List<Button> tabs)
    {
        var navigation = Find<Panel>(tabs[0], "NavigationPaper");
        var before = (StyleBoxTexture)navigation.GetThemeStylebox("panel");
        Check(OriginalPaper(before, "button_navigation.png") && ReferenceEquals(before.Texture,
            CraftingPaperStyle.ButtonStyle(CraftingPaperStyle.ButtonKind.Navigation).Texture), "选中导航使用原selectedFalse纸纹，无中央方形色块");
        PointerButton(tabs[0], true); await Frames(1);
        var pressed = (StyleBoxTexture)navigation.GetThemeStylebox("panel");
        Check(OriginalPaper(pressed, "button_navigation.png") && pressed.ExpandMarginTop == -1
            && pressed.ModulateColor != before.ModulateColor, "导航真实按下时整纸压暗下沉");
        PointerButton(tabs[0], false); await Frames(2);
        Check(((StyleBoxTexture)navigation.GetThemeStylebox("panel")).ExpandMarginTop == 0, "导航松开恢复纸面且保留侧签选择");
        var modes = Field<List<Button>>(_ui, "_modeTabs");
        Check(modes.Count == 4 && modes.All(RegularPaper), "四种修炼模式全状态使用普通整纸素材，无折角页签");
        PointerButton(modes[0], true); await Frames(1);
        Check(((StyleBoxTexture)modes[0].GetThemeStylebox("pressed")).ExpandMarginTop == -1,
            "已选中的模式再次按住也有瞬时反馈");
        PointerButton(modes[0], false); await Frames(2);
        Check(((StyleBoxTexture)modes[0].GetThemeStylebox("pressed")).ExpandMarginTop == 0,
            "松开后保留整纸选中色并恢复高度");
        PointerButton(modes[1], true); await Frames(1);
        if (DisplayServer.GetName() != "headless")
            Check(modes[1].GetDrawMode().ToString().Contains("Pressed", StringComparison.Ordinal)
                && ((StyleBoxTexture)modes[1].GetThemeStylebox("pressed")).ExpandMarginTop == -1,
                "普通修炼选择按钮响应真实按下状态");
        PointerButton(modes[1], false); await Frames(2);
        await Click(modes[0]);
    }
    private async Task Capture(string name)
    {
        if (DisplayServer.GetName() == "headless") return;
        GetViewport().PushInput(new InputEventMouseMotion { Position = Vector2.One * 4, GlobalPosition = Vector2.One * 4 }, true);
        HoverInfoManager.HideAll();
        await Frames(); await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage();
        Check(image.GetWidth() == _width && image.GetHeight() == _height, "原生截图尺寸 " + name);
        string path = $"quiet_room_{name}_{_width}.png";
        Check(image.SavePng(System.IO.Path.Combine(_output, path)) == Error.Ok, "保存截图 " + name); _captures.Add(path);
    }
    private Character Actor(string name, string realm = "云息三层", bool learned = true)
    {
        var actor = new Character { ProcessMode = ProcessModeEnum.Disabled }; AddChild(actor);
        var books = learned ? GongfaAvailability.EnabledBooks().Take(3).Select(book => book.ResourcePath).ToArray() : [];
        var data = new HumanData { entity_name = name, cultivation_realm = realm,
            spirit_root_type = (int)GongfaResource.Wuxing.METAL_WOOD_WATER_FIRE_EARTH, spirit_root_name = "五行灵根", root_weights = [20, 20, 20, 20, 20],
            main_gongfa_path = learned ? GongfaAvailability.DefaultBookPath : "", current_world_stamina = 100,
            max_world_stamina = 100, ambient_lingqi_pressure = 180, spirit_strands = realm == "云息三层" ? 36 : 0 };
        foreach (string book in books) data.learned_gongfa_paths.Add(book);
        if (learned && !data.learned_gongfa_paths.Contains(GongfaAvailability.DefaultBookPath)) data.learned_gongfa_paths.Add(GongfaAvailability.DefaultBookPath);
        actor.load_from_data(data); actor.Hide(); actor.current_world_stamina = actor.max_world_stamina = 100; return actor;
    }
    private void Configure()
    {
        var args = OS.GetCmdlineUserArgs();
        string width = args.FirstOrDefault(arg => arg.StartsWith("--quiet-width="))?.Split('=', 2)[1] ?? "1280";
        if (!int.TryParse(width, out _width) || _width is not (1280 or 1920)) throw new ArgumentException("Expected quiet-width=1280 or 1920");
        _height = _width == 1920 ? 1080 : 720;
        string project = System.IO.Path.GetFullPath(ProjectSettings.GlobalizePath("res://"));
        string user = System.IO.Path.GetFullPath(ProjectSettings.GlobalizePath("user://"));
        Check(user.StartsWith(System.IO.Path.Combine(project, "output") + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), "隔离APPDATA位于项目output内");
        string supplied = args.FirstOrDefault(arg => arg.StartsWith("--quiet-output="))?.Split('=', 2)[1] ?? "res://output/quiet-room-paper-native";
        _output = System.IO.Path.GetFullPath(supplied.StartsWith("res://") ? ProjectSettings.GlobalizePath(supplied) : supplied);
        if (!_output.StartsWith(System.IO.Path.Combine(project, "output") + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Output must remain inside project output");
        System.IO.Directory.CreateDirectory(_output);
        GetWindow().Mode = Window.ModeEnum.Windowed; GetWindow().Borderless = true; GetWindow().Unfocusable = true;
        GetWindow().ContentScaleSize = Vector2I.Zero; GetWindow().Size = new Vector2I(_width, _height);
    }
    public override void _Ready() { ProcessMode = ProcessModeEnum.Always; CallDeferred(nameof(Run)); }
    private async void Run()
    {
        bool passed = false; string error = "";
        try
        {
            Configure(); await Frames(); _clock = GetNode<GameTimeMgr>("/root/GameTimeManager");
            var actor = Actor("静室实机验证");
            _ui = new CultivationUI(); AddChild(_ui); _ui.show_entity(actor); await Frames(5);
            var tabs = Field<List<Button>>(_ui, "_roomTabs"); var tabIds = tabs.Select(tab => tab.GetInstanceId()).ToArray();
            Check(tabs.Count == 3 && tabs.All(tab => tab is QuietRoomPaperTab), "正式静室仅三页素材导航");
            Check(Field<Label>(_ui, "_name").Text == actor.entity_data!.entity_name, "姓名取真实Owner");
            Check(Field<Label>(_ui, "_roomTime").Text == QuietRoomStyle.Date(_clock), "日期只显示真实年月日");
            Check(Field<Label>(_ui, "_power").GetThemeColor("font_color") == new Color("5d9da8"), "灵力缕数为淡青色");
            await CheckPaperFeedback(tabs);
            double originalTime = _clock.get_precise_game_hours(); int originalStrands = actor.spirit_strands; float originalEnergy = actor.current_world_stamina;
            for (int page = 0; page < 3; page++)
            {
                await Click(tabs[page]); await ToSignal(GetTree().CreateTimer(.25), SceneTreeTimer.SignalName.Timeout);
                Check(Field<int>(_ui, "_roomPage") == page && tabs.Select(tab => tab.GetInstanceId()).SequenceEqual(tabIds), "切页保持真实正文与侧栏节点 " + page);
                Check(tabs.Cast<QuietRoomPaperTab>().Select((tab, index) => Math.Abs(tab.Reveal - (index == page ? 18 : 0)) < .1f && Math.Abs(tab.BindingOpacity - (index == page ? 1 : 0)) < .01).All(value => value), "纸签18px位移与装订边终态 " + page);
                await Capture(new[] { "practice", "study", "breakthrough" }[page]);
            }
            Check(originalTime == _clock.get_precise_game_hours() && actor.spirit_strands == originalStrands && actor.current_world_stamina == originalEnergy, "查看三页不推进时间、不耗精力、不增灵力");
            _ui.SetRoomReducedMotion(true); await Click(tabs[1]);
            Check(((QuietRoomPaperTab)tabs[1]).Reveal == 18 && ((QuietRoomPaperTab)tabs[0]).Reveal == 0, "减少动态效果直接进入选中终态");
            var book = (GongfaResource?)Invoke(_ui, "selected_study_book");
            Check(book is not null && Field<GridContainer>(_ui, "_paperStudyChapters").GetChildCount() == book.get_sorted_chapters().Count, "卷章清单来自实际所选秘籍");
            Check(Field<VBoxContainer>(_ui, "_studyList").GetChildren().OfType<Button>().All(RegularPaper)
                && Field<GridContainer>(_ui, "_paperStudyChapters").GetChildren().OfType<Button>().All(RegularPaper),
                "书目和卷章选择使用普通selectedFalse纸面，全状态无折角及中央块");
            var chapterButton = Field<GridContainer>(_ui, "_paperStudyChapters").GetChildren().OfType<Button>().FirstOrDefault();
            if (chapterButton is not null)
            {
                await Click(chapterButton); Check(Find<Control>(_ui, "QuietRoomChapterOverlay").Visible, "真实卷章可展开查阅");
                _ui._Input(new InputEventKey { Keycode = Key.Escape, Pressed = true });
                Check(!Find<Control>(_ui, "QuietRoomChapterOverlay").Visible && _ui.Visible, "Esc仅收起卷章浮层");
            }
            await Click(tabs[0]); await Click(Find<Button>(_ui, "PracticeMode2")); await Capture("date");
            Check(Field<VBoxContainer>(_ui, "_dateRow").IsVisibleInTree() && !Field<Label>(_ui, "_roomTime").Text.Contains('时'), "指定日期仅年月日");
            await Click(Find<Button>(_ui, "PracticeMode0"));
            var strands = Field<SpinBox>(_ui, "_strands"); strands.Value = 2;
            var field = Find<QuietRoomNumber>(_ui, "PracticeStrands"); await Click(Find<Button>(field, "NumberPlus"));
            Check(strands.Value == 3 && Find<LineEdit>(field, "NumberInput").Text == "3", "裸三角同步原SpinBox与可见灵力输入");
            await Click(Find<Button>(field, "NumberMinus"));
            actor.current_world_stamina = 0; _ui.show_entity(actor); strands.Value = 2;
            int before = actor.spirit_strands; double timeBefore = _clock.get_precise_game_hours();
            await Click(Find<Button>(_ui, "PracticeStartButton"));
            Check(actor.spirit_strands == before + 2 && actor.current_world_stamina == 0 && _clock.get_precise_game_hours() > timeBefore, "正式吐纳真实获得两缕、推进时间且零精力可行");
            var reader = Actor("首卷研读验证", learned: false); var initialBook = GD.Load<GongfaResource>(GongfaAvailability.DefaultBookPath); reader.grant_intro_gongfa(initialBook);
            _ui.show_teaching_for(reader); await Frames();
            timeBefore = _clock.get_precise_game_hours(); before = reader.get_learned_gongfa_chapter_ids(initialBook).Count;
            await Click(Find<Button>(_ui, "StudyStartButton"));
            Check(_clock.get_precise_game_hours() - timeBefore == 24 && reader.current_world_stamina == 76, "原研读入口保留24小时24精力真实结算");
            Check(reader.get_learned_gongfa_chapter_ids(initialBook).Count > before || reader.get_current_study_summary().GetValueOrDefault("progress", 0f).AsSingle() > 0, "所选读本产生真实卷章进度");
            var advanced = Actor("归元九息验证", "云息十层"); _ui.show_entity(advanced); await Frames();
            Check(Field<YuanxiPanel>(_ui, "_yuanxiPanel").IsVisibleInTree(), "归元九息保留嵌入入口");
            await Capture("yuanxi");
            var back = Find<Button>(_ui, "QuietRoomReturnButton"); var arrow = Find<Control>(back, "ReturnTriangle");
            Check(back.Position == new Vector2(1534, 18) && back.Size == new Vector2(114, 46) && back.GetThemeFontSize("font_size") == 22, "返回遵守全局右上边距、尺寸与字级");
            var date = Field<Label>(_ui, "_roomTime");
            Check(Math.Abs(date.GetGlobalRect().Position.Y - back.GetGlobalRect().Position.Y) < .5f && Math.Abs(date.GetGlobalRect().Size.Y - back.GetGlobalRect().Size.Y) < .5f && date.GetThemeFontSize("font_size") == back.GetThemeFontSize("font_size") && date.VerticalAlignment == VerticalAlignment.Center, "右上日期与返回同一行、同高度同字号并居中");
            Check(arrow.Position.X >= 0 && arrow.Position.X + arrow.Size.X <= back.Size.X && arrow.Position.Y >= 0, "统一返回三角位于按钮内部右侧");
            await Click(back); Check(!_ui.Visible, "右上返回收起正式静室");
            passed = true;
        }
        catch (Exception caught) { error = caught.ToString(); GD.PushError(error); }
        finally
        {
            if (GodotObject.IsInstanceValid(_ui)) _ui.QueueFree(); await Frames();
            if (_output.Length > 0 && System.IO.Directory.Exists(_output)) System.IO.File.WriteAllText(System.IO.Path.Combine(_output, $"quiet_room_checks_{_width}.json"), JsonSerializer.Serialize(new { passed, error, width = _width, height = _height, checks = _checks, captures = _captures, isolated = true, playerSaveRead = false }, new JsonSerializerOptions { WriteIndented = true }));
            GD.Print($"QUIET_PAPER_UI_{(passed ? "PASS" : "FAIL")} width={_width} checks={_checks.Count} captures={_captures.Count}"); GetTree().Quit(passed ? 0 : 1);
        }
    }
}
