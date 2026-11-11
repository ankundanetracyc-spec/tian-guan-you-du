using Godot;
using System;
using System.Collections.Generic;

/// <summary>对话卡右上方的独立回应纸签；台词继续留在底部剧情卡。</summary>
public partial class StoryDialoguePanel
{
    private VBoxContainer? _bottomChoices;
    private ScrollContainer? _responseScroll;
    private Action? _bottomCancel;
    private bool _bottomChoosing;
    private int _bottomChoiceGeneration;

    /// <summary>事项说明使用旁白身份；有标题时将标题放在正文开头。</summary>
    public void show_narration_choices(string title, string body,
        IEnumerable<(string Label, Action Activate)> options, Action? onCancel = null)
    {
        var trimmedTitle = title.Trim();
        var narration = trimmedTitle.Length == 0 ? body : $"{trimmedTitle}：{body}";
        show_bottom_choices("旁白", narration, null, options, onCancel);
    }

    public void show_bottom_choices(string speaker, string body, WorldNpcData? npc,
        IEnumerable<(string Label, Action Activate)> options, Action? onCancel = null)
    {
        if (npc is null) { hide_speaker_portrait(); present(speaker, body, false, null); }
        else show_npc_speech(speaker, body, npc);
        if (_dialogueColumn is null) return;
        _bottomChoosing = true; _bottomCancel = onCancel;
        if (_continueHint is not null) { _continueHint.Text = "选择回应"; _continueHint.Show(); }
        _responseScroll = new ScrollContainer { Name = "DialogueResponses", MouseFilter = MouseFilterEnum.Ignore,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            // 立绘内部各部件有独立层级；重叠时选项必须完整显示在它们之上。
            ClipContents = true, ZAsRelative = false, ZIndex = 260 };
        AddChild(_responseScroll);
        _bottomChoices = new VBoxContainer { Name = "DialogueResponseList", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _bottomChoices.AddThemeConstantOverride("separation", 10);
        _responseScroll.AddChild(_bottomChoices);
        var generation = _bottomChoiceGeneration;
        foreach (var option in options)
        {
            var button = new Button { Text = option.Label, CustomMinimumSize = new Vector2(0, 54),
                SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = HorizontalAlignment.Left,
                ClipText = true, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis };
            WorldHudStyle.StylePaperButton(button, 21);
            foreach (var state in new[] { "normal", "hover", "pressed" })
                button.AddThemeStyleboxOverride(state, new StyleBoxEmpty { ContentMarginLeft = 22, ContentMarginRight = 36 });
            var backing = new ColorRect { Color = new Color("edeade"), MouseFilter = MouseFilterEnum.Ignore, ShowBehindParent = true };
            button.AddChild(backing); button.MoveChild(backing, 0);
            backing.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            backing.OffsetLeft = backing.OffsetTop = 6; backing.OffsetRight = backing.OffsetBottom = -6;
            var arrow = new Label { Text = "›", MouseFilter = MouseFilterEnum.Ignore, Visible = false };
            arrow.AddThemeFontSizeOverride("font_size", 25); arrow.AddThemeColorOverride("font_color", QuietRoomStyle.Teal);
            button.AddChild(arrow); arrow.AnchorLeft = arrow.AnchorRight = 1; arrow.AnchorTop = arrow.AnchorBottom = .5f;
            arrow.OffsetLeft = -28; arrow.OffsetRight = -8; arrow.OffsetTop = -16; arrow.OffsetBottom = 16;
            button.MouseEntered += () => arrow.Show(); button.MouseExited += () => arrow.Hide();
            _bottomChoices.AddChild(button);
            button.Resized += () => button.TooltipText = button.GetThemeFont("font").GetStringSize(button.Text,
                HorizontalAlignment.Left, -1, button.GetThemeFontSize("font_size")).X > button.Size.X - 58f ? button.Text : "";
            button.Pressed += () =>
            {
                if (!_bottomChoosing || generation != _bottomChoiceGeneration) return;
                reset_bottom_choices(); hide_speaker_portrait(); Hide(); _on_closed = null;
                option.Activate();
            };
        }
        layout_bottom_dialogue(); CallDeferred(nameof(layout_floating_choices));
    }

    private void reset_bottom_choices()
    {
        _bottomChoiceGeneration++;
        _bottomChoosing = false; _bottomCancel = null;
        if (_responseScroll is not null)
        {
            _responseScroll.GetParent()?.RemoveChild(_responseScroll);
            _responseScroll.QueueFree(); _responseScroll = null;
        }
        _bottomChoices = null;
    }

    private bool close_bottom_choices()
    {
        if (!_bottomChoosing) return false;
        var cancel = _bottomCancel;
        reset_bottom_choices(); hide_speaker_portrait(); Hide(); _on_closed = null;
        cancel?.Invoke(); return true;
    }
}
