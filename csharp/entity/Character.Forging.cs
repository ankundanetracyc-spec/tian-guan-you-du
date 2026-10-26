using Godot;
using System;
using System.Linq;

public partial class Character
{
    private bool _forgingCommitting;

    public System.Collections.Generic.Dictionary<string, int> GetForgingStock(ProcessForgingTrial model) =>
        model.Parameters.Profiles.Keys.ToDictionary(name => name,
            name => get_inventory_total_count(model.Materials[name].ResourcePath));

    public bool TryForge(ProcessForgingTrial model, ProcessForgingTrial.Draft supplied,
        out ProcessForgingTrial.Evaluation result)
    {
        result = new();
        if (human is null || current_health <= 0 || _forgingCommitting)
        { result.Errors.Add("人物暂不能炼器。"); return false; }
        ensure_inventory_state();
        var draft = new ProcessForgingTrial.Draft {
            Category = supplied.Category, Proficiency = Math.Clamp((int)human.forging_proficiency, 0, 100),
            Inputs = supplied.Inputs.Select(i => new ProcessForgingTrial.Input {
                Role = i.Role, Material = i.Material, Quantity = i.Quantity }).ToList()
        };
        // 正式事务使用当前资源定义，拒绝调用方传入的试算档案。
        model = new ProcessForgingTrial();
        var stock = GetForgingStock(model);
        var preparation = model.Inspect(draft, stock, max_spirit_perception, used_spirit_perception);
        result.Errors.AddRange(preparation.Errors);
        if (!result.CanSubmit) return false;
        if (!can_spend_world_stamina(preparation.Energy))
        { result.Errors.Add("当前精力不足。"); return false; }
        result = model.Evaluate(draft, stock);
        if (!result.CanSubmit) return false;
        var paths = human.inventory_item_paths.ToList();
        var counts = human.inventory_item_counts.ToList();
        var positions = human.inventory_item_positions.ToList();
        foreach (var (name, quantity) in result.Required)
        {
            var path = model.Materials[name].ResourcePath;
            var remaining = quantity;
            for (int i = paths.Count - 1; i >= 0 && remaining > 0; i--)
            {
                if (paths[i] != path) continue;
                int removed = Math.Min(counts[i], remaining);
                counts[i] -= removed; remaining -= removed;
                if (counts[i] == 0) { paths.RemoveAt(i); counts.RemoveAt(i); positions.RemoveAt(i); }
            }
        }
        ArtifactInstanceData? product = null;
        var free = -1;
        if (result.Success)
        {
            var occupied = positions.Concat(human.artifact_inventory_positions).ToHashSet();
            free = Enumerable.Range(0, max_pack_slots).FirstOrDefault(i => !occupied.Contains(i), -1);
            if (free < 0) { result.Errors.Add("行囊已满，请先整理行囊再开炼。"); return false; }
            result.Product!.InstanceId = "forged-" + Guid.NewGuid().ToString("N");
            result.Product.Lifecycle = "人物炼制，固定快照保存";
            product = ForgedArtifactInventory.Create(result.Product);
        }
        _forgingCommitting = true;
        try
        {
            human.inventory_item_paths = new(paths);
            human.inventory_item_counts = new(counts);
            human.inventory_item_positions = new(positions);
            human.current_world_stamina = Math.Max(0, human.current_world_stamina - result.Energy);
            if (product is not null) { human.artifact_inventory.Add(product); human.artifact_inventory_positions.Add(free); }
            human.forging_records.Add(crafting_record_date() + (result.Success ? $"炼成{result.Product!.Name}" : "未成器") +
                $" · {result.Hours}时辰 · 精力{result.Energy}\n" + string.Join("、", draft.Inputs.Select(i =>
                    $"{i.Role.Replace("复合身材", "辅体")}：{i.Material}×{i.Quantity}")));
            notify_inventory_changed();
            WorldStaminaChanged?.Invoke(this);
            if (IsInsideTree())
            {
                GetTree().CallGroup("main_scene", "notify_world_stamina_changed", this);
                GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.advance_time(2 * result.Hours, "forging");
            }
            return true;
        }
        finally { _forgingCommitting = false; }
    }
}
