using Godot;

/// <summary>使用真实空间库存的炼器验收夹具；投料必须能摆入，不创建堆叠或无占地的假物品。</summary>
public static class ForgingSmokeFixtures
{
    public const string Stone = "res://resources/entities/items/灵石.tres";
    public static ProcessForgingTrial.Draft SmallDraft(ProcessForgingTrial model, int index = 1)
    {
        var draft = model.ExampleDraft(index);
        foreach (var input in draft.Inputs) input.Quantity = 1;
        return draft;
    }
    public static Dictionary<string, int> Materials(ProcessForgingTrial model, ProcessForgingTrial.Draft draft, int copies = 1) =>
        draft.Inputs.GroupBy(i => i.Material).ToDictionary(g => model.Materials[g.Key].ResourcePath, g => g.Sum(i => i.Quantity) * copies);
    public static ProcessForgingTrial.Draft ArtifactDraft(ProcessForgingTrial model)
    {
        // 仅为验收配置既有材料的合法投料，保留真实公式结果，不强加主动能力。
        var draft = SmallDraft(model, 3);
        foreach (var input in draft.Inputs)
            if (input.Role == "质材") { input.Material = "灵石"; input.Quantity = 8; }
            else if (input.Role == "和材" && input.Material == "褐铜砂") input.Material = "灵泉露";
            else if (input.Role == "和材" && input.Material == "灵石") input.Quantity = 8;
        return draft;
    }
    public static void Restock(Character actor, ProcessForgingTrial model, ProcessForgingTrial.Draft draft, int copies = 1)
    {
        if (copies == 0) return;
        if (!actor.try_exchange_inventory(new Dictionary<string, int>(), Materials(model, draft, copies)))
            throw new Exception("测试投料不适配真实行囊，请减小炉次或先释放空间。");
        if (actor.get_spatial_inventory().Any(i => !i.IsPlaced)) throw new Exception("测试投料未实际摆入行囊。");
    }
    public static Character Actor(ProcessForgingTrial model, ProcessForgingTrial.Draft draft, int copies = 1)
    {
        var actor = new Character { entity_data = new HumanData {
            base_forging_proficiency = draft.Proficiency, forging_proficiency = draft.Proficiency,
            current_world_stamina = 100, max_spirit_perception = 10000, current_health = 100, max_health = 100 } };
        try { Restock(actor, model, draft, copies); return actor; }
        catch { actor.Free(); throw; }
    }
    public static void FillWithStones(Character actor)
    {
        for (int i = 0; i < Character.InventorySlotCapacity && actor.add_item_to_inventory(Stone); i++) { }
        if (actor.get_inventory_occupied_cell_count() != Character.InventorySlotCapacity)
            throw new Exception("满包测试没有填满实际占格。");
    }
}
