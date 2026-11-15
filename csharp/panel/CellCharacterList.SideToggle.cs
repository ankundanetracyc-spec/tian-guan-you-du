using Godot;

public partial class CellCharacterList
{
    public void configure_side_toggle()
    {
        if (_toggle_button is null) return;
        _toggle_button.Pressed -= toggle_expanded;
        _toggle_button.Pressed += toggle_expanded_from_side;
        _toggle_button.AnchorLeft = 0;
        _toggle_button.AnchorRight = 0;
        _toggle_button.AnchorTop = 0;
        _toggle_button.AnchorBottom = 0;
        _toggle_button.FocusMode = Control.FocusModeEnum.None;
        _toggle_button.Flat = false;
        _toggle_button.MouseFilter = Control.MouseFilterEnum.Stop;
        _toggle_button.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
        // 名单属于普通场景 HUD（10），人物档案为 20；档案打开时应盖过名单和开关。
        _toggle_button.ZIndex = 1;
        WorldHudStyle.StylePaperButton(_toggle_button);
        apply_expanded_state();
    }

    private void toggle_expanded_from_side()
    {
        toggle_expanded();
    }

    private static StyleBoxFlat side_toggle_style(Color color, Color border)
    {
        var style = new StyleBoxFlat
        {
            BgColor = color,
            BorderColor = border,
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            ContentMarginLeft = 3,
            ContentMarginRight = 3,
            ContentMarginTop = 3,
            ContentMarginBottom = 3
        };
        style.SetCornerRadiusAll(3);
        return style;
    }
}
