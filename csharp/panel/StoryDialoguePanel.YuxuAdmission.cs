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
    private bool _yuxuAdmissionActive;

    /// <summary>
    /// 使用共用双人暗幕与逐字试探。证据文案由调用方提供；
    /// 全部展示或点击“略过展示”后，按钮改为“继续”并调用 proceed。
    /// </summary>
    public void show_yuxu_admission_review(WorldNpcData elder, IReadOnlyList<string> evidenceLines, Action proceed, Action? cancelled = null,
        Character? player = null, bool testMode = false)
    {
        ArgumentNullException.ThrowIfNull(elder);
        ArgumentNullException.ThrowIfNull(proceed);
        var lines = normalize_yuxu_admission_lines(evidenceLines, nameof(evidenceLines));

        var column = begin_yuxu_admission_stage(elder, YuxuAdmissionStage.Review, cancelled, player: player, testMode: testMode);
        _yuxuAdmissionProceed = proceed;
        add_yuxu_admission_lines(column, lines);
        add_yuxu_admission_continue(column, continue_yuxu_admission_review);
        start_yuxu_admission_reveal();
    }

    /// <summary>展示唯一问题“可有宗门？”及“有／无”两项；选择时以 true/false 回调。</summary>
    public void show_yuxu_admission_question(WorldNpcData elder, Action<bool> answered, Action? cancelled = null,
        Character? player = null, bool testMode = false)
    {
        ArgumentNullException.ThrowIfNull(elder);
        ArgumentNullException.ThrowIfNull(answered);

        var column = begin_yuxu_admission_stage(elder, YuxuAdmissionStage.Question, cancelled, player: player, testMode: testMode);
        _yuxuAdmissionAnswered = answered;
        var question = social_label("小友，可有宗门？", 24);
        question.HorizontalAlignment = HorizontalAlignment.Center;
        column.AddChild(question);

        var choiceCenter = new CenterContainer(); column.AddChild(choiceCenter);
        var choices = new VBoxContainer { CustomMinimumSize = new Vector2(Mathf.Min(260, GetViewportRect().Size.X * .28f), 0) };
        choices.AddThemeConstantOverride("separation", 10);
        choiceCenter.AddChild(choices);
        add_yuxu_admission_choice(choices, "有", true);
        add_yuxu_admission_choice(choices, "无", false);
        _socialChoosing = true;
        _socialCancel = cancel_yuxu_admission;
    }

    /// <summary>
    /// 展示调用方提供的分支收束文字，并在点击“继续”或通过全局返回流程结束后调用 completed。
    /// 可用于“已有宗门”的礼貌结束，也可用于“尚无宗门”的引路反馈。
    /// </summary>
    public void show_yuxu_admission_result(WorldNpcData elder, IReadOnlyList<string> resultLines, Action completed, string title = "山门迎真",
        Character? player = null, bool testMode = false)
    {
        ArgumentNullException.ThrowIfNull(elder);
        ArgumentNullException.ThrowIfNull(completed);
        var lines = normalize_yuxu_admission_lines(resultLines, nameof(resultLines));

        var column = begin_yuxu_admission_stage(elder, YuxuAdmissionStage.Result, title: title, player: player, testMode: testMode);
        _yuxuAdmissionCompleted = completed;
        add_yuxu_admission_lines(column, lines);
        add_yuxu_admission_continue(column, continue_yuxu_admission_result);
        start_yuxu_admission_reveal();
    }

    private VBoxContainer begin_yuxu_admission_stage(WorldNpcData elder, YuxuAdmissionStage stage, Action? cancelled = null,
        string title = "山门迎真", Character? player = null, bool testMode = false)
    {
        // 清掉共享社交动画及旧底栏回调，避免阶段切换后残留旧交互状态。
        reset_social_presentation();
        _on_closed = null;
        hide_speaker_portrait();
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

        _socialPlayer = player;
        _socialTestMode = testMode;
        var column = build_social_stage(player, elder, title, testMode);
        _socialContent!.Name = "YuxuAdmissionInteraction";

        Show();
        MoveToFront();
        _socialChoosing = true;
        _socialCancel = cancel_yuxu_admission;
        return column;
    }

    private void add_yuxu_admission_choice(VBoxContainer parent, string text, bool hasSect)
    {
        var button = social_interaction_button(text);
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
        // 若主流程稍后才决定如何展示，全局返回流程仍可安全结束当前问题页。
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

    private void add_yuxu_admission_lines(VBoxContainer column, IReadOnlyList<string> lines)
    {
        var presentation = new List<NpcSocialPresentationText.Line>(lines.Count);
        string[] reviewWaits = ["长老凝神看了片刻", "长老缓缓抬眼", "长老略作沉吟", "长老收敛目光"];
        string[] resultWaits = ["对方缓缓开口", "对方略一颔首", "话声稍歇", "对方将话接过"];
        for (var index = 0; index < lines.Count; index++)
        {
            var pending = _yuxuAdmissionStage == YuxuAdmissionStage.Review
                ? reviewWaits[index % reviewWaits.Length] : resultWaits[index % resultWaits.Length];
            // 不抽换证据结果、不重排因果；只有等待语属于展示前奏。
            presentation.Add(new(pending, lines[index], "yuxu_evidence"));
        }
        _socialLines = presentation;
        _socialScroll = new ScrollContainer {
            Name = "YuxuAdmissionProcessScroll", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            CustomMinimumSize = new Vector2(0, Mathf.Min(lines.Count * 70f, GetViewportRect().Size.Y * .43f)) };
        column.AddChild(_socialScroll);
        var list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", 12); _socialScroll.AddChild(list);
        foreach (var line in presentation)
        {
            var label = social_label(line.Pending + "……", 21);
            label.CustomMinimumSize = new Vector2(0, 29);
            label.Modulate = new Color(1, 1, 1, 0); label.VisibleCharacters = 0;
            list.AddChild(label); _socialFactors.Add(label);
        }
    }

    private void add_yuxu_admission_continue(VBoxContainer column, Action action)
    {
        var center = new CenterContainer(); column.AddChild(center);
        _socialDone = social_interaction_button("略过展示");
        _socialDone.CustomMinimumSize = new Vector2(Mathf.Min(260, GetViewportRect().Size.X * .28f), 44);
        _socialDone.Pressed += action;
        center.AddChild(_socialDone);
    }

    private void start_yuxu_admission_reveal()
    {
        _socialRevealIndex = 0;
        _socialRevealElapsed = 0;
        _socialRevealing = _socialLines.Count > 0;
        if (!_socialRevealing)
        {
            if (_socialDone is not null) _socialDone.Text = "继续";
            return;
        }

        begin_social_line();
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
