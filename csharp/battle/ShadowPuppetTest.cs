using Godot;

/// <summary>皮影局内角色的可运行测试：1/2/3 换装，Space 切换施法抬臂。</summary>
public partial class ShadowPuppetTest : Node2D
{
    private static readonly string[] BodyPaths = ["res://mods/appearance/皮影局内男修_v5/body/vermilion_body.png"];

    private Sprite2D? _body;
    private PuppetOverlay? _overlay;
    private int _outfit;
    private bool _casting;

    public override void _Ready()
    {
        if (!Engine.IsEditorHint()) { BattlePreviewScene.Mount(this); return; }
        var backdrop = new ColorRect { Color = new Color("161b1b") };
        backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var ui = new CanvasLayer { Layer = -1 }; ui.AddChild(backdrop); AddChild(ui);
        var hint = new Label { Text = "皮影局内模型测试  ·  赭红套装  ·  [Space] 掐诀施法", Position = new Vector2(28, 24) };
        hint.AddThemeFontSizeOverride("font_size", 22); hint.AddThemeColorOverride("font_color", new Color("e8d9aa")); AddChild(hint);

        _body = new Sprite2D { Position = new Vector2(640, 590), Scale = new Vector2(.34f, .34f), ZIndex = 4 };
        AddChild(_body); SetOutfit(0);
        _overlay = new PuppetOverlay { Position = _body.Position, Scale = _body.Scale, ZIndex = 0 };
        AddChild(_overlay);
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key) return;
        if (key.Keycode is Key.Key1 or Key.Key2 or Key.Key3) SetOutfit((int)key.Keycode - (int)Key.Key1);
        if (key.Keycode == Key.Space) _casting = !_casting;
    }

    public override void _Process(double delta)
    {
        if (_overlay is null) return;
        var target = _casting ? 1f : 0f;
        _overlay.cast_weight = Mathf.MoveToward(_overlay.cast_weight, target, (float)delta * 3.5f);
        _overlay.ApplyPose();
    }

    private void SetOutfit(int index)
    {
        _outfit = Mathf.Clamp(index, 0, BodyPaths.Length - 1);
        if (_body is not null) _body.Texture = GD.Load<Texture2D>(BodyPaths[_outfit]);
        _overlay?.SetPalette(_outfit);
    }
}

/// <summary>由真实 PNG 部件组成的局内皮影角色；锚点和局部缩放锁定在同一套母版比例。</summary>
public partial class PuppetOverlay : Node2D
{
    public float cast_weight;
    private const string AssetRoot = "res://mods/appearance/皮影局内男修_v5/";
    private static readonly Vector2 CanvasCenter = new(512, 1024);
    private static readonly Vector2 FarShoulderPx = new(380, 986);
    private static readonly Vector2 NearShoulderPx = new(654, 986);
    private Node2D? _farArm;
    private Node2D? _nearArm;

    public void SetPalette(int outfit)
    {
        // 当前首套部件为玄青母版；先只换身体衣袍，避免给皮影五官做错误的换色。
        Modulate = new Color(1, 1, 1, 1);
    }

    public override void _Ready()
    {
        // 由后至前：后发 → 头部 → 躯干 → 发饰 → 前发。躯干本体由 ShadowPuppetTest 的 _body 绘制。
        AddLayer("hair_back", "hair/hair_back.png", 2);
        AddLayer("head", "head/male_bald.png", 3);
        AddLayer("ornament", "ornament/cloud_hairpin.png", 5);
        AddLayer("hair_front", "hair/hair_front.png", 6);

        // 远臂压在躯干之下，近臂盖在躯干和头发之前；两条都是完整袖+手掌，无中段关节。
        _farArm = AddWholeArm("far_arm", FarShoulderPx, 1, "arm/left_arm.png");
        _nearArm = AddWholeArm("near_arm", NearShoulderPx, 7, "arm/right_arm.png");
        ApplyPose();
    }

    private void AddLayer(string name, string path, int zIndex)
    {
        var sprite = new Sprite2D
        {
            Name = name,
            Texture = GD.Load<Texture2D>(AssetRoot + path),
            ZIndex = zIndex
        };
        AddChild(sprite);
    }

    private Node2D AddWholeArm(string name, Vector2 shoulderPx, int zIndex, string path)
    {
        var arm = new Node2D { Name = name, Position = shoulderPx - CanvasCenter, ZIndex = zIndex };
        AddChild(arm);
        arm.AddChild(new Sprite2D
        {
            Name = "whole_arm",
            Texture = GD.Load<Texture2D>(AssetRoot + path),
            Centered = false,
            Position = -shoulderPx
        });
        return arm;
    }

    public void ApplyPose()
    {
        if (_farArm is null || _nearArm is null) return;
        _farArm.Rotation = -.32f + cast_weight * .18f;
        _nearArm.Rotation = -.18f - cast_weight * .30f;
    }
}
