using Godot;

public static partial class TalismanCraftingService
{
    private sealed class InkStock
    {
        public required string Path;
        public required ProductSnapshot Snapshot;
        public int Remaining;
    }
    private sealed class SheetPlan
    {
        public List<Source> Available = [];
        public List<InkStock> Inks = [];
        public double Load;
        public double Fire;
        public double PaperContactRemaining = 1;
        public double ToughnessDamage;
        public double SettingPenalty;
        public int Tier = -1;
        public int PaperSourceCount;
        public SheetPlan Clone() => new() { Available = new(Available), Inks = new(Inks), Load = Load, Fire = Fire,
            PaperContactRemaining = PaperContactRemaining, ToughnessDamage = ToughnessDamage, SettingPenalty = SettingPenalty,
            Tier = Tier, PaperSourceCount = PaperSourceCount };
    }

    private static void EvaluateDrawing(Character owner, Request request, Quote quote)
    {
        if (!IsRecipeKnown(owner, request.RecipeId)) quote.Blockers.Add("尚未掌握此符谱。");
        if (request.Quantity is < 1 or > 64) quote.Blockers.Add("一次最多准备64张符纸。");
        var writing = Methods("drawing").FirstOrDefault(m => m.Id == request.WritingId);
        if (writing is null || !IsMethodKnown(owner, request.WritingId)) quote.Blockers.Add("尚未掌握此写法。");
        var paper = LoadProduct(request.PaperPath)?.Snapshot;
        var brush = LoadProduct(request.BrushPath)?.Snapshot;
        if (paper?.Kind != "paper" || owner.get_inventory_total_count(request.PaperPath) < request.Quantity)
            quote.Blockers.Add("符纸不足或资料已失效。");
        if (brush?.Kind != "brush" || owner.get_inventory_total_count(request.BrushPath) < 1)
            quote.Blockers.Add("请选择行囊中的符笔。");
        var stocks = new List<InkStock>();
        if (request.InkSelections.Any(s => s.Quantity <= 0)) quote.Blockers.Add("配墨份数须为正整数。");
        foreach (var group in request.InkSelections.GroupBy(s => s.Path, StringComparer.Ordinal))
        {
            long amount = group.Sum(s => (long)s.Quantity);
            var ink = LoadProduct(group.Key)?.Snapshot;
            if (amount is < 1 or > 64 || ink?.Kind != "ink" || amount > owner.get_inventory_total_count(group.Key))
            { quote.Blockers.Add("所选符墨不足或资料已失效。"); continue; }
            stocks.Add(new InkStock { Path = group.Key, Snapshot = ink, Remaining = (int)amount });
        }
        if (stocks.Count == 0) quote.Blockers.Add("请选择符墨。");
        if (stocks.Count > 8) quote.Blockers.Add("本次最多配入八种符墨。");
        if (!quote.CanStart || paper is null || brush is null) return;

        double p = quote.Proficiency / (quote.Proficiency + 40);
        double complexity = Math.Max(0, stocks.Count - 1) + (request.WritingId == "common" ? 0 : 1);
        quote.SpiritNeed = (int)Math.Ceiling(1 + complexity + Math.Max(0, 2.5 - paper.Connection - brush.PenControl));
        double burden = Math.Max(0, complexity - (1 + quote.Proficiency / 12));
        double delivery = Math.Clamp(.64 + .16 * p + .12 * brush.PenDelivery - .04 * burden, .35, .95);
        double writingRetention = request.WritingId switch { "shumo" => .9, "touzhi" => .94, "ninghao" => .83, _ => 1 };
        double spreadLimit = request.WritingId == "shumo" ? 1.08 : .91 + brush.PenControl * .15 + p * .08;
        double capacity = paper.Capacity * (request.WritingId == "touzhi" ? 1.18 : 1);
        double toughness = paper.Toughness + brush.PenControl * .2 + p * .1;
        double connection = paper.Connection + brush.PenDelivery * .2 + p * .08 + (request.WritingId == "yunpu" ? .12 : 0);
        double closing = paper.Retention + brush.PenClosing * .2 + p * .06;
        if (request.WritingId == "touzhi") toughness -= .08;
        if (request.WritingId == "ninghao") { toughness -= .08; capacity *= .9; }

        string mainUtility = request.RecipeId == "hushen" ? "护" : "破";
        string sealUtility = request.RecipeId == "hushen" ? "固" : "藏";
        var products = new Dictionary<int, int>();
        var plans = new List<SheetPlan>();
        bool incompatibleContact = false;
        bool AddInk(SheetPlan plan, InkStock stock)
        {
            double addedLoad = .35 + stock.Snapshot.Concentration * .5;
            double spread = paper.Permeability + stock.Snapshot.Flow * .44;
            if (spread > spreadLimit) { incompatibleContact = true; return false; }
            if (plan.Load + addedLoad > Math.Min(capacity, brush.PenCapacity) + 1e-6) return false;
            // 本张纸的接触份额只用一次。两方向及多行子对共分预算，已用份额不再次结算。
            double contact = Math.Min(plan.PaperContactRemaining, addedLoad / Math.Max(.1, capacity));
            plan.PaperContactRemaining -= contact;
            var relation = Contact(paper, stock.Snapshot);
            double sourceLoss = 0;
            double sourceRecovery = 0;
            sourceLoss += contact * .5 * relation.ForwardOvercomes * .14;
            sourceRecovery += contact * .5 * relation.ForwardGenerates * .07;
            plan.ToughnessDamage += contact * .5 * relation.BackwardOvercomes * .14;
            // 生纸只抵消本张已有纸伤，不给无损纸叠加一份新韧性。
            plan.ToughnessDamage = Math.Max(0, plan.ToughnessDamage - contact * .5 * relation.BackwardGenerates * .06);
            if (plan.Inks.Count > 0)
            {
                // 新墨与既有墨共分另一半调合预算；每次只接触新加入部分。
                int sourceCursor = plan.PaperSourceCount;
                foreach (var old in plan.Inks)
                {
                    var mix = Contact(old.Snapshot, stock.Snapshot);
                    double share = .5 / plan.Inks.Count;
                    sourceLoss += share * mix.ForwardOvercomes * .1;
                    sourceRecovery += share * mix.ForwardGenerates * .04;
                    // 新墨克旧墨，仅损伤尚未封持的在场来源，按本次份额扣一次。
                    double oldLoss = share * mix.BackwardOvercomes * .06;
                    if (oldLoss > 0)
                        for (int i = sourceCursor; i < sourceCursor + old.Snapshot.Sources.Count; i++)
                            plan.Available[i] = plan.Available[i] with { Amount = plan.Available[i].Amount * (1 - oldLoss) };
                    sourceCursor += old.Snapshot.Sources.Count;
                }
            }
            // 附纸只处理这次实际送达损失，凝定只处理收口，不再重复乘成一个万能墨性。
            double attachmentLoss = Math.Max(0, .58 - stock.Snapshot.Attachment) * .18;
            double effectiveDelivery = Math.Clamp(delivery - sourceLoss - attachmentLoss + sourceRecovery, .25, .98);
            plan.SettingPenalty += addedLoad / Math.Max(.1, capacity) * Math.Max(0, .58 - stock.Snapshot.Setting) * .18;
            plan.Inks.Add(stock); plan.Load += addedLoad;
            plan.Available.AddRange(stock.Snapshot.Sources.Select(s => s with { Amount = s.Amount * effectiveDelivery * writingRetention }));
            plan.Fire += stock.Snapshot.ElementShares.Where(e => e.Name == "火").Sum(e => e.Amount);
            var afterWriting = new List<Source>(plan.Available);
            bool writingReady = request.WritingId switch
            {
                "ninghao" => PaySources(afterWriting, ["聚"], .02),
                "touzhi" => PaySources(afterWriting, ["通", "行"], .02),
                _ => true
            };
            plan.Tier = writingReady ? AchievableTier(afterWriting, mainUtility, sealUtility, connection, closing - plan.SettingPenalty,
                toughness - plan.ToughnessDamage, request.RecipeId != "yanshi" || plan.Fire >= .08) : -1;
            return true;
        }
        // 第一遍锁定本批能完成的全部基础包；此时绝不为了第一张升档先吃后续纸的墨。
        for (int sheet = 0; sheet < request.Quantity; sheet++)
        {
            var plan = new SheetPlan { Available = new(paper.Sources), PaperSourceCount = paper.Sources.Count,
                Fire = paper.ElementShares.Where(e => e.Name == "火").Sum(e => e.Amount) };
            // 来源不足时不落笔；同能力墨的排序不依显示名或加入先后。
            foreach (var stock in stocks.Where(s => s.Remaining > 0)
                         .OrderByDescending(s => s.Snapshot.Sources.Where(v => v.Name == mainUtility).Sum(v => v.Amount))
                         .ThenBy(s => s.Path, StringComparer.Ordinal))
            {
                for (int n = 0; n < Math.Min(stock.Remaining, 4); n++)
                {
                    if (!AddInk(plan, stock)) break;
                    if (plan.Tier >= 0) break;
                }
                if (plan.Tier >= 0) break;
            }
            if (plan.Tier < 0)
            {
                if (plans.Count == 0)
                {
                    quote.Blockers.Add(incompatibleContact ? "纸墨容易洇散，可改用束墨留痕或更合适的纸墨。" :
                        request.WritingId == "ninghao" ? "聚势、主纹或收口支持不足，请检查聚的真实余蕴与纸笔承墨。" :
                        request.WritingId == "touzhi" ? "透纸所需通行、主纹或收口支持不足。" :
                        "主纹或收口支持不足，请补足相应符墨并检查纸笔承墨。");
                }
                else quote.Notes.Add("余墨不足下一张完整主纹与收口，未启用的纸墨保留。");
                break;
            }
            foreach (var used in plan.Inks) used.Remaining--;
            plans.Add(plan);
        }
        if (!quote.CanStart) return;
        // 第二遍才允许余墨聚势。试配不升既有档则释放，绝不挪用已经锁定的基础包。
        if (request.WritingId == "ninghao")
        {
            foreach (var plan in plans)
            {
                var trial = plan.Clone();
                var extra = new List<InkStock>();
                foreach (var stock in stocks.Where(s => s.Remaining > 0)
                             .OrderByDescending(s => s.Snapshot.Sources.Where(v => v.Name == mainUtility).Sum(v => v.Amount))
                             .ThenBy(s => s.Path, StringComparer.Ordinal))
                    for (int n = 0; n < Math.Min(stock.Remaining, 4); n++)
                    { if (!AddInk(trial, stock)) break; extra.Add(stock); }
                if (trial.Tier <= plan.Tier) continue;
                foreach (var ink in extra) ink.Remaining--;
                plan.Available = trial.Available; plan.Inks = trial.Inks; plan.Tier = trial.Tier;
                plan.Load = trial.Load; plan.ToughnessDamage = trial.ToughnessDamage;
                plan.SettingPenalty = trial.SettingPenalty;
            }
        }
        int finished = plans.Count, opened = plans.Sum(p => p.Inks.Count);
        if (finished == 0) { quote.Blockers.Add("纸墨未能组成完整的基础用料。"); return; }
        foreach (var plan in plans)
        {
            foreach (var ink in plan.Inks) quote.Outgoing[ink.Path] = quote.Outgoing.GetValueOrDefault(ink.Path) + 1;
            products[plan.Tier] = products.GetValueOrDefault(plan.Tier) + 1;
            string writingCost = request.WritingId switch { "ninghao" => "聚势另实付聚0.02份。", "touzhi" => "透纸另实付通或行共0.02份。", _ => "" };
            quote.ExecutionLog.Add($"第{plans.IndexOf(plan) + 1}张：完整预留主纹、连接及收口；开启{plan.Inks.Count}份墨，成符{plan.Tier + 1}档。{writingCost}纸墨接触仅结算一次，纸伤{plan.ToughnessDamage:0.######}。开启份的剩余来源随本张收束，不回填原瓶。");
        }
        quote.Outgoing[request.PaperPath] = finished;
        foreach (var product in products.OrderBy(p => p.Key))
            quote.PlannedProducts.Add((new ProductSnapshot { Kind = "talisman", RecipeId = request.RecipeId, Tier = product.Key }, product.Value));
        quote.ExpectedCount = finished;
        double work = request.WritingId switch { "yunpu" => 1.25, "shumo" => 1.15, "ninghao" => 1.3, _ => 1 };
        quote.DurationHours = Round((.18 + finished * .17 + opened * .055 + complexity * .06) * work * (1 - .28 * p));
        quote.EnergyCost = (float)Math.Ceiling((1 + finished * 1.4 + opened * .35 + complexity * .6) * work * (1 - .24 * p));
        quote.Summary = $"制得{RecipeList.First(r => r.Id == request.RecipeId).Name}{finished}张。";
        if (request.WritingId == "ninghao") quote.Notes.Add("基础收口已预留，聚势只使用本张可调用余蕴。");
    }

