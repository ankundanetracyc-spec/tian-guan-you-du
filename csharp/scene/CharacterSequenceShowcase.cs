using Godot;
using System.Globalization;
using System.Text.Json.Nodes;

/// <summary>游戏内的 Spine 骨骼动画测试入口，便于确认待机、施法、攻击、受击与跪地死亡。</summary>
public partial class CharacterSequenceShowcase : Node2D
{
    private const string SpineResourcePath = BattlePaperDoll.CharacterSpineResourcePath;
    private const string FemaleSpineResourcePath = BattlePaperDoll.FemaleSpineResourcePath;
    private const float SpineFramesPerSecond = 60f;
    private const float SkeletonPreviewScale = .42f;
    private const float SkeletonPelvisScreenY = 630f;

    private static float GetPelvisDrop(bool female) => female ? 60f : 70f;

    private Polygon2D? _shade;
    private Node2D? _spineCharacter;
    private Label? _frameLabel;
    private bool _usingSpine;
    private bool _usingProceduralRig;
    private bool _spinePaused;
    private string _spineAnimation = "idle";
    private string _selectedReadyPose = "idle";
    private readonly Dictionary<string, Button> _readyPoseButtons = new();
    private bool _female = true;
    private bool _previewLoop;
    private float _previewTime;
    private float _previewBlendTime;
    private float _previewBlendDuration;
    private Dictionary<string, PreviewBoneSetup> _previewRigBones = new();
    private JsonObject? _previewAnimations;
    private Dictionary<string, float> _previewAnimationLengths = new();
    private Dictionary<string, PreviewWorldBone> _previewBlendStart = new();
    private Button? _maleButton;
    private Button? _femaleButton;

    private readonly record struct PreviewBoneSetup(string Name, string Parent, Vector2 Position, float Rotation);
    private readonly record struct PreviewWorldBone(Vector2 Position, float Rotation);

