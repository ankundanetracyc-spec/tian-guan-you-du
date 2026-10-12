using Godot;

/// <summary>静室分页和休息的临时角色验收，不读取或写入正式旅程。</summary>
public partial class QuietRoomSmoke : Node
{
    private string _folder = "res://docs/美术规范/静室与休息界面概念_20261010/实机验收";
    private bool _capture;
    private static IEnumerable<T> Nodes<T>(Node node) where T : Node
    {
        foreach (var child in node.GetChildren()) { if (child is T match) yield return match; foreach (var descendant in Nodes<T>(child)) yield return descendant; }
    }
    private static void Check(bool condition, string reason) { if (!condition) throw new InvalidOperationException(reason); }
    private static Button Button(Node node, string text) => Nodes<Button>(node).FirstOrDefault(button => button.Text == text) ?? throw new InvalidOperationException("缺少按钮："+text+"；当前："+string.Join(" / ",Nodes<Button>(node).Select(button=>button.Text)));
    private async System.Threading.Tasks.Task Capture(string name)
    {
        for (var i = 0; i < 6; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!_capture) return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng(_folder + "/" + name + ".png");
    }
    public override async void _Ready()
    {
        try
        {
            _capture = OS.GetCmdlineUserArgs().Contains("--capture");
            if (OS.GetCmdlineUserArgs().Contains("--compact")) { GetWindow().ContentScaleSize = new Vector2I(1280, 720); _folder += "/1280x720"; }
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(_folder));
            var time = GetNode<GameTimeMgr>("/root/GameTimeManager"); time.year = 1; time.month = 3; time.day = 15; time.hour = 10;
            var data = new HumanData { entity_name = "静室验收", cultivation_realm = "云息三层", spirit_root_type = (int)GongfaResource.Wuxing.METAL_WOOD_WATER, spirit_root_name = "三灵根", root_weights = [34, 33, 33, 0, 0], main_gongfa_path = "res://resources/gongfa/黄庭养形篇.tres", learned_gongfa_paths = ["res://resources/gongfa/黄庭养形篇.tres", "res://resources/gongfa/乘物游骸真经.tres"], ambient_lingqi_pressure = 180, current_world_stamina = 40, max_world_stamina = 100, has_spirit_core = true };
            var character = new Character(); AddChild(character); character.load_from_data(data); character.Hide();
            character.grant_intro_gongfa(GD.Load<GongfaResource>("res://resources/gongfa/黄庭养形篇.tres"));
            var room = new CultivationUI(); AddChild(room); room.show_entity(character);
            var bg = room.GetNode<TextureRect>("QuietRoom/SharedBackground"); var bgId = bg.GetInstanceId(); var texture = bg.Texture;
            var now = time.get_total_game_hours();
            var tabs = new[] { "吐纳修行", "功法研读", "闭关突破", "修命·淬体", "修性·观法" };
            for (var i = 0; i < tabs.Length; i++)
            {
                Button(room, tabs[i]).EmitSignal(BaseButton.SignalName.Pressed); await Capture($"{i+1:00}_{tabs[i]}");
                Check(bg.IsVisibleInTree() && bg.GetInstanceId() == bgId && bg.Texture == texture, "切页改变了共用背景");
                Check(time.get_total_game_hours() == now, "切页推进了游戏时间");
                Check(Nodes<Label>(room).Any(label => label.IsVisibleInTree() && label.Text.Contains("10时")), "子页未显示当前时间");
            }
            Button(room, "吐纳修行").EmitSignal(BaseButton.SignalName.Pressed);
            foreach (var mode in new[] { "指定灵力", "指定天数", "指定日期", "下层所需" }) { Button(room, mode).EmitSignal(BaseButton.SignalName.Pressed); await Capture("模式_" + mode); }
            room.show_teaching_for(character); Check(room.GetNode<Control>("QuietRoom/RoomPages/StudyPage").IsVisibleInTree(), "授业没有直达研读页");
            Check(Nodes<Button>(room).Any(button => button.IsVisibleInTree() && button.Icon?.ResourcePath == "res://textures/items/item_secret_manual_ink_v1.png"), "秘籍未使用项目原图");
            var studyReader = new Character(); AddChild(studyReader);
            studyReader.load_from_data(new HumanData {entity_name="首卷研读验收", cultivation_realm="云息三层", spirit_root_type=(int)GongfaResource.Wuxing.METAL_WOOD_WATER, current_world_stamina=100});
            studyReader.grant_intro_gongfa(GD.Load<GongfaResource>("res://resources/gongfa/黄庭养形篇.tres"));
            room.show_teaching_for(studyReader);
            var studyStart = Button(room,"开始研读");
            Check(!studyStart.Disabled,"默认显示的秘籍不能研读");
            Check(!Nodes<Label>(room).Any(label=>label.IsVisibleInTree() && (label.Text.Contains("首卷读成后") || label.Text.Contains("切换功法保留"))),"研读页仍显示冗余提示");
            var studyBefore=time.get_total_game_hours(); var staminaBefore=studyReader.current_world_stamina;
            studyStart.EmitSignal(BaseButton.SignalName.Pressed);
            Check(time.get_total_game_hours()==studyBefore+24 && studyReader.current_world_stamina==staminaBefore-24,"默认读本按钮未真正研读或费用错误");
            Check(studyReader.get_current_study_summary().GetValueOrDefault("progress",0f).AsSingle()>0 || studyReader.get_learned_gongfa_chapter_ids(GD.Load<GongfaResource>("res://resources/gongfa/黄庭养形篇.tres")).Count>0,"研读无实际进度");
            now=time.get_total_game_hours();
            GD.Print("QUIET_ROOM_STUDY PASS: default selected book, real progress, 24 hours/stamina, redundant text removed");
            room.show_body_overview(character); Check(Button(room, "吐纳修行").Disabled, "查看态允许越权进入静室行动"); room.close_panel();
            var rest = new RestPanel(); AddChild(rest); var confirmed = 0; rest.Open(character, time, "李宅 · 偏房", count => confirmed = count); await Capture("06_白天休息");
            if (OS.GetCmdlineUserArgs().Contains("--compact"))
            {
                var start = Button(rest, "开始休息");
                Nodes<ScrollContainer>(rest).First().EnsureControlVisible(start); await Capture("06b_休息行动滚动");
                Check(start.GetGlobalRect().Position.Y >= 0 && start.GetGlobalRect().End.Y <= rest.GetViewportRect().End.Y, "小窗口休息行动不可达");
            }
            var restBg = rest.GetNode<TextureRect>("SharedBackground"); Check(restBg.Texture.ResourcePath.EndsWith("rest_day.png"), "白天背景错误");
            Check(time.get_total_game_hours() == now && confirmed == 0, "打开休息推进时间");
            time.add_hours(12); await Capture("07_夜晚休息"); Check(restBg.Texture.ResourcePath.EndsWith("rest_night.png"), "时间变化没有刷新夜景");
            Button(rest, "开始休息").EmitSignal(BaseButton.SignalName.Pressed); Check(confirmed == 8 && !rest.Visible, "休息确认没有传回8小时或关闭");
            rest.Open(character, time, "李宅 · 偏房", count => confirmed = count, 8, "剧情限定休息至次日清晨。"); await Capture("08_剧情限定休息");
            Check(!Button(rest, "按时长").IsVisibleInTree() && !Button(rest, "指定日期").IsVisibleInTree(), "剧情限定休息仍开放自定义"); rest.Close();
            time.month = 12; time.day = 30; time.hour = 22; rest.Open(character, time, "李宅", count => confirmed = count); Button(rest, "指定日期").EmitSignal(BaseButton.SignalName.Pressed); Button(rest, "开始休息").EmitSignal(BaseButton.SignalName.Pressed); Check(confirmed == 24, "年末指定日期计算错误");
            character.cultivation_realm = "云息十层"; room.show_entity(character); await Capture("09_十层归元九息"); Check(Nodes<YuanxiPanel>(room).Any(panel => panel.IsVisibleInTree()), "归元九息未嵌入静室"); room.close_panel();
            // 覆盖实际行动结算，而非仅切换页面：低效率、零体力也须能修炼。
            foreach (var mode in new[] { "指定灵力", "指定天数", "指定日期", "下层所需" })
            {
                character.cultivation_realm = "云息三层"; character.spirit_strands = 36; character.current_world_stamina = 0;
                room.show_entity(character); Button(room, mode).EmitSignal(BaseButton.SignalName.Pressed);
                if (mode == "指定灵力") Nodes<SpinBox>(room.GetNode("QuietRoom/RoomPages/PracticePage")).First().Value = 12;
                var start = Button(room, "开始修炼"); Check(!start.Disabled, mode + "在零体力下被禁用");
                if (mode == "指定灵力") await Capture("10_零体力指定12缕");
                var beforeTime = time.get_total_game_hours(); start.EmitSignal(BaseButton.SignalName.Pressed);
                Check(time.get_total_game_hours() > beforeTime && character.current_world_stamina == 0, mode + "未推进时间或改变体力");
                if (mode == "指定灵力") Check(character.spirit_strands == 48, "指定12缕没有实际获得12缕");
                if (mode == "下层所需") Check(character.is_breakthrough_ready(), "下层所需未修炼至突破门槛");
                room.close_panel();
            }
            character.spirit_strands = 36; Check(character.manual_cultivate() && character.current_world_stamina == 0, "单次吐纳仍受体力限制");
            Check(character.get_available_study_hours(24) == 0, "错误地取消了研读体力限制");
            character.cultivation_realm = "云息十层"; Check(character.start_yuanxi(9), "零体力无法开始养息");
            character.advance_yuanxi(100000); Check(character.yuanxi_count == 9 && !character.is_yuanxi_active() && character.current_world_stamina == 0, "零体力未完成九息或体力改变");
            GD.Print("CULTIVATION_NO_STAMINA_PASS: four real actions, 12 strands, hours/date, realm target, manual, nine breaths, study still costs stamina");
            GD.Print("QUIET_ROOM_SMOKE_PASS: five pages, four modes, shared texture, time, original icons, readonly, day/night, forced rest, year rollover, yuanxi");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PrintErr(error); GetTree().Quit(1); }
    }
}
