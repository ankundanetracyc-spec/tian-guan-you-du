using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>名单右侧的局部人物页，复用真实NPC、立绘及交互组件。</summary>
public partial class NpcInteractionPanel : Control
{
    private Label? _name, _summary, _details, _introduction, _actionHint;
    private GridContainer? _actions;
    private ScrollContainer? _actionScroll;
    private PanelContainer? _frame;
    private Control? _portraitSlot;
    private PortraitRenderer? _portrait;
    private Func<IEnumerable<NpcInteractionAction>>? _actionResolver;
    private Func<WorldNpcData?>? _targetResolver;
    private string _actionSignature = "", _profileSignature = "", _displayedNpcId = "";
    private bool _profileMode;
    private string _notice = "";
    private int _generation;
    private const float ViewportMargin = 12f, RosterGap = 14f;

    public override void _Ready()
    {
        AddToGroup("npc_interaction_panel");
        Theme = QuietRoomStyle.MakeTheme();
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        ZIndex = 240;
        _frame = new PanelContainer { Name = "NpcInteractionPage", MouseFilter = MouseFilterEnum.Stop };
        _frame.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        AddChild(_frame);
        var paper = new WorldHudFrame { Name = "NpcPaperSurface" };
        _frame.AddChild(paper);
        paper.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var margin = new MarginContainer();
        foreach (var edge in new[] { "left", "right", "top", "bottom" })
            margin.AddThemeConstantOverride("margin_" + edge, 18);
        _frame.AddChild(margin);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 12);
        margin.AddChild(column);
        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 12);
        column.AddChild(header);
        _name = InkLabel("", 25);
        _name.Name = "NpcName";
        _name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _name.ClipText = true;
        _name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        header.AddChild(_name);
        var closeButton = ActionButton("关闭");
        closeButton.Name = "CloseButton";
        closeButton.CustomMinimumSize = new Vector2(58, 34);
        closeButton.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        closeButton.Pressed += close;
        header.AddChild(closeButton);
        var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 18);
        column.AddChild(body);
        _portraitSlot = new Control { Name = "PortraitSlot", MouseFilter = MouseFilterEnum.Ignore, SizeFlagsVertical = SizeFlags.ExpandFill };
        body.AddChild(_portraitSlot);
        _portrait = new PortraitRenderer { Name = "NpcPortrait", MouseFilter = MouseFilterEnum.Ignore };
        _portraitSlot.AddChild(_portrait);
        _portrait.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var right = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        right.AddThemeConstantOverride("separation", 10);
        body.AddChild(right);
        _summary = InkLabel("", 17);
        _summary.Name = "NpcSummary";
        _summary.AddThemeColorOverride("font_color", QuietRoomStyle.Teal);
        right.AddChild(_summary);
        _details = InkLabel("", 18);
        _details.Name = "NpcBasicInformation";
        _details.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        right.AddChild(_details);
        _introduction = InkLabel("", 15);
        _introduction.Name = "NpcIntroduction";
        _introduction.ClipText = true;
        _introduction.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        right.AddChild(_introduction);
        _actionScroll = new ScrollContainer
        {
            Name = "InteractionScroll", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill,
            ClipContents = true
        };
        right.AddChild(_actionScroll);
        _actions = new GridContainer { Name = "InteractionActions", Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _actions.AddThemeConstantOverride("h_separation", 10);
        _actions.AddThemeConstantOverride("v_separation", 9);
        _actionScroll.AddChild(_actions);
        _actionHint = InkLabel("", 13);
        _actionHint.Name = "InteractionRequirements";
        _actionHint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        right.AddChild(_actionHint);
        GetViewport().SizeChanged += place_next_to_roster;
        Hide();
    }

    public void show_npc_page(WorldNpcData npc, Func<WorldNpcData?> targetResolver,
        Func<IEnumerable<NpcInteractionAction>> actionResolver, string notice = "")
    {
        _generation++;
        _profileMode = true;
        _notice = notice;
        _targetResolver = targetResolver;
        _actionResolver = actionResolver;
        _profileSignature = "";
        _displayedNpcId = npc.npc_id;
        _portraitSlot?.Show();
        _summary?.Show(); _details?.Show();
        refresh_menu_actions(true);
        Show(); MoveToFront();
        CallDeferred(nameof(place_next_to_roster));
    }

    public void show_menu(string name, IEnumerable<(string label, Action action)> options)
    {
        prepare_legacy(name);
        _actionResolver = null;
        var actions = options.Select((option, index) => new NpcInteractionAction("fixed." + index, option.label, option.action)).ToArray();
        _actionSignature = signature(actions);
        rebuild_actions(actions);
        Show(); MoveToFront();
        CallDeferred(nameof(place_next_to_roster));
    }

    public void show_dynamic_menu(string name, Func<IEnumerable<NpcInteractionAction>> resolver)
    {
        prepare_legacy(name);
        _actionResolver = resolver;
        refresh_menu_actions(true);
        Show(); MoveToFront();
        CallDeferred(nameof(place_next_to_roster));
    }

    private void prepare_legacy(string name)
    {
        _generation++;
        _profileMode = false;
        _notice = "";
        _targetResolver = null; _displayedNpcId = ""; _profileSignature = "";
        if (_name is not null) _name.Text = name;
        _portraitSlot?.Hide(); _summary?.Hide(); _details?.Hide(); _introduction?.Hide();
    }

    public void refresh_menu_actions(bool force = true)
    {
        refresh_profile();
        if (_actionResolver is null) return;
        var next = _actionResolver().Where(action => action is not null).ToArray();
        var nextSignature = signature(next);
        if (force || nextSignature != _actionSignature)
        {
            _actionSignature = nextSignature;
            rebuild_actions(next);
            if (_actionScroll is not null) _actionScroll.ScrollVertical = 0;
        }
        CallDeferred(nameof(place_next_to_roster));
    }

    private void refresh_profile()
    {
        if (!_profileMode || _targetResolver?.Invoke() is not { } npc) return;
        var key = string.Join("|", npc.GetInstanceId(), npc.npc_id, npc.npc_name, npc.gender, npc.current_age,
            npc.cultivation_realm, npc.formal_identity, npc.faction_id, npc.current_state,
            npc.seclusion_until_month, npc.appearance_seed, npc.is_alive);
        if (key == _profileSignature) return;
        _profileSignature = key;
        _displayedNpcId = npc.npc_id;
        if (_name is not null) _name.Text = TextOr(npc.npc_name, "无名修士");
        if (_summary is not null) _summary.Text =
            TextOr(npc.species, "人族") + "  ·  " + (npc.gender == 1 ? "女" : "男") + $"  ·  {npc.current_age}岁";
        var faction = InfoPanel.npc_faction_name(npc.faction_id);
        if (faction == npc.faction_id && faction.Contains('_')) faction = "未详";
        var hour = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? 0;
        var state = npc.is_in_seclusion_at_hour(hour) ? "闭关中" : TextOr(npc.current_state, "空闲");
        if (_details is not null) _details.Text =
            "境界：" + TextOr(npc.cultivation_realm, "未详") +
            (NpcPublicIdentity.Title(npc) is { Length: > 0 } title ? "\n身份：" + title : "") +
            "\n所属：" + faction + "\n状态：" + state;
        if (_introduction is not null)
        {
            _introduction.Text = _notice;
            _introduction.TooltipText = _introduction.Text;
            _introduction.AutowrapMode = _notice.Length > 0 ? TextServer.AutowrapMode.WordSmart : TextServer.AutowrapMode.Off;
            _introduction.ClipText = _notice.Length == 0;
            _introduction.TextOverrunBehavior = _notice.Length > 0 ? TextServer.OverrunBehavior.NoTrimming : TextServer.OverrunBehavior.TrimEllipsis;
            _introduction.CustomMinimumSize = new Vector2(0, _notice.Length > 0 ? 76 : 0);
            _introduction.Visible = _introduction.Text.Length > 0;
        }
        _portrait?.show_world_npc(npc);
    }

    private void rebuild_actions(IEnumerable<NpcInteractionAction> options)
    {
        if (_actions is null) return;
        foreach (var child in _actions.GetChildren()) { _actions.RemoveChild(child); child.QueueFree(); }
        var list = options.ToArray();
        if (list.Length == 0) _actions.AddChild(InkLabel("当前没有可用交互", 16));
        var generation = _generation;
        foreach (var option in list)
        {
            var button = ActionButton(option.Label);
            button.SetMeta("action_id", option.Id);
            button.Disabled = !option.Enabled;
            button.TooltipText = option.UnavailableReason;
            if (option.Id is "attack" or "attack.confirm")
                foreach (var color in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color" })
                    button.AddThemeColorOverride(color, new Color("824d40"));
            button.Pressed += () =>
            {
                if (generation != _generation) return;
                var current = _actionResolver is null ? option : _actionResolver().FirstOrDefault(a => a.Id == option.Id);
                if (current is null || !current.Enabled) { refresh_menu_actions(); return; }
                close();
                current.Activate();
            };
            _actions.AddChild(button);
        }
        if (_actionHint is not null)
        {
            _actionHint.Text = string.Join("\n", list.Where(a => !a.Enabled && a.UnavailableReason.Length > 0)
                .Select(a => a.Label + "：" + a.UnavailableReason));
            _actionHint.Visible = _actionHint.Text.Length > 0;
        }
    }

    private void place_next_to_roster()
    {
        if (_frame is null || !IsInsideTree()) return;
        var viewport = GetViewportRect().Size;
        var roster = GetTree().GetFirstNodeInGroup("cell_character_list") as CellCharacterList;
        var rect = roster?.get_ui_rect() ?? new Rect2(20, 132, 312, 0);
        var left = Mathf.Max(ViewportMargin, rect.End.X + RosterGap);
        var top = Mathf.Max(110f, rect.Position.Y);
        var right = viewport.X - ViewportMargin;
        if (_profileMode && viewport.X - 384f - left >= 520f) right = viewport.X - 384f - ViewportMargin;
        var width = Mathf.Min(_profileMode ? 760f : 280f, Mathf.Max(160f, right - left));
        var maxHeight = Mathf.Max(220f, viewport.Y - top - (_profileMode ? 150f : ViewportMargin));
        var height = _profileMode ? Mathf.Min(580f, maxHeight)
            : Mathf.Min(maxHeight, 90f + (_actions?.GetCombinedMinimumSize().Y ?? 0));
        if (_actions is not null) _actions.Columns = _profileMode && width >= 500f ? 2 : 1;
        if (_portraitSlot is not null) _portraitSlot.CustomMinimumSize = new Vector2(Mathf.Clamp(width * .38f, 160f, 280f), 0);
        _frame.CustomMinimumSize = Vector2.Zero;
        _frame.Size = new Vector2(width, height);
        _frame.Position = new Vector2(left, Mathf.Min(top, Mathf.Max(ViewportMargin, viewport.Y - height - ViewportMargin)));
    }

    public Rect2 get_interaction_rect() => _frame?.GetGlobalRect() ?? new Rect2();
    public string get_displayed_npc_id() => _displayedNpcId;

    public void close()
    {
        _generation++;
        _actionResolver = null; _targetResolver = null;
        _actionSignature = ""; _profileSignature = ""; _displayedNpcId = "";
        Hide();
    }

    public override void _ExitTree() => GetViewport().SizeChanged -= place_next_to_roster;

    private static string TextOr(string value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value;
    private static string signature(IEnumerable<NpcInteractionAction> actions) => string.Join("\n",
        actions.Select(a => a.Id + "|" + a.Label + "|" + a.Enabled + "|" + a.UnavailableReason));

    private static Label InkLabel(string text, int size)
    {
        var label = new Label { Text = text, MouseFilter = MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", QuietRoomStyle.Ink);
        return label;
    }

    private static Button ActionButton(string text)
    {
        var button = new Button { Text = text, ClipText = true, CustomMinimumSize = new Vector2(0, 38),
            SizeFlagsHorizontal = SizeFlags.ExpandFill, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis };
        button.AddThemeFontSizeOverride("font_size", 17);
        foreach (var color in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color" })
            button.AddThemeColorOverride(color, QuietRoomStyle.Ink);
        button.AddThemeColorOverride("font_disabled_color", new Color("85867a"));
        foreach (var state in new[] { "normal", "hover", "pressed", "disabled" })
            button.AddThemeStyleboxOverride(state, new StyleBoxFlat
            {
                BgColor = new Color(state == "hover" ? "f1ead9" : state == "pressed" ? "d6d9c9" : "e7e0ce"),
                BorderColor = new Color(state == "disabled" ? "b1ad9f" : "8c8974"),
                BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
                CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3, CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3,
                ContentMarginLeft = 10, ContentMarginRight = 10
            });
        return button;
    }
}