    private static readonly (string Parent, string Child, Color Color)[] PreviewBones =
    [
        ("hips", "lower_abdomen", new Color("#a8d8a0")),
        ("lower_abdomen", "waist", new Color("#a8d8a0")),
        ("waist", "spine_mid", new Color("#a8d8a0")),
        ("spine_mid", "torso", new Color("#a8d8a0")),
        ("torso", "ribcage", new Color("#a8d8a0")),
        ("ribcage", "chest", new Color("#a8d8a0")),
        ("chest", "neck", new Color("#f3d27a")),
        ("neck", "head", new Color("#f3d27a")),
        ("chest", "scapula_far", new Color("#82b6dd")),
        ("scapula_far", "clavicle_far", new Color("#82b6dd")),
        ("clavicle_far", "arm_far", new Color("#82b6dd")),
        ("arm_far", "forearm_far", new Color("#82b6dd")),
        ("forearm_far", "hand_far", new Color("#82b6dd")),
        ("hand_far", "palm_far", new Color("#82b6dd")),
        ("palm_far", "thumb_far", new Color("#82b6dd")),
        ("thumb_far", "thumb_far_mid", new Color("#82b6dd")),
        ("thumb_far_mid", "thumb_far_tip", new Color("#82b6dd")),
        ("palm_far", "finger_far", new Color("#82b6dd")),
        ("finger_far", "finger_far_mid", new Color("#82b6dd")),
        ("finger_far_mid", "finger_far_tip", new Color("#82b6dd")),
        ("palm_far", "middle_far", new Color("#82b6dd")),
        ("middle_far", "middle_far_mid", new Color("#82b6dd")),
        ("middle_far_mid", "middle_far_tip", new Color("#82b6dd")),
        ("palm_far", "ring_far", new Color("#82b6dd")),
        ("ring_far", "ring_far_mid", new Color("#82b6dd")),
        ("ring_far_mid", "ring_far_tip", new Color("#82b6dd")),
        ("palm_far", "pinky_far", new Color("#82b6dd")),
        ("pinky_far", "pinky_far_mid", new Color("#82b6dd")),
        ("pinky_far_mid", "pinky_far_tip", new Color("#82b6dd")),
        ("chest", "scapula_near", new Color("#f2a66d")),
        ("scapula_near", "clavicle_near", new Color("#f2a66d")),
        ("clavicle_near", "arm_near", new Color("#f2a66d")),
        ("arm_near", "forearm_near", new Color("#f2a66d")),
        ("forearm_near", "hand_near", new Color("#f2a66d")),
        ("hand_near", "palm_near", new Color("#f2a66d")),
        ("palm_near", "thumb_near", new Color("#f2a66d")),
        ("thumb_near", "thumb_near_mid", new Color("#f2a66d")),
        ("thumb_near_mid", "thumb_near_tip", new Color("#f2a66d")),
        ("palm_near", "finger_near", new Color("#f2a66d")),
        ("finger_near", "finger_near_mid", new Color("#f2a66d")),
        ("finger_near_mid", "finger_near_tip", new Color("#f2a66d")),
        ("palm_near", "middle_near", new Color("#f2a66d")),
        ("middle_near", "middle_near_mid", new Color("#f2a66d")),
        ("middle_near_mid", "middle_near_tip", new Color("#f2a66d")),
        ("palm_near", "ring_near", new Color("#f2a66d")),
        ("ring_near", "ring_near_mid", new Color("#f2a66d")),
        ("ring_near_mid", "ring_near_tip", new Color("#f2a66d")),
        ("palm_near", "pinky_near", new Color("#f2a66d")),
        ("pinky_near", "pinky_near_mid", new Color("#f2a66d")),
        ("pinky_near_mid", "pinky_near_tip", new Color("#f2a66d")),
        ("hips", "thigh_far", new Color("#bda5da")),
        ("thigh_far", "shin_far", new Color("#bda5da")),
        ("shin_far", "foot_far", new Color("#bda5da")),
        ("foot_far", "toe_far", new Color("#bda5da")),
        ("hips", "thigh_near", new Color("#d5b5cf")),
        ("thigh_near", "shin_near", new Color("#d5b5cf")),
        ("shin_near", "foot_near", new Color("#d5b5cf")),
        ("foot_near", "toe_near", new Color("#d5b5cf"))
    ];
    private static readonly string[] PreviewBoneNames =
    [
        "root", "hips", "lower_abdomen", "waist", "spine_mid", "torso", "ribcage", "chest", "neck", "head",
        "scapula_far", "clavicle_far", "arm_far", "upper_arm_deform_far", "forearm_far", "forearm_deform_far", "hand_far", "palm_far",
        "thumb_far", "thumb_far_mid", "thumb_far_tip", "finger_far", "finger_far_mid", "finger_far_tip",
        "middle_far", "middle_far_mid", "middle_far_tip", "ring_far", "ring_far_mid", "ring_far_tip", "pinky_far", "pinky_far_mid", "pinky_far_tip",
        "scapula_near", "clavicle_near", "arm_near", "upper_arm_deform_near", "forearm_near", "forearm_deform_near", "hand_near", "palm_near",
        "thumb_near", "thumb_near_mid", "thumb_near_tip", "finger_near", "finger_near_mid", "finger_near_tip",
        "middle_near", "middle_near_mid", "middle_near_tip", "ring_near", "ring_near_mid", "ring_near_tip", "pinky_near", "pinky_near_mid", "pinky_near_tip",
        "thigh_far", "shin_far", "foot_far", "toe_far", "thigh_near", "shin_near", "foot_near", "toe_near"
    ];

    public override void _Ready()
    {
        _shade = GetNodeOrNull<Polygon2D>("Shade");
        if (_shade is not null)
        {
            _shade.Color = new Color("#202a2b");
            _shade.ShowBehindParent = true;
        }
        _frameLabel = GetNodeOrNull<Label>("UILayer/FrameLabel");
        var ui = GetNode<CanvasLayer>("UILayer");
        _maleButton = new Button { Text = "男修", Position = new Vector2(40, 125), Size = new Vector2(110, 44) };
        _femaleButton = new Button { Text = "女修", Position = new Vector2(166, 125), Size = new Vector2(110, 44) };
        _maleButton.Pressed += () => ShowCharacter(false);
        _femaleButton.Pressed += () => ShowCharacter(true);
        ui.AddChild(_maleButton);
        ui.AddChild(_femaleButton);
        var poses = new HBoxContainer { Position = new Vector2(40, 188) };
        foreach (var (text, animation) in new[]
                 { ("防守待机", "idle"), ("施法蓄势", "ready_cast"), ("剑指引剑", "ready_sword") })
        {
            var button = new Button { Text = text, ToggleMode = true, CustomMinimumSize = new Vector2(126, 44) };
            button.Pressed += () => SelectReadyPose(animation);
            _readyPoseButtons[animation] = button;
            poses.AddChild(button);
        }
        ui.AddChild(poses);
        var actions = new HBoxContainer { Position = new Vector2(40, 245) };
        foreach (var (text, animation, loop) in new[]
                 {
                     ("施法", "cast", false), ("转身摆击", "attack", false),
                     ("上挑掌", "attack_rising", false), ("跃身踢击", "attack_leap", false),
                     ("休整", "rest", false), ("吐纳", "breath", false),
                     ("受击", "hurt", false), ("跪地", "death_kneel", false)
                 })
        {
            var button = new Button { Text = text, CustomMinimumSize = new Vector2(102, 44) };
            button.Pressed += () => PlaySpineAnimation(animation, loop);
            actions.AddChild(button);
        }
        ui.AddChild(actions);
        ShowCharacter(_female);
    }

