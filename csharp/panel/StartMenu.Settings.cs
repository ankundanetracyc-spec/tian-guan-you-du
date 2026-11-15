using Godot;

public partial class StartMenu
{
    private const string StartMenuFontPath = "res://resources/font/LXGWZhenKaiSlabGB-Regular.ttf";
    private GameSettingsPanel? _settingsOverlay;

    private void ApplySavedStartMenuSettings() => GameSettingsPanel.ApplySavedSettings();

    private void OpenStartMenuSettings()
    {
        if (_settingsOverlay is not null) return;
        _settingsOverlay = new GameSettingsPanel { Name = "StartMenuSettingsOverlay" };
        _settingsOverlay.Closed += on_start_menu_settings_closed;
        AddChild(_settingsOverlay);
        _settingsOverlay.MoveToFront();
    }

    private void CloseStartMenuSettings()
    {
        if (_settingsOverlay is null) return;
        var panel = _settingsOverlay;
        _settingsOverlay = null;
        panel.Closed -= on_start_menu_settings_closed;
        panel.ClosePanel();
        focus_start_menu_settings_button();
    }

    private void on_start_menu_settings_closed()
    {
        _settingsOverlay = null;
        focus_start_menu_settings_button();
    }

    private void focus_start_menu_settings_button()
    {
        if (GetNodeOrNull<Button>("StartMenuMenu/Settings") is { } button && button.IsVisibleInTree()) button.GrabFocus();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey key || !key.Pressed || key.Echo || key.Keycode != Key.Escape) return;
        if (_settingsOverlay is not null) CloseStartMenuSettings();
        else if (_demoOverlay is not null) CloseDemoEntries();
        else return;
        GetViewport().SetInputAsHandled();
    }

    public static HBoxContainer SettingsRow(string heading, string summary)
    {
        var row = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(0, 74),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            Alignment = BoxContainer.AlignmentMode.Begin
        };
        row.AddThemeConstantOverride("separation", 20);
        var labels = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(220, 0),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
            Alignment = BoxContainer.AlignmentMode.Center
        };
        labels.AddThemeConstantOverride("separation", 3);
        var title = new Label { Text = heading };
        StyleSettingsText(title, 20, new Color(0.15f, 0.21f, 0.18f));
        var hint = new Label { Text = summary };
        StyleSettingsText(hint, 13, new Color(0.39f, 0.39f, 0.33f));
        labels.AddChild(title);
        labels.AddChild(hint);
        row.AddChild(labels);
        return row;
    }

    public static OptionButton CreateSettingsOption(params string[] options)
    {
        var selector = new OptionButton { CustomMinimumSize = new Vector2(250, 46) };
        foreach (var option in options) selector.AddItem(option);
        return selector;
    }

    public static ColorRect SettingsRule() => new()
    {
        CustomMinimumSize = new Vector2(0, 1),
        Color = new Color(0.35f, 0.31f, 0.22f, 0.35f),
        MouseFilter = Control.MouseFilterEnum.Ignore
    };

    public static Button SettingsButton(string label)
    {
        var button = new Button
        {
            Text = label,
            CustomMinimumSize = new Vector2(170, 46),
            FocusMode = Control.FocusModeEnum.All
        };
        StyleSettingsText(button, 17, new Color(0.91f, 0.88f, 0.79f));
        button.AddThemeColorOverride("font_hover_color", new Color(0.96f, 0.93f, 0.84f));
        button.AddThemeColorOverride("font_pressed_color", new Color(0.96f, 0.93f, 0.84f));
        button.AddThemeColorOverride("font_focus_color", new Color(0.96f, 0.93f, 0.84f));
        button.AddThemeStyleboxOverride("focus", SettingsFocusStyle());
        button.AddThemeStyleboxOverride("normal", MenuPaperButtonStyle(true));
        button.AddThemeStyleboxOverride("hover", MenuPaperButtonStyle(true, true));
        button.AddThemeStyleboxOverride("pressed", MenuPaperButtonStyle(true, true));
        return button;
    }

    private static StyleBoxFlat SettingsButtonStyle(Color background, Color border) => new()
    {
        BgColor = background,
        BorderColor = border,
        ContentMarginLeft = 16,
        ContentMarginRight = 16,
        ContentMarginTop = 8,
        ContentMarginBottom = 8,
        BorderWidthLeft = 1,
        BorderWidthTop = 1,
        BorderWidthRight = 1,
        BorderWidthBottom = 1,
        CornerRadiusTopLeft = 4,
        CornerRadiusTopRight = 4,
        CornerRadiusBottomLeft = 4,
        CornerRadiusBottomRight = 4
    };

    public static void StyleSettingsText(Control control, int fontSize, Color color)
    {
        control.AddThemeFontSizeOverride("font_size", fontSize);
        control.AddThemeColorOverride("font_color", color);
        if (GD.Load<FontFile>(StartMenuFontPath) is { } font) control.AddThemeFontOverride("font", font);
    }
}
