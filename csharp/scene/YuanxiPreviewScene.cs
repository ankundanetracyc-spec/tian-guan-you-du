using Godot;

/// <summary>临时十层角色入口；复用正式养息结算，不写正式旅程。</summary>
public partial class YuanxiPreviewScene : Control
{
    private Character? _character;
    private YuanxiPanel? _panel;
    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var data = new HumanData
        {
            entity_name = "归元息体验角色", cultivation_realm = "云息十层",
            spirit_root_type = (int)GongfaResource.Wuxing.METAL_WOOD_WATER,
            spirit_root_name = "三灵根", root_weights = [34, 33, 33, 0, 0],
            main_gongfa_path = "res://resources/gongfa/黄庭养形篇.tres",
            ambient_lingqi_pressure = 180,
            max_world_stamina = 100, current_world_stamina = 100,
            has_spirit_core = true
        };
        _character = new Character(); _character.load_from_data(data); AddChild(_character); _character.Hide();
        _panel = new YuanxiPanel(); AddChild(_panel);
        _panel.Closed += () => GetTree().ChangeSceneToFile("res://tscn/scene/StartMenu.tscn");
        _panel.ShowFor(_character);
        var tools = new HBoxContainer { ZIndex = 700, AnchorTop = 1, AnchorBottom = 1, OffsetLeft = 36, OffsetTop = -60, OffsetBottom = -16 };
        AddChild(tools);
        var ordinary = new Button { Text = "临时角色：普通体力100" }; tools.AddChild(ordinary);
        ordinary.Pressed += () => Reset(100);
        var full = new Button { Text = "临时角色：零体力（仍可修炼）" }; tools.AddChild(full);
        full.Pressed += () => Reset(0);
    }
    private void Reset(float stamina)
    {
        if (_character?.entity_data is not HumanData data) return;
        _character.stop_yuanxi();
        data.yuanxi_count = 0; data.yuanxi_phase = 0; data.yuanxi_phase_hours = 0; data.yuanxi_notice = "临时角色已重置；不影响正式存档。";
        _character.max_world_stamina = 100; _character.current_world_stamina = stamina;
        _panel?.ShowFor(_character);
    }
}