    private void ShowCharacter(bool female)
    {
        _female = female;
        foreach (var (name, button) in _readyPoseButtons) button.SetPressedNoSignal(name == _selectedReadyPose);
        if (_maleButton is not null) _maleButton.Disabled = !female;
        if (_femaleButton is not null) _femaleButton.Disabled = female;
        if (_spineCharacter is not null && IsInstanceValid(_spineCharacter))
        {
            RemoveChild(_spineCharacter);
            _spineCharacter.QueueFree();
        }
        _spineCharacter = null;
        _spinePaused = false;
        _spineAnimation = _selectedReadyPose;
        _previewTime = 0f;
        _previewBlendDuration = 0f;
        _previewBlendStart.Clear();
        _usingProceduralRig = false;
        _usingSpine = TryCreateSpineCharacter(out var failureReason);
        if (_usingSpine)
        {
            PlaySpineAnimation(_selectedReadyPose, true);
        }
        else if (TryLoadProceduralRig(out var rigFailureReason))
        {
            _usingProceduralRig = true;
            PlaySpineAnimation(_selectedReadyPose, true);
        }
        else
        {
            if (_frameLabel is not null)
                _frameLabel.Text = $"{(_female ? "女修" : "男修")} · 骨架数据不可用：{rigFailureReason}";
            GD.PushWarning($"骨架动作展示不可用：Spine={failureReason}; Rig={rigFailureReason}");
        }
    }

    public override void _Process(double delta)
    {
        if (_usingSpine && GodotObject.IsInstanceValid(_spineCharacter))
        {
            QueueRedraw();
            if (!_spinePaused && !IsReadyPose(_spineAnimation) && _spineAnimation != "death_kneel" && !_spineCharacter!.Call("is_playing", 0).AsBool())
                PlaySpineAnimation(_selectedReadyPose, true);
            UpdateSpineLabel();
            return;
        }
        if (_usingProceduralRig)
        {
            if (!_spinePaused)
            {
                var length = GetPreviewAnimationLength(_spineAnimation);
                _previewTime += (float)delta;
                if (_previewBlendDuration > 0f)
                {
                    _previewBlendTime += (float)delta;
                    if (_previewBlendTime >= _previewBlendDuration)
                    {
                        _previewBlendDuration = 0f;
                        _previewBlendStart.Clear();
                    }
                }
                if (length > 0f && _previewTime >= length)
                {
                    if (_previewLoop) _previewTime %= length;
                    else if (_spineAnimation != "death_kneel") PlaySpineAnimation(_selectedReadyPose, true);
                    else _previewTime = length;
                }
            }
            QueueRedraw();
            UpdateSpineLabel();
        }
    }

    public override void _Draw()
    {
        if (_usingProceduralRig)
        {
            DrawProceduralSkeleton();
            return;
        }
        if (!_usingSpine || _spineCharacter is null || !GodotObject.IsInstanceValid(_spineCharacter)) return;

        var positions = new Dictionary<string, Vector2>();
        foreach (var boneName in PreviewBoneNames)
        {
            var bone = _spineCharacter.Call("get_bone", boneName).AsGodotDictionary();
            if (!bone.ContainsKey("worldX") || !bone.ContainsKey("worldY") || !bone.ContainsKey("m00") || !bone.ContainsKey("m10")) continue;
            var spinePoint = new Vector2(bone["worldX"].AsSingle(), -bone["worldY"].AsSingle());
            positions[boneName] = ToLocal(_spineCharacter.ToGlobal(spinePoint));
        }

        DrawSkeleton(positions);
    }

