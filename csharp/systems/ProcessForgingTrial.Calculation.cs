using System;
using System.Collections.Generic;
using System.Linq;

public sealed partial class ProcessForgingTrial
{
    public static Assessment Assess(double capacity, double maintenance, double transmission, double stability, double levelThreshold = 10, double stabilityThreshold = .8)
    {
        if (!Nonnegative(capacity) || !Nonnegative(maintenance) || !Nonnegative(transmission) || !Nonnegative(stability) || stability > 1 || !Positive(levelThreshold) || !Positive(stabilityThreshold) || stabilityThreshold > 1) throw new ArgumentException("评阶指标非法");
        double level = Math.Min(capacity, Math.Min(maintenance, transmission)) * stability;
        return new Assessment { Capacity = capacity, Maintenance = maintenance, Transmission = transmission, Stability = stability, SpiritLevel = level,
            IsArtifact = level >= levelThreshold && stability >= stabilityThreshold };
    }
    private double Metric(string key, Dictionary<string, double> vector)
    {
        var row = _parameters.Metrics[key]; return vector.Values.Sum() * row.GetValueOrDefault("*") + vector.Sum(x => x.Value * row.GetValueOrDefault(x.Key));
    }
    private static void Add(Dictionary<string, double> vector, string key, double amount) => vector[key] = vector.GetValueOrDefault(key) + amount;
    private static string Group(string role) => role is "主体" or "承接" or "复合身材" ? "身材" : role;
    public static double Relation(string root, string source, out string[] bridge)
    {
        bridge = []; const string cycle = "木火土金水";
        int a = root.Length == 1 ? cycle.IndexOf(root, StringComparison.Ordinal) : -1, b = source.Length == 1 ? cycle.IndexOf(source, StringComparison.Ordinal) : -1;
        if (a < 0 || b < 0) return .85;
        if (a == b || (a + 1) % 5 == b) return 1;
        if ((b + 1) % 5 == a) return .9;
        var path = new List<string>(); for (int n = (a + 1) % 5; n != b; n = (n + 1) % 5) path.Add(cycle[n].ToString());
        bridge = path.ToArray(); return .6;
    }
    public Evaluation Evaluate(Draft draft, IReadOnlyDictionary<string, int>? stock = null)
    {
        var r = ClonePreparation(Inspect(draft, stock)); if (!r.CanSubmit) return r;
        var p = _parameters;
        var sources = Normalize(draft.Inputs).Select(i => new SourceRecord { Input = Clone(i), Material = Snapshot(i.Material), EffectiveQuantity = Q(i.Quantity), Retention = Retention(p.Profiles[i.Material], draft.Proficiency) })
            .OrderBy(x => x.Input.Role, StringComparer.Ordinal).ThenBy(x => x.Material.DefinitionId, StringComparer.Ordinal).ThenBy(x => x.Material.Name, StringComparer.Ordinal).ToList();
        var solids = sources;
        var blade = solids.Where(x => x.Input.Role is "主体" or "复合身材").ToList();
        double Volume(SourceRecord s) => s.EffectiveQuantity * s.Retention * p.Profiles[s.Input.Material].Volume;
        double Grade(MaterialSnapshot m)
        {
            var g = m.Utilities.Values.OrderByDescending(x => x).ToArray();
            if (g.Length == 0) return 0;
            return Math.Clamp(Math.Ceiling(g[0] * 2 + (g.Length > 1 ? g[1] * .5 : 0) + g.Skip(2).Sum() * .25), 1, 27);
        }
        double BodyGrowth(SourceRecord x) => Math.Exp(N("GradeBodyGrowth") * Math.Max(0, Grade(x.Material) - 1));
        double volume = blade.Sum(Volume), lambda = blade.Sum(x => Volume(x) * p.Profiles[x.Input.Material].Extension) / volume, tau = blade.Sum(x => Volume(x) * p.Profiles[x.Input.Material].Thickness) / volume;
        double width = Math.Cbrt(volume / (lambda * tau));
        var a = new Artifact { RulesVersion = p.Version, InputHash = Fingerprint(draft), Proficiency = draft.Proficiency, Volume = volume, Width = width, Length = width * lambda, Thickness = width * tau,
            Sources = sources, ParameterSnapshot = _parameterText, CraftingHours = r.Hours, CraftingEnergy = r.Energy };
        a.Shape = (lambda >= 12 ? "修长" : "宽展") + (tau >= .16 ? "厚脊" : "薄脊") + "剑"; a.Name = a.Shape;
        foreach (var s in solids)
        {
            double sum = s.Material.Utilities.Sum(x => W(x.Value)), strongest = s.Material.Utilities.Select(x => W(x.Value)).DefaultIfEmpty().Max();
            s.Supply = s.EffectiveQuantity * p.SupplyScale * strongest * s.Retention;
            if (sum > 0) foreach (var (u, g) in s.Material.Utilities.OrderBy(x => x.Key, StringComparer.Ordinal)) s.Vector[u] = s.Supply * W(g) / sum;
        }
        var roots = solids.Where(x => x.Input.Role == "质材").ToList();
        if (roots.Count == 0) roots = solids.Where(x => x.Input.Role == "主体").ToList();
        double rootWeight = roots.Sum(x => x.Supply);
        var tokens = solids.Where(x => x.Input.Role == "和材").SelectMany(x => Enumerable.Range(0, x.Input.Quantity).Select(n => (Source: x, Index: n))).ToList();
        var spent = new HashSet<(SourceRecord Source, int Index)>();
        double conflictLoss = 0;
        foreach (var s in solids)
        {
            double delivered = 0;
            foreach (var root in roots)
            {
                double relation = Relation(root.Material.Element, s.Material.Element, out var bridge);
                var selected = new List<(SourceRecord Source, int Index)>();
                // 通关只为性材支路支付，已消费的实物份数不能再进入工艺向量。
                if (s.Input.Role == "性材" && bridge.Length > 0)
                {
                    foreach (string element in bridge)
                    {
                        var options = tokens.Where(t => !spent.Contains(t) && !selected.Contains(t) && t.Source.Material.Element == element).ToList();
                        if (options.Count == 0) { selected.Clear(); break; } selected.Add(options[0]);
                    }
                    if (selected.Count == bridge.Length)
                    {
                        relation = .9;
                        foreach (var t in selected) spent.Add(t);
                        a.Calculation.Ledger.Add(s.Material.Name + "支路通关使用：" + string.Join("、", selected.Select(t => t.Source.Material.Name + "第" + (t.Index + 1) + "份")));
                    }
                }
                double polarity = Math.Exp(-N("PolarityDecay") * Math.Max(0, Math.Abs(root.Material.Polarity - s.Material.Polarity) - N("PolarityTolerance")));
                delivered += (rootWeight > 0 ? root.Supply / rootWeight : 1.0 / roots.Count) * relation * polarity;
            }
            s.Delivery = delivered;
            conflictLoss += s.Supply * (1 - delivered) * N("ConflictLoss");
        }
        Dictionary<string, double> body = [], character = [], process = [];
        foreach (var s in solids)
        {
            double bridgeFraction = spent.Count(t => t.Source == s) / (double)s.Input.Quantity;
            s.BridgeSpent = s.Supply * s.Delivery * bridgeFraction;
            double usable = s.Supply * s.Delivery - s.BridgeSpent;
            var role = p.RoleWeights[Group(s.Input.Role)];
            s.BodySupply = usable * role[0]; s.CharacterSupply = usable * role[1]; s.ProcessSupply = usable * role[2]; s.Loss = s.Supply * (1 - s.Delivery);
            foreach (var pair in s.Vector)
            {
                double value = pair.Value * s.Delivery * (1 - bridgeFraction);
                Add(body, pair.Key, value * role[0]); Add(character, pair.Key, value * role[1]); Add(process, pair.Key, value * role[2]);
            }
            s.Retained = s.EffectiveQuantity * s.Retention * s.Delivery;
            if (s.Material.Element.Length > 0) Add(a.MaterialElementContributions, s.Material.Element, s.Retained);
        }
        double totalRetained = solids.Sum(x => x.Retained);
        a.Polarity = totalRetained > 0 ? solids.Sum(x => x.Material.Polarity * x.Retained) / totalRetained : 0;
        double connection = solids.Sum(x => x.EffectiveQuantity * x.Retention * p.Profiles[x.Input.Material].Connection);
        double needed = volume * N("ConnectionPerVolume"); double connected = Math.Min(1, connection / needed);
        a.StructureCapacity = N("CapacityPerVolume") * blade.Sum(x => Volume(x) * p.Profiles[x.Input.Material].Strength * BodyGrowth(x)) * connected;
        double support = Metric("Stability", process) + body.Values.Sum() * N("BodyStability");
        double loss = conflictLoss + Math.Max(0, needed - connection) * N("ConnectionLoss") + solids.Sum(x => x.Supply * (1 - x.Retention)) * N("ProcessingLoss");
        double stability = support <= 0 ? 0 : Math.Exp(-loss / support);
        double capacity = Math.Min(a.StructureCapacity, Metric("Capacity", body) * N("CapacityScale") * connected);
        double maintenance = Metric("Maintenance", process) * N("MaintenanceScale") + body.Values.Sum() * N("BodyMaintenance");
        double transmission = Metric("Transmission", process) * N("TransmissionScale") * connected;
        a.Calculation.Assessment = Assess(capacity, maintenance, transmission, stability, N("SpiritThreshold"), N("StabilityThreshold"));
        a.Tier = a.Calculation.Assessment.IsArtifact ? "法器" : "凡器";
        a.RootBudget = maintenance;
        double edge = blade.Sum(x => Volume(x) * p.Profiles[x.Input.Material].Edge * BodyGrowth(x)) / volume;
        double finish = blade.Sum(x => Volume(x) * x.Retention) / volume;
        double indirect = solids.Where(x => Group(x.Input.Role) != "身材").Sum(x => x.BodySupply * Metric("Sharpness", x.Vector) / Math.Max(1e-12, x.Supply));
        a.Sharpness = Math.Max(0, (int)Math.Floor(N("SharpnessScale") * edge * finish * Math.Clamp(Math.Sqrt(N("SharpnessReferenceThickness") / tau), N("SharpnessShapeMinimum"), N("SharpnessShapeMaximum")) + N("IndirectSharpnessScale") * indirect));
        a.Calculation.CharacterPotential = character;
        var budgets = new Dictionary<string, double>(StringComparer.Ordinal) { ["structure"] = a.StructureCapacity, ["root"] = maintenance, ["通"] = process.GetValueOrDefault("通"), ["聚"] = process.GetValueOrDefault("聚"), ["藏"] = process.GetValueOrDefault("藏") };
        foreach (var pair in character) budgets["source/" + pair.Key] = pair.Value;
        a.Calculation.Resources = budgets;
        BuildEffectsAndBonds(a, budgets);
        if (a.Effects.Any(x => x.CarrierId == "summon"))
        {
            a.BodyMaximum = (int)Math.Floor(N("BodyScale") * a.StructureCapacity / N("CapacityPerVolume"));
            a.SpiritRequired = Math.Max(1, (int)Math.Ceiling(N("SpiritBodyScale") * volume + N("SpiritEffectScale") * a.Effects.Where(x => x.CarrierId == "summon").Sum(x => x.Allocated)));
        }
        a.Calculation.Ledger.Add("本体和角色收益按岗位权重分账；锋锐与评阶各自计算，等级不再乘属性倍率。");
        a.Calculation.Ledger.Add("灵性判定只评价本体能力，不生成额外效力。凡器仅允许基础加成。");
        r.Product = a; return r;
    }
    private void BuildEffects(Artifact a, Dictionary<string, double> budgets)
    {
        var choices = new List<(Atom Atom, Carrier Carrier, string Source, double Weight, Dictionary<string, double> Costs, double Minimum)>();
        foreach (var carrier in _parameters.Carriers.OrderBy(x => x.Id, StringComparer.Ordinal))
        {
            if (carrier.Special && a.Tier == "凡器") continue;
            if (carrier.Id == "summon" && a.Volume < N("MinimumSummonVolume")) continue;
            foreach (var atom in _parameters.Atoms.Where(x => carrier.Atoms.Contains(x.Id)).OrderBy(x => x.Id, StringComparer.Ordinal))
                foreach (var source in atom.Sources.OrderBy(x => x.Key, StringComparer.Ordinal))
                {
                    if (a.Calculation.CharacterPotential.GetValueOrDefault(source.Key) <= 0) continue;
                    var costs = new Dictionary<string, double> { ["source/" + source.Key] = 1, ["root"] = 1, ["structure"] = 1 };
                    foreach (var cost in carrier.Costs.Where(x => x.Value > 0)) costs[cost.Key] = cost.Value;
                    double resource = carrier.Resource.Length == 0 ? 1 : budgets.GetValueOrDefault(carrier.Resource) / (budgets.GetValueOrDefault(carrier.Resource) + N("WeightScale"));
                    double minimum = Math.Max(N("MinimumEffect"), atom.Kind == "integer" ? 1 / atom.Coefficient : 0);
                    choices.Add((atom, carrier, source.Key, carrier.Weight * source.Value * resource, costs, minimum));
                }
        }
        string Id((Atom Atom, Carrier Carrier, string Source, double Weight, Dictionary<string, double> Costs, double Minimum) c) => c.Carrier.Id + "/" + c.Atom.Id + "/" + c.Source;
        // 同来源总偏好归一化，不因配置更多表达凭空增加一份总权重。
        var totalWeights = choices.GroupBy(x => x.Source).ToDictionary(x => x.Key, x => x.Sum(y => y.Weight));
        var requests = choices.Where(x => x.Weight > 0).Select(c => new ForgingBudgetSolver.Demand(Id(c), c.Weight / totalWeights[c.Source], c.Minimum, c.Costs)).ToList();
        var allocation = ForgingBudgetSolver.Solve(requests, budgets);
        foreach (var c in choices)
        {
            if (!allocation.TryGetValue(Id(c), out double x)) continue;
            double value = x * c.Atom.Coefficient;
            if (c.Atom.Maximum > 0) value = Math.Min(c.Atom.Maximum, value);
            if (c.Atom.Kind == "integer") value = Math.Floor(value + 1e-9);
            if (value <= 0) continue;
            var effect = a.Effects.FirstOrDefault(e => e.AtomId == c.Atom.Id && e.CarrierId == c.Carrier.Id);
            if (effect is null)
            {
                effect = new Effect { EffectId = "trial2/" + c.Carrier.Id + "/" + c.Atom.Id, AtomId = c.Atom.Id, CarrierId = c.Carrier.Id, Route = c.Carrier.Label, Scope = c.Carrier.Scope,
                    Event = c.Carrier.Event, Condition = c.Carrier.Condition, Special = c.Carrier.Special, AttributeKey = c.Carrier.Id == "base" ? c.Atom.Id : "" };
                if (c.Carrier.Id == "binding") effect.Condition += c.Atom.Id == "physical_defense_ignore_ratio" ? "；要求攻伐、气血、含单体物理伤害" : "；要求守御、含实际应对化解";
                a.Effects.Add(effect);
            }
            effect.Allocated += x; effect.SourceAllocations[c.Source] = x;
            foreach (var cost in c.Costs) Add(a.Calculation.Used, cost.Key, cost.Value * x);
        }
        foreach (var effect in a.Effects)
        {
            var atom = _parameters.Atoms.Single(x => x.Id == effect.AtomId);
            double raw = effect.Allocated * atom.Coefficient;
            effect.Value = atom.Maximum > 0 ? Math.Min(atom.Maximum, raw) : raw;
            if (atom.Kind == "integer") effect.Value = Math.Floor(effect.Value + 1e-9);
            effect.QuantizationLoss = Math.Max(0, effect.Allocated - effect.Value / atom.Coefficient);
            string value = atom.Kind == "ratio" ? $"{effect.Value * 100:0.##}%" : atom.Kind == "percent" ? $"+{effect.Value:0.##}%" : $"+{effect.Value:0}";
            effect.Description = atom.Label + " " + value;
        }
        foreach (var b in budgets) a.Calculation.Unused[b.Key] = Math.Max(0, b.Value - a.Calculation.Used.GetValueOrDefault(b.Key));
    }
}

