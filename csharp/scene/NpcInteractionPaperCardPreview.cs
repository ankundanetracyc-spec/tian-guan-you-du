using Godot;

/// <summary>Preview-only paper presentation of the real local NPC page. Formal entries use the original class.</summary>
public partial class NpcInteractionPaperCardPreview : NpcInteractionPanel
{
    private PanelContainer _paperFrame = null!;
    private Control _portraitMount = null!;
    private GridContainer _paperActions = null!;
    public Rect2 DesignBounds { get; set; } = new(340, 145, 874, 602);
    public override void _Ready()
    {
        base._Ready();
        Theme = CraftingPaperStyle.MakeTheme();
        _paperFrame = GetNode<PanelContainer>("NpcInteractionPage");
        GetNode<Control>("NpcInteractionPage/NpcPaperSurface").Hide();
        _paperFrame.AddThemeStyleboxOverride("panel", CraftingPaperStyle.Paper(0, true, .97f));
        ((Button)FindChild("CloseButton", true, false)).Hide();
        var margin = _paperFrame.GetChildren().OfType<MarginContainer>().Single();
        foreach (string edge in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride("margin_" + edge, 25);
        _portraitMount = (Control)FindChild("PortraitSlot", true, false);
        _portraitMount.ClipContents = true;
        _paperActions = (GridContainer)FindChild("InteractionActions", true, false);
        _paperActions.AddThemeConstantOverride("h_separation", 12);
        _paperActions.AddThemeConstantOverride("v_separation", 12);
        var body = (HBoxContainer)_portraitMount.GetParent();
        body.AddThemeConstantOverride("separation", 26);
        var divider = new VSeparator { MouseFilter = MouseFilterEnum.Ignore };
        divider.AddThemeStyleboxOverride("separator", new StyleBoxLine { Color = new Color("b7ad97"), Thickness = 1, Vertical = true });
        body.AddChild(divider); body.MoveChild(divider, 1);
        foreach (var (name, size) in new[] { ("NpcName", 30), ("NpcSummary", 21), ("NpcBasicInformation", 21), ("NpcIntroduction", 18), ("InteractionRequirements", 17) })
        {
            var label = (Label)FindChild(name, true, false);
            label.AddThemeFontOverride("font", GD.Load<FontFile>(CraftingPaperStyle.FontPath));
            label.AddThemeFontSizeOverride("font_size", size);
            label.AddThemeColorOverride("font_color", name is "NpcSummary" or "InteractionRequirements" ? CraftingPaperStyle.Muted : CraftingPaperStyle.Ink);
        }
        ((Label)FindChild("NpcName", true, false)).CustomMinimumSize = new(0, 48);
    }
    public void ShowPaperNpc(WorldNpcData npc, Func<WorldNpcData?> target, Func<IEnumerable<NpcInteractionAction>> actions)
    {
        show_npc_page(npc, target, actions);
        SkinActions(); ArrangePaper();
        CallDeferred(nameof(ArrangePaper));
    }
    public void RefreshPaperActions() { refresh_menu_actions(); SkinActions(); ArrangePaper(); }
    private void SkinActions()
    {
        foreach (var button in _paperActions.GetChildren().OfType<Button>())
        {
            PaperButtonFeedback.Apply(button, fontSize: 22);
            button.CustomMinimumSize = new(0, 52);
            button.TooltipText = "";
            button.Name = "NpcAction_" + button.GetMeta("action_id").AsString().Replace('.', '_');
            if (button.GetMeta("action_id").AsString() is "attack" or "attack.confirm")
                foreach (string state in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color" })
                    button.AddThemeColorOverride(state, CraftingPaperStyle.Danger);
        }
    }
    private void ArrangePaper()
    {
        if (_paperFrame is null) return;
        _paperFrame.Position = DesignBounds.Position; _paperFrame.Size = DesignBounds.Size;
        _portraitMount.CustomMinimumSize = new(260, 0);
        _paperActions.Columns = 2;
    }
    public override void _Process(double delta) { if (Visible) ArrangePaper(); }
}