    private void DrawSkeleton(Dictionary<string, Vector2> positions)
    {
        var floorY = SkeletonPelvisScreenY + 770f * SkeletonPreviewScale;
        DrawLine(new Vector2(310, floorY), new Vector2(1610, floorY), new Color("#647272", .62f), 1.4f, true);
        if (positions.TryGetValue("head", out var head) && positions.TryGetValue("neck", out var neck))
        {
            var up = (head - neck).Normalized();
            var right = new Vector2(-up.Y, up.X);
            var outline = Enumerable.Range(0, 33).Select(index =>
            {
                var angle = index * Mathf.Tau / 32f;
                return head + right * Mathf.Cos(angle) * 27f + up * Mathf.Sin(angle) * 38f;
            }).ToArray();
            DrawPolyline(outline, new Color("#f3d27a", .7f), 1.4f, true);
        }
        if (positions.TryGetValue("hips", out var pelvis))
        {
            DrawLine(pelvis + new Vector2(-9, 0), pelvis + new Vector2(9, 0), new Color("#fff0b3", .8f), 1.4f, true);
            DrawLine(pelvis + new Vector2(0, -9), pelvis + new Vector2(0, 9), new Color("#fff0b3", .8f), 1.4f, true);
        }
        foreach (var (parent, child, color) in PreviewBones)
        {
            if (!positions.TryGetValue(parent, out var from) || !positions.TryGetValue(child, out var to)) continue;
            bool fingerBone = child.StartsWith("thumb_", StringComparison.Ordinal) ||
                              child.StartsWith("finger_", StringComparison.Ordinal) ||
                              child.StartsWith("middle_", StringComparison.Ordinal) ||
                              child.StartsWith("ring_", StringComparison.Ordinal) ||
                              child.StartsWith("pinky_", StringComparison.Ordinal);
            var lineWidth = fingerBone ? 1.4f : 2.7f;
            DrawLine(from, to, new Color("#182b2d"), lineWidth + 2.2f, true);
            DrawLine(from, to, color, lineWidth, true);
        }

        foreach (var (name, position) in positions)
        {
            if (name == "root") continue;
            bool fingerBone = name.StartsWith("thumb_", StringComparison.Ordinal) ||
                              name.StartsWith("finger_", StringComparison.Ordinal) ||
                              name.StartsWith("middle_", StringComparison.Ordinal) ||
                              name.StartsWith("ring_", StringComparison.Ordinal) ||
                              name.StartsWith("pinky_", StringComparison.Ordinal);
            bool deformationHelper = name.StartsWith("upper_arm_deform_", StringComparison.Ordinal) ||
                                     name.StartsWith("forearm_deform_", StringComparison.Ordinal);
            if (fingerBone || deformationHelper) continue;
            float jointSize = name.Contains("_mid", StringComparison.Ordinal) ? 2.3f : 3f;
            DrawCircle(position, jointSize + 1.6f, new Color("#182b2d"));
            DrawCircle(position, jointSize, deformationHelper ? new Color("#a7b3b2") : name == "root" ? new Color("#fff0b3") : new Color("#f5f0d8"));
            var label = name switch
            {
                "hips" => "骨盆",
                "head" => "头",
                "clavicle_near" => "近肩",
                "forearm_near" => "近肘",
                "hand_near" => "近腕",
                "shin_near" => "近膝",
                "foot_near" => "近踝",
                _ => string.Empty
            };
            if (label.Length > 0)
            {
                var offset = name switch
                {
                    "head" => new Vector2(35f, -25f),
                    "chest" => new Vector2(35f, -8f),
                    "ribcage" => new Vector2(-42f, -7f),
                    "spine_mid" => new Vector2(-42f, 10f),
                    "hips" => new Vector2(12f, 17f),
                    "lower_abdomen" => new Vector2(-44f, 22f),
                    "clavicle_near" => new Vector2(42f, -20f),
                    "forearm_near" => new Vector2(48f, 8f),
                    "hand_near" => new Vector2(50f, 36f),
                    "shin_near" => new Vector2(12f, 14f),
                    "foot_near" => new Vector2(16f, 16f),
                    _ => new Vector2(10f, 12f)
                };
                DrawString(ThemeDB.FallbackFont, position + offset + new Vector2(1, 1), label, HorizontalAlignment.Left, -1f, 12, new Color("#182b2d"));
                DrawString(ThemeDB.FallbackFont, position + offset, label, HorizontalAlignment.Left, -1f, 12, new Color("#fff7d5"));
            }
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
            if (choiceKey.Keycode == Key.Key1) { SelectReadyPose("idle"); return; }
            if (choiceKey.Keycode == Key.Key2) { SelectReadyPose("ready_cast"); return; }
            if (choiceKey.Keycode == Key.Key3) { SelectReadyPose("ready_sword"); return; }
        }
        if ((_usingSpine && GodotObject.IsInstanceValid(_spineCharacter)) || _usingProceduralRig)
        {
            if (input.IsActionPressed("ui_accept"))
            {
                _spinePaused = !_spinePaused;
                if (_usingSpine && _spineCharacter is not null && GodotObject.IsInstanceValid(_spineCharacter))
                    _spineCharacter.Call("set_active", !_spinePaused);
                UpdateSpineLabel();
            }
            if (input is InputEventKey spineKey && spineKey.Pressed && !spineKey.Echo)
            {
                if (spineKey.Keycode == Key.C) PlaySpineAnimation("cast", false);
                if (spineKey.Keycode == Key.A) PlaySpineAnimation("attack", false);
                if (spineKey.Keycode == Key.R) PlaySpineAnimation("attack_rising", false);
                if (spineKey.Keycode == Key.L) PlaySpineAnimation("attack_leap", false);
                if (spineKey.Keycode == Key.S) PlaySpineAnimation("rest", false);
                if (spineKey.Keycode == Key.T) PlaySpineAnimation("breath", false);
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
        spine.Position = GetSkeletonRootPosition();
        spine.Scale = new Vector2(SkeletonPreviewScale, SkeletonPreviewScale);
        // The preview draws only the runtime's bone transforms.
        spine.Modulate = new Color(1f, 1f, 1f, 0f);
        spine.ZIndex = 0;
        AddChild(spine);
        spine.Call("set_resource", resource);
        spine.Call("set_flip_x", false);
        spine.Call("set_active", true);
        foreach (var animation in new[] { "idle", "ready_cast", "ready_sword", "cast", "attack", "attack_rising", "attack_leap", "rest", "breath", "hurt", "death_kneel" })
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

    private Vector2 GetSkeletonRootPosition()
    {
        const float scale = SkeletonPreviewScale;
        var pelvis = _female ? new Vector2(126f, 176f) : new Vector2(123f, 168f);
        return new Vector2(960f - pelvis.X * scale,
            SkeletonPelvisScreenY + (pelvis.Y + GetPelvisDrop(_female)) * scale);
    }

    private void PlaySpineAnimation(string animation, bool loop)
    {
        if (!_usingProceduralRig && (_spineCharacter is null || !GodotObject.IsInstanceValid(_spineCharacter))) return;
        if (_spineAnimation == "death_kneel") return;
        var mixDuration = GetMixDuration(_spineAnimation, animation);
        var blendStart = _usingProceduralRig ? EvaluatePreviewLocalPose(_spineAnimation, _previewTime) : null;
        _spineAnimation = animation;
        _spinePaused = false;
        if (_usingSpine && _spineCharacter is not null && GodotObject.IsInstanceValid(_spineCharacter))
        {
            _spineCharacter.Call("set_active", true);
            _spineCharacter.Call("play", animation, 1.0f, loop, 0, mixDuration);
        }
        if (_usingProceduralRig)
        {
            _previewTime = 0f;
            _previewLoop = loop;
            _previewBlendStart = blendStart ?? new Dictionary<string, PreviewWorldBone>();
            _previewBlendTime = 0f;
            _previewBlendDuration = mixDuration;
            QueueRedraw();
        }
        UpdateSpineLabel();
    }

    private static bool IsReadyPose(string animation)
        => animation is "idle" or "ready_cast" or "ready_sword";

    private void SelectReadyPose(string animation)
    {
        if (!IsReadyPose(animation)) return;
        _selectedReadyPose = animation;
        foreach (var (name, button) in _readyPoseButtons) button.SetPressedNoSignal(name == animation);
        // A deliberate new preview after death recreates the character; death never returns automatically.
        if (_spineAnimation == "death_kneel") ShowCharacter(_female);
        else PlaySpineAnimation(animation, true);
    }

    private static float GetMixDuration(string from, string to)
    {
        if (from == to) return 0.08f;
        if (to == "hurt") return 0.10f;
        if (to == "death_kneel") return 0.16f;
        if (IsReadyPose(to)) return 0.22f;
        if (IsReadyPose(from)) return 0.18f;
        return 0.15f;
    }

    private bool TryLoadProceduralRig(out string failureReason)
    {
        failureReason = "未知原因";
        var resourcePath = _female ? FemaleSpineResourcePath : SpineResourcePath;
        try
        {
            var source = Godot.FileAccess.GetFileAsString(resourcePath);
            if (string.IsNullOrWhiteSpace(source))
            {
                failureReason = $"骨架文件为空：{resourcePath}";
                return false;
            }

            var root = JsonNode.Parse(source) as JsonObject;
            if (root?["bones"] is not JsonArray boneArray || root["animations"] is not JsonObject animations)
            {
                failureReason = $"骨架或动作段缺失：{resourcePath}";
                return false;
            }

            var bones = new Dictionary<string, PreviewBoneSetup>();
            foreach (var item in boneArray)
            {
                if (item is not JsonObject bone) continue;
                var name = ReadJsonString(bone["name"]);
                if (string.IsNullOrWhiteSpace(name)) continue;
                bones[name] = new PreviewBoneSetup(
                    name,
                    ReadJsonString(bone["parent"]),
                    new Vector2(ReadJsonFloat(bone["x"]), ReadJsonFloat(bone["y"])),
                    ReadJsonFloat(bone["rotation"]));
            }

            var missing = PreviewBoneNames.Where(name => !bones.ContainsKey(name)).ToArray();
            if (missing.Length > 0)
            {
                failureReason = $"骨架缺少预览关节：{string.Join("、", missing)}";
                return false;
            }

            _previewRigBones = bones;
            _previewAnimations = animations;
            _previewAnimationLengths.Clear();
            foreach (var (animationName, animationNode) in animations)
            {
                var maxTime = 0f;
                var boneTimelines = (animationNode as JsonObject)?["bones"] as JsonObject;
                if (boneTimelines is not null)
                {
                    foreach (var (_, boneNode) in boneTimelines)
                    {
                        if (boneNode is not JsonObject timelines) continue;
                        foreach (var (_, timelineNode) in timelines)
                        {
                            if (timelineNode is not JsonArray keys) continue;
                            foreach (var keyNode in keys)
                                if (keyNode is JsonObject key)
                                    maxTime = Mathf.Max(maxTime, ReadJsonFloat(key["time"]));
                        }
                    }
                }
                _previewAnimationLengths[animationName] = maxTime;
            }

            foreach (var animation in new[] { "idle", "ready_cast", "ready_sword", "cast", "attack", "attack_rising", "attack_leap", "rest", "breath", "hurt", "death_kneel" })
            {
                if (!_previewAnimationLengths.ContainsKey(animation))
                {
                    failureReason = $"动作段不存在：{animation}";
                    return false;
                }
            }
            return true;
        }
        catch (Exception exception)
        {
            failureReason = exception.Message;
            return false;
        }
    }

    private static string ReadJsonString(JsonNode? node)
    {
        if (node is not JsonValue value) return string.Empty;
        try { return value.GetValue<string>(); }
        catch { return value.ToString(); }
    }

    private static float ReadJsonFloat(JsonNode? node, float fallback = 0f)
    {
        if (node is null) return fallback;
        return float.TryParse(node.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }

    private float GetPreviewAnimationLength(string animation)
        => _previewAnimationLengths.TryGetValue(animation, out var length) ? length : 0f;

    private Dictionary<string, PreviewWorldBone> EvaluatePreviewLocalPose(string animation, float time)
    {
        var pose = new Dictionary<string, PreviewWorldBone>(_previewRigBones.Count);
        JsonObject? animationData = _previewAnimations?[animation] as JsonObject;
        JsonObject? boneTimelines = animationData?["bones"] as JsonObject;
        foreach (var bone in _previewRigBones.Values)
        {
            var setupPosition = bone.Position;
            var localRotation = bone.Rotation;
            JsonObject? timelines = boneTimelines?[bone.Name] as JsonObject;
            if (timelines is not null)
            {
                var translateKeys = timelines["translate"] as JsonArray;
                setupPosition += new Vector2(
                    EvaluateTimeline(translateKeys, time, "x"),
                    EvaluateTimeline(translateKeys, time, "y"));
                localRotation += EvaluateTimeline(timelines["rotate"] as JsonArray, time, "angle");
            }

            pose[bone.Name] = new PreviewWorldBone(setupPosition, localRotation);
        }
        if (_previewBlendDuration > 0f && _previewBlendStart.Count > 0)
        {
            var blend = Mathf.Clamp(_previewBlendTime / _previewBlendDuration, 0f, 1f);
            foreach (var name in pose.Keys.ToArray())
            {
                if (!_previewBlendStart.TryGetValue(name, out var start)) continue;
                var target = pose[name];
                pose[name] = new PreviewWorldBone(start.Position.Lerp(target.Position, blend),
                    LerpDegrees(start.Rotation, target.Rotation, blend));
            }
        }
        return pose;
    }

    private Dictionary<string, PreviewWorldBone> EvaluatePreviewPose(string animation, float time)
    {
        var local = EvaluatePreviewLocalPose(animation, time);
        var world = new Dictionary<string, PreviewWorldBone>(local.Count);
        foreach (var bone in _previewRigBones.Values)
        {
            var transform = local[bone.Name];
            if (string.IsNullOrEmpty(bone.Parent) || !world.TryGetValue(bone.Parent, out var parent))
                world[bone.Name] = transform;
            else
                world[bone.Name] = new PreviewWorldBone(
                    parent.Position + transform.Position.Rotated(Mathf.DegToRad(parent.Rotation)),
                    parent.Rotation + transform.Rotation);
        }
        return world;
    }

    private static float EvaluateTimeline(JsonArray? keys, float time, string property)
    {
        if (keys is null || keys.Count == 0) return 0f;
        var first = keys[0] as JsonObject;
        if (first is null) return 0f;
        var firstTime = ReadJsonFloat(first["time"]);
        if (time <= firstTime) return ReadJsonFloat(first[property]);

        for (var index = 0; index < keys.Count - 1; index++)
        {
            if (keys[index] is not JsonObject from || keys[index + 1] is not JsonObject to) continue;
            var fromTime = ReadJsonFloat(from["time"]);
            var toTime = ReadJsonFloat(to["time"]);
            if (time > toTime) continue;
            var duration = Mathf.Max(0.0001f, toTime - fromTime);
            var amount = Mathf.Clamp((time - fromTime) / duration, 0f, 1f);
            if (from["curve"] is JsonArray curve && curve.Count >= 4)
                amount = EvaluateBezierTime(curve, amount);
            else if (ReadJsonString(from["curve"]) == "stepped")
                amount = 0f;
            var startValue = ReadJsonFloat(from[property]);
            var endValue = ReadJsonFloat(to[property], startValue);
            return Mathf.Lerp(startValue, endValue, amount);
        }

        return ReadJsonFloat((keys[^1] as JsonObject)?[property]);
    }

    private static float EvaluateBezierTime(JsonArray curve, float progress)
    {
        var controlX1 = ReadJsonFloat(curve[0]);
        var controlY1 = ReadJsonFloat(curve[1]);
        var controlX2 = ReadJsonFloat(curve[2]);
        var controlY2 = ReadJsonFloat(curve[3]);
        var lower = 0f;
        var upper = 1f;
        var parameter = progress;
        for (var iteration = 0; iteration < 12; iteration++)
        {
            var x = CubicBezier(parameter, controlX1, controlX2);
            if (x < progress) lower = parameter;
            else upper = parameter;
            parameter = (lower + upper) * .5f;
        }
        return CubicBezier(parameter, controlY1, controlY2);
    }

    private static float CubicBezier(float t, float control1, float control2)
    {
        var inverse = 1f - t;
        return 3f * inverse * inverse * t * control1 + 3f * inverse * t * t * control2 + t * t * t;
    }

    private static float LerpDegrees(float from, float to, float weight)
    {
        var delta = Mathf.PosMod(to - from + 180f, 360f) - 180f;
        return from + delta * weight;
    }

    private void DrawProceduralSkeleton()
    {
        if (_previewRigBones.Count == 0) return;
        var pose = EvaluatePreviewPose(_spineAnimation, _previewTime);

        const float scale = SkeletonPreviewScale;
        var origin = new Vector2(960f, SkeletonPelvisScreenY + GetPelvisDrop(_female) * scale);
        var pelvisSetup = _previewRigBones.TryGetValue("hips", out var pelvisBone)
            ? pelvisBone.Position
            : Vector2.Zero;
        var positions = new Dictionary<string, Vector2>(pose.Count);
        foreach (var (name, transform) in pose)
        {
            var relativeToPelvis = transform.Position - pelvisSetup;
            var point = origin + new Vector2(relativeToPelvis.X, -relativeToPelvis.Y) * scale;
            positions[name] = ToLocal(point);
        }
        DrawSkeleton(positions);
    }

    private void StepSpineFrame(int direction)
    {
        if (_usingProceduralRig)
        {
            var previewLength = Mathf.Max(.0001f, GetPreviewAnimationLength(_spineAnimation));
            var previewFrameCount = Mathf.Max(1, Mathf.CeilToInt(previewLength * SpineFramesPerSecond) + (_previewLoop ? 0 : 1));
            var currentPreviewFrame = Mathf.Clamp(Mathf.FloorToInt(_previewTime * SpineFramesPerSecond + .5f), 0, previewFrameCount - 1);
            var nextPreviewFrame = _previewLoop ? (currentPreviewFrame + direction) % previewFrameCount
                : Mathf.Clamp(currentPreviewFrame + direction, 0, previewFrameCount - 1);
            if (nextPreviewFrame < 0) nextPreviewFrame += previewFrameCount;
            _previewTime = Mathf.Min(previewLength, nextPreviewFrame / SpineFramesPerSecond);
            _previewBlendDuration = 0f;
            _previewBlendStart.Clear();
            _spinePaused = true;
            QueueRedraw();
            UpdateSpineLabel();
            return;
        }
        if (_spineCharacter is null || !GodotObject.IsInstanceValid(_spineCharacter)) return;
        var length = Mathf.Max(.0001f, _spineCharacter.Call("get_animation_length", _spineAnimation).AsSingle());
        var loop = IsReadyPose(_spineAnimation);
        var frameCount = Mathf.Max(1, Mathf.CeilToInt(length * SpineFramesPerSecond) + (loop ? 0 : 1));
        var currentFrame = Mathf.Clamp(Mathf.FloorToInt(_spineCharacter.Call("tell", 0).AsSingle() * SpineFramesPerSecond + .5f), 0, frameCount - 1);
        var nextFrame = loop ? (currentFrame + direction) % frameCount : Mathf.Clamp(currentFrame + direction, 0, frameCount - 1);
        if (nextFrame < 0) nextFrame += frameCount;
        _spineCharacter.Call("seek", 0, Mathf.Min(length, nextFrame / SpineFramesPerSecond));
        _spineCharacter.Call("_animation_process", 0.0);
        _spinePaused = true;
        _spineCharacter.Call("set_active", false);
        UpdateSpineLabel();
    }

    private void UpdateSpineLabel()
    {
        if (_frameLabel is null) return;
        float length;
        float time;
        if (_usingProceduralRig)
        {
            length = Mathf.Max(.0001f, GetPreviewAnimationLength(_spineAnimation));
            time = _previewTime;
        }
        else
        {
            if (_spineCharacter is null || !GodotObject.IsInstanceValid(_spineCharacter)) return;
            length = Mathf.Max(.0001f, _spineCharacter.Call("get_animation_length", _spineAnimation).AsSingle());
            time = _spineCharacter.Call("tell", 0).AsSingle();
        }
        var frameCount = Mathf.Max(1, Mathf.CeilToInt(length * SpineFramesPerSecond) + (IsReadyPose(_spineAnimation) ? 0 : 1));
        var frame = Mathf.Clamp(Mathf.FloorToInt(time * SpineFramesPerSecond + .5f), 0, frameCount - 1);
        var action = GetActionDisplayName(_spineAnimation);
        var runtime = _usingProceduralRig ? "数据驱动姿势" : "Spine 3.8";
        _frameLabel.Text = $"{(_female ? "女修" : "男修")} · {action} · 帧 {frame + 1:D2} / {frameCount:D2} · 纯骨架 · {runtime}" + (_spinePaused ? " · 已暂停" : " · 播放中");
    }

    private static string GetActionDisplayName(string animation) => animation switch
    {
        "idle" => "防守待机",
        "ready_cast" => "施法蓄势",
        "ready_sword" => "剑指引剑",
        "cast" => "施法",
        "attack" => "转身摆击",
        "attack_rising" => "上挑掌",
        "attack_leap" => "跃身踢击",
        "rest" => "休整",
        "breath" => "吐纳",
        "hurt" => "受击",
        "death_kneel" => "死亡",
        _ => animation
    };
}
