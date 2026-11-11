using Godot;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

/// <summary>20261109 首轮候选规则。结果快照冻结能力，造诣只参与本次加工和驾驭。</summary>
public static partial class TalismanCraftingService
{
    public const int RulesVersion = 20261109;
    public const string SpiritStonePath = "res://resources/entities/items/灵石.tres";
    public const int BrushPrice = 2;
    public const float LearningEnergy = 8;
    public const double LearningHours = 1;
    private const string ProductRoot = "user://talisman/products/";
    private static readonly string[] Utilities = ["固", "聚", "通", "化", "引", "破", "护", "镇", "生", "明", "藏", "行"];
    private static readonly string[] Elements = ["金", "木", "水", "火", "土"];
    private static readonly Dictionary<string, TalismanProductItemData> DefinitionCache = new(StringComparer.Ordinal);

    private static int ProductStorageVolume(ProductSnapshot snapshot) => snapshot.Kind switch
    {
        "brush" => 2,
        "paper" or "ink" or "talisman" => 1,
        _ => throw new ArgumentException("符箓产物缺少实体体积分类：" + snapshot.Kind)
    };

    public sealed class Request
    {
        public string Mode { get; set; } = "paper";
        public List<TalismanCraftingCatalog.Selection> Materials { get; set; } = [];
        public string TechniqueId { get; set; } = "yangmai";
        public List<string> RetainedUtilities { get; set; } = [];
        public string RecipeId { get; set; } = "hushen";
        public string PaperPath { get; set; } = "";
        public List<TalismanCraftingCatalog.Selection> InkSelections { get; set; } = [];
        public string BrushPath { get; set; } = "";
        public int Quantity { get; set; } = 1;
        public string WritingId { get; set; } = "common";
    }

    public sealed record Source(string Name, int Grade, double Amount);
    public sealed record ElementShare(string Name, double Amount);
    public sealed class ProductSnapshot
    {
        public int Version { get; set; } = RulesVersion;
        public string Kind { get; set; } = "";
        public int SpiritGrade { get; set; }
        public double Capacity { get; set; }
        public double Permeability { get; set; }
        public double Toughness { get; set; }
        public double Concentration { get; set; }
        public double Flow { get; set; }
        public double Attachment { get; set; }
        public double Setting { get; set; }
        public double Connection { get; set; }
        public double Retention { get; set; }
        public List<Source> Sources { get; set; } = [];
        public List<Source> StructureSources { get; set; } = [];
        public List<ElementShare> ElementShares { get; set; } = [];
        public List<string> Elements { get; set; } = [];
        public double InputSourceBudget { get; set; }
        public double ProcessingSpent { get; set; }
        public double LostSourceBudget { get; set; }
        public string RecipeId { get; set; } = "";
        public int Tier { get; set; }
        public double PenCapacity { get; set; }
        public double PenControl { get; set; }
        public double PenDelivery { get; set; }
        public double PenClosing { get; set; }
        public string BrushId { get; set; } = "";
    }

