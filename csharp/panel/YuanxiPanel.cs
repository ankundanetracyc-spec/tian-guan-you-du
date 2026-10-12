using Godot;

/// <summary>归元九息的正式修炼入口；所有结算由角色执行，界面只安排演出。</summary>
public partial class YuanxiPanel : Control
{
    public bool EmbeddedInQuietRoom { get; set; }
    private Character? _character;
    private RealmQiVisual? _qi;
    private Label? _title, _status, _notice;
    private SpinBox? _target;
    private Button? _one, _continuous, _continue, _stop;
    private bool _playing;
    private double _elapsed;
    private int _shownCount = -1;
    public event Action? Closed;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); ZIndex = 680;
        var background = new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, Texture = GD.Load<Texture2D>("res://textures/ui/realm_yunxi_paper_v6.png"), StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, MouseFilter = MouseFilterEnum.Stop };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(background); background.Visible = !EmbeddedInQuietRoom;
        _qi = new RealmQiVisual { MouseFilter = MouseFilterEnum.Ignore };
        _qi.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _qi.AnchorLeft = .28f; _qi.AnchorRight = .76f; _qi.AnchorTop = .12f; _qi.AnchorBottom = .87f;
        AddChild(_qi);
        var poem = new RealmPoemVisual { Visible = !EmbeddedInQuietRoom }; poem.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        poem.AnchorLeft = .77f; poem.AnchorRight = .96f; poem.AnchorTop = .15f; poem.AnchorBottom = .72f; AddChild(poem);
        var left = new VBoxContainer { AnchorLeft = .035f, AnchorRight = .28f, AnchorTop = .10f, AnchorBottom = .92f };
        left.AddThemeConstantOverride("separation", 16); AddChild(left);
        _title = Text("云息十层", 30); left.AddChild(_title);
        left.AddChild(Text("归元九息", 24));
        _status = Text("", 20); left.AddChild(_status);
        _notice = Text("", 17); left.AddChild(_notice);
        _one = Button(left, "养成一息", () => Start(false));
        var row = new HBoxContainer(); left.AddChild(row);
        var targetLabel = Text("连续养至", 18); targetLabel.AutowrapMode = TextServer.AutowrapMode.Off; row.AddChild(targetLabel);
        _target = new SpinBox { MinValue = 1, MaxValue = 9, Step = 1, Value = 9, CustomMinimumSize = new Vector2(90, 40) }; row.AddChild(_target); row.AddChild(Text("息", 18));
        _continuous = Button(left, "连续养息", () => Start(true));
        _continue = Button(left, "继续闭关", () => { _playing = true; Refresh(); });
        _stop = Button(left, "出关（放弃当前未成一息）", () => { _playing = false; _character?.stop_yuanxi(); Refresh(); });
        var close = new Button { Text = "收起", AnchorLeft = 1, AnchorRight = 1, OffsetLeft = -150, OffsetRight = -40, OffsetTop = 30, OffsetBottom = 75 };
        close.Pressed += () => { _playing = false; Hide(); Closed?.Invoke(); }; AddChild(close); close.Visible = !EmbeddedInQuietRoom;
        if (EmbeddedInQuietRoom)
        {
            // 归元息仍是静室吐纳内容：前景气机图与操作纸面，不另换环境。
            var diagramPaper = new PanelContainer { AnchorLeft = .02f, AnchorRight = .56f, AnchorTop = .08f, AnchorBottom = .9f, MouseFilter = MouseFilterEnum.Ignore };
            diagramPaper.AddThemeStyleboxOverride("panel", QuietRoomStyle.Paper(.82f, 0)); AddChild(diagramPaper); MoveChild(diagramPaper, 1);
            _qi.AnchorLeft = .04f; _qi.AnchorRight = .54f; _qi.AnchorTop = .12f; _qi.AnchorBottom = .86f;
            var paper = new PanelContainer { AnchorLeft = .59f, AnchorRight = 1, AnchorTop = 0, AnchorBottom = 1 };
            paper.AddThemeStyleboxOverride("panel", QuietRoomStyle.Paper(.95f, 24)); AddChild(paper);
            var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; paper.AddChild(scroll);
            left.Reparent(scroll); left.SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft); left.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            foreach (var text in left.FindChildren("*", "Label", true, false).OfType<Label>()) text.AddThemeColorOverride("font_color", QuietRoomStyle.Ink);
            foreach (var button in new[] { _one, _continuous, _continue, _stop }) if (button is not null) QuietRoomStyle.Primary(button);
        }
        Hide();
    }
    private static Label Text(string text, int size)
    {
        var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart, MouseFilter = MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", size); label.AddThemeColorOverride("font_color", new Color(.88f, .82f, .65f));
        return label;
    }
    private static Button Button(VBoxContainer parent, string text, Action pressed)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(0, 44) };
        button.Pressed += pressed; parent.AddChild(button); return button;
    }
    public void ShowFor(Character character)
    {
        _character = character; _playing = false; _shownCount = -1;
        Show(); MoveToFront(); Refresh();
    }
    private void Start(bool multiple)
    {
        if (_character?.start_yuanxi(multiple ? (int)(_target?.Value ?? 9) : _character.yuanxi_count + 1) == true) _playing = true;
        Refresh();
    }
    public override void _Process(double delta)
    {
        if (!IsVisibleInTree() || _character is null) return;
        if (_playing && GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.is_paused != true)
        {
            _elapsed += delta;
            if (_elapsed >= .25) { _elapsed = 0; _character.advance_yuanxi(16); }
            if (!_character.is_yuanxi_active()) _playing = false;
        }
        Refresh();
    }
    private void Refresh()
    {
        if (_character is null || _title is null || _qi is null) return;
        var count = _character.yuanxi_count;
        var active = _character.is_yuanxi_active();
        _title.Text = _character.is_yunxi_complete() ? "云息十层圆满" : "云息十层";
        if (_shownCount >= 0 && count > _shownCount)
        {
            _qi.PlayBreathGain(_shownCount, count, () => { _qi.ShowPath(1); _qi.ShowBreaths(_character.yuanxi_count); });
        }
        else if (!_qi.IsTransitioning) { _qi.ShowPath(1); _qi.ShowBreaths(count); }
        _shownCount = count;
        var remaining = _character.get_yuanxi_remaining_hours();
        _status!.Text = $"归元息　{count}／九息\n\n" + (count == 9 ? "九息俱全，可准备铸玄基。\n铸玄基后的修行内容暂未开放。" : $"{(active ? "正在" : "准备")}养成第{count + 1}息 · {_character.get_yuanxi_phase_name()}\n本息尚需 {(double.IsFinite(remaining) ? $"{remaining / 16:0.0}个修炼日（{remaining:0.0}小时）" : "有效主修功法与灵根")}\n\n修炼不消耗体力，按实际修炼时长推进时间。");
        _notice!.Text = _character.get_yuanxi_notice() + "\n\n每息依次经历周行、牵引、平衡。收起界面会暂停演出并保留闭关，回来可继续；进行其他耗时行动视为出关。";
        _one!.Disabled = _continuous!.Disabled = active || count == 9 || _character.get_cultivation_rate() <= 0;
        _target!.MinValue = Math.Min(9, count + 1); _target.Editable = !active && count < 9;
        _continue!.Visible = active && !_playing;
        _stop!.Visible = active;
    }
}
