using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using static TalismanCraftingCatalog;
using Service = TalismanCraftingService;

/// <summary>地点内的符工页面。所有选择来自人物实际行囊，结算交由同一三工艺服务。</summary>
public partial class TalismanCraftingScene : Control
{
    public Character? OwnerCharacter { get; set; }
    public string ActiveMode => _mode;
    private const string Art = "res://textures/ui/talisman_crafting_20261108/";
    private Control _canvas = null!, _page = null!;
    private HBoxContainer _modeTabs = null!;
    private Control? _modal;
    private Label _attainment = null!, _toast = null!;
    private Label _spiritHeaderValue = null!, _energyHeaderValue = null!;
    private string _mode = "paper", _paperMethod = "yangmai", _inkMethod = "junyan", _writing = "common";
    private string _paper = "", _brush = "", _recipe = "hushen";
    private readonly Dictionary<string, int> _paperMaterials = new(), _inkMaterials = new(), _inks = new();
    private readonly HashSet<string> _retained = new();
    private int _quantity = 1;
    private bool _busy, _refreshQueued;
    private int _noticeVersion;
    private readonly Dictionary<string, Button> _tabs = new();

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        Theme = CraftingPaperStyle.MakeTheme();
        var background = new TextureRect { Texture = GD.Load<Texture2D>(Art + "desk_v1.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, MouseFilter = MouseFilterEnum.Ignore,
            Modulate = new Color(.89f, .91f, .85f) };
        AddChild(background); background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _canvas = new Control { Name = "TalismanCanvas", Size = new Vector2(1672, 941), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_canvas);
        BuildHeader();
        _page = new Control { Name = "CraftingPage", MouseFilter = MouseFilterEnum.Ignore };
        _canvas.AddChild(_page);
        BuildModeTabs();
        _toast = Text("", 22); _toast.Name = "CraftingNotice"; _toast.HorizontalAlignment = HorizontalAlignment.Center;
        Place(_canvas, _toast, 448, 704, 750, 31);
        Resized += FitCanvas;
        FitCanvas(); Refresh();
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }) return;
        if (_busy) { GetViewport().SetInputAsHandled(); return; }
        if (_modal is not null) CloseModal(); else if (!_busy) ReturnToLocation();
        GetViewport().SetInputAsHandled();
    }

    private void FitCanvas()
    {
        if (_canvas is null) return;
        float scale = Mathf.Min(Size.X / 1672f, Size.Y / 941f);
        _canvas.Scale = Vector2.One * scale;
        _canvas.Position = (Size - new Vector2(1672, 941) * scale) * .5f;
    }

    public void ReturnToLocation()
    {
        if (_busy) return;
        QueueFree();
    }

    public void SetMode(string mode)
    {
        if (_busy || mode is not ("paper" or "ink" or "drawing")) return;
        _mode = mode; _toast.Text = ""; CloseModal(); Refresh();
    }

    public Service.Request CurrentRequest() => new()
    {
        Mode = _mode,
        Materials = (_mode == "paper" ? _paperMaterials : _inkMaterials).Select(p => new Selection(p.Key, p.Value)).ToList(),
        TechniqueId = _mode == "paper" ? _paperMethod : _inkMethod,
        RetainedUtilities = _retained.OrderBy(n => Array.IndexOf(UtilityNames, n)).ToList(),
        RecipeId = _recipe, PaperPath = _paper, InkSelections = _inks.Select(p => new Selection(p.Key, p.Value)).ToList(),
        BrushPath = _brush, Quantity = _quantity, WritingId = _writing
    };

    private static readonly string[] UtilityNames = ["固", "聚", "通", "化", "引", "破", "护", "镇", "生", "明", "藏", "行"];
    private Dictionary<string, int> Materials => _mode == "paper" ? _paperMaterials : _inkMaterials;

    public void SelectMaterial(string path, int quantity)
    {
        var entry = DiscoverMaterials().FirstOrDefault(m => m.Path == path);
        if (entry is null || !IsEligible(entry, _mode)) return;
        _toast.Text = "";
        quantity = Math.Clamp(quantity, 0, Math.Min(99, entry.Stock(OwnerCharacter)));
        if (quantity == 0) Materials.Remove(path); else Materials[path] = quantity;
        SyncRetention(); RequestRefresh();
    }

    private void SyncRetention()
    {
        if (_mode != "ink") return;
        var available = Analyze("ink", _inkMaterials.Select(p => new Selection(p.Key, p.Value)), OwnerCharacter).AvailableUtilities.Select(u => u.Name).ToHashSet();
        _retained.IntersectWith(available);
        if (_retained.Count == 0)
            foreach (var name in UtilityNames.Where(available.Contains).Take(4)) _retained.Add(name);
    }

    private void RequestRefresh()
    {
        if (_refreshQueued || !IsInsideTree()) return;
        _refreshQueued = true;
        Callable.From(() => { _refreshQueued = false; if (IsInsideTree() && !_busy) Refresh(); }).CallDeferred();
    }

    private void Refresh()
    {
        foreach (var child in _page.GetChildren()) { _page.RemoveChild(child); child.QueueFree(); }
        foreach (var (mode, tab) in _tabs) CraftingPaperStyle.ApplyButton(tab, fontSize: 22, selected: mode == _mode);
        RefreshHeaderValues();
        var art = CentralIllustration();
        art.Name = "CentralArt";
        Place(_page, art, 448, 150, 750, _mode == "ink" ? 492 : 552);
        if (_mode == "drawing") BuildDrawingLeft(); else BuildMaterialLeft();
        BuildRight(); BuildNotes();
        if (_mode == "ink") BuildRetention();
    }

    private void RefreshHeaderValues()
    {
        _attainment.Text = $"{Math.Max(0, (OwnerCharacter?.entity_data as HumanData)?.talisman_proficiency ?? 0):0.#}";
        _spiritHeaderValue.Text = OwnerCharacter is null ? "—" : $"{Math.Max(0, OwnerCharacter.max_spirit_perception - OwnerCharacter.used_spirit_perception)}/{OwnerCharacter.max_spirit_perception}";
        _energyHeaderValue.Text = OwnerCharacter is null ? "—" : $"{OwnerCharacter.current_world_stamina:0.#}";
    }

    private void BuildHeader()
    {
        var body = CraftingPaperHeader.Create(_canvas, "TalismanHeader");
        var help = Button("符工说明", OpenHelp, 22); help.Name = "TalismanHelpButton";
        CraftingPaperHeader.Place(body, help, CraftingPaperHeader.Cell.Help);
        _spiritHeaderValue = CraftingPaperHeader.Metric(body, CraftingPaperHeader.Cell.Spirit, "灵识", "—");
        _energyHeaderValue = CraftingPaperHeader.Metric(body, CraftingPaperHeader.Cell.Energy, "精力", "—");
        _attainment = CraftingPaperHeader.Metric(body, CraftingPaperHeader.Cell.Attainment, "符道造诣", "");
        var plans = Button("配伍册", OpenPlanBook, 22); plans.Name = "PlanBookButton";
        CraftingPaperHeader.Place(body, plans, CraftingPaperHeader.Cell.Book1);
        var book = Button("符工录", OpenKnowledge, 22); book.Name = "KnowledgeButton";
        CraftingPaperHeader.Place(body, book, CraftingPaperHeader.Cell.Book2);
        var back = Button("返回", ReturnToLocation, 22); back.Name = "ReturnButton";
        _canvas.AddChild(back); CraftingPaperHeader.PlaceReturn(_canvas, back);
        var arrow = new ReturnArrow(); Place(back, arrow, 88, 19, 7, 8);
    }

    private void BuildModeTabs()
    {
        _modeTabs = new HBoxContainer { Name = "TalismanModeTabs" };
        _modeTabs.AddThemeConstantOverride("separation", 8);
        Place(_canvas, _modeTabs, 48, 138, 352, 46);
        foreach (var (mode, name) in new[] { ("paper", "制符纸"), ("ink", "研墨"), ("drawing", "画符") })
        {
            var button = Button(name, () => SetMode(mode), 22);
            button.Name = "CraftTab_" + mode; button.CustomMinimumSize = new Vector2(112, 46);
            _modeTabs.AddChild(button); _tabs[mode] = button;
        }
    }

    private void BuildMaterialLeft()
    {
        var left = Paper(_page, 24, 118, 400, 797); left.Name = "TalismanLeftPanel";
        var heading = Text("本批材料", 28); heading.Name = "TalismanLeftHeading";
        Place(left, heading, 24, 86, 210, 42);
        var add = Button("选材", OpenMaterialPicker, 21); add.Name = "SelectMaterialsButton";
        Place(left, add, 272, 86, 104, 42);
        var list = Scroll(left, 24, 144, 352, 571);
        foreach (var (path, count) in Materials.ToArray())
        {
            var material = DiscoverMaterials().FirstOrDefault(m => m.Path == path);
            if (material is null)
            {
                var missing = new Control { Name = "MaterialCard_Missing_" + list.GetChildCount(), CustomMinimumSize = new Vector2(337, 114) };
                list.AddChild(missing);
                Place(missing, Text("材料已失效", 23), 12, 8, 313, 35);
                Place(missing, Text($"需要 {count} 份", 20, true), 12, 53, 204, 36);
                var discard = Button("移除", () => { Materials.Remove(path); SyncRetention(); RequestRefresh(); }, 19);
                discard.Name = "RemoveMissingMaterial_" + list.GetChildCount(); Place(missing, discard, 247, 53, 78, 36);
                continue;
            }
            var card = new Control { Name = "MaterialCard_" + material.Id, CustomMinimumSize = new Vector2(337, 166) }; list.AddChild(card);
            card.AddChild(new ColorRect { Color = new Color(.53f, .64f, .48f, .17f), Size = new Vector2(337, 164), MouseFilter = MouseFilterEnum.Ignore });
            Place(card, Image(material.IconPath), 7, 9, 85, 100);
            var name = Button(material.Name, () => OpenMaterialDetails(material), 24); ClearStyle(name);
            Place(card, name, 98, 5, 228, 37);
            Place(card, Text($"{material.ElementText} · {material.Texture} · {material.Stock(OwnerCharacter)}份", 18, true), 100, 45, 233, 28);
            var util = Text(material.UtilityText, 18, true); util.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            Place(card, util, 100, 73, 227, 43);
            Place(card, Text("用量", 20, true), 14, 121, 65, 36);
            AddStepper(card, "MaterialDose_" + material.Id, count, 98, 121, value => SelectMaterial(path, value), Math.Min(99, material.Stock(OwnerCharacter)));
            var remove = Button("移除", () => SelectMaterial(path, 0), 18); Place(card, remove, 263, 121, 61, 35);
        }
        if (Materials.Count == 0)
        {
            var empty = Text(_mode == "paper" ? "选取纸料" : "选取墨料", 27, true); empty.HorizontalAlignment = HorizontalAlignment.Center;
            empty.CustomMinimumSize = new Vector2(320, 185); list.AddChild(empty);
        }
        Line(left, 24, 733, 352);
        Place(left, Text($"{Materials.Count} 味 · {Materials.Values.Sum()} 份", 20, true), 24, 750, 230, 36);
        var clear = Button("清空", () => { Materials.Clear(); _retained.Clear(); RequestRefresh(); }, 19); ClearStyle(clear);
        Place(left, clear, 302, 750, 74, 36);
    }

    private void BuildRetention()
    {
        var bar = Paper(_page, 470, 649, 706, 59, false, .96f);
        Place(bar, Text("留墨功用", 20, true), 13, 13, 103, 33);
        var analysis = Analyze("ink", _inkMaterials.Select(p => new Selection(p.Key, p.Value)), OwnerCharacter);
        float x = 124;
        foreach (var util in analysis.AvailableUtilities)
        {
            string name = util.Name;
            var chip = Button(name, () =>
            {
                if (!_retained.Remove(name)) { if (_retained.Count >= 4) { Notice("最多保留四项功用。", true); return; } _retained.Add(name); }
                RequestRefresh();
            }, 22);
            chip.Name = "Retain_" + name; chip.TooltipText = util.GradeDisplay;
            CraftingPaperStyle.ApplyButton(chip, fontSize: 22, selected: _retained.Contains(name));
            Place(bar, chip, x, 10, 43, 39); x += 46;
        }
    }

    private void BuildRight()
    {
        var right = Paper(_page, 1234, 118, 414, 797);
        Place(right, Text(_mode == "drawing" ? "行符写法" : _mode == "paper" ? "制纸技艺" : "研墨技艺", 28), 24, 24, 366, 42);
        var methods = Service.Methods(_mode);
        float y = 82;
        foreach (var method in methods)
        {
            bool known = Service.IsMethodKnown(OwnerCharacter, method.Id);
            string selected = _mode == "drawing" ? _writing : _mode == "paper" ? _paperMethod : _inkMethod;
            var button = Button("", () =>
            {
                if (!Service.IsMethodKnown(OwnerCharacter, method.Id)) { OpenKnowledge(method.Id); return; }
                if (_mode == "drawing") _writing = method.Id; else if (_mode == "paper") _paperMethod = method.Id; else _inkMethod = method.Id;
                RequestRefresh();
            }, 23);
            button.Name = "Technique_" + method.Id;
            button.TooltipText = method.Description + "\n" + method.Tradeoff;
            CraftingPaperStyle.ApplyButton(button, fontSize: 23, selected: selected == method.Id);
            float height = _mode == "drawing" ? 74 : 132;
            Place(right, button, 24, y, 366, height);
            Place(button, Text(method.Name, 25), 17, 9, 250, 36);
            var mark = Text(known ? selected == method.Id ? "已选" : "" : "研习", 18, true);
            mark.HorizontalAlignment = HorizontalAlignment.Right; Place(button, mark, 269, 13, 77, 29);
            var brief = Text(method.Description, _mode == "drawing" ? 18 : 20, true);
            brief.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            Place(button, brief, 17, 46, 334, _mode == "drawing" ? 24 : 42);
            if (_mode != "drawing")
            {
                var trade = Text(method.Tradeoff, 18, true); trade.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                Place(button, trade, 17, 92, 334, 31);
            }
            if (selected == method.Id)
                foreach (var label in button.GetChildren().OfType<Label>())
                    label.AddThemeColorOverride("font_color", new Color("173c2c"));
            y += height + (_mode == "drawing" ? 8 : 11);
        }
        if (_mode == "drawing")
        {
            Place(right, Text("制符上限", 21), 24, 515, 130, 36);
            AddStepper(right, "DrawingQuantity", _quantity, 205, 515, value => { _quantity = Math.Max(1, value); RequestRefresh(); }, 99, 1);
        }
        var quote = Service.Evaluate(OwnerCharacter, CurrentRequest());
        Line(right, 24, 603, 366);
        Place(right, Text("本次用度", 24), 24, 620, 340, 36);
        Place(right, Text("精力", 22, true), 24, 665, 64, 35);
        var energy = Text($"{quote.EnergyCost:0.#}", 22); energy.Name = "CraftEnergyValue";
        energy.HorizontalAlignment = HorizontalAlignment.Right; Place(right, energy, 88, 665, 67, 35);
        Place(right, Text("耗时", 22, true), 183, 665, 64, 35);
        var duration = Text($"{quote.DurationHours:0.##} 时辰", 22); duration.Name = "CraftDurationValue";
        duration.HorizontalAlignment = HorizontalAlignment.Right; Place(right, duration, 247, 665, 143, 35);
        Place(right, Text("灵识", 22, true), 24, 703, 64, 31);
        var spirit = Text(quote.SpiritNeed.ToString(), 22); spirit.Name = "CraftSpiritValue";
        spirit.HorizontalAlignment = HorizontalAlignment.Right; Place(right, spirit, 88, 703, 67, 31);
        var start = Button(_mode == "paper" ? "凝制符纸" : _mode == "ink" ? "研成符墨" : "开始画符", StartCraft, 27);
        start.Name = "StartCraftButton"; start.Disabled = !quote.CanStart || OwnerCharacter is null || _busy;
        CraftingPaperStyle.ApplyButton(start, CraftingPaperStyle.ButtonKind.Primary, 27);
        Place(right, start, 24, 738, 366, 54);
    }

    private void BuildNotes()
    {
        var panel = Paper(_page, 448, 735, 750, 180);
        Place(panel, Text("配伍", 24), 24, 16, 400, 36);
        var more = Button("详看", OpenRelations, 19); ClearStyle(more); Place(panel, more, 648, 16, 78, 36);
        var quote = Service.Evaluate(OwnerCharacter, CurrentRequest());
        var notes = quote.Blockers.Concat(quote.Notes).Distinct().Take(3).ToList();
        if (notes.Count == 0) notes.Add("配伍可行。" );
        float y = 57;
        foreach (string note in notes)
        {
            float height = note.Length > 32 ? 54 : 34;
            if (y + height > 176) break;
            var label = Text("· " + note, 20, true); label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            if (quote.Blockers.Contains(note)) label.AddThemeColorOverride("font_color", CraftingPaperStyle.Danger);
            Place(panel, label, 23, y, 699, height); y += height + 2;
        }
    }

    private async void Notice(string message, bool danger = false)
    {
        int version = ++_noticeVersion;
        _toast.Text = message;
        _toast.AddThemeColorOverride("font_color", danger ? CraftingPaperStyle.Danger : CraftingPaperStyle.Accent);
        await ToSignal(GetTree().CreateTimer(2.6), SceneTreeTimer.SignalName.Timeout);
        if (GodotObject.IsInstanceValid(this) && IsInsideTree() && version == _noticeVersion) _toast.Text = "";
    }

    private static Label Text(string value, int size = 22, bool muted = false)
    {
        var label = CraftingPaperStyle.Text(value, size, muted);
        label.VerticalAlignment = VerticalAlignment.Center;
        return label;
    }
    private static T Place<T>(Control parent, T child, float x, float y, float w, float h) where T : Control
    {
        if (child is Label label)
        {
            label.ClipText = true;
            if (label.AutowrapMode != TextServer.AutowrapMode.Off)
            {
                // A wrapped label created at zero width otherwise keeps a tall minimum and centers outside its card.
                label.VerticalAlignment = VerticalAlignment.Top;
                float lineHeight = label.GetThemeFont("font").GetHeight(label.GetThemeFontSize("font_size"));
                label.MaxLinesVisible = Math.Max(1, (int)Math.Floor(h / Math.Max(1, lineHeight)));
                label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            }
        }
        // Like the alchemy/forging skins, the paper art must respect the control's intended hit box.
        if (child is Button button)
        {
            float textWidth = button.GetThemeFont("font").GetStringSize(button.Text, fontSize: button.GetThemeFontSize("font_size")).X;
            float textHeight = button.GetThemeFont("font").GetHeight(button.GetThemeFontSize("font_size"));
            foreach (string state in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled" })
            {
                var style = (StyleBox)button.GetThemeStylebox(state).Duplicate();
                style.ContentMarginLeft = style.ContentMarginRight = Math.Min(style.GetContentMargin(Side.Left), Math.Max(0, (w - textWidth) * .5f - 1));
                style.ContentMarginTop = style.ContentMarginBottom = Math.Min(style.GetContentMargin(Side.Top), Math.Max(0, (h - textHeight) * .5f));
                button.AddThemeStyleboxOverride(state, style);
            }
            button.CustomMinimumSize = new Vector2(Math.Min(button.CustomMinimumSize.X, w), Math.Min(button.CustomMinimumSize.Y, h));
        }
        parent.AddChild(child); child.Position = new Vector2(x, y); child.Size = new Vector2(w, h); return child;
    }
    private static Control Paper(Control parent, float x, float y, float w, float h, bool bordered = true, float brightness = CraftingPaperHeader.PaperBrightness)
    {
        var panel = new Panel { MouseFilter = MouseFilterEnum.Stop };
        panel.AddThemeStyleboxOverride("panel", CraftingPaperHeader.Paper(0, bordered, brightness));
        Place(parent, panel, x, y, w, h); return panel;
    }
    private static Button Button(string text, Action action, int size = 22)
    {
        var button = new Button { Text = text, MouseDefaultCursorShape = CursorShape.PointingHand };
        CraftingPaperStyle.ApplyButton(button, fontSize: size); button.Pressed += action; return button;
    }
    private static void ClearStyle(Button button)
    {
        foreach (string state in new[] { "normal", "hover", "pressed", "disabled", "focus" })
            button.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
    }
    private static TextureRect Image(string path)
    {
        Texture2D? texture = !string.IsNullOrWhiteSpace(path) && ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
        return new TextureRect { Texture = texture, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore };
    }

    private TextureRect CentralIllustration()
    {
        if (_mode == "paper") return Image(TalismanArtCatalog.PaperScenePath());
        string source = TalismanArtCatalog.PaintedRoot + "raw/" + (_mode == "ink" ? "ink" : _recipe == "yanshi" ? "yanshi" : "hushen") + ".png";
        var art = Image(ResourceLoader.Exists(source) ? source : Art + (_mode == "ink" ? "ink.svg" : "drawing.svg"));
        if (_mode == "drawing" && ResourceLoader.Exists(source))
        {
            string brushId = Service.OwnedProducts(OwnerCharacter, "brush").FirstOrDefault(b => b.Path == _brush)?.Snapshot.BrushId ?? "qingzhu";
            string brushPath = TalismanArtCatalog.PaintedRoot + "raw/brush_" + brushId + ".png";
            var brush = Image(brushPath);
            art.AddChild(brush);
            brush.AnchorLeft = .61f; brush.AnchorRight = .84f;
            brush.AnchorTop = .03f; brush.AnchorBottom = .97f;
        }
        return art;
    }
    private static void Line(Control parent, float x, float y, float width) => Place(parent, CraftingPaperStyle.Divider(), x, y, width, 2);
    private static VBoxContainer Scroll(Control parent, float x, float y, float width, float height)
    {
        var scroll = Place(parent, new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }, x, y, width, height);
        var list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; list.AddThemeConstantOverride("separation", 12); scroll.AddChild(list); return list;
    }
    private static void AddStepper(Control parent, string name, int value, float x, float y, Action<int> changed, int maximum = 99, int minimum = 0)
    {
        var minus = Button("−", () => changed(Math.Clamp(value - 1, minimum, Math.Max(minimum, maximum))), 21);
        minus.Name = name + "Minus"; Place(parent, minus, x, y, 39, 36);
        var input = new LineEdit { Name = name, Text = value.ToString(), Alignment = HorizontalAlignment.Center,
            ContextMenuEnabled = false, SelectAllOnFocus = true };
        CraftingPaperStyle.ApplyInput(input, fontSize: 22, horizontalPadding: 0, verticalPadding: 0);
        input.AddThemeConstantOverride("minimum_character_width", 1);
        input.CustomMinimumSize = Vector2.Zero;
        Place(parent, input, x + 43, y, 58, 36);
        void Commit() { if (int.TryParse(input.Text, out int parsed)) changed(Math.Clamp(parsed, minimum, Math.Max(minimum, maximum))); else input.Text = value.ToString(); }
        input.TextSubmitted += _ => Commit(); input.FocusExited += Commit;
        var plus = Button("+", () => changed(Math.Clamp(value + 1, minimum, Math.Max(minimum, maximum))), 21);
        plus.Name = name + "Plus"; Place(parent, plus, x + 105, y, 39, 36);
    }
    private sealed partial class ReturnArrow : Control
    {
        public ReturnArrow() { MouseFilter = MouseFilterEnum.Ignore; }
        public override void _Draw() => DrawColoredPolygon([Vector2.Zero, new Vector2(0, 8), new Vector2(7, 4)], CraftingPaperStyle.Ink);
    }
}
