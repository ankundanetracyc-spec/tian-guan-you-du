using Godot;
using System;
using System.Collections.Generic;

/// <summary>黑幕上的人物交互过程：两侧人物立绘，中间逐项呈现可见反应。</summary>
public partial class StoryDialoguePanel
{
    private const string SocialIconRoot = "res://textures/ui/social_interaction_20261013/";
    private Control? _socialContent;
    private Action? _socialCancel;
    private Character? _socialPlayer;
    private bool _socialTestMode;
    private readonly List<Label> _socialFactors = [];
    private Label? _socialOutcome, _socialSpeech;
    private ProgressBar? _socialInk;
    private Button? _socialDone;
    private bool _socialChoosing, _socialRevealing;
    private int _socialRevealIndex;
    private double _socialRevealElapsed;
    private IReadOnlyList<NpcSocialPresentationText.Line> _socialLines = [];
    private ScrollContainer? _socialScroll;
    private static int _socialVariation = System.Environment.TickCount;
    private static readonly int[] SocialTentativeDots = [1, 2, 1, 0, 1, 2, 1, 0];
    private bool _socialAnswerShown;

    private void reset_social_presentation()
    {
        if (_socialContent is not null) { _socialContent.Hide(); RemoveChild(_socialContent); _socialContent.QueueFree(); _socialContent = null; }
        _socialFactors.Clear(); _socialOutcome = _socialSpeech = null; _socialCancel = null; _socialPlayer = null; _socialTestMode = false;
        _socialInk = null; _socialDone = null; _socialChoosing = _socialRevealing = false;
        _socialLines = []; _socialScroll = null;
        if (_body is not null) _body.Modulate = Colors.White;
        _dialogueHeight = 250;
        if (_dialogueFrame is not null) { _dialogueFrame.Show(); _dialogueFrame.OffsetTop = -250; }
        if (_continueHint is not null) { _continueHint.Show(); _continueHint.Text = "点击任意处继续　›"; }
    }

