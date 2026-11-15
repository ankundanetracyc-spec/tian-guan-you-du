using System.Linq;
using Godot;

/// <summary>
/// A brief, characterless scene transition for the true disciple's scheduled lecture check-in.
/// Attendance is recorded by MainScene when this panel is opened. The single choice only changes narration.
/// </summary>
public partial class TrueDiscipleLecturePanel : Control
{
    private const string BackgroundPath = "res://textures/ui/true_disciple_lecture/yuxu_lecture_ambient_window_v1.png";
    private static readonly string[] OpeningBeats =
    [
        "观复峰讲席开场，淡青墨色铺陈开来，今日的话题渐渐清晰。",
        "经义落到修行日常，讲席由一处常见疑难说起。",
    ];
    private static readonly LecturePrompt[] LecturePrompts =
    [
        new(
            "随讲一问：功课日日未断，进境却迟迟不显，该如何自处？",
            [
                new("先将日常吐纳与功课逐一理顺", "这一答把话头落在根基上：循着日常功课逐项检视，眼下的迟滞便有了可辨之处。"),
                new("暂缓求快，守住眼下的修行节奏", "这一答把话头落在持守上：不急于求快，先让每日修行保持安稳。"),
            ]),
        new(
            "随讲一问：经卷读过便忘，只记得字句，难得其中意思。",
            [
                new("合卷复述一遍，再回看疏漏之处", "话题由记诵转向体会：合卷复述，能看清哪些地方已经明白，哪些仍需细读。"),
                new("把经义放回日常修行中印证", "话题由字句落到日用：将经义放进实际修行里体会，也能慢慢读出其中关节。"),
            ]),
        new(
            "随讲一问：修行仿佛停滞，眼下是否该立刻改换法门？",
            [
                new("先回看根基功课是否有疏漏", "这一答先从根基处着眼：把已学的功课重新走一遍，再判断前路更为妥当。"),
                new("守住日课，再观察一段时日", "这一答先把步子放稳：守住眼下的日课，留些时日观察变化，再作打算。"),
            ]),
    ];
    private static readonly Vector4[] BubbleAnchors =
    [
        new(0.10f, 0.28f, 0.62f, 0.68f),
        new(0.38f, 0.28f, 0.90f, 0.68f),
        new(0.16f, 0.20f, 0.84f, 0.80f),
        new(0.23f, 0.28f, 0.77f, 0.70f),
    ];

    private enum LectureStage { Opening, Prompt, Response, Closing }

    private sealed record LectureChoice(string Text, string FollowUp);
    private sealed record LecturePrompt(string Question, LectureChoice[] Choices);

    public static int PromptVariantCount => LecturePrompts.Length;
    public bool IsOpen => Visible;

