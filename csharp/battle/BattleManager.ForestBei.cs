using Godot;

public partial class BattleManager
{
    private IEnumerable<Beast> PackWolves(GodotObject source) => all_units.Where(Alive)
        .OfType<Beast>().Where(wolf => wolf.species_id == "forest_wolf"
            && wolf.faction == source.Get("faction").AsInt32());

    private bool HasForestBeiChief(GodotObject source) => all_units.Where(Alive).OfType<Beast>()
        .Any(bei => bei.species_id == "forest_bei" && bei.faction == source.Get("faction").AsInt32()
            && bei.racial_talent?.talent_id == "forest_bei_pack_chief");

    public int grant_pack_hunt_to_wolves(GodotObject source)
    {
        if (!Alive(source)) return 0;
        var status = GD.Load<StatusEffect>("res://resources/battle/statuses/逐猎.tres");
        if (status is null) return 0;
        var changed = 0;
        foreach (var wolf in PackWolves(source))
        {
            var before = wolf.get_status_stacks("逐猎");
            wolf.add_status(status, -1, 1, source.GetInstanceId().ToString(), status.max_stacks, source.Get("entity_name").AsString());
            if (wolf.get_status_stacks("逐猎") > before)
            {
                changed++;
                if (!is_spectator_battle()) BattleInkCombatVfx.PlayForestBeiBlessing(wolf);
            }
        }
        return changed;
    }
}
