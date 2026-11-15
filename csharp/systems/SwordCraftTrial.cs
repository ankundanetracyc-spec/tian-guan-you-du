using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

/// <summary>剑类候选工艺的隔离试炼。只读取资源，不接触人物、库存或战斗资源。</summary>
public sealed class SwordCraftTrial
{
    public const string ParameterPath = "res://docs/plans/百工技艺/炼器/剑类炼器首轮试算参数_20261021.json";
    public readonly JsonElement Parameters;
    public readonly Dictionary<string, ItemData> Materials = [];
    public static readonly string[] Polarities = ["至阴", "阴", "偏阴", "和", "偏阳", "阳", "至阳"];
    public sealed class Input(string key, string group, string label, int min, int max)
    {
        public string Key = key, Group = group, Label = label, Material = "";
        public int Min = min, Max = max, Quantity = min;
    }
    public sealed class Draft(string form)
    {
        public string Form = form;
        public List<Input> Inputs = [];
        public HashSet<string> Methods = [];
        public Dictionary<string, string> Priorities = [];
        public string Primary = "破障";
        public double Proficiency = 12;
    }
    public sealed class Result
    {
        public List<string> Issues = [], Relations = [];
        public Dictionary<string, int> Materials = [];
        public Dictionary<string, double> Effects = [];
        public int Load, Capacity, Difficulty, Heat, OfferedHeat, Energy, Polarity, Completed, Required, BodyHp, Spirit;
        public double Hours, Growth;
        public bool Valid => Issues.Count == 0;
    }
    public SwordCraftTrial()
    {
        using var json = JsonDocument.Parse(Godot.FileAccess.GetFileAsString(ParameterPath));
        Parameters = json.RootElement.Clone();
        using var dir = DirAccess.Open("res://resources/material_craft");
        if (dir is null) return;
        foreach (var file in dir.GetFiles().Where(x => x.EndsWith("材性.tres")))
        {
            string path = "res://resources/entities/items/" + file.Replace("材性.tres", ".tres");
            if (!ResourceLoader.Exists(path)) continue;
            var item = GD.Load<ItemData>(path);
            if (item?.craft_data is MaterialCraftData) Materials[item.entity_name] = item;
        }
    }
    public JsonElement Form(string name) => Parameters.GetProperty("forms").GetProperty(name);
    public JsonElement Method(string name) => Parameters.GetProperty("methods").GetProperty(name);
    public static double N(JsonElement x, string name) => x.GetProperty(name).GetDouble();
    public static string S(JsonElement x, string name) => x.GetProperty(name).GetString()!;
    public double Q(int q) => N(Parameters.GetProperty("quantity_factors"), q.ToString());
    public MaterialCraftData Craft(string name) => (MaterialCraftData)Materials[name].craft_data!;
    public int Grade(string name, string utility) => Craft(name).utilities.Where(x => x.utility_id == utility).Select(x => x.grade).DefaultIfEmpty().Max();
    public Draft Create(string name)
    {
        var d = new Draft(name);
        foreach (var p in Form(name).GetProperty("parts").EnumerateObject())
            d.Inputs.Add(new Input(p.Name, "身材", p.Name, p.Value[1].GetInt32(), p.Value[2].GetInt32()));
        d.Inputs.Add(new Input("root", "质材", "根性", 1, (int)N(Parameters, "root_quantity_max")));
        d.Inputs.Add(new Input("破障", "性材", "破障来源", 1, (int)N(Parameters, "source_quantity_max")));
        d.Inputs.Add(new Input("固结", "性材", "固结来源", 1, (int)N(Parameters, "source_quantity_max")));
        for (int i = 0; i < N(Form(name), "auxiliary_slots"); i++) d.Inputs.Add(new Input("aux" + i, "和材", "辅助 " + (i + 1), 1, (int)N(Parameters, "auxiliary_max_per_slot")));
        foreach (var fuel in Parameters.GetProperty("fuels").EnumerateObject())
            d.Inputs.Add(new Input(fuel.Name, "燃料", fuel.Name, (int)N(fuel.Value, "minimum"), (int)N(fuel.Value, "maximum")));
        return d;
    }
    public string[] Utilities(Draft d) => d.Methods.Select(m => S(Method(m), "utility")).Distinct().OrderBy(x => x).ToArray();
    public bool Active(Draft d, Input input) => input.Group != "性材" || Utilities(d).Contains(input.Key);
    public string[] Choices(Draft d, Input input)
    {
        if (input.Group == "身材") return [Form(d.Form).GetProperty("parts").GetProperty(input.Key)[0].GetString()!];
        if (input.Group == "质材") return Parameters.GetProperty("roots").EnumerateObject().Select(x => x.Name).ToArray();
        if (input.Group == "性材") return Materials.Keys.Where(n => Grade(n, S(Parameters.GetProperty("utilities"), input.Key)) > 0).OrderBy(x => x).ToArray();
        if (input.Group == "燃料") return [input.Key];
        return Parameters.GetProperty("auxiliary_materials").EnumerateArray().Select(x => x.GetString()!).ToArray();
    }
    public (string relation, List<string> bridge) Relation(string root, string source)
    {
        const string cycle = "木火土金水";
        int a = cycle.IndexOf(root, StringComparison.Ordinal), b = cycle.IndexOf(source, StringComparison.Ordinal);
        if (root.Length != 1 || source.Length != 1 || a < 0 || b < 0) return ("材性待核对", []);
        if (a == b) return ("同行", []);
        if ((a + 1) % 5 == b) return ("根生性", []);
        if ((b + 1) % 5 == a) return ("性生根", []);
        List<string> bridge = [];
        for (int i = (a + 1) % 5; i != b; i = (i + 1) % 5) bridge.Add(cycle[i].ToString());
        return ("通关", bridge);
    }
    public Result Evaluate(Draft d)
    {
        var r = new Result(); var form = Form(d.Form); var utilities = Utilities(d);
        var inputs = d.Inputs.Where(x => Active(d, x)).ToArray();
        var body = inputs.Where(x => x.Group == "身材").ToArray();
        r.Required = body.Length; r.Completed = body.Count(x => x.Material != "");
        int extra = body.Sum(x => x.Quantity - x.Min);
        int drives = d.Methods.Count(m => S(Method(m), "kind").StartsWith("drive_"));
        r.Load = d.Methods.Sum(m => (int)N(Method(m), "load")) + (drives > 0 ? (int)N(Parameters, "summon_common_load") : 0) + (utilities.Length == 2 ? (int)N(Parameters, "dual_utility_load") : 0);
        r.Capacity = (int)N(form, "capacity") + extra * (int)N(Parameters, "capacity_per_extra_body_unit");
        if (d.Methods.Count == 0) r.Issues.Add("尚未配置功用与方法");
        if (r.Load > r.Capacity) r.Issues.Add($"器身承载不足：{r.Load} / {r.Capacity}");
        List<(string kind, string value)> demands = [];
        foreach (var m in d.Methods)
            foreach (var a in Method(m).GetProperty("auxiliary").EnumerateObject())
                for (int i = 0; i < a.Value.GetInt32(); i++) demands.Add(("utility", a.Name));
        var root = inputs.First(x => x.Key == "root");
        Dictionary<string, double> efficiencies = [], supplies = [];
        int bridgeCount = 0;
        foreach (var u in utilities)
        {
            var source = inputs.First(x => x.Key == u);
            if (root.Material == "" || source.Material == "") continue;
            var (relation, bridge) = Relation(Craft(root.Material).element, Craft(source.Material).element);
            r.Relations.Add($"{Craft(root.Material).element} → {(bridge.Count > 0 ? string.Join(" → ", bridge) + " → " : "")}{Craft(source.Material).element}　{relation}");
            if (relation == "材性待核对") { r.Issues.Add(relation); continue; }
            bridgeCount += bridge.Count;
            demands.AddRange(bridge.Select(x => ("element", x)));
            efficiencies[u] = N(Parameters.GetProperty("relation_efficiency"), relation);
            supplies[u] = N(Parameters, "source_supply_per_grade") * Grade(source.Material, S(Parameters.GetProperty("utilities"), u)) * Q(source.Quantity);
        }
        var crafting = Parameters.GetProperty("crafting");
        r.Difficulty = (int)new[] { N(form, "proficiency"), N(crafting, "proficiency_per_extra_method") * Math.Max(0, d.Methods.Count - 1), utilities.Length == 2 ? N(crafting, "dual_utility_proficiency") : 0, N(crafting, "proficiency_per_bridge_material") * bridgeCount }.Max();
        if (d.Proficiency < r.Difficulty) r.Issues.Add($"炼器造诣需 {r.Difficulty}，当前 {d.Proficiency:0.##}");
        foreach (var x in inputs)
        {
            bool required = x.Group != "和材" && x.Min > 0;
            if (x.Material == "") { if (required) r.Issues.Add($"待投：{x.Label}"); continue; }
            if (!Choices(d, x).Contains(x.Material) || x.Quantity < x.Min || x.Quantity > x.Max) { r.Issues.Add($"{x.Label} 投料不符"); continue; }
            if (x.Quantity > 0) r.Materials[x.Material] = r.Materials.GetValueOrDefault(x.Material) + x.Quantity;
        }
        var aux = inputs.Where(x => x.Group == "和材" && x.Material != "").ToArray();
        if (aux.GroupBy(x => x.Material).Any(g => g.Sum(x => x.Quantity) > N(Parameters, "auxiliary_max_per_slot"))) r.Issues.Add("同种和材合计不得超过3份");
        var auxUnits = aux.SelectMany(x => Enumerable.Repeat(x.Material, x.Quantity)).ToList();
        bool Match(int index, List<string> units)
        {
            if (index >= demands.Count) return true;
            var need = demands[index];
            for (int i = 0; i < units.Count; i++)
            {
                if (need.kind == "utility" ? Grade(units[i], need.value) == 0 : Craft(units[i]).element != need.value) continue;
                var remaining = new List<string>(units); remaining.RemoveAt(i);
                if (Match(index + 1, remaining)) return true;
            }
            return false;
        }
        if (!Match(0, auxUnits)) r.Issues.Add("辅助投入不足：" + string.Join("、", demands.GroupBy(x => x).Select(g => $"{(g.Key.kind == "utility" ? "功用" : "通关")}{g.Key.value} ×{g.Count()}")));
        var nonfuel = inputs.Where(x => x.Group != "燃料" && x.Material != "").ToArray();
        int total = nonfuel.Sum(x => x.Quantity);
        r.Heat = total + (int)N(crafting, "heat_per_load") * r.Load;
        r.OfferedHeat = inputs.Where(x => x.Group == "燃料" && x.Material != "").Sum(x => (int)N(Parameters.GetProperty("fuels").GetProperty(x.Material), "heat") * x.Quantity);
        if (r.OfferedHeat < r.Heat) r.Issues.Add($"供热不足：{r.OfferedHeat} / {r.Heat}");
        double weight = 0, polarity = 0;
        foreach (var x in nonfuel)
        {
            string role = x.Group switch { "身材" => "body", "质材" => "root", "性材" => "source", _ => "auxiliary" };
            double w = N(Parameters.GetProperty("polarity_weights"), role) * Q(x.Quantity);
            string tag = Materials[x.Material].tags.FirstOrDefault(t => t.StartsWith("相性[")) ?? "相性[和]";
            string p = tag.Replace("相性[", "").TrimEnd(']');
            polarity += N(Parameters.GetProperty("polarity_values"), p) * w; weight += w;
        }
        r.Polarity = weight > 0 ? (int)Math.Round(polarity / weight, MidpointRounding.AwayFromZero) : 0;
        int major = nonfuel.Select(x => Math.Max(1, (int)Math.Ceiling(Craft(x.Material).material_grade_value() / 3d))).DefaultIfEmpty(1).Max();
        int baseline = body.Sum(x => x.Min) + 1 + utilities.Length;
        r.Hours = Math.Ceiling(4 * Math.Pow(2, major - 1) * N(form, "time_factor") * (1 + N(crafting, "time_per_extra_method") * Math.Max(0, d.Methods.Count - 1) + N(crafting, "time_per_extra_utility") * Math.Max(0, utilities.Length - 1) + N(crafting, "time_per_extra_material") * Math.Max(0, total - baseline)) - 1e-8) / 4;
        r.Energy = (int)Math.Ceiling(r.Hours * N(crafting, "stamina_per_hour"));
        double margin = d.Proficiency - r.Difficulty;
        r.Growth = margin < 0 ? 0 : Math.Min(margin < N(crafting, "growth_full_margin") ? N(crafting, "growth_full") : margin < N(crafting, "growth_stop_margin") ? N(crafting, "growth_reduced") : 0, Math.Max(0, r.Difficulty + N(crafting, "growth_stop_margin") - d.Proficiency));
        if (drives > 0)
        {
            r.BodyHp = (int)(N(form, "body_hp") + N(Parameters, "body_hp_per_extra_body_unit") * extra + N(Parameters, "body_hp_per_extra_root_unit") * (root.Quantity - 1));
            r.Spirit = (int)(N(Parameters.GetProperty("battle_costs"), "spirit_base") + N(Parameters.GetProperty("battle_costs"), "spirit_per_extra_drive_branch") * (drives - 1));
        }
        if (supplies.Count == utilities.Length && d.Methods.Count > 0 && d.Proficiency >= r.Difficulty)
        {
            double retention = N(Parameters.GetProperty("retention"), "entry") + N(Parameters.GetProperty("retention"), "per_proficiency") * Math.Min(N(Parameters.GetProperty("retention"), "extra_proficiency_cap"), d.Proficiency - r.Difficulty);
            double throughput = Math.Min(N(Parameters.GetProperty("roots"), root.Material) * Q(root.Quantity), N(form, "throughput") + extra * N(Parameters, "support_per_extra_body_unit"));
            var minimum = utilities.ToDictionary(u => u, u => d.Methods.Where(m => S(Method(m), "utility") == u).Sum(m => N(Method(m), "minimum")) / efficiencies[u] / retention);
            var allocated = Allocate(throughput, minimum, supplies, utilities.ToDictionary(u => u, u => d.Primary == u ? 2d : 1d));
            if (allocated is null) r.Issues.Add("功用效力不足：减少方法，或补充性材及根材支撑");
            else foreach (var u in utilities)
            {
                var names = d.Methods.Where(m => S(Method(m), "utility") == u).ToArray();
                double effect = allocated[u] * efficiencies[u] * retention;
                var shares = Allocate(effect, names.ToDictionary(m => m, m => N(Method(m), "minimum")), names.ToDictionary(m => m, _ => effect), names.ToDictionary(m => m, m => d.Priorities.GetValueOrDefault(m) == "主用" ? 2d : 1d))!;
                foreach (var m in names) r.Effects[m] = shares[m] * (d.Methods.Count == 1 ? N(form, "single_method_factor") : 1) * (S(Method(m), "kind") is "direct_attack" or "drive_attack" ? N(form, "physical_active_factor") : 1);
            }
        }
        return r;
    }
    private static Dictionary<string, double>? Allocate(double total, Dictionary<string, double> minima, Dictionary<string, double> caps, Dictionary<string, double> weights)
    {
        if (minima.Values.Sum() > total + 1e-8 || minima.Any(x => x.Value > caps[x.Key] + 1e-8)) return null;
        var values = new Dictionary<string, double>(minima); double left = total - values.Values.Sum();
        while (left > 1e-8)
        {
            var available = values.Keys.Where(k => caps[k] - values[k] > 1e-8).ToArray();
            if (available.Length == 0) break;
            double sum = available.Sum(k => weights[k]), spent = 0;
            foreach (var k in available) { double grant = Math.Min(caps[k] - values[k], left * weights[k] / sum); values[k] += grant; spent += grant; }
            if (spent < 1e-10) break; left -= spent;
        }
        return values;
    }
    public string DescribeEffect(string method, double value)
    {
        var e = Parameters.GetProperty("effects"); string kind = S(Method(method), "kind");
        int Floor(double v) => (int)Math.Floor(v + 1e-8);
        if (S(Method(method), "utility") == "破障")
            return (kind != "rewrite" ? $"基础物理伤害 {Floor(N(e, "physical_base") + value * N(e, "physical_per_effect"))}，" : "附加化法：") + $"忽略 {Math.Min(N(e, "penetration_percent_cap"), Floor(value * N(e, "penetration_percent_per_effect")))}% 物防";
        return kind switch { "passive" => $"常驻物防 +{Floor(value / N(e, "defense_divisor"))}", "rewrite" => $"实际应对时，化解 +{Floor(value / N(e, "resolve_divisor"))}", _ => $"为自身增加 {Floor(value / N(e, "shield_divisor"))} 护盾" };
    }
}