    private Label? _headerTitle;
    private Label? _headerSubtitle;
    private Label? _beatCounter;
    private Label? _narration;
    private Button? _advanceButton;
    private PanelContainer? _narrationCard;
    private HBoxContainer? _choiceTray;
    private LectureStage _stage;
    private int _openingBeatIndex;
    private int _promptIndex;
    private double _ambientRemaining;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        MouseFilter = MouseFilterEnum.Stop;
        ZIndex = 260;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        build_shell();
        Hide();
    }

    public void ShowLecture(string periodLabel, int promptVariant)
    {
        _openingBeatIndex = 0;
        _promptIndex = ((promptVariant % LecturePrompts.Length) + LecturePrompts.Length) % LecturePrompts.Length;
        _headerTitle!.Text = "观复峰 · 真传讲席";
        _headerSubtitle!.Text = $"本期到场履职　·　{periodLabel}";
        present_opening(0, false);
        Modulate = new Color(1, 1, 1, 0);
        Show();
        MoveToFront();
        CreateTween().TweenProperty(this, "modulate:a", 1.0f, 0.24);
    }

    public void Close()
    {
        _ambientRemaining = 0;
        Hide();
    }

    public override void _Process(double delta)
    {
        if (!Visible || _ambientRemaining <= 0) return;
        _ambientRemaining -= delta;
        if (_ambientRemaining > 0) return;

        if (_stage == LectureStage.Opening)
        {
            if (_openingBeatIndex < OpeningBeats.Length - 1)
                present_opening(_openingBeatIndex + 1, true);
            else
                present_prompt(true);
        }
        else if (_stage == LectureStage.Response)
        {
            present_closing(true);
        }
    }

    private void build_shell()
    {
        var background = new TextureRect
        {
            Texture = ResourceLoader.Load<Texture2D>(BackgroundPath),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var veil = new ColorRect
        {
            Color = new Color(0.055f, 0.065f, 0.07f, 0.24f),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        veil.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(veil);

        var margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 38);
        margin.AddThemeConstantOverride("margin_top", 26);
        margin.AddThemeConstantOverride("margin_right", 38);
        margin.AddThemeConstantOverride("margin_bottom", 24);
        AddChild(margin);

        var column = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        column.AddThemeConstantOverride("separation", 16);
        margin.AddChild(column);

        var headerCard = new PanelContainer
        {
            CustomMinimumSize = new Vector2(0, 78),
            MouseFilter = MouseFilterEnum.Stop,
        };
        headerCard.AddThemeStyleboxOverride("panel", panel_style(new Color(0.075f, 0.09f, 0.095f, 0.80f), new Color("b5a57c"), 7, 14));
        column.AddChild(headerCard);

        var header = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        header.AddThemeConstantOverride("separation", 18);
        headerCard.AddChild(header);
        var titleColumn = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        titleColumn.AddThemeConstantOverride("separation", 2);
        header.AddChild(titleColumn);
        _headerTitle = make_label("观复峰 · 真传讲席", 29, PaperText);
        titleColumn.AddChild(_headerTitle);
        _headerSubtitle = make_label("本期到场履职", 15, PaperMuted);
        titleColumn.AddChild(_headerSubtitle);

        var skipButton = make_button("略过场景", 42);
        skipButton.CustomMinimumSize = new Vector2(138, 42);
        skipButton.Pressed += Close;
        header.AddChild(skipButton);

        var stage = new Control
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        column.AddChild(stage);

        _narrationCard = new PanelContainer
        {
            AnchorLeft = BubbleAnchors[0].X,
            AnchorTop = BubbleAnchors[0].Y,
            AnchorRight = BubbleAnchors[0].Z,
            AnchorBottom = BubbleAnchors[0].W,
            CustomMinimumSize = new Vector2(420, 250),
            MouseFilter = MouseFilterEnum.Stop,
        };
        _narrationCard.AddThemeStyleboxOverride("panel", panel_style(new Color(0.94f, 0.90f, 0.81f, 0.88f), new Color("99865d"), 8, 20));
        stage.AddChild(_narrationCard);

        var cardContent = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        cardContent.AddThemeConstantOverride("separation", 10);
        _narrationCard.AddChild(cardContent);
        _beatCounter = make_label("讲席片段 1 / 4", 14, InkSoft);
        cardContent.AddChild(_beatCounter);
        cardContent.AddChild(make_rule());
        _narration = make_label("", 21, Ink);
        _narration.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _narration.SizeFlagsVertical = SizeFlags.ExpandFill;
        _narration.VerticalAlignment = VerticalAlignment.Center;
        cardContent.AddChild(_narration);

        _choiceTray = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        _choiceTray.AddThemeConstantOverride("separation", 10);
        _choiceTray.Hide();
        cardContent.AddChild(_choiceTray);

        var actionRow = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(0, 48),
            Alignment = BoxContainer.AlignmentMode.End,
        };
        _advanceButton = make_button("继续", 44);
        _advanceButton.CustomMinimumSize = new Vector2(150, 44);
        _advanceButton.Pressed += on_advance_pressed;
        actionRow.AddChild(_advanceButton);
        column.AddChild(actionRow);
    }

    private void present_opening(int index, bool animate)
    {
        _stage = LectureStage.Opening;
        _openingBeatIndex = index;
        _ambientRemaining = 4.8;
        _choiceTray!.Hide();
        _advanceButton!.Show();
        _advanceButton.Text = "继续";
        set_beat($"讲席开场 · {index + 1} / 5", OpeningBeats[index], index, animate);
    }

    private void present_prompt(bool animate)
    {
        _stage = LectureStage.Prompt;
        _ambientRemaining = 0;
        _choiceTray!.GetChildren().ToList().ForEach(child => child.QueueFree());
        var prompt = LecturePrompts[_promptIndex];
        for (var index = 0; index < prompt.Choices.Length; index++)
        {
            var choiceIndex = index;
            var button = make_button(prompt.Choices[index].Text, 62);
            button.CustomMinimumSize = new Vector2(0, 62);
            button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            button.Pressed += () => present_response(choiceIndex);
            _choiceTray.AddChild(button);
        }
        _choiceTray.Show();
        _advanceButton!.Hide();
        set_beat("随讲一问 · 3 / 5", prompt.Question, 2, animate);
    }

    private void present_response(int choiceIndex)
    {
        if (_stage != LectureStage.Prompt) return;
        var prompt = LecturePrompts[_promptIndex];
        if (choiceIndex < 0 || choiceIndex >= prompt.Choices.Length) return;
        _stage = LectureStage.Response;
        _ambientRemaining = 4.8;
        _choiceTray!.Hide();
        _advanceButton!.Show();
        _advanceButton.Text = "继续";
        set_beat("回应余韵 · 4 / 5", prompt.Choices[choiceIndex].FollowUp, 3, true);
    }

    private void present_closing(bool animate)
    {
        _stage = LectureStage.Closing;
        _ambientRemaining = 0;
        _choiceTray!.Hide();
        _advanceButton!.Show();
        _advanceButton.Text = "返回观复峰";
        set_beat("讲席收束 · 5 / 5", "讲席言尽，墨色渐淡。本期到场履职已记。", 3, animate);
    }

    private void set_beat(string counter, string narration, int anchorIndex, bool animate)
    {
        _beatCounter!.Text = counter;
        _narration!.Text = narration;
        var anchors = BubbleAnchors[anchorIndex];
        _narrationCard!.AnchorLeft = anchors.X;
        _narrationCard.AnchorTop = anchors.Y;
        _narrationCard.AnchorRight = anchors.Z;
        _narrationCard.AnchorBottom = anchors.W;
        if (!animate) return;
        _narrationCard.Modulate = new Color(1, 1, 1, 0.48f);
        CreateTween().TweenProperty(_narrationCard, "modulate:a", 1.0f, 0.22);
    }

    private void on_advance_pressed()
    {
        switch (_stage)
        {
            case LectureStage.Opening:
                if (_openingBeatIndex < OpeningBeats.Length - 1)
                    present_opening(_openingBeatIndex + 1, true);
                else
                    present_prompt(true);
                break;
            case LectureStage.Response:
                present_closing(true);
                break;
            case LectureStage.Closing:
                Close();
                break;
        }
    }

    private static Label make_label(string text, int size, Color color)
    {
        var label = new Label { Text = text, MouseFilter = MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private static Button make_button(string text, int height)
    {
        var button = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(0, height),
            FocusMode = FocusModeEnum.None,
            Theme = ResourceLoader.Load<Theme>("res://resources/font/default_theme.tres"),
        };
        button.AddThemeFontSizeOverride("font_size", 16);
        button.AddThemeColorOverride("font_color", PaperText);
        button.AddThemeColorOverride("font_hover_color", new Color("f4e4b9"));
        button.AddThemeStyleboxOverride("normal", button_style(new Color(0.15f, 0.13f, 0.095f, 0.92f), new Color("756340")));
        button.AddThemeStyleboxOverride("hover", button_style(new Color(0.26f, 0.21f, 0.13f, 0.96f), new Color("c5a15b")));
        button.AddThemeStyleboxOverride("pressed", button_style(new Color(0.10f, 0.09f, 0.07f, 0.96f), new Color("a4864f")));
        return button;
    }

    private static HSeparator make_rule()
    {
        var line = new HSeparator();
        line.AddThemeStyleboxOverride("separator", new StyleBoxLine { Color = new Color("a99870"), Thickness = 1 });
        return line;
    }

    private static StyleBoxFlat button_style(Color background, Color border) => panel_style(background, border, 5, 6);

    private static StyleBoxFlat panel_style(Color background, Color border, int radius, float padding)
    {
        var style = new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            ContentMarginLeft = padding,
            ContentMarginRight = padding,
            ContentMarginTop = padding,
            ContentMarginBottom = padding,
        };
        style.SetCornerRadiusAll(radius);
        return style;
    }

    private static readonly Color PaperText = new("f0e7cf");
    private static readonly Color PaperMuted = new("cabda0");
    private static readonly Color Ink = new("332d22");
    private static readonly Color InkSoft = new("625841");
}