    public sealed record ProductEntry(string Path, TalismanProductItemData Item, ProductSnapshot Snapshot, int Count);
    public sealed record ProductResult(string Path, TalismanProductItemData Item, ProductSnapshot Snapshot, int Count);
    public sealed record MethodInfo(string Id, string Name, string Description, string Tradeoff,
        double RequiredProficiency, bool DefaultKnown);
    public sealed record RecipeInfo(string Id, string Name, string Description);
    public sealed record BrushOffer(string Id, string Name, string Description, int Price);
    public static IReadOnlyList<BrushOffer> BrushOffers() =>
    [new("qingzhu", "青竹符笔", "承墨、运锋与收锋均衡。", 2),
        new("jinghao", "静毫符笔", "控墨与收锋较好，承墨略少。", 5),
        new("cangfeng", "藏锋符笔", "承墨较多，适合铺陈长纹。", 5)];
    public sealed class Quote
    {
        public bool CanStart => Blockers.Count == 0;
        public List<string> Blockers { get; } = [];
        public List<string> Notes { get; } = [];
        public double Proficiency { get; internal set; }
        public float EnergyCost { get; internal set; }
        public double DurationHours { get; internal set; }
        public int SpiritNeed { get; internal set; }
        public int ExpectedCount { get; internal set; }
        public string Summary { get; internal set; } = "";
        public double SourceInputBudget { get; internal set; }
        public double SourceProcessingSpent { get; internal set; }
        public double SourceLostBudget { get; internal set; }
        public double SourceRetainedTotal { get; internal set; }
        internal Dictionary<string, int> Outgoing { get; } = new(StringComparer.Ordinal);
        internal List<(ProductSnapshot Snapshot, int Count)> PlannedProducts { get; } = [];
        internal List<string> ExecutionLog { get; } = [];
    }
    public sealed class Outcome
    {
        public List<ProductResult> Products { get; } = [];
        public Quote Quote { get; internal set; } = new();
        public List<string> Log { get; } = [];
        public string Summary { get; internal set; } = "";
    }

    private static readonly MethodInfo[] PaperMethods =
    [
        new("yangmai", "养脉", "舒展纸脉，兼顾成纸与余蕴。", "承墨与韧性取平衡。", 0, true),
        new("ningwen", "凝纹", "凝实纸纹，减少洇散。", "承墨略少，用时较长。", 8, false),
        new("diemai", "叠脉", "叠合纸脉，增加承墨与韧性。", "同批成纸较少。", 18, false)
    ];
    private static readonly MethodInfo[] InkMethods =
    [
        new("junyan", "均研", "匀开墨源，兼顾用量与流动。", "浓墨仍需合适纸笔。", 0, true),
        new("chengling", "澄灵", "澄去结滞，改善铺展与附纸。", "弃去部分墨料，用时较长。", 8, false),
        new("juyun", "聚蕴", "收配墨液，集中已经取得的墨蕴。", "成墨较少，承墨负担较重。", 18, false)
    ];
    private static readonly MethodInfo[] WritingMethods =
    [
        new("common", "常法", "依符谱行纹，均衡用墨与收口。", "按纸墨现有条件成符。", 0, true),
        new("shumo", "束墨留痕", "收束落墨，抑制易洇的墨迹。", "多耗少量墨与工时。", 8, false),
        new("touzhi", "透纸行墨", "借纸脉入墨，适合较密纸材。", "更考验纸的韧性。", 12, false),
        new("yunpu", "匀铺长行", "匀送墨源，接续复杂纹路。", "需要更多工时。", 15, false),
        new("ninghao", "凝毫聚势", "集中余蕴，尝试更高符效档。", "多耗墨，纸笔负担增加。", 25, false)
    ];
    private static readonly RecipeInfo[] RecipeList =
    [new("hushen", "护身符", "以护持主纹护身，固纹收口。"), new("yanshi", "炎矢符", "以火行破势成矢，藏意收符。")];
    public static IReadOnlyList<MethodInfo> Methods(string mode) => mode switch
    { "paper" => PaperMethods, "ink" => InkMethods, "drawing" => WritingMethods, _ => [] };
    public static IReadOnlyList<RecipeInfo> Recipes() => RecipeList;
    public static bool IsRecipeKnown(Character? owner, string id) => owner?.entity_data is HumanData human &&
        RecipeList.Any(r => r.Id == id) && (id is "hushen" or "yanshi" || human.talisman_recipe_ids.Contains(id));
    public static bool IsMethodKnown(Character? owner, string id) => owner?.entity_data is HumanData human &&
        PaperMethods.Concat(InkMethods).Concat(WritingMethods).FirstOrDefault(m => m.Id == id) is { } method &&
        (method.DefaultKnown || human.talisman_method_ids.Contains(id));

