using Godot;
using System.Reflection;

/// <summary>原生十槽、化法拖拽、旧档与首页入口的隔离验收，不读取正式旅程。</summary>
public partial class QueueCombatRestorationSmoke : Node
{
    private const string Attack = "res://resources/activeskills/应敌.tres";
    private const string Heavy = "res://resources/activeskills/痛击.tres";
    private const string Guard = "res://resources/activeskills/架招.tres";
    private const string Book = "res://resources/gongfa/玄枢含章经.tres";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private InfoPanel _panel = null!;
    private Character _owner = null!;
    private int _checks;
    private readonly string _scratch = "res://tmp/queue_combat_restore_20261105";
    private void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); _checks++; GD.Print("QUEUE_RESTORE_PASS " + message); }
    private static void Call(object owner, string method, params object[] args) => owner.GetType().GetMethod(method, Private)!.Invoke(owner, args);
    private T Find<T>(string name) where T : Node => (T)_panel.FindChild(name, true, false)!;
    private Button Slot(int index) => Find<Button>("CombatSkillSlot_" + (index + 1));
    private Button Library(string key) => Find<VBoxContainer>("CharacterSkillLibrary").GetChildren().OfType<Button>()
        .Single(button => button.GetMeta("character_skill_key", "").AsString() == key);
    private Vector2 ScreenCenter(Control control) => GetViewport().GetFinalTransform() * (control.GetGlobalTransformWithCanvas() * (control.Size / 2));
    private async Task Frames(int count = 8)
    { for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private void Pointer(Vector2 point) => Input.ParseInputEvent(new InputEventMouseMotion { Position = point, GlobalPosition = point });
    private void Mouse(Vector2 point, bool pressed) => Input.ParseInputEvent(new InputEventMouseButton
        { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = pressed });
    private async Task Click(Button button)
    {
        Call(_panel, "hide_tooltip"); var point = ScreenCenter(button);
        Pointer(point); Mouse(point, true); Mouse(point, false); await Frames();
    }
    private async Task Drag(Button source, Button target, bool hold = false)
    {
        Call(_panel, "hide_tooltip"); var from = ScreenCenter(source); var to = ScreenCenter(target);
        Pointer(from); Mouse(from, true); await Frames();
        if (hold) await ToSignal(GetTree().CreateTimer(.45), SceneTreeTimer.SignalName.Timeout);
        Input.ParseInputEvent(new InputEventMouseMotion { Position = from + new Vector2(24, 0), GlobalPosition = from + new Vector2(24, 0), Relative = new Vector2(24, 0), ButtonMask = MouseButtonMask.Left });
        await Frames();
        Input.ParseInputEvent(new InputEventMouseMotion { Position = to, GlobalPosition = to, Relative = to - from - new Vector2(24, 0), ButtonMask = MouseButtonMask.Left });
        await Frames(); Mouse(to, false); await Frames(); Pointer(new Vector2(270, 85)); await Frames();
    }
    private async Task Capture(string name)
    {
        if (DisplayServer.GetName() == "headless") return;
        Call(_panel, "hide_tooltip"); Pointer(new Vector2(270, 85)); await Frames();
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var screenshot = GetViewport().GetTexture().GetImage();
        Check(screenshot.SavePng(_scratch + "/" + name + ".png") == Error.Ok, "实机截图 " + name);
    }
    private void Fits()
    {
        var grid = Find<GridContainer>("CombatSkillSlots");
        var page = (Control)grid.GetParent().GetParent().GetParent();
        foreach (var slot in grid.GetChildren().OfType<Button>())
        {
            var rect = slot.GetGlobalRect(); var bounds = page.GetGlobalRect();
            Check(rect.Position.X >= bounds.Position.X && rect.End.X <= bounds.End.X + 1 && rect.End.Y <= bounds.End.Y + 1, "槽位保持页面内 " + slot.Name);
        }
        var sources = Find<VBoxContainer>("RewriteSourceList");
        Check(sources.GetGlobalRect().Position.X > grid.GetGlobalRect().End.X, "化法侧栏与十槽分列");
        var automatic = Slot(9).GetNode<TextureRect>("AutomaticSkillSlotLock");
        Check(automatic.Size == new Vector2(28, 28) && new Rect2(Vector2.Zero, Slot(9).Size).Encloses(automatic.GetRect()), "第十槽锁标等比显示且不溢出");
    }
    public override void _Ready() => CallDeferred(nameof(Run));
    private async void Run()
    {
        try
        {
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(_scratch));
            GetWindow().ContentScaleSize = new Vector2I(1672, 941); GetWindow().Size = new Vector2I(1672, 941);
            var book = GD.Load<GongfaResource>(Book);
            var data = new HumanData { entity_name = "回合战斗还原验收", cultivation_realm = "云息九层",
                main_gongfa_path = Book, learned_gongfa_paths = [Book], base_physique = 12, base_intelligence = 12, base_soul = 12,
                spirit_root_type = (int)GongfaResource.Wuxing.METAL_WOOD_WATER, spirit_root_name = "三灵根", root_weights = [34, 33, 33, 0, 0],
                action_combat_skill_slots_configured = true, action_combat_skill_slot_paths = [Heavy, Guard, "", "", ""] };
            data.learned_gongfa_chapters[Book] = new Godot.Collections.Array<string>(book.get_sorted_chapters().Select(chapter => chapter.get_id()));
            _owner = new Character { entity_data = data, Visible = false }; AddChild(_owner);
            _owner.set_combat_skill_slot_paths([Attack, "", Heavy]);
            var initial = _owner.get_combat_skill_slot_paths().ToArray(); var historicalAction = data.action_combat_skill_slot_paths.ToArray();
            _panel = GD.Load<PackedScene>("res://tscn/panel/InfoPanel.tscn").Instantiate<InfoPanel>(); AddChild(_panel); _panel.show_entity(_owner);
            Call(_panel, "render_new_page", 2); await Frames();
            Check(Find<GridContainer>("CombatSkillSlots").GetChildCount() == 10 && Find<Label>("CombatSlotCount").Text == "2 / 10", "原十槽及实际装配计数");
            Check(initial.SequenceEqual(_owner.get_combat_skill_slot_paths()), "打开页面保持旧十槽及空位");
            Check(!_panel.FindChildren("*", "Button", true, false).OfType<Button>().Any(button => button.Name.ToString().Contains("Action") || button.Text.Contains("即时演练")), "人物页无即时装配及演练入口");
            Check(Find<GridContainer>("CombatSkillSlots").FindChildren("AutomaticSkillSlotLock", "TextureRect", true, false).Count == 1, "玄枢第十槽提示恢复");
            Check(Library(Guard).Disabled == false, "原应对技能可装配");
            await Click(Slot(7)); await Click(Library(Guard));
            Check(_owner.get_combat_skill_slot_paths()[7] == Guard && _owner.get_combat_skill_slot_paths()[1] == "", "选择指定空槽装配且不挪动其他槽");
            await Drag(Slot(7), Slot(1));
            Check(_owner.get_combat_skill_slot_paths()[1] == Guard && _owner.get_combat_skill_slot_paths()[7] == "", "真实拖拽移至空槽并保留原空位");
            await Drag(Slot(0), Slot(2));
            Check(_owner.get_combat_skill_slot_paths()[0] == Heavy && _owner.get_combat_skill_slot_paths()[2] == Attack, "真实拖拽交换已有技能");
            await Drag(Slot(1), Slot(9));
            Check(_owner.get_combat_skill_slot_paths()[1] == Guard && _owner.get_combat_skill_slot_paths()[9] == "", "玄枢第十槽拒绝应对技能");
            var choice = _owner.get_skill_rewrite_choices().Single(item => item.Available && item.Source.StartsWith("玄枢含章经") && item.Title == "架招化法");
            var rows = (Dictionary<string, Button>)typeof(InfoPanel).GetField("_skillRewriteSourceRows", Private)!.GetValue(_panel)!;
            var source = rows[choice.Id];
            var sourceScroll = Find<ScrollContainer>("RewriteSourceListScroll"); sourceScroll.EnsureControlVisible(source); await Frames();
            await Drag(source, Slot(1), hold: true);
            Check(_owner.get_skill_rewrite_binding(choice) == Guard, "真实长按拖拽恢复化法显式绑定");
            Check(Slot(1).FindChild("SkillRewriteBadge", true, false) is not null, "已绑定技能显示化法标记");
            Check(_owner.get_snapshot_data().combat_resolved_skills.Any(skill => skill is not null && skill.gongfa_rewrite_effect_ids.Contains("xuan_shu_c3_jiazhao")), "绑定进入原战斗快照");
            Fits(); await Capture("十槽与化法_1672");
            await Click(Slot(1));
            Check(_owner.get_combat_skill_slot_paths()[1] == "" && _owner.get_skill_rewrite_binding(choice) == Guard, "卸下只清空原槽且保留化法来源绑定");
            await Click(Slot(1)); await Click(Library(Guard));
            Check(_owner.get_combat_skill_slot_paths()[1] == Guard, "重装恢复原化法目标");
            Check(data.action_combat_skill_slot_paths.SequenceEqual(historicalAction), "历史五槽不覆盖十槽也不被界面写入");
            var savedSlots = _owner.get_combat_skill_slot_paths().ToArray();
            var savePath = _scratch + "/isolated_character.tres";
            Check(ResourceSaver.Save(data, savePath) == Error.Ok, "隔离角色资源保存");
            var loaded = ResourceLoader.Load<HumanData>(savePath, cacheMode: ResourceLoader.CacheMode.Ignore);
            Check(loaded.combat_skill_references.Select(reference => reference.Key).SequenceEqual(savedSlots) && loaded.skill_rewrite_bindings[choice.Id] == Guard, "十槽空位与化法跨存读保留");
            var heavy = _owner.resolve_combat_skill(Heavy)!; var attack = _owner.resolve_combat_skill(Attack)!;
            Check(heavy.energy_cost == 2 && heavy.mana_cost == 0 && heavy.target_type == 2 && heavy.base_damage == 11, "痛击保持原回合费用目标与数值");
            Check(attack.energy_cost == 1 && attack.mana_cost == 0, "应敌恢复原资源身份");
            GetWindow().Size = new Vector2I(1280, 720); await Frames(); Fits(); await Capture("十槽与化法_1280");
            var graphTab = _panel.FindChildren("*", "Button", true, false).OfType<Button>().Single(button => button.Text == "造诣图谱");
            await Click(graphTab); Check(_panel.FindChild("SkillNodeNetwork", true, false) is Control { Visible: true }, "造诣图谱仍可访问");
            var loadoutTab = _panel.FindChildren("*", "Button", true, false).OfType<Button>().Single(button => button.Text == "战斗配置");
            await Click(loadoutTab); Check(Find<GridContainer>("CombatSkillSlots").GetChildCount() == 10, "从图谱返回仍为十槽");
            _panel.hide_panel(); _panel.QueueFree(); await Frames();
            var menu = GD.Load<PackedScene>("res://tscn/scene/StartMenu.tscn").Instantiate<StartMenu>(); AddChild(menu); await Frames();
            menu.GetNode<Button>("StartMenuMenu/DemoToggle").EmitSignal(BaseButton.SignalName.Pressed); await Frames();
            var entries = menu.FindChildren("*", "Button", true, false).OfType<Button>().ToArray();
            Check(entries.Any(button => button.Text == "自定义战斗 · 秘籍与队列" && button.IsVisibleInTree()), "首页保留原队列战斗测试入口");
            Check(!entries.Any(button => button.Name.ToString() is "ActionManualBattle" or "ActionHumanSparring" or "ActionBattleOriginalWanderer" or "BloodWaveNpcTrial" or "TopDownBattleTrial"), "首页撤下所有即时战斗试玩入口");
            Check(!GetNode<GameSaveManager>("/root/GameSaveManager").has_active_slot, "检查未使用正式旅程存档");
            GD.Print($"QUEUE_COMBAT_RESTORATION_OK {_checks}"); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError("QUEUE_COMBAT_RESTORATION_FAIL " + error); GetTree().Quit(1); }
    }
}
