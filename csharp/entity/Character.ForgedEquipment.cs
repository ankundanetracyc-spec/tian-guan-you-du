using Godot;

public partial class Character
{
    public ArtifactBondRuntime.Totals get_artifact_bonds() => ArtifactBondRuntime.Aggregate(human is null ? [] :
        human.artifact_inventory.Where(item => item is not null && human.equipped_artifact_ids.Contains(item.instance_id)));

    private void apply_artifact_bond_defense() => physical_defense = Mathf.FloorToInt(physical_defense * (1 + get_artifact_bonds().DefenseBonus));
    public ArtifactInstanceData? get_equipped_forged_sword() => human?.artifact_inventory.FirstOrDefault(item =>
        item is not null && human.equipped_artifact_ids.Contains(item.instance_id) &&
        ForgedArtifactInventory.TryRead(item, out var snapshot) && snapshot.Category == "剑");

    private bool equip_forged_sword(ArtifactInstanceData item)
    {
        if (human is null || !ForgedArtifactInventory.TryRead(item, out var snapshot) || snapshot.Category != "剑") return false;
        var previous = get_equipped_forged_sword();
        if (previous is not null) { human.equipped_artifact_ids.Remove(previous.instance_id); revoke_artifact_skill(previous.instance_id); }
        human.weapon_path = "";
        human.equipped_artifact_ids.Add(item.instance_id);
        refresh_gongfa_effects(); notify_inventory_changed();
        return true;
    }

    private void remove_forged_weapon_for_standard_equipment()
    {
        if (get_equipped_forged_sword() is not { } previous || human is null) return;
        human.equipped_artifact_ids.Remove(previous.instance_id); revoke_artifact_skill(previous.instance_id);
    }

    private void apply_forged_equipment_base_bonuses()
    {
        if (get_equipped_forged_sword() is not { } item || !ForgedArtifactInventory.TryRead(item, out var snapshot)) return;
        using var attributeSource = TraceAttributeSource(item.artifact_name);
        foreach (var effect in snapshot.Effects.Where(effect => effect.CarrierId == "base" && effect.AtomId == "physical_defense"))
            apply_runtime_gongfa_bonus("physical_defense", (float)effect.Value);
    }
}
