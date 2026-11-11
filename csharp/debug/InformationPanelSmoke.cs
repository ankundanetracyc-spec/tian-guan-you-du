using Godot;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

/// <summary>Isolated UI interaction checks; never reads or writes a real journey.</summary>
public partial class InformationPanelSmoke : Node
{
    private const string Evidence = "res://docs/美术规范/悬停公式与长段排版统一_20261109/信息栏/";
    private int _checks;
    private void Check(bool ok, string message)
    {
        _checks++;
        if (!ok) throw new InvalidOperationException(message);
    }
    private static object? Call(object target, string method, params object[] args) =>
        target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(target, args);
    private Vector2 _testPointer;
    private async Task Settle() { for (int i = 0; i < 16; i++) { await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); Move(_testPointer); } }
    private static Vector2 Center(Control control) => control.GetGlobalTransformWithCanvas() * (control.Size / 2);
    private void Move(Vector2 position)
    {
        _testPointer = position;
        GetViewport().PushInput(new InputEventMouseMotion { Position = position, GlobalPosition = position }, true);
    }
    private void Click(Control control) => Click(Center(control));
    private void Click(Vector2 position)
    {
        Move(position);
        GetViewport().PushInput(new InputEventMouseButton { Position = position, GlobalPosition = position, ButtonIndex = MouseButton.Left, Pressed = true }, true);
        GetViewport().PushInput(new InputEventMouseButton { Position = position, GlobalPosition = position, ButtonIndex = MouseButton.Left, Pressed = false }, true);
    }
    private async Task Capture(string name, Control? focus = null)
    {
        if (DisplayServer.GetName() == "headless") return;
        Move(_testPointer);
        if (OS.GetCmdlineUserArgs().Contains("--vertical-item-actions")) name = "20261026_" + name;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage();
        if (focus is not null)
        {
            using var cardImage = image.GetRegion(new Rect2I((Vector2I)focus.GetGlobalRect().Position, (Vector2I)focus.Size));
            Check(cardImage.SavePng(Evidence + name + ".png") == Error.Ok, "悬停区域截图保存 " + name);
        }
        else Check(image.SavePng(Evidence + name + ".png") == Error.Ok, "截图保存 " + name);
    }
    private void Fits(Control control)
    {
        var rect = control.GetGlobalRect();
        var viewport = GetViewport().GetVisibleRect();
        Check(rect.Position.X >= 0 && rect.Position.Y >= 0 && rect.End.X <= viewport.End.X + 1 && rect.End.Y <= viewport.End.Y + 1, "信息栏完整位于画布内");
    }
    public override void _Ready() => CallDeferred(nameof(Run));
    private async void Run()
    {
        try
        {
            PlayerInfoText.SetMode(PlayerInfoText.InformationMode.Simple, persist: false);
            GetWindow().ContentScaleSize = new Vector2I(1672, 941);
            GetWindow().Size = new Vector2I(1672, 941);
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(Evidence));
            var data = new HumanData { unique_id = "information-panel-isolated", max_health = 100, current_health = 35 };
            var owner = new Character { entity_data = data }; AddChild(owner);
            int auditedSkills = 0;
            foreach (var file in System.IO.Directory.EnumerateFiles(ProjectSettings.GlobalizePath("res://resources/activeskills"), "*.tres", System.IO.SearchOption.AllDirectories))
            {
                var resourcePath = "res://" + System.IO.Path.GetRelativePath(ProjectSettings.GlobalizePath("res://"), file).Replace('\\', '/');
                if (GD.Load<Resource>(resourcePath) is not ActiveSkillData auditSkill) continue;
                var full = CardPresentation.CombatEffectText(auditSkill, owner);
                var summary = PlayerInfoText.Summary(full);
                Check(!System.Text.RegularExpressions.Regex.IsMatch(summary, @"计算公式|计算方式|\b(?:max|min|round|floor|ceil|clamp)\s*\("), "技能资源默认说明无公式 " + resourcePath);
                var result = System.Text.RegularExpressions.Regex.Match(full, @"当前预估造成\s+\d+(?:\.\d+)?");
                if (result.Success) Check(summary.Contains(result.Value.Replace("预估", "")), "最终效果数值保留 " + resourcePath);
                auditedSkills++;
            }
            GD.Print("INFORMATION_SKILL_RESOURCES_AUDITED " + auditedSkills);
            const string materialPath = "res://resources/entities/items/苦苔根.tres";
            const string swordPath = "res://resources/entities/equipment/铁剑.tres";
            const string pillPath = "res://resources/entities/items/alchemy/huichun_1.tres";
            Check(owner.add_item_to_inventory_silently(materialPath), "材料入测试行囊");
            Check(owner.add_item_to_inventory_silently(swordPath), "装备入测试行囊");
            Check(owner.add_item_to_inventory_silently(pillPath), "丹药入测试行囊");
            var storage = GD.Load<PackedScene>("res://tscn/panel/StoragePanel.tscn").Instantiate<StoragePanel>();
            AddChild(storage); storage.bind_player(owner); storage.show_panel();
            await Settle();
            StorageItemSlot Slot(string path) => storage.FindChildren("*", "Control", true, false).OfType<StorageItemSlot>().First(s => s.item_path == path);
            var popup = storage.FindChild("ItemPopup", true, false) as Control ?? throw new Exception("无菜单节点");
            var hover = GetNode<HoverInfoManager>("/root/HoverInfoManager").GetNode<Control>("SharedHoverInfoCard");
            var material = Slot(materialPath);
            var materialInfo = (HoverInfoManager.HoverInfoContent)Call(storage, "build_hover_content", material)!;
            Check(materialInfo.Sections is { Count: > 0 } && materialInfo.Body.Contains("\n五行：") && materialInfo.Body.Contains("\n质地：") && materialInfo.Body.Contains("\n相性：") && materialInfo.Body.Contains("\n功用："), "材料字段各占一行");
            Check(materialInfo.Subtitle == "" && materialInfo.Body.Contains("简介：") && !materialInfo.Body.Contains("材性与功用"), "材料无分类副标题并保留简介位置");
            var hideItem = GD.Load<ItemData>("res://resources/entities/items/兽皮.tres");
            var hideInfo = StoragePanel.ItemHover(hideItem, 99);
            Check(hideInfo == StoragePanel.ItemHover(hideItem, 1) || hideInfo.Body == StoragePanel.ItemHover(hideItem, 1).Body, "材料信息不随持有数量变化");
            Check(PlayerInfoText.Summary(hideInfo.Body).Contains("品阶：9品上") && hideInfo.Body.Contains("固·九品、护·九品") && hideInfo.Body.TrimEnd().EndsWith("简介："), "兽皮保留真实品阶和功用品级及空简介");
            Check(!PlayerInfoText.Summary(hideInfo.Body).Contains("×") && PlayerInfoText.Detailed(hideInfo.Body).Contains("材评：2.5 = 1×2＋1×0.5"), "材料两档只改变末尾品阶计算来源");
            Move(Center(material)); HoverInfoManager.SetInfo(material, materialInfo, true); await Settle();
            Check(hover.Visible && !popup.Visible, "悬停有信息，无操作菜单"); Fits(hover);
            Check(hover.FindChildren("*", "ScrollContainer", true, false).OfType<ScrollContainer>().All(s => s.VerticalScrollMode == ScrollContainer.ScrollMode.Disabled), "详情无内部滚动条");
            await Capture("01_材料悬停");
            Click(material); await Settle(); Check(!popup.Visible, "无操作材料单击不弹窗");
            var sword = Slot(swordPath); Click(sword); await Settle();
            var action = popup.FindChildren("*", "Button", true, false).OfType<Button>().First(b => b.Text != "取消");
            Check(popup.Visible && action.Text == "装备" && !hover.Visible, "装备单击只有操作");
            var cancel = popup.FindChildren("*", "Button", true, false).OfType<Button>().Single(b => b.Text == "取消");
            Check(action is PaperButton && cancel is PaperButton && action.GetParent() is VBoxContainer,
                "互动操作复用统一纸面按钮并竖直排列");
            Check(cancel.GetGlobalRect().Position.Y >= action.GetGlobalRect().End.Y && Mathf.IsEqualApprox(action.Size.X, cancel.Size.X),
                "互动按钮上下分隔且等宽");
            Check(!popup.FindChildren("*", "Label", true, false).Any() && !popup.FindChildren("*", "RichTextLabel", true, false).Any(), "操作菜单不重复详情文字");
            Fits(popup); await Capture("02_装备操作菜单");
            // Re-registering hovered data must not cover an open operation menu.
            HoverInfoManager.SetInfo(sword, StoragePanel.ItemHover(GD.Load<ItemData>(swordPath), 1, swordPath), true);
            await Settle(); Check(!hover.Visible, "打开操作时暂停该格悬停");
            Click(action); await Settle(); Check(owner.is_item_equipped(swordPath), "真实按钮完成装备");
            sword = Slot(swordPath); Click(sword); await Settle();
            Check(action.Text == "卸下", "装备后刷新卸下操作");
            Click(action); await Settle(); Check(!owner.is_item_equipped(swordPath), "真实按钮完成卸下");
            Click(Slot(swordPath)); await Settle(); Click(Slot(materialPath)); await Settle();
            Check(!popup.Visible, "点击材料关闭旧菜单");
            Click(Slot(swordPath)); await Settle(); Click(Slot(swordPath)); await Settle(); Check(!popup.Visible, "再次单击同格关闭菜单");
            Click(Slot(swordPath)); await Settle(); Click(new Vector2(900, 780)); await Settle(); Check(!popup.Visible, "点击空处关闭菜单");
            var pillSlot = Slot(pillPath); var pillInfo = (HoverInfoManager.HoverInfoContent)Call(storage, "build_hover_content", pillSlot)!;
            Check(pillInfo.Body.Contains("丹毒：") && pillInfo.Body.Contains("简介：") && pillInfo.Subtitle == "" && pillInfo.Title == "回春丹", "丹药共用字段且名称不重复档位和数量");
            Move(Center(pillSlot)); HoverInfoManager.SetInfo(pillSlot, pillInfo, true); await Settle(); Fits(hover); await Capture("03_丹药悬停");
            Click(pillSlot); await Settle();
            Check(popup.Visible && action.Text == "使用", $"可服用丹药显示使用（菜单{popup.Visible}，动作{action.Text}，生命{data.current_health}，命中{GetViewport().GuiGetHoveredControl()?.Name}）");
            Click(action); await Settle(); Check(data.current_health > 35, "服丹实际恢复生命");
            Check(!storage.FindChildren("*", "Control", true, false).OfType<StorageItemSlot>().Any(s => s.item_path == pillPath) && !popup.Visible, "最后一颗消耗后关闭菜单");
            storage.hide_panel(); await Settle();
            var anchor = new Button { Text = "技能信息预览", Position = new Vector2(100, 100), Size = new Vector2(150, 60) }; AddChild(anchor);
            foreach (var name in new[] { "应敌", "架招", "痛击" })
            {
                var skill = GD.Load<ActiveSkillData>($"res://resources/activeskills/{name}.tres");
                var expected = CardPresentation.Underline(CardPresentation.CombatEffectText(skill, owner));
                var info = CombatSkillBar.BuildSkillHoverInfo(owner, skill, null);
                Check(info.Sections is { Count: > 0 } && info.Body.Contains(expected), name + "完整保留效果及公式");
                Check(!info.Subtitle.Contains("类型：") && info.Body.Contains("目标：") && info.Body.Contains("使用次数："), name + "保留必要字段并撤下旧分类");
                if (name == "架招") Check(info.Body.Contains("触发应对时，消耗") && info.Body.Contains("未触发应对时，") && info.Body.Contains("未触发应对"), "应对双分支保留");
                Move(Center(anchor)); HoverInfoManager.SetInfo(anchor, info, true); await Settle(); Fits(hover);
                var visibleBody = hover.FindChildren("*", "RichTextLabel", true, false).OfType<RichTextLabel>().Single();
                Check(!System.Text.RegularExpressions.Regex.IsMatch(visibleBody.Text, @"计算公式|计算方式|\b(?:max|min|round|floor)\s*\("), name + "主栏无公式");
                Check(!visibleBody.Text.Contains("只计当前已生效"), name + "主栏无结算说明");
                await Capture("04_技能_" + name);
                if (name == "痛击")
                {
                    var rulesButton = hover.FindChildren("*", "Button", true, false).OfType<Button>().Single(b => b.Text.Contains("规则详解"));
                    Check(rulesButton.Visible, "计算说明有主动查阅入口");
                    Click(rulesButton); await Settle();
                    var rules = GetNode<HoverInfoManager>("/root/HoverInfoManager").GetNode<Control>("RulesReader");
                    Check(rules.Visible && rules.FindChildren("*", "RichTextLabel", true, false).OfType<RichTextLabel>().Single().Text.Contains("计算公式"), "点击打开完整规则与公式");
                    await Capture("08_主动查看规则");
                    Click(rules.FindChildren("*", "Button", true, false).OfType<Button>().Single()); await Settle();
                    Check(!rules.Visible, "关闭规则返回原界面");
                }
            }
            foreach (var position in new[] { new Vector2(8, 8), new Vector2(1450, 8), new Vector2(8, 780), new Vector2(1450, 780) })
            {
                anchor.Position = position; Move(Center(anchor));
                HoverInfoManager.SetInfo(anchor, CombatSkillBar.BuildSkillHoverInfo(owner, GD.Load<ActiveSkillData>("res://resources/activeskills/痛击.tres"), null), true);
                await Settle(); Fits(hover);
            }
            HoverInfoManager.HideAll();
            GetWindow().ContentScaleSize = new Vector2I(1280, 720);
            GetWindow().Size = new Vector2I(1280, 720); anchor.Position = new Vector2(100, 100); await Settle();
            Move(Center(anchor)); HoverInfoManager.SetInfo(anchor, CombatSkillBar.BuildSkillHoverInfo(owner, GD.Load<ActiveSkillData>("res://resources/activeskills/架招.tres"), null), true);
            await Settle(); Fits(hover); await Capture("05_小窗口应对技能");
            foreach (var path in new[] { "玄枢含章经/复命", "玄枢含章经/惊蛰", "百骸衍枝化生宝册/化法/架招第四卷", "太和感物养元宝册/涵虚", "赤阳参同经/大冶" })
            {
                var skill = GD.Load<ActiveSkillData>($"res://resources/activeskills/{path}.tres");
                var info = CombatSkillBar.BuildSkillHoverInfo(owner, skill, null);
                Check(info.Body.Contains(CardPresentation.Underline(CardPresentation.CombatEffectText(skill, owner))), "复杂技能完整效果 " + path);
                Move(Center(anchor)); HoverInfoManager.SetInfo(anchor, info, true); await Settle(); Fits(hover);
                if (path.Contains("架招第四卷")) await Capture("07_小窗口长技能");
            }
            HoverInfoManager.HideAll(); anchor.Hide(); storage.show_panel(); await Settle();
            Click(Slot(swordPath)); await Settle(); Fits(popup); await Capture("06_小窗口操作菜单");
            storage.hide_panel(); Check(!popup.Visible, "关闭行囊收起操作菜单");
            GetWindow().ContentScaleSize = new Vector2I(1672, 941); GetWindow().Size = new Vector2I(1672, 941);
            var originalSettingsPath = GameSettingsPanel.SettingsPath;
            GameSettingsPanel.SettingsPath = "user://tests/information_mode_smoke.cfg";
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("user://tests"));
            try
            {
                var settings = new GameSettingsPanel(); AddChild(settings); await Settle();
                var selector = settings.FindChild("InformationModeSelector", true, false) as OptionButton;
                Check(selector is not null && selector.ItemCount == 2, "正式设置提供简略和详细两档");
                await Capture("09_信息显示设置");
                var sample = "自身获得8层周流。计算公式：体魄[10]×0.8 = 8。\n只计当前已生效的自身增伤，未计施放时触发的额外效果。";
                var liveLabel = new Label(); AddChild(liveLabel); PlayerInfoText.Bind(liveLabel, sample);
                var liveRich = new RichTextLabel(); AddChild(liveRich); PlayerInfoText.Bind(liveRich, sample);
                Check(liveLabel.Text.Contains("8层周流") && !liveLabel.Text.Contains("×"), "简略模式保留结果");
                selector!.Select(1); selector.EmitSignal(OptionButton.SignalName.ItemSelected, 1L);
                Check(liveLabel.Text.Contains("8层周流。\n\n计算来源\n8 = 体魄[10]×0.8") && liveRich.Text == liveLabel.Text, "设置立即将存量文字切至末尾独行计算来源");
                Check(!liveLabel.Text.Contains("只计当前"), "详细模式不恢复通用结算噪声");
                var saved = new ConfigFile(); Check(saved.Load(GameSettingsPanel.SettingsPath) == Error.Ok && saved.GetValue("interface", "information_detail").AsInt32() == 1, "详细模式持久保存");
                settings.QueueFree(); await Settle();
                var reopened = new GameSettingsPanel(); AddChild(reopened); await Settle();
                Check(reopened.FindChildren("InformationModeSelector", "OptionButton", true, false).OfType<OptionButton>().Single().Selected == 1, "重新打开设置保留详细选项");
                reopened.QueueFree(); await Settle();
                liveLabel.Hide(); liveRich.Hide(); anchor.Show(); anchor.Position = new Vector2(100, 100);
                var detailInfo = CombatSkillBar.BuildSkillHoverInfo(owner, GD.Load<ActiveSkillData>("res://resources/activeskills/痛击.tres"), null);
                Move(Center(anchor)); HoverInfoManager.SetInfo(anchor, detailInfo, true); await Settle();
                var body = hover.FindChildren("*", "RichTextLabel", true, false).OfType<RichTextLabel>().Single();
                Check(body.Text.Contains("计算来源[/b][/color]\n") && body.Text.Contains("体魄["), "详细悬停在正文末尾分行显示公式");
                Check(!body.Text.Contains("只计当前已生效") && !body.Text.Contains("计算公式："), "详细悬停没有公式标题与通用结算清单");
                Fits(hover); await Capture("10_详细版技能公式");
                PlayerInfoText.SetMode(PlayerInfoText.InformationMode.Simple, persist: false); await Settle();
                Check(!body.Text.Contains("体魄["), "已打开悬停即时切回简略");
                PlayerInfoText.SetMode(PlayerInfoText.InformationMode.Detailed, persist: false); await Settle();
                Check(body.Text.Contains("体魄["), "已打开悬停即时切回详细");
                GetWindow().ContentScaleSize = new Vector2I(1280, 720); GetWindow().Size = new Vector2I(1280, 720);
                await Settle(); Move(Center(anchor));
                HoverInfoManager.SetInfo(anchor, CombatSkillBar.BuildSkillHoverInfo(owner, GD.Load<ActiveSkillData>("res://resources/activeskills/百骸衍枝化生宝册/化法/架招第四卷.tres"), null), true);
                await Settle(); Fits(hover);
                Check(body.Text.Contains("计算来源") && body.Text.Contains("体魄["), "小窗口长技能保留末尾详细公式");
                await Capture("11_小窗口详细版长技能");
                HoverInfoManager.SetInfo(anchor, hideInfo, true); await Settle(); Fits(hover);
                Check(body.Text.Contains("简介：") && !body.Text.Contains("数量") && !body.Text.Contains("分类"), "小窗口材料简介占位与通用字段可见");
                await Capture("12_材料通用悬停_详细");
                PlayerInfoText.SetMode(PlayerInfoText.InformationMode.Simple, persist: false); await Settle(); Fits(hover);
                await Capture("13_材料通用悬停_简略");
                anchor.Hide(); HoverInfoManager.HideAll();
                GetWindow().ContentScaleSize = new Vector2I(1672, 941); GetWindow().Size = new Vector2I(1672, 941);
                var exchange = GD.Load<PackedScene>("res://tscn/world/bai_chuan/YuxuSectExchangePanel.tscn").Instantiate<YuxuSectExchangePanel>();
                AddChild(exchange); exchange.open_panel(100); await Settle();
                var manager = GetNode<HoverInfoManager>("/root/HoverInfoManager");
                var entries = (System.Collections.Generic.Dictionary<Control, HoverInfoManager.HoverInfoContent>)typeof(HoverInfoManager)
                    .GetField("_entries", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(manager)!;
                var exchangeHide = entries.First(pair => pair.Value.Title == "兽皮" && exchange.IsAncestorOf(pair.Key));
                Check(exchangeHide.Value.Body == hideInfo.Body && exchangeHide.Value.Subtitle == "", "真实兑换与行囊共用材料信息且无贡献价格");
                Move(Center(exchangeHide.Key)); HoverInfoManager.SetInfo(exchangeHide.Key, exchangeHide.Value, true); await Settle(); Fits(hover);
                await Capture("14_兑换材料通用悬停");
                exchange.QueueFree(); await Settle();
                var sampleNames = new[] { "兽皮", "玄铁", "灵泉露", "凝露松脂" };
                foreach (var name in sampleNames)
                    Check(owner.add_item_to_inventory_silently($"res://resources/entities/items/{name}.tres"), name + "加入隔离行囊");
                storage.show_panel(); await Settle();
                foreach (var name in sampleNames)
                {
                    var slot = Slot($"res://resources/entities/items/{name}.tres");
                    var sampleInfo = (HoverInfoManager.HoverInfoContent)Call(storage, "build_hover_content", slot)!;
                    Move(Center(slot)); HoverInfoManager.SetInfo(slot, sampleInfo, true); await Settle(); Fits(hover);
                    Check(hover.Visible && sampleInfo.Body.Contains("简介：") && !sampleInfo.ShowRules, name + "通用材料栏保留简介且无详解");
                    Check(!hover.FindChildren("*", "Button", true, false).OfType<Button>().Any(b => b.Visible && b.Text.Contains("规则详解")), name + "材料无详解按钮");
                    GetViewport().PushInput(new InputEventKey { Keycode = Key.F1, Pressed = true }, true);
                    GetViewport().PushInput(new InputEventKey { Keycode = Key.F1, Pressed = false }, true);
                    await Settle(); Check(hover.Visible, name + "按F1不打开材料规则页");
                    await Capture("15_材料定稿_" + name, hover);
                }
                PlayerInfoText.SetMode(PlayerInfoText.InformationMode.Detailed, persist: false); await Settle(); Fits(hover);
                Check(!hover.FindChildren("*", "Button", true, false).OfType<Button>().Any(b => b.Visible && b.Text.Contains("规则详解")), "详细版材料同样无详解入口");
                await Capture("16_材料定稿_详细", hover);
                storage.hide_panel();
                PlayerInfoText.SetMode(PlayerInfoText.InformationMode.Simple, persist: false);
                var pillSamples = new[] { "alchemy/huichun_1", "alchemy/tongmai_2", "alchemy/jingxin_4", "定脉丹" };
                foreach (var name in pillSamples)
                    Check(owner.add_item_to_inventory_silently($"res://resources/entities/items/{name}.tres"), name + "加入隔离行囊");
                storage.show_panel(); await Settle();
                foreach (var name in pillSamples)
                {
                    var slot = Slot($"res://resources/entities/items/{name}.tres");
                    var info = (HoverInfoManager.HoverInfoContent)Call(storage, "build_hover_content", slot)!;
                    Check(info.Subtitle == "" && info.Body.Contains("丹阶：") && info.Body.Contains("\n药效：") && info.Body.Contains("\n丹毒：") && info.Body.Contains("简介："), name + "丹药固定字段");
                    Check(!info.Body.Contains("使用条件") && !info.Body.Contains("颗数") && !info.Body.Contains("瓷瓶") && !info.ShowRules, name + "丹药无重复及页面信息");
                    Move(Center(slot)); HoverInfoManager.SetInfo(slot, info, true); await Settle(); Fits(hover);
                    await Capture("17_丹药定稿_" + info.Title, hover);
                }
                var heal = GD.Load<PillItemData>("res://resources/entities/items/alchemy/huichun_1.tres");
                Check(PlayerInfoText.Summary(AlchemyPillVisual.Hover(heal).Body).Contains("恢复18点生命值") && AlchemyPillVisual.Hover(heal).Body.TrimEnd().EndsWith("简介："), "回春药效与空简介保持");
                Check(PlayerInfoText.Summary(AlchemyPillVisual.Hover(heal).Body).Contains("＋10")
                    && PlayerInfoText.Detailed(AlchemyPillVisual.Hover(heal).Body).Contains("10 = (8×1.2)"), "详细丹毒计算来源与实际值一致");
                data.pill_toxicity = 40;
                data.pill_toxicity_last_game_hour = -1;
                var absorbed = AlchemyPillVisual.Hover(heal, user: owner);
                Check(PlayerInfoText.Summary(absorbed.Body).Contains("恢复16点生命值，基础18点"), "丹毒影响下保留基础和本人吸收值");
                PlayerInfoText.SetMode(PlayerInfoText.InformationMode.Detailed, persist: false);
                var healSlot = Slot("res://resources/entities/items/alchemy/huichun_1.tres");
                Move(Center(healSlot)); HoverInfoManager.SetInfo(healSlot, absorbed, true); await Settle(); Fits(hover);
                await Capture("18_丹药定稿_受丹毒影响详细", hover);
                storage.hide_panel();
                var manualSamples = new[] { "玄枢含章经", "太和感物养元宝册", "舒卷天章" };
                foreach (var name in manualSamples)
                    Check(owner.add_item_to_inventory_silently($"res://resources/entities/items/{name}秘籍.tres"), name + "加入隔离行囊");
                storage.show_panel(); await Settle();
                foreach (var name in manualSamples)
                {
                    var slot = Slot($"res://resources/entities/items/{name}秘籍.tres");
                    var info = (HoverInfoManager.HoverInfoContent)Call(storage, "build_hover_content", slot)!;
                    Check(info.Body.Contains("修习境界：凡人至云息十层") && info.Body.Contains("\n修习要求：") && info.Body.Contains("\n关键词：") && info.Body.Contains("简介："), name + "秘籍字段完整");
                    Check(!info.Body.Contains("及以上") && !info.Body.Contains("研读") && !info.Body.Contains("—"), name + "不重复境界且无研读值");
                    Check(info.Subtitle == "" && !info.ShowRules && !info.Body.Contains("res://"), name + "无分类数量和实现路径");
                    Move(Center(slot)); HoverInfoManager.SetInfo(slot, info, true); await Settle(); Fits(hover);
                    await Capture("19_秘籍预览_" + name, hover);
                }
                var noRequirements = new GongfaResource { required_study_progress = 0 };
                var emptyManual = new ItemData { item_category = "秘籍", gongfa = noRequirements };
                Check(ManualItemPresentation.Hover(emptyManual).Body.Contains("修习要求：无"), "无要求秘籍固定显示无");
                var xuanshu = GD.Load<ItemData>("res://resources/entities/items/玄枢含章经秘籍.tres");
                Check(ManualItemPresentation.Hover(xuanshu).Body.Contains("灵慧≥5") && ManualItemPresentation.Hover(xuanshu).Body.Contains("关键词：周天"), "秘籍读取真实属性门槛和心法关键词");
                GetWindow().ContentScaleSize = new Vector2I(1280, 720); GetWindow().Size = new Vector2I(1280, 720); await Settle();
                var manualSlot = Slot("res://resources/entities/items/太和感物养元宝册秘籍.tres");
                Move(Center(manualSlot)); HoverInfoManager.SetInfo(manualSlot, ManualItemPresentation.Hover(GD.Load<ItemData>(manualSlot.item_path)), true); await Settle(); Fits(hover);
                await Capture("20_秘籍预览_小窗口", hover);
                storage.hide_panel();
            }
            finally
            {
                DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(GameSettingsPanel.SettingsPath));
                GameSettingsPanel.SettingsPath = originalSettingsPath;
            }
            GD.Print($"INFORMATION_PANEL_SMOKE_OK {_checks}"); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
