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
    private const int HoverScrollStep = 72;
    private const string BoundMeta = "shared_hover_info_bound";
    private static HoverInfoManager? _instance;

    private readonly Dictionary<Control, HoverInfoContent> _entries = new();
    private PanelContainer? _card;
    private TextureRect? _icon;
    private Label? _title;
    private Label? _subtitle;
    private ScrollContainer? _scroll;
    private RichTextLabel? _body;
    private Godot.Timer? _showTimer;
    private Godot.Timer? _hideTimer;
    private Control? _pendingAnchor;
    private Control? _activeAnchor;
    private HoverInfoContent? _visibleContent;
    private bool _pointerOverCard;
    private Vector2 _pointerPosition;
    private int _layoutTicket;

    public readonly record struct HoverInfoContent(string Title, string Body, Texture2D? Icon = null, string Subtitle = "");

    public override void _EnterTree()
    {
        _instance = this;
        Layer = 160;
        ProcessMode = ProcessModeEnum.Always;
    }

    public override void _Ready()
    {
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
        if (GetTree() is { } tree) tree.NodeAdded -= on_node_added;
        if (GetViewport() is { } viewport) viewport.SizeChanged -= on_viewport_resized;
        if (_instance == this) _instance = null;
    }

    public override void _Process(double delta)
    {
        // Some existing controls refresh TooltipText as their state changes. Capture only
        // the currently displayed control, rather than polling or laying out the whole UI.
        if (_activeAnchor is null || !GodotObject.IsInstanceValid(_activeAnchor)) return;
        if (!string.IsNullOrWhiteSpace(_activeAnchor.TooltipText))
        {
            var changed = _activeAnchor.TooltipText;
            set_tooltip(_activeAnchor, changed);
        }
        if (!_activeAnchor.IsVisibleInTree()) hide_all();

        // The card is mouse-transparent so controls beneath it can receive hover. Track
        // its rectangle instead of relying on MouseEntered/Exited from the overlay.
        var overCard = pointer_over(_card);
        var overAnchor = pointer_over(_activeAnchor);
        if (overCard || overAnchor) _hideTimer?.Stop();
        else if (_pointerOverCard) _hideTimer?.Start(HideGrace);
        _pointerOverCard = overCard;
    }

    public override void _Input(InputEvent @event)
    {
        // GUI signals run after _Input. Use this event's viewport position immediately,
        // rather than waiting for the platform's polled cursor position to catch up.
        if (@event is InputEventMouse pointer) _pointerPosition = pointer.Position;
        if (@event is not InputEventMouseButton mouse || !mouse.Pressed) return;
        if (mouse.ButtonIndex == MouseButton.Middle) { hide_all(); return; }
        if (_card?.Visible != true || _scroll is null || !pointer_over(_card, mouse.Position)) return;

        var scrollDelta = mouse.ButtonIndex switch
        {
            MouseButton.WheelUp => -HoverScrollStep,
            MouseButton.WheelDown => HoverScrollStep,
            _ => 0,
        };
        if (scrollDelta == 0) return;

        var scrollbar = _scroll.GetVScrollBar();
        var maxScroll = Math.Max(scrollbar.MinValue, scrollbar.MaxValue - scrollbar.Page);
        // A short, transparent hint must not swallow the underlying list's wheel.
        if (maxScroll <= scrollbar.MinValue) return;
        scrollbar.Value = Math.Clamp(scrollbar.Value + scrollDelta * (mouse.Factor > 0 ? mouse.Factor : 1), scrollbar.MinValue, maxScroll);
        GetViewport().SetInputAsHandled();
    }

    public static void SetTooltip(Control? anchor, string? text)
    {
        if (_instance is { } manager && GodotObject.IsInstanceValid(manager))
            manager.set_tooltip(anchor, text ?? "");
    }

    public static void SetInfo(Control? anchor, HoverInfoContent content, bool immediate = false)
    {
        if (_instance is { } manager && GodotObject.IsInstanceValid(manager))
            manager.set_info(anchor, content, immediate);
    }

    public static void CopyInfo(Control? source, Control? destination)
    {
        if (source is null || destination is null || !GodotObject.IsInstanceValid(source) || !GodotObject.IsInstanceValid(destination)) return;
        if (_instance is { } manager && GodotObject.IsInstanceValid(manager) && manager._entries.TryGetValue(source, out var content))
        {
            manager.set_info(destination, content);
            return;
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
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        header.AddChild(_icon);
        _title = make_label(24, new Color("f0ead7"));
        _title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _title.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        header.AddChild(_title);
        _subtitle = make_label(17, new Color("d7c8a0"));
        _subtitle.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        column.AddChild(_subtitle);
        var separator = new ColorRect { Color = new Color(.61f, .58f, .47f, .38f), CustomMinimumSize = new Vector2(0, 1), MouseFilter = Control.MouseFilterEnum.Ignore };
        column.AddChild(separator);

        _scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
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
        _scroll.AddChild(_body);
        AddChild(_card);
        // Every visual child must ignore GUI input too; setting only the outer card to
        // Ignore still lets nested containers and scrollbars block the hovered item.
        set_mouse_filter_recursive(_card, Control.MouseFilterEnum.Ignore);
        if (_scroll.GetVScrollBar() is { } scrollbar)
            scrollbar.MouseFilter = Control.MouseFilterEnum.Ignore;
    }

    private static Label make_label(int size, Color color)
    {
        var label = new Label { MouseFilter = Control.MouseFilterEnum.Ignore, VerticalAlignment = VerticalAlignment.Center };
        label.AddThemeFontOverride("font", GD.Load<FontFile>("res://resources/font/LXGWZhenKaiSlabGB-Regular.ttf"));
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private void on_node_added(Node node) => CallDeferred(MethodName.bind_added_node_deferred, node);

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
        anchor.SetMeta(BoundMeta, true);
        anchor.MouseEntered += () => on_anchor_entered(anchor);
        anchor.MouseExited += () => on_anchor_exited(anchor);
        anchor.ItemRectChanged += () => on_anchor_rect_changed(anchor);
        anchor.TreeExiting += () => on_anchor_tree_exiting(anchor);
        if (!string.IsNullOrWhiteSpace(anchor.TooltipText)) set_tooltip(anchor, anchor.TooltipText);
    }

    private void set_tooltip(Control? anchor, string text)
    {
        if (anchor is null || !GodotObject.IsInstanceValid(anchor)) return;
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
        if (!GodotObject.IsInstanceValid(anchor) || !anchor.IsVisibleInTree()) return;
        // Capture later state updates that still use Godot's TooltipText property.
        if (!string.IsNullOrWhiteSpace(anchor.TooltipText)) set_tooltip(anchor, anchor.TooltipText);
        if (!_entries.TryGetValue(anchor, out var content)) return;
        _hideTimer?.Stop();
        _pointerOverCard = false;
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
        if (_activeAnchor == anchor && !pointer_over(_card)) _hideTimer?.Start(HideGrace);
    }

    private void on_anchor_tree_exiting(Control anchor)
    {
        _entries.Remove(anchor);
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
        if (anchor is null || !GodotObject.IsInstanceValid(anchor) || !anchor.IsVisibleInTree()) return;
        if (!pointer_over(anchor)) return;
        if (_entries.TryGetValue(anchor, out var content)) display(anchor, content);
    }

    private void display(Control anchor, HoverInfoContent content)
    {
        if (_card is null || _title is null || _subtitle is null || _icon is null || _scroll is null || _body is null) return;
        if (_activeAnchor == anchor && _visibleContent == content) return;
        if (_activeAnchor != anchor)
        {
            _hideTimer?.Stop();
            _pointerOverCard = false;
        }
        _activeAnchor = anchor;
        _visibleContent = content;
        _title.Text = content.Title;
        _title.Visible = !string.IsNullOrWhiteSpace(content.Title);
        _icon.Texture = content.Icon;
        _icon.Visible = content.Icon is not null;
        _subtitle.Text = content.Subtitle;
        _subtitle.Visible = !string.IsNullOrWhiteSpace(content.Subtitle);
        _body.Text = content.Body;
        _scroll.ScrollVertical = 0;
        _card.Show();
        _card.MoveToFront();
        schedule_layout();
    }

    private void schedule_layout()
    {
        var ticket = ++_layoutTicket;
        CallDeferred(MethodName.layout_card, ticket);
    }

    private void layout_card(int ticket)
    {
        if (ticket != _layoutTicket || _activeAnchor is null || !GodotObject.IsInstanceValid(_activeAnchor) || _card is null || _body is null || _scroll is null) return;
        var viewport = GetViewport().GetVisibleRect().Size;
        var width = Mathf.Max(100f, Mathf.Min(PreferredWidth, viewport.X - ViewportMargin * 2));
        var bodyWidth = Mathf.Max(48f, width - 44f);
        _body.CustomMinimumSize = new Vector2(bodyWidth, 0);
        _scroll.CustomMinimumSize = new Vector2(0, 40);
        var maxHeight = Mathf.Max(80f, viewport.Y - ViewportMargin * 2);
        var maxBodyHeight = Mathf.Min(280f, Mathf.Max(40f, maxHeight - 126f));
        var naturalBodyHeight = Mathf.Max(40f, _body.GetContentHeight() + 8f);
        _scroll.CustomMinimumSize = new Vector2(0, Mathf.Min(naturalBodyHeight, maxBodyHeight));
        _card.CustomMinimumSize = new Vector2(width, 0);
        _card.Size = _card.GetCombinedMinimumSize();
        place_card(_activeAnchor);
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
        if (pointer_over(_card) || pointer_over(_activeAnchor)) return;
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
        _pointerOverCard = false;
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
        foreach (var child in control.GetChildren())
            if (child is Control childControl)
                set_mouse_filter_recursive(childControl, filter);
    }

    private void on_viewport_resized()
    {
        if (_activeAnchor is not null && GodotObject.IsInstanceValid(_activeAnchor)) schedule_layout();
    }
}
