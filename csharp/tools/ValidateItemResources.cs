using Godot;

/// <summary>编辑器/CI 物品资源校验入口。</summary>
public partial class ValidateItemResources : Node
{
    public override void _Ready()
    {
        var failures = new List<string>(); var ids = new HashSet<string>(); var checkedCount = 0;
        foreach (var directory in new[] { "res://resources/entities/items", "res://resources/entities/equipment" })
        {
            using var access = DirAccess.Open(directory); if (access is null) { failures.Add($"无法打开物品目录：{directory}"); continue; }
            foreach (var file in access.GetFiles())
            {
                if (!file.EndsWith(".tres")) continue; var path = directory.PathJoin(file); var item = ResourceLoader.Load<ItemData>(path); if (item is null) { failures.Add($"无法作为 ItemData 加载：{path}"); continue; }
                checkedCount++; foreach (var error in item.validate_definition()) failures.Add($"{path}：{error}");
                if (item is EquipmentData && (item.can_stack || item.max_stack != 1)) failures.Add($"{path}：装备必须不可堆叠且单格上限为1");
                if (item.item_category == "材料" && (!item.can_stack || item.max_stack != 999)) failures.Add($"{path}：材料必须可堆叠且单格上限为999");
                if (!ids.Add(item.definition_id.ToString())) failures.Add($"重复物品ID：{item.definition_id}");
            }
        }
        if (failures.Count > 0) { foreach (var failure in failures) GD.PushError(failure); GetTree().Quit(1); return; }
        GD.Print($"物品资源校验通过：{checkedCount} 项，ID与词缀均合法。"); GetTree().Quit();
    }
}
