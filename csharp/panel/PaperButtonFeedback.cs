using Godot;

/// <summary>Opt-in full-paper states: no separately tinted rectangle behind the caption.</summary>
public static class PaperButtonFeedback
{
    private const string Hook = "paper_feedback_hook", Held = "paper_feedback_held",
        Kind = "paper_feedback_kind", Selection = "paper_feedback_selection";
    public static StyleBoxTexture Face(CraftingPaperStyle.ButtonKind kind,
        CraftingPaperStyle.ButtonState state = CraftingPaperStyle.ButtonState.Normal, bool selected = false)
    {
        // These pages retain their established whole-paper tint and temporary press feedback.
        // Start from the normal face so a selected surface is never tinted twice.
        if (state == CraftingPaperStyle.ButtonState.Selected)
        { selected = true; state = CraftingPaperStyle.ButtonState.Normal; }
        var face = CraftingPaperStyle.ButtonStyle(kind, state);
        if (selected && kind != CraftingPaperStyle.ButtonKind.Navigation && state != CraftingPaperStyle.ButtonState.Disabled)
            face.ModulateColor *= new Color(CraftingPaperStyle.SelectedDarkening, CraftingPaperStyle.SelectedDarkening, CraftingPaperStyle.SelectedDarkening);
        if (state == CraftingPaperStyle.ButtonState.Pressed)
        {
            face.ExpandMarginTop = -1; face.ExpandMarginBottom = 1;
            face.ContentMarginTop += 1; face.ContentMarginBottom -= 1;
        }
        return face;
    }

    public static void Apply(Button button, CraftingPaperStyle.ButtonKind kind = CraftingPaperStyle.ButtonKind.Regular,
        int fontSize = 22, bool selected = false, bool trackPress = true)
    {
        CraftingPaperStyle.ApplyButton(button, kind, fontSize, selected: false);
        button.SetMeta(Kind, (int)kind); button.SetMeta(Selection, selected);
        if (trackPress && !button.HasMeta(Hook))
        {
            button.SetMeta(Hook, true);
            button.ButtonDown += () => SetHeld(button, true);
            button.ButtonUp += () => SetHeld(button, false);
            button.FocusExited += () => SetHeld(button, false);
            button.VisibilityChanged += () => { if (!button.IsVisibleInTree()) SetHeld(button, false); };
        }
        Refresh(button);
        button.AddThemeColorOverride("font_shadow_color", Colors.Transparent);
    }

    private static void SetHeld(Button button, bool held)
    {
        if (!GodotObject.IsInstanceValid(button) || button.IsQueuedForDeletion()) return;
        button.SetMeta(Held, held); Refresh(button);
    }

    private static void Refresh(Button button)
    {
        var kind = (CraftingPaperStyle.ButtonKind)button.GetMeta(Kind).AsInt32();
        bool selected = button.GetMeta(Selection, false).AsBool();
        bool held = button.GetMeta(Held, false).AsBool();
        foreach (var (name, state) in new[]
        {
            ("normal", CraftingPaperStyle.ButtonState.Normal), ("hover", CraftingPaperStyle.ButtonState.Hover),
            ("pressed", CraftingPaperStyle.ButtonState.Pressed), ("hover_pressed", CraftingPaperStyle.ButtonState.Pressed),
            ("disabled", CraftingPaperStyle.ButtonState.Disabled)
        })
        {
            var surface = state;
            // A latched choice keeps its selection color after release, while
            // the physical press is temporary even when it was already chosen.
            if (button.ToggleMode && selected && !held && state == CraftingPaperStyle.ButtonState.Pressed)
                surface = name == "hover_pressed" ? CraftingPaperStyle.ButtonState.Hover : CraftingPaperStyle.ButtonState.Normal;
            button.AddThemeStyleboxOverride(name, Face(kind, surface, selected));
        }
    }
}
