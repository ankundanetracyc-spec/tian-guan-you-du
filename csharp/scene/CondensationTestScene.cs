using Godot;

/// <summary>从启动菜单直接进入的凝核界面测试场景；只创建临时角色，不写入正式存档。</summary>
public partial class CondensationTestScene : Control
{
    private const string BackdropPath = "res://textures/start_menu/ink_mountain_dawn_v2.png";
    private CondensationCardGamePanel? _panel;
    private Character? _target;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        BuildBackdrop();
        _target = BuildTarget();
        AddChild(_target);
        _target.Visible = false;
        _panel = new CondensationCardGamePanel { Name = "CondensationCardGamePanel" };
        AddChild(_panel);
        _panel.Closed += ReturnToStartMenu;
        CallDeferred(nameof(StartTest));
    }

    private void BuildBackdrop()
    {
        var backdrop = new TextureRect
        {
            Texture = GD.Load<Texture2D>(BackdropPath),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = MouseFilterEnum.Ignore,
            Modulate = new Color(0.82f, 0.84f, 0.79f, 1f)
        };
        backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(backdrop);
        var shade = new ColorRect { Color = new Color(0.035f, 0.055f, 0.05f, 0.30f), MouseFilter = MouseFilterEnum.Ignore };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(shade);
    }

    private static Character BuildTarget()
    {
        var data = new CharacterGenerator().generate("云息", (int)WorldEntity.Faction.PLAYER);
        data.entity_name = "凝核试演者";
        data.cultivation_realm = "云息六层";
        data.base_intelligence = 15;
        data.base_comprehension = 50;
        var character = new Character();
        character.load_from_data(data);
        character.entity_type = WorldEntity.EntityType.CHARACTER;
        character.faction = (int)WorldEntity.Faction.PLAYER;
        character.unique_id = "condensation_test_menu_character";
        return character;
    }

    private void StartTest()
    {
        if (_panel is not null && _target is not null) _panel.ShowFor(_target, true);
    }

    private void ReturnToStartMenu()
    {
        GetTree().ChangeSceneToFile("res://tscn/scene/StartMenu.tscn");
    }
}
