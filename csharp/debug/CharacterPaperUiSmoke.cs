using Godot;
using System.Reflection;
using System.Text.Json;

/// <summary>真实InfoPanel＋隔离内存人物；不载入旅程、不保存人物或修改正式资源。</summary>
public partial class CharacterPaperUiSmoke : Node
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private string Output => OS.GetCmdlineUserArgs().FirstOrDefault(arg=>arg.StartsWith("--character-output=")) is { } value
        ? value["--character-output=".Length..].TrimEnd('/')+"/" : "res://docs/美术规范/角色纸墨木石实装_20261108/";
    private readonly List<object> _checks = [];
    private readonly List<string> _captures = [];
    private InfoPanel _panel = null!;
    private Character _actor = null!;
    private int _width;
    private bool _headless;
    private bool _hasFailedCheck;
    private string _failure = "";
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, Hidden)!.GetValue(owner)!;
    private static object? Call(object owner, string name, params object[] args) => owner.GetType().GetMethod(name, Hidden)!.Invoke(owner, args);
    private static IEnumerable<T> All<T>(Node node) where T : Node
    {
        if (node is T item) yield return item;
        foreach (var child in node.GetChildren()) foreach (var descendant in All<T>(child)) yield return descendant;
    }
    private void Check(bool passed, string name, string detail = "")
    {
        _checks.Add(new { name, passed, detail });
        if (!passed)
        {
            _hasFailedCheck = true;
            _failure += (string.IsNullOrEmpty(_failure) ? "" : "\n") + name + ": " + detail;
            GD.PrintErr("CHARACTER_PAPER_UI_CHECK_FAIL " + name + ": " + detail);
        }
        else GD.Print("CHARACTER_PAPER_UI_OK " + name);
    }
    private async Task Frames(int count = 3)
    {
        for (int index = 0; index < count; index++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
    private async Task Settle() { await ToSignal(GetTree().CreateTimer(.28), SceneTreeTimer.SignalName.Timeout); await Frames(); }
    private void MovePointer(Vector2 point) => GetViewport().PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point }, true);
    private void PointerButton(Vector2 point, bool pressed) => GetViewport().PushInput(new InputEventMouseButton
        { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = pressed }, true);
    private async Task Click(BaseButton button)
    {
        for (Node? ancestor = button.GetParent(); ancestor is not null; ancestor = ancestor.GetParent())
            if (ancestor is ScrollContainer scroll)
            {
                scroll.EnsureControlVisible(button);
                await Frames();
            }
        var usable = button.IsVisibleInTree() && !button.Disabled;
        Check(usable, "实际可操作控件 " + button.Name);
        if (!usable) return;
        if (_headless) button.EmitSignal(BaseButton.SignalName.Pressed);
        else
        {
            var point = button.GetGlobalTransformWithCanvas() * (button.Size * .5f);
            MovePointer(point); await Frames(1); PointerButton(point, true); await Frames(1); PointerButton(point, false);
        }
        await Frames();
    }
    private static Rect2 Bounds(Control control)
    {
        var transform = control.GetGlobalTransformWithCanvas();
        var box = new Rect2(transform * Vector2.Zero, Vector2.Zero);
        foreach (var point in new[] { new Vector2(control.Size.X, 0), control.Size, new Vector2(0, control.Size.Y) }) box = box.Expand(transform * point);
        return box;
    }
    private async Task Capture(string name, bool preservePointer = false)
    {
        if (_headless) return;
        HoverInfoManager.HideAll(); Call(_panel, "hide_tooltip");
        if (!preservePointer) MovePointer(new Vector2(3, 3));
        await Frames(); await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage();
        Check(image.GetWidth() == _width && image.GetHeight() == _width * 9 / 16, "完整图尺寸 " + name);
        var file = $"角色_{name}_{_width}.png";
        Check(image.SavePng(Output + file) == Error.Ok, "完整图保存 " + name); _captures.Add(file);
    }

    private async Task CheckPaperStates(Button button)
    {
        var kind = (CraftingPaperStyle.ButtonKind)button.GetMeta("paper_feedback_kind").AsInt32();
        Check(kind == CraftingPaperStyle.ButtonKind.Regular, "普通筛选使用Regular整纸形制");
        Check(button.GetMeta("paper_feedback_selection").AsBool(), "实际点击后普通筛选保留选中态");
        var normal = (StyleBoxTexture)button.GetThemeStylebox("normal");
        var source = PaperButtonFeedback.Face(kind);
        Check(normal.Texture == source.Texture && normal.ModulateColor.IsEqualApprox(source.ModulateColor * new Color(CraftingPaperStyle.SelectedDarkening, CraftingPaperStyle.SelectedDarkening, CraftingPaperStyle.SelectedDarkening)), "普通筛选整纸压暗且保留原暖色");
        foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled" })
            Check(button.GetThemeStylebox(state) is StyleBoxTexture, "按钮生产纸面状态 " + state);
        foreach (var state in new[] { "font_color", "font_hover_color", "font_pressed_color" })
            Check(button.GetThemeColor(state).IsEqualApprox(CraftingPaperStyle.Ink), "文字持续使用深墨 " + state);
        await Capture("按钮_选中");
        if (_headless) return;
        var point = Bounds(button).GetCenter(); MovePointer(point); await Frames(); await Capture("按钮_悬停", true);
        PointerButton(point, true); await Frames();
        var pressed = (StyleBoxTexture)button.GetThemeStylebox("pressed");
        Check(button.GetMeta("paper_feedback_held").AsBool() && pressed.ExpandMarginTop == -1 && pressed.ExpandMarginBottom == 1, "真实鼠标按住整纸下沉1单位");
        await Capture("按钮_按住", true);
        PointerButton(point, false); await Frames();
        Check(!button.GetMeta("paper_feedback_held").AsBool() && button.GetMeta("paper_feedback_selection").AsBool(), "松开恢复选中纸面并清除瞬时按住态");
        button.Disabled = true; MovePointer(new Vector2(3, 3)); await Frames(); await Capture("按钮_禁用", true);
        button.Disabled = false; await Frames();
    }
    public string get_profile_location_for(Character actor) => ReferenceEquals(actor, _actor) ? "观复峰居所" : "位置未详";

    public override async void _Ready()
    {
        bool passed = false;
        try
        {
            var userDirectory = System.IO.Path.GetFullPath(ProjectSettings.GlobalizePath("user://"));
            var project = System.IO.Path.GetFullPath(ProjectSettings.GlobalizePath("res://"));
            bool Within(string parent) => userDirectory.StartsWith(System.IO.Path.GetFullPath(parent).TrimEnd('\\', '/') + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
            var isolated = Within(System.IO.Path.Combine(project, "tmp")) || Within(System.IO.Path.Combine(project, ".godot", "test-results")) || Within(System.IO.Path.Combine(project, "output"));
            Check(isolated, "APPDATA使用项目内隔离目录", userDirectory);
            if (!isolated) throw new InvalidOperationException("Refusing to inspect UI against the player's actual APPDATA directory.");
            _width = OS.GetCmdlineUserArgs().Contains("--wide") || OS.GetCmdlineUserArgs().Contains("--paper-width=1920") ? 1920 : 1280;
            _headless = DisplayServer.GetName().Equals("headless", StringComparison.OrdinalIgnoreCase);
            System.IO.Directory.CreateDirectory(ProjectSettings.GlobalizePath(Output));
            GetWindow().ContentScaleSize = Vector2I.Zero; GetWindow().Size = new Vector2I(_width, _width * 9 / 16);
            GetWindow().Mode = Window.ModeEnum.Windowed; GetWindow().Borderless = true; GetWindow().Unfocusable = true;
            var clock = GetNode<GameTimeMgr>("/root/GameTimeManager"); clock.pause_time();
            _actor = new Character(); AddChild(_actor); _actor.Hide();
            _actor.load_from_data(new HumanData
            {
                entity_name = "角色纸墨验收", cultivation_realm = "云息三层", current_age = 20, appearance_seed = 121,
                spirit_root_type = (int)GongfaResource.Wuxing.METAL_WOOD_WATER, spirit_root_name = "三灵根", root_weights = [34, 33, 33, 0, 0],
                base_physique = 8, base_intelligence = 8, base_soul = 8, base_body_proficiency = 6, base_magic_proficiency = 8, base_sword_proficiency = 6,
                current_world_stamina = 100, sect_affiliation_id = "yuxu_xianzong", sect_current_rank = "弟子", karma_value = 3,
                spirit_strands = 7
            });
            var intro = GD.Load<GongfaResource>(GongfaAvailability.DefaultBookPath);
            var introGranted = _actor.grant_intro_gongfa(intro);
            Check(introGranted, "现有待研读秘籍作为真实人物目录夹具");
            if (!introGranted) throw new InvalidOperationException("Current enabled manual fixture could not be granted.");
            _panel = GD.Load<PackedScene>("res://tscn/panel/InfoPanel.tscn").Instantiate<InfoPanel>(); AddChild(_panel);
            _panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); _panel.show_entity(_actor); await Settle();
            await Capture("概览_首次");
            var canvas = Field<Control>(_panel, "_characterCanvas"); var tabs = Field<List<Button>>(_panel, "_characterTabs");
            var bindings = Field<List<Panel>>(_panel, "_paperNavBindings");
            Check(tabs.Select(button => button.Text).SequenceEqual(new[] { "概览", "属性", "战斗", "功法", "造诣", "装备", "关系" }), "正式角色七页顺序与造诣独立入口");
            var wood = (StyleBoxTexture)canvas.GetNode<Panel>("CharacterPaperShell").GetThemeStylebox("panel");
            Check(wood.Texture is AtlasTexture { Region: var region } && region == new Rect2(110, 285, 850, 140)
                && wood.TextureMarginLeft == 0 && wood.TextureMarginRight == 0 && wood.TextureMarginTop == 0 && wood.TextureMarginBottom == 0, "原木纹满铺且无最外层装饰框");
            Check(Bounds(canvas).Size.IsEqualApprox(GetViewport().GetVisibleRect().Size) && canvas.Position.IsEqualApprox(Vector2.Zero), "角色画布跟随实际窗口铺满");
            var header = new Control[] { canvas.GetNode<PanelContainer>("CharacterPagePlaque"), Field<Label>(_panel, "_characterName"), Field<Label>(_panel, "_characterRealm"), Field<Label>(_panel, "_characterRoot"), canvas.GetNode<Button>("CharacterInventory"), canvas.GetNode<Button>("CharacterReturn") };
            Check(header.All(item => Math.Abs(item.Position.Y - 12) < .1 && Math.Abs(item.Size.Y - 46) < .1), "页眉题签姓名境界灵根行囊返回同一行46高");
            Check(new[] { Field<Label>(_panel, "_characterTitle"), Field<Label>(_panel, "_characterName"), Field<Label>(_panel, "_characterRealm"), Field<Label>(_panel, "_characterRoot") }.All(label => label.GetThemeFontSize("font_size") == 22 && label.VerticalAlignment == VerticalAlignment.Center), "页眉字级22且垂直居中");
            Check(canvas.GetNode<Button>("CharacterInventory").Position.X + 114 + 14 == canvas.GetNode<Button>("CharacterReturn").Position.X, "行囊紧靠固定右上返回左侧");
            Check(_panel.current_card == _actor, "正式面板绑定当前真实人物");
            var facts = Field<Dictionary<string, Label>>(_panel, "_overviewValueLabels");
            Check(facts["faction"].Text == "玉虚仙宗" && facts["realm"].Text == _actor.cultivation_realm && facts["spirit_root"].Text == _actor.spirit_root_name, "画像下基本资料读取运行人物");
            Check(Field<Label>(_panel, "_overviewName").Text == _actor.entity_name && Field<Label>(_panel, "_characterName").Visible == false, "概览姓名只在画像下出现");
            Check(!facts.ContainsKey("location") && !All<Label>(canvas).Any(label => label.Text == "所在"), "人物概览不显示所在行");
            Check(Field<Label>(_panel, "_overviewKarmaValue").Text.Contains("+3") && Field<Label>(_panel, "_overviewRenownValue").Text.Contains("百川"), "业力和声望保留实际读数");
            var statusPanel = Field<PanelContainer>(_panel, "_overviewWorldStatusSection");
            Check(statusPanel.Size.X >= 900 && statusPanel.Size.Y >= 370, "局外状态保留宽阔区域");
            Check(All<Label>(statusPanel).Any(label => label.Text == "暂无特殊状态") && !All<Label>(statusPanel).Any(label => label.Text.StartsWith("生命 ")), "当前状态使用局外记录而非生命资源");
            Check(Field<Label>(_panel, "_overviewMoodSummary").Text.Contains("未详"), "未绑定八维心境不冒充平稳");
            var moodHelp = All<Button>(canvas).First(button => button.Name == "MoodDetails");
            Check(moodHelp.IconAlignment == HorizontalAlignment.Center && moodHelp.VerticalIconAlignment == VerticalAlignment.Center,
                "心境问号图标在按钮纸面水平垂直居中");
            var separators = All<HSeparator>(canvas).ToList();
            Check(separators.Count > 0 && separators.All(separator => separator.GetThemeStylebox("separator") is StyleBoxLine { Thickness: 1, Vertical: false } or StyleBoxFlat),
                "新版角色分割线使用普通水平直线，无中央花纹贴图");
            var stamp = (_actor.current_world_stamina, _actor.current_health, _actor.spirit_strands, clock.get_total_game_hours());
            string[] names = ["概览", "属性", "战斗", "功法", "造诣", "装备", "关系"];
            for (int index = 0; index < names.Length; index++)
            {
                await Click(tabs[index]); await Settle();
                var pages = Field<Dictionary<int, Control>>(_panel, "_characterPages");
                Check(pages.Count(pair => pair.Value.Visible) == 1 && pages[index].Visible, "唯一活动分页 " + names[index]);
                Check(Math.Abs(tabs[index].Position.X - 18) < .1 && bindings[index].Modulate.A > .99f, "选中纸签右移并露装订边 " + names[index]);
                Check(tabs.Where((_, other) => other != index).All(button => Math.Abs(button.Position.X) < .1) && bindings.Where((_, other) => other != index).All(binding => binding.Modulate.A < .01f), "未选纸签收回装订边隐藏 " + names[index]);
                Check(tabs[index].GetThemeStylebox("normal") is StyleBoxTexture, "导航专用PNG材质 " + names[index]);
                var pageBounds = Bounds(pages[index]); var viewport = GetViewport().GetVisibleRect();
                Check(viewport.Encloses(pageBounds), "正文保留在两档窗口内 " + names[index], pageBounds.ToString());
                Check(Math.Abs(pages[index].Position.X - 232) < .1 && Math.Abs(pages[index].Position.Y - 76) < .1 && Math.Abs(pages[index].Size.X - (canvas.Size.X - 248)) < 1 && Math.Abs(pages[index].Size.Y - 845) < 1, "正文使用两侧余白与扩大高度 " + names[index], $"position={pages[index].Position}; size={pages[index].Size}; canvas={canvas.Size}; native containers round minimum width to whole design pixels");
                if (index == 1)
                    Check(All<Label>(pages[index]).Any(label => label.Text == $"{_actor.spirit_strands} 缕" && label.GetThemeColor("font_color").IsEqualApprox(new Color("5d9da8"))), "灵力数值与单位为淡青色");
                if (index == 3) Check(Field<VBoxContainer>(_panel, "_manualList").GetChildCount() > 0, "真实待研读秘籍仍可从功法库查看");
                if (index == 2) Check(All<CharacterCombatBoardPage>(pages[index]).Single().Actor==_actor && !All<Button>(pages[index]).Any(button => button.Text == "造诣图谱") && !All<BattleBoardView>(pages[index]).Any(), "战斗展示本人已学持有目录，图谱仍独立且首页不放配置盘");
                if (index == 4) Check(All<Control>(pages[index]).Any(control => control.Name == "SkillNodeNetwork") && All<TextureButton>(pages[index]).Any(), "造诣页展示真实人物图谱节点");
                await Capture(names[index]);
            }
            Check(stamp == (_actor.current_world_stamina, _actor.current_health, _actor.spirit_strands, clock.get_total_game_hours()), "只查看七页不扣资源或推进游戏时间");
            await Click(tabs[3]); await Settle();
            var gongfa = Field<Dictionary<int, Control>>(_panel, "_characterPages")[3];
            await Click(All<Button>(gongfa).First(button => button.Text == "已学"));
            Check(Field<VBoxContainer>(_panel, "_manualList").GetChildCount() > 0 && All<Label>(Field<VBoxContainer>(_panel, "_manualList")).Any(label => label.Text.Contains("没有符合条件")), "秘籍筛选读取未研读的真实状态");
            await Click(All<Button>(gongfa).First(button => button.Text == "全部"));
            await CheckPaperStates(All<Button>(gongfa).First(button => button.Text == "全部"));
            var search = All<LineEdit>(gongfa).First(); search.Text = intro.gongfa_name; await Frames();
            Check(All<Label>(Field<VBoxContainer>(_panel, "_manualList")).Any(label => label.Text == intro.gongfa_name), "秘籍检索保留真实书目");
            search.Text = ""; await Frames();
            await Click(All<Button>(Field<HBoxContainer>(_panel, "_manualSlots")).First());
            Check(Field<Control>(_panel, "_gongfaModal").Visible, "原主修秘籍选择入口保持"); _panel.dismiss_top_overlay();

            await Click(tabs[4]); await Settle();
            var proficiency = Field<Dictionary<int, Control>>(_panel, "_characterPages")[4];
            var faces = _actor.get_skill_node_face_configs(); var nodes = _actor.get_skill_node_configs();
            Check(All<Button>(proficiency).Count(button => button.Name.ToString().StartsWith("SkillNodeFace_")) == faces.Count, "独立造诣页读取真实全部脉面");
            var zoom = Field<float>(_panel, "_skillNodeZoom");
            await Click(All<Button>(proficiency).First(button => button.Name == "SkillNodeZoomIn"));
            Check(Field<float>(_panel, "_skillNodeZoom") > zoom, "独立图谱真实放大按钮");
            await Click(All<Button>(proficiency).First(button => button.Name == "SkillNodeZoomReset"));
            Check(Math.Abs(Field<float>(_panel, "_skillNodeZoom") - 1) < .001, "独立图谱真实复位按钮");
            foreach (var node in nodes)
            {
                await Click(All<Button>(proficiency).First(button => button.Name == "SkillNodeFace_" + node.face_id)); await Frames();
                await Click(All<TextureButton>(proficiency).First(button => button.Name == "SkillNode_" + node.node_id)); await Frames();
                Check(Field<int>(_panel, "_activeNewPage") == 4 && Field<string>(_panel, "_selectedSkillNodeId") == node.node_id, "图谱选节点仍留独立造诣页 " + node.node_name);
                Check(_actor.can_unlock_skill_node(node.node_id, out _), "图谱原资格检查 " + node.node_name);
                await Click(All<Button>(proficiency).First(button => button.Name == "SkillNodeLearn")); await Frames();
                Check(_actor.has_unlocked_skill_node(node.node_id) && _actor.get_available_battle_skills().Any(skill => node.get_skill_paths().Contains(Character.combat_skill_key(skill))), "真实参悟与技能解锁回调 " + node.node_name);
                Check(All<Button>(proficiency).First(button => button.Name == "SkillNodeLearn").Disabled && Field<int>(_panel, "_activeNewPage") == 4, "参悟后保留本页并禁止重复学习 " + node.node_name);
            }
            await Capture("造诣_参悟");
            await Click(tabs[2]); await Settle();
            var path = _actor.get_default_combat_skill_paths().First();
            var combat = Field<Dictionary<int, Control>>(_panel, "_characterPages")[2];
            var priorSlots = _actor.get_combat_skill_slot_paths().ToArray();
            await Click(All<Button>(combat).Single(button=>button.Name=="OpenCharacterPreparation"));
            Check(All<CharacterCombatBoardEditor>(_panel).Any(), "正式目录配招入口打开全屏原生编辑器");
            await Click(All<Button>(_panel).Single(button=>button.Name=="CloseCharacterPreparation"));
            Check(!All<CharacterCombatBoardEditor>(_panel).Any()&&_actor.get_combat_skill_slot_paths().SequenceEqual(priorSlots), "返回配招不改原人物技能引用");

            await Click(tabs[5]); await Settle();
            var equipment = Field<Dictionary<int, Control>>(_panel, "_characterPages")[5];
            Check(All<Button>(equipment).Count(button => button.Name.ToString().StartsWith("EquipmentSlot")) == 4 && All<Button>(equipment).Any(button => button.Name == "EquipmentClothingSlot"), "现行四装备位及独立衣着入口保持");
            await Click(All<Button>(equipment).First(button => button.Name == "EquipmentSlot0"));
            Check(Field<PanelContainer>(_panel, "_readerPanel").Visible, "无可用装备仍打开原真实阅读反馈"); _panel.dismiss_top_overlay();
            const string equipmentPath = "res://resources/entities/equipment/铁剑.tres";
            Check(_actor.add_item_to_inventory_silently(equipmentPath), "复用现有装备资源作为隔离库存夹具");
            await Click(All<Button>(equipment).First(button => button.Name == "EquipmentSlot0"));
            var equipmentName = GD.Load<EquipmentData>(equipmentPath).entity_name;
            await Click(All<Button>(Field<VBoxContainer>(_panel, "_gongfaModalChoices")).First(button => button.Text == equipmentName));
            Check(((HumanData)_actor.entity_data!).weapon_path == equipmentPath, "装备选择仍写入原人物真实装备字段");
            await Click(All<Button>(equipment).First(button => button.Name == "EquipmentSlot0"));
            await Click(All<Button>(Field<VBoxContainer>(_panel, "_gongfaModalChoices")).First(button => button.Text == "卸下当前装备"));
            Check(((HumanData)_actor.entity_data!).weapon_path.Length == 0, "装备卸下仍走原人物真实回调");
            var wardrobe = Field<WardrobePicker>(_panel, "_wardrobe");
            var clothingBefore = ((HumanData)_actor.entity_data!).portrait_clothing_item_path;
            await Click(All<Button>(equipment).First(button => button.Name == "EquipmentClothingSlot"));
            Check(wardrobe.Visible, "衣着入口仍打开原真实试穿草稿");
            await Capture("衣着_原生弹层");
            var cancelClothing = All<Button>(wardrobe).First(button => button.Name == "CancelClothing");
            Check(GetViewport().GetVisibleRect().Encloses(Bounds(cancelClothing)), "衣着取消按钮在两档窗口内");
            await Click(cancelClothing);
            Check(!wardrobe.Visible && ((HumanData)_actor.entity_data!).portrait_clothing_item_path == clothingBefore, "衣着取消不修改原穿戴字段");
            await Click(tabs[0]); await Settle();
            await Click(All<Button>(canvas).First(button => button.Name == "MoodDetails"));
            Check(Field<Control>(_panel, "_moodDetailsOverlay").Visible, "真实心境说明入口可打开"); Check(_panel.dismiss_top_overlay(), "心境说明沿原返回优先级收起");
            await Click(All<Button>(canvas).First(button => button.Name == "ViewPortrait"));
            Check(Field<Control>(_panel, "_gongfaModal").Visible && All<PortraitRenderer>(Field<VBoxContainer>(_panel, "_gongfaModalChoices")).Any(), "查看立绘使用真实人物渲染器"); _panel.dismiss_top_overlay();
            var data = (HumanData)_actor.entity_data!;
            var record = new WorldStatusRecord
            {
                status_id = WorldStatusRules.SevereInjuryId, display_name = "重伤",
                description = "参加正式战斗时生命上限暂降 15%；需通过实际休息行动调养恢复。",
                source_name = "隔离界面验收", recovery_hours_required = WorldStatusRules.severe_injury_recovery_hours_for_realm(_actor.cultivation_realm),
                recovery_realm = _actor.cultivation_realm, expires_at_total_game_hours = WorldStatusRules.PermanentExpiry
            };
            Check(WorldStatusRules.add(data.world_status_records, record, clock.get_total_game_hours()), "复用已定义重伤记录作隔离状态夹具");
            _panel.refresh_player_world_status_details(_actor, clock.get_total_game_hours()); await Frames();
            Check(All<Label>(statusPanel).Any(label => label.Text == "重伤") && All<Label>(statusPanel).Any(label => label.Text.Contains(record.description)), "状态区读取实际效果和恢复记录");
            await Capture("局外状态");
            var npc = new WorldNpcData { npc_id = "character_paper_memory_npc", npc_name = "界面验收访客", faction_id = "yuxu_xianzong", current_node_id = "yuxu_guanfufeng_residence", current_age = 24 };
            int talks = 0, trades = 0;
            _panel.NpcTalkRequested += target => { if (ReferenceEquals(target, npc)) talks++; };
            _panel.NpcTradeRequested += target => { if (ReferenceEquals(target, npc)) trades++; };
            _panel.show_npc(npc); await Settle();
            Check(tabs[0].Visible && tabs.Skip(1).All(button => !button.Visible) && !canvas.GetNode<Button>("CharacterInventory").Visible, "未探明NPC只开放只读概览并隐藏行囊");
            var npcPage = Field<VBoxContainer>(_panel, "_npc_page"); var npcScroll = npcPage.GetParent() as ScrollContainer;
            if (npcScroll is not null) { npcScroll.EnsureControlVisible(All<Button>(npcPage).First()); await Frames(); }
            await Click(All<Button>(npcPage).First(button => button.Text == "交谈"));
            await Click(All<Button>(npcPage).First(button => button.Text == "交易"));
            Check(talks == 1 && trades == 1 && _panel.current_card is null, "NPC交谈交易继续发出原只读人物事件");
            await Capture("NPC_只读概览");
            NpcProbeKnowledge.Capture(npc, "character_paper_memory_probe", "1年1月1日", 0);
            _panel.show_npc(npc); await Settle(); await Click(tabs[1]); await Settle();
            Check(Field<ScrollContainer>(_panel, "_npcProbePage").Visible && tabs[1].Visible && tabs.Skip(2).All(button => !button.Visible), "已探明NPC仅增加已有属性快照入口");
            Check(Field<ScrollContainer>(_panel, "_npcProbePage").GetThemeStylebox("panel") is StyleBoxTexture { ModulateColor.R: > .8f }, "NPC属性快照使用浅纸承托，深墨文字不落在木底");
            Check(All<Label>(Field<ScrollContainer>(_panel, "_npcProbePage")).Any(label => label.Text == "探查所得"), "NPC属性继续使用原记录而非实时私有资料");
            await Capture("NPC_属性快照");
            _panel.show_entity(_actor); await Settle();
            Check(tabs.All(button => button.Visible) && canvas.GetNode<Button>("CharacterInventory").Visible, "切回主角恢复七页和行囊入口");
            var storage = GD.Load<PackedScene>("res://tscn/panel/StoragePanel.tscn").Instantiate<StoragePanel>(); AddChild(storage); storage.Hide();
            await Click(canvas.GetNode<Button>("CharacterInventory"));
            Check(storage.Visible && !_panel.Visible && ReferenceEquals(Field<Character>(storage, "_player"), _actor), "行囊入口绑定原人物并打开正式行囊");
            storage.hide_panel(); _panel.show_entity(_actor); await Frames();
            await Click(canvas.GetNode<Button>("CharacterReturn"));
            Check(!_panel.Visible && _panel.current_card is null && clock.get_total_game_hours() == stamp.Item4, "统一返回保留原收起解绑且不推进时间");
            passed = !_hasFailedCheck;
        }
        catch (Exception error) { _failure = error.ToString(); GD.PushError("CHARACTER_PAPER_UI_FAIL " + _failure); }
        finally
        {
            var report = new { status = passed ? "passed" : "failed", width = _width, height = _width * 9 / 16, headless = _headless, nativeGpuSubViewport = GetViewport() is SubViewport, checks = _checks, captures = _captures, error = _failure, actualGameSavesReadOrWritten = false };
            var path = ProjectSettings.GlobalizePath(Output + $"验证记录_{_width}.json");
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            System.IO.File.WriteAllText(path, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            GD.Print($"CHARACTER_PAPER_UI_{(passed ? "PASS" : "FAIL")} width={_width} checks={_checks.Count} captures={_captures.Count}");
            GetTree().Quit(passed ? 0 : 1);
        }
    }
}
