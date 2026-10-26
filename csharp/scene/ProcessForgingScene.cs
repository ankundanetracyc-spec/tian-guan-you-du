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
    private int _inputGroup;
    private Character? _forgingOwner;
    private int _trialSpiritCapacity = 240;
    public void ReturnToBaigong()
    {
        if (!Visible) return; // 携器对练仍由对练页处理返回。
        if (_overlay is not null) { CloseOverlay(); return; }
        HoverInfoManager.HideAll();
        QueueFree();
    }
    public void BindOwner(Character owner) { _forgingOwner = owner; _draft.Proficiency = Math.Clamp((int)((owner.entity_data as HumanData)?.forging_proficiency ?? 0), 0, 100); }
    private int SpiritCapacity => _forgingOwner?.max_spirit_perception ?? _trialSpiritCapacity;
    private int OtherSpirit => _forgingOwner?.used_spirit_perception ?? 0;
    private int AvailableSpirit => Math.Max(0, SpiritCapacity - Math.Max(0, OtherSpirit));
    private void SyncOwnerStock()
    {
        if (_forgingOwner is null) return;
        _draft.Proficiency = Math.Clamp((int)((_forgingOwner.entity_data as HumanData)?.forging_proficiency ?? 0), 0, 100);
        foreach (var (name, count) in _forgingOwner.GetForgingStock(_model)) _session.Stock[name] = count;
    }
    private IReadOnlyList<ProcessForgingTrial.Artifact> OwnedArtifacts()
    {
        if (_forgingOwner is null) return _session.Artifacts;
        var items = new List<ProcessForgingTrial.Artifact>();
        foreach (var item in _forgingOwner.get_artifact_items())
            if (ForgedArtifactInventory.TryRead(item, out var snapshot)) items.Add(snapshot);
        return items;
    }
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
        if (OS.GetCmdlineUserArgs().Contains("--preview-sword-item")) CallDeferred(nameof(PreviewSwordItem));
    }
    private void Edit() { _revision++; Refresh(); }
    private void Refresh()
    {
        SyncOwnerStock();
        if (_inputScroll is not null && IsInstanceValid(_inputScroll)) _scroll = _inputScroll.ScrollVertical;
        if (_page is not null) { _canvas.RemoveChild(_page); _page.QueueFree(); }
        _buttons.Clear();
        _page = new Control { Size = new Vector2(1672, 941), MouseFilter = MouseFilterEnum.Pass }; _canvas.AddChild(_page);
        var header = Paper(_page, 24, 18, 1624, 76);
        Put(header, Btn("〈 返回", () => { if (_forgingOwner is not null) ReturnToBaigong(); else GetTree().ChangeSceneToFile("res://tscn/scene/StartMenu.tscn"); }), 15, 13, 106, 48);
        Put(header, Text("炼 器", 34), 150, 12, 150, 50);
        Put(header, Text("剑类 · 材料成器", 21), 312, 25, 325, 34);
        Put(header, Btn("参考投料", Examples), 665, 15, 165, 46);
        Put(header, Text("炼器造诣", 19), 867, 25, 115, 32);
        Put(header, Text(_draft.Proficiency.ToString(), 23), 984, 25, 112, 32);
        Put(header, Btn($"器物册 · {OwnedArtifacts().Count}", Gallery), 1130, 15, 210, 46);
        Put(header, Btn("炼器说明", Help), 1385, 15, 205, 46);
        BuildInputs(); BuildProcess(); BuildCosts();
    }
    private void Craft()
    {
        ProcessForgingTrial.Evaluation result;
        if (_forgingOwner is not null)
        {
            if (_forgingOwner.TryForge(_model, _draft, out result)) { _revision++; _session.Record(result, _draft); }
        }
        else result = _session.Submit(_model, _draft, _revision, SpiritCapacity, OtherSpirit);
        _notice = result.CanSubmit ? result.Success ? (_forgingOwner is null ? "本次成器已记入器物册。" : "本次成器已收入行囊。") : "本次未成器；已消耗投料与本次用度。" : string.Join("；", result.Errors);
        Refresh();
        if (result.Success) ShowArtifact(result.Product!, 0);
        else Message(result.CanSubmit ? "本次未成器" : "未能开炼", string.Join("\n", result.Errors.Concat(result.Failures)) + (result.CanSubmit ? $"\n\n本次用度：{result.Hours}时辰、{result.Energy}精力。" : "\n\n未扣除材料。"));
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
        Put(panel, Wrap("载入后将替换当前投料。可继续调整材料与份数，成器结果在炼后揭晓。", 18), 30, footer, 680, 80);
    }
    private void LoadExample(int index)
    {
        CloseOverlay(); _draft = _model.ExampleDraft(index);
        if (_forgingOwner?.entity_data is HumanData ownerData) _draft.Proficiency = Math.Clamp((int)ownerData.forging_proficiency, 0, 100);
        _scroll = 0; _notice = "已载入“" + _model.Parameters.Examples[index].Name + "”，可继续调整投料。"; Edit();
    }
    private string MaterialInfo(string name, string role)
    {
        var m = _model.Snapshot(name); var p = _model.Parameters.Profiles[name];
        return $"{name} · {m.Texture} · {m.Element.DefaultIfEmptyName()} · {ProcessForgingTrial.PolarityLabel(m.Polarity)}\n" + string.Join("、", m.Utilities.Select(x => x.Key + MaterialCraftData.utility_grade_name(x.Value))) +
            $"\n可用岗位：{string.Join("、", p.Roles.Select(RoleLabel))}\n加工难度{p.Difficulty} · 当前库存{_session.Stock[name]}\n本次岗位：{RoleLabel(role)}；材料在不同位置发挥不同作用。";
    }
    private static string RoleLabel(string role) => role is "主体" or "承接" or "复合身材" ? "身材／" + DisplayText(role) : role;
    // 保留现有工艺键及旧快照，只统一玩家看到的岗位名称。
    private static string DisplayText(string text) => text.Replace("复合身材", "辅体");
    private void Picker(string role, ProcessForgingTrial.Input? selected)
    {
        var panel = Overlay("选择" + RoleLabel(role) + "", 1030, 650);
        var search = new LineEdit { PlaceholderText = "搜索材料名" }; Put(panel, search, 28, 80, 500, 44);
        var elements = new OptionButton(); elements.AddItem("全部五行"); foreach (var value in new[] { "金", "木", "水", "火", "土", "无行" }) elements.AddItem(value); Put(panel, elements, 545, 80, 180, 44);
        var functions = new OptionButton(); functions.AddItem("全部功用"); foreach (var value in _model.Choices(role).SelectMany(name => _model.Snapshot(name).Utilities.Keys).Distinct().OrderBy(value => value)) functions.AddItem(value); Put(panel, functions, 740, 80, 260, 44);
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; Put(panel, scroll, 28, 142, 972, 420);
        var grid = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill }; grid.AddThemeConstantOverride("h_separation", 10); grid.AddThemeConstantOverride("v_separation", 12); scroll.AddChild(grid);
        void Fill()
        {
            foreach (var child in grid.GetChildren()) { grid.RemoveChild(child); child.QueueFree(); }
            foreach (string name in _model.Choices(role).Where(x => x.Contains(search.Text, StringComparison.OrdinalIgnoreCase)))
            {
                var p = _model.Parameters.Profiles[name];
                var fact = _model.Snapshot(name);
                if (elements.Selected > 0 && fact.Element.DefaultIfEmptyName() != elements.GetItemText(elements.Selected)) continue;
                if (functions.Selected > 0 && !fact.Utilities.ContainsKey(functions.GetItemText(functions.Selected))) continue;
                var remaining = Math.Max(0, _session.Stock[name] - _draft.Inputs.Where(input => input.Material == name && input != selected).Sum(input => input.Quantity));
                var button = Btn(name + $" · {fact.Element.DefaultIfEmptyName()}\n可用{remaining}份 · 加工难度{p.Difficulty}", () =>
                {
                    var existing = _draft.Inputs.FirstOrDefault(input => input.Role == role && input.Material == name && input != selected);
                    if (existing is not null)
                    {
                        existing.Quantity = Math.Min(8, existing.Quantity + Math.Min(remaining, selected?.Quantity ?? 1));
                        if (selected is not null) _draft.Inputs.Remove(selected);
                    }
                    else if (selected is null) _draft.Inputs.Add(new ProcessForgingTrial.Input { Role = role, Material = name });
                    else { selected.Material = name; selected.Quantity = Math.Min(selected.Quantity, remaining); }
                    CloseOverlay(); Edit();
                });
                button.Disabled = remaining == 0 || _draft.Inputs.Any(input => input != selected && input.Role == role && input.Material == name && input.Quantity >= 8);
                button.TooltipText = MaterialInfo(name, role); button.CustomMinimumSize = new Vector2(310, 90); button.AddThemeFontSizeOverride("font_size", 18);
                button.Icon = GD.Load<Texture2D>(_model.Materials[name].icon_path); button.ExpandIcon = true; button.AddThemeConstantOverride("icon_max_width", 56); grid.AddChild(button);
            }
        }
        search.TextChanged += _ => Fill(); elements.ItemSelected += _ => Fill(); functions.ItemSelected += _ => Fill(); Fill();
        Put(panel, Wrap("每份实物只承担当前岗位。可将同种材料的不同份数投入其他岗位，库存按整炉合计。", 18), 28, 580, 970, 55);
    }
    private void Gallery()
    {
        var panel = Overlay("器物册", 980, 700);
        var list = ScrollList(panel, 28, 80, 924, 590);
        var artifacts = OwnedArtifacts();
        if (artifacts.Count == 0) list.AddChild(Wrap("尚无成器。先选择参考投料或自行配料，开炼后再来查看。", 23));
        foreach (var artifact in artifacts)
        {
            var b = Btn(artifact.Name + " · " + artifact.Tier + "\n锋锐 " + artifact.Sharpness + " · " + ProcessForgingTrial.PolarityLabel(artifact.Polarity), () => ShowArtifact(artifact, 0));
            b.Name = "ForgedArtifactItem_" + artifact.InstanceId;
            b.Icon = ForgedArtifactPresentation.Icon(artifact); b.ExpandIcon = true; b.AddThemeConstantOverride("icon_max_width", 160);
            b.CustomMinimumSize = new Vector2(0, 85); b.SizeFlagsHorizontal = SizeFlags.ExpandFill; list.AddChild(b);
            HoverInfoManager.SetInfo(b, ForgedArtifactPresentation.Hover(artifact));
        }
        list.AddChild(Text("炼制记录", 24));
        foreach (var attempt in _session.Attempts.Reverse())
        {
            var b = Btn($"第{attempt.Number}次 · {(attempt.Success ? "成器" : "未成器")} · {attempt.Hours}时辰", () =>
                    Message("第" + attempt.Number + "次炼制记录", string.Join("\n", attempt.Inputs.Select(x => $"{RoleLabel(x.Role)} · {x.Material} ×{x.Quantity}")) + $"\n\n{attempt.Hours}时辰、{attempt.Energy}精力。\n" + (attempt.Success ? "成器已收入器物册。" : string.Join("\n", attempt.Failures))));
            b.CustomMinimumSize = new Vector2(0, 53); list.AddChild(b);
        }
    }
    public override void _UnhandledInput(InputEvent e)
    {
        if (_overlay is not null && e.IsActionPressed("ui_cancel")) { CloseOverlay(); GetViewport().SetInputAsHandled(); }
    }
    private void ShowArtifact(ProcessForgingTrial.Artifact artifact, int tab)
    {
        var panel = Overlay(artifact.Name + " · " + artifact.Tier + "", 1460, 810);
        var tabs = new HBoxContainer(); tabs.AddThemeConstantOverride("separation", 14); Put(panel, tabs, 28, 76, 1390, 46);
        string[] names = ["器物信息", "能力详情", "炼制记录"];
        for (int i = 0; i < names.Length; i++) { int index = i; var b = Btn(names[i], () => ShowArtifact(artifact, index)); b.CustomMinimumSize = new Vector2(210, 45); if (tab == i) ForgingInkStyle.Button(b, true); tabs.AddChild(b); }
        var trial = Btn("携器对练", () => OpenEquipmentTrial(artifact));
        trial.Name = "EnterEquipmentTrial"; tabs.AddChild(trial);
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
        }
        else
        {
            Add($"炼制造诣 {artifact.Proficiency}  ／  本次成器：{artifact.Tier}", 24);
            Add($"本次用度：{artifact.CraftingHours}时辰 / {artifact.CraftingEnergy}精力", 20);
            Add("材料五行来源：" + string.Join("、", artifact.MaterialElementContributions.Keys.OrderBy(x => x, StringComparer.Ordinal).DefaultIfEmpty("无行")), 20);
            foreach (var source in artifact.Sources.OrderBy(x => Array.IndexOf(ProcessForgingTrial.RoleOrder, x.Input.Role)))
                Add($"{RoleLabel(source.Input.Role)} · {source.Input.Material} ×{source.Input.Quantity}", 21);
        }
    }
    private void OpenEquipmentTrial(ProcessForgingTrial.Artifact artifact)
    {
        var preview = GD.Load<PackedScene>("res://tools/ForgedEquipmentPreview.tscn").Instantiate<ForgedEquipmentPreview>();
        preview.Configure(artifact);
        CloseOverlay(); Hide();
        preview.Closed += () => { Show(); ShowArtifact(artifact, 0); };
        GetTree().Root.AddChild(preview);
    }
    private void Help() => Message("炼器说明", "选择剑大类，配置材料及整份投料，开炼后查看结果。\n\n身材主要形成剑的本体与锋锐；质材主要建立灵性基础；性材主要提供角色收益；和材主要调和材料之间的相克。各岗位不是独占来源。\n\n材料五行用于炼制配伍，影响潜力保留与稳定性。材料相克时，可投入相应的和材调和。\n\n凡器具备本体性能和基础加成。器身能够稳定承载、维持并传导灵性效力时，可达到法器水准；是否有主动技能不是评阶依据。阴阳偏极也能正常成器。\n\n材料品阶和功用品级影响成器效力，炼器造诣影响材料的发挥。\n\n造诣20开放第二和材，32开放第二性材及辅体，50开放第三和材；位置解锁不等于器物等级。质材、性材及和材可按需选用。\n\n每个位置的同种材料可投入1—8份。材料品阶、功用品级与份数共同决定灵识需求；可支配灵识不足时无法开炼。" );
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





