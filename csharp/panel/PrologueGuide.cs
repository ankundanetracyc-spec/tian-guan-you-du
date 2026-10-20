using Godot;

/// <summary>序章采用少量提示与阶段任务；对话、旁白、玄镜心念分开呈现。</summary>
public partial class PrologueGuide : Control
{
    private Label? _title, _objective, _taskTitle;
    private RichTextLabel? _body;
    private RichTextLabel? _taskObjective;
    private PanelContainer? _dialoguePanel, _taskPanel;
    private Character? _child;
    private int _stage, _dialogueIndex;
    private bool _territoryExplained;
    private StoryLine[] _dialogueLines = [];
    private Action? _dialogueFinished;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore; ZIndex = 150; Build(); Hide();
    }

    public override void _Input(InputEvent @event)
    {
        if (!IsVisibleInTree() || _dialogueLines.Length == 0) return;
        if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Left } mouse) return;
        // 在 GUI 命中测试前接收整屏点击；按下和松开都拦截，防止操作穿透到功能面板。
        GetViewport().SetInputAsHandled();
        if (!mouse.Pressed) ContinueDialogue();
    }

    public void begin(Character child)
    {
        if (_child is not null)
        {
            _child.GongfaChapterCompleted -= on_gongfa_chapter_completed;
            _child.StudyStarted -= on_study_started;
            _child.MainGongfaChanged -= on_main_gongfa_changed;
            _child.BreakthroughCompleted -= on_breakthrough_completed;
        }
        _dialogueLines = []; _dialogueFinished = null; _dialogueIndex = 0;
        _dialoguePanel?.Hide();
        _child = child; _stage = -1; _territoryExplained = false; ZIndex = 1; Show(); MoveToFront(); ShowRoleObjective();
        _child.GongfaChapterCompleted += on_gongfa_chapter_completed;
        _child.StudyStarted += on_study_started;
        _child.MainGongfaChanged += on_main_gongfa_changed;
        _child.BreakthroughCompleted += on_breakthrough_completed;
    }

    public void on_territory_opened(string section) { }

    public void on_character_opened(WorldEntity entity)
    {
        if (_stage != 0 || entity != _child) return;
        SetTask(PrologueText.CharacterOpenedTask);
    }

    private void on_study_started(Character character, GongfaResource gongfa)
    {
        if (_stage != 0 || character != _child) return;
        _stage = 1;
        SetTask(PrologueText.StudyStartedTask(gongfa.gongfa_name));
    }

    private void on_gongfa_chapter_completed(Character character, string gongfaName, string _, string chapterName, Godot.Collections.Dictionary __)
    {
        if (_stage != 1 || character != _child) return;
        _stage = 2;
        StartDialogue(
            [Narration(PrologueText.ChapterCompletedNarration(gongfaName, chapterName)),
             Speech("李家家主", PrologueText.ChapterCompletedFather)],
            () => SetTask(PrologueText.SetMainTask(gongfaName)));
    }

    private void on_main_gongfa_changed(Character character, GongfaResource gongfa)
    {
        if (_stage != 2 || character != _child) return;
        _stage = 3;
        StartDialogue(
            [Narration(PrologueText.FirstQiNarration),
             Speech("李家家主", PrologueText.FirstQiFather),
             Speech("李家家主", PrologueText.MainSetFather)],
            () => SetTask("静室引气\n前往静室，主动开始修炼。"));
    }

    private void on_breakthrough_completed(Character character, string newRealm)
    {
        if (_stage != 4 || character != _child || newRealm != "云息一层") return;
        _stage = 5;
        StartDialogue(
            [Narration(PrologueText.BreakthroughNarration),
             Speech("李家家主", PrologueText.BreakthroughFatherPaths),
             Speech("李家家主", PrologueText.BreakthroughFatherBeast)],
            () => SetTask(PrologueText.BeastTask));
    }

    private void ShowRoleObjective()
    {
        _stage = 0;
        SetTask(InitialTask());
        _taskPanel?.Show();
    }

    private void StartDialogue(StoryLine[] lines, Action finished)
    {
        ZIndex = 2000; // 保证剧情对话显示在功能面板之上；点击由 _Input 接收。
        MoveToFront();
        _dialoguePanel?.Show(); _taskPanel?.Hide(); _dialogueLines = lines; _dialogueIndex = 0; _dialogueFinished = finished;
        show_dialogue_line(lines[0]); _objective!.Text = "点击屏幕任意位置继续";
    }

    private void ContinueDialogue()
    {
        if (_dialogueLines.Length == 0) return;
        _dialogueIndex++;
        if (_dialogueIndex < _dialogueLines.Length) { show_dialogue_line(_dialogueLines[_dialogueIndex]); return; }
        _dialogueLines = []; var finished = _dialogueFinished; _dialogueFinished = null; _dialoguePanel?.Hide(); finished?.Invoke();
        ZIndex = 1; // 阶段任务是背景提示，不能盖住角色、辖地等交互面板。
        _taskPanel?.Show();
    }

    private string InitialTask() => PrologueText.InitialTask(_child?.entity_name ?? "幼子", CurrentStudyName());
    private string CurrentStudyName()
    {
        if (_child is null) return "首卷";
        var summary = _child.get_current_study_summary();
        if (!summary.GetValueOrDefault("active", false).AsBool())
        {
            var pending = _child.get_pending_intro_gongfa();
            return pending is null ? "首卷" : pending.gongfa_name;
        }
        var book = summary.GetValueOrDefault("gongfa_name", "首卷").AsString();
        var chapter = summary.GetValueOrDefault("chapter_name", "").AsString();
        return string.IsNullOrEmpty(chapter) ? book : $"{book}·{chapter}";
    }
    private void SetTask(string value) { _taskTitle!.Text = "眼下要事"; _taskObjective!.Clear(); _taskObjective.AppendText(value); }
    private void show_dialogue_line(StoryLine line)
    {
        // 空文案不能留下一个无法推进的黑框；开发期也能直接看出漏配位置。
        _title!.Text = string.IsNullOrWhiteSpace(line.Source) ? "旁白" : line.Source;
        _body!.Clear();
        _body.AppendText(string.IsNullOrWhiteSpace(line.Text) ? "（此处文案尚未配置。）" : line.Text);
    }
    private readonly record struct StoryLine(string Source, string Text);
    private static StoryLine Narration(string text) => new("旁白", text);
    private static StoryLine Speech(string speaker, string text) => new(speaker, $"“{text}”");
    private static StoryLine MirrorThought(string text) => new("玄镜", $"（{text}）");

    private void Build()
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(760, 208), MouseFilter = MouseFilterEnum.Ignore };
        // 对话仍贴屏幕下方；对话进行时不允许操作底栏，结束后立即恢复。
        panel.SetAnchorsPreset(LayoutPreset.BottomWide); panel.OffsetLeft = 160; panel.OffsetRight = -160; panel.OffsetTop = -238; panel.OffsetBottom = -30;
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(0.055f, 0.05f, 0.04f, .96f), BorderColor = new Color(0.63f, 0.51f, 0.3f, .95f), BorderWidthLeft = 2, BorderWidthRight = 2, BorderWidthTop = 2, BorderWidthBottom = 2, CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8 }); AddChild(panel); _dialoguePanel = panel;
        var margin = new MarginContainer(); margin.AddThemeConstantOverride("margin_left", 26); margin.AddThemeConstantOverride("margin_right", 26); margin.AddThemeConstantOverride("margin_top", 16); margin.AddThemeConstantOverride("margin_bottom", 16); panel.AddChild(margin);
        var box = new VBoxContainer(); box.AddThemeConstantOverride("separation", 8); margin.AddChild(box);
        _title = new Label { Text = "旁白", MouseFilter = MouseFilterEnum.Ignore }; _title.AddThemeFontSizeOverride("font_size", 22); _title.Modulate = new Color(.96f, .85f, .58f); box.AddChild(_title);
        // RichTextLabel 与任务卡使用同一渲染控件，固定正文高度，避免 Label 在容器布局中被压为零高。
        _body = new RichTextLabel { BbcodeEnabled = false, FitContent = false, ScrollActive = false, CustomMinimumSize = new Vector2(0, 104), SizeFlagsVertical = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        _body.AddThemeFontSizeOverride("normal_font_size", 18); _body.Modulate = new Color(.91f, .88f, .8f); box.AddChild(_body);
        var footer = new HBoxContainer(); footer.AddThemeConstantOverride("separation", 18); box.AddChild(footer);
        _objective = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore }; _objective.AddThemeFontSizeOverride("font_size", 16); _objective.Modulate = new Color(.98f, .92f, .65f); footer.AddChild(_objective);
        panel.Hide(); // 初始阶段只有任务卡；有台词时才由 StartDialogue 显示对话框。

        // 阶段任务只是信息提示，不能截走辖地与底栏按钮的鼠标事件。
        var task = new PanelContainer { CustomMinimumSize = new Vector2(390, 156), Size = new Vector2(390, 156), MouseFilter = MouseFilterEnum.Ignore };
        task.SetAnchorsPreset(LayoutPreset.TopLeft); task.Position = new Vector2(32, SundialClock.Radius + 40); task.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(.06f, .055f, .04f, .91f), BorderColor = new Color(.58f, .47f, .28f, .9f), BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1, CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8 }); AddChild(task); _taskPanel = task;
        var taskMargin = new MarginContainer(); taskMargin.AddThemeConstantOverride("margin_left", 16); taskMargin.AddThemeConstantOverride("margin_right", 16); taskMargin.AddThemeConstantOverride("margin_top", 12); taskMargin.AddThemeConstantOverride("margin_bottom", 12); task.AddChild(taskMargin);
        var taskBox = new VBoxContainer(); taskBox.AddThemeConstantOverride("separation", 6); taskMargin.AddChild(taskBox);
        _taskTitle = new Label { Text = "当前引导", MouseFilter = MouseFilterEnum.Ignore }; _taskTitle.AddThemeFontSizeOverride("font_size", 18); _taskTitle.Modulate = new Color(.96f, .84f, .58f); taskBox.AddChild(_taskTitle);
        _taskObjective = new RichTextLabel { BbcodeEnabled = true, FitContent = true, ScrollActive = false, CustomMinimumSize = new Vector2(0, 62), MouseFilter = MouseFilterEnum.Ignore }; _taskObjective.AddThemeFontSizeOverride("normal_font_size", 15); _taskObjective.Modulate = new Color(.9f, .88f, .8f); taskBox.AddChild(_taskObjective);
        task.Hide();
    }
}
