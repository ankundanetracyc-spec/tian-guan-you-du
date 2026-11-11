using Godot;
using System;

/// <summary>
/// Optional item-owned travel description. Missing data is unknown, never an
/// implicit free journey or a speed borrowed from the person driving.
/// Travel and resource settlement read these values from the item instance.
/// </summary>
[GlobalClass]
public partial class ItemTravelData : Resource
{
    // An explicit type avoids interpreting every item with a speed as a boat.
    [Export] public string travel_mode = "";
    [Export] public string visual_id = "";
    [Export] public bool has_dun_speed;
    [Export] public double dun_speed;
    // Total people aboard, including whoever drives. Zero means unspecified.
    [Export] public int passenger_capacity;
    // Structured description for a periodic resource cost. These values do not
    // by themselves authorize or perform any inventory transaction.
    [Export(PropertyHint.File, "*.tres")] public string consumption_resource_path = "";
    [Export] public int consumption_amount;
    [Export] public double consumption_period_hours;
    [Export(PropertyHint.MultilineText)] public string consumption_description = "";

    public bool try_get_dun_speed(out double speed)
    {
        speed = dun_speed;
        return has_dun_speed && double.IsFinite(speed) && speed > 0;
    }

    public bool try_get_passenger_capacity(out int capacity)
    {
        capacity = passenger_capacity;
        return capacity > 0;
    }

    public string describe_consumption()
    {
        if (consumption_amount > 0 && double.IsFinite(consumption_period_hours) && consumption_period_hours > 0 &&
            !string.IsNullOrWhiteSpace(consumption_resource_path) && ResourceLoader.Exists(consumption_resource_path) &&
            GD.Load<ItemData>(consumption_resource_path) is { } resource && !string.IsNullOrWhiteSpace(resource.entity_name))
            return $"{resource.entity_name} ×{consumption_amount}／{consumption_period_hours:0.##}小时";
        return string.IsNullOrWhiteSpace(consumption_description) ? "未定" : consumption_description.Trim();
    }
}
