using Godot;
using System;
using System.Collections.Generic;

/// <summary>玉虚山门迎真过程展示；只渲染调用方提供的可见信息，不读取人物隐藏判定数据。</summary>
public partial class StoryDialoguePanel
{
    private enum YuxuAdmissionStage
    {
        None,
        Review,
        WaitingForQuestion,
        Question,
        WaitingForResult,
        Result,
    }

    private YuxuAdmissionStage _yuxuAdmissionStage;
    private WorldNpcData? _yuxuAdmissionElder;
    private Action? _yuxuAdmissionProceed;
    private Action<bool>? _yuxuAdmissionAnswered;
    private Action? _yuxuAdmissionCancelled;
    private Action? _yuxuAdmissionCompleted;
    private readonly List<Button> _yuxuAdmissionChoiceButtons = [];
    private int _yuxuAdmissionGeneration;
    private bool _yuxuAdmissionActive;

    /// <summary>
    /// 展示长老头像、姓名与逐条淡入的可见判定依据。证据文案由调用方提供；
    /// 全部展示或点击“略过展示”后，按钮改为“继续”并调用 proceed。
    /// </summary>
    public void show_yuxu_admission_review(WorldNpcData elder, IReadOnlyList<string> evidenceLines, Action proceed, Action? cancelled = null)
    {
        ArgumentNullException.ThrowIfNull(elder);
        ArgumentNullException.ThrowIfNull(proceed);
        var lines = normalize_yuxu_admission_lines(evidenceLines, nameof(evidenceLines));

        var column = begin_yuxu_admission_stage(elder, YuxuAdmissionStage.Review, cancelled);
        _yuxuAdmissionProceed = proceed;
        foreach (var line in lines)
        {
            var label = social_label(line, 21, "68715d");
            label.Modulate = new Color(1, 1, 1, 0);
            column.AddChild(label);
            _socialFactors.Add(label);
        }

        add_yuxu_admission_progress(column);
        _socialDone = social_button("略过展示");
        _socialDone.Pressed += continue_yuxu_admission_review;
        column.AddChild(_socialDone);
        start_yuxu_admission_reveal(lines.Length);
    }

    /// <summary>展示唯一问题“可有宗门？”及“有／无”两项；选择时以 true/false 回调。</summary>
    public void show_yuxu_admission_question(WorldNpcData elder, Action<bool> answered, Action? cancelled = null)
    {
        ArgumentNullException.ThrowIfNull(elder);
        ArgumentNullException.ThrowIfNull(answered);

        var column = begin_yuxu_admission_stage(elder, YuxuAdmissionStage.Question, cancelled);
        _yuxuAdmissionAnswered = answered;
        var question = social_label("可有宗门？", 24);
        question.HorizontalAlignment = HorizontalAlignment.Center;
        column.AddChild(question);

        var choices = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        choices.AddThemeConstantOverride("separation", 16);
        column.AddChild(choices);
        add_yuxu_admission_choice(choices, "有", true);
        add_yuxu_admission_choice(choices, "无", false);
        _socialChoosing = true;
        _socialCancel = cancel_yuxu_admission;
    }

    /// <summary>
    /// 展示调用方提供的分支收束文字，并在点击“继续”或按 Esc 结束后调用 completed。
    /// 可用于“已有宗门”的礼貌结束，也可用于“尚无宗门”的引路反馈。
    /// </summary>
    public void show_yuxu_admission_result(WorldNpcData elder, IReadOnlyList<string> resultLines, Action completed, string title = "山门迎真")
    {
        ArgumentNullException.ThrowIfNull(elder);
        ArgumentNullException.ThrowIfNull(completed);
        var lines = normalize_yuxu_admission_lines(resultLines, nameof(resultLines));

        var column = begin_yuxu_admission_stage(elder, YuxuAdmissionStage.Result, title: title);
        _yuxuAdmissionCompleted = completed;
        foreach (var line in lines)
        {
            var label = social_label(line, 22);
            label.Modulate = new Color(1, 1, 1, 0);
            column.AddChild(label);
            _socialFactors.Add(label);
        }

        add_yuxu_admission_progress(column);
        _socialDone = social_button("略过展示");
        _socialDone.Pressed += continue_yuxu_admission_result;
        column.AddChild(_socialDone);
        start_yuxu_admission_reveal(lines.Length);
    }

