using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>Scene-local skin candidate around the real unbound forging UI; never a game entry.</summary>
public partial class ForgingPaperPreviewScene : Control
{
    private const string SourceScene = "res://tscn/scene/ProcessForgingScene.tscn";
    private const string SkinMarker = "forging_paper_preview_skinned";
    private const string ReturnMarker = "forging_paper_preview_return";
    private const string NavigationMarker = "forging_paper_preview_navigation";
    private const string MaterialMarker = "forging_paper_preview_material";
    private static readonly Color PanelTone = new(CraftingPaperHeader.PaperBrightness, CraftingPaperHeader.PaperBrightness, CraftingPaperHeader.PaperBrightness);
    private static readonly Regex NativeHours = new(@"(?<amount>\d+(?:\.\d+)?)\s*时辰", RegexOptions.CultureInvariant);
    private static readonly string[] ButtonStates = ["normal", "hover", "pressed", "hover_pressed", "disabled"];
    private static readonly string[] ButtonColors = ["font_color", "font_hover_color", "font_pressed_color", "font_hover_pressed_color", "font_focus_color", "font_disabled_color"];

    private ProcessForgingScene _forging = null!;
    private ForgingNativeFirePresentation? _fire;
    private SceneTree? _observedTree;
    private Theme _paperTheme = null!;
    private Control _closedCanvas = null!;
    private bool _skinQueued, _applyingSkin, _exiting;

