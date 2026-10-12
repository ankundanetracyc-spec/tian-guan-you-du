using Godot;
using System;
using System.Collections.Generic;

/// <summary>设置入口：在首页即时应用画面与总音量，并保存到用户配置。</summary>
public partial class StartMenu
{
    private const string StartMenuSettingsPath = "user://start_menu_settings.cfg";
    private const string StartMenuFontPath = "res://resources/font/LXGWZhenKaiSlabGB-Regular.ttf";
    private static readonly Vector2I[] WindowedResolutions =
    [
        new(1280, 720),
        new(1600, 900),
        new(1920, 1080),
        new(2560, 1440)
    ];

    private readonly ConfigFile _startMenuSettings = new();
    private Control? _settingsOverlay;
    private OptionButton? _displayModeSelector;
    private OptionButton? _resolutionSelector;
    private HSlider? _masterVolumeSlider;
    private Label? _masterVolumeValue;
    private Vector2I[] _availableWindowedResolutions = [];
    private Vector2I _windowedResolution = new(1600, 900);

    private void ApplySavedStartMenuSettings()
    {
        if (_startMenuSettings.Load(StartMenuSettingsPath) != Error.Ok)
            return;

        if (_startMenuSettings.HasSectionKey("video", "windowed_width") && _startMenuSettings.HasSectionKey("video", "windowed_height"))
            _windowedResolution = new Vector2I(
                _startMenuSettings.GetValue("video", "windowed_width", 1600).AsInt32(),
                _startMenuSettings.GetValue("video", "windowed_height", 900).AsInt32());

        if (_startMenuSettings.HasSectionKey("video", "fullscreen"))
            ApplyWindowMode(_startMenuSettings.GetValue("video", "fullscreen", true).AsBool());

        if (_startMenuSettings.HasSectionKey("audio", "master_percent"))
            ApplyMasterVolume(_startMenuSettings.GetValue("audio", "master_percent", 100).AsSingle());
    }