    public static ProductSnapshot? ReadSnapshot(string json)
    {
        try { return string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<ProductSnapshot>(json); }
        catch (JsonException) { return null; }
    }
    public static bool ValidateSnapshot(ProductSnapshot value) => value.Version == RulesVersion &&
        value.Kind is "paper" or "ink" or "brush" or "talisman" && value.SpiritGrade is >= 0 and <= 9 &&
        value.Tier is >= 0 and <= 4 && value.Sources is not null && value.StructureSources is not null &&
        value.ElementShares is not null && value.Elements is not null &&
        value.Sources.Concat(value.StructureSources).All(s => s is not null && Utilities.Contains(s.Name) && s.Grade is >= 1 and <= 9 &&
            double.IsFinite(s.Amount) && s.Amount >= 0) &&
        value.Sources.Select(s => (s.Name, s.Grade)).Distinct().Count() == value.Sources.Count &&
        value.StructureSources.Select(s => (s.Name, s.Grade)).Distinct().Count() == value.StructureSources.Count &&
        new[] { value.Capacity, value.Permeability, value.Toughness, value.Concentration, value.Flow, value.Attachment,
            value.Setting, value.Connection, value.Retention, value.InputSourceBudget, value.ProcessingSpent,
            value.LostSourceBudget, value.PenCapacity, value.PenControl, value.PenDelivery, value.PenClosing }
            .All(n => double.IsFinite(n) && n >= 0) &&
        Math.Abs(value.Sources.Sum(s => s.Amount) + value.StructureSources.Sum(s => s.Amount) + value.ProcessingSpent + value.LostSourceBudget - value.InputSourceBudget) <= .0002 &&
        (value.Kind != "ink" || value.Sources.Select(s => s.Name).Distinct().Count() <= 4) &&
        value.ElementShares.All(s => s is not null && Elements.Contains(s.Name) && double.IsFinite(s.Amount) && s.Amount > 0) &&
        value.Elements.SequenceEqual(value.ElementShares.Select(s => s.Name)) && value.Elements.Distinct().Count() == value.Elements.Count &&
        (value.Kind is not ("paper" or "ink") || value.SpiritGrade == SummaryGrade(value.Sources.Concat(value.StructureSources))) &&
        (value.Kind != "paper" || value.Capacity > 0) &&
        (value.Kind != "talisman" || RecipeList.Any(r => r.Id == value.RecipeId));

    public static IReadOnlyList<KeyValuePair<string, string>> PlayerFields(ProductSnapshot snapshot)
    {
        var spirit = snapshot.SpiritGrade == 0 ? "无品" : MaterialCraftData.utility_grade_name(snapshot.SpiritGrade);
        return snapshot.Kind switch
        {
            "paper" => [new("灵性", spirit), new("承墨量", Level(snapshot.Capacity, 1.25, 1.85, "少", "中", "多")),
                new("渗墨性", Level(snapshot.Permeability, .38, .68)), new("韧性", Level(snapshot.Toughness, .43, .73, "偏弱", "适中", "较强"))],
            "ink" => [new("功用", string.Join("、", snapshot.Sources.Select(s => s.Name).Distinct())), new("灵性", spirit),
                new("浓淡", Level(snapshot.Concentration, .42, .73, "淡", "适中", "浓")), new("流动性", Level(snapshot.Flow, .38, .68))],
            "brush" => [new("承墨", Level(snapshot.PenCapacity, 1.3, 1.9)), new("控墨", Level(snapshot.PenControl, .4, .7)),
                new("运锋", Level(snapshot.PenDelivery, .4, .7)), new("收锋", Level(snapshot.PenClosing, .4, .7))],
            _ => [new("符效", (snapshot.Tier + 1) + "档")]
        };
    }
    public static IReadOnlyList<KeyValuePair<string, string>> DisplayFields(ProductSnapshot snapshot) => PlayerFields(snapshot);
    private static string Level(double value, double low, double high, string a = "偏低", string b = "适中", string c = "偏高") => value < low ? a : value > high ? c : b;

