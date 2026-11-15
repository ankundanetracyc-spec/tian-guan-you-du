using Godot;
using Godot.Collections;
using System;
using System.Linq;

/// <summary>Run-local quiet room for optional attribute training and paced manual study.</summary>
public partial class RoguelikeMeditationPanel : Control
{
    private Character _player = null!;
    private Dictionary _instance = [];
    private int _layer;
    private string _archetype = "sword";
    private Action? _changed;
    private VBoxContainer _content = null!;

    public static RoguelikeMeditationPanel ShowFor(Node owner, Character player, Dictionary instance,
        int layer, string archetype, Action changed)
    {
        var panel = new RoguelikeMeditationPanel
        {
            Name = "RoguelikeMeditationPanel",
            _player = player,
            _instance = instance,
            _layer = layer,
            _archetype = archetype,
            _changed = changed,
            MouseFilter = MouseFilterEnum.Stop,
            ZIndex = 240,
        };
        panel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        owner.AddChild(panel);
        panel.Build();
        return panel;
    }

    private void Build()
    {
        var veil = new ColorRect { Color = new Color(.02f, .025f, .02f, .72f), MouseFilter = MouseFilterEnum.Stop };
        veil.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(veil);

        var paper = new PanelContainer { Name = "MeditationPaper", CustomMinimumSize = new Vector2(900, 700), MouseFilter = MouseFilterEnum.Stop };
        paper.SetAnchorsPreset(LayoutPreset.Center);
        paper.OffsetLeft = -450; paper.OffsetTop = -350; paper.OffsetRight = 450; paper.OffsetBottom = 350;
        paper.AddThemeStyleboxOverride("panel", PaperStyle());
        AddChild(paper);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 14);
        paper.AddChild(root);
        var header = new HBoxContainer(); root.AddChild(header);
        var title = new Label { Text = $"秘境静室 · 第{_layer}层", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        title.AddThemeFontSizeOverride("font_size", 27); title.AddThemeColorOverride("font_color", Ink()); header.AddChild(title);
        var close = Button("离开静室", 17); close.Pressed += QueueFree; header.AddChild(close);
        var intro = new Label
        {
            Text = "每间静室可修习一项根基，并将三部秘籍读到本层所启卷次；第27层会补齐未读卷章。",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0, 48),
        };
        intro.AddThemeColorOverride("font_color", QuietInk()); root.AddChild(intro);
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        root.AddChild(scroll);
        _content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _content.AddThemeConstantOverride("separation", 12); scroll.AddChild(_content);
        Refresh();
    }

    private void Refresh()
    {
        if (_content is null) return;
        foreach (var child in _content.GetChildren()) { _content.RemoveChild(child); child.QueueFree(); }
        var room = RoomState();
        var stage = RoguelikeManualRules.ChapterStageForQuietRoom(_layer);
        var trained = room.GetValueOrDefault("trained", false).AsBool();
        AddSection("周天修习", trained ? "本间静室的根基修习已完成。" : "选择一项根基，提升 1 点；不消耗境界晋升次数。");
        var paths = new (string Id, string Label)[] { ("physique", "体魄"), ("intelligence", "灵慧"), ("soul", "神魂") };
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 9); _content.AddChild(row);
        foreach (var (id, label) in paths)
        {
            var button = Button(label + " +1", 17); button.SizeFlagsHorizontal = SizeFlags.ExpandFill; button.Disabled = trained;
            button.Pressed += () => Train(id);
            row.AddChild(button);
        }

        AddSection("秘籍研读", stage <= 0 ? "本层没有新的研读卷次。" : $"最多研读至第{ChineseNumber(stage)}卷；每卷沿用该秘籍现有的属性、造诣和战斗修持奖励。秘境研读不额外消耗时间或精力。");
        foreach (var bookPath in RoguelikeManualRules.BookPaths(_archetype))
        {
            var name = RoguelikeManualRules.BookName(bookPath);
            var learned = RoguelikeManualRules.LearnedChapterCount(_player, bookPath);
            var total = RoguelikeManualRules.BookChapterCount(bookPath);
            var line = new HBoxContainer(); line.AddThemeConstantOverride("separation", 12); _content.AddChild(line);
            var label = new Label { Text = $"《{name}》　{learned}/{total}卷", SizeFlagsHorizontal = SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center };
            label.AddThemeColorOverride("font_color", Ink()); label.AddThemeFontSizeOverride("font_size", 18); line.AddChild(label);
            var study = Button("研读至本层", 16);
            study.Disabled = stage <= 0 || learned >= Math.Min(total, stage);
            study.Pressed += () => Study(bookPath, stage);
            line.AddChild(study);
        }
        var studyAll = Button("全部研读至本层", 17);
        studyAll.Disabled = stage <= 0 || RoguelikeManualRules.BookPaths(_archetype).All(path =>
            RoguelikeManualRules.LearnedChapterCount(_player, path) >= Math.Min(RoguelikeManualRules.BookChapterCount(path), stage));
        studyAll.Pressed += () =>
        {
            var count = RoguelikeManualRules.StudyAllThroughStage(_player, _archetype, stage);
            if (count <= 0) return;
            _changed?.Invoke();
            Refresh();
            ShowNotice($"三部秘籍共完成{count}卷研读。");
        };
        _content.AddChild(studyAll);
    }

    private void Train(string path)
    {
        var room = RoomState();
        if (room.GetValueOrDefault("trained", false).AsBool() || !_player.apply_roguelike_meditation_attribute(path)) return;
        room["trained"] = true;
        _changed?.Invoke();
        Refresh();
    }

    private void Study(string bookPath, int stage)
    {
        var count = RoguelikeManualRules.StudyBookThroughStage(_player, bookPath, stage);
        if (count > 0) ShowNotice($"《{RoguelikeManualRules.BookName(bookPath)}》已完成{count}卷研读。");
        _changed?.Invoke();
        Refresh();
    }

    private Dictionary RoomState()
    {
        var rooms = _instance.GetValueOrDefault("roguelike_quiet_rooms", new Dictionary()).AsGodotDictionary();
        var key = _layer.ToString();
        if (!rooms.ContainsKey(key) || rooms[key].VariantType != Variant.Type.Dictionary)
            rooms[key] = new Dictionary();
        _instance["roguelike_quiet_rooms"] = rooms;
        return rooms[key].AsGodotDictionary();
    }

    private void AddSection(string title, string body)
    {
        var panel = new PanelContainer(); panel.AddThemeStyleboxOverride("panel", SectionStyle()); _content.AddChild(panel);
        var box = new VBoxContainer(); box.AddThemeConstantOverride("separation", 4); panel.AddChild(box);
        var heading = new Label { Text = title }; heading.AddThemeFontSizeOverride("font_size", 20); heading.AddThemeColorOverride("font_color", Ink()); box.AddChild(heading);
        var text = new Label { Text = body, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        text.AddThemeColorOverride("font_color", QuietInk()); text.AddThemeFontSizeOverride("font_size", 15); box.AddChild(text);
    }

    private void ShowNotice(string text)
    {
        var popup = new AcceptDialog { Title = "静室", DialogText = text };
        AddChild(popup); popup.Confirmed += popup.QueueFree; popup.Canceled += popup.QueueFree; popup.PopupCentered();
    }

    private static string ChineseNumber(int value) => value switch { 1 => "一", 2 => "二", 3 => "三", 4 => "四", _ => value.ToString() };
    private static Button Button(string text, int size)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(150, 42), FocusMode = FocusModeEnum.None };
        button.AddThemeFontSizeOverride("font_size", size);
        button.AddThemeColorOverride("font_color", Ink());
        button.AddThemeStyleboxOverride("normal", SectionStyle());
        button.AddThemeStyleboxOverride("hover", SectionStyle(new Color(.86f, .79f, .64f, .94f)));
        button.AddThemeStyleboxOverride("pressed", SectionStyle(new Color(.72f, .65f, .52f, .98f)));
        return button;
    }

    private static StyleBoxFlat PaperStyle()
    {
        var style = new StyleBoxFlat { BgColor = new Color("e5d8b7"), BorderColor = new Color("715e43"), CornerRadiusTopLeft = 12, CornerRadiusTopRight = 12, CornerRadiusBottomLeft = 12, CornerRadiusBottomRight = 12 };
        style.SetBorderWidthAll(2); style.ContentMarginLeft = 24; style.ContentMarginRight = 24; style.ContentMarginTop = 20; style.ContentMarginBottom = 20; return style;
    }
    private static StyleBoxFlat SectionStyle(Color? color = null)
    {
        var style = new StyleBoxFlat { BgColor = color ?? new Color(.91f, .86f, .73f, .72f), BorderColor = new Color("9d8865"), CornerRadiusTopLeft = 7, CornerRadiusTopRight = 7, CornerRadiusBottomLeft = 7, CornerRadiusBottomRight = 7 };
        style.SetBorderWidthAll(1); style.ContentMarginLeft = 12; style.ContentMarginRight = 12; style.ContentMarginTop = 8; style.ContentMarginBottom = 8; return style;
    }
    private static Color Ink() => new("3a3023");
    private static Color QuietInk() => new("5e523f");
}
