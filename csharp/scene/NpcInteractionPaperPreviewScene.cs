using Godot;

/// <summary>Isolated local NPC menu proposal. Reuses the formal renderer and menu lifecycle, without world actions.</summary>
public partial class NpcInteractionPaperPreviewScene : Control
{
    public const string BackgroundPath = "res://textures/baishi/interiors_20261105/inn_lobby.png";
    private readonly List<WorldNpcData> _people = [];
    private readonly List<Button> _rosterButtons = [];
    private Control _canvas = null!;
    private NpcInteractionPaperCardPreview _card = null!;
    private Tween? _opening;
    public bool ExitOnClose { get; set; } = true;
    public NpcInteractionPaperCardPreview Card => _card;
    public Control DesignCanvas => _canvas;
    public IReadOnlyList<WorldNpcData> People => _people;
    public int SelectedIndex { get; private set; }
    public int SceneClickCount { get; private set; }
    public string LastRequestedAction { get; private set; } = "";
    public string LastRequestedNpc { get; private set; } = "";
    public bool ExtendedActions { get; set; }
    public event Action? Closed;

    private static T Put<T>(Control parent, T child, float x, float y, float width, float height) where T : Control
    { parent.AddChild(child); child.Position = new(x, y); child.Size = new(width, height); return child; }
    private static Label Text(string caption, int size = 22, bool muted = false)
    {
        var label = CraftingPaperStyle.Text(caption, size, muted);
        label.VerticalAlignment = VerticalAlignment.Center; return label;
    }
    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); Theme = CraftingPaperStyle.MakeTheme();
        MouseFilter = MouseFilterEnum.Pass;
        _canvas = QuietRoomStyle.Canvas(this); _canvas.Name = "NpcPaperCanvas";
        var background = new TextureRect
        {
            Texture = GD.Load<Texture2D>(BackgroundPath), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, MouseFilter = MouseFilterEnum.Ignore
        };
        Put(_canvas, background, 0, 0, 1672, 941);
        // Input outside the local paper remains available, with no scene-wide mask.
        _canvas.GuiInput += input => { if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true }) SceneClickCount++; };
        _people.AddRange(CreateSamplePeople());
        BuildHeader(); BuildRoster();
        _card = new NpcInteractionPaperCardPreview { Name = "NpcPaperCard" }; _canvas.AddChild(_card);
        SelectNpc(0);
    }
    private void BuildHeader()
    {
        var place = new PanelContainer { Name = "NpcPlace" };
        var placeSkin = CraftingPaperStyle.Paper(12, true, .96f);
        placeSkin.ContentMarginTop = placeSkin.ContentMarginBottom = 0;
        place.AddThemeStyleboxOverride("panel", placeSkin);
        Put(_canvas, place, 24, 18, 280, 46);
        var label = Text("白石城 · 客栈"); label.HorizontalAlignment = HorizontalAlignment.Center; place.AddChild(label);
        var datePaper = new Panel { MouseFilter = MouseFilterEnum.Ignore };
        var dateSkin = CraftingPaperStyle.Paper(12, true, .97f);
        dateSkin.ContentMarginTop = dateSkin.ContentMarginBottom = 0;
        datePaper.AddThemeStyleboxOverride("panel", dateSkin);
        Put(_canvas, datePaper, 1094, 18, 412, 46);
        var date = Text(LetterMailbox.Date(LetterPaperPreviewScene.SampleHour)); date.Name = "NpcDate";
        date.HorizontalAlignment = HorizontalAlignment.Right;
        Put(_canvas, date, 1106, 18, 388, 46);
        var back = new Button { Name = "NpcReturn", Text = "返回" }; PaperButtonFeedback.Apply(back);
        Put(_canvas, back, 1534, 18, 114, 46);
        Put(back, new QuietRoomTriangle { Right = true, MouseFilter = MouseFilterEnum.Ignore }, 91, 20, 5, 7);
        back.Pressed += ClosePreview;
    }
    private void BuildRoster()
    {
        var paper = new Panel { Name = "NpcRoster", MouseFilter = MouseFilterEnum.Ignore };
        paper.AddThemeStyleboxOverride("panel", CraftingPaperStyle.Paper(0, true, .96f));
        Put(_canvas, paper, 24, 145, 292, 602);
        Put(paper, Text("在场人物", 23), 22, 18, 184, 36);
        var count = Text(_people.Count.ToString(), 19, true); count.HorizontalAlignment = HorizontalAlignment.Right;
        Put(paper, count, 220, 18, 45, 36);
        Put(paper, CraftingPaperStyle.Divider(), 22, 65, 247, 2);
        for (int index = 0; index < _people.Count; index++)
        {
            int selected = index; var npc = _people[index];
            var row = new Button { Name = "NpcRoster_" + index, Text = "", ToggleMode = true };
            PaperButtonFeedback.Apply(row); Put(paper, row, 14, 87 + index * 110, 264, 96);
            var portrait = new PortraitRenderer { Name = "RosterPortrait", MouseFilter = MouseFilterEnum.Ignore };
            portrait.set_headshot_mode(true); Put(row, portrait, 7, 8, 68, 80); portrait.show_generated_npc(npc);
            Put(row, Text(npc.npc_name, 22), 85, 16, 168, 33);
            var realm = Text(npc.cultivation_realm, 17, true); Put(row, realm, 85, 52, 168, 27);
            row.Pressed += () => SelectNpc(selected); _rosterButtons.Add(row);
        }
    }
    public void SelectNpc(int index)
    {
        if (index < 0 || index >= _people.Count) return;
        SelectedIndex = index; var npc = _people[index]; LastRequestedAction = LastRequestedNpc = "";
        _card.ShowPaperNpc(npc, () => npc, () => PreviewActions(npc));
        for (int i = 0; i < _rosterButtons.Count; i++)
        { _rosterButtons[i].SetPressedNoSignal(i == index); PaperButtonFeedback.Apply(_rosterButtons[i], selected: i == index); }
        _opening?.Kill(); _card.Modulate = new Color(1, 1, 1, .70f);
        _opening = CreateTween(); _opening.TweenProperty(_card, "modulate:a", 1f, .16);
    }
    private IEnumerable<NpcInteractionAction> PreviewActions(WorldNpcData npc)
    {
        bool secluded = npc.current_state == "闭关修行";
        if (secluded)
        {
            yield return PreviewAction(npc, "leave_acquaintance_note", "留下结识拜帖");
            yield return PreviewAction(npc, "npc.unavailable", "当前暂不可交互", false, "对方正在闭关，暂不接待。 ");
            yield break;
        }
        yield return PreviewAction(npc, "talk", "交谈");
        yield return PreviewAction(npc, "trade", "交易");
        yield return PreviewAction(npc, "spar", "切磋");
        yield return PreviewAction(npc, "probe", "探查");
        yield return PreviewAction(npc, "attack", "攻击", false, "此地禁止私斗。");
        if (ExtendedActions)
            for (int i = 0; i < 18; i++) yield return PreviewAction(npc, "scroll_" + i, i % 2 == 0 ? "交谈" : "交易");
    }
    private NpcInteractionAction PreviewAction(WorldNpcData npc, string id, string caption, bool enabled = true, string reason = "")
        => new(id, caption, () => { LastRequestedAction = id; LastRequestedNpc = npc.npc_id; }, enabled, reason);
    public void RefreshPreview() => SelectNpc(SelectedIndex);
    public void ClosePreview()
    {
        _card.close(); Closed?.Invoke();
        if (ExitOnClose)
        {
            // Finish disposable portrait-data finalizers while the native runtime is still alive.
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            GetTree().Quit();
        }
        else Hide();
    }
    public override void _UnhandledKeyInput(InputEvent input)
    {
        if (input is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        { ClosePreview(); GetViewport().SetInputAsHandled(); }
    }
    public override void _ExitTree() => _opening?.Kill();
    public static IEnumerable<WorldNpcData> CreateSamplePeople()
    {
        // Existing social/page smoke fixtures, kept only in this standalone scene.
        yield return new WorldNpcData
        {
            npc_id = "paper_preview_luqinghe", npc_name = "陆清和", faction_id = "yuxu_xianzong", formal_identity = "内门弟子",
            cultivation_realm = "云息四层", current_age = 19, gender = 1, appearance_seed = 930614, current_state = "空闲"
        };
        yield return new WorldNpcData
        {
            npc_id = "paper_preview_luheng", npc_name = "陆衡", faction_id = "baopu_shan", formal_identity = "行脚修士",
            cultivation_realm = "云息二层", current_age = 20, gender = 0, appearance_seed = 684201, current_state = "空闲"
        };
        yield return new WorldNpcData
        {
            npc_id = "paper_preview_xuzhiqiu", npc_name = "许知秋", faction_id = "baopu_shan", formal_identity = "",
            cultivation_realm = "云息二层", current_age = 20, gender = 1, appearance_seed = 930615, current_state = "空闲"
        };
    }
}