    public int SkinPassCount { get; private set; }
    public int StyledControlCount { get; private set; }
    public ForgingNativeFirePresentation? NativeFirePreview => _fire;
    public string PreviewLedger => IsRoomValid ? _forging.NativeFireLedger : "";
    public bool OriginalInputsVisible => IsRoomValid && _forging.NativeFireInputsVisible;
    private bool IsRoomValid => _forging is not null && IsInstanceValid(_forging) && !_forging.IsQueuedForDeletion();

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        BuildClosedView();
        _forging = GD.Load<PackedScene>(SourceScene).Instantiate<ProcessForgingScene>();
        _forging.Name = "NativeForgingRoom";
        _observedTree = GetTree();
        // NodeAdded removes the old menu callback before a newly rebuilt button
        // can receive input. Appearance is deferred and coalesced separately.
        _observedTree.NodeAdded += OnPreviewNodeAdded;
        _observedTree.TreeChanged += QueuePreviewSkin;
        AddChild(_forging);
        _paperTheme = CreateLocalTheme(_forging.Theme);
        _forging.Theme = _paperTheme;
        try
        {
            // This public preview API refuses a character owner, loads the real
            // composite example, and does not submit or debit the session.
            _fire = _forging.EnableNativeFirePreview();
        }
        catch (Exception error)
        {
            GD.PushWarning("Forging paper preview kept its native workshop: " + error.Message);
        }
        MoveChild(_closedCanvas, GetChildCount() - 1);
        QueuePreviewSkin();
    }

    public override void _ExitTree()
    {
        _exiting = true;
        if (_observedTree is not null)
        {
            _observedTree.NodeAdded -= OnPreviewNodeAdded;
            _observedTree.TreeChanged -= QueuePreviewSkin;
            _observedTree = null;
        }
    }

    private void OnPreviewNodeAdded(Node node)
    {
        if (!IsRoomValid || !_forging.IsAncestorOf(node)) return;
        if (node is Button button && button.Text.StartsWith("〈 返回", StringComparison.Ordinal)) RewireReturn(button);
        QueuePreviewSkin();
    }

    private void QueuePreviewSkin()
    {
        if (_exiting || _applyingSkin || _skinQueued || !IsInsideTree()) return;
        _skinQueued = true;
        CallDeferred(nameof(ApplyPreviewSkin));
    }

    private void ApplyPreviewSkin()
    {
        _skinQueued = false;
        if (_exiting || !IsRoomValid) return;
        _applyingSkin = true;
        try
        {
            // Reuse the native selectors and quantity controls, including their
            // callbacks, while giving each material icon the full card height.
            foreach (var card in Descendants(_forging).OfType<PanelContainer>().ToArray())
                ArrangeMaterialCard(card);
            var controls = Descendants(_forging).OfType<Control>().Where(control => !control.IsQueuedForDeletion()).ToArray();
            foreach (var button in controls.OfType<Button>())
            {
                if (button.Text.StartsWith("〈 返回", StringComparison.Ordinal)) RewireReturn(button);
                if (button.HasMeta(ReturnMarker)) PlaceReturnAndHelp(button);
            }
            foreach (var control in controls)
            {
                FormatVisibleHours(control);
                if (control.HasMeta(SkinMarker)) continue;
                if (control is InventoryPaperFrame oldFrame)
                {
                    // The new paper already includes its own fine material edge.
                    oldFrame.Hide();
                }
                else if (control is Button button) SkinButton(button);
                else if (control is LineEdit input) SkinInput(input);
                else if (control is Panel || control is PanelContainer) SkinPanel(control);
                else if (control is VScrollBar) SkinThemeStyles(control, "VScrollBar");
                else if (control is ProgressBar) SkinThemeStyles(control, "ProgressBar");
                else if (control is HSeparator) SkinThemeStyles(control, "HSeparator");
                control.SetMeta(SkinMarker, true);
                StyledControlCount++;
            }
            SkinPassCount++;
        }
        finally { _applyingSkin = false; }
    }

    private static IEnumerable<Node> Descendants(Node parent)
    {
        foreach (var child in parent.GetChildren())
        {
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }

    private static void ArrangeMaterialCard(PanelContainer card)
    {
        if (card.HasMeta(MaterialMarker) || card.IsQueuedForDeletion()) return;
        var details = card.GetChildren().OfType<VBoxContainer>().FirstOrDefault();
        var selector = details?.GetChildren().OfType<Button>()
            .FirstOrDefault(button => button.Icon is not null && button.Text.Contains('\n'));
        var quantities = details?.GetChildren().OfType<HBoxContainer>().FirstOrDefault();
        if (details is null || selector is null || quantities is null) return;

        var icon = new TextureButton
        {
            Name = "MaterialLargeIcon",
            TextureNormal = selector.Icon,
            IgnoreTextureSize = true,
            StretchMode = TextureButton.StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = new Vector2(80, 84),
            SizeFlagsVertical = SizeFlags.ExpandFill,
            TooltipText = selector.TooltipText,
            MouseDefaultCursorShape = CursorShape.PointingHand,
            Disabled = selector.Disabled
        };
        // Both the icon and the single-line selector open the original picker.
        icon.Pressed += () => selector.EmitSignal(BaseButton.SignalName.Pressed);
        selector.Icon = null;
        selector.Text = selector.Text.Replace("  ›\n", "  ·  ", StringComparison.Ordinal)
            .Replace('\n', ' ');
        selector.Alignment = HorizontalAlignment.Left;
        selector.CustomMinimumSize = new Vector2(0, 38);
        selector.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        selector.Name = "MaterialSingleLine";
        details.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        details.AddThemeConstantOverride("separation", 8);
        var quantityLabel = quantities.GetChildren().OfType<Label>().FirstOrDefault(label => label.Text == "份数");
        if (quantityLabel is not null) quantityLabel.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;

        card.RemoveChild(details);
        var row = new HBoxContainer { Name = "MaterialCardRow", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 12);
        card.AddChild(row);
        row.AddChild(icon);
        row.AddChild(details);
        card.SetMeta(MaterialMarker, true);
    }

    private static Theme CreateLocalTheme(Theme? nativeTheme)
    {
        // Keep every native font, font size and text color. Only this instance
        // receives the additional PNG-backed popup, progress and scroll styles.
        var local = nativeTheme?.Duplicate() as Theme ?? new Theme();
        var materialTheme = CraftingPaperStyle.MakeTheme();
        foreach (var type in new[] { "PopupMenu", "VScrollBar", "ProgressBar", "HSeparator" })
        foreach (var name in materialTheme.GetStyleboxList(type))
            local.SetStylebox(name, type, materialTheme.GetStylebox(name, type));
        if (local.GetStylebox("panel", "PopupMenu") is StyleBoxTexture popup)
        {
            var copy = (StyleBoxTexture)popup.Duplicate();
            copy.ModulateColor = PanelTone;
            local.SetStylebox("panel", "PopupMenu", copy);
        }
        local.SetStylebox("hover", "PopupMenu", PaperButtonFeedback.Face(
            CraftingPaperStyle.ButtonKind.Regular, CraftingPaperStyle.ButtonState.Hover, selected: true));
        return local;
    }

    private static void SkinPanel(Control panel)
    {
        var native = panel.GetThemeStylebox("panel");
        var paper = CraftingPaperHeader.Paper(padding: 0, bordered: panel is not PanelContainer);
        paper.ModulateColor = panel.HasMeta(MaterialMarker) ? new Color("b8cbbd") : PanelTone;
        CopyMargins(native, paper);
        panel.AddThemeStyleboxOverride("panel", paper);
    }

    private static void SkinButton(Button button)
    {
        var minimum = button.CustomMinimumSize;
        var size = button.Size;
        var fontSize = button.GetThemeFontSize("font_size");
        var font = button.GetThemeFont("font");
        var nativeStyles = ButtonStates.ToDictionary(state => state, state => button.GetThemeStylebox(state));
        var deepGreen = nativeStyles["normal"] is StyleBoxTexture original && IsJadeTexture(original.Texture);
        var flatText = button.Text.Replace(" ", "", StringComparison.Ordinal);
        bool materialTab = flatText is "身材" or "质材" or "性材" or "和材";
        bool selectedTab = materialTab && deepGreen;
        var kind = flatText is "＋" or "－" ? CraftingPaperStyle.ButtonKind.Icon
            : deepGreen && (flatText == "开炼" || flatText.StartsWith("已炼制", StringComparison.Ordinal)) ? CraftingPaperStyle.ButtonKind.Primary
            : CraftingPaperStyle.ButtonKind.Regular;
        CraftingPaperStyle.ApplyButton(button, kind, fontSize: fontSize, selected: selectedTab);
        foreach (var state in ButtonStates)
        {
            if (button.GetThemeStylebox(state) is not StyleBoxTexture paper) continue;
            bool pressed = state is "pressed" or "hover_pressed";
            if (state == "hover_pressed" && button.GetThemeStylebox("pressed") is StyleBoxTexture pressedFace)
                paper.ModulateColor = pressedFace.ModulateColor;
            // Every state paints the full material face; none inherits the old
            // theme's text-sized pressed patch. Keep the hit area stationary.
            CopyMargins(nativeStyles["normal"], paper);
            if (pressed)
            {
                paper.ExpandMarginTop = -1; paper.ExpandMarginBottom = 1;
                paper.ContentMarginTop += 1; paper.ContentMarginBottom -= 1;
            }
        }
        button.AddThemeFontOverride("font", font);
        foreach (var color in ButtonColors)
            button.AddThemeColorOverride(color, color == "font_disabled_color" ? CraftingPaperStyle.DisabledInk : CraftingPaperStyle.Ink);
        button.AddThemeColorOverride("font_shadow_color", Colors.Transparent);
        button.SetMeta("forging_paper_selected", selectedTab);
        // The approved control skins must not enlarge the native hit boxes.
        button.CustomMinimumSize = minimum;
        button.Size = size;
    }

    private static bool IsJadeTexture(Texture2D? texture) => texture switch
    {
        AtlasTexture atlas => IsJadeTexture(atlas.Atlas),
        null => false,
        _ => texture.ResourcePath.EndsWith("/jade_button_v3.png", StringComparison.Ordinal)
    };

    private static void SkinInput(LineEdit input)
    {
        var minimum = input.CustomMinimumSize;
        var size = input.Size;
        var font = input.GetThemeFont("font");
        var fontSize = input.GetThemeFontSize("font_size");
        var native = input.GetThemeStylebox("normal");
        var ink = input.GetThemeColor("font_color");
        CraftingPaperStyle.ApplyInput(input, fontSize: fontSize);
        foreach (var state in new[] { "normal", "read_only" })
        {
            if (input.GetThemeStylebox(state) is not StyleBoxTexture paper) continue;
            CopyMargins(native, paper);
        }
        input.AddThemeFontOverride("font", font);
        input.AddThemeColorOverride("font_color", ink);
        input.CustomMinimumSize = minimum;
        input.Size = size;
    }

    private void SkinThemeStyles(Control control, string type)
    {
        var minimum = control.CustomMinimumSize;
        var size = control.Size;
        foreach (var name in _paperTheme.GetStyleboxList(type))
        {
            var source = _paperTheme.GetStylebox(name, type);
            var style = (StyleBox)source.Duplicate();
            if (style is StyleBoxTexture paper && control is VScrollBar)
                paper.ModulateColor = Colors.White;
            control.AddThemeStyleboxOverride(name, style);
        }
        control.CustomMinimumSize = minimum;
        control.Size = size;
    }

    private static void CopyMargins(StyleBox source, StyleBox target)
    {
        target.ContentMarginLeft = source.GetContentMargin(Side.Left);
        target.ContentMarginRight = source.GetContentMargin(Side.Right);
        target.ContentMarginTop = source.GetContentMargin(Side.Top);
        target.ContentMarginBottom = source.GetContentMargin(Side.Bottom);
    }

    private void RewireReturn(Button button)
    {
        if (button.HasMeta(ReturnMarker)) return;
        foreach (var connection in button.GetSignalConnectionList(BaseButton.SignalName.Pressed))
        {
            var callback = connection["callable"].AsCallable();
            if (button.IsConnected(BaseButton.SignalName.Pressed, callback))
                button.Disconnect(BaseButton.SignalName.Pressed, callback);
        }
        button.Pressed += ClosePreview;
        button.Text = "返回";
        button.SetMeta(ReturnMarker, true);
    }

    private void PlaceReturnAndHelp(Button back)
    {
        if (back.GetParent() is not Control header || header.HasMeta(NavigationMarker)) return;
        var help = header.GetChildren().OfType<Button>().FirstOrDefault(button => button.Text == "炼器说明");
        if (help is null) return;
        // One compact row: all captions use the same font metrics and center.
        header.Name = "ForgingPaperHeader";
        CraftingPaperHeader.Apply(header);
        var rowFont = back.GetThemeFont("font");
        foreach (var control in header.GetChildren().OfType<Control>())
        {
            if (control is not Label && control is not Button) continue;
            control.Position = new Vector2(control.Position.X, 8);
            control.Size = new Vector2(control.Size.X, 46);
            control.AddThemeFontOverride("font", rowFont);
            control.AddThemeFontSizeOverride("font_size", 22);
            if (control is Label label)
            {
                label.VerticalAlignment = VerticalAlignment.Center;
                label.AddThemeColorOverride("font_color", CraftingPaperStyle.Ink);
            }
        }
        CraftingPaperHeader.Place(header, help, CraftingPaperHeader.Cell.Help);
        help.Name = "ForgingHelpButton";
        var title = header.GetChildren().OfType<Label>().Single(label => label.Text.Replace(" ", "", StringComparison.Ordinal) == "炼器");
        var category = header.GetChildren().OfType<Button>().Single(button => button.Text.StartsWith("器类", StringComparison.Ordinal));
        var examples = header.GetChildren().OfType<Button>().Single(button => button.Text == "参考投料");
        var context = new Control { Name = "CraftingHeaderContext" };
        CraftingPaperHeader.Place(header, context, CraftingPaperHeader.Cell.Context);
        category.Reparent(context, false); category.Position = Vector2.Zero; category.Size = new Vector2(234, 46);
        examples.Reparent(context, false); examples.Position = new Vector2(258, 0); examples.Size = new Vector2(234, 46);
        var book = header.GetChildren().OfType<Button>().Single(button => button.Text.StartsWith("器物册", StringComparison.Ordinal));
        CraftingPaperHeader.Place(header, book, CraftingPaperHeader.Cell.Book);
        var attainmentCaption = header.GetChildren().OfType<Label>().Single(label => label.Text == "炼器造诣");
        var attainmentValue = header.GetChildren().OfType<Label>().Single(label => label != title && label != attainmentCaption);
        CraftingPaperHeader.Metric(header, CraftingPaperHeader.Cell.Attainment, "炼器造诣", attainmentValue.Text);
        foreach (var old in new[] { title, attainmentCaption, attainmentValue }) { header.RemoveChild(old); old.QueueFree(); }
        var spirit = _forging.NativePreviewSpirit;
        var spiritValue = CraftingPaperHeader.Metric(header, CraftingPaperHeader.Cell.Spirit, "灵识", $"{spirit.Available}/{spirit.Total}");
        CraftingPaperHeader.Metric(header, CraftingPaperHeader.Cell.Energy, "精力", $"{_forging.NativePreviewEnergy:0.#}");
        // Move the existing availability summary into the shared column. The
        // right-hand panel continues to show this batch's demand and breakdown.
        var available = Descendants(_forging).OfType<Label>().FirstOrDefault(label => label.Text.StartsWith("可支配灵识", StringComparison.Ordinal));
        if (available is not null)
        {
            spiritValue.TooltipText = available.TooltipText; spiritValue.MouseFilter = MouseFilterEnum.Pass;
            available.Hide();
            var costs = available.GetParent<Control>();
            var demand = costs.GetChildren().OfType<Label>().FirstOrDefault(label => label.Text.StartsWith("本次需求", StringComparison.Ordinal));
            if (demand is not null) demand.Position = new Vector2(demand.Position.X, 66);
            foreach (var bar in costs.GetChildren().OfType<ProgressBar>()) bar.Position = new Vector2(bar.Position.X, 112);
            foreach (var scroll in costs.GetChildren().OfType<ScrollContainer>())
            { scroll.Position = new Vector2(scroll.Position.X, 190); scroll.Size = new Vector2(scroll.Size.X, 273); }
        }
        CraftingPaperHeader.PlaceReturn(header.GetParent<Control>(), back);
        var triangle = new Polygon2D
        {
            Name = "PreviewReturnTriangle",
            Polygon = [new Vector2(0, 0), new Vector2(6, 4), new Vector2(0, 8)],
            Color = QuietRoomStyle.Ink
        };
        back.AddChild(triangle);
        void PositionTriangle() => triangle.Position = new Vector2(back.Size.X - 26, back.Size.Y / 2 - 4);
        back.Resized += PositionTriangle;
        PositionTriangle();
        header.SetMeta(NavigationMarker, true);
    }

    private static string DisplayHours(string text) => NativeHours.Replace(text, match =>
        double.TryParse(match.Groups["amount"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? (value * 2).ToString("0.##", CultureInfo.InvariantCulture) + "小时" : match.Value);

    private static void FormatVisibleHours(Control control)
    {
        if (control is Label label) label.Text = DisplayHours(label.Text);
        else if (control is Button button) button.Text = DisplayHours(button.Text);
        else if (control is RichTextLabel rich) rich.Text = DisplayHours(rich.Text);
        if (!string.IsNullOrEmpty(control.TooltipText)) control.TooltipText = DisplayHours(control.TooltipText);
    }

    private void BuildClosedView()
    {
        _closedCanvas = QuietRoomStyle.Canvas(this);
        _closedCanvas.Name = "PreviewClosedCanvas";
        _closedCanvas.Theme = CraftingPaperStyle.MakeTheme();
        var background = new TextureRect
        {
            Texture = GD.Load<Texture2D>("res://textures/ui/forging_20261021/ink_workshop.png"),
            Size = new Vector2(1672, 941),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _closedCanvas.AddChild(background);
        var paper = new PanelContainer { Position = new Vector2(546, 328), Size = new Vector2(580, 260) };
        paper.AddThemeStyleboxOverride("panel", CraftingPaperHeader.Paper(28));
        _closedCanvas.AddChild(paper);
        var body = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        body.AddThemeConstantOverride("separation", 26);
        paper.AddChild(body);
        var title = CraftingPaperStyle.Text("炼器预览已收起", 30);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        body.AddChild(title);
        var resume = new Button { Text = "继续查看", CustomMinimumSize = new Vector2(190, 50), SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
        CraftingPaperStyle.ApplyButton(resume, CraftingPaperStyle.ButtonKind.Primary, 22);
        resume.Pressed += OpenPreview;
        body.AddChild(resume);
        _closedCanvas.Hide();
    }

    private void ClosePreview()
    {
        HoverInfoManager.HideAll();
        if (IsRoomValid) _forging.Hide();
        _closedCanvas.Show();
    }

    private void OpenPreview()
    {
        _closedCanvas.Hide();
        if (IsRoomValid) _forging.Show();
        QueuePreviewSkin();
    }

    public Godot.Collections.Dictionary ReviewSnapshot() => new()
    {
        ["inputs_visible"] = OriginalInputsVisible,
        ["preview_visible"] = IsRoomValid && _forging.Visible,
        ["closed_visible"] = _closedCanvas.Visible,
        ["fire_ready"] = _fire is not null,
        ["ledger"] = PreviewLedger,
        ["skin_passes"] = SkinPassCount,
        ["styled_controls"] = StyledControlCount
    };

    // The GDScript review host must release temporary managed arrays while the
    // native engine is still alive, rather than during CLR shutdown.
    public void FlushReviewInterop()
    {
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        // Native child overlays handle Escape first; this only closes the
        // independent preview when no native dialog consumed the event.
        if (@event is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }) return;
        if (_closedCanvas.Visible) OpenPreview(); else ClosePreview();
        GetViewport().SetInputAsHandled();
    }
}
