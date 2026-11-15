using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class Character
{
    private const string SpiritBoatStone = "res://resources/entities/items/灵石.tres";

    /// <summary>A boat must be an actual owned artifact with an explicit flight profile.</summary>
    public static bool is_usable_spirit_boat(ArtifactInstanceData? item)
    {
        if (item?.travel_data is not { travel_mode: "flight", visual_id: "spirit_boat" } data ||
            data.consumption_resource_path != SpiritBoatStone || data.consumption_amount <= 0 ||
            !double.IsFinite(data.consumption_period_hours) || data.consumption_period_hours <= 0 ||
            !data.try_get_dun_speed(out _) || !double.IsFinite(item.travel_charge_hours) || item.travel_charge_hours < 0) return false;
        return ForgedArtifactInventory.TryRead(item, out var snapshot) && snapshot.Category == "舟";
    }

    public ArtifactInstanceData? get_owned_spirit_boat(string id)
    {
        var item = find_artifact(id);
        return is_usable_spirit_boat(item) ? item : null;
    }

    public bool board_spirit_boat(string id, int occupants = 1)
    {
        if (human is null || _spatialCommitting || get_owned_spirit_boat(id) is not { travel_in_use: false } boat ||
            occupants < 1 || boat.travel_data?.try_get_passenger_capacity(out var capacity) != true || occupants > capacity ||
            human.artifact_inventory.Any(item => item is { travel_in_use: true })) return false;
        boat.travel_in_use = true;
        boat.EmitChanged();
        notify_inventory_changed();
        return true;
    }

    public bool leave_spirit_boat(string id)
    {
        if (get_owned_spirit_boat(id) is not { travel_in_use: true } boat) return false;
        boat.travel_in_use = false;
        boat.EmitChanged();
        notify_inventory_changed();
        return true;
    }

    /// <summary>Charge only complete stones, atomically with the inventory transaction.</summary>
    public bool prepare_spirit_boat_hours(string id, double neededHours)
    {
        if (!double.IsFinite(neededHours) || neededHours < 0 || get_owned_spirit_boat(id) is not { travel_in_use: true } boat || boat.travel_data is not { } data) return false;
        double shortfall = Math.Max(0, neededHours - boat.travel_charge_hours);
        if (shortfall <= 1e-8) return true;
        double rawUnits = Math.Ceiling(shortfall / data.consumption_period_hours - 1e-10);
        if (!double.IsFinite(rawUnits) || rawUnits < 1 || rawUnits > int.MaxValue / (double)data.consumption_amount) return false;
        int units = (int)rawUnits, stones = units * data.consumption_amount;
        var previous = boat.travel_charge_hours;
        return try_exchange_inventory(new Dictionary<string, int> { [SpiritBoatStone] = stones }, new Dictionary<string, int>(), beforeCommit: () =>
        {
            if (get_owned_spirit_boat(id) != boat || !boat.travel_in_use || boat.travel_charge_hours != previous) return false;
            boat.travel_charge_hours = previous + units * data.consumption_period_hours;
            boat.EmitChanged();
            return true;
        });
    }

    /// <summary>Consume actual completed flight time from the same unique instance.</summary>
    public bool consume_spirit_boat_hours(string id, double hours)
    {
        if (!double.IsFinite(hours) || hours < 0 || get_owned_spirit_boat(id) is not { travel_in_use: true } boat ||
            boat.travel_charge_hours + 1e-7 < hours) return false;
        boat.travel_charge_hours = Math.Max(0, boat.travel_charge_hours - hours);
        boat.EmitChanged();
        return true;
    }
}
