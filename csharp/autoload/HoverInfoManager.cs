using Godot;
using System;
using System.Collections.Generic;

/// <summary>One shared ink-paper hover reader for runtime controls and rich object details.</summary>
public partial class HoverInfoManager : CanvasLayer
{
    private const double ShowDelay = 0.10;
    private const double HideGrace = 0.10;
    private const float ViewportMargin = 12f;
    private const float PreferredWidth = 460f;
    private const string BoundMeta = "shared_hover_info_bound";
    private static HoverInfoManager? _instance;

    private readonly Dictionary<Control, HoverInfoContent> _entries = new();
    private readonly Dictionary<Control, Func<HoverInfoContent>> _providers = new();
    private PanelContainer? _card;
    private TextureRect? _icon;
    private InventoryFootprintView? _headerFootprint;
    private HoverFootprintFooter? _footprints;
    private Label? _title;
    private Label? _subtitle;
    private ScrollContainer? _scroll;
    private RichTextLabel? _body;
    private HBoxContainer? _bodyColumns;
    private RichTextLabel? _secondaryBody;
    private string _bodySource = "";
    private float _bodyWidth;
    private Godot.Timer? _showTimer;
    private Godot.Timer? _hideTimer;
    private Control? _pendingAnchor;
    private Control? _activeAnchor;
    private HoverInfoContent? _visibleContent;
    private readonly HashSet<Node> _dragOwners = new();
    private readonly HashSet<Node> _dragOwnerHooks = new();
    private bool _nativeDragging;
    private bool _dragWasActive;
    private Vector2 _pointerPosition;
    private int _layoutTicket;
    private Button? _rulesButton;
    private Control? _rulesOverlay;
    private RichTextLabel? _rulesText;
    private Control? _rulesOwner;

    public readonly record struct HoverInfoSection(string Title, string Body);

    public readonly record struct HoverInfoContent(string Title, string Body, Texture2D? Icon = null, string Subtitle = "",
        IReadOnlyList<HoverInfoSection>? Sections = null, float CardWidth = PreferredWidth, float IconSize = 64f,
        bool ShowRules = true, bool LiveUpdate = false, Vector2I? Footprint = null,
        IReadOnlyList<HoverGridDiagram>? GridDiagrams = null, string RulesText = "", string RulesTitle = "",
        bool InventoryFootprintBelowTitle = false, bool CompactGridFooter = false, bool SingleColumn = false);