    private void OpenStartMenuSettings()
    {
        if (_settingsOverlay is not null)
            return;

        _settingsOverlay = new Control
        {
            Name = "StartMenuSettingsOverlay",
            MouseFilter = Control.MouseFilterEnum.Stop,
            ZIndex = 300
        };
        _settingsOverlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(_settingsOverlay);

        var veil = new ColorRect
        {
            Color = new Color(0.025f, 0.045f, 0.04f, 0.50f),
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        veil.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _settingsOverlay.AddChild(veil);

        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _settingsOverlay.AddChild(center);

        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(740, 570),
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.94f, 0.92f, 0.85f, 0.99f),
            BorderColor = new Color(0.36f, 0.43f, 0.37f, 0.65f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            ContentMarginLeft = 38,
            ContentMarginRight = 38,
            ContentMarginTop = 30,
            ContentMarginBottom = 28
        });
        center.AddChild(panel);

        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 15);
        panel.AddChild(content);

        var title = new Label
        {
            Text = "设　置",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        StyleSettingsText(title, 36, new Color(0.12f, 0.20f, 0.17f));
        content.AddChild(title);

        var subtitle = new Label
        {
            Text = "画面与声音　·　改动会即时生效并自动保存",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        StyleSettingsText(subtitle, 16, new Color(0.34f, 0.36f, 0.29f));
        content.AddChild(subtitle);
        content.AddChild(SettingsRule());

        var currentMode = DisplayServer.WindowGetMode();
        var isFullscreen = currentMode is DisplayServer.WindowMode.Fullscreen or DisplayServer.WindowMode.ExclusiveFullscreen;

        var displayRow = SettingsRow("显示模式", "选择全屏或窗口显示。");
        _displayModeSelector = CreateSettingsOption("全屏", "窗口");
        StyleSettingsOption(_displayModeSelector);
        _displayModeSelector.Select(isFullscreen ? 0 : 1);
        displayRow.AddChild(_displayModeSelector);
        content.AddChild(displayRow);

        var resolutionRow = SettingsRow("窗口分辨率", "全屏时使用显示器分辨率。");
        _resolutionSelector = new OptionButton { CustomMinimumSize = new Vector2(250, 46) };
        StyleSettingsOption(_resolutionSelector);
        _availableWindowedResolutions = GetAvailableWindowedResolutions();
        foreach (var resolution in _availableWindowedResolutions)
            _resolutionSelector.AddItem($"{resolution.X} × {resolution.Y}");
        var selectedResolution = System.Array.IndexOf(_availableWindowedResolutions, _windowedResolution);
        if (selectedResolution < 0)
        {
            selectedResolution = _availableWindowedResolutions.Length - 1;
            _windowedResolution = _availableWindowedResolutions[selectedResolution];
        }
        _resolutionSelector.Select(selectedResolution);
        _resolutionSelector.Disabled = isFullscreen;
        resolutionRow.AddChild(_resolutionSelector);
        content.AddChild(resolutionRow);
        content.AddChild(SettingsRule());

        var volumeRow = SettingsRow("总音量", "调整游戏主音量。");
        _masterVolumeSlider = new HSlider
        {
            MinValue = 0,
            MaxValue = 100,
            Step = 1,
            CustomMinimumSize = new Vector2(300, 42),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        var masterBus = AudioServer.GetBusIndex("Master");
        var currentVolume = masterBus >= 0 && AudioServer.IsBusMute(masterBus)
            ? 0f
            : _startMenuSettings.HasSectionKey("audio", "master_percent")
                ? _startMenuSettings.GetValue("audio", "master_percent", 100).AsSingle()
                : 100f;
        currentVolume = Mathf.Clamp(currentVolume, 0f, 100f);
        _masterVolumeSlider.Value = currentVolume;
        StyleSettingsSlider(_masterVolumeSlider);
        _masterVolumeValue = new Label
        {
            Text = $"{Mathf.RoundToInt(currentVolume)}%",
            CustomMinimumSize = new Vector2(64, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        StyleSettingsText(_masterVolumeValue, 17, new Color(0.16f, 0.22f, 0.18f));
        volumeRow.AddChild(_masterVolumeSlider);
        volumeRow.AddChild(_masterVolumeValue);
        content.AddChild(volumeRow);

        var footerNote = new Label
        {
            Text = "窗口模式下可选择常用的 16:9 分辨率。按 Esc 可返回。",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        StyleSettingsText(footerNote, 14, new Color(0.40f, 0.39f, 0.32f));
        content.AddChild(footerNote);

        var footer = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        footer.AddThemeConstantOverride("separation", 18);
        var reset = SettingsButton("恢复默认");
        reset.Pressed += ResetStartMenuSettings;
        var close = SettingsButton("返回");
        close.Pressed += CloseStartMenuSettings;
        footer.AddChild(reset);
        footer.AddChild(close);
        content.AddChild(footer);

        _displayModeSelector.ItemSelected += index =>
        {
            var fullscreen = index == 0;
            ApplyWindowMode(fullscreen);
            if (_resolutionSelector is not null)
                _resolutionSelector.Disabled = fullscreen;
            SaveStartMenuSettings();
        };
        _resolutionSelector.ItemSelected += index =>
        {
            _windowedResolution = _availableWindowedResolutions[Mathf.Clamp((int)index, 0, _availableWindowedResolutions.Length - 1)];
            if (!IsFullscreen())
                ApplyWindowMode(false);
            SaveStartMenuSettings();
        };
        _masterVolumeSlider.ValueChanged += value =>
        {
            var percent = Mathf.Clamp((float)value, 0f, 100f);
            ApplyMasterVolume(percent);
            if (_masterVolumeValue is not null)
                _masterVolumeValue.Text = $"{Mathf.RoundToInt(percent)}%";
            SaveStartMenuSettings();
        };
        close.GrabFocus();
    }

    private void CloseStartMenuSettings()
    {
        if (_settingsOverlay is null)
            return;

        _settingsOverlay.QueueFree();
        _settingsOverlay = null;
        _displayModeSelector = null;
        _resolutionSelector = null;
        _masterVolumeSlider = null;
        _masterVolumeValue = null;
        GetNode<Button>("StartMenuMenu/Settings").GrabFocus();
    }

    private void ResetStartMenuSettings()
    {
        _windowedResolution = new Vector2I(1600, 900);
        ApplyWindowMode(true);
        ApplyMasterVolume(100f);
        if (_displayModeSelector is not null)
            _displayModeSelector.Select(0);
        if (_resolutionSelector is not null)
        {
            var resetIndex = System.Array.IndexOf(_availableWindowedResolutions, _windowedResolution);
            if (resetIndex < 0)
            {
                resetIndex = _availableWindowedResolutions.Length - 1;
                _windowedResolution = _availableWindowedResolutions[resetIndex];
            }
            _resolutionSelector.Select(resetIndex);
            _resolutionSelector.Disabled = true;
        }
        if (_masterVolumeSlider is not null)
            _masterVolumeSlider.Value = 100;
        if (_masterVolumeValue is not null)
            _masterVolumeValue.Text = "100%";
        SaveStartMenuSettings();
    }

    private void ApplyWindowMode(bool fullscreen)
    {
        if (fullscreen)
        {
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
            return;
        }

        DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
        var screen = DisplayServer.ScreenGetSize();
        var size = _windowedResolution;
        if (size.X > screen.X || size.Y > screen.Y)
        {
            _availableWindowedResolutions = GetAvailableWindowedResolutions();
            size = _availableWindowedResolutions[^1];
            _windowedResolution = size;
        }
        DisplayServer.WindowSetSize(size);
        DisplayServer.WindowSetPosition(DisplayServer.ScreenGetPosition() + new Vector2I(Mathf.Max(0, (screen.X - size.X) / 2), Mathf.Max(0, (screen.Y - size.Y) / 2)));
    }

    private bool IsFullscreen()
    {
        var mode = DisplayServer.WindowGetMode();
        return mode is DisplayServer.WindowMode.Fullscreen or DisplayServer.WindowMode.ExclusiveFullscreen;
    }

    private static void ApplyMasterVolume(float percent)
    {
        var bus = AudioServer.GetBusIndex("Master");
        if (bus < 0)
            return;

        percent = Mathf.Clamp(percent, 0f, 100f);
        AudioServer.SetBusMute(bus, percent <= 0f);
        if (percent > 0f)
            AudioServer.SetBusVolumeDb(bus, Mathf.LinearToDb(percent / 100f));
    }

    private void SaveStartMenuSettings()
    {
        _startMenuSettings.SetValue("video", "fullscreen", IsFullscreen());
        _startMenuSettings.SetValue("video", "windowed_width", _windowedResolution.X);
        _startMenuSettings.SetValue("video", "windowed_height", _windowedResolution.Y);
        var bus = AudioServer.GetBusIndex("Master");
        var volumePercent = bus >= 0 && AudioServer.IsBusMute(bus)
            ? 0f
            : _masterVolumeSlider is not null
                ? (float)_masterVolumeSlider.Value
                : _startMenuSettings.GetValue("audio", "master_percent", 100).AsSingle();
        _startMenuSettings.SetValue("audio", "master_percent", Mathf.Clamp(volumePercent, 0f, 100f));

        var error = _startMenuSettings.Save(StartMenuSettingsPath);
        if (error != Error.Ok)
            GD.PushWarning($"开始界面设置未能保存：{error}");
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey key || !key.Pressed || key.Echo || key.Keycode != Key.Escape)
            return;
        if (_settingsOverlay is not null) CloseStartMenuSettings();
        else if (_demoOverlay is not null) CloseDemoEntries();
        else return;
        GetViewport().SetInputAsHandled();
    }

    private static HBoxContainer SettingsRow(string heading, string summary)
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

    private static OptionButton CreateSettingsOption(params string[] options)
    {
        var selector = new OptionButton { CustomMinimumSize = new Vector2(250, 46) };
        foreach (var option in options)
            selector.AddItem(option);
        return selector;
    }

    private static Vector2I[] GetAvailableWindowedResolutions()
    {
        var screen = DisplayServer.ScreenGetSize();
        var available = new List<Vector2I>();
        foreach (var resolution in WindowedResolutions)
            if (resolution.X <= screen.X && resolution.Y <= screen.Y)
                available.Add(resolution);

        if (available.Count == 0)
        {
            var width = Math.Max(1, Math.Min(1280, screen.X));
            var height = width * 9 / 16;
            if (height > screen.Y)
            {
                height = Math.Max(1, screen.Y);
                width = height * 16 / 9;
            }
            available.Add(new Vector2I(width, height));
        }
        return available.ToArray();
    }

    private static ColorRect SettingsRule() => new()
    {
        CustomMinimumSize = new Vector2(0, 1),
        Color = new Color(0.35f, 0.31f, 0.22f, 0.35f),
        MouseFilter = Control.MouseFilterEnum.Ignore
    };

    private static Button SettingsButton(string label)
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

    private static void StyleSettingsText(Control control, int fontSize, Color color)
    {
        control.AddThemeFontSizeOverride("font_size", fontSize);
        control.AddThemeColorOverride("font_color", color);
        if (GD.Load<FontFile>(StartMenuFontPath) is { } font)
            control.AddThemeFontOverride("font", font);
    }
}
