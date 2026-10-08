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
        _toggle_button.AddThemeStyleboxOverride("normal", side_toggle_style(new Color(.025f, .03f, .026f, .96f), new Color(.72f, .65f, .47f, .78f)));
        _toggle_button.AddThemeStyleboxOverride("hover", side_toggle_style(new Color(.14f, .15f, .12f, .98f), new Color(.88f, .77f, .52f, .95f)));
        _toggle_button.AddThemeStyleboxOverride("pressed", side_toggle_style(new Color(.20f, .18f, .13f, 1f), new Color(.94f, .82f, .55f, 1f)));
        apply_expanded_state();
    }

    private void toggle_expanded_from_side()
    {
        _expanded = !_expanded;
        Size = new Vector2(300, _expanded_height);
        if (_list_panel is not null)
        {
            _list_panel.Visible = true;
            if (_characters is not null) _characters.Visible = _expanded;
        }
        if (_toggle_button is not null)
        {
            _toggle_button.Text = _expanded ? "◀" : "▶";
            _toggle_button.TooltipText = _expanded ? "收起角色列表" : "展开角色列表";
            _toggle_button.Size = new Vector2(30, Mathf.Max(60, _expanded_height * .5f));
        }

        _toggle_tween?.Kill();
        _toggle_tween = CreateTween();
        _toggle_tween.SetTrans(Tween.TransitionType.Cubic);
        _toggle_tween.SetEase(Tween.EaseType.InOut);
        if (_toggle_button is not null)
        {
            var toggle_position = new Vector2(_expanded ? 270 : 0, (_expanded_height - _toggle_button.Size.Y) * .5f);
            _toggle_tween.TweenProperty(_toggle_button, "position", toggle_position, .2);
        }
        if (_list_panel is not null)
            _toggle_tween.Parallel().TweenProperty(_list_panel, "position", _expanded ? Vector2.Zero : new Vector2(-270, 0), .2);
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
