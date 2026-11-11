using Godot;

/// <summary>Preview-only skin over the formal bottom-dialogue and response lifecycle.</summary>
public partial class StoryDialoguePaperPreviewPanel : StoryDialoguePanel
{
    public Label SpeakerLabel { get; private set; } = null!;
    public Label BodyLabel { get; private set; } = null!;
    public ScrollContainer BodyScroll { get; private set; } = null!;
    public PanelContainer PaperFrame { get; private set; } = null!;
    public override void _Ready()
    {
        base._Ready(); Theme = CraftingPaperStyle.MakeTheme();
        var originalPaper = (Control)FindChild("DialogueInkPaper", true, false);
        PaperFrame = (PanelContainer)originalPaper.GetParent(); PaperFrame.Name = "PaperDialogueFrame";
        originalPaper.Hide();
        PaperFrame.AddThemeStyleboxOverride("panel", CraftingPaperStyle.Paper(0, true, .98f));
        var margin = PaperFrame.GetChildren().OfType<MarginContainer>().Single();
        var column = margin.GetChildren().OfType<VBoxContainer>().Single();
        var heading = column.GetChildren().OfType<HBoxContainer>().Single();
        SpeakerLabel = heading.GetChildren().OfType<Label>().Single(); SpeakerLabel.Name = "PaperDialogueSpeaker";
        SpeakerLabel.AddThemeColorOverride("font_color", CraftingPaperStyle.Ink);
        BodyScroll = column.GetChildren().OfType<ScrollContainer>().Single(); BodyScroll.Name = "PaperDialogueBodyScroll";
        BodyLabel = BodyScroll.GetChildren().OfType<Label>().Single(); BodyLabel.Name = "PaperDialogueBody";
        BodyLabel.AddThemeColorOverride("font_color", CraftingPaperStyle.Ink);
        var hint = column.GetChildren().OfType<Label>().Single(); hint.Name = "PaperDialogueHint";
        hint.AddThemeFontSizeOverride("font_size", 17); hint.AddThemeColorOverride("font_color", CraftingPaperStyle.Muted);
        foreach (var label in new[] { SpeakerLabel, BodyLabel, hint })
        {
            label.AddThemeFontOverride("font", GD.Load<FontFile>(CraftingPaperStyle.FontPath));
            label.AddThemeColorOverride("font_shadow_color", Colors.Transparent);
        }
        heading.GetChildren().OfType<HSeparator>().Single().AddThemeStyleboxOverride("separator",
            new StyleBoxLine { Color = new Color("a59783"), Thickness = 1 });
        Resized += () => CallDeferred(nameof(AlignPortrait));
    }
    public void SkinResponses()
    {
        if (FindChild("DialogueResponseList", true, false) is not VBoxContainer choices) return;
        int index = 0;
        foreach (var button in choices.GetChildren().OfType<Button>())
        {
            button.Name = "PaperResponse_" + index++;
            foreach (var backing in button.GetChildren().OfType<ColorRect>()) backing.Hide();
            PaperButtonFeedback.Apply(button, fontSize: 22);
            button.Alignment = HorizontalAlignment.Left;
            foreach (var arrow in button.GetChildren().OfType<Label>())
            {
                arrow.AddThemeFontOverride("font", GD.Load<FontFile>(CraftingPaperStyle.FontPath));
                arrow.AddThemeColorOverride("font_color", CraftingPaperStyle.Muted);
            }
        }
    }
    public void AlignPortrait()
    {
        if (PaperFrame is null || FindChild("SpeakerPortrait", true, false) is not Control { Visible: true } portrait) return;
        // The old layout adds 8px below the card edge. Clip the visible portrait exactly above the paper instead.
        portrait.Position = new Vector2(portrait.Position.X, PaperFrame.Position.Y - portrait.Size.Y);
    }
    public override void _Process(double delta) { base._Process(delta); AlignPortrait(); }
}
