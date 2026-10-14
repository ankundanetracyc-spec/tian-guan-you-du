using Godot;
using System;
using System.Collections.Generic;

/// <summary>Shared settings overlay used by both the title screen and the in-game pause menu.</summary>
public partial class GameSettingsPanel : Control
{
    private const string SettingsPath = "user://start_menu_settings.cfg";
    private static readonly Vector2I[] WindowedResolutions =
    [
        new(1280, 720), new(1600, 900), new(1920, 1080), new(2560, 1440)
    ];

    private readonly ConfigFile _settings = new();
    private OptionButton? _displayModeSelector;
    private OptionButton? _resolutionSelector;
    private HSlider? _masterVolumeSlider;
    private Label? _masterVolumeValue;
    private Vector2I[] _availableWindowedResolutions = [];
    private Vector2I _windowedResolution = new(1600, 900);

    public event Action? Closed;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        MouseFilter = MouseFilterEnum.Stop;
        ZIndex = 300;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _settings.Load(SettingsPath);
        if (_settings.HasSectionKey("video", "windowed_width") && _settings.HasSectionKey("video", "windowed_height"))
            _windowedResolution = new Vector2I(
                _settings.GetValue("video", "windowed_width", 1600).AsInt32(),
                _settings.GetValue("video", "windowed_height", 900).AsInt32());
        build_panel();
    }

    public static void ApplySavedSettings()
    {
        var settings = new ConfigFile();
        if (settings.Load(SettingsPath) != Error.Ok) return;
        if (settings.HasSectionKey("video", "fullscreen"))
            ApplyWindowMode(settings.GetValue("video", "fullscreen", true).AsBool(),
                new Vector2I(settings.GetValue("video", "windowed_width", 1600).AsInt32(), settings.GetValue("video", "windowed_height", 900).AsInt32()));
        if (settings.HasSectionKey("audio", "master_percent")) ApplyMasterVolume(settings.GetValue("audio", "master_percent", 100).AsSingle());
    }

    public void ClosePanel()
    {
        Closed?.Invoke();
        QueueFree();
    }

    private void build_panel()
    {
        var veil = new ColorRect { Color = new Color(0.025f, 0.045f, 0.04f, 0.50f), MouseFilter = MouseFilterEnum.Stop };
        veil.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(veil);

        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);
        var viewport = GetViewportRect().Size;
        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(Mathf.Min(740, Mathf.Max(320, viewport.X - 32)), Mathf.Min(570, Mathf.Max(260, viewport.Y - 32))),
            MouseFilter = MouseFilterEnum.Stop
        };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.94f, 0.92f, 0.85f, 0.99f),
            BorderColor = new Color(0.36f, 0.43f, 0.37f, 0.65f),
            BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
            ContentMarginLeft = 38, ContentMarginRight = 38, ContentMarginTop = 30, ContentMarginBottom = 28
        });
        center.AddChild(panel);

        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, SizeFlagsVertical = SizeFlags.ExpandFill };
        panel.AddChild(scroll);
        var content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 15);
        scroll.AddChild(content);
        var title = new Label { Text = "设　置", HorizontalAlignment = HorizontalAlignment.Center };
        StartMenu.StyleSettingsText(title, 36, new Color(0.12f, 0.20f, 0.17f));
        content.AddChild(title);
        var subtitle = new Label { Text = "画面与声音　·　改动会即时生效并自动保存", HorizontalAlignment = HorizontalAlignment.Center };
        StartMenu.StyleSettingsText(subtitle, 16, new Color(0.34f, 0.36f, 0.29f));
        content.AddChild(subtitle);
        content.AddChild(StartMenu.SettingsRule());

        var isFullscreen = IsFullscreen();
        var displayRow = StartMenu.SettingsRow("显示模式", "选择全屏或窗口显示。");
        _displayModeSelector = StartMenu.CreateSettingsOption("全屏", "窗口");
        StartMenu.StyleSettingsOption(_displayModeSelector);
        _displayModeSelector.Select(isFullscreen ? 0 : 1);
        displayRow.AddChild(_displayModeSelector);
        content.AddChild(displayRow);

        var resolutionRow = StartMenu.SettingsRow("窗口分辨率", "全屏时使用显示器分辨率。");
        _resolutionSelector = new OptionButton { CustomMinimumSize = new Vector2(250, 46) };
        StartMenu.StyleSettingsOption(_resolutionSelector);
        _availableWindowedResolutions = GetAvailableWindowedResolutions();
        foreach (var resolution in _availableWindowedResolutions) _resolutionSelector.AddItem($"{resolution.X} × {resolution.Y}");
        var selectedResolution = Array.IndexOf(_availableWindowedResolutions, _windowedResolution);
        if (selectedResolution < 0)
        {
            selectedResolution = _availableWindowedResolutions.Length - 1;
            _windowedResolution = _availableWindowedResolutions[selectedResolution];
        }
        _resolutionSelector.Select(selectedResolution);
        _resolutionSelector.Disabled = isFullscreen;
        resolutionRow.AddChild(_resolutionSelector);
        content.AddChild(resolutionRow);
        content.AddChild(StartMenu.SettingsRule());

        var volumeRow = StartMenu.SettingsRow("总音量", "调整游戏主音量。");
        _masterVolumeSlider = new HSlider { MinValue = 0, MaxValue = 100, Step = 1, CustomMinimumSize = new Vector2(300, 42), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var masterBus = AudioServer.GetBusIndex("Master");
        var currentVolume = masterBus >= 0 && AudioServer.IsBusMute(masterBus)
            ? 0f
            : _settings.HasSectionKey("audio", "master_percent") ? _settings.GetValue("audio", "master_percent", 100).AsSingle() : 100f;
        currentVolume = Mathf.Clamp(currentVolume, 0f, 100f);
        _masterVolumeSlider.Value = currentVolume;
        StartMenu.StyleSettingsSlider(_masterVolumeSlider);
        _masterVolumeValue = new Label { Text = $"{Mathf.RoundToInt(currentVolume)}%", CustomMinimumSize = new Vector2(64, 0), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        StartMenu.StyleSettingsText(_masterVolumeValue, 17, new Color(0.16f, 0.22f, 0.18f));
        volumeRow.AddChild(_masterVolumeSlider);
        volumeRow.AddChild(_masterVolumeValue);
        content.AddChild(volumeRow);

        var footerNote = new Label { Text = "窗口模式下可选择常用的 16:9 分辨率。", HorizontalAlignment = HorizontalAlignment.Center };
        StartMenu.StyleSettingsText(footerNote, 14, new Color(0.40f, 0.39f, 0.32f));
        content.AddChild(footerNote);
        var footer = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        footer.AddThemeConstantOverride("separation", 18);
        var reset = StartMenu.SettingsButton("恢复默认");
        reset.Pressed += reset_settings;
        var close = StartMenu.SettingsButton("返回");
        close.Pressed += ClosePanel;
        footer.AddChild(reset);
        footer.AddChild(close);
        content.AddChild(footer);

        _displayModeSelector.ItemSelected += index =>
        {
            var fullscreen = index == 0;
            apply_window_mode(fullscreen);
            if (_resolutionSelector is not null) _resolutionSelector.Disabled = fullscreen;
            save_settings();
        };
        _resolutionSelector.ItemSelected += index =>
        {
            _windowedResolution = _availableWindowedResolutions[Mathf.Clamp((int)index, 0, _availableWindowedResolutions.Length - 1)];
            if (!IsFullscreen()) apply_window_mode(false);
            save_settings();
        };
        _masterVolumeSlider.ValueChanged += value =>
        {
            var percent = Mathf.Clamp((float)value, 0f, 100f);
            ApplyMasterVolume(percent);
            if (_masterVolumeValue is not null) _masterVolumeValue.Text = $"{Mathf.RoundToInt(percent)}%";
            save_settings();
        };
        close.GrabFocus();
    }

    private void reset_settings()
    {
        _windowedResolution = new Vector2I(1600, 900);
        apply_window_mode(true);
        ApplyMasterVolume(100f);
        _displayModeSelector?.Select(0);
        if (_resolutionSelector is not null)
        {
            var resetIndex = Array.IndexOf(_availableWindowedResolutions, _windowedResolution);
            if (resetIndex < 0)
            {
                resetIndex = _availableWindowedResolutions.Length - 1;
                _windowedResolution = _availableWindowedResolutions[resetIndex];
            }
            _resolutionSelector.Select(resetIndex);
            _resolutionSelector.Disabled = true;
        }
        if (_masterVolumeSlider is not null) _masterVolumeSlider.Value = 100;
        if (_masterVolumeValue is not null) _masterVolumeValue.Text = "100%";
        save_settings();
    }

    private void apply_window_mode(bool fullscreen) => ApplyWindowMode(fullscreen, _windowedResolution);

    private static void ApplyWindowMode(bool fullscreen, Vector2I windowedResolution)
    {
        if (fullscreen)
        {
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
            return;
        }
        DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
        var screen = DisplayServer.ScreenGetSize();
        var size = windowedResolution;
        if (size.X > screen.X || size.Y > screen.Y)
        {
            var available = GetAvailableWindowedResolutions();
            size = available[^1];
        }
        DisplayServer.WindowSetSize(size);
        DisplayServer.WindowSetPosition(DisplayServer.ScreenGetPosition() + new Vector2I(Mathf.Max(0, (screen.X - size.X) / 2), Mathf.Max(0, (screen.Y - size.Y) / 2)));
    }

    private static bool IsFullscreen()
    {
        var mode = DisplayServer.WindowGetMode();
        return mode is DisplayServer.WindowMode.Fullscreen or DisplayServer.WindowMode.ExclusiveFullscreen;
    }

    private static void ApplyMasterVolume(float percent)
    {
        var bus = AudioServer.GetBusIndex("Master");
        if (bus < 0) return;
        percent = Mathf.Clamp(percent, 0f, 100f);
        AudioServer.SetBusMute(bus, percent <= 0f);
        if (percent > 0f) AudioServer.SetBusVolumeDb(bus, Mathf.LinearToDb(percent / 100f));
    }

    private void save_settings()
    {
        _settings.SetValue("video", "fullscreen", IsFullscreen());
        _settings.SetValue("video", "windowed_width", _windowedResolution.X);
        _settings.SetValue("video", "windowed_height", _windowedResolution.Y);
        var bus = AudioServer.GetBusIndex("Master");
        var volumePercent = bus >= 0 && AudioServer.IsBusMute(bus) ? 0f : (float)(_masterVolumeSlider?.Value ?? 100.0);
        _settings.SetValue("audio", "master_percent", Mathf.Clamp(volumePercent, 0f, 100f));
        var error = _settings.Save(SettingsPath);
        if (error != Error.Ok) GD.PushWarning($"游戏设置未能保存：{error}");
    }

    private static Vector2I[] GetAvailableWindowedResolutions()
    {
        var screen = DisplayServer.ScreenGetSize();
        var available = new List<Vector2I>();
        foreach (var resolution in WindowedResolutions)
            if (resolution.X <= screen.X && resolution.Y <= screen.Y) available.Add(resolution);
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
}
