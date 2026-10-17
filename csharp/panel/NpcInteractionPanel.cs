using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>点选地点人物后的行动菜单，避免直接跳入人物台词。</summary>
public partial class NpcInteractionPanel : Control
{
    private Label? _name;
    private VBoxContainer? _actions;
    private ScrollContainer? _actionScroll;
    private Button? _leaveButton;
    private PanelContainer? _frame;
    private Func<IEnumerable<NpcInteractionAction>>? _actionResolver;
    private string _actionSignature = "";
    private const float ViewportMargin = 8f;
    private const float RosterGap = 12f;
    private const float PreferredWidth = 235f;

    public override void _Ready()
    {
        AddToGroup("npc_interaction_panel");
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        ZIndex = 240;

        var frame = new PanelContainer { MouseFilter = MouseFilterEnum.Stop };
        _frame = frame;
        frame.AnchorLeft = 0;
        frame.AnchorRight = 0;
        frame.AnchorTop = 0;
        frame.AnchorBottom = 0;
        // 紧挨左侧人物列表展开，保留名单本身可见。
        frame.OffsetLeft = 318;
        frame.OffsetTop = 136;
        frame.OffsetRight = 553;
        frame.OffsetBottom = 196;
        frame.AddThemeStyleboxOverride("panel", make_frame_style());
        AddChild(frame);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 16);
        margin.AddThemeConstantOverride("margin_top", 12);
        margin.AddThemeConstantOverride("margin_right", 16);
        margin.AddThemeConstantOverride("margin_bottom", 12);
        frame.AddChild(margin);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 8);
        margin.AddChild(column);
        _name = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            ClipText = true,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
        };
        _name.AddThemeFontSizeOverride("font_size", 21);
        _name.AddThemeColorOverride("font_color", new Color("e6c77e"));
        column.AddChild(_name);
        _actionScroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipContents = true,
        };
        column.AddChild(_actionScroll);
        _actions = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.Fill,
        };
        _actions.AddThemeConstantOverride("separation", 6);
        _actionScroll.AddChild(_actions);
        _leaveButton = new Button { Text = "关闭", CustomMinimumSize = new Vector2(0, 30) };
        _leaveButton.AddThemeFontSizeOverride("font_size", 16);
        _leaveButton.AddThemeColorOverride("font_color", new Color("d8d1c2"));
        _leaveButton.Pressed += close;
        column.AddChild(_leaveButton);
        GetViewport().SizeChanged += place_next_to_roster;
        Hide();
    }

    public void show_menu(string name, IEnumerable<(string label, Action action)> options)
    {
        _name!.Text = name;
        _actionResolver = null;
        var actions = options.Select((option, index) => new NpcInteractionAction($"fixed.{index}", option.label, option.action)).ToArray();
        _actionSignature = signature(actions);
        rebuild_actions(actions);
        Show();
        MoveToFront();
        CallDeferred(nameof(place_next_to_roster));
    }

    public void show_dynamic_menu(string name, Func<IEnumerable<NpcInteractionAction>> resolver)
    {
        _name!.Text = name;
        _actionResolver = resolver;
        refresh_menu_actions(true);
        Show();
        MoveToFront();
        CallDeferred(nameof(place_next_to_roster));
    }

    public void refresh_menu_actions(bool force = true)
    {
        if (_actionResolver is null) return;
        var next = _actionResolver().Where(action => action is not null).ToArray();
        var nextSignature = signature(next);
        if (!force && nextSignature == _actionSignature) return;
        _actionSignature = nextSignature;
        rebuild_actions(next);
        if (_actionScroll is not null) _actionScroll.ScrollVertical = 0;
        CallDeferred(nameof(place_next_to_roster));
    }

    private void rebuild_actions(IEnumerable<NpcInteractionAction> options)
    {
        if (_actions is null) return;
        foreach (var child in _actions.GetChildren())
        {
            _actions.RemoveChild(child);
            child.QueueFree();
        }
        var availableActions = options.Where(option => option is not null).ToArray();
        if (availableActions.Length == 0)
        {
            var empty = new Label
            {
                Text = "当前没有可用交互",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, 38),
            };
            empty.AddThemeFontSizeOverride("font_size", 15);
            empty.AddThemeColorOverride("font_color", new Color("b9b09a"));
            _actions.AddChild(empty);
            return;
        }

        foreach (var option in availableActions)
        {
            var button = new Button
            {
                Text = option.Label,
                TooltipText = option.Enabled || string.IsNullOrWhiteSpace(option.UnavailableReason) ? option.Label : option.UnavailableReason,
                CustomMinimumSize = new Vector2(0, 38),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                Disabled = !option.Enabled,
                TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            };
            button.AddThemeFontSizeOverride("font_size", 17);
            button.AddThemeColorOverride("font_color", new Color("e8dfcb"));
            button.AddThemeColorOverride("font_hover_color", new Color("fff0c6"));
            button.AddThemeColorOverride("font_pressed_color", new Color("fff6dd"));
            button.AddThemeColorOverride("font_disabled_color", new Color("8d887b"));
            button.AddThemeStyleboxOverride("normal", make_action_button_style(new Color("25251f"), new Color("62563c")));
            button.AddThemeStyleboxOverride("hover", make_action_button_style(new Color("39372c"), new Color("b79b5a")));
            button.AddThemeStyleboxOverride("pressed", make_action_button_style(new Color("51452e"), new Color("d0b36c")));
            button.AddThemeStyleboxOverride("disabled", make_action_button_style(new Color("1c1d19"), new Color("3d3a31")));
            button.Pressed += () =>
            {
                close();
                option.Activate();
            };
            _actions.AddChild(button);
            if (!option.Enabled && !string.IsNullOrWhiteSpace(option.UnavailableReason))
            {
                var reason = new Label
                {
                    Text = option.UnavailableReason,
                    AutowrapMode = TextServer.AutowrapMode.WordSmart,
                    SizeFlagsHorizontal = SizeFlags.ExpandFill,
                    CustomMinimumSize = new Vector2(0, 34),
                    MouseFilter = MouseFilterEnum.Ignore,
                };
                reason.AddThemeFontSizeOverride("font_size", 13);
                reason.AddThemeColorOverride("font_color", new Color("b9b09a"));
                _actions.AddChild(reason);
            }
        }
    }

    private void place_next_to_roster()
    {
        if (_frame is null || !IsInsideTree()) return;
        var roster = GetTree().GetFirstNodeInGroup("cell_character_list") as CellCharacterList;
        var viewport = GetViewportRect().Size;
        var maxWidth = Mathf.Max(1f, viewport.X - ViewportMargin * 2f);
        var maxHeight = Mathf.Max(1f, viewport.Y - ViewportMargin * 2f);
        var actionSize = _actions?.GetCombinedMinimumSize() ?? Vector2.Zero;
        var nameSize = _name?.GetCombinedMinimumSize() ?? Vector2.Zero;
        var leaveSize = _leaveButton?.GetCombinedMinimumSize() ?? new Vector2(0, 30);
        const float horizontalMargins = 32f;
        const float verticalMargins = 24f;
        const float columnGaps = 16f;
        var desiredWidth = Mathf.Max(PreferredWidth, Mathf.Max(actionSize.X, nameSize.X) + horizontalMargins);
        var width = Mathf.Min(maxWidth, desiredWidth);
        var fixedHeight = nameSize.Y + leaveSize.Y + verticalMargins + columnGaps;
        var desiredHeight = fixedHeight + actionSize.Y;
        var height = Mathf.Min(maxHeight, desiredHeight);
        var visibleActionsHeight = Mathf.Max(0f, height - fixedHeight);
        if (_actionScroll is not null)
            _actionScroll.CustomMinimumSize = new Vector2(Mathf.Max(0f, width - horizontalMargins), visibleActionsHeight);
        _frame.CustomMinimumSize = new Vector2(width, height);
        _frame.Size = new Vector2(width, height);

        var left = 318f;
        var top = 136f;
        var rosterRect = new Rect2();
        if (roster is not null)
        {
            rosterRect = roster.get_ui_rect();
            var rightCandidate = rosterRect.End.X + RosterGap;
            var leftCandidate = rosterRect.Position.X - width - RosterGap;
            if (rightCandidate + width <= viewport.X - ViewportMargin)
                left = rightCandidate;
            else if (leftCandidate >= ViewportMargin)
                left = leftCandidate;
            else
            {
                var rightSpace = viewport.X - ViewportMargin - rosterRect.End.X;
                var leftSpace = rosterRect.Position.X - ViewportMargin;
                left = rightSpace >= leftSpace
                    ? Mathf.Max(ViewportMargin, rightCandidate)
                    : Mathf.Max(ViewportMargin, rosterRect.Position.X - width - RosterGap);
            }
            top = rosterRect.Position.Y;
        }
        left = Mathf.Clamp(left, ViewportMargin, Mathf.Max(ViewportMargin, viewport.X - width - ViewportMargin));
        top = Mathf.Clamp(top, ViewportMargin, Mathf.Max(ViewportMargin, viewport.Y - height - ViewportMargin));
        _frame.Position = new Vector2(left, top);
    }

    public void close()
    {
        _actionResolver = null;
        _actionSignature = "";
        Hide();
    }

    public override void _ExitTree() => GetViewport().SizeChanged -= place_next_to_roster;

    private static string signature(IEnumerable<NpcInteractionAction> actions) => string.Join("\n", actions.Select(action => $"{action.Id}\u001f{action.Label}\u001f{action.Enabled}\u001f{action.UnavailableReason}"));

    private static StyleBoxFlat make_frame_style()
    {
        return new StyleBoxFlat
        {
            BgColor = new Color(0.02f, 0.027f, 0.022f, 0.94f),
            BorderColor = new Color("9a7a3d"),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6
        };
    }

    private static StyleBoxFlat make_action_button_style(Color background, Color border)
    {
        return new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            CornerRadiusBottomLeft = 3,
            CornerRadiusBottomRight = 3,
        };
    }
}
