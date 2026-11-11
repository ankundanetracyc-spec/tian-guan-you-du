using Godot;

public partial class Character
{
    private bool _talismanCommitting;
    public double TalismanAttainment => Math.Max(0, human?.talisman_proficiency ?? 0);
    public const float TalismanLearningEnergy = 8;
    public const double TalismanLearningHours = 1;

    public bool TryCraftTalisman(TalismanCraftingService.Request request,
        out TalismanCraftingService.Outcome outcome, out string reason)
    {
        outcome = new(); reason = "";
        if (_talismanCommitting || human is null) { reason = "人物暂不能制作。"; return false; }
        _talismanCommitting = true;
        try
        {
            var quote = TalismanCraftingService.Evaluate(this, request);
            outcome.Quote = quote;
            if (!quote.CanStart) { reason = string.Join("\n", quote.Blockers); return false; }
            var incoming = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var result in quote.PlannedProducts)
            {
                if (!TalismanCraftingService.TrySaveProduct(result.Snapshot, out string path, out var item) || item is null)
                { reason = "成品保存未就绪，未扣材料。"; return false; }
                incoming[path] = incoming.GetValueOrDefault(path) + result.Count;
                outcome.Products.Add(new(path, item, result.Snapshot, result.Count));
            }
            var stagedOutcome = outcome;
            bool committed = try_exchange_inventory(quote.Outgoing, incoming, true, () =>
            {
                if (!can_spend_world_stamina(quote.EnergyCost) || current_health <= 0) return false;
                human.current_world_stamina = Math.Max(0, human.current_world_stamina - quote.EnergyCost);
                foreach (var result in stagedOutcome.Products) human.talisman_product_definitions[result.Path] = result.Item.snapshot_json;
                return true;
            });
            if (!committed) { outcome.Products.Clear(); reason = "材料不足或行囊放不下成品，未扣用料与精力。"; return false; }
            outcome.Summary = quote.Summary;
            outcome.Log.AddRange(quote.ExecutionLog);
            human.talisman_records.Add(crafting_record_date() + quote.Summary +
                $" · 符道{TalismanAttainment:0.##} · {quote.DurationHours:0.##}时辰 · 精力{quote.EnergyCost:0.##}\n" +
                string.Join("\n", quote.ExecutionLog));
            CompleteTalismanWork(quote.DurationHours);
            return true;
        }
        finally { _talismanCommitting = false; }
    }

    public bool TryBuyTalismanBrush(out string reason) => TryBuyTalismanBrush("qingzhu", out reason);
    public bool TryBuyTalismanBrush(string id, out string reason)
    {
        reason = "";
        if (_talismanCommitting || human is null || current_health <= 0 || HasMeta("artifact_battle_clone"))
        { reason = "人物当前不能购置。"; return false; }
        var offer = TalismanCraftingService.BrushOffers().FirstOrDefault(o => o.Id == id);
        if (offer is null) { reason = "未找到这支符笔。"; return false; }
        _talismanCommitting = true;
        try
        {
            if (!TalismanCraftingService.TrySaveProduct(TalismanCraftingService.BrushSnapshot(id), out var path, out var item) || item is null)
            { reason = "符笔资料未就绪。"; return false; }
            if (!try_exchange_inventory(new Dictionary<string, int> { [TalismanCraftingService.SpiritStonePath] = offer.Price },
                    new Dictionary<string, int> { [path] = 1 }, true))
            { reason = "灵石不足或行囊放不下符笔，未扣灵石。"; return false; }
            human.talisman_product_definitions[path] = item.snapshot_json;
            human.talisman_records.Add(crafting_record_date() + "备置" + offer.Name + $" · 灵石{offer.Price}");
            return true;
        }
        finally { _talismanCommitting = false; }
    }

    public bool TryLearnTalismanMethod(string id, out string reason)
    {
        reason = "";
        if (_talismanCommitting || human is null || current_health <= 0 || HasMeta("artifact_battle_clone"))
        { reason = "人物当前不能研习。"; return false; }
        var method = new[] { "paper", "ink", "drawing" }.SelectMany(TalismanCraftingService.Methods).FirstOrDefault(m => m.Id == id);
        if (method is null) { reason = "未找到这项技艺。"; return false; }
        if (!double.IsFinite(TalismanAttainment)) { reason = "符道造诣数据无效。"; return false; }
        if (TalismanCraftingService.IsMethodKnown(this, id)) { reason = "已经掌握此技艺。"; return false; }
        if (TalismanAttainment < method.RequiredProficiency) { reason = $"需要符道造诣{method.RequiredProficiency:0.##}。"; return false; }
        if (!can_spend_world_stamina(TalismanLearningEnergy)) { reason = "当前精力不足。"; return false; }
        _talismanCommitting = true;
        try
        {
            human.current_world_stamina = Math.Max(0, human.current_world_stamina - TalismanLearningEnergy);
            human.talisman_method_ids.Add(id);
            human.talisman_records.Add(crafting_record_date() + "研习" + method.Name + $" · {TalismanLearningHours}时辰 · 精力{TalismanLearningEnergy}");
            CompleteTalismanWork(TalismanLearningHours);
            return true;
        }
        finally { _talismanCommitting = false; }
    }

    private void CompleteTalismanWork(double hours)
    {
        WorldStaminaChanged?.Invoke(this);
        if (!IsInsideTree()) return;
        GetTree().CallGroup("main_scene", "notify_world_stamina_changed", this);
        GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.advance_time((float)(hours * 2), "talisman_crafting");
    }
}
