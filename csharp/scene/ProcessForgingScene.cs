using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>材料优先的炼器试炼入口。炼前只呈现投入及过程条件，炼后揭示独立器物。</summary>
public partial class ProcessForgingScene : Control
{
    private ProcessForgingTrial _model = null!;
    private ProcessForgingTrial.Session _session = null!;
    private ProcessForgingTrial.Draft _draft = new();
    private Control _canvas = null!, _page = null!;
    private Control? _overlay;
    private ScrollContainer? _inputScroll;
    private int _scroll;
    private long _revision;
    private string _notice = "先配材料，再开炼揭晓；样方仅填入材料，不指定成品。";
    private readonly Dictionary<string, Button> _buttons = [];
    private const string Evidence = "res://docs/美术规范/炼器物品展示_20261022/";
    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); MouseFilter = MouseFilterEnum.Stop;
        Theme = QuietRoomStyle.MakeTheme();
        Theme.SetColor("font_placeholder_color", "LineEdit", new Color("797d6e"));
        _model = new ProcessForgingTrial(); _session = new ProcessForgingTrial.Session(_model.Parameters.Profiles.Keys);
        var bg = new TextureRect { Texture = GD.Load<Texture2D>(ForgingInkStyle.Assets + "ink_workshop.png"), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(bg); bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _canvas = QuietRoomStyle.Canvas(this); Refresh();
        if (OS.GetCmdlineUserArgs().Contains("--audit-process-forging")) CallDeferred(nameof(AuditProcess));
    }
    private void Edit() { _revision++; Refresh(); }
    private void Refresh()
    {
        if (_inputScroll is not null && IsInstanceValid(_inputScroll)) _scroll = _inputScroll.ScrollVertical;
        if (_page is not null) { _canvas.RemoveChild(_page); _page.QueueFree(); }
        _buttons.Clear();
        _page = new Control { Size = new Vector2(1672, 941), MouseFilter = MouseFilterEnum.Pass }; _canvas.AddChild(_page);
        var header = Paper(_page, 24, 18, 1624, 76);
        Put(header, Btn("〈 返回", () => { if (GetParent() is BaigongPanel) QueueFree(); else GetTree().ChangeSceneToFile("res://tscn/scene/StartMenu.tscn"); }), 15, 13, 106, 48);
        Put(header, Text("炼 器", 34), 150, 12, 150, 50);
        Put(header, Text("剑类 · 材料成器试炼", 21), 312, 25, 325, 34);
        Put(header, Btn("参考投料", Examples), 665, 15, 165, 46);
        Put(header, Text("试炼造诣", 19), 867, 25, 115, 32);
        var proficiency = new SpinBox { MinValue = 0, MaxValue = 100, Step = 1, Value = _draft.Proficiency, TooltipText = "只改变本页试炼加工条件。降低造诣不会偷偷删除已填材料；超出位置时禁止提交。" };
        proficiency.ValueChanged += value => { _draft.Proficiency = (int)value; Edit(); };
        Put(header, proficiency, 984, 17, 112, 43);
        Put(header, Btn($"器物册 · {_session.Artifacts.Count}", Gallery), 1130, 15, 210, 46);
        Put(header, Btn("炼器说明", Help), 1385, 15, 205, 46);
        BuildInputs(); BuildProcess(); BuildCosts();
        Put(_page, Text("独立试炼 · 不扣行囊、不推进正式时间、不赠送装备 · 所有数值为候选", 17), 360, 904, 1100, 30);
    }
    private void BuildInputs()
    {
        var panel = Paper(_page, 24, 112, 400, 784);
        Put(panel, Text("投 料", 27), 22, 15, 180, 40);
        Put(panel, Text("大类  剑  ／  材料形成器型", 17), 22, 58, 355, 30);
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; _inputScroll = scroll;
        Put(panel, scroll, 14, 101, 372, 608);
        var list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; list.AddThemeConstantOverride("separation", 8); scroll.AddChild(list);
        list.AddChild(Text("身材 · 成形与连接", 22));
        var bodyPanel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        bodyPanel.AddThemeStyleboxOverride("panel", ForgingInkStyle.Paper(padding: 10, plain: true));
        list.AddChild(bodyPanel);
        var bodyList = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        bodyList.AddThemeConstantOverride("separation", 8); bodyPanel.AddChild(bodyList);
        foreach (string role in ProcessForgingTrial.RoleOrder)
        {
            bool bodyRole = role is "主体" or "承接" or "复合身材";
            var roleList = bodyRole ? bodyList : list;
            var inputs = _draft.Inputs.Where(x => x.Role == role).ToArray(); int slots = _model.Slots(role, _draft.Proficiency);
            var line = new HBoxContainer(); roleList.AddChild(line);
            var title = Text(role + $"  {inputs.Length}/{slots}", bodyRole ? 18 : 22); title.SizeFlagsHorizontal = SizeFlags.ExpandFill; line.AddChild(title);
            var add = Btn("＋", () => Picker(role, null)); add.CustomMinimumSize = new Vector2(46, 32); add.Disabled = inputs.Length >= slots; line.AddChild(add);
            if (slots == 0) roleList.AddChild(Text("造诣32开放复合成材", 15));
            if (inputs.Length == 0 && slots > 0)
            {
                var pick = Btn(role is "复合身材" or "质材" or "性材" or "和材" ? "＋ 选用材料（可选）" : "＋ 选择材料", () => Picker(role, null));
                pick.CustomMinimumSize = new Vector2(0, 52); pick.SizeFlagsHorizontal = SizeFlags.ExpandFill; roleList.AddChild(pick);
            }
            foreach (var input in inputs)
            {
                var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 5); roleList.AddChild(row);
                var material = Btn(input.Material, () => Picker(role, input)); material.CustomMinimumSize = new Vector2(bodyRole ? 175 : 195, 58); material.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                material.Icon = GD.Load<Texture2D>(_model.Materials[input.Material].icon_path); material.ExpandIcon = true; material.AddThemeConstantOverride("icon_max_width", 42);
                material.TooltipText = MaterialInfo(input.Material, role); material.AddThemeFontSizeOverride("font_size", 18); row.AddChild(material);
                var quantity = new SpinBox { MinValue = 1, MaxValue = 8, Step = 1, Value = input.Quantity, CustomMinimumSize = new Vector2(75, 58) };
                quantity.ValueChanged += value => { input.Quantity = (int)value; Edit(); }; row.AddChild(quantity);
                var remove = Btn("×", () => { _draft.Inputs.Remove(input); Edit(); }); remove.CustomMinimumSize = new Vector2(32, 58); row.AddChild(remove);
            }
        }
        Put(panel, Btn("清空投料", () => { _draft.Inputs.Clear(); _scroll = 0; Edit(); }), 20, 730, 165, 40);
        Put(panel, Btn("查看加工范围", () => Message("首批加工档案", $"当前仅适配{_model.Parameters.Profiles.Count}种已有材料。\n\n" + string.Join("\n", _model.Parameters.Profiles.Select(x => x.Key + "：" + string.Join("、", x.Value.Roles))) + "\n\n质地只是加工类型。缺少档案的材料不会默认可用。")), 200, 730, 178, 40);
        scroll.SetDeferred(ScrollContainer.PropertyName.ScrollVertical, _scroll);
    }
    private void BuildProcess()
    {
        var top = Paper(_page, 442, 112, 742, 113, .94f);
        Put(top, Text("以材成器", 29), 24, 12, 260, 42);
        Put(top, Text("所投之材，经加工成器；等级与实际效果在炼后揭晓。", 18), 24, 64, 690, 30);
        // 中部留给已入库的水墨锻造台，不再用丹炉或提前显示成品剑图。
        var strip = Paper(_page, 442, 458, 742, 82, .92f);
        Put(strip, Text("提炼  →  成形  →  相合  →  导通  →  固化", 23), 25, 12, 700, 34);
        Put(strip, Text("保留材料潜力     形成实体结构     组织配伍与通路", 16), 32, 49, 680, 25);
        var relations = Paper(_page, 442, 558, 742, 161);
        Put(relations, Text("投入五行", 23), 22, 12, 690, 36);
        var inputs = _draft.Inputs.ToArray();
        string text = inputs.Length == 0 ? "选择材料后，在此查看投入的五行构成。" : string.Join("  ／  ", inputs.GroupBy(x => _model.Snapshot(x.Material).Element.DefaultIfEmptyName()).OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => x.Key + " " + x.Sum(i => i.Quantity) + "份"));
        Put(relations, Wrap(text, 18), 22, 57, 696, 91);
        var yin = Paper(_page, 442, 737, 742, 159);
        Put(yin, Text("投入阴阳 · 七档", 23), 22, 12, 550, 34);
        double value = _model.Inspect(_draft, _session.Stock).InputPolarity;
        for (int n = 0; n < 7; n++)
        {
            int i = n; var p = new Panel(); p.AddThemeStyleboxOverride("panel", QuietRoomStyle.Edge(i == (int)Math.Round(value, MidpointRounding.AwayFromZero) + 3));
            Put(yin, p, 24 + i * 99, 60, 94, 39); var l = Text(ProcessForgingTrial.PolarityNames[i], 19); l.HorizontalAlignment = HorizontalAlignment.Center; Put(p, l, 0, 1, 94, 36);
        }
        Put(yin, Text("当前投入：" + ProcessForgingTrial.PolarityLabel(value) + "。阴阳偏极也可成器。", 17), 24, 117, 695, 28);
    }
    private void BuildCosts()
    {
        var panel = Paper(_page, 1202, 112, 446, 784);
        var r = _model.Inspect(_draft, _session.Stock);
        Put(panel, Text("开炼用度", 27), 24, 14, 380, 40);
        var body = new VBoxContainer(); body.AddThemeConstantOverride("separation", 12); Put(panel, body, 24, 73, 398, 368);
        foreach (var value in new[] { $"实物投入      {r.Units} / {_model.Parameters.MaxUnits} 份", $"炼器造诣      {_draft.Proficiency}", $"当前工时      {(r.Hours > 0 ? r.Hours.ToString() : "—")} 时辰", $"试炼精力      {(r.Energy > 0 ? r.Energy.ToString() : "—")}" }) body.AddChild(Text(value, 23));
        body.AddChild(Wrap("用度只在本页记账。开炼消耗本页投入，结果在结束后揭晓。", 17));
        var conditions = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        Put(panel, conditions, 24, 385, 398, 170);
        var line = Wrap(string.Join("\n", r.Errors.DefaultIfEmpty("材料与份数已记录。")), 18); line.SizeFlagsHorizontal = SizeFlags.ExpandFill; conditions.AddChild(line);
        bool submitted = _session.IsSubmitted(_model, _draft, _revision);
        var craft = Btn(submitted ? "已结算 · 改料后再炼" : "开 炼", Craft); ForgingInkStyle.Button(craft, true); craft.Disabled = !r.CanSubmit || submitted; _buttons["craft"] = craft;
        Put(panel, craft, 24, 577, 398, 57);
        Put(panel, Wrap(_notice, 17), 24, 652, 395, 74);
        Put(panel, Text($"本页累计：{_session.Hours} 时辰 · {_session.Artifacts.Count} 件成器", 17), 24, 744, 398, 28);
    }
    private void Craft()
    {
        var result = _session.Submit(_model, _draft, _revision);
        _notice = result.CanSubmit ? result.Success ? "本次成器已收入试炼器物册，可随时重看。" : "本次未成器；已记录投入与失败原因。" : string.Join("；", result.Errors);
        Refresh();
        if (result.Success) ShowArtifact(result.Product!, 0);
        else Message(result.CanSubmit ? "本次未成器" : "未能开炼", string.Join("\n", result.Errors.Concat(result.Failures)) + (result.CanSubmit ? $"\n\n已消耗本页投入，记账{result.Hours}时辰、{result.Energy}精力。\n正式行囊与人物均未改变。" : "\n\n未扣除材料。"));
    }
    private void Examples()
    {
        int footer = 104 + 80 * _model.Parameters.Examples.Count;
        var panel = Overlay("参考投料 · 不预设成品", 750, footer + 130);
        int y = 87;
        for (int i = 0; i < _model.Parameters.Examples.Count; i++)
        {
            int index = i; var example = _model.Parameters.Examples[i];
            Put(panel, Btn(example.Name + $"  ·  造诣{example.Proficiency}", () => LoadExample(index)), 28, y, 690, 62); y += 80;
        }
        Put(panel, Wrap("载入会替换本页投料及试炼造诣；不补库存。每套均可继续改料，结果由同一模型计算。", 18), 30, footer, 680, 80);
    }
    private void LoadExample(int index)
    {
        CloseOverlay(); _draft = _model.ExampleDraft(index); _scroll = 0; _notice = "已载入“" + _model.Parameters.Examples[index].Name + "”，可以改料或直接试炼。"; Edit();
    }
    private string MaterialInfo(string name, string role)
    {
        var m = _model.Snapshot(name); var p = _model.Parameters.Profiles[name];
        return $"{name} · {m.Texture} · {m.Element.DefaultIfEmptyName()} · {ProcessForgingTrial.PolarityLabel(m.Polarity)}\n" + string.Join("、", m.Utilities.Select(x => x.Key + MaterialCraftData.utility_grade_name(x.Value))) +
            $"\n可用岗位：{string.Join("、", p.Roles.Select(RoleLabel))}\n加工难度{p.Difficulty} · 当前库存{_session.Stock[name]}\n本次岗位：{RoleLabel(role)}；各岗位主要贡献不同，均参与统一计算。";
    }
    private static string RoleLabel(string role) => role is "主体" or "承接" or "复合身材" ? "身材／" + DisplayText(role) : role;
    // 保留现有工艺键及旧快照，只统一玩家看到的岗位名称。
    private static string DisplayText(string text) => text.Replace("复合身材", "辅体");
    private void Picker(string role, ProcessForgingTrial.Input? selected)
    {
        var panel = Overlay("选择" + RoleLabel(role) + " · 只显示有加工档案的材料", 1030, 650);
        var search = new LineEdit { PlaceholderText = "搜索材料名" }; Put(panel, search, 28, 80, 972, 44);
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; Put(panel, scroll, 28, 142, 972, 420);
        var grid = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill }; grid.AddThemeConstantOverride("h_separation", 10); grid.AddThemeConstantOverride("v_separation", 12); scroll.AddChild(grid);
        void Fill()
        {
            foreach (var child in grid.GetChildren()) { grid.RemoveChild(child); child.QueueFree(); }
            foreach (string name in _model.Choices(role).Where(x => x.Contains(search.Text, StringComparison.OrdinalIgnoreCase)))
            {
                var p = _model.Parameters.Profiles[name];
                var button = Btn(name + $"\n库存 {_session.Stock[name]} · 加工难度 {p.Difficulty}", () =>
                {
                    if (selected is null) _draft.Inputs.Add(new ProcessForgingTrial.Input { Role = role, Material = name }); else selected.Material = name;
                    CloseOverlay(); Edit();
                });
                button.TooltipText = MaterialInfo(name, role); button.CustomMinimumSize = new Vector2(310, 90); button.AddThemeFontSizeOverride("font_size", 18);
                button.Icon = GD.Load<Texture2D>(_model.Materials[name].icon_path); button.ExpandIcon = true; button.AddThemeConstantOverride("icon_max_width", 56); grid.AddChild(button);
            }
        }
        search.TextChanged += _ => Fill(); Fill();
        Put(panel, Wrap("每份实物只承担当前岗位。可将同种材料的不同份数投入其他岗位，库存按整炉合计。", 18), 28, 580, 970, 55);
    }
    private void Gallery()
    {
        var panel = Overlay("试炼器物册 · 离开本页后清空", 980, 700);
        var list = ScrollList(panel, 28, 80, 924, 590);
        if (_session.Artifacts.Count == 0) list.AddChild(Wrap("尚无成器。先选择参考投料或自行配料，开炼后再来查看。", 23));
        foreach (var artifact in _session.Artifacts)
        {
            var b = Btn(artifact.Name + " · " + artifact.Tier + "\n锋锐 " + artifact.Sharpness + " · " + ProcessForgingTrial.PolarityLabel(artifact.Polarity), () => ShowArtifact(artifact, 0));
            b.Name = "ForgedArtifactItem_" + artifact.InstanceId;
            b.Icon = ForgedArtifactPresentation.Icon(artifact); b.ExpandIcon = true; b.AddThemeConstantOverride("icon_max_width", 160);
            b.CustomMinimumSize = new Vector2(0, 85); b.SizeFlagsHorizontal = SizeFlags.ExpandFill; list.AddChild(b);
            HoverInfoManager.SetInfo(b, ForgedArtifactPresentation.Hover(artifact));
        }
        list.AddChild(Text("试炼记录 · 含失败投入", 24));
        foreach (var attempt in _session.Attempts.Reverse())
        {
            var b = Btn($"第{attempt.Number}次 · {(attempt.Success ? "成器" : "未成器")} · {attempt.Hours}时辰", () =>
                    Message("第" + attempt.Number + "次试炼记录", string.Join("\n", attempt.Inputs.Select(x => $"{RoleLabel(x.Role)} · {x.Material} ×{x.Quantity}")) + $"\n\n{attempt.Hours}时辰、{attempt.Energy}试炼精力。\n" + (attempt.Success ? "成器已收入器物册。" : string.Join("\n", attempt.Failures))));
            b.CustomMinimumSize = new Vector2(0, 53); list.AddChild(b);
        }
    }
    public override void _UnhandledInput(InputEvent e)
    {
        if (_overlay is not null && e.IsActionPressed("ui_cancel")) { CloseOverlay(); GetViewport().SetInputAsHandled(); }
    }
    private void ShowArtifact(ProcessForgingTrial.Artifact artifact, int tab)
    {
        var panel = Overlay(artifact.Name + " · " + artifact.Tier + "  ／  试炼成器", 1460, 810);
        var tabs = new HBoxContainer(); tabs.AddThemeConstantOverride("separation", 14); Put(panel, tabs, 28, 76, 1390, 46);
        string[] names = ["器物信息", "能力详情", "炼制记录"];
        for (int i = 0; i < names.Length; i++) { int index = i; var b = Btn(names[i], () => ShowArtifact(artifact, index)); b.CustomMinimumSize = new Vector2(210, 45); if (tab == i) ForgingInkStyle.Button(b, true); tabs.AddChild(b); }
        var list = ScrollList(panel, 32, 145, 1392, 616);
        void Add(string text, int size = 22) => list.AddChild(Wrap(text, size));
        if (tab == 0)
        {
            var header = new HBoxContainer { CustomMinimumSize = new(0, 98) }; header.AddThemeConstantOverride("separation", 24); list.AddChild(header);
            var icon = new TextureRect { Name = "ForgedArtifactIcon", Texture = ForgedArtifactPresentation.Icon(artifact),
                CustomMinimumSize = new(240, 90), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Stop };
            header.AddChild(icon); HoverInfoManager.SetInfo(icon, ForgedArtifactPresentation.Hover(artifact));
            var identity = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; header.AddChild(identity);
            var name = Wrap(artifact.Name, 28); identity.AddChild(name);
            identity.AddChild(Wrap(ForgedArtifactPresentation.Subtitle(artifact), 21));
            foreach (var section in ForgedArtifactPresentation.Sections(artifact))
            {
                Add(section.Title, 26); Add(section.Body, 21);
            }
            Add("试炼成器 · 仅供查看，装备功能暂未开放。", 17);
        }
        else if (tab == 1)
        {
            Add("实际能力", 26);
            if (artifact.Effects.Count == 0) Add("本器只有剑的本体性能。");
            foreach (var effect in artifact.Effects.Where(x => !x.Special))
            {
                Add(ForgedArtifactPresentation.EffectDetails(effect), 21);
            }
            foreach (var section in ForgedArtifactPresentation.AbilitySections(artifact))
            {
                Add(section.Title, 25); Add(section.Body, 21);
            }
            Add("这里展示本器已形成的能力，装备与战斗使用暂未开放。", 19);
        }
        else
        {
            Add($"炼制造诣 {artifact.Proficiency}  ／  本次成器：{artifact.Tier}", 24);
            Add($"本次用度：{artifact.CraftingHours}时辰 / {artifact.CraftingEnergy}试炼精力", 20);
            Add("材料五行来源：" + string.Join("、", artifact.MaterialElementContributions.Keys.OrderBy(x => x, StringComparer.Ordinal).DefaultIfEmpty("无行")), 20);
            foreach (var source in artifact.Sources.OrderBy(x => Array.IndexOf(ProcessForgingTrial.RoleOrder, x.Input.Role)))
                Add($"{RoleLabel(source.Input.Role)} · {source.Input.Material} ×{source.Input.Quantity}", 21);
            Add("各岗位按实际材性共同参与本体、收益和工艺。内部评阶与分配记录随器物快照保存。", 18);
            Add("规则版本：" + artifact.RulesVersion + "\n实例：" + artifact.InstanceId, 16);
        }
    }
    private void Help() => Message("炼器原理与试炼说明", "选择剑大类，配置材料及整份投料，开炼后查看结果。\n\n身材主要形成剑的本体与锋锐；质材主要建立灵性基础；性材主要提供角色收益；和材主要处理配伍与通路。各岗位不是独占来源。\n\n材料五行用于炼制配伍，影响潜力保留与稳定性。同属性不凭空增益，相克可用真实和材通关。材料来源不代表伤害属性或伤害占比，单纯连接或通关不会使其他效果附带五行伤害。\n\n凡器具备本体性能和基础加成。器身能够稳定承载、维持并传导灵性效力时，可达到法器水准；是否有主动技能不是评阶依据。阴阳偏极也能正常成器。\n\n材料品阶和功用品级参与统一计算，不按材料名字匹配配方，不要求某品原料才能评某档。造诣改善加工保留，不能凭空创造材料能力。\n\n造诣20开放第二和材，32开放第二性材及辅体，50开放第三和材；位置解锁不等于器物等级。质材、性材及和材可按需选用。\n\n本页每种材料30份。非法提交不扣料；合法尝试消耗本页投入。所有数值仅供试炼，正式行囊、历法、存档和战斗不受影响。炼前不显示成品预测或补料建议。" );
    private Control Overlay(string title, float width, float height)
    {
        CloseOverlay(); HoverInfoManager.HideAll();
        _overlay = new Control { Size = new Vector2(1672, 941), MouseFilter = MouseFilterEnum.Stop, ZIndex = 20 }; _canvas.AddChild(_overlay);
        var shade = new ColorRect { Color = new Color(0, 0, 0, .63f), Size = _overlay.Size, MouseFilter = MouseFilterEnum.Stop }; _overlay.AddChild(shade);
        var paper = Paper(_overlay, (1672 - width) / 2, (941 - height) / 2, width, height, 1);
        Put(paper, Text(title, 27), 28, 17, width - 170, 43);
        Put(paper, Btn("关闭", CloseOverlay), width - 117, 17, 90, 43); return paper;
    }
    private void CloseOverlay() { if (_overlay is not null) { _canvas.RemoveChild(_overlay); _overlay.QueueFree(); _overlay = null; } HoverInfoManager.HideAll(); }
    private void Message(string title, string text)
    {
        var panel = Overlay(title, 1000, 720); var list = ScrollList(panel, 32, 87, 935, 590); list.AddChild(Wrap(text, 23));
    }
    private static VBoxContainer ScrollList(Control parent, float x, float y, float w, float h)
    {
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; Put(parent, scroll, x, y, w, h);
        var list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; list.AddThemeConstantOverride("separation", 16); scroll.AddChild(list); return list;
    }
    private static Label Text(string text, int size = 20) => QuietRoomStyle.Text(DisplayText(text), size);
    private static Label Wrap(string text, int size = 20) { var l = Text(text, size); l.AutowrapMode = TextServer.AutowrapMode.WordSmart; l.SizeFlagsHorizontal = SizeFlags.ExpandFill; return l; }
    private static Button Btn(string text, Action action) { var b = new Button { Text = DisplayText(text), MouseDefaultCursorShape = CursorShape.PointingHand }; ForgingInkStyle.Button(b); b.Pressed += action; return b; }
    private static void Put(Control parent, Control node, float x, float y, float w, float h) { parent.AddChild(node); node.Position = new Vector2(x, y); node.Size = new Vector2(w, h); }
    private static Control Paper(Control parent, float x, float y, float w, float h, float alpha = .97f)
    { var panel = new Panel { MouseFilter = MouseFilterEnum.Pass }; panel.AddThemeStyleboxOverride("panel", ForgingInkStyle.Paper(h > w * 1.3f, alpha, padding: 0, plain: w / h > 7)); Put(parent, panel, x, y, w, h); QuietRoomStyle.Frame(panel); return panel; }
}





