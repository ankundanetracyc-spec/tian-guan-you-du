using Godot;

public partial class MainHUD
{
    private void ConfigureNavigation()
    {
        if (_actionBarToggle is null) return;
        WorldHudStyle.StyleSmallButton(_actionBarToggle);
        var frame = new WorldHudFrame { Name = "ActionTogglePaper", Circular = true };
        AddChild(frame);
        frame.SetAnchorsPreset(LayoutPreset.BottomRight);
        frame.OffsetLeft = -82; frame.OffsetRight = -22;
        frame.OffsetTop = -112; frame.OffsetBottom = -52;
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
        _actionBarToggle.TooltipText = _actionBarExpanded ? "收起下方功能入口" : "展开下方功能入口";
    }
}