    private VBoxContainer begin_yuxu_admission_stage(WorldNpcData elder, YuxuAdmissionStage stage, Action? cancelled = null, string title = "山门迎真")
    {
        // 清掉共享社交动画及旧底栏回调，避免阶段切换后残留旧交互状态。
        reset_social_presentation();
        _on_closed = null;
        hide_speaker_portrait();
        _yuxuAdmissionGeneration++;
        _yuxuAdmissionActive = true;
        _yuxuAdmissionStage = stage;
        _yuxuAdmissionElder = elder;
        _yuxuAdmissionProceed = null;
        _yuxuAdmissionAnswered = null;
        _yuxuAdmissionCancelled = cancelled;
        _yuxuAdmissionCompleted = null;
        _yuxuAdmissionChoiceButtons.Clear();

        _dialogueFrame?.Hide();
        _continueHint?.Hide();

        var overlay = new Control { Name = "YuxuAdmissionInteraction", MouseFilter = MouseFilterEnum.Stop };
        _socialContent = overlay;
        AddChild(overlay);
        overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var shade = new ColorRect { Color = new Color(.07f, .10f, .08f, .38f), MouseFilter = MouseFilterEnum.Ignore };
        overlay.AddChild(shade);
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        overlay.AddChild(center);
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var paper = new PanelContainer
        {
            Name = "YuxuAdmissionPaper",
            CustomMinimumSize = new Vector2(Mathf.Min(560, GetViewportRect().Size.X * .82f), 0),
        };
        center.AddChild(paper);
        paper.AddThemeStyleboxOverride("panel", social_paper("e8dfc9"));

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 14);
        paper.AddChild(column);

        var portraitRow = new CenterContainer();
        column.AddChild(portraitRow);
        var portrait = new PortraitRenderer { Name = "YuxuAdmissionHeadshot", CustomMinimumSize = new Vector2(112, 112) };
        portrait.set_headshot_mode(true);
        portraitRow.AddChild(portrait);
        portrait.show_world_npc(elder);

        var name = social_label(string.IsNullOrWhiteSpace(elder.npc_name) ? "接引长老" : elder.npc_name, 25);
        name.HorizontalAlignment = HorizontalAlignment.Center;
        column.AddChild(name);

        var heading = social_label(string.IsNullOrWhiteSpace(title) ? "山门迎真" : title, 20, "737b65");
        heading.HorizontalAlignment = HorizontalAlignment.Center;
        column.AddChild(heading);

