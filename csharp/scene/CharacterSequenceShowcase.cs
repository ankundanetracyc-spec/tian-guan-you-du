using Godot;

/// <summary>游戏内的 Spine 骨骼动画测试入口，便于确认待机、攻击、受击与跪地死亡。</summary>
public partial class CharacterSequenceShowcase : Node2D
{
    private const string SpineResourcePath = BattlePaperDoll.CharacterSpineResourcePath;
    private const string FemaleSpineResourcePath = BattlePaperDoll.FemaleSpineResourcePath;
    private const float SpineFramesPerSecond = 60f;

    private Sprite2D? _character;
    private Node2D? _spineCharacter;
    private Label? _frameLabel;
    private bool _usingSpine;
    private bool _spinePaused;
    private string _spineAnimation = "idle";
    private bool _female = true;
    private Button? _maleButton;
    private Button? _femaleButton;

    public override void _Ready()
    {
        _character = GetNodeOrNull<Sprite2D>("Character");
        _frameLabel = GetNodeOrNull<Label>("UILayer/FrameLabel");
        var ui = GetNode<CanvasLayer>("UILayer");
        _maleButton = new Button { Text = "男修", Position = new Vector2(40, 125), Size = new Vector2(110, 44) };
        _femaleButton = new Button { Text = "女修", Position = new Vector2(166, 125), Size = new Vector2(110, 44) };
        _maleButton.Pressed += () => ShowCharacter(false);
        _femaleButton.Pressed += () => ShowCharacter(true);
        ui.AddChild(_maleButton);
        ui.AddChild(_femaleButton);
        var actions = new HBoxContainer { Position = new Vector2(40, 188) };
        foreach (var (text, animation, loop) in new[] { ("待机", "idle", true), ("攻击", "attack", false), ("受击", "hurt", false), ("跪地", "death_kneel", false) })
        {
            var button = new Button { Text = text, CustomMinimumSize = new Vector2(96, 44) };
            button.Pressed += () => PlaySpineAnimation(animation, loop);
            actions.AddChild(button);
        }
        ui.AddChild(actions);
        ShowCharacter(_female);
    }

    private void ShowCharacter(bool female)
    {
        _female = female;
        if (_maleButton is not null) _maleButton.Disabled = !female;
        if (_femaleButton is not null) _femaleButton.Disabled = female;
        if (_spineCharacter is not null && IsInstanceValid(_spineCharacter))
        {
            RemoveChild(_spineCharacter);
            _spineCharacter.QueueFree();
        }
        _spineCharacter = null;
        _spinePaused = false;
        _usingSpine = TryCreateSpineCharacter(out var failureReason);
        if (_usingSpine)
        {
            _character?.Hide();
            PlaySpineAnimation("idle", true);
        }
        else
        {
            if (_character is not null)
            {
                _character.Texture = ResourceLoader.Load<Texture2D>(_female ? BattlePaperDoll.FemaleStaticPortraitPath : BattlePaperDoll.DefaultPortraitPath);
                _character.Modulate = Colors.White;
                _character.Scale = new Vector2(_female ? .42f : .48f, _female ? .42f : .48f);
                _character.Position = new Vector2(960, _female ? 545 : 495);
                _character.Show();
            }
            if (_frameLabel is not null)
                _frameLabel.Text = $"{(_female ? "女修" : "男修")} · 静态预览，当前运行器不支持骨骼动作";
            GD.PushWarning($"角色动作展示使用静态回退：{failureReason}");
        }
    }

    public override void _Process(double delta)
    {
        if (_usingSpine && GodotObject.IsInstanceValid(_spineCharacter))
        {
            if (!_spinePaused && _spineAnimation != "idle" && _spineAnimation != "death_kneel" && !_spineCharacter!.Call("is_playing", 0).AsBool())
                PlaySpineAnimation("idle", true);
            UpdateSpineLabel();
            return;
        }
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (input.IsActionPressed("ui_cancel"))
        {
            GetTree().ChangeSceneToFile("res://tscn/scene/StartMenu.tscn");
            return;
        }
        if (input is InputEventKey choiceKey && choiceKey.Pressed && !choiceKey.Echo)
        {
            if (choiceKey.Keycode == Key.M) { ShowCharacter(false); return; }
            if (choiceKey.Keycode == Key.F) { ShowCharacter(true); return; }
        }
        if (_usingSpine && GodotObject.IsInstanceValid(_spineCharacter))
        {
            if (input.IsActionPressed("ui_accept"))
            {
                _spinePaused = !_spinePaused;
                _spineCharacter!.Call("set_active", !_spinePaused);
                UpdateSpineLabel();
            }
            if (input is InputEventKey spineKey && spineKey.Pressed && !spineKey.Echo)
            {
                if (spineKey.Keycode == Key.A) PlaySpineAnimation("attack", false);
                if (spineKey.Keycode == Key.H) PlaySpineAnimation("hurt", false);
                if (spineKey.Keycode == Key.D) PlaySpineAnimation("death_kneel", false);
            }
            if (input.IsActionPressed("ui_left")) StepSpineFrame(-1);
            if (input.IsActionPressed("ui_right")) StepSpineFrame(1);
        }
    }