    private static bool PaySources(List<Source> sources, IReadOnlyList<string> names, double amount)
    {
        if (sources.Where(s => names.Contains(s.Name)).Sum(s => s.Amount) + 1e-8 < amount) return false;
        // 先用较低真实层，完整扣除技法支出后才比较主纹与收口，绝不再把同一份给主效。
        foreach (int index in Enumerable.Range(0, sources.Count).Where(i => names.Contains(sources[i].Name))
                     .OrderBy(i => sources[i].Grade).ThenBy(i => i))
        {
            double used = Math.Min(amount, sources[index].Amount);
            sources[index] = sources[index] with { Amount = sources[index].Amount - used };
            amount -= used;
            if (amount <= 1e-8) break;
        }
        return true;
    }

    private static (double ForwardGenerates, double ForwardOvercomes, double BackwardGenerates, double BackwardOvercomes)
        Contact(ProductSnapshot from, ProductSnapshot to)
    {
        double fromTotal = from.ElementShares.Sum(e => e.Amount), toTotal = to.ElementShares.Sum(e => e.Amount);
        if (fromTotal <= 0 || toTotal <= 0) return (0, 0, 0, 0);
        double fg = 0, fo = 0, bg = 0, bo = 0;
        foreach (var a in from.ElementShares)
        foreach (var b in to.ElementShares)
        {
            double share = a.Amount / fromTotal * b.Amount / toTotal;
            if (Generates(a.Name, b.Name)) fg += share;
            if (Overcomes(a.Name, b.Name)) fo += share;
            if (Generates(b.Name, a.Name)) bg += share;
            if (Overcomes(b.Name, a.Name)) bo += share;
        }
        return (fg, fo, bg, bo);
    }

    private static int AchievableTier(IEnumerable<Source> source, string main, string seal,
        double connection, double closing, double toughness, bool elementReady)
    {
        if (!elementReady) return -1;
        var values = source.ToArray();
        double[] needs = [1, 1.45, 2, 2.8, 3.7];
        for (int tier = 4; tier >= 0; tier--)
        {
            int grade = tier switch { 4 => 3, >= 2 => 2, _ => 1 };
            double mainAmount = values.Where(s => s.Name == main && s.Grade >= grade).Sum(s => s.Amount);
            double sealAmount = values.Where(s => s.Name == seal && s.Grade >= grade).Sum(s => s.Amount);
            if (mainAmount + 1e-6 < .085 * needs[tier] || sealAmount + 1e-6 < .025 * needs[tier]) continue;
            if (connection < .35 + tier * .055 || closing < .4 + tier * .045 || toughness < .38 + tier * .055) continue;
            return tier;
        }
        return -1;
    }
}