    public static IReadOnlyList<ProductEntry> OwnedProducts(Character? owner, string kind)
    {
        if (owner?.entity_data is not HumanData human) return [];
        RestoreDefinitions(human);
        var result = new List<ProductEntry>();
        foreach (var path in human.inventory_item_paths.Distinct(StringComparer.Ordinal))
        {
            var item = LoadProduct(path);
            if (item?.Snapshot is not { } value || !ValidateSnapshot(value) || value.Kind != kind) continue;
            int count = owner.get_inventory_total_count(path);
            if (count > 0) result.Add(new ProductEntry(path, item, value, count));
        }
        return result.OrderBy(v => v.Item.entity_name, StringComparer.Ordinal).ThenBy(v => v.Path, StringComparer.Ordinal).ToArray();
    }
    public static TalismanProductItemData? LoadProduct(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        if (DefinitionCache.TryGetValue(path, out var cached) && GodotObject.IsInstanceValid(cached))
        {
            if (cached.Snapshot is not { } existing || !ValidateSnapshot(existing)) return null;
            cached.storage_volume = ProductStorageVolume(existing);
            TalismanArtCatalog.ApplyIcon(cached, existing);
            return cached;
        }
        if (!ResourceLoader.Exists(path)) return null;
        var value = ResourceLoader.Load(path) as TalismanProductItemData;
        if (value?.Snapshot is not { } snapshot || !ValidateSnapshot(snapshot)) return null;
        value.storage_volume = ProductStorageVolume(snapshot);
        TalismanArtCatalog.ApplyIcon(value, snapshot);
        DefinitionCache[path] = value;
        TalismanCraftingCatalog.RegisterProductDefinition(path);
        return value;
    }

    public static Quote Evaluate(Character? owner, Request request)
    {
        var quote = new Quote();
        if (owner?.entity_data is not HumanData human || owner.current_health <= 0 || owner.HasMeta("artifact_battle_clone"))
        { quote.Blockers.Add("人物当前不能制作。"); return quote; }
        quote.Proficiency = Math.Max(0, human.talisman_proficiency);
        if (!double.IsFinite(quote.Proficiency)) { quote.Blockers.Add("符道造诣数据无效。"); return quote; }
        RestoreDefinitions(human);
        if (request.Mode == "drawing") EvaluateDrawing(owner, request, quote);
        else EvaluateMaterial(owner, request, quote);
        if (quote.CanStart && !owner.can_spend_world_stamina(quote.EnergyCost)) quote.Blockers.Add("当前精力不足。");
        if (quote.CanStart && quote.SpiritNeed > Math.Max(0, owner.max_spirit_perception - owner.used_spirit_perception))
            quote.Blockers.Add("当前可用灵识不足。");
        return quote;
    }

