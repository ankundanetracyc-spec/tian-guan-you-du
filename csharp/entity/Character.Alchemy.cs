using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class Character
{
    private bool _alchemyCommitting;
    public double AlchemyAttainment => Math.Max(0, human?.alchemy_proficiency ?? 0);
    /// <summary>正式界面只使用配伍自动产量。</summary>
    public bool TryBrewAlchemy(IReadOnlyList<AlchemySlotSimulation.Input> supplied,
        out AlchemySlotSimulation.Result? result, out string reason) => TryBrewAlchemy(supplied, 0, out result, out reason);

    internal bool TryBrewAlchemy(IReadOnlyList<AlchemySlotSimulation.Input> supplied, int count,
        out AlchemySlotSimulation.Result? result, out string reason)
    {
        result = null; reason = "";
        if (count is < 0 or > 4) { reason = "内部试算颗数无效。"; return false; }
        if (human is null || current_health <= 0 || _alchemyCommitting) { reason = "人物暂不能炼丹。"; return false; }
        ensure_inventory_state();
        var inputs = new List<AlchemySlotSimulation.Input>();
        foreach (var input in supplied)
        {
            var item = ResourceLoader.Exists(input.Path) ? GD.Load<ItemData>(input.Path) : null;
            if (item?.item_category != "材料" || item.craft_data is not MaterialCraftData craft)
            { reason = "投料材料已失效，请重新选材。"; return false; }
            inputs.Add(new(item.entity_name, input.Path, craft, input.Dose, input.Role));
        }
        var simulation = AlchemySlotSimulation.Simulate(inputs, AlchemyAttainment,
            max_spirit_perception, used_spirit_perception, count);
        result = simulation;
        if (!simulation.CanSimulate) { reason = simulation.BlockingReason; return false; }
        if (!can_spend_world_stamina(simulation.EnergyNeed)) { reason = "当前精力不足。"; return false; }
        // 在副本数组中完成扣料，全部通过才一次性发布，避免信号重入导致半炉结算。
        ensure_inventory_state();
        var paths = human.inventory_item_paths.ToList();
        var amounts = human.inventory_item_counts.ToList();
        var positions = human.inventory_item_positions.ToList();
        foreach (var input in inputs)
        {
            if (paths.Select((path, i) => path == input.Path ? amounts[i] : 0).Sum() < input.Dose)
            { reason = $"{input.Name}数量不足，需要{input.Dose}份。"; return false; }
            int remaining = input.Dose;
            for (int i = paths.Count - 1; i >= 0 && remaining > 0; i--)
            {
                if (paths[i] != input.Path) continue;
                int removed = Math.Min(amounts[i], remaining); amounts[i] -= removed; remaining -= removed;
                if (amounts[i] == 0) { paths.RemoveAt(i); amounts.RemoveAt(i); positions.RemoveAt(i); }
            }
        }
        var outputs = simulation.Pills.Where(pill => pill.Quality >= 20)
            .GroupBy(pill => AlchemyQualityRules.Tier(pill.Quality))
            .Select(group => (Path: AlchemyProductCatalog.Path(simulation.ProductId, group.Key), Count: group.Count())).ToArray();
        var outputPlans = new List<(string Path, int Count, PillItemData Pill)>();
        foreach (var output in outputs)
        {
            var item = ResourceLoader.Exists(output.Path) ? GD.Load<PillItemData>(output.Path) : null;
            var containerStyle = item is null ? null : PillContainerCatalog.LoadContainerDefinition(item.container_material_id);
            if (item is null || item.validate_definition().Count != 0 || containerStyle is null || containerStyle.validate_definition().Count != 0)
            { reason = "丹药或对应瓷瓶外观定义未就绪，未扣投料。"; return false; }
            outputPlans.Add((output.Path, output.Count, item));
        }
        // 成品留给通用领取队列；满包时可在领取页整理，不提前入包。
        _alchemyCommitting = true;
        try
        {
            human.inventory_item_paths = new(paths);
            human.inventory_item_counts = new(amounts);
            human.inventory_item_positions = new(positions);
            human.current_world_stamina = Math.Max(0, human.current_world_stamina - simulation.EnergyNeed);
            human.alchemy_records.Add(crafting_record_date() + simulation.Outcome +
                $" · {AlchemySlotSimulation.DurationHours(inputs):0.##}时辰 · 精力{simulation.EnergyNeed}\n" +
                string.Join("、", inputs.Select(i => $"{i.Name}×{i.Dose}")));
            // 炼制一次完成；不登记长期灵识占用。历法推进会处理正常世界事件。
            ensure_spatial_inventory_state();
            EmitSignal(SignalName.inventory_changed);
            InventoryChanged?.Invoke(this);
            WorldStaminaChanged?.Invoke(this);
            if (IsInsideTree())
            {
                GetTree().CallGroup("main_scene", "notify_world_stamina_changed", this);
                GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.advance_time(
                    (float)(2 * AlchemySlotSimulation.DurationHours(inputs)), "alchemy");
            }
            // 领取页成功入包后才发布获得通知。
            return true;
        }
        finally { _alchemyCommitting = false; }
    }

    public bool CanUseAlchemyPill(PillItemData item)
    {
        if (HasMeta("artifact_battle_clone")) return false;
        if (human is null || !AlchemyProductCatalog.IsEnabled(item.pill_id) || !has_inventory_item(item.ResourcePath) || item.efficacy_tier is < 0 or > 4) return false;
        bool effectAvailable = item.pill_id switch
        {
            "huichun" => current_health > 0 && current_health < max_health,
            "tongmai" => current_health > 0 && current_mana < max_mana &&
                (long)max_mana * AlchemyProductCatalog.ManaPercent[item.efficacy_tier] / 100 > 0,
            "jingxin" => current_health > 0 && human.mood_value < 0,
            _ => false
        };
        if (!effectAvailable) return false;
        return true;
    }

    public bool TryUseAlchemyPill(PillItemData item)
    {
        if (human is null || !CanUseAlchemyPill(item)) return false;
        ensure_inventory_state();
        var paths = human.inventory_item_paths.ToList();
        var amounts = human.inventory_item_counts.ToList();
        var positions = human.inventory_item_positions.ToList();
        int index = FindPillUseIndex(paths, amounts, item.ResourcePath);
        if (index < 0) return false;
        amounts[index]--;
        if (amounts[index] == 0)
        {
            paths.RemoveAt(index);
            amounts.RemoveAt(index);
            positions.RemoveAt(index);
        }
        human.inventory_item_paths = new(paths);
        human.inventory_item_counts = new(amounts);
        human.inventory_item_positions = new(positions);
        int tier = item.efficacy_tier;
        int amount = AlchemyPillAmount(item.pill_id, tier);
        switch (item.pill_id)
        {
            case "huichun": current_health = Math.Min(max_health, current_health + amount); break;
            case "tongmai": current_mana = Math.Min(max_mana, current_mana + amount); break;
            case "jingxin": human.mood_value = Math.Min(0, human.mood_value + amount); break;
        }
        AddPillToxicity(item.pill_id, tier);
        ensure_spatial_inventory_state();
            EmitSignal(SignalName.inventory_changed);
        InventoryChanged?.Invoke(this);
        if (IsInsideTree()) GetTree().CallGroup("main_scene", "notify_world_stamina_changed", this);
        return true;
    }

    private static int FindPillUseIndex(List<string> paths, List<int> amounts, string pillPath)
    {
        for (int i = paths.Count - 1; i >= 0; i--)
            if (paths[i] == pillPath && amounts[i] > 0) return i;
        return -1;
    }

    private bool TryAddStagedItem(List<string> paths, List<int> amounts, List<int> positions,
        string path, int count, ItemData item)
    {
        if (human is null || count <= 0) return count == 0;
        if (item.can_stack)
        {
            long existing = 0;
            var existingIndex = -1;
            for (var i = 0; i < paths.Count; i++)
                if (paths[i] == path)
                {
                    existingIndex = existingIndex < 0 ? i : existingIndex;
                    existing += Math.Max(0, amounts[i]);
                }
            if (existing > int.MaxValue - (long)count) return false;
            if (existingIndex >= 0)
            {
                amounts[existingIndex] += count;
                return true;
            }
            var occupied = positions.Concat(human.artifact_inventory_positions).ToHashSet();
            var free = Enumerable.Range(0, max_pack_slots).FirstOrDefault(index => !occupied.Contains(index), -1);
            if (free < 0) return false;
            paths.Add(path);
            amounts.Add(count);
            positions.Add(free);
            return true;
        }

        var occupiedSlots = positions.Concat(human.artifact_inventory_positions).ToHashSet();
        var freeSlots = Enumerable.Range(0, max_pack_slots).Where(index => !occupiedSlots.Contains(index)).Take(count).ToArray();
        if (freeSlots.Length < count) return false;
        foreach (var free in freeSlots)
        {
            paths.Add(path);
            amounts.Add(1);
            positions.Add(free);
        }
        return true;
    }
}
