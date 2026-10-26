using Godot;
using Godot.Collections;

public partial class Character
{
    public Array<BattleSupplySlotData> battle_supplies => human?.battle_supplies ?? [];

    public bool set_battle_supply(int index, string path, int count)
    {
        if (human is null || HasMeta("artifact_battle_clone")) return false;
        ensure_inventory_state();
        var prepared = new BattleEntityData { pack_item_paths = pack_items.Duplicate(), pack_item_counts = pack_counts.Duplicate(),
            battle_supplies = human.battle_supplies.Duplicate(true) };
        if (!BattleSupplyRules.Set(prepared, index, path, count)) return false;
        human.battle_supplies = prepared.battle_supplies;
        human.EmitChanged();
        return true;
    }

    private void ApplyBattleSupplySnapshot(BattleEntityData snapshot)
    {
        // 兼容没有行囊快照的历史／属性测试数据，不能以空数组清掉真实行囊。
        if (human is null || !snapshot.pack_state_included) return;
        human.inventory_item_paths = snapshot.pack_item_paths.Duplicate();
        human.inventory_item_counts = snapshot.pack_item_counts.Duplicate();
        human.inventory_item_positions = snapshot.pack_item_positions.Duplicate();
        human.battle_supplies = snapshot.battle_supplies.Duplicate(true);
        human.pill_toxicity = PillToxicityRules.Normalize(snapshot.pill_toxicity);
        human.pill_toxicity_last_game_hour = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_precise_game_hours() ?? -1;
        ensure_inventory_state();
        BattleSupplyRules.Normalize(human.battle_supplies, pack_items, pack_counts);
    }

    private void CaptureBattleSupplySnapshot(BattleEntityData snapshot)
    {
        if (human is null) return;
        snapshot.pack_state_included = true;
        snapshot.pack_item_positions = human.inventory_item_positions.Duplicate();
        snapshot.battle_supplies = human.battle_supplies.Duplicate(true);
        BattleSupplyRules.Normalize(snapshot.battle_supplies, snapshot.pack_item_paths, snapshot.pack_item_counts);
    }

    public bool CanUseBattlePill(int index, int manaCapacity)
    {
        if (human is null || current_health <= 0 || index < 0 || index >= battle_supplies.Count) return false;
        var slot = battle_supplies[index];
        var pill = BattleSupplyRules.Load(slot.item_path);
        return pill is not null && slot.count > 0 && has_inventory_item(slot.item_path) &&
            BattleSupplyRules.Amount(pill, manaCapacity, PillToxicity) > 0 &&
            (pill.pill_id == "huichun" ? current_health < max_health : current_mana < manaCapacity);
    }

    public bool TryUseBattlePill(int index, int manaCapacity, out int restored)
    {
        restored = 0;
        if (human is null || current_health <= 0 || index < 0 || index >= battle_supplies.Count) return false;
        var slot = battle_supplies[index];
        var pill = BattleSupplyRules.Load(slot.item_path);
        if (pill is null || slot.count <= 0 || !has_inventory_item(slot.item_path)) return false;
        var missing = pill.pill_id == "huichun" ? max_health - current_health : Math.Max(0, manaCapacity - current_mana);
        restored = Math.Min(missing, BattleSupplyRules.Amount(pill, manaCapacity, PillToxicity));
        if (restored <= 0 || !remove_item_from_inventory(slot.item_path, 1)) { restored = 0; return false; }
        if (pill.pill_id == "huichun") restored = apply_healing(BattleSupplyRules.Amount(pill, manaCapacity, PillToxicity));
        else current_mana += restored;
        slot.count--;
        AddPillToxicity(pill.pill_id, pill.efficacy_tier);
        return true;
    }
}
