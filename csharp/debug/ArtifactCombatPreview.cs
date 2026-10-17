using Godot;
using Godot.Collections;

/// <summary>独立交互验收入口；使用内存角色，不读写正式旅程。</summary>
public partial class ArtifactCombatPreview : Node2D
{
    private Character? _loadoutOwner;
    public override void _Ready()
    {
        DisplayServer.WindowSetTitle("法器战斗接入测试");
        CallDeferred(nameof(Start));
    }
    private PackedScene CharacterScene()
    {
        var prototype = new Character(); var scene = new PackedScene(); scene.Pack(prototype); prototype.Free(); return scene;
    }
    private void Start()
    {
        _loadoutOwner = new Character { entity_data = new HumanData
        {
            entity_name = "试器修士", unique_id = "artifact-preview-player", cultivation_realm = "云息三层",
            max_health = 200, current_health = 200, max_mana = 3, current_mana = 3,
            max_spirit_perception = 5, artifact_slot_count = 4, physique = 10, agility = 10,
            precision = 100, crit_resist = 20, soul = 10, initiative_speed = 150
        } };
        AddChild(_loadoutOwner); _loadoutOwner.Hide();
        foreach (var (guard, inner, id) in new[] { (false, false, "preview-ming-outer"), (true, false, "preview-shou-outer"),
            (false, true, "preview-ming-inner"), (true, true, "preview-shou-inner") })
        {
            var item = ArtifactCombatSamples.Create(guard, inner, id);
            _loadoutOwner.add_artifact_to_inventory(item); _loadoutOwner.equip_artifact(item.instance_id);
        }
        _loadoutOwner.set_combat_skill_slot_paths(["res://resources/activeskills/应敌.tres", "res://resources/activeskills/架招.tres",
            "res://resources/activeskills/痛击.tres", "artifact:preview-ming-outer", "artifact:preview-shou-outer"]);
        StartBattle();
        var panel = GD.Load<PackedScene>("res://tscn/panel/InfoPanel.tscn").Instantiate<InfoPanel>();
        GetNode<CanvasLayer>("UILayer").AddChild(panel); panel.Hide();
        var button = new Button { Text = "查看／调整测试装配", Position = new(32, 112), Size = new(230, 42) };
        GetNode<CanvasLayer>("UILayer").AddChild(button);
        button.Pressed += () => panel.show_entity(_loadoutOwner);
        var reset = new Button { Text = "按当前装配重开测试", Position = new(278, 112), Size = new(240, 42) };
        GetNode<CanvasLayer>("UILayer").AddChild(reset); reset.Pressed += StartBattle;
        if (OS.GetCmdlineUserArgs().Contains("--capture-preview")) CapturePreview();
    }
    private void StartBattle()
    {
        if (_loadoutOwner is null) return;
        var player = _loadoutOwner.get_snapshot_data(); player.faction = 1;
        player.entity_name = "试器修士"; player.max_mana = 3; player.current_mana = 3;
        player.max_spirit_perception = 5; player.max_health = 200; player.current_health = 200;
        var enemy = new BattleEntityData
        {
            uid = "artifact-preview-enemy", entity_name = "试器陪练", faction = 2, cultivation_realm = "云息三层",
            max_health = 500, current_health = 500, max_stamina = 6, max_mana = 3, max_spirit_perception = 2,
            physique = 8, agility = 8, precision = 100, dodge = 0, crit_resist = 20, physical_defense = 5,
            initiative_speed = 100, combat_skill_slots_configured = true,
            combat_skill_slot_paths = ["artifact:preview-enemy-outer"],
            equipped_artifacts = [ArtifactCombatSamples.Create(false, false, "preview-enemy-outer")]
        };
        GetNode<BattleManager>("BattleMgr").setup_team_battle([CharacterScene()], [CharacterScene()], [player], [enemy]);
    }
    private async void CapturePreview()
    {
        for (var i = 0; i < 20; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        DirAccess.MakeDirRecursiveAbsolute("E:/stacklands/outputs/artifact-combat");
        GetViewport().GetTexture().GetImage().SavePng("E:/stacklands/outputs/artifact-combat/preview.png");
        GD.Print("ARTIFACT_PREVIEW_CAPTURED");
        GetTree().Quit();
    }
}