    /// <summary>Keep plain text available to callers while giving the shared reader explicit sections.</summary>
    public static HoverInfoContent Structured(string title, Texture2D? icon, string subtitle, params HoverInfoSection[] sections)
    {
        var populated = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Where(sections, section => !string.IsNullOrWhiteSpace(section.Body)));
        return new(title, string.Join("\n\n", System.Linq.Enumerable.Select(populated,
            section => string.IsNullOrWhiteSpace(section.Title) ? section.Body : section.Title + "\n" + section.Body)), icon, subtitle, populated);
    }

    private readonly HashSet<Control> _suspended = new();

    /// <summary>Custom drags suppress the shared reader until their owning node releases or leaves the tree.</summary>
    public static void SetDragInProgress(Node? owner, bool dragging)
    {
        if (owner is null || !GodotObject.IsInstanceValid(owner) || _instance is not { } manager) return;
        if (dragging)
        {
            if (!owner.IsInsideTree()) return;
            manager._dragOwners.Add(owner);
            if (manager._dragOwnerHooks.Add(owner))
                owner.TreeExiting += () => { manager._dragOwners.Remove(owner); manager._dragOwnerHooks.Remove(owner); };
            manager._dragWasActive = true;
            manager.hide_all();
        }
        else manager._dragOwners.Remove(owner);
    }

    public static bool IsDragInProgress() => _instance is { } manager && GodotObject.IsInstanceValid(manager)
        && manager.drag_in_progress();

    private bool drag_in_progress() => _nativeDragging || _dragOwners.Count > 0 || GetViewport().GuiIsDragging();

    public override void _Notification(int what)
    {
        if (what == Control.NotificationDragBegin)
        {
            _nativeDragging = true; _dragWasActive = true; hide_all();
        }
        else if (what == Control.NotificationDragEnd) _nativeDragging = false;
    }

    public static void SuspendFor(Control? anchor, bool suspended)
    {
        if (anchor is null || _instance is not { } manager) return;
        if (suspended)
        {
            manager._suspended.Add(anchor);
            if (manager._activeAnchor == anchor || manager._pendingAnchor == anchor) manager.hide_all();
        }
        else manager._suspended.Remove(anchor);
    }

    public override void _EnterTree()
    {
        _instance = this;
        Layer = 160;
        ProcessMode = ProcessModeEnum.Always;
    }

    public override void _Ready()
    {
        PlayerInfoText.ModeChanged += on_information_mode_changed;
        _pointerPosition = GetViewport().GetMousePosition();
        build_card();
        _showTimer = new Godot.Timer { OneShot = true, WaitTime = ShowDelay, ProcessMode = ProcessModeEnum.Always };
        _hideTimer = new Godot.Timer { OneShot = true, WaitTime = HideGrace, ProcessMode = ProcessModeEnum.Always };
        AddChild(_showTimer);
        AddChild(_hideTimer);
        _showTimer.Timeout += show_pending;
        _hideTimer.Timeout += hide_if_pointer_left;
        GetTree().NodeAdded += on_node_added;
        GetViewport().SizeChanged += on_viewport_resized;
        scan_subtree(GetTree().Root);
    }

    public override void _ExitTree()
    {
        PlayerInfoText.ModeChanged -= on_information_mode_changed;
        if (GetTree() is { } tree) tree.NodeAdded -= on_node_added;
        if (GetViewport() is { } viewport) viewport.SizeChanged -= on_viewport_resized;
        if (_instance == this) _instance = null;
    }

    public override void _Process(double delta)
    {
        if (drag_in_progress())
        {
            _dragWasActive = true;
            hide_all();
            return;
        }
        if (_dragWasActive)
        {
            _dragWasActive = false;
            // A drop may leave the pointer over a control without another MouseEntered.
            // Re-arm only the actual source beneath it, with the normal show delay.
            for (var hovered = GetViewport().GuiGetHoveredControl(); hovered is not null; hovered = hovered.GetParent() as Control)
                if (_entries.ContainsKey(hovered) || _providers.ContainsKey(hovered))
                { on_anchor_entered(hovered); break; }
        }
        if (_rulesOverlay?.Visible == true && (_rulesOwner is null || !GodotObject.IsInstanceValid(_rulesOwner) || !_rulesOwner.IsVisibleInTree()))
            _rulesOverlay.Hide();
        // Some existing controls refresh TooltipText as their state changes. Capture only
        // the currently displayed control, rather than polling or laying out the whole UI.
        if (_activeAnchor is null || !GodotObject.IsInstanceValid(_activeAnchor)) return;
        if (!string.IsNullOrWhiteSpace(_activeAnchor.TooltipText))
        {
            var changed = _activeAnchor.TooltipText;
            set_tooltip(_activeAnchor, changed);
        }
        if (!_activeAnchor.IsVisibleInTree()) { hide_all(); return; }
        if (pointer_over(_activeAnchor)) _hideTimer?.Stop();
        else if (_hideTimer?.IsStopped() == true) _hideTimer.Start(HideGrace);
    }

    public override void _Input(InputEvent @event)
    {
        // GUI signals run after _Input, so use this event's viewport position immediately.
        if (@event is InputEventMouse pointer) _pointerPosition = pointer.Position;
        if (drag_in_progress()) { _dragWasActive = true; hide_all(); return; }
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F1 } && _card?.Visible == true && _rulesButton?.Visible == true)
        { show_rules(); GetViewport().SetInputAsHandled(); return; }
        if (_rulesOverlay?.Visible == true && @event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        { _rulesOverlay.Hide(); GetViewport().SetInputAsHandled(); return; }
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Middle }) hide_all();
    }

    public static void SetTooltip(Control? anchor, string? text)
    {
        if (_instance is { } manager && GodotObject.IsInstanceValid(manager))
            manager.set_tooltip(anchor, text ?? "");
    }

    public static void SetInfo(Control? anchor, HoverInfoContent content, bool immediate = false)
    {
        if (_instance is { } manager && GodotObject.IsInstanceValid(manager))
        {
            if (anchor is not null) manager._providers.Remove(anchor);
            manager.set_info(anchor, content, immediate);
        }
    }

    /// <summary>Read the current role when hover opens, rather than freezing derived numbers when a list is built.</summary>
    public static void SetProvider(Control? anchor, Func<HoverInfoContent> provider)
    {
        if (anchor is null || !GodotObject.IsInstanceValid(anchor) || _instance is not { } manager) return;
        manager.bind_control(anchor);
        anchor.TooltipText = "";
        manager._providers[anchor] = provider;
        if (!manager.drag_in_progress() && (manager._activeAnchor == anchor || manager.pointer_over(anchor)))
            manager.set_info(anchor, provider(), false);
    }

    public static void CopyInfo(Control? source, Control? destination)
    {
        if (source is null || destination is null || !GodotObject.IsInstanceValid(source) || !GodotObject.IsInstanceValid(destination)) return;
        if (_instance is { } manager && GodotObject.IsInstanceValid(manager))
        {
            if (manager._providers.TryGetValue(source, out var provider)) { SetProvider(destination, provider); return; }
            if (manager._entries.TryGetValue(source, out var content)) { manager.set_info(destination, content); return; }
        }
        destination.TooltipText = source.TooltipText;
    }

    public static void HideFor(Control? anchor)
    {
        if (_instance is { } manager && GodotObject.IsInstanceValid(manager))
            manager.hide_for(anchor);
    }

    public static void HideAll()
    {
        if (_instance is { } manager && GodotObject.IsInstanceValid(manager))
            manager.hide_all();
    }

    public static bool DismissVisible()
    {
        if (_instance is { } reader && reader._rulesOverlay?.Visible == true) { reader._rulesOverlay.Hide(); return true; }
        if (_instance is not { } manager || !GodotObject.IsInstanceValid(manager) || manager._card?.Visible != true) return false;
        manager.hide_all();
        return true;
    }

    private void build_card()
    {
        _card = new PanelContainer
        {
            Name = "SharedHoverInfoCard",
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 10,
        };
        _card.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        var frame = new InventoryPaperFrame { Dark = true };
        _card.AddChild(frame);
        frame.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var margin = new MarginContainer();
        foreach (var side in new[] { "left", "right" }) margin.AddThemeConstantOverride($"margin_{side}", 18);
        foreach (var side in new[] { "top", "bottom" }) margin.AddThemeConstantOverride($"margin_{side}", 15);
        _card.AddChild(margin);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 8);
        margin.AddChild(column);

        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 12);
        column.AddChild(header);
        _icon = new TextureRect
        {
            CustomMinimumSize = new Vector2(64, 64),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        header.AddChild(_icon);
        _title = make_label(24, new Color("f0ead7"));
        _title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _title.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        var identity = new VBoxContainer
        {
            Name = "ItemIdentity",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };
        identity.AddThemeConstantOverride("separation", 8);
        header.AddChild(identity);
        identity.AddChild(_title);
        _headerFootprint = new InventoryFootprintView
        {
            Name = "HeaderInventoryFootprint",
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
        };
        identity.AddChild(_headerFootprint);
        _subtitle = make_label(17, new Color("d7c8a0"));
        _subtitle.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        column.AddChild(_subtitle);
        var separator = new ColorRect { Color = new Color(.61f, .58f, .47f, .38f), CustomMinimumSize = new Vector2(0, 1), MouseFilter = Control.MouseFilterEnum.Ignore };
        column.AddChild(separator);

        _scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        column.AddChild(_scroll);
        _body = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _body.AddThemeFontOverride("normal_font", GD.Load<FontFile>("res://resources/font/LXGWZhenKaiSlabGB-Regular.ttf"));
        _body.AddThemeFontOverride("bold_font", GD.Load<FontFile>("res://resources/font/LXGWZhenKaiSlabGB-Regular.ttf"));
        _body.AddThemeFontSizeOverride("normal_font_size", 18);
        _body.AddThemeColorOverride("default_color", new Color("ded8c6"));
        _body.AddThemeConstantOverride("line_separation", 5);
        _bodyColumns = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _bodyColumns.AddThemeConstantOverride("separation", 18);
        _scroll.AddChild(_bodyColumns);
        _bodyColumns.AddChild(_body);
        AddChild(_card);
        // Every visual child must ignore GUI input too; setting only the outer card to
        // Ignore still lets nested containers and scrollbars block the hovered item.
        set_mouse_filter_recursive(_card, Control.MouseFilterEnum.Ignore);
        if (_scroll.GetVScrollBar() is { } scrollbar)
            scrollbar.MouseFilter = Control.MouseFilterEnum.Ignore;
        _rulesButton = new Button { Text = "规则详解  [F1]", MouseFilter = Control.MouseFilterEnum.Ignore,
            FocusMode = Control.FocusModeEnum.None, SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd, Flat = true };
        _rulesButton.AddThemeFontOverride("font", GD.Load<FontFile>("res://resources/font/LXGWZhenKaiSlabGB-Regular.ttf"));
        _rulesButton.AddThemeFontSizeOverride("font_size", 16);
        _rulesButton.AddThemeColorOverride("font_color", new Color("d7c8a0"));
        column.AddChild(_rulesButton);
        _footprints = new HoverFootprintFooter();
        column.AddChild(_footprints);
        set_mouse_filter_recursive(_footprints, Control.MouseFilterEnum.Ignore);
        build_rules_reader();
    }

    private void build_rules_reader()
    {
        _rulesOverlay = new Control { Name = "RulesReader", Visible = false, MouseFilter = Control.MouseFilterEnum.Stop };
        AddChild(_rulesOverlay);
        _rulesOverlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var shade = new ColorRect { Color = new Color(0, 0, 0, .65f) };
        _rulesOverlay.AddChild(shade); shade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var panel = new PanelContainer(); _rulesOverlay.AddChild(panel);
        panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        panel.AnchorLeft = .12f; panel.AnchorRight = .88f; panel.AnchorTop = .1f; panel.AnchorBottom = .9f;
        var style = new StyleBoxFlat { BgColor = new Color("20241c"), ContentMarginLeft = 24, ContentMarginRight = 24,
            ContentMarginTop = 20, ContentMarginBottom = 20 };
        panel.AddThemeStyleboxOverride("panel", style);
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 16); panel.AddChild(column);
        var close = PaperButton.Create("关闭规则详解", 20, false);
        close.CustomMinimumSize = new Vector2(180, 42);
        close.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;
        close.Pressed += () => _rulesOverlay.Hide(); column.AddChild(close);
        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        column.AddChild(scroll);
        _rulesText = new RichTextLabel { BbcodeEnabled = true, FitContent = true, ScrollActive = false,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _rulesText.AddThemeFontOverride("normal_font", GD.Load<FontFile>("res://resources/font/LXGWZhenKaiSlabGB-Regular.ttf"));
        _rulesText.AddThemeFontSizeOverride("normal_font_size", 20);
        _rulesText.AddThemeColorOverride("default_color", new Color("ded8c6"));
        scroll.AddChild(_rulesText);
    }

    private void show_rules()
    {
        if (_visibleContent is not { } content || !content.ShowRules || _rulesOverlay is null || _rulesText is null) return;
        _rulesOwner = _activeAnchor;
        _rulesText.Text = (string.IsNullOrWhiteSpace(content.RulesTitle) ? content.Title : content.RulesTitle)
            + "\n\n" + (string.IsNullOrWhiteSpace(content.RulesText) ? content.Body : content.RulesText);
        _rulesOverlay.Show(); _rulesOverlay.MoveToFront(); hide_all();
    }

    private static Label make_label(int size, Color color)
    {
        var label = new Label { MouseFilter = Control.MouseFilterEnum.Ignore, VerticalAlignment = VerticalAlignment.Center };
        label.AddThemeFontOverride("font", GD.Load<FontFile>("res://resources/font/LXGWZhenKaiSlabGB-Regular.ttf"));
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private void on_node_added(Node node)
    {
        if (node is Control control && _card is { } card && (control == card || card.IsAncestorOf(control)))
        { set_mouse_filter_recursive(control, Control.MouseFilterEnum.Ignore); return; }
        CallDeferred(MethodName.bind_added_node_deferred, node);
    }

    private void bind_added_node_deferred(Node node)
    {
        if (GodotObject.IsInstanceValid(node) && node is Control control) bind_control(control);
    }

    private void scan_subtree(Node node)
    {
        if (node is Control control) bind_control(control);
        foreach (var child in node.GetChildren()) scan_subtree(child);
    }

    private void bind_control(Control anchor)
    {
        if (!GodotObject.IsInstanceValid(anchor) || anchor.HasMeta(BoundMeta)) return;
        // Reader contents are output, never new hover sources. In particular a
        // dynamically created formula column must not bind a hover to itself.
        if (_card is { } card && (anchor == card || card.IsAncestorOf(anchor))
            || _rulesOverlay is { } rules && (anchor == rules || rules.IsAncestorOf(anchor))) return;
        anchor.SetMeta(BoundMeta, true);
        // Cover legacy object detail controls as they enter the tree, including pages
        // that still construct labels directly instead of using the shared factories.
        if (!anchor.HasMeta("player_information_source_bound") && anchor is Label label && PlayerInfoText.HasDetails(label.Text))
        {
            PlayerInfoText.Bind(label, label.Text);
        }
        else if (!anchor.HasMeta("player_information_source_bound") && anchor is RichTextLabel rich && PlayerInfoText.HasDetails(rich.Text))
        {
            PlayerInfoText.Bind(rich, rich.Text);
        }
        anchor.MouseEntered += () => on_anchor_entered(anchor);
        anchor.MouseExited += () => on_anchor_exited(anchor);
        anchor.ItemRectChanged += () => on_anchor_rect_changed(anchor);
        anchor.TreeExiting += () => on_anchor_tree_exiting(anchor);
        if (!string.IsNullOrWhiteSpace(anchor.TooltipText)) set_tooltip(anchor, anchor.TooltipText);
    }

    private void set_tooltip(Control? anchor, string text)
    {
        if (anchor is null || !GodotObject.IsInstanceValid(anchor)) return;
        _providers.Remove(anchor);
        bind_control(anchor);
        anchor.TooltipText = "";
        if (string.IsNullOrWhiteSpace(text))
        {
            _entries.Remove(anchor);
            if (_pendingAnchor == anchor) { _pendingAnchor = null; _showTimer?.Stop(); }
            if (_activeAnchor == anchor) hide_all();
            return;
        }
        var content = new HoverInfoContent("", text);
        if (_entries.TryGetValue(anchor, out var existing) && existing == content) return;
        set_info(anchor, content);
    }

    private void set_info(Control? anchor, HoverInfoContent content, bool immediate = false)
    {
        if (anchor is null || !GodotObject.IsInstanceValid(anchor)) return;
        bind_control(anchor);
        anchor.TooltipText = "";
        if (string.IsNullOrWhiteSpace(content.Title) && string.IsNullOrWhiteSpace(content.Body) && string.IsNullOrWhiteSpace(content.Subtitle))
        {
            set_tooltip(anchor, "");
            return;
        }
        _entries[anchor] = content;
        if (_suspended.Contains(anchor) || drag_in_progress()) return;
        if (_activeAnchor == anchor) display(anchor, content);
        else if (pointer_over(anchor))
        {
            _hideTimer?.Stop();
            if (immediate || _card?.Visible == true)
            {
                _pendingAnchor = null;
                _showTimer?.Stop();
                display(anchor, content);
            }
            else
            {
                if (_pendingAnchor == anchor && _showTimer is { } showTimer && !showTimer.IsStopped()) return;
                _pendingAnchor = anchor;
                _showTimer?.Start(ShowDelay);
            }
        }
    }

    private void on_anchor_entered(Control anchor)
    {
        if (!GodotObject.IsInstanceValid(anchor) || !anchor.IsVisibleInTree() || _suspended.Contains(anchor) || drag_in_progress()) return;
        if (_providers.TryGetValue(anchor, out var provider)) _entries[anchor] = provider();
        // Capture later state updates that still use Godot's TooltipText property.
        if (!string.IsNullOrWhiteSpace(anchor.TooltipText)) set_tooltip(anchor, anchor.TooltipText);
        if (!_entries.TryGetValue(anchor, out var content)) return;
        _hideTimer?.Stop();
        if (_card?.Visible == true)
        {
            _pendingAnchor = null;
            _showTimer?.Stop();
            display(anchor, content);
            return;
        }
        _pendingAnchor = anchor;
        _showTimer?.Start(ShowDelay);
    }

    private void on_anchor_exited(Control anchor)
    {
        if (_pendingAnchor == anchor && _activeAnchor != anchor)
        {
            _pendingAnchor = null;
            _showTimer?.Stop();
        }
        if (_activeAnchor == anchor) _hideTimer?.Start(HideGrace);
    }

    private void on_anchor_tree_exiting(Control anchor)
    {
        _suspended.Remove(anchor);
        _entries.Remove(anchor);
        _providers.Remove(anchor);
        if (_pendingAnchor == anchor) { _pendingAnchor = null; _showTimer?.Stop(); }
        if (_activeAnchor == anchor) hide_all();
    }

    private void on_anchor_rect_changed(Control anchor)
    {
        if (_activeAnchor == anchor && _card?.Visible == true) place_card(anchor);
    }

    private void show_pending()
    {
        var anchor = _pendingAnchor;
        _pendingAnchor = null;
        if (anchor is null || !GodotObject.IsInstanceValid(anchor) || !anchor.IsVisibleInTree() || drag_in_progress()) return;
        if (!pointer_over(anchor)) return;
        if (_providers.TryGetValue(anchor, out var provider)) _entries[anchor] = provider();
        if (_entries.TryGetValue(anchor, out var content)) display(anchor, content);
    }

    private void display(Control anchor, HoverInfoContent content)
    {
        if (_suspended.Contains(anchor) || drag_in_progress()) return;
        if (_card is null || _title is null || _subtitle is null || _icon is null || _scroll is null || _body is null) return;
        if (_activeAnchor == anchor && _visibleContent == content) return;
        var liveRefresh = content.LiveUpdate && _activeAnchor == anchor && _visibleContent is { LiveUpdate: true };
        if (_activeAnchor != anchor)
        {
            _hideTimer?.Stop();
        }
        _activeAnchor = anchor;
        _visibleContent = content;
        _title.Text = content.Title;
        _title.Visible = !string.IsNullOrWhiteSpace(content.Title);
        _icon.Texture = content.Icon;
        _icon.CustomMinimumSize = new Vector2(content.IconSize, content.IconSize);
        _icon.Visible = content.Icon is not null;
        _headerFootprint?.SetFootprint(null);
        if (content.InventoryFootprintBelowTitle && _headerFootprint is not null)
        {
            var inventory = System.Linq.Enumerable.FirstOrDefault(content.GridDiagrams ?? System.Array.Empty<HoverGridDiagram>(),
                diagram => diagram.Kind == HoverGridKind.Inventory);
            if (inventory is not null) _headerFootprint.SetDiagram(inventory);
            else _headerFootprint.SetFootprint(content.Footprint);
            _headerFootprint.ConfigureScale(24f, 120f);
        }
        _footprints?.SetDiagrams(content.Footprint, content.GridDiagrams,
            content.InventoryFootprintBelowTitle || content.CompactGridFooter);
        _subtitle.Text = content.Subtitle;
        _subtitle.Visible = !string.IsNullOrWhiteSpace(content.Subtitle);
        _bodySource = content.Sections is { Count: > 0 } sections
            ? string.Join("\n\n", System.Linq.Enumerable.Select(sections, section =>
                string.IsNullOrWhiteSpace(PlayerInfoText.Summary(section.Body)) ? "" :
                string.IsNullOrWhiteSpace(section.Title) ? PlayerInfoText.Summary(section.Body) : $"[color=#d7c8a0][b]{section.Title}[/b][/color]\n{PlayerInfoText.Summary(section.Body)}"))
            : PlayerInfoText.Display(content.Body);
        if (content.Sections is { Count: > 0 } && PlayerInfoText.Mode == PlayerInfoText.InformationMode.Detailed
            && PlayerInfoText.CalculationSources(content.Body) is { Count: > 0 } calculations)
            _bodySource += "\n\n[color=#d7c8a0][b]计算来源[/b][/color]\n" + string.Join("\n\n", calculations);
        _bodySource = PlayerInfoText.LayoutLongParagraphs(_bodySource);
        _body.Text = _bodySource;
        if (_rulesButton is not null)
        {
            _rulesButton.Visible = content.ShowRules && (!string.IsNullOrWhiteSpace(content.RulesText) || PlayerInfoText.HasDetails(content.Body));
            _rulesButton.Text = string.IsNullOrWhiteSpace(content.RulesTitle) ? "规则详解  [F1]" : content.RulesTitle + "  [F1]";
        }
        if (liveRefresh)
        {
            if (_secondaryBody is not null) configure_body_columns(_bodyWidth, true);
            return;
        }
        _scroll.VerticalScrollMode = ScrollContainer.ScrollMode.Disabled;
        _scroll.ScrollVertical = 0;
        _card.Modulate = new Color(1, 1, 1, 0);
        _card.Show();
        _card.MoveToFront();
        schedule_layout();
    }

    private void on_information_mode_changed()
    {
        if (_activeAnchor is not { } anchor || _visibleContent is not { } content || _card?.Visible != true) return;
        if (_providers.TryGetValue(anchor, out var provider)) content = provider();
        _visibleContent = null;
        display(anchor, content);
    }

    private void schedule_layout()
    {
        var ticket = ++_layoutTicket;
        CallDeferred(MethodName.layout_card, ticket);
    }

    private bool configure_body_columns(float availableWidth, bool allowColumns)
    {
        if (_body is null || _bodyColumns is null) return false;
        _bodyWidth = availableWidth;
        var paragraphs = _bodySource.Split("\n\n", StringSplitOptions.None);
        var canSplit = allowColumns && _visibleContent?.SingleColumn != true && availableWidth >= 576f && paragraphs.Length > 1
            && paragraphs.All(paragraph => balanced_body_markup(paragraph));
        if (!canSplit)
        {
            if (_secondaryBody is not null)
            {
                _bodyColumns.RemoveChild(_secondaryBody);
                _secondaryBody.QueueFree();
                _secondaryBody = null;
            }
            _body.Text = _bodySource;
            _body.CustomMinimumSize = new Vector2(availableWidth, 0);
            _body.Size = new Vector2(availableWidth, 0);
            return false;
        }
        if (_secondaryBody is null)
        {
            _secondaryBody = new RichTextLabel { Name = "HoverBodyContinuation", BbcodeEnabled = true,
                FitContent = true, ScrollActive = false, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                AutowrapMode = TextServer.AutowrapMode.WordSmart, MouseFilter = Control.MouseFilterEnum.Ignore };
            _secondaryBody.AddThemeFontOverride("normal_font", _body.GetThemeFont("normal_font"));
            _secondaryBody.AddThemeFontOverride("bold_font", _body.GetThemeFont("bold_font"));
            _secondaryBody.AddThemeColorOverride("default_color", _body.GetThemeColor("default_color"));
            _bodyColumns.AddChild(_secondaryBody);
        }
        var fontSize = _body.GetThemeFontSize("normal_font_size");
        var spacing = _body.GetThemeConstant("line_separation");
        _secondaryBody.AddThemeFontSizeOverride("normal_font_size", fontSize);
        _secondaryBody.AddThemeConstantOverride("line_separation", spacing);
        var columnWidth = (availableWidth - 18f) / 2f;
        var font = _body.GetThemeFont("normal_font");
        float Estimate(string text)
        {
            var plain = System.Text.RegularExpressions.Regex.Replace(text, @"\[/?[a-zA-Z][^\]]*\]", "");
            var height = font.GetMultilineStringSize(plain, HorizontalAlignment.Left, columnWidth, fontSize, -1,
                TextServer.LineBreakFlag.Mandatory | TextServer.LineBreakFlag.WordBound | TextServer.LineBreakFlag.Adaptive).Y;
            return height + Mathf.Max(0, Mathf.Ceil(height / font.GetHeight(fontSize)) - 1) * spacing;
        }
        var split = 1;
        var bestHeight = float.MaxValue;
        for (var index = 1; index < paragraphs.Length; index++)
        {
            var score = Mathf.Max(Estimate(string.Join("\n\n", paragraphs.Take(index))),
                Estimate(string.Join("\n\n", paragraphs.Skip(index))));
            if (score < bestHeight) { bestHeight = score; split = index; }
        }
        _body.Text = string.Join("\n\n", paragraphs.Take(split));
        _secondaryBody.Text = string.Join("\n\n", paragraphs.Skip(split));
        foreach (var label in new[] { _body, _secondaryBody })
        {
            label.CustomMinimumSize = new Vector2(columnWidth, 0);
            label.Size = new Vector2(columnWidth, 0);
        }
        return true;
    }

    private static bool balanced_body_markup(string text)
    {
        var tags = new Stack<string>();
        foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(text,
            @"\[(?<close>/)?(?<tag>b|i|u|s|color|font|font_size|bgcolor|fgcolor|url)(?:=[^\]]*)?\]"))
        {
            var tag = match.Groups["tag"].Value;
            if (!match.Groups["close"].Success) tags.Push(tag);
            else if (tags.Count == 0 || tags.Pop() != tag) return false;
        }
        return tags.Count == 0;
    }

    private float body_content_height() => Mathf.Max(40f,
        Mathf.Max(_body?.GetContentHeight() ?? 0, _secondaryBody?.GetContentHeight() ?? 0) + 8f);

    private async void layout_card(int ticket)
    {
        if (ticket != _layoutTicket || _activeAnchor is null || !GodotObject.IsInstanceValid(_activeAnchor) || _card is null || _body is null || _scroll is null) return;
        var viewport = GetViewport().GetVisibleRect().Size;
        var width = Mathf.Max(100f, Mathf.Min(_visibleContent?.CardWidth ?? PreferredWidth, viewport.X - ViewportMargin * 2));
        var bodyWidth = Mathf.Max(48f, width - 44f);
        _body.AddThemeFontSizeOverride("normal_font_size",18);
        _body.AddThemeConstantOverride("line_separation", 5);
        configure_body_columns(bodyWidth, false);
        _footprints?.ConfigureWidth(width - 36f);
        _scroll.CustomMinimumSize = new Vector2(0, 40);
        var maxHeight = Mathf.Max(80f, viewport.Y - ViewportMargin * 2);
        // Give the RichTextLabel and parent containers a layout pass at the final width.
        // First-hover text otherwise measures at the old/zero width and pins a huge minimum.
        _card.CustomMinimumSize = new Vector2(width, 0);
        _card.Size = new Vector2(width, 0);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (ticket != _layoutTicket || _activeAnchor is null || !GodotObject.IsInstanceValid(_activeAnchor) || !_card.Visible) return;
        // Reserve measured header, margins and all footer rows. Footprints belong to
        // the card's height budget, rather than being clipped below a full-height body.
        var overhead = _card.GetCombinedMinimumSize().Y - _scroll.GetCombinedMinimumSize().Y;
        var maxBodyHeight = Mathf.Max(40f, maxHeight - overhead);
        var naturalBodyHeight = body_content_height();
        var maxWidth = Mathf.Max(100f, viewport.X - ViewportMargin * 2);
        var singleColumn = _visibleContent?.SingleColumn == true;
        var preferredHeight = singleColumn ? Mathf.Min(maxHeight, viewport.Y * .82f) : maxHeight;
        var preferredBodyHeight = Mathf.Max(40f, preferredHeight - overhead);
        var preferredMaxWidth = Mathf.Min(maxWidth, Mathf.Max(width, 760f));
        var isItem = _visibleContent is { } spatial && (spatial.Footprint.HasValue || spatial.GridDiagrams is { Count: > 0 });
        while (width < maxWidth && (naturalBodyHeight > maxBodyHeight
            || singleColumn && naturalBodyHeight > preferredBodyHeight && width < preferredMaxWidth))
        {
            var widthLimit = naturalBodyHeight > maxBodyHeight ? maxWidth : preferredMaxWidth;
            width = Mathf.Min(widthLimit, width + 120f);
            bodyWidth = Mathf.Max(48f, width - 44f);
            var hadColumns = _secondaryBody is not null;
            configure_body_columns(bodyWidth, isItem);
            _footprints?.ConfigureWidth(width - 36f);
            if (!hadColumns && _secondaryBody is not null)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                if (ticket != _layoutTicket || _activeAnchor is null || !GodotObject.IsInstanceValid(_activeAnchor) || !_card.Visible) return;
            }
            overhead = _card.GetCombinedMinimumSize().Y - _scroll.GetCombinedMinimumSize().Y;
            maxBodyHeight = Mathf.Max(40f, maxHeight - overhead);
            preferredBodyHeight = Mathf.Max(40f, preferredHeight - overhead);
            naturalBodyHeight = body_content_height();
            if (singleColumn)
                for (var spacing = _body.GetThemeConstant("line_separation") - 1; naturalBodyHeight > maxBodyHeight && spacing >= 0; spacing--)
                {
                    _body.AddThemeConstantOverride("line_separation", spacing);
                    configure_body_columns(bodyWidth, false);
                    naturalBodyHeight = body_content_height();
                }
        }
        for (var fontSize = 17; naturalBodyHeight > maxBodyHeight && fontSize >= 12; fontSize--)
        {
            _body.AddThemeFontSizeOverride("normal_font_size", fontSize);
            configure_body_columns(bodyWidth, _secondaryBody is not null);
            naturalBodyHeight = body_content_height();
        }
        // Explicit field breaks cannot be shortened by further widening. On small
        // viewports, reclaim inter-line spacing before the text or footer can clip.
        for (var spacing = 4; !singleColumn && naturalBodyHeight > maxBodyHeight && spacing >= 0; spacing--)
        {
            _body.AddThemeConstantOverride("line_separation", spacing);
            configure_body_columns(bodyWidth, _secondaryBody is not null);
            naturalBodyHeight = body_content_height();
        }
        _scroll.CustomMinimumSize = new Vector2(0, naturalBodyHeight);
        _card.CustomMinimumSize = new Vector2(width, 0);
        _card.ResetSize();
        _card.Size = _card.GetCombinedMinimumSize();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (ticket != _layoutTicket || _activeAnchor is null || !GodotObject.IsInstanceValid(_activeAnchor) || !_card.Visible) return;
        _card.ResetSize();
        _card.Size = _card.GetCombinedMinimumSize();
        place_card(_activeAnchor);
        _card.Modulate = Colors.White;
    }

    private void place_card(Control anchor)
    {
        if (_card is null || !_card.Visible || !GodotObject.IsInstanceValid(anchor)) return;
        var viewport = GetViewport().GetVisibleRect().Size;
        var target = _card.GetCanvasTransform().AffineInverse() * viewport_rect(anchor);
        var size = _card.Size;
        var right = target.End.X + 14f;
        var left = target.Position.X - size.X - 14f;
        var x = right + size.X <= viewport.X - ViewportMargin ? right : left;
        if (x < ViewportMargin || x + size.X > viewport.X - ViewportMargin)
        {
            x = target.Position.X;
            if (target.End.Y + size.Y <= viewport.Y - ViewportMargin) x = Mathf.Clamp(x, ViewportMargin, Mathf.Max(ViewportMargin, viewport.X - size.X - ViewportMargin));
        }
        var y = target.Position.Y;
        if (y + size.Y > viewport.Y - ViewportMargin) y = target.End.Y - size.Y;
        if (y < ViewportMargin && target.End.Y + size.Y <= viewport.Y - ViewportMargin) y = target.End.Y + 10f;
        _card.Position = new Vector2(
            Mathf.Clamp(x, ViewportMargin, Mathf.Max(ViewportMargin, viewport.X - size.X - ViewportMargin)),
            Mathf.Clamp(y, ViewportMargin, Mathf.Max(ViewportMargin, viewport.Y - size.Y - ViewportMargin)));
    }

    private void hide_if_pointer_left()
    {
        if (!drag_in_progress() && pointer_over(_activeAnchor)) return;
        hide_all();
    }

    private void hide_for(Control? anchor)
    {
        if (anchor is null || !GodotObject.IsInstanceValid(anchor)) return;
        if (_pendingAnchor == anchor) { _pendingAnchor = null; _showTimer?.Stop(); }
        if (_activeAnchor == anchor) _hideTimer?.Start(HideGrace);
    }

    private void hide_all()
    {
        _showTimer?.Stop();
        _hideTimer?.Stop();
        _pendingAnchor = null;
        _activeAnchor = null;
        _visibleContent = null;
        _card?.Hide();
    }

    private bool pointer_over(Control? control, Vector2? viewportPosition = null)
    {
        if (control is null || !GodotObject.IsInstanceValid(control) || !control.IsVisibleInTree()) return false;
        var point = viewportPosition ?? _pointerPosition;
        for (Node? node = control; node is not null && node is not CanvasLayer && node is not Viewport; node = node.GetParent())
        {
            if (node is not Control region || (region != control && !region.ClipContents)) continue;
            var transform = region.GetGlobalTransformWithCanvas();
            if (Mathf.IsZeroApprox(transform.X.Cross(transform.Y)) ||
                !new Rect2(Vector2.Zero, region.Size).HasPoint(transform.AffineInverse() * point)) return false;
        }
        return true;
    }

    private static Rect2 viewport_rect(Control control)
    {
        var transform = control.GetGlobalTransformWithCanvas();
        var bounds = new Rect2(transform * Vector2.Zero, Vector2.Zero);
        bounds = bounds.Expand(transform * new Vector2(control.Size.X, 0));
        bounds = bounds.Expand(transform * new Vector2(0, control.Size.Y));
        return bounds.Expand(transform * control.Size);
    }

    private static void set_mouse_filter_recursive(Control control, Control.MouseFilterEnum filter)
    {
        control.MouseFilter = filter;
        if (filter == Control.MouseFilterEnum.Ignore) control.FocusMode = Control.FocusModeEnum.None;
        foreach (var child in control.GetChildren(true))
            if (child is Control childControl)
                set_mouse_filter_recursive(childControl, filter);
    }

    private void on_viewport_resized()
    {
        if (_activeAnchor is not null && GodotObject.IsInstanceValid(_activeAnchor)) schedule_layout();
    }
}
