using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

/// <summary>隔离炼器试炼。准备信息与成器结算分离，无人物/存档/战斗执行依赖。</summary>
public sealed partial class ProcessForgingTrial
{
    public const string ConfigPath = "res://resources/crafting/process_forging_candidate_v3.json";
    public static readonly string[] PolarityNames = ["至阴", "阴", "偏阴", "和", "偏阳", "阳", "至阳"];
    public static readonly string[] RoleOrder = ["主体", "承接", "复合身材", "质材", "性材", "和材"];
    public sealed class Profile
    {
        public string[] Roles { get; set; } = [];
        public int Difficulty { get; set; }
        public double Retention { get; set; }
        public double Volume { get; set; }
        public double Extension { get; set; }
        public double Thickness { get; set; }
        public double Edge { get; set; }
        public double Strength { get; set; }
        public double Connection { get; set; }
    }
    public sealed class Input
    {
        public string Role { get; set; } = "";
        public string Material { get; set; } = "";
        public int Quantity { get; set; } = 1;
    }
    public sealed class Draft
    {
        public string Category { get; set; } = "剑";
        public int Proficiency { get; set; } = 12;
        public List<Input> Inputs { get; set; } = [];
    }
    public sealed class Example
    {
        public string Name { get; set; } = "";
        public int Proficiency { get; set; }
        public List<Input> Inputs { get; set; } = [];
    }
    public sealed class Atom
    {
        public string Id { get; set; } = "";
        public Dictionary<string, double> Sources { get; set; } = [];
        public double Coefficient { get; set; }
        public string Kind { get; set; } = "";
        public string Label { get; set; } = "";
        public double Maximum { get; set; }
    }
    public sealed class Carrier
    {
        public string Id { get; set; } = "";
        public string Label { get; set; } = "";
        public string[] Atoms { get; set; } = [];
        public Dictionary<string, double> Costs { get; set; } = [];
        public double Weight { get; set; }
        public string Resource { get; set; } = "";
        public string Scope { get; set; } = "";
        public string Event { get; set; } = "";
        public string Condition { get; set; } = "";
        public bool Special { get; set; }
    }
    public sealed class Config
    {
        public string Version { get; set; } = "";
        public int ExtraSourceProficiency { get; set; }
        public int ExtraAuxiliaryProficiency { get; set; }
        public int AdvancedAuxiliaryProficiency { get; set; }
        public int MaxUnits { get; set; }
        public double QuantityScale { get; set; }
        public double GradeBase { get; set; }
        public double SupplyScale { get; set; }
        public Dictionary<string, double[]> RoleWeights { get; set; } = [];
        public Dictionary<string, double> Scalars { get; set; } = [];
        public Dictionary<string, Dictionary<string, double>> Metrics { get; set; } = [];
        public Dictionary<string, Profile> Profiles { get; set; } = [];
        public List<Atom> Atoms { get; set; } = [];
        public List<Carrier> Carriers { get; set; } = [];
        public List<Example> Examples { get; set; } = [];
    }
    public sealed class MaterialSnapshot
    {
        public string Name { get; set; } = "";
        public string DefinitionId { get; set; } = "";
        public string Element { get; set; } = "";
        public string Texture { get; set; } = "";
        public int Polarity { get; set; }
        public Dictionary<string, int> Utilities { get; set; } = [];
    }
    public sealed class SourceRecord
    {
        public Input Input { get; set; } = new();
        public MaterialSnapshot Material { get; set; } = new();
        public double EffectiveQuantity { get; set; }
        public double Retention { get; set; }
        public double Supply { get; set; }
        public double Delivery { get; set; } = 1;
        public double Retained { get; set; }
        public double BodySupply { get; set; }
        public double CharacterSupply { get; set; }
        public double ProcessSupply { get; set; }
        public double BridgeSpent { get; set; }
        public double Loss { get; set; }
        public Dictionary<string, double> Vector { get; set; } = [];
    }
    public sealed class Effect
    {
        public string EffectId { get; set; } = "";
        public string AtomId { get; set; } = "";
        public string CarrierId { get; set; } = "";
        public string Route { get; set; } = "";
        public string AttributeKey { get; set; } = "";
        public string Scope { get; set; } = "";
        public string Event { get; set; } = "";
        public string Condition { get; set; } = "";
        public string Description { get; set; } = "";
        public string BattleTemplateStatus { get; set; } = "未装配";
        public double Allocated { get; set; }
        public double Value { get; set; }
        public double QuantizationLoss { get; set; }
        public bool Special { get; set; }
        // 仅为来源追溯，不是成器功用分组。
        public Dictionary<string, double> SourceAllocations { get; set; } = [];
    }
    public sealed class Assessment
    {
        public double Capacity { get; set; }
        public double Maintenance { get; set; }
        public double Transmission { get; set; }
        public double Stability { get; set; }
        public double SpiritLevel { get; set; }
        public bool IsArtifact { get; set; }
    }
    public sealed class CalculationRecord
    {
        public Assessment Assessment { get; set; } = new();
        public Dictionary<string, double> CharacterPotential { get; set; } = [];
        public Dictionary<string, double> Resources { get; set; } = [];
        public Dictionary<string, double> Used { get; set; } = [];
        public Dictionary<string, double> Unused { get; set; } = [];
        public List<string> Ledger { get; set; } = [];
    }
    public sealed class Artifact
    {
        public int SchemaVersion { get; set; } = 3;
        public string InstanceId { get; set; } = "";
        public string Name { get; set; } = "";
        public string Category { get; set; } = "剑";
        public string Tier { get; set; } = "";
        public int Sharpness { get; set; }
        public string RulesVersion { get; set; } = "";
        public string InputHash { get; set; } = "";
        public string Shape { get; set; } = "";
        public double Volume { get; set; }
        public double Length { get; set; }
        public double Width { get; set; }
        public double Thickness { get; set; }
        public double RootBudget { get; set; }
        public double StructureCapacity { get; set; }
        public double Polarity { get; set; }
        // 材料保留量的来源记录，不是成器五行、伤害比例或克制参数。
        public Dictionary<string, double> MaterialElementContributions { get; set; } = [];
        public int BodyMaximum { get; set; }
        public int SpiritRequired { get; set; }
        public int Proficiency { get; set; }
        public int CraftingHours { get; set; }
        public int CraftingEnergy { get; set; }
        public List<Effect> Effects { get; set; } = [];
        public List<string> BondContributions { get; set; } = [];
        public List<SourceRecord> Sources { get; set; } = [];
        public CalculationRecord Calculation { get; set; } = new();
        public string ParameterSnapshot { get; set; } = "";
        public string Lifecycle { get; set; } = "独立试炼；未接入装备、正式存档与战斗";
    }
    /// <summary>可展示的客观准备信息，无成器预测、评阶或补料建议。</summary>
    public class Preparation
    {
        public List<string> Errors { get; set; } = [];
        public Dictionary<string, int> Required { get; set; } = [];
        public int Units { get; set; }
        public int Hours { get; set; }
        public int Energy { get; set; }
        public double InputPolarity { get; set; }
        public bool CanSubmit => Errors.Count == 0;
    }
    public sealed class Evaluation : Preparation
    {
        public List<string> Failures { get; set; } = [];
        public Artifact? Product { get; set; }
        public bool Success => CanSubmit && Failures.Count == 0 && Product is not null;
    }
    public Config Parameters => Clone(_parameters);
    private readonly Config _parameters;
    public Dictionary<string, ItemData> Materials { get; } = [];
    private readonly Dictionary<string, MaterialSnapshot> _facts = [];
    private readonly string _parameterText;
    public ProcessForgingTrial()
    {
        _parameterText = Godot.FileAccess.GetFileAsString(ConfigPath);
        _parameters = JsonSerializer.Deserialize<Config>(_parameterText) ?? throw new InvalidOperationException("炼器参数为空"); Validate();
        using var dir = DirAccess.Open("res://resources/material_craft") ?? throw new InvalidOperationException("材性目录缺失");
        foreach (var file in dir.GetFiles().Where(x => x.EndsWith("材性.tres")).OrderBy(x => x, StringComparer.Ordinal))
        {
            string path = "res://resources/entities/items/" + file.Replace("材性.tres", ".tres");
            if (ResourceLoader.Exists(path) && GD.Load<ItemData>(path) is { craft_data: MaterialCraftData } item) Materials[item.entity_name] = item;
        }
        foreach (var (name, item) in Materials)
        {
            var data = (MaterialCraftData)item.craft_data!; int polarity = Math.Sign(data.polarity) * 2;
            foreach (var tag in item.tags) for (int i = 0; i < PolarityNames.Length; i++) if (tag == "相性[" + PolarityNames[i] + "]") polarity = i - 3;
            _facts[name] = new MaterialSnapshot { Name = name, DefinitionId = item.definition_id.ToString(), Element = data.element, Texture = data.texture, Polarity = polarity,
                Utilities = data.utilities.Where(x => !string.IsNullOrWhiteSpace(x.utility_id)).GroupBy(x => x.utility_id).ToDictionary(x => x.Key, x => x.Max(v => v.grade), StringComparer.Ordinal) };
        }
        foreach (string name in _parameters.Profiles.Keys) if (!_facts.ContainsKey(name)) throw new InvalidOperationException("加工材料缺失：" + name);
        ValidateFacts();
    }
    private ProcessForgingTrial(Config config, IEnumerable<MaterialSnapshot> facts)
    {
        _parameterText = JsonSerializer.Serialize(config); _parameters = Clone(config); Validate();
        foreach (var fact in facts) _facts[fact.Name] = Clone(fact); ValidateFacts();
    }
    private ProcessForgingTrial(Artifact archive)
    {
        if (archive.SchemaVersion != 3) throw new InvalidOperationException("旧试炼快照不能自动套用无燃料规则");
        _parameterText = archive.ParameterSnapshot;
        _parameters = JsonSerializer.Deserialize<Config>(_parameterText) ?? throw new InvalidOperationException("缺少参数快照"); Validate();
        foreach (var source in archive.Sources) _facts[source.Material.Name] = Clone(source.Material); ValidateFacts();
    }
    public static ProcessForgingTrial Isolated(Config config, IEnumerable<MaterialSnapshot> facts) => new(config, facts);
    private void ValidateFacts()
    {
        foreach (var fact in _facts.Values) if (fact.Polarity < -3 || fact.Polarity > 3 || fact.Utilities.Values.Any(x => x < 1 || x > 9)) throw new InvalidOperationException("材性快照非法：" + fact.Name);
    }
    private static bool Positive(double x) => double.IsFinite(x) && x > 0;
    private static bool Nonnegative(double x) => double.IsFinite(x) && x >= 0;
    private double N(string key) => _parameters.Scalars[key];
    private void Validate()
    {
        var p = _parameters;
        if (p.Version != "sword-process-trial-3" || p.MaxUnits <= 0 || !Positive(p.QuantityScale) || !Positive(p.SupplyScale) || !Positive(p.GradeBase) || p.GradeBase <= 1) throw new InvalidOperationException("炼器公式参数缺项");
        string[] names = ["CapacityPerVolume", "ConnectionPerVolume", "ConnectionLoss", "ConflictLoss", "ProcessingLoss", "PolarityTolerance", "PolarityDecay", "CapacityScale", "MaintenanceScale", "TransmissionScale", "GradeBodyGrowth", "SharpnessScale", "IndirectSharpnessScale", "SpiritThreshold", "StabilityThreshold", "BodyScale", "SpiritBodyScale", "SpiritEffectScale", "MinimumSummonVolume", "WeightScale", "MinimumEffect", "BodyStability", "BodyMaintenance", "SharpnessReferenceThickness", "SharpnessShapeMinimum", "SharpnessShapeMaximum", "RetentionFloor", "ProficiencySpan"];
        foreach (string name in names) if (!p.Scalars.TryGetValue(name, out double x) || !Positive(x)) throw new InvalidOperationException("公式刻度缺项：" + name);
        if (N("StabilityThreshold") > 1) throw new InvalidOperationException("稳定标尺超过1");
        if (N("RetentionFloor") > 1 || N("SharpnessShapeMaximum") < N("SharpnessShapeMinimum") || p.ExtraAuxiliaryProficiency < 0 || p.ExtraSourceProficiency < 0 || p.AdvancedAuxiliaryProficiency < p.ExtraAuxiliaryProficiency) throw new InvalidOperationException("加工刻度非法");
        foreach (string role in new[] { "身材", "质材", "性材", "和材" }) if (!p.RoleWeights.TryGetValue(role, out var row) || row.Length != 3 || row.Any(x => !Nonnegative(x)) || Math.Abs(row.Sum() - 1) > 1e-9) throw new InvalidOperationException("岗位贡献不守恒：" + role);
        foreach (string metric in new[] { "Capacity", "Maintenance", "Transmission", "Stability", "Sharpness" }) if (!p.Metrics.TryGetValue(metric, out var row) || row.Values.Any(x => !Nonnegative(x))) throw new InvalidOperationException("公共指标映射缺项：" + metric);
        foreach (var (name, v) in p.Profiles) if (v.Roles.Length == 0 || v.Roles.Any(x => !RoleOrder.Contains(x)) || !Positive(v.Retention) || v.Retention > 1 || v.Difficulty < 0 || !Nonnegative(v.Edge) || !Nonnegative(v.Strength) || !Nonnegative(v.Connection) ||
            (v.Roles.Any(x => x is "主体" or "复合身材") && (!Positive(v.Volume) || !Positive(v.Extension) || !Positive(v.Thickness) || !Positive(v.Strength))) || (v.Roles.Contains("承接") && !Positive(v.Connection))) throw new InvalidOperationException("物理加工档案不完整：" + name);
        if (p.Atoms.Select(x => x.Id).Distinct().Count() != p.Atoms.Count || p.Carriers.Select(x => x.Id).Distinct().Count() != p.Carriers.Count) throw new InvalidOperationException("重复效果定义");
        foreach (var a in p.Atoms) if (string.IsNullOrEmpty(a.Id) || !Positive(a.Coefficient) || !Nonnegative(a.Maximum) || a.Sources.Count == 0 || a.Sources.Values.Any(x => !Positive(x)) || a.Kind is not ("percent" or "ratio" or "integer")) throw new InvalidOperationException("非法效果原子");
        foreach (var c in p.Carriers) if (string.IsNullOrEmpty(c.Id) || !Positive(c.Weight) || c.Atoms.Any(x => !p.Atoms.Any(a => a.Id == x)) || c.Costs.Any(x => x.Key is not ("通" or "聚" or "藏") || !Nonnegative(x.Value)) || (!c.Special && (c.Id != "base" || c.Costs.Values.Any(x => x != 0)))) throw new InvalidOperationException("非法效果表达");
    }
    public double Q(int q) => (1 - Math.Exp(-q / _parameters.QuantityScale)) / (1 - Math.Exp(-1 / _parameters.QuantityScale));
    public double W(int g) => g <= 0 ? 0 : Math.Pow(_parameters.GradeBase, g - 1);
    private double Retention(Profile p, int z) => p.Retention * (N("RetentionFloor") + (1 - N("RetentionFloor")) * Math.Clamp((z - p.Difficulty) / N("ProficiencySpan"), 0, 1));
    public MaterialSnapshot Snapshot(string name) => Clone(_facts[name]);
    public MaterialSnapshot[] MaterialFacts() => _facts.Values.Select(Clone).ToArray();
    public static string PolarityLabel(double x) => PolarityNames[Math.Clamp((int)Math.Round(x, MidpointRounding.AwayFromZero), -3, 3) + 3];
    public int Slots(string role, int z) => role switch { "复合身材" => z >= _parameters.ExtraSourceProficiency ? 1 : 0, "性材" => z >= _parameters.ExtraSourceProficiency ? 2 : 1, "和材" => z >= _parameters.AdvancedAuxiliaryProficiency ? 3 : z >= _parameters.ExtraAuxiliaryProficiency ? 2 : 1, _ => 1 };
    public string[] Choices(string role) => _parameters.Profiles.Where(x => x.Value.Roles.Contains(role) && _facts.ContainsKey(x.Key)).Select(x => x.Key).OrderBy(x => x, StringComparer.Ordinal).ToArray();
    public Draft ExampleDraft(int i) => Clone(new Draft { Proficiency = _parameters.Examples[i].Proficiency, Inputs = _parameters.Examples[i].Inputs });
    public static T Clone<T>(T data) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(data))!;
    public static List<Input> Normalize(IEnumerable<Input> inputs) => inputs.Where(x => !string.IsNullOrWhiteSpace(x.Material)).GroupBy(x => (x.Role, x.Material)).Select(x => new Input { Role = x.Key.Role, Material = x.Key.Material, Quantity = x.Sum(v => v.Quantity) }).OrderBy(x => x.Role, StringComparer.Ordinal).ThenBy(x => x.Material, StringComparer.Ordinal).ToList();
    public string Fingerprint(Draft draft) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { draft.Category, draft.Proficiency, Inputs = Normalize(draft.Inputs), Parameters = _parameterText, Materials = Normalize(draft.Inputs).Where(x => _facts.ContainsKey(x.Material)).Select(x => Snapshot(x.Material)).ToArray() })))).ToLowerInvariant();
    public static Evaluation Replay(Artifact a) => new ProcessForgingTrial(a).Evaluate(new Draft { Category = a.Category, Proficiency = a.Proficiency, Inputs = a.Sources.Select(x => Clone(x.Input)).ToList() });
    public Preparation Inspect(Draft draft, IReadOnlyDictionary<string, int>? stock = null)
    {
        var r = new Preparation(); var p = _parameters;
        if (draft.Category != "剑") r.Errors.Add("当前仅开放剑类加工");
        if (draft.Proficiency < 0 || draft.Proficiency > 100) r.Errors.Add("试炼造诣须在0—100之间");
        if (draft.Inputs.Any(x => x.Quantity < 1 || x.Quantity > 8)) r.Errors.Add("每行份数须为1—8整份");
        List<Input> inputs; try { inputs = Normalize(draft.Inputs); } catch (OverflowException) { r.Errors.Add("投料合计超出范围"); return r; }
        foreach (var i in inputs)
        {
            if (i.Quantity > 8) r.Errors.Add(i.Material + "同岗合计不能超过8份");
            if (!_facts.ContainsKey(i.Material) || !p.Profiles.TryGetValue(i.Material, out var v) || !v.Roles.Contains(i.Role)) { r.Errors.Add(i.Material + "没有该岗位的加工数据"); continue; }
            r.Required[i.Material] = r.Required.GetValueOrDefault(i.Material) + i.Quantity;
        }
        foreach (string role in RoleOrder) if (inputs.Count(x => x.Role == role) > Slots(role, draft.Proficiency)) r.Errors.Add(role + "超过当前开放位置");
        foreach (string role in new[] { "主体", "承接" }) if (!inputs.Any(x => x.Role == role)) r.Errors.Add("缺少" + role);
        r.Units = inputs.Sum(x => x.Quantity); if (r.Units > p.MaxUnits) r.Errors.Add("全炉最多" + p.MaxUnits + "份");
        if (stock is not null) foreach (var (name, q) in r.Required) if (!stock.TryGetValue(name, out int count) || count < q) r.Errors.Add(name + "试炼库存不足");
        var known = inputs.Where(x => x.Quantity > 0 && p.Profiles.ContainsKey(x.Material) && _facts.ContainsKey(x.Material)).ToArray();
        r.Hours = (int)Math.Ceiling(known.Sum(x => x.Quantity) / 3.0); r.Energy = r.Hours * 2;
        double weight = known.Sum(x => Q(x.Quantity) * Retention(p.Profiles[x.Material], draft.Proficiency));
        r.InputPolarity = weight == 0 ? 0 : known.Sum(x => _facts[x.Material].Polarity * Q(x.Quantity) * Retention(p.Profiles[x.Material], draft.Proficiency)) / weight;
        return r;
    }
    public sealed class Session
    {
        public sealed class Attempt
        {
            public int Number { get; set; }
            public List<Input> Inputs { get; set; } = [];
            public bool Success { get; set; }
            public string ArtifactId { get; set; } = "";
            public List<string> Failures { get; set; } = [];
            public int Hours { get; set; }
            public int Energy { get; set; }
        }
        public Dictionary<string, int> Stock { get; } = [];
        private readonly HashSet<string> _submitted = [];
        private readonly List<Artifact> _artifacts = [];
        private readonly List<Attempt> _attempts = [];
        public IReadOnlyList<Artifact> Artifacts => _artifacts.Select(Clone).ToArray();
        public IReadOnlyList<Attempt> Attempts => _attempts.Select(Clone).ToArray();
        public int Hours { get; private set; }
        public int Energy { get; private set; }
        public Session(IEnumerable<string> materials) { foreach (string name in materials) Stock[name] = 30; }
        public bool IsSubmitted(ProcessForgingTrial model, Draft draft, long revision) => _submitted.Contains(revision + ":" + model.Fingerprint(draft));
        public Evaluation Submit(ProcessForgingTrial model, Draft draft, long revision)
        {
            var preparation = model.Inspect(draft, Stock); if (!preparation.CanSubmit) return ClonePreparation(preparation);
            string token = revision + ":" + model.Fingerprint(draft);
            if (_submitted.Contains(token)) { var denied = ClonePreparation(preparation); denied.Errors.Add("本次草稿已结算；改料或重新载入后再试"); return denied; }
            var result = model.Evaluate(Clone(draft), Stock); if (!result.CanSubmit) return result;
            _submitted.Add(token); foreach (var (name, q) in result.Required) Stock[name] -= q;
            Hours += result.Hours; Energy += result.Energy;
            if (result.Success) { result.Product!.InstanceId = "trial-" + Guid.NewGuid().ToString("N"); _artifacts.Add(Clone(result.Product)); }
            _attempts.Add(new Attempt { Number = _attempts.Count + 1, Inputs = Normalize(draft.Inputs), Success = result.Success, ArtifactId = result.Product?.InstanceId ?? "", Failures = [.. result.Failures], Hours = result.Hours, Energy = result.Energy });
            return result;
        }
    }
    private static Evaluation ClonePreparation(Preparation p) => new() { Errors = [.. p.Errors], Required = new(p.Required), Units = p.Units, Hours = p.Hours, Energy = p.Energy, InputPolarity = p.InputPolarity };
}
internal static class ForgingElementDisplay
{
    public static string DefaultIfEmptyName(this string value) => string.IsNullOrEmpty(value) ? "无行" : value;
}