    private static Label social_label(string text, int size, string color = "e6dfce")
    {
        var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart, HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", size); label.AddThemeColorOverride("font_color", new Color(color));
        label.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, .8f)); label.AddThemeConstantOverride("shadow_offset_x", 1); label.AddThemeConstantOverride("shadow_offset_y", 2);
        return label;
    }

    private static StyleBoxFlat social_paper(string color) => new()
    {
        BgColor = new Color(color), BorderColor = new Color("92927d"), BorderWidthTop = 1, BorderWidthBottom = 1,
        ContentMarginLeft = 24, ContentMarginRight = 24, ContentMarginTop = 18, ContentMarginBottom = 18
    };

    // 山门迎真共用的朴素纸面按钮；保留给该独立流程使用。
    private static Button social_button(string text)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(0, 44) };
        button.AddThemeFontSizeOverride("font_size", 22);
        foreach (var state in new[] { "normal", "hover", "pressed", "focus" })
            button.AddThemeStyleboxOverride(state, social_paper(state == "normal" ? "dcd4bb" : "c6c9b0"));
        button.AddThemeColorOverride("font_color", new Color("343d37"));
        button.AddThemeColorOverride("font_hover_color", new Color("263d30"));
        return button;
    }

    private static Button social_interaction_button(string text)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(0, 46), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        button.AddThemeFontSizeOverride("font_size", 19);
        button.AddThemeColorOverride("font_color", new Color("e7dec9"));
        button.AddThemeColorOverride("font_hover_color", new Color("fff2d1"));
        foreach (var (state, color) in new[] { ("normal", "292d29"), ("hover", "42483e"), ("pressed", "1c211e"), ("focus", "353a32") })
        {
            var style = new StyleBoxFlat
            {
                BgColor = new Color(color, .88f), BorderColor = new Color("8c8063", .75f),
                BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
                CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
                ContentMarginLeft = 18, ContentMarginRight = 18, ContentMarginTop = 8, ContentMarginBottom = 8
            };
            button.AddThemeStyleboxOverride(state, style);
        }
        return button;
    }

    private static (string Path, string Caption) social_action_icon(string? kind)
    {
        if (kind == "山门迎真") return (SocialIconRoot + "greeting_v1.png", "迎真");
        if (kind is "拜师礼" or "入门录籍") return (SocialIconRoot + "greeting_v1.png", kind);
        if (kind?.Contains("求购", StringComparison.Ordinal) == true || kind?.Contains("交易", StringComparison.Ordinal) == true) return (SocialIconRoot + "trade_v1.png", "交易");
        if (kind?.Contains("切磋", StringComparison.Ordinal) == true || kind?.Contains("较", StringComparison.Ordinal) == true) return (SocialIconRoot + "spar_v1.png", "切磋");
        if (kind?.Contains("来访", StringComparison.Ordinal) == true || kind?.Contains("拜访", StringComparison.Ordinal) == true) return (SocialIconRoot + "greeting_v1.png", "来访");
        if (kind?.Contains("闲谈", StringComparison.Ordinal) == true) return (SocialIconRoot + "greeting_v1.png", "闲谈");
        if (kind?.Contains("告辞", StringComparison.Ordinal) == true) return (SocialIconRoot + "greeting_v1.png", "告辞");
        return (SocialIconRoot + "greeting_v1.png", "结识");
    }

    private VBoxContainer build_social_stage(Character? player, WorldNpcData npc, string? kind, bool testMode)
    {
        _dialogueFrame!.Hide(); hide_speaker_portrait();
        var viewport = GetViewportRect().Size;
        var portraitWidth = Mathf.Clamp(viewport.X * .20f, 130f, 300f);
        var portraitHeight = Mathf.Min(portraitWidth * 1.5f, viewport.Y * .7f);
        var stage = new Control { Name = "SocialInteraction", MouseFilter = MouseFilterEnum.Stop };
        _socialContent = stage; AddChild(stage); stage.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var shade = new ColorRect { Color = new Color(.018f, .022f, .02f, .74f), MouseFilter = MouseFilterEnum.Ignore };
        stage.AddChild(shade); shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var composition = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        composition.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        stage.AddChild(composition);

        var row = new HBoxContainer { Name = "SocialComposition", MouseFilter = MouseFilterEnum.Ignore };
        composition.AddChild(row);
        var playerData = player?.entity_data as HumanData;
        var leftPortrait = build_social_portrait(player?.entity_name ?? "我", npc, true, portraitWidth, portraitHeight, testMode, playerData);
        row.AddChild(leftPortrait.Column);
        if (player is not null) leftPortrait.Portrait.show_character(player);

        var center = new VBoxContainer { Name = "InteractionProcess", SizeFlagsVertical = Control.SizeFlags.ShrinkCenter, Alignment = BoxContainer.AlignmentMode.Center };
        center.AddThemeConstantOverride("separation", 14); row.AddChild(center);
        var iconInfo = social_action_icon(kind);
        var iconSlot = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        var iconColumn = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        iconColumn.AddThemeConstantOverride("separation", 4); iconSlot.AddChild(iconColumn);
        var icon = new TextureRect
        {
            Name = "SocialActionIcon", Texture = GD.Load<Texture2D>(iconInfo.Path),
            CustomMinimumSize = new Vector2(138, 92), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore
        };
        iconColumn.AddChild(icon);
        iconColumn.AddChild(social_label(iconInfo.Caption, 18, "c6bdab"));
        center.AddChild(iconSlot);
        var rightPortrait = build_social_portrait(string.IsNullOrWhiteSpace(npc.npc_name) ? "对方" : npc.npc_name, npc, false, portraitWidth, portraitHeight, testMode, playerData);
        row.AddChild(rightPortrait.Column);
        rightPortrait.Portrait.show_world_npc(npc);
        void LayoutComposition()
        {
            if (_socialContent != stage) return;
            var size = GetViewportRect().Size;
            var width = Mathf.Clamp(size.X * .20f, 130f, 300f);
            var height = Mathf.Min(width * 1.5f, size.Y * .7f);
            var gap = Mathf.RoundToInt(Mathf.Clamp(size.X * .018f, 16f, 30f));
            row.AddThemeConstantOverride("separation", gap);
            leftPortrait.Column.CustomMinimumSize = rightPortrait.Column.CustomMinimumSize = new Vector2(width, 0);
            leftPortrait.Portrait.CustomMinimumSize = rightPortrait.Portrait.CustomMinimumSize = new Vector2(width, height);
            center.CustomMinimumSize = new Vector2(Mathf.Max(180, Mathf.Min(620, size.X - 48 - width * 2 - gap * 2)), 0);
            if (_socialScroll is not null)
                _socialScroll.CustomMinimumSize = new Vector2(0, Mathf.Min(_socialLines.Count * (_yuxuAdmissionActive ? 76f : 38f), Mathf.Max(120, size.Y * .43f)));
        }
        stage.Resized += LayoutComposition;
        LayoutComposition();
        return center;
    }

    private (VBoxContainer Column, PortraitRenderer Portrait) build_social_portrait(string name, WorldNpcData npc, bool isPlayer, float width, float height, bool testMode, HumanData? playerData)
    {
        var column = new VBoxContainer { CustomMinimumSize = new Vector2(width, 0), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter, Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore };
        column.AddThemeConstantOverride("separation", 12);
        var portraitCenter = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        var portrait = new PortraitRenderer { Name = isPlayer ? "PlayerSocialPortrait" : "NpcSocialPortrait", CustomMinimumSize = new Vector2(width, height) };
        portraitCenter.AddChild(portrait); column.AddChild(portraitCenter);
        var nameLabel = social_label(name, 21, "e6d7b5"); nameLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        if (!isPlayer && NpcPersonalityRules.TestInspection)
        {
            nameLabel.TooltipText = NpcPersonalityRules.InspectionText(npc, testing: true);
            if (testMode)
            {
                var temporary = NpcSocialPrototypeRules.GetTemporaryStats(npc);
                nameLabel.TooltipText += $"\n\n仅测试试算 · 不存档\nNPC 临时魅力 {temporary.Charisma} · 交际 {temporary.Social} · 心境 {npc.mood_value}";
                if (playerData is not null)
                    nameLabel.TooltipText += $"\n玩家魅力 {playerData.charisma:0.#} · 交际 {playerData.social:0.#} · 心境 {playerData.mood_value}";
            }
        }
        column.AddChild(nameLabel);
        return (column, portrait);
    }

    public void show_social_choices(Character player, WorldNpcData npc, string opening, IReadOnlyList<(string Text, Action Choose)> choices, Action cancel, string? title = null, string cancelHint = "右键婉拒", bool testMode = false)
    {
        show_npc_speech(npc.npc_name, opening);
        _socialPlayer = player; _socialTestMode = testMode;
        _socialChoosing = true; _socialCancel = cancel;
        var column = build_social_stage(player, npc, title, testMode);
        var openingLabel = social_label(opening, 24); openingLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        column.AddChild(openingLabel);

        var choiceCenter = new CenterContainer(); column.AddChild(choiceCenter);
        var choiceList = new VBoxContainer { Name = "SocialChoices", CustomMinimumSize = new Vector2(Mathf.Min(300, GetViewportRect().Size.X * .28f), 0) };
        choiceList.AddThemeConstantOverride("separation", 10); choiceCenter.AddChild(choiceList);
        foreach (var (text, choose) in choices)
        {
            var button = social_interaction_button(text);
            button.Pressed += () => { if (!_socialChoosing) return; _socialChoosing = false; _socialCancel = null; choose(); };
            choiceList.AddChild(button);
        }
        var hint = social_label(cancelHint, 15, "c6bda8"); column.AddChild(hint);
    }

    public void show_social_result(WorldNpcData npc, NpcSocialRules.Response response, Action? completed = null)
    {
        var player = _socialPlayer;
        var testMode = _socialTestMode;
        show_npc_speech(npc.npc_name, response.Result, completed);
        _socialPlayer = player; _socialTestMode = testMode;
        var column = build_social_stage(player, npc, response.ActionName, testMode);
        if (response.Recollection is { } recall && NpcPersonalRecallRules.IsValid(npc, recall))
        {
            var recollection = social_label(recall.Text, 19, "cfc8b8");
            recollection.Name = "SocialRecollection";
            column.AddChild(recollection);
        }
        _socialLines = NpcSocialPresentationText.Create(response, System.Threading.Interlocked.Increment(ref _socialVariation));
        _socialScroll = new ScrollContainer
        {
            Name = "SocialProcessScroll", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            CustomMinimumSize = new Vector2(0, Mathf.Min(_socialLines.Count * 38f, Mathf.Max(120, GetViewportRect().Size.Y * .43f)))
        };
        column.AddChild(_socialScroll);
        var lines = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        lines.AddThemeConstantOverride("separation", 9); _socialScroll.AddChild(lines);
        foreach (var line in _socialLines)
        {
            var label = social_label(line.Pending + "……", 21, "e5dece");
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.CustomMinimumSize = new Vector2(0, 29);
            label.Modulate = new Color(1, 1, 1, 0); label.VisibleCharacters = 0;
            lines.AddChild(label); _socialFactors.Add(label);
        }
        if (_socialLines.Count == 0) _socialScroll.Hide();
        _socialOutcome = social_label(response.Result + (response.StaminaSpent > 0 ? $"\n体力 −{response.StaminaSpent:0.#}" : "") +
            (string.IsNullOrWhiteSpace(response.RelationshipFeedback) ? "" : $"\n关系记事：{response.RelationshipFeedback}"), 22, "e8dcbf");
        _socialOutcome.Modulate = new Color(1, 1, 1, 0); column.AddChild(_socialOutcome);
        var buttonCenter = new CenterContainer(); column.AddChild(buttonCenter);
        _socialDone = social_interaction_button("略过过程");
        _socialDone.CustomMinimumSize = new Vector2(Mathf.Min(260, GetViewportRect().Size.X * .28f), 44);
        _socialDone.Pressed += close; buttonCenter.AddChild(_socialDone);
        _socialRevealIndex = 0; _socialRevealElapsed = 0; _socialRevealing = true;
        if (_socialFactors.Count == 0) finish_social_reveal();
        else begin_social_line();
    }

    private bool handle_social_close()
    {
        if (_socialChoosing) { var cancel = _socialCancel; _socialChoosing = false; _socialCancel = null; cancel?.Invoke(); return true; }
        if (_socialRevealing) { finish_social_reveal(); return true; }
        return false;
    }

    private void finish_social_reveal()
    {
        for (var index = 0; index < _socialFactors.Count; index++)
        {
            var factor = _socialFactors[index];
            if (_socialLines.Count == _socialFactors.Count) factor.Text = _socialLines[index].Result;
            factor.VisibleCharacters = -1; factor.Modulate = Colors.White;
        }
        if (_socialOutcome is not null) _socialOutcome.Modulate = Colors.White;
        if (_socialSpeech is not null) _socialSpeech.Text = "对方收住话头，交谈暂告一段落。";
        if (_socialInk is not null) _socialInk.Value = 1;
        if (_socialDone is not null) _socialDone.Text = _yuxuAdmissionActive ? "继续" : "确定";
        _socialRevealing = false;
    }

    private void begin_social_line()
    {
        var label = _socialFactors[_socialRevealIndex];
        _socialAnswerShown = false;
        label.Text = _socialLines[_socialRevealIndex].Pending + "……";
        label.VisibleCharacters = 0; label.Modulate = new Color(.78f, .80f, .74f);
        var scroll = _socialScroll;
        Callable.From(() =>
        {
            if (scroll is not null && IsInstanceValid(scroll) && IsInstanceValid(label) && label.IsInsideTree())
                scroll.EnsureControlVisible(label);
        }).CallDeferred();
    }

    private void process_social_reveal(double delta)
    {
        if (!_socialRevealing || !Visible) return;
        _socialRevealElapsed += delta;
        if (_socialLines.Count > 0)
        {
            // 每行：逐字写出等待语 → 句尾进退两次 → 撤回 → 显出结果 → 下一行。
            while (_socialRevealIndex < _socialLines.Count)
            {
                var line = _socialLines[_socialRevealIndex];
                var label = _socialFactors[_socialRevealIndex];
                var writing = line.Pending.Length * .05;
                const double hesitation = .88, appearing = .20, hold = .28;
                var retract = (line.Pending.Length + 2) * .016;
                var duration = writing + hesitation + retract + appearing + hold;
                var elapsed = _socialRevealElapsed;
                if (elapsed >= duration)
                {
                    label.Text = line.Result; label.VisibleCharacters = -1; label.Modulate = Colors.White;
                    _socialRevealElapsed -= duration;
                    if (++_socialRevealIndex == _socialLines.Count) { finish_social_reveal(); return; }
                    begin_social_line(); continue;
                }
                if (elapsed < writing)
                    label.VisibleCharacters = Math.Min(line.Pending.Length, (int)(elapsed / .05));
                else if (elapsed < writing + hesitation)
                {
                    label.VisibleCharacters = line.Pending.Length + SocialTentativeDots[Math.Min(7, (int)((elapsed - writing) / .11))];
                    label.Modulate = new Color(.78f, .80f, .74f, .76f + .24f * Mathf.Abs(Mathf.Sin((float)elapsed * 8)));
                }
                else if (elapsed < writing + hesitation + retract)
                    label.VisibleCharacters = Math.Max(0, line.Pending.Length + 2 - (int)((elapsed - writing - hesitation) / .016));
                else
                {
                    if (!_socialAnswerShown) { label.Text = line.Result; label.VisibleCharacters = -1; _socialAnswerShown = true; }
                    label.Modulate = new Color(1, 1, 1, Mathf.Clamp((float)((elapsed - writing - hesitation - retract) / appearing), 0, 1));
                }
                return;
            }
            return;
        }
        // 山门迎真沿用自身的证据与阶段回调。
        if (_socialInk is not null) _socialInk.Value = Mathf.Min(1, (float)((_socialRevealIndex + _socialRevealElapsed / .65) / (_socialFactors.Count + 1)));
        if (_socialRevealElapsed < .65) return;
        _socialRevealElapsed = 0;
        if (_socialRevealIndex < _socialFactors.Count) _socialFactors[_socialRevealIndex++].Modulate = Colors.White;
        else finish_social_reveal();
    }
}