    private sealed record BatchMaterial(TalismanCraftingCatalog.MaterialEntry Material, double Quantity);
    private static void EvaluateMaterial(Character owner, Request request, Quote quote)
    {
        var analysis = TalismanCraftingCatalog.Analyze(request.Mode, request.Materials, owner,
            request.Mode == "ink" ? request.RetainedUtilities : null);
        quote.Blockers.AddRange(analysis.Blockers);
        quote.Notes.AddRange(analysis.Notes.Select(n => n.Text));
        if (analysis.TotalQuantity > 64) quote.Blockers.Add("一批最多投入64份材料。");
        var method = Methods(request.Mode).FirstOrDefault(m => m.Id == request.TechniqueId);
        if (method is null) quote.Blockers.Add("请选择本工艺的技艺。");
        else if (!IsMethodKnown(owner, method.Id)) quote.Blockers.Add("尚未研习" + method.Name + "。");
        if (!quote.CanStart) return;

        var catalog = TalismanCraftingCatalog.DiscoverMaterials().ToDictionary(m => m.Path, StringComparer.Ordinal);
        foreach (var s in request.Materials.GroupBy(v => v.Path, StringComparer.Ordinal)) quote.Outgoing[s.Key] = s.Sum(v => v.Quantity);
        // 同签名先合并，原料身份、名称、加入顺序均不影响加工。
        var batch = quote.Outgoing.Select(s => new BatchMaterial(catalog[s.Key], s.Value))
            .GroupBy(m => TalismanCraftingCatalog.Signature(m.Material), StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => new BatchMaterial(g.First().Material, g.Sum(m => m.Quantity))).ToArray();
        var p = quote.Proficiency / (quote.Proficiency + 40);
        double total = batch.Sum(m => m.Quantity);
        double fiber = batch.Where(m => m.Material.Texture == "纤维").Sum(m => m.Quantity);
        double liquid = batch.Where(m => m.Material.Texture == "液体").Sum(m => m.Quantity);
        double glue = batch.Where(m => m.Material.Texture == "黏胶").Sum(m => m.Quantity);
        double solid = batch.Where(m => m.Material.Texture is "致密" or "颗粒").Sum(m => m.Quantity);
        double complexity = Math.Max(0, batch.Length - 2) + Math.Max(0, analysis.AvailableUtilities.Count - 3) * .45;
        double burden = Math.Max(0, complexity - (1 + quote.Proficiency / 12));
        double methodWork = request.TechniqueId switch { "ningwen" => 1.2, "diemai" => 1.35, "chengling" => 1.3, "juyun" => 1.25, _ => 1 };
        quote.DurationHours = Round((.22 + total * .11 + complexity * .08) * methodWork * (1 - .28 * p));
        quote.EnergyCost = (float)Math.Ceiling((2 + total * .55 + complexity * .8) * methodWork * (1 - .24 * p));
        quote.SpiritNeed = (int)Math.Ceiling(1 + complexity * .8 + Math.Max(0, total - 8) * .15);
        if (burden > 0) quote.Notes.Add("配伍较繁，当前造诣会多损一些余蕴。");

        double yieldScale = request.TechniqueId switch { "diemai" => .65, "chengling" => .8, "juyun" => .65, _ => 1 };
        int count = Math.Clamp((int)Math.Floor((request.Mode == "paper" ? fiber : liquid + solid) * yieldScale + 1e-8), 1, 64);
        var retained = request.Mode == "ink" ? request.RetainedUtilities.Distinct().ToHashSet(StringComparer.Ordinal) : Utilities.ToHashSet(StringComparer.Ordinal);
        if (request.Mode == "ink" && retained.Count == 0)
        { quote.Blockers.Add("请选择成墨保留的功用（最多四项）。"); return; }
        var sources = new List<Source>();
        var structureSources = new List<Source>();
        var elementAmounts = new Dictionary<string, double>(StringComparer.Ordinal);
        double processing = 0, loss = 0, budget = 0, retainedFiber = 0;
        foreach (var group in batch)
        {
            var utilityLayers = group.Material.UtilityLayers;
            double unitBudget = utilityLayers.Count == 0 ? 0 : group.Quantity;
            budget += unitBudget;
            // 工具先取得基础来源，功用只从已经取得的部分支出，不能提前帮助自身取得。
            double basic = group.Material.Texture switch { "致密" => .62, "颗粒" => .75, "液体" => .9, _ => .82 };
            double initial = unitBudget * basic;
            double spent = initial * .1;
            processing += spent;
            double processPerUtility = utilityLayers.Count == 0 ? 0 : spent / utilityLayers.Count;
            double helper = utilityLayers.Where(u => u.Name is "破" or "化" or "明" or "行")
                .Sum(_ => processPerUtility) / Math.Max(1, group.Quantity);
            double generate = 0, overcome = 0;
            foreach (var other in batch)
            {
                if (ReferenceEquals(other, group) || other.Material.Elements.Count == 0 || group.Material.Elements.Count == 0) continue;
                // 所有接触对子共分这批一次接触预算；多行、更多材料不复制满额生克。
                double contact = other.Quantity / total * group.Quantity / total;
                foreach (var from in other.Material.Elements)
                foreach (var to in group.Material.Elements)
                {
                    double share = contact / (other.Material.Elements.Count * group.Material.Elements.Count);
                    if (Generates(from, to)) generate += share;
                    if (Overcomes(from, to)) overcome += share;
                }
            }
            double acquire = Math.Clamp(basic + .13 * p + Math.Min(.06, helper) + .16 * generate - .26 * overcome - .025 * burden, .35, .97);
            double acquiredAfterProcessing = Math.Max(0, unitBudget * acquire - spent);
            // 澄灵会舍弃携源残余；聚蕴的去液也携走真实来源。
            double methodRetain = request.TechniqueId switch { "chengling" => .86, "juyun" => .9, _ => 1 };
            acquiredAfterProcessing *= methodRetain;
            if (group.Material.Texture == "纤维") retainedFiber += group.Quantity * acquire * methodRetain;
            double structuralShare = request.Mode == "paper" ? .43 : .12;
            double structure = acquiredAfterProcessing * structuralShare;
            double callable = acquiredAfterProcessing - structure;
            loss += unitBudget - spent - structure - callable;
            foreach (var utility in utilityLayers)
            {
                double a = callable / utilityLayers.Count;
                structureSources.Add(new Source(utility.Name, utility.Grade, structure / utilityLayers.Count / count));
                if (retained.Contains(utility.Name)) sources.Add(new Source(utility.Name, utility.Grade, a / count));
                else loss += a;
            }
            // 只有实际留下的物质/来源进入五行，不把加工流失物的标签复写给成品。
            double keptMass = request.Mode == "paper"
                ? group.Material.Texture == "纤维" ? group.Quantity * acquire : group.Material.Texture == "黏胶" ? group.Quantity * .65 * acquire : acquiredAfterProcessing * .25
                : group.Quantity * acquire * methodRetain;
            foreach (var element in group.Material.Elements)
                elementAmounts[element] = elementAmounts.GetValueOrDefault(element) + keptMass / group.Material.Elements.Count / count;
        }
        var sourceList = MergeSources(sources);
        var structural = MergeSources(structureSources);
        double Structural(string utility) => structural.Where(s => s.Name == utility).Sum(s => s.Amount);
        var snapshot = new ProductSnapshot
        {
            Kind = request.Mode, Sources = sourceList, StructureSources = structural,
            // 制作经过在批次记录保存；成材签名只冻结真实结余能力，不把制造履历加入堆叠键。
            InputSourceBudget = Round(sourceList.Sum(s => s.Amount) + structural.Sum(s => s.Amount)),
            ElementShares = elementAmounts.Where(v => v.Value > .00001).OrderBy(v => Array.IndexOf(Elements, v.Key))
                .Select(v => new ElementShare(v.Key, Round(v.Value))).ToList()
        };
        snapshot.Elements = snapshot.ElementShares.Select(e => e.Name).ToList();
        if (request.Mode == "paper")
        {
            snapshot.Capacity = Round(Math.Max(.45, retainedFiber / count * 1.3 + Structural("聚") * .25 + Structural("通") * .4 - glue / count * .18));
            snapshot.Permeability = Round(Math.Clamp(.64 - glue / Math.Max(1, fiber) * .28 - Structural("固") * .16 + Structural("通") * .2 + Structural("行") * .1, .1, .9));
            snapshot.Toughness = Round(Math.Clamp(.4 + retainedFiber / count * .09 + Structural("固") * .4 + Structural("护") * .35 + Structural("生") * .15 + glue / count * .14 - burden * .025, .15, .95));
            if (request.TechniqueId == "ningwen") { snapshot.Capacity = Round(snapshot.Capacity * .88); snapshot.Permeability = Round(snapshot.Permeability * .72); snapshot.Toughness = Round(Math.Min(.98, snapshot.Toughness + .06 + .04 * p)); }
            snapshot.Connection = Round(Math.Clamp(.46 + Structural("通") * .45 + Structural("引") * .3 + Structural("行") * .3 + p * .1, .2, .95));
            snapshot.Retention = Round(Math.Clamp(.5 + Structural("固") * .35 + Structural("藏") * .5 + Structural("护") * .2, .2, .95));
        }
        else
        {
            snapshot.Concentration = Round(Math.Clamp((solid + liquid * .42) / Math.Max(.1, liquid + solid * .6), .15, .95));
            if (request.TechniqueId == "juyun") snapshot.Concentration = Round(Math.Min(.98, snapshot.Concentration + .18));
            snapshot.Flow = Round(Math.Clamp(.35 + liquid / Math.Max(1, solid + liquid) * .34 - glue / Math.Max(1, total) * .45 + Structural("化") * .3 + Structural("行") * .25 + Structural("通") * .2, .12, .92));
            snapshot.Attachment = Round(Math.Clamp(.55 + glue / total * .4 + Structural("固") * .5, .25, .95));
            snapshot.Setting = Round(Math.Clamp(.52 + Structural("固") * .3 + Structural("镇") * .4, .2, .95));
            if (request.TechniqueId == "chengling") { snapshot.Flow = Round(Math.Min(.95, snapshot.Flow + .12)); snapshot.Attachment = Round(Math.Min(.95, snapshot.Attachment + .08)); }
        }
        snapshot.SpiritGrade = SummaryGrade(snapshot.Sources.Concat(snapshot.StructureSources));
        quote.SourceInputBudget = Round(budget);
        quote.SourceProcessingSpent = Round(processing);
        quote.SourceRetainedTotal = Round((snapshot.Sources.Sum(s => s.Amount) + snapshot.StructureSources.Sum(s => s.Amount)) * count);
        // 以最终均分精度记余差，全部去向只记一次；分份精度不扩大来源预算。
        quote.SourceLostBudget = Round(budget - processing - quote.SourceRetainedTotal);
        quote.ExpectedCount = count;
        quote.PlannedProducts.Add((snapshot, count));
        quote.Summary = request.Mode == "paper" ? $"制得符纸{count}张。" : $"制得符墨{count}份。";
        quote.ExecutionLog.Add($"取得与加工：来源预算{budget:0.######}，加工支出{processing:0.######}。");
        quote.ExecutionLog.Add($"分成{count}份，调用来源{snapshot.Sources.Sum(s => s.Amount) * count:0.######}，结构来源{snapshot.StructureSources.Sum(s => s.Amount) * count:0.######}，损失{loss:0.######}。");
        if (!ValidateSnapshot(snapshot)) quote.Blockers.Add("成材来源校验未通过，未扣材料。");
        if (quote.SourceLostBudget < -.0002 || Math.Abs(quote.SourceInputBudget - quote.SourceProcessingSpent -
            quote.SourceRetainedTotal - quote.SourceLostBudget) > .0002)
            quote.Blockers.Add("批次来源校验未通过，未扣材料。");
    }

