using Godot;
using Godot.Collections;

/// <summary>村庄功能分区的容量与建筑路径登记。</summary>
public partial class VillageZoneManager : Node
{
    [Signal] public delegate void zone_data_changedEventHandler(string zone_id);

    public sealed class ZoneDef(string id, string display_name, Color color)
    {
        public readonly string id = id;
        public readonly string display_name = display_name;
        public readonly Color color = color;
    }

    private readonly System.Collections.Generic.Dictionary<string, ZoneDef> zone_defs = new()
    {
        ["residential"] = new ZoneDef("residential", "宅院", new Color(0.9f, 0.7f, 0.2f)),
        ["resource"] = new ZoneDef("resource", "田庄", new Color(0.4f, 0.65f, 0.3f)),
        ["farmland"] = new ZoneDef("farmland", "良田", new Color(0.3f, 0.7f, 0.2f)),
        ["quarry"] = new ZoneDef("quarry", "石矿", new Color(0.55f, 0.55f, 0.6f)),
    };
    public readonly Array<string> resource_sub_zones = ["farmland", "quarry"];
    public readonly Dictionary zone_buildings = [];
    public readonly Dictionary zone_max_buildings = new() { ["residential"] = 7, ["farmland"] = 5, ["quarry"] = 3 };

    public override void _Ready()
    {
        AddToGroup("village_zone_manager");
        foreach (var zone in zone_defs.Keys) zone_buildings[zone] = new Array<string>();
    }

    public bool register_building(string zone_id, string building_path)
    {
        if (!zone_buildings.TryGetValue(zone_id, out var value) || value.VariantType != Variant.Type.Array) return false;
        var buildings = value.AsGodotArray();
        if (buildings.Count >= get_limit(zone_id)) return false;
        buildings.Add(building_path); EmitSignal(SignalName.zone_data_changed, zone_id); return true;
    }

    public bool unregister_building(string zone_id, string building_path)
    {
        if (!zone_buildings.TryGetValue(zone_id, out var value) || value.VariantType != Variant.Type.Array) return false;
        var buildings = value.AsGodotArray();
        var index = buildings.IndexOf(building_path);
        if (index < 0) return false;
        buildings.RemoveAt(index); EmitSignal(SignalName.zone_data_changed, zone_id); return true;
    }

    public int get_building_count(string zone_id) => zone_buildings.TryGetValue(zone_id, out var value) && value.VariantType == Variant.Type.Array ? value.AsGodotArray().Count : 0;

    public Array<string> get_zone_summary(string zone_id)
    {
        return zone_id switch
        {
            "residential" => [$"宅院 {get_building_count(zone_id)}/{get_limit(zone_id)}", "点击管理人员"],
            "resource" => resource_summary(),
            "farmland" => [$"农田 {get_building_count(zone_id)}/{get_limit(zone_id)}", ""],
            "quarry" => [$"石矿 {get_building_count(zone_id)}/{get_limit(zone_id)}", ""],
            _ => ["", ""],
        };
    }

    private Array<string> resource_summary()
    {
        var total = 0; var maximum = 0;
        foreach (var zone in resource_sub_zones) { total += get_building_count(zone); maximum += get_limit(zone); }
        return [$"田庄 {total}/{maximum}", "点击管理资源"];
    }

    private int get_limit(string zone_id) => zone_max_buildings.TryGetValue(zone_id, out var value) ? value.AsInt32() : 99;
}
