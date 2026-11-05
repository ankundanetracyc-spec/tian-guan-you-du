using Godot;
using System.Reflection;
using System.Threading.Tasks;

/// <summary>Exercises the actual character page, independent persistence, and the real drill entry.</summary>
public partial class ActionBattlePanelSmoke : Node
{
    private int _checks;
    private InfoPanel _panel = null!;
    private Character _owner = null!;
    private string _evidence = "", _scratch = "";
    private ActionCharacterBattleSession? _session;
    private void Check(bool value, string text)
    {
        if (!value) throw new InvalidOperationException(text);
        _checks++; GD.Print("ACTION_PANEL_PASS " + text);
    }
    private static void Call(InfoPanel panel, string method, params object[] args) =>
        typeof(InfoPanel).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(panel, args);
    private async Task Settle()
    { for (var i = 0; i < 5; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private T Find<T>(string name) where T : Node => (T)_panel.FindChild(name, true, false)!;
    private Button Library(string key) => Find<VBoxContainer>("ActionSkillLibrary").GetChildren().OfType<Button>()
        .Single(button => button.GetMeta("action_skill_key", "").AsString() == key);
    private void Click(string name) => Find<Button>(name).EmitSignal(BaseButton.SignalName.Pressed);
    private async Task Capture(string name)
    {
        if (DisplayServer.GetName() == "headless") return;
        Call(_panel, "hide_tooltip");
        await Settle(); await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var screenshot = GetViewport().GetTexture().GetImage();
        Check(screenshot.SavePng(_evidence + "/" + name + ".png") == Error.Ok, "实际窗口截图 " + name);
    }
    private async Task Drag(Button source, Button target)
    {
        Call(_panel, "hide_tooltip");
        var from = source.GetGlobalRect().GetCenter(); var to = target.GetGlobalRect().GetCenter();
        GetViewport().PushInput(new InputEventMouseButton { Position = from, GlobalPosition = from, ButtonIndex = MouseButton.Left, Pressed = true });
        await Settle();
        GetViewport().PushInput(new InputEventMouseMotion { Position = from + new Vector2(24, 0), GlobalPosition = from + new Vector2(24, 0), Relative = new Vector2(24, 0), ButtonMask = MouseButtonMask.Left });
        await Settle();
        GetViewport().PushInput(new InputEventMouseMotion { Position = to, GlobalPosition = to, Relative = to - from - new Vector2(24, 0), ButtonMask = MouseButtonMask.Left });
        await Settle();
        GetViewport().PushInput(new InputEventMouseButton { Position = to, GlobalPosition = to, ButtonIndex = MouseButton.Left, Pressed = false });
        await Settle();
    }
    public override void _Ready() => CallDeferred(nameof(Run));
    private async void Run()
    {
        try
        {
            _evidence = OS.GetEnvironment("ACTION_BATTLE_PANEL_OUTPUT");
            _scratch = OS.GetEnvironment("ACTION_BATTLE_PANEL_WORK");
            if (_evidence.Length == 0) _evidence = ProjectSettings.GlobalizePath("res://work/action_battle_panel/evidence");
            if (_scratch.Length == 0) _scratch = ProjectSettings.GlobalizePath("res://work/action_battle_panel");
            DirAccess.MakeDirRecursiveAbsolute(_evidence); DirAccess.MakeDirRecursiveAbsolute(_scratch);
            _owner = new Character { Visible = false, entity_data = new HumanData
            { entity_name = "战斗配置预览", cultivation_realm = "云息一层", base_physique = 8, base_intelligence = 10, base_soul = 8 } };
            AddChild(_owner);
            var data = (HumanData)_owner.entity_data!;
            var oldSlots = _owner.get_combat_skill_slot_paths().ToArray();
            var oldBindings = data.skill_rewrite_bindings.Duplicate();
            Check(ActionCombatLoadout.Read(_owner).SequenceEqual(new[] { ActionCombatLoadout.Heavy, ActionCombatLoadout.Guard, "", "", "" }), "默认五槽，已学技能不自动装配");
            Check(!data.action_combat_skill_slots_configured, "读取默认配置不标记手动装配");
            Check(!ActionCombatLoadout.Assign(_owner, 2, ActionCombatLoadout.Blood), "未学催血不能装配");
            Check(!ActionCombatLoadout.Assign(_owner, 2, ActionCombatLoadout.BasicAttack) && !ActionCombatLoadout.Assign(_owner, 2, ActionCombatLoadout.Dodge), "固定拳掌和错步不进入自由槽");
            Check(!ActionCombatLoadout.Assign(_owner, -1, "") && !ActionCombatLoadout.Assign(_owner, 5, ""), "槽位边界拒绝越界输入");
            Check(!ActionCombatLoadout.Write(_owner, new() { ActionCombatLoadout.Heavy, ActionCombatLoadout.Heavy, "", "", "" }), "重复装配整笔拒绝");
            Check(!ActionCombatLoadout.Write(_owner, new() { "not-a-skill", "", "", "", "" }), "无效技能整笔拒绝");
            Check(!ActionCombatLoadout.Write(_owner, new()), "非五槽配置拒绝");
            Check(!data.action_combat_skill_slots_configured, "无效写入不改变原配置");

            _owner.active_skills.Add(GD.Load<ActiveSkillData>(ActionCombatLoadout.Blood));
            _owner.active_skills.Add(GD.Load<ActiveSkillData>(ActionCombatLoadout.Qi));
            const string pending = "res://resources/activeskills/避锋.tres";
            _owner.active_skills.Add(GD.Load<ActiveSkillData>(pending));
            _panel = GD.Load<PackedScene>("res://tscn/panel/InfoPanel.tscn").Instantiate<InfoPanel>();
            AddChild(_panel); _panel.show_entity(_owner); Call(_panel, "render_new_page", 2); await Settle();
            Check(Find<GridContainer>("ActionSkillSlots").GetChildCount() == 5, "真实角色战斗页只有五个自由槽");
            Check(Find<HBoxContainer>("FixedActionControls").GetChildren().OfType<Button>().Count() == 2, "拳掌和错步两个固定入口");
            Check(Find<RichTextLabel>("ActionSkillDescription").Text.Contains("四段连招") && Find<RichTextLabel>("ActionSkillDescription").Text.Contains("2.2秒"), "拳掌详情使用现行四段说明");
            Check(Find<Label>("ActionSlotCount").Text == "2 / 5", "计数只统计动作五槽");
            var formulasInitiallyShown = Find<Button>("ToggleActionSkillFormulas").Text.StartsWith("收起");
            if (!formulasInitiallyShown) { Click("ToggleActionSkillFormulas"); await Settle(); }
            Check(Find<RichTextLabel>("ActionSkillDescription").Text.Contains("max("), "显式展开可读取实际冷却公式");
            Click("ToggleActionSkillFormulas"); await Settle();
            Check(!Find<RichTextLabel>("ActionSkillDescription").Text.Contains("max("), "收起公式保留简洁说明");

            Click("ActionSlot2"); Library(ActionCombatLoadout.Blood).EmitSignal(BaseButton.SignalName.Pressed); await Settle();
            Check(ActionCombatLoadout.Read(_owner)[2] == ActionCombatLoadout.Blood, "点击槽位再选技能可装配");
            Check(Find<RichTextLabel>("ActionSkillDescription").Text.Contains("运功1秒") && Find<RichTextLabel>("ActionSkillDescription").Text.Contains("移速50%"), "催血显示真实运功和移动限制");
            Library(ActionCombatLoadout.Qi).EmitSignal(BaseButton.SignalName.Pressed); await Settle();
            Click("EquipActionSkill"); await Settle();
            Check(ActionCombatLoadout.Read(_owner)[3] == ActionCombatLoadout.Qi, "详情按钮装入下一个空槽");
            Check(Find<RichTextLabel>("ActionSkillDescription").Text.Contains("公共周天"), "聚炁详情说明公共周天或零容量限制");
            Library(pending).EmitSignal(BaseButton.SignalName.Pressed); await Settle();
            Check(Find<RichTextLabel>("ActionSkillDescription").Text.Contains("即时战斗待适配") && _panel.FindChild("EquipActionSkill", true, false) is null, "未迁移技能可查阅且无装配按钮");
            Check(!ActionCombatLoadout.Assign(_owner, 4, pending), "不可用技能不能绕过界面装配");
            Check(_owner.get_combat_skill_slot_paths().SequenceEqual(oldSlots) && data.skill_rewrite_bindings.Count == oldBindings.Count, "新配置不覆盖旧装配和化法绑定");

            Library(ActionCombatLoadout.Qi).EmitSignal(BaseButton.SignalName.Pressed); await Settle();
            await Capture("角色战斗页_1920");
            foreach (var size in new[] { new Vector2I(1280, 720), new Vector2I(1920, 1080) })
            {
                GetWindow().Mode = Window.ModeEnum.Windowed; GetWindow().Size = size; await Settle();
                var frame = Find<PanelContainer>("ActionCombatLoadout").GetGlobalRect();
                Check(Find<Control>("CharacterPage2").GetGlobalRect().Encloses(frame), $"{size.X}宽窗口装配面板不溢出页面");
                foreach (var button in Find<GridContainer>("ActionSkillSlots").GetChildren().OfType<Button>())
                    Check(frame.Encloses(button.GetGlobalRect()), $"{size.X}宽窗口槽位不溢出 {button.Name}");
                Check(frame.Encloses(Find<Button>("ActionBattleDrill").GetGlobalRect()), $"{size.X}宽窗口演练按钮不溢出");
                if (size.X == 1280) await Capture("角色战斗页_1280");
            }

            if (DisplayServer.GetName() != "headless")
            {
                await Drag(Find<Button>("ActionSlot0"), Find<Button>("ActionSlot1"));
                Check(ActionCombatLoadout.Read(_owner)[0] == ActionCombatLoadout.Guard && ActionCombatLoadout.Read(_owner)[1] == ActionCombatLoadout.Heavy, "真实鼠标拖拽交换槽位");
                await Drag(Find<Button>("ActionSlot0"), Find<Button>("ActionSlot1"));
                Check(ActionCombatLoadout.Read(_owner)[0] == ActionCombatLoadout.Heavy, "再次拖拽恢复顺序");
            }
            Library(ActionCombatLoadout.Qi).EmitSignal(BaseButton.SignalName.Pressed); await Settle();
            Click("UnequipActionSkill"); await Settle();
            Check(ActionCombatLoadout.Read(_owner)[3] == "", "详情按钮可卸下技能");
            Click("ActionSlot3"); Library(ActionCombatLoadout.Qi).EmitSignal(BaseButton.SignalName.Pressed); await Settle();

            var saved = ActionCombatLoadout.Read(_owner).ToArray();
            Check(ResourceSaver.Save(data, _scratch + "/action-loadout-roundtrip.tres") == Error.Ok, "动作配置写入独立测试存档");
            var reloaded = ResourceLoader.Load<HumanData>(_scratch + "/action-loadout-roundtrip.tres", cacheMode: ResourceLoader.CacheMode.Ignore);
            Check(reloaded.action_combat_skill_slots_configured && reloaded.action_combat_skill_slot_paths.SequenceEqual(saved), "存读档保留顺序和装配状态");
            var empty = new Godot.Collections.Array<string>(Enumerable.Repeat("", 5));
            Check(ActionCombatLoadout.Write(_owner, empty) && ActionCombatLoadout.Read(_owner).All(key => key.Length == 0), "主动清空全部技能不恢复默认");
            Check(ActionCombatLoadout.Write(_owner, new(saved)), "恢复原配置");

            var graphTab = _panel.FindChildren("*", "Button", true, false).OfType<Button>().First(button => button.IsVisibleInTree() && button.Text == "造诣图谱");
            graphTab.EmitSignal(BaseButton.SignalName.Pressed); await Settle();
            Check(_panel.FindChild("SkillNodeNetwork", true, false) is Control { Visible: true }, "造诣图谱学习入口保留");
            _panel.FindChildren("*", "Button", true, false).OfType<Button>().First(button => button.IsVisibleInTree() && button.Text == "战斗配置").EmitSignal(BaseButton.SignalName.Pressed); await Settle();
            Check(ActionCombatLoadout.Read(_owner).SequenceEqual(saved), "切换图谱不改变装配");

            var health = _owner.current_health; var mana = _owner.current_mana; var world = GetTree().CurrentScene;
            Click("ActionBattleDrill"); await Settle();
            _session = GetTree().GetFirstNodeInGroup("current_character_action_drill") as ActionCharacterBattleSession;
            Check(_session is not null, "实际演练按钮进入角色即时战斗");
            var arena = GetTree().CurrentScene; arena.SetProcess(false);
            Check(arena.Get("skill_slots").AsGodotArray().Select(value => value.AsString()).SequenceEqual(ActionCombatLoadout.ReadNames(_owner)), "演练按保存顺序读取五个技能槽");
            var attributes = _owner.get_snapshot_data();
            var capacity = ActionBattleAttributeRuntime.InitializeActionSnapshot(attributes);
            foreach (var key in new[] { ActionCombatLoadout.BasicAttack, ActionCombatLoadout.Dodge, ActionCombatLoadout.Heavy, ActionCombatLoadout.Guard, ActionCombatLoadout.Blood, ActionCombatLoadout.Qi })
            {
                var preview = ActionCombatSkillPresentation.Read(key, attributes, capacity);
                Check(Mathf.IsEqualApprox(preview["cooldown"].AsSingle(), arena.Call("skill_cooldown_duration", ActionCombatLoadout.Name(key)).AsSingle()), "详情冷却等于实际角色冷却 " + ActionCombatLoadout.Name(key));
            }
            arena.Set("configuring", true); arena.Call("assign_skill_slot", 4, "聚炁");
            _session!.Close(); _session = null; await Settle();
            Check(GetTree().CurrentScene == world && _panel.Visible && !GetTree().Paused, "退出演练返回原人物面板");
            Check(_owner.current_health == health && _owner.current_mana == mana && ActionCombatLoadout.Read(_owner).SequenceEqual(saved), "演练内改动不回写人物资源或局外配置");
            Library(ActionCombatLoadout.Qi).EmitSignal(BaseButton.SignalName.Pressed); await Settle();
            await Capture("角色战斗页_1920");
            Click("FixedBasicAttack"); await Settle();
            await Capture("角色战斗页_拳掌");
            Click("ToggleActionSkillFormulas"); await Settle();
            await Capture("角色战斗页_计算公式");
            GD.Print($"ACTION_BATTLE_PANEL_OK {_checks}");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            _session?.Close(); GD.PushError(error.ToString()); GetTree().Quit(1);
        }
    }
}
