using Godot;
using System;

/// <summary>Routes gameplay Escape and right-click input above scene-local UI handlers.</summary>
public partial class GameplayInputRouter : Node
{
    private const string StartMenuPath = "res://tscn/scene/StartMenu.tscn";
    private const float RightClickDragThreshold = 6f;
    private CanvasLayer? _layer;
    private Control? _root;
    private PanelContainer? _menuPanel;
    private VBoxContainer? _menuContent;
    private Label? _subtitle;
    private Button? _saveButton;
    private Button? _settingsButton;
    private GameSettingsPanel? _settingsPanel;
    private bool _pauseMenuOpen;
    private bool _homeConfirmationOpen;
    private bool _pausedBeforeMenu;
    private bool _rightPressActive;
    private bool _rightPressDragged;
    private Vector2 _rightPressPosition;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        SetProcess(true);
    }

    public override void _Input(InputEvent @event)
    {
        if (SceneLoadingScreen.IsChangingScene || JourneyCreationLoadingView.IsLoading)
        {
            _rightPressActive = false;
            GetViewport().SetInputAsHandled();
            return;
        }
        if (!is_gameplay_scene())
        {
            _rightPressActive = false;
            return;
        }
        if (GetTree().GetFirstNodeInGroup("action_transition") is ActionTransitionPanel { IsPlaying: true })
        {
            _rightPressActive = false;
            GetViewport().SetInputAsHandled();
            return;
        }
        if (GetNodeOrNull<BattleTransitionManager>("/root/BattleTransitionManager")?.is_flow_transition_active() == true)
        {
            if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape } ||
                @event is InputEventMouseButton { ButtonIndex: MouseButton.Right })
            {
                _rightPressActive = false;
                GetViewport().SetInputAsHandled();
            }
            return;
        }

        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            handle_escape();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (@event is InputEventMouseMotion motion && _rightPressActive &&
            motion.Position.DistanceTo(_rightPressPosition) > RightClickDragThreshold)
            _rightPressDragged = true;

        if (@event is not InputEventMouseButton mouse || mouse.ButtonIndex != MouseButton.Right) return;
        if (mouse.Pressed)
        {
            _rightPressActive = true;
            _rightPressDragged = false;
            _rightPressPosition = mouse.Position;
            GetViewport().SetInputAsHandled();
            return;
        }
        if (!_rightPressActive) return;
        _rightPressActive = false;
        _rightPressDragged |= mouse.Position.DistanceTo(_rightPressPosition) > RightClickDragThreshold;
        if (!_rightPressDragged) handle_right_click_back();
        GetViewport().SetInputAsHandled();
    }

    public override void _Process(double delta)
    {
        if (_rightPressActive && !Input.IsMouseButtonPressed(MouseButton.Right))
            _rightPressActive = false;
    }

    private bool is_gameplay_scene() => GetTree().CurrentScene?.IsInGroup("gameplay_scene") == true;

    private void handle_escape()
    {
        if (_settingsPanel is not null)
        {
            _settingsPanel.ClosePanel();
            return;
        }
        if (_homeConfirmationOpen)
        {
            build_pause_menu();
            return;
        }
        if (_pauseMenuOpen)
        {
            close_pause_menu();
            return;
        }
        open_pause_menu();
    }

    private void handle_right_click_back()
    {
        if (_settingsPanel is not null)
        {
            _settingsPanel.ClosePanel();
            return;
        }
        if (_homeConfirmationOpen)
        {
            build_pause_menu();
            return;
        }
        if (_pauseMenuOpen)
        {
            close_pause_menu();
            return;
        }
        var main = GetTree().GetFirstNodeInGroup("main_scene") as MainScene;
        if (main?.try_close_topmost_popup_window() == true) return;
        if (GetNodeOrNull<BattleTransitionManager>("/root/BattleTransitionManager") is { } transition && transition.has_open_flow_view())
        {
            transition.try_return_from_open_flow();
            return;
        }
        main?.try_return_to_previous_page();
    }

    private void open_pause_menu()
    {
        HoverInfoManager.HideAll();
        _pausedBeforeMenu = GetTree().Paused;
        _pauseMenuOpen = true;
        GetTree().Paused = true;
        ensure_menu_layer();
        _root!.Show();
        _menuPanel!.Show();
        build_pause_menu();
    }

    private void close_pause_menu()
    {
        if (!_pauseMenuOpen) return;
        _pauseMenuOpen = false;
        _homeConfirmationOpen = false;
        _settingsPanel?.ClosePanel();
        _settingsPanel = null;
        _root?.Hide();
        GetTree().Paused = _pausedBeforeMenu;
    }

    private void ensure_menu_layer()
    {
        if (_layer is not null && GodotObject.IsInstanceValid(_layer)) return;
        _layer = new CanvasLayer { Name = "GameplayPauseMenuLayer", Layer = 200, ProcessMode = ProcessModeEnum.Always };
        GetTree().Root.AddChild(_layer);
        _root = new Control { Name = "GameplayPauseMenu", MouseFilter = Control.MouseFilterEnum.Stop, ProcessMode = ProcessModeEnum.Always };
        _root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _layer.AddChild(_root);
        var veil = new ColorRect { Color = new Color(0.015f, 0.025f, 0.02f, 0.68f), MouseFilter = Control.MouseFilterEnum.Stop };
        veil.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(veil);
        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(center);
        _menuPanel = new PanelContainer { Name = "MenuPanel", CustomMinimumSize = new Vector2(400, 0), MouseFilter = Control.MouseFilterEnum.Stop };
        _menuPanel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("eee9d8"), BorderColor = new Color("8b7546"),
            BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
            ContentMarginLeft = 34, ContentMarginRight = 34, ContentMarginTop = 30, ContentMarginBottom = 28
        });
        center.AddChild(_menuPanel);
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 8);
        margin.AddThemeConstantOverride("margin_right", 8);
        margin.AddThemeConstantOverride("margin_top", 6);
        margin.AddThemeConstantOverride("margin_bottom", 6);
        _menuPanel.AddChild(margin);
        _menuContent = new VBoxContainer();
        _menuContent.AddThemeConstantOverride("separation", 12);
        margin.AddChild(_menuContent);
        var title = new Label { Text = "暂歇", HorizontalAlignment = HorizontalAlignment.Center };
        StartMenu.StyleSettingsText(title, 32, new Color("26352b"));
        _menuContent.AddChild(title);
        _subtitle = new Label { Text = "修行暂驻于此。", HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        StartMenu.StyleSettingsText(_subtitle, 15, new Color("5f5a4b"));
        _menuContent.AddChild(_subtitle);
        _menuContent.AddChild(StartMenu.SettingsRule());
        _root.Hide();
    }

    private void build_pause_menu()
    {
        if (_menuContent is null) return;
        _homeConfirmationOpen = false;
        _subtitle!.Text = "修行暂驻于此。";
        clear_menu_buttons();
        add_menu_button("继续", close_pause_menu);
        _saveButton = add_menu_button("保存", save_progress);
        var saveManager = GetNodeOrNull<GameSaveManager>("/root/GameSaveManager");
        var battle = GetTree().CurrentScene is BattleScene;
        var canSave = saveManager?.has_active_slot == true &&
            (!battle || GetNodeOrNull<BattleTransitionManager>("/root/BattleTransitionManager")?.has_world_progress_snapshot() == true);
        _saveButton.Disabled = !canSave;
        _settingsButton = add_menu_button("设置", open_settings);
        add_menu_button("返回首页", request_return_home);
        if (_root is not null) _root.Show();
        if (_menuPanel is not null) _menuPanel.Show();
        _settingsButton?.GrabFocus();
    }

    private void show_home_confirmation()
    {
        _homeConfirmationOpen = true;
        _subtitle!.Text = "要先保存当前进度再返回首页吗？";
        clear_menu_buttons();
        var saveManager = GetNodeOrNull<GameSaveManager>("/root/GameSaveManager");
        var isBattle = GetTree().CurrentScene is BattleScene;
        var canSave = saveManager?.has_active_slot == true &&
            (!isBattle || GetNodeOrNull<BattleTransitionManager>("/root/BattleTransitionManager")?.has_world_progress_snapshot() == true);
        if (canSave) add_menu_button("保存并返回首页", () => return_to_title(true));
        add_menu_button("不保存，返回首页", () => return_to_title(false));
        add_menu_button("取消", build_pause_menu);
    }

    private void save_progress()
    {
        var saved = false;
        if (GetTree().CurrentScene is MainScene main)
            saved = main.save_from_gameplay_menu();
        else if (GetTree().CurrentScene is BattleScene)
            saved = GetNodeOrNull<BattleTransitionManager>("/root/BattleTransitionManager")?.save_world_progress_snapshot() == true;
        _subtitle!.Text = saved ? "进度已保存。" : "保存失败；请确认当前行动已经结算。";
    }

    private void request_return_home()
    {
        var hasUnsaved = GetTree().CurrentScene is MainScene main && main.has_unsaved_progress;
        hasUnsaved |= GetTree().CurrentScene is BattleScene;
        if (hasUnsaved) show_home_confirmation();
        else return_to_title(false);
    }

    private async void return_to_title(bool saveFirst)
    {
        if (SceneLoadingScreen.IsChangingScene) return;
        if (GetTree().CurrentScene is MainScene main && !main.prepare_return_to_title_from_menu(saveFirst))
        {
            _subtitle!.Text = "保存失败；当前进度仍在本次修行中。";
            build_return_home_retry(saveFirst);
            return;
        }
        if (GetTree().CurrentScene is BattleScene)
        {
            var transition = GetNodeOrNull<BattleTransitionManager>("/root/BattleTransitionManager");
            if (saveFirst && transition?.save_world_progress_snapshot() != true)
            {
                _subtitle!.Text = "战前进度未能保存，仍可选择不保存返回。";
                build_return_home_retry(false);
                return;
            }
        }
        _pauseMenuOpen = false;
        _homeConfirmationOpen = false;
        _root?.Hide();
        var error = await SceneLoadingScreen.ChangeSceneAsync(GetTree(), StartMenuPath, "返回首页");
        if (error == Error.Ok)
        {
            GetTree().Paused = false;
            GetNodeOrNull<BattleTransitionManager>("/root/BattleTransitionManager")?.abandon_battle_for_title();
            _root?.QueueFree();
            _layer?.QueueFree();
            _root = null;
            _layer = null;
            _menuPanel = null;
            _menuContent = null;
            _subtitle = null;
            return;
        }
        if (GetTree().CurrentScene is MainScene currentMain) currentMain.cancel_return_to_title_from_menu();
        _pauseMenuOpen = true;
        GetTree().Paused = true;
        _root?.Show();
        _subtitle!.Text = $"暂时无法返回首页：{error}";
        build_pause_menu();
    }

    private void build_return_home_retry(bool allowSave)
    {
        _homeConfirmationOpen = true;
        clear_menu_buttons();
        if (allowSave) add_menu_button("再试一次保存", () => return_to_title(true));
        add_menu_button("不保存，返回首页", () => return_to_title(false));
        add_menu_button("取消", build_pause_menu);
    }

    private void open_settings()
    {
        if (_settingsPanel is not null) return;
        _menuPanel?.Hide();
        _settingsPanel = new GameSettingsPanel { Name = "GameplaySettingsPanel" };
        _settingsPanel.Closed += on_settings_closed;
        _layer?.AddChild(_settingsPanel);
    }

    private void on_settings_closed()
    {
        if (_settingsPanel is not null) _settingsPanel.Closed -= on_settings_closed;
        _settingsPanel = null;
        if (_pauseMenuOpen)
        {
            _menuPanel?.Show();
            build_pause_menu();
        }
    }

    private void clear_menu_buttons()
    {
        if (_menuContent is null) return;
        foreach (var child in _menuContent.GetChildren())
            if (child is Button)
            {
                _menuContent.RemoveChild(child);
                child.QueueFree();
            }
    }

    private Button add_menu_button(string label, Action action)
    {
        var button = StartMenu.SettingsButton(label);
        button.CustomMinimumSize = new Vector2(300, 48);
        button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        button.Pressed += action;
        _menuContent!.AddChild(button);
        return button;
    }
}
