using Godot;
using System;
using System.Collections.Generic;

/// <summary>人物来访与过程浮层；复用模态入口、头像和费用，不播放日常逐句对白。</summary>
public partial class StoryDialoguePanel
{
    private Control? _socialContent;
    private Action? _socialCancel;
    private readonly List<Label> _socialFactors = [];
    private Label? _socialOutcome, _socialSpeech;
    private ProgressBar? _socialInk;
    private Button? _socialDone;
    private bool _socialChoosing, _socialRevealing;
    private int _socialRevealIndex;
    private double _socialRevealElapsed;
    private void reset_social_presentation()
    {
        if (_socialContent is not null) { _socialContent.Hide(); _socialContent.QueueFree(); _socialContent = null; }
        _socialFactors.Clear(); _socialOutcome = _socialSpeech = null; _socialCancel = null; _socialInk = null; _socialDone = null;
        _socialChoosing = _socialRevealing = false;
        if (_body is not null) _body.Modulate = Colors.White;
        _dialogueHeight = 250;
        if (_dialogueFrame is not null) { _dialogueFrame.Show(); _dialogueFrame.OffsetTop = -250; }
        if (_continueHint is not null) { _continueHint.Show(); _continueHint.Text = "点击任意处继续　›"; }
    }
    private static Label social_label(string text, int size, string color = "343d37")
    {
        var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart, MouseFilter = MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", size); label.AddThemeColorOverride("font_color", new Color(color)); return label;
    }
    private static StyleBoxFlat social_paper(string color) => new() {
        BgColor = new Color(color), BorderColor = new Color("92927d"), BorderWidthTop = 1, BorderWidthBottom = 1,
        ContentMarginLeft = 24, ContentMarginRight = 24, ContentMarginTop = 18, ContentMarginBottom = 18 };
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
    private VBoxContainer build_social_stage(WorldNpcData npc, string title)
    {
        _dialogueFrame!.Hide(); hide_speaker_portrait();
        var stage = new Control { Name = "SocialInteraction", MouseFilter = MouseFilterEnum.Stop };
        _socialContent = stage; AddChild(stage); stage.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var shade = new ColorRect { Color = new Color(.07f, .10f, .08f, .38f), MouseFilter = MouseFilterEnum.Ignore };
        stage.AddChild(shade); shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        stage.AddChild(center); center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var paper = new PanelContainer { Name = "SocialPaper", CustomMinimumSize = new Vector2(Mathf.Min(560, GetViewportRect().Size.X * .82f), 0) };
        center.AddChild(paper); paper.AddThemeStyleboxOverride("panel", social_paper("e8dfc9"));
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 14); paper.AddChild(column);
        var portraitRow = new CenterContainer(); column.AddChild(portraitRow);
        var portrait = new PortraitRenderer { Name = "SocialHeadshot", CustomMinimumSize = new Vector2(112, 112) };
        portrait.set_headshot_mode(true); portraitRow.AddChild(portrait); portrait.show_world_npc(npc);
        var name = social_label(npc.npc_name, 25); name.HorizontalAlignment = HorizontalAlignment.Center; column.AddChild(name);
        var heading = social_label(title + (NpcPersonalityRules.TestInspection ? " · 测试查阅" : " · 已知信息"), 20, "737b65");
        heading.HorizontalAlignment = HorizontalAlignment.Center;
        heading.MouseFilter = MouseFilterEnum.Pass;
        heading.TooltipText = NpcPersonalityRules.InspectionText(npc, NpcPersonalityRules.TestInspection);
        column.AddChild(heading);
        return column;
    }
    public void show_social_choices(Character player, WorldNpcData npc, string opening, IReadOnlyList<(string Text, Action Choose)> choices, Action cancel, string? title = null, string cancelHint = "Esc 婉拒")
    {
        show_npc_speech(npc.npc_name, opening);
        _socialChoosing = true; _socialCancel = cancel;
        var column = build_social_stage(npc, title ?? (NpcSocialRules.KnowsName(npc) ? "闲谈来访" : "希望结识"));
        column.AddChild(social_label(opening, 22));
        foreach (var (text, choose) in choices)
        {
            var button = social_button(text);
            button.Pressed += () => { if (!_socialChoosing) return; _socialChoosing = false; _socialCancel = null; choose(); };
            column.AddChild(button);
        }
        column.AddChild(social_label(cancelHint, 15, "797c6d"));
    }
    public void show_social_result(WorldNpcData npc, NpcSocialRules.Response response, Action? completed = null)
    {
        show_npc_speech(npc.npc_name, response.Result, completed);
        var column = build_social_stage(npc, "人物交互");
        _socialSpeech = social_label($"正在与{npc.npc_name}{response.ActionName}……", 22);
        column.AddChild(_socialSpeech);
        _socialInk = new ProgressBar { ShowPercentage = false, MaxValue = 1, CustomMinimumSize = new Vector2(0, 5), MouseFilter = MouseFilterEnum.Ignore };
        _socialInk.AddThemeStyleboxOverride("background", new StyleBoxFlat { BgColor = new Color("d0cab6") });
        _socialInk.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = new Color("536956") });
        column.AddChild(_socialInk);
        foreach (var factor in response.Factors)
        {
            var label = social_label(factor, 21, "68715d"); label.Modulate = new Color(1, 1, 1, 0);
            column.AddChild(label); _socialFactors.Add(label);
        }
        _socialOutcome = social_label(response.Result + (response.StaminaSpent > 0 ? $"\n体力 −{response.StaminaSpent:0.#}" : ""), 23);
        _socialOutcome.Modulate = new Color(1, 1, 1, 0); column.AddChild(_socialOutcome);
        _socialDone = social_button("略过展示"); _socialDone.Pressed += close; column.AddChild(_socialDone);
        _socialRevealIndex = 0; _socialRevealElapsed = 0; _socialRevealing = true;
        if (_socialFactors.Count == 0) finish_social_reveal();
    }
    private bool handle_social_close()
    {
        if (_socialChoosing) { var cancel = _socialCancel; _socialChoosing = false; _socialCancel = null; cancel?.Invoke(); return true; }
        if (_socialRevealing) { finish_social_reveal(); return true; }
        return false;
    }
    private void finish_social_reveal()
    {
        foreach (var factor in _socialFactors) factor.Modulate = Colors.White;
        if (_socialOutcome is not null) _socialOutcome.Modulate = Colors.White;
        if (_socialSpeech is not null) _socialSpeech.Text = "交互结束";
        if (_socialInk is not null) _socialInk.Value = 1;
        if (_socialDone is not null) _socialDone.Text = "确定";
        _socialRevealing = false;
    }
    private void process_social_reveal(double delta)
    {
        if (!_socialRevealing || !Visible) return;
        _socialRevealElapsed += delta;
        if (_socialInk is not null) _socialInk.Value = Mathf.Min(1, (float)((_socialRevealIndex + _socialRevealElapsed / .65) / (_socialFactors.Count + 1)));
        if (_socialRevealElapsed < .65) return;
        _socialRevealElapsed = 0;
        if (_socialRevealIndex < _socialFactors.Count) _socialFactors[_socialRevealIndex++].Modulate = Colors.White;
        else finish_social_reveal();
    }
}
