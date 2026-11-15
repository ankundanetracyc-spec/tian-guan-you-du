using Godot;

public partial class MainHUD
{
    public void set_paper_scene(bool paper)
    {
        // Each label now has its own paper backing on every scene background.
    }

    private void ConfigureNavigation()
    {
        if (_actionBar is not null)
        {
            _actionBar.OffsetLeft = -820;
            _actionBar.OffsetRight = -104;
            _actionBar.OffsetTop = -128;
            _actionBar.OffsetBottom = -22;
            _actionBar.MouseFilter = MouseFilterEnum.Ignore;
            _actionBar.AddThemeConstantOverride("separation", 6);
            foreach (var entry in _actionBar.GetChildren().OfType<VBoxContainer>())
            {
                entry.MouseFilter = MouseFilterEnum.Ignore;
                entry.AddThemeConstantOverride("separation", 4);
                if (entry.GetChildOrNull<Button>(0) is { } button)
                {
                    button.CustomMinimumSize = new Vector2(80, 76);
                    button.ExpandIcon = true;
                    button.MouseDefaultCursorShape = CursorShape.PointingHand;
                    button.AddThemeStyleboxOverride("hover", new StyleBoxEmpty());
                    button.AddThemeStyleboxOverride("pressed", new StyleBoxEmpty());
                    var ring = WorldHudArt.Mark("ink_ring", new Vector2(0, -2), new Vector2(80, 80));
                    ring.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
                    ring.Name = "NavigationInkRing";
                    button.AddChild(ring);
                    button.MouseEntered += () => button.Modulate = new Color("e1e9db");
                    button.MouseExited += () => button.Modulate = Colors.White;
                }
                if (entry.GetNodeOrNull<Label>("Label") is { } label)
                {
                    WorldHudStyle.StyleText(label, 18);
                    WorldHudArt.AddLabelPaper(label);
                    label.CustomMinimumSize = new Vector2(80, 26);
                    label.MouseFilter = MouseFilterEnum.Ignore;
                }
            }
        }
        if (_actionBarToggle is null) return;
        WorldHudStyle.StyleSmallButton(_actionBarToggle);
        var frame = new WorldHudFrame { Name = "ActionTogglePaper", Circular = true };
        AddChild(frame);
        frame.SetAnchorsPreset(LayoutPreset.BottomRight);
        frame.OffsetLeft = -82; frame.OffsetRight = -22;
        frame.OffsetTop = -120; frame.OffsetBottom = -60;
        _actionBarToggle.OffsetTop = -120; _actionBarToggle.OffsetBottom = -60;
        _actionBarToggle.AddThemeFontSizeOverride("font_size", 22);
        _actionBarToggle.MoveToFront();
        _actionBarToggle.MouseEntered += () => frame.Modulate = new Color(.96f, .98f, .95f);
        _actionBarToggle.MouseExited += () => frame.Modulate = Colors.White;
        _actionBarToggle.Pressed += toggle_action_bar;
        RefreshNavigationToggle();
    }

    private void RefreshNavigationToggle()
    {
        if (_actionBarToggle is null) return;
        _actionBarToggle.Text = _actionBarExpanded ? "▶" : "◀";
    }
}