        Show();
        MoveToFront();
        _socialChoosing = true;
        _socialCancel = cancel_yuxu_admission;
        return column;
    }

    private void add_yuxu_admission_choice(HBoxContainer parent, string text, bool hasSect)
    {
        var button = social_button(text);
        button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        button.Pressed += () => choose_yuxu_admission_answer(hasSect);
        parent.AddChild(button);
        _yuxuAdmissionChoiceButtons.Add(button);
    }

    private void choose_yuxu_admission_answer(bool hasSect)
    {
        if (!_yuxuAdmissionActive || _yuxuAdmissionStage != YuxuAdmissionStage.Question || !_socialChoosing)
            return;

        _socialChoosing = false;
        _socialCancel = null;
        _yuxuAdmissionStage = YuxuAdmissionStage.WaitingForResult;
        foreach (var button in _yuxuAdmissionChoiceButtons)
            if (GodotObject.IsInstanceValid(button)) button.Disabled = true;
        var callback = _yuxuAdmissionAnswered;
        _yuxuAdmissionAnswered = null;
        callback?.Invoke(hasSect);

        // 正常串联会在 answered 回调内调用 show_yuxu_admission_result。
        // 若主流程稍后才决定如何展示，Esc 仍可安全结束当前问题页。
        if (_yuxuAdmissionActive && _yuxuAdmissionStage == YuxuAdmissionStage.WaitingForResult)
        {
            _socialChoosing = true;
            _socialCancel = cancel_yuxu_admission;
        }
    }

    private void continue_yuxu_admission_review()
    {
        if (!_yuxuAdmissionActive || _yuxuAdmissionStage != YuxuAdmissionStage.Review)
            return;
        if (_socialRevealing)
        {
            finish_social_reveal();
            if (_socialDone is not null) _socialDone.Text = "继续";
            return;
        }

        _yuxuAdmissionStage = YuxuAdmissionStage.WaitingForQuestion;
        var callback = _yuxuAdmissionProceed;
        _yuxuAdmissionProceed = null;
        callback?.Invoke();
    }

    private void continue_yuxu_admission_result()
    {
        if (!_yuxuAdmissionActive || _yuxuAdmissionStage != YuxuAdmissionStage.Result)
            return;
        if (_socialRevealing)
        {
            finish_social_reveal();
            if (_socialDone is not null) _socialDone.Text = "继续";
            return;
        }
        end_yuxu_admission(invokeCompleted: true);
    }

    private void start_yuxu_admission_reveal(int lineCount)
    {
        _socialRevealIndex = 0;
        _socialRevealElapsed = 0;
        _socialRevealing = lineCount > 0;
        if (lineCount == 0)
        {
            if (_socialDone is not null) _socialDone.Text = "继续";
            return;
        }

        var stage = _socialContent;
        var generation = _yuxuAdmissionGeneration;
        var delay = .65 * (lineCount + 1) + .12;
        GetTree().CreateTimer(delay).Timeout += () =>
        {
            if (!GodotObject.IsInstanceValid(this) || generation != _yuxuAdmissionGeneration ||
                !_yuxuAdmissionActive || _socialContent != stage || _socialRevealing || _socialDone is null)
                return;
            _socialDone.Text = "继续";
        };
    }

    private void add_yuxu_admission_progress(VBoxContainer column)
    {
        _socialInk = new ProgressBar
        {
            ShowPercentage = false,
            MaxValue = 1,
            CustomMinimumSize = new Vector2(0, 5),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _socialInk.AddThemeStyleboxOverride("background", new StyleBoxFlat { BgColor = new Color("d0cab6") });
        _socialInk.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = new Color("536956") });
        column.AddChild(_socialInk);
    }

    private void cancel_yuxu_admission()
    {
        if (_yuxuAdmissionStage == YuxuAdmissionStage.Result)
            end_yuxu_admission(invokeCompleted: true);
        else
            end_yuxu_admission(invokeCancelled: true);
    }

    private void end_yuxu_admission(bool invokeCompleted = false, bool invokeCancelled = false)
    {
        var callback = invokeCompleted ? _yuxuAdmissionCompleted : null;
        var cancelCallback = invokeCancelled ? _yuxuAdmissionCancelled : null;
        _yuxuAdmissionGeneration++;
        _yuxuAdmissionActive = false;
        _yuxuAdmissionStage = YuxuAdmissionStage.None;
        _yuxuAdmissionElder = null;
        _yuxuAdmissionProceed = null;
        _yuxuAdmissionAnswered = null;
        _yuxuAdmissionCancelled = null;
        _yuxuAdmissionCompleted = null;
        _yuxuAdmissionChoiceButtons.Clear();
        _on_closed = null;
        reset_social_presentation();
        hide_speaker_portrait();
        Hide();
        callback?.Invoke();
        cancelCallback?.Invoke();
    }

    private static string[] normalize_yuxu_admission_lines(IReadOnlyList<string> lines, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var visibleLines = new List<string>(lines.Count);
        foreach (var line in lines)
            if (!string.IsNullOrWhiteSpace(line)) visibleLines.Add(line);
        if (visibleLines.Count == 0)
            throw new ArgumentException("至少需要一条可展示的迎真文案。", parameterName);
        return visibleLines.ToArray();
    }
}