    private bool TryCreateSpineCharacter(out string failureReason)
    {
        failureReason = "未知原因";
        if (!ClassDB.CanInstantiate("Spine"))
        {
            failureReason = "当前 Godot 不是 Spine 3.8 运行时";
            return false;
        }

        var resourcePath = _female ? FemaleSpineResourcePath : SpineResourcePath;
        var resource = ResourceLoader.Load<Resource>(resourcePath);
        if (resource is null)
        {
            failureReason = $"Spine 资源加载失败：{resourcePath}";
            return false;
        }

        if (ClassDB.Instantiate("Spine").AsGodotObject() is not Node2D spine)
        {
            failureReason = "Spine 节点创建失败";
            return false;
        }

        spine.Name = _female ? "FemaleShadowSpine" : "C1SpineCharacter";
        spine.Position = new Vector2(960, _female ? 545 : 495);
        // Match the v6 showcase height while using v7's 1024x1536 canvas.
        spine.Scale = new Vector2(_female ? .42f : .48f, _female ? .42f : .48f);
        spine.Modulate = Colors.White;
        spine.ZIndex = 0;
        AddChild(spine);
        spine.Call("set_resource", resource);
        spine.Call("set_flip_x", false);
        spine.Call("set_active", true);
        foreach (var animation in new[] { "idle", "attack", "hurt", "death_kneel" })
        {
            if (spine.Call("get_animation_length", animation).AsSingle() <= 0f)
            {
                spine.QueueFree();
                failureReason = $"{resourcePath} 缺少 {animation} 动画";
                return false;
            }
        }
        _spineCharacter = spine;
        return true;
    }

    private void PlaySpineAnimation(string animation, bool loop)
    {
        if (_spineCharacter is null || !GodotObject.IsInstanceValid(_spineCharacter)) return;
        _spineAnimation = animation;
        _spinePaused = false;
        _spineCharacter.Call("set_active", true);
        _spineCharacter.Call("play", animation, 1.0f, loop, 0, 0.0f);
        UpdateSpineLabel();
    }

    private void StepSpineFrame(int direction)
    {
        if (_spineCharacter is null || !GodotObject.IsInstanceValid(_spineCharacter)) return;
        var length = Mathf.Max(.0001f, _spineCharacter.Call("get_animation_length", _spineAnimation).AsSingle());
        var frameCount = Mathf.Max(1, Mathf.CeilToInt(length * SpineFramesPerSecond));
        var currentFrame = Mathf.Clamp(Mathf.FloorToInt(_spineCharacter.Call("tell", 0).AsSingle() * SpineFramesPerSecond + .5f), 0, frameCount - 1);
        var nextFrame = (currentFrame + direction) % frameCount;
        if (nextFrame < 0) nextFrame += frameCount;
        _spineCharacter.Call("seek", 0, nextFrame / SpineFramesPerSecond);
        _spineCharacter.Call("_animation_process", 0.0);
        _spinePaused = true;
        _spineCharacter.Call("set_active", false);
        UpdateSpineLabel();
    }

    private void UpdateSpineLabel()
    {
        if (_frameLabel is null || _spineCharacter is null || !GodotObject.IsInstanceValid(_spineCharacter)) return;
        var length = Mathf.Max(.0001f, _spineCharacter.Call("get_animation_length", _spineAnimation).AsSingle());
        var frameCount = Mathf.Max(1, Mathf.CeilToInt(length * SpineFramesPerSecond));
        var frame = Mathf.Clamp(Mathf.FloorToInt(_spineCharacter.Call("tell", 0).AsSingle() * SpineFramesPerSecond + .5f), 0, frameCount - 1);
        var action = _spineAnimation switch
        {
            "attack" => "攻击",
            "hurt" => "受击",
            "death_kneel" => "死亡",
            _ => "待机"
        };
        _frameLabel.Text = $"{(_female ? "女修" : "男修")} · {action} · 帧 {frame + 1:D2} / {frameCount:D2}" + (_spinePaused ? "  · 已暂停" : "  · 播放中");
    }
}
