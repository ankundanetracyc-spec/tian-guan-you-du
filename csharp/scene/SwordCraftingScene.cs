using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>复用正式材性资源的炼器试炼界面；独立用料，不写入正式存档。</summary>
public partial class SwordCraftingScene : Control
{
    private SwordCraftTrial _model = null!;
    private readonly Dictionary<string, SwordCraftTrial.Draft> _drafts = [];
    private readonly Dictionary<string, int> _stock = [];
    private string _form = "细身剑", _selected = "剑身";
    private bool _methods;
    private Control _canvas = null!, _content = null!, _modal = null!;
    private Label _notice = null!;
    private ScrollContainer? _inputScroll;
    private readonly Dictionary<string, int> _scrolls = [];
    private string _renderedForm = "";
    private readonly Dictionary<string, Control> _inputRows = [];
    private bool _resultOpen;
    private SwordCraftTrial.Draft Draft => _drafts[_form];
    private int _crafted;
    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Theme = QuietRoomStyle.MakeTheme();
        Theme.SetColor("font_placeholder_color", "LineEdit", new Color("797d6e"));
        _model = new SwordCraftTrial();
        foreach (var f in _model.Parameters.GetProperty("forms").EnumerateObject()) _drafts[f.Name] = _model.Create(f.Name);
        foreach (var n in _model.Materials.Keys) _stock[n] = 30;
        var bg = new TextureRect { Texture = GD.Load<Texture2D>("res://textures/ui/forging_20261021/ink_workshop.png"), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(bg); bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _canvas = QuietRoomStyle.Canvas(this);
        Refresh();
        if (OS.GetCmdlineUserArgs().Contains("--audit-forging")) CallDeferred(nameof(Audit));
    }
    private void Refresh()
    {
        if (_inputScroll is not null && IsInstanceValid(_inputScroll)) _scrolls[_renderedForm] = _inputScroll.ScrollVertical;
        if (_content is not null) { _canvas.RemoveChild(_content); _content.QueueFree(); }
        _content = new Control { Size = new Vector2(1672, 941), MouseFilter = MouseFilterEnum.Pass }; _canvas.AddChild(_content);
        var header = Paper(_content, 24, 18, 1624, 76, .97f);
        Put(header, Button("〈 返回", () => { if (GetParent() is BaigongPanel) QueueFree(); else GetTree().ChangeSceneToFile("res://tscn/scene/StartMenu.tscn"); }), 15, 12, 110, 48);
        Put(header, Text("炼 器", 34), 156, 13, 180, 50);
        Put(header, Text("剑类工坊  /  独立试炼", 19), 350, 27, 320, 32);
        Put(header, Text("炼器造诣", 19), 988, 28, 110, 32);
        var proficiency = new SpinBox { MinValue = 0, MaxValue = 100, Step = 1, Value = Draft.Proficiency, TooltipText = "调整本页试炼造诣，不影响人物" };
        proficiency.ValueChanged += value => { foreach (var d in _drafts.Values) d.Proficiency = value; Refresh(); };
        Put(header, proficiency, 1108, 17, 115, 42);
        Put(header, Text($"试炼成器 {_crafted} 件", 19), 1250, 28, 230, 32);
        Put(header, Button("说明", Help), 1488, 12, 106, 48);
        BuildInputs(); BuildCenter(); BuildLedger();
        _notice = Text("试炼材料每种 30 份 · 仅在本页使用，不消耗行囊，不生成正式装备", 17);
        Put(_content, _notice, 420, 905, 850, 28);
    }
    private void BuildInputs()
    {
        var panel = Paper(_content, 24, 112, 350, 784);
        Put(panel, Text("器形与投料", 25), 22, 13, 280, 36);
        var forms = new HBoxContainer(); forms.AddThemeConstantOverride("separation", 5);
        Put(panel, forms, 17, 60, 316, 44);
        foreach (var f in _drafts.Keys)
        {
            string name = f;
            var b = Button(name, () => { _form = name; _selected = Draft.Inputs[0].Key; Refresh(); });
            b.SizeFlagsHorizontal = SizeFlags.ExpandFill; b.AddThemeFontSizeOverride("font_size", 18);
            if (name == _form) ForgingInkStyle.Button(b, true);
            forms.AddChild(b);
        }
        string desc = _form switch { "细身剑" => "轻简器身 · 少量部件，适合专精", "阔刃剑" => "宽展刃面 · 加强直接攻伐转化", _ => "加厚剑脊 · 容纳更多组合方法" };
        Put(panel, Text(desc, 17), 22, 112, 310, 30);
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _inputScroll = scroll; _renderedForm = _form; _inputRows.Clear();
        Put(panel, scroll, 16, 153, 320, 552);
        var list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; list.AddThemeConstantOverride("separation", 5); scroll.AddChild(list);
        string group = "";
        foreach (var input in Draft.Inputs.Where(x => _model.Active(Draft, x)))
        {
            if (group != input.Group) { group = input.Group; list.AddChild(Text(group + (group == "和材" ? " · 按方法与通关要求投入" : group == "燃料" ? " · 不参与阴阳与效力" : ""), 18)); }
            var row = new PanelContainer(); row.AddThemeStyleboxOverride("panel", ForgingInkStyle.Paper(selected: _selected == input.Key, padding: 6, plain: true)); list.AddChild(row);
            _inputRows[input.Key] = row;
            var h = new HBoxContainer(); row.AddChild(h);
            var pick = Button("", () => { _selected = input.Key; Refresh(); Picker(input); });
            pick.CustomMinimumSize = new Vector2(190, 64); pick.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            pick.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
            string name = input.Material.Length > 0 ? input.Material : "选择材料";
            bool required = input.Group != "和材" && input.Min > 0;
            pick.Text = $"{input.Label} {(required ? "· 必需" : "· 可选")}\n{name}";
            pick.Alignment = HorizontalAlignment.Left; pick.AddThemeFontSizeOverride("font_size", 17);
            if (input.Material != "")
            {
                pick.Icon = GD.Load<Texture2D>(_model.Materials[input.Material].icon_path); pick.ExpandIcon = true;
                pick.AddThemeConstantOverride("icon_max_width", 42); pick.TooltipText = MaterialInfo(input.Material);
            }
            h.AddChild(pick);
            var qty = new VBoxContainer(); h.AddChild(qty);
            var counter = new HBoxContainer(); qty.AddChild(counter);
            var minus = Button("−", () => { if (input.Quantity > input.Min) input.Quantity--; Refresh(); }); minus.CustomMinimumSize = new Vector2(28, 30); counter.AddChild(minus);
            counter.AddChild(Text(input.Quantity.ToString(), 18));
            var plus = Button("+", () => { if (input.Quantity < input.Max) input.Quantity++; Refresh(); }); plus.CustomMinimumSize = new Vector2(28, 30); counter.AddChild(plus);
            var clear = Button("移除", () => { input.Material = ""; Refresh(); }); clear.AddThemeFontSizeOverride("font_size", 14); qty.AddChild(clear);
        }
        Put(panel, Button("载入参考投料", LoadReference), 21, 721, 172, 43);
        Put(panel, Button("清空本图", () => { double p = Draft.Proficiency; _drafts[_form] = _model.Create(_form); Draft.Proficiency = p; Refresh(); }), 204, 721, 124, 43);
        scroll.SetDeferred(ScrollContainer.PropertyName.ScrollVertical, _scrolls.GetValueOrDefault(_form));
    }
    private void BuildCenter()
    {
        var tabs = new HBoxContainer(); tabs.AddThemeConstantOverride("separation", 12); Put(_content, tabs, 405, 112, 830, 46);
        var a = Button("结构与投料", () => { _methods = false; Refresh(); }); var b = Button("功用与方法", () => { _methods = true; Refresh(); });
        a.SizeFlagsHorizontal = b.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        ForgingInkStyle.Button(a, !_methods); ForgingInkStyle.Button(b, _methods); tabs.AddChild(a); tabs.AddChild(b);
        if (_methods) BuildMethods(); else
        {
            var drawing = new SwordCraftDiagram { FormName = _form, MouseFilter = MouseFilterEnum.Ignore };
            var caption = Paper(_content, 444, 177, 755, 48, .88f);
            Put(caption, Text("器身结构 · 点击部件可定位左侧投料", 19), 18, 9, 700, 34);
            var blueprint = Paper(_content, 445, 290, 760, 235, .78f);
            Put(_content, drawing, 473, 256, 690, 330);
            int i = 0;
            foreach (var input in Draft.Inputs.Where(x => x.Group == "身材"))
            {
                float[] anchors = _form == "细身剑" ? [180, 560] : [140, 345, 560];
                float x = 473 + anchors[i] - 95;
                var bpart = Button($"{input.Label}  {(input.Material == "" ? "待投" : "已投")}", () => { _selected = input.Key; Refresh(); CallDeferred(nameof(RevealSelected)); Picker(input); });
                bpart.TooltipText = $"要求 {input.Min}–{input.Max} 份；允许：{string.Join("、", _model.Choices(Draft, input))}";
                bpart.AddThemeStyleboxOverride("normal", ForgingInkStyle.Paper(alpha: .95f, plain: true));
                if (_selected == input.Key) bpart.AddThemeColorOverride("font_color", QuietRoomStyle.Teal);
                Put(_content, bpart, x, 550, 190, 38); i++;
            }
        }
        var r = _model.Evaluate(Draft);
        var foot = Paper(_content, 405, 667, 840, 229, .96f);
        Put(foot, Text("配伍与工艺", 24), 22, 14, 360, 40);
        Put(foot, Text("根性 → 功用来源", 17), 22, 57, 370, 28);
        var relation = Text(r.Relations.Count > 0 ? string.Join("\n", r.Relations) : "投入质材与性材后，查看配伍路径", 18); relation.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        Put(foot, relation, 22, 91, 380, 108);
        Put(foot, Text("当前投入的阴阳倾向", 18), 444, 57, 360, 30);
        bool hasInput = Draft.Inputs.Any(x => _model.Active(Draft, x) && x.Group != "燃料" && x.Material != "");
        for (int j = 0; j < 7; j++)
        {
            var mark = Text(SwordCraftTrial.Polarities[j], 18); mark.HorizontalAlignment = HorizontalAlignment.Center;
            Put(foot, mark, 429 + j * 53, 126, 54, 34);
            bool current = hasInput && j - 3 == r.Polarity;
            var dot = new ColorRect { Color = current ? QuietRoomStyle.Teal : new Color("b5ac94"), MouseFilter = MouseFilterEnum.Ignore };
            Put(foot, dot, 450 + j * 53, 108, current ? 12 : 6, 6);
        }
        Put(foot, Text("允许至阴至阳 · 不要求归于中和", 16), 446, 181, 370, 30);
    }
    private void BuildMethods()
    {
        var panel = Paper(_content, 405, 172, 840, 466);
        Put(panel, Text("选定功用，再选择它如何生效", 25), 22, 16, 790, 40);
        Put(panel, Text("多种方法可并存；共用材料效力，组合越多，要求越高。", 17), 22, 60, 790, 30);
        int col = 0;
        foreach (var u in new[] { "破障", "固结" })
        {
            string utility = u; float x = 24 + col * 406;
            Put(panel, Text(u, 25), x, 109, 200, 34);
            Put(panel, Button(Draft.Primary == u ? "主用" : "设为主用", () => { Draft.Primary = utility; Refresh(); }), x + 210, 108, 150, 34);
            int i = 0;
            foreach (var m in _model.Parameters.GetProperty("methods").EnumerateObject().Where(m => SwordCraftTrial.S(m.Value, "utility") == u))
            {
                string id = m.Name, kind = SwordCraftTrial.S(m.Value, "kind");
                string label = kind switch { "passive" => "常驻加持", "rewrite" => "附加化法", "direct_attack" or "direct_shield" => "直接施展", _ => "召出御使" };
                string detail = kind switch { "passive" => "装备即生效", "rewrite" => u == "破障" ? "攻伐 · 气血 · 单体单段物理" : "守御 · 应对 · 已有化解", "direct_attack" => "独立技能 · 单体攻伐", "direct_shield" => "独立技能 · 自身护盾", _ => "共用器体 · 召出后逐次御使" };
                var check = new CheckButton { Text = label, ButtonPressed = Draft.Methods.Contains(id) };
                ForgingInkStyle.Button(check, Draft.Methods.Contains(id));
                check.Toggled += enabled => { if (enabled) Draft.Methods.Add(id); else Draft.Methods.Remove(id); Refresh(); };
                Put(panel, check, x, 151 + i * 75, 230, 38);
                Put(panel, Text(detail, 15), x, 194 + i * 75, 360, 26);
                if (Draft.Methods.Contains(id))
                {
                    var priority = Button(Draft.Priorities.GetValueOrDefault(id, "并重"), () => { Draft.Priorities[id] = Draft.Priorities.GetValueOrDefault(id) == "主用" ? "并重" : "主用"; Refresh(); }); priority.AddThemeFontSizeOverride("font_size", 16);
                    Put(panel, priority, x + 255, 152 + i * 75, 100, 32);
                }
                i++;
            }
            col++;
        }
    }
    private List<string> StockIssues(SwordCraftTrial.Result r) => r.Materials.Where(x => x.Value > _stock.GetValueOrDefault(x.Key)).Select(x => $"{x.Key}需 {x.Value}，余 {_stock.GetValueOrDefault(x.Key)}").ToList();
    private void BuildLedger()
    {
        var r = _model.Evaluate(Draft); var issues = r.Issues.Concat(StockIssues(r)).ToArray();
        var panel = Paper(_content, 1276, 112, 372, 784, .98f);
        Put(panel, Text("开炼准备", 28), 24, 20, 310, 45);
        Put(panel, Text($"部件完成　{r.Completed} / {r.Required}\n工艺承载　{r.Load} / {r.Capacity}\n造诣要求　{r.Difficulty}\n供热　　　{r.OfferedHeat} / {r.Heat}", 21), 24, 83, 322, 158);
        Put(panel, Text($"预计耗时　{r.Hours:0.##} 时辰\n精力用度　{r.Energy}\n本次用料　{r.Materials.Values.Sum()} 份", 21), 24, 255, 320, 110);
        Put(panel, Text(issues.Length == 0 ? "投料与工艺已备齐" : "尚需处理", 23), 24, 387, 320, 34);
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; Put(panel, scroll, 24, 431, 324, 218);
        var message = Text(issues.Length == 0 ? string.Join("\n", r.Materials.Select(x => $"{x.Key} ×{x.Value}　/ 余{_stock.GetValueOrDefault(x.Key)}")) : string.Join("\n", issues.Select(x => "· " + x)), 18);
        message.AutowrapMode = TextServer.AutowrapMode.WordSmart; message.CustomMinimumSize = new Vector2(302, 0); scroll.AddChild(message);
        var craft = Button("开炼 · 试炼", Craft); craft.Disabled = issues.Length > 0; ForgingInkStyle.Button(craft, true); craft.AddThemeFontSizeOverride("font_size", 25); Put(panel, craft, 24, 671, 324, 56);
        Put(panel, Text("候选工艺 · 不写入正式存档", 16), 40, 738, 300, 27);
    }
    private string MaterialInfo(string n)
    {
        var m = _model.Materials[n]; var c = _model.Craft(n);
        return $"{n}　{c.material_grade_name()}\n{string.Join("　", m.tags)}\n{c.describe()}\n试炼库存 {_stock.GetValueOrDefault(n)} 份\n{m.description}";
    }
    private Control Modal(string heading, out VBoxContainer body)
    {
        CloseModal(); _modal = new Control { Size = new Vector2(1672, 941), ZIndex = 50 }; _canvas.AddChild(_modal);
        Put(_modal, new ColorRect { Color = new Color(0.08f, .12f, .11f, .68f) }, 0, 0, 1672, 941);
        var panel = Paper(_modal, 395, 155, 882, 630, 1);
        Put(panel, Text(heading, 29), 28, 20, 710, 45);
        Put(panel, Button("关闭", CloseModal), 748, 22, 100, 42);
        body = new VBoxContainer(); body.AddThemeConstantOverride("separation", 12); Put(panel, body, 28, 86, 826, 518);
        return panel;
    }
    private void CloseModal() { _resultOpen = false; if (_modal is not null) { _modal.GetParent()?.RemoveChild(_modal); _modal.QueueFree(); _modal = null!; } }
    private void RevealSelected() { if (_inputScroll is not null && _inputRows.TryGetValue(_selected, out var row)) _inputScroll.EnsureControlVisible(row); }
    private void Picker(SwordCraftTrial.Input input)
    {
        Modal($"{input.Group} · {input.Label}", out var box);
        box.AddChild(Text($"每槽 {input.Min}–{input.Max} 份 · 同种材料跨槽合并核对库存", 18));
        var search = new LineEdit { PlaceholderText = "查找可用材料" }; box.AddChild(search);
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; box.AddChild(scroll);
        var grid = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill }; grid.AddThemeConstantOverride("h_separation", 10); grid.AddThemeConstantOverride("v_separation", 12); scroll.AddChild(grid);
        void Populate(string filter)
        {
            foreach (var child in grid.GetChildren()) { grid.RemoveChild(child); child.QueueFree(); }
            foreach (var n in _model.Choices(Draft, input).Where(n => n.Contains(filter, StringComparison.OrdinalIgnoreCase)))
            {
                string name = n;
                var b = Button($"{n}\n余 {_stock.GetValueOrDefault(n)} 份", () => { input.Material = name; CloseModal(); Refresh(); });
                b.CustomMinimumSize = new Vector2(260, 100); b.Icon = GD.Load<Texture2D>(_model.Materials[n].icon_path); b.ExpandIcon = true; b.AddThemeConstantOverride("icon_max_width", 64); b.TooltipText = MaterialInfo(n); grid.AddChild(b);
            }
        }
        search.TextChanged += Populate; Populate(""); search.GrabFocus();
    }
    private void Help()
    {
        Modal("炼器 · 工坊手记", out var body);
        var text = Text("一、器形决定真实部件槽，切换器形会保留各自草稿。\n\n二、最多配置破障、固结两项功用。同一功用可以同时采用常驻、化法、直施、御使等已定义方法；可用方法各有条件。\n\n三、共用效力先满足各方法最低需求，再按主用／并重分配。添加方法不会复制已有强度。\n\n四、化法无需在局内施放；独立技能才占技能栏。御器的召唤与御使各占行动，共用一件器体。\n\n五、当前是候选工艺试炼，每种材料独立提供30份；不会消耗行囊，也不会向正式存档赠送法宝。", 21);
        text.AutowrapMode = TextServer.AutowrapMode.WordSmart; body.AddChild(text);
    }
    private void Craft()
    {
        if (_resultOpen) return;
        var r = _model.Evaluate(Draft);
        if (!r.Valid || StockIssues(r).Count > 0) { Refresh(); return; }
        foreach (var (n, q) in r.Materials) _stock[n] -= q;
        double proficiency = Draft.Proficiency + r.Growth;
        foreach (var d in _drafts.Values) d.Proficiency = proficiency;
        _crafted++; Refresh();
        Modal($"试炼成器 · {_form}", out var body);
        body.AddChild(Text($"相性：{SwordCraftTrial.Polarities[r.Polarity + 3]}　　器蕴：{(Draft.Primary == "固结" && _model.Utilities(Draft).Contains("固结") ? "凝固" : _model.Utilities(Draft).Contains("破障") ? "锋锐" : "凝固")}一层", 23));
        foreach (var (method, value) in r.Effects) { body.AddChild(Text(method, 23)); body.AddChild(Text(_model.DescribeEffect(method, value), 20)); }
        if (r.Spirit > 0) body.AddChild(Text($"共用器体 {r.BodyHp} · 在场灵识 {r.Spirit}\n召唤：体力1、法力1；御使：体力1、法力0。", 18));
        body.AddChild(Text($"试炼用料已扣除，共 {r.Materials.Values.Sum()} 份。\n此结果仅用于工艺验证，未生成正式装备或战斗技能。", 18));
        _resultOpen = true;
    }
    private void LoadReference()
    {
        double proficiency = Draft.Proficiency;
        _drafts[_form] = _model.Create(_form); var d = Draft; d.Proficiency = proficiency;
        d.Methods.Add(_form == "阔刃剑" ? "破障直施" : "破障化法");
        foreach (var input in d.Inputs.Where(x => x.Group == "身材")) input.Material = _model.Choices(d, input)[0];
        d.Inputs.First(x => x.Key == "root").Material = "铜髓矿";
        d.Inputs.First(x => x.Key == "破障").Material = "裂甲石";
        var aux = d.Inputs.First(x => x.Group == "和材"); aux.Material = _form == "阔刃剑" ? "灵石" : "风纹石英";
        var fuel = d.Inputs.First(x => x.Key == "松心炭"); fuel.Material = "松心炭"; fuel.Quantity = 4;
        if (_model.Evaluate(d).OfferedHeat < _model.Evaluate(d).Heat) { var boost = d.Inputs.First(x => x.Key == "地脉焰炭"); boost.Material = "地脉焰炭"; boost.Quantity = 1; }
        Refresh();
    }
    private async void Audit()
    {
        try
        {
        int checks = 0;
        void Check(bool condition, string name) { if (!condition) throw new InvalidOperationException("FORGING_AUDIT_FAIL " + name); checks++; }
        CheckReferenceFixtures(Check);
        foreach (var f in _drafts.Keys.ToArray()) { _form = f; LoadReference(); Check(_model.Evaluate(Draft).Valid, f + "参考投料"); }
        _form = "细身剑"; LoadReference(); var valid = _model.Evaluate(Draft);
        Check(valid.Polarity is >= -3 and <= 3, "七档阴阳");
        Draft.Methods.Add("破障御使"); Check(!_model.Evaluate(Draft).Valid, "复合超载"); Draft.Methods.Remove("破障御使");
        Check(_model.Evaluate(Draft).Valid, "撤销方法重算");
        var old = _stock["玄铁"]; _stock["玄铁"] = 0; Check(StockIssues(valid).Count > 0, "合并库存不足"); _stock["玄铁"] = old;
        Craft(); Check(_crafted == 1 && _stock["玄铁"] == old - 2, "独立扣料"); Craft(); Check(_crafted == 1 && _stock["玄铁"] == old - 2, "重复提交不扣料");
        await CapturePage("实际界面_成器.png"); CloseModal();
        string draftMaterial = Draft.Inputs[0].Material; _form = "厚脊剑"; _form = "细身剑"; Check(Draft.Inputs[0].Material == draftMaterial, "切形草稿保留");
        await CheckControls(Check);
        Input.WarpMouse(new Vector2(1660, 940)); Refresh();
        await CapturePage("实际界面_结构.png");
        _methods = true; Refresh();
        await CapturePage("实际界面_方法.png");
        Picker(Draft.Inputs.First(x => x.Key == "root")); await CapturePage("实际界面_选材.png"); CloseModal();
        _form = "厚脊剑"; _methods = false; _drafts[_form] = _model.Create(_form); Refresh(); await CapturePage("实际界面_缺料.png");
        LoadReference(); await CapturePage("实际界面_厚脊.png");
        GD.Print($"FORGING_AUDIT_OK {checks}"); Input.SetCustomMouseCursor(null); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); Input.SetCustomMouseCursor(null); GetTree().Quit(1); }
    }
    private static Label Text(string text, int size = 20) => QuietRoomStyle.Text(text, size);
    private static Button Button(string text, Action action) { var b = new Button { Text = text, MouseDefaultCursorShape = CursorShape.PointingHand }; ForgingInkStyle.Button(b); b.Pressed += action; return b; }
    private static void Put(Control parent, Control node, float x, float y, float w, float h) { parent.AddChild(node); node.Position = new Vector2(x, y); node.Size = new Vector2(w, h); }
    private static Control Paper(Control parent, float x, float y, float w, float h, float alpha = .94f)
    {
        var p = new Panel { MouseFilter = MouseFilterEnum.Pass }; p.AddThemeStyleboxOverride("panel", ForgingInkStyle.Paper(h > w * 1.3f, alpha, padding: 0, plain: w / h > 7)); Put(parent, p, x, y, w, h); QuietRoomStyle.Frame(p); return p;
    }
}

/// <summary>独立水墨剑形与部件指示层；交互标注不烘焙进素材。</summary>
public partial class SwordCraftDiagram : Control
{
    public string FormName = "细身剑";
    public override void _Draw()
    {
        var ink = new Color("41514dc9");
        var sword = ForgingInkStyle.Texture(ForgingInkStyle.SwordFile(FormName));
        DrawTextureRect(sword, new Rect2(35, 104, 610, 122), false);
        foreach (float x in FormName == "细身剑" ? new[] { 180f, 560f } : new[] { 140f, 345f, 560f }) { DrawLine(new Vector2(x, 195), new Vector2(x, 294), ink, 1, true); DrawCircle(new Vector2(x, 195), 4, QuietRoomStyle.Teal); }
    }
}
