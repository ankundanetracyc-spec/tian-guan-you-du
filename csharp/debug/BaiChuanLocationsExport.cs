using Godot;

/// <summary>One-time generation of the formal catalogue, without legacy background assets.</summary>
public partial class BaiChuanLocationsExport : Node
{
    public override void _Ready()
    {
        var old = GD.Load<WorldNodeMapData>("res://resources/world/yunqu_state_nodes.tres");
        var catalogue = BaiChuanLocationCatalog.Build(old);
        var error = ResourceSaver.Save(catalogue, "res://resources/world/bai_chuan/bai_chuan_locations_20261101.tres");
        if (error != Error.Ok) { GD.PushError($"无法写入百川地点目录：{error}"); GetTree().Quit(1); return; }
        GD.Print("BAICHUAN_LOCATIONS_EXPORT_OK");
        GetTree().Quit();
    }
}