    internal static List<Source> MergeSources(IEnumerable<Source> values) => values.GroupBy(s => (s.Name, s.Grade))
        .Select(g => new Source(g.Key.Name, g.Key.Grade, Round(g.Sum(s => s.Amount))))
        .Where(s => s.Amount > .000001).OrderBy(s => Array.IndexOf(Utilities, s.Name)).ThenBy(s => s.Grade).ToList();
    private static int SummaryGrade(IEnumerable<Source> sources)
    {
        var values = sources.ToArray();
        // 不平均品级，也不取一点高品洗整批；达到一份灵性摘要阈值的最高真实层次。
        for (int grade = 9; grade >= 1; grade--) if (values.Where(s => s.Grade >= grade).Sum(s => s.Amount) >= .2 - 1e-6) return grade;
        return 0;
    }
    internal static double Round(double value) => Math.Round(value, 6, MidpointRounding.AwayFromZero);
    private static bool Generates(string from, string to) => (from, to) is ("木", "火") or ("火", "土") or ("土", "金") or ("金", "水") or ("水", "木");
    private static bool Overcomes(string from, string to) => (from, to) is ("木", "土") or ("土", "水") or ("水", "火") or ("火", "金") or ("金", "木");

    internal static bool TrySaveProduct(ProductSnapshot snapshot, out string path, out TalismanProductItemData? item)
    {
        string json = JsonSerializer.Serialize(snapshot);
        string digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant()[..32];
        path = ProductRoot + digest + ".tres";
        item = LoadProduct(path);
        if (item is not null)
        {
            if (item.snapshot_json != json) return false;
            // ResourceLoader 的缓存可在文件已删除时继续存在；恢复合同必须检查真实文件。
            if (Godot.FileAccess.FileExists(path)) return true;
        }
        if (!ValidateSnapshot(snapshot)) return false;
        string name = snapshot.Kind switch { "paper" => "符纸", "ink" => "符墨", "brush" => BrushOffers().FirstOrDefault(b => b.Id == snapshot.BrushId)?.Name ?? "符笔", _ => TalismanCombatCatalog.Name(snapshot.RecipeId) };
        string icon = TalismanArtCatalog.IconPath(snapshot);
        item = new TalismanProductItemData
        {
            snapshot_json = json, definition_id = new StringName("talisman_" + digest), entity_name = name,
            item_category = snapshot.Kind == "talisman" ? "符箓" : "器具", item_subcategory = snapshot.Kind,
            icon_path = icon, can_stack = snapshot.Kind != "brush", storage_volume = ProductStorageVolume(snapshot), inventory_width = 1,
            inventory_height = snapshot.Kind is "paper" or "brush" or "talisman" ? 2 : 1,
            description = snapshot.Kind == "talisman" ? TalismanCombatCatalog.EffectText(snapshot.RecipeId, snapshot.Tier) :
                string.Join("\n", PlayerFields(snapshot).Select(f => f.Key + "：" + f.Value)),
            usage_type = snapshot.Kind == "talisman" ? 1 : 0,
            battle_skill_path = snapshot.Kind == "talisman" ? TalismanCombatCatalog.SkillPath(snapshot.RecipeId, snapshot.Tier) : ""
        };
        try
        {
            System.IO.Directory.CreateDirectory(ProjectSettings.GlobalizePath(ProductRoot));
            if (ResourceSaver.Save(item, path) != Error.Ok) { item = null; return false; }
            item.TakeOverPath(path);
            DefinitionCache[path] = item;
            TalismanCraftingCatalog.RegisterProductDefinition(path);
            return true;
        }
        catch (System.IO.IOException) { item = null; return false; }
        catch (UnauthorizedAccessException) { item = null; return false; }
    }
    public static void RestoreDefinitions(HumanData human)
    {
        human.talisman_product_definitions ??= new();
        foreach (var entry in human.talisman_product_definitions)
        {
            if (Godot.FileAccess.FileExists(entry.Key))
            {
                // 普通行囊首次展示前也刷新已有文件及 ResourceLoader 共享缓存的显示素材。
                LoadProduct(entry.Key);
                continue;
            }
            if (ReadSnapshot(entry.Value) is { } snapshot && ValidateSnapshot(snapshot)) TrySaveProduct(snapshot, out _, out _);
        }
        // 兼容只保留库存路径、未带恢复字典的旧生成物；不把通用原料当符工产品。
        foreach (string path in human.inventory_item_paths.Distinct(StringComparer.Ordinal))
            if (path.StartsWith(ProductRoot, StringComparison.Ordinal) && !human.talisman_product_definitions.ContainsKey(path)) LoadProduct(path);
    }
    internal static ProductSnapshot BasicBrush() => BrushSnapshot("qingzhu");
    public static ProductSnapshot BrushSnapshot(string id) => id switch
    {
        "jinghao" => new() { Kind = "brush", BrushId = id, PenCapacity = 1.3, PenControl = .82, PenDelivery = .58, PenClosing = .8 },
        "cangfeng" => new() { Kind = "brush", BrushId = id, PenCapacity = 2.2, PenControl = .48, PenDelivery = .76, PenClosing = .54 },
        _ => new() { Kind = "brush", BrushId = "qingzhu", PenCapacity = 1.5, PenControl = .55, PenDelivery = .55, PenClosing = .58 }
    };
}
