using Godot;

/// <summary>真实场景往返的隔离角色；不创建或打开正式旅程。</summary>
public partial class HumanCombatBoardReturnWorld : Node2D
{
    public Character Player { get; private set; } = null!;
    public override void _Ready()
    {
        var data = new HumanData { data_initialized = true, entity_name = "旧档配招验收", current_health = 55, max_health = 100,
            physique = 10, intelligence = 10, soul = 10, base_physique = 10, base_intelligence = 10, base_soul = 10,
            cultivation_realm = "云息三层", combat_skill_slots_configured = true,
            combat_skill_slot_paths = ["res://resources/activeskills/应敌.tres", "res://resources/activeskills/架招.tres", "res://resources/activeskills/痛击.tres", "", "", "", "", "", "", ""],
            inventory_item_paths = ["res://resources/entities/items/alchemy/huichun_1.tres"], inventory_item_counts = [2], inventory_item_positions = [0] };
        data.combat_skill_references = new(data.combat_skill_slot_paths.Select(CombatSkillReference.FromKey));
        Player = new Character { Name = "Player" }; AddChild(Player); Player.load_from_data(data);
        Player.unique_id = "board_return_player"; Player.faction = 1;
        AddChild(new CombatTrigger { Name = "CombatTrigger" });
    }
}
