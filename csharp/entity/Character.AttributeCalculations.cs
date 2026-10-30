using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class Character
{
    // Presentation-only, rebuilt with attributes, never serialized to the save.
    public sealed record AttributeCalculation(string Expression, float Result, IReadOnlyList<string> Sources);
    private readonly Dictionary<string, AttributeCalculation> _attributeCalculations = new();
    private static readonly string[] CalculationKeys = ["physique", "intelligence", "soul", "fortune", "comprehension", "max_health", "initiative_speed", "precision", "dodge", "critical", "crit_resist", "physical_defense", "physical_damage_reduction", "crit_damage_mult", "max_spirit_perception", "sword_proficiency", "blade_proficiency", "spear_proficiency", "body_proficiency", "magic_proficiency", "forging_proficiency", "talisman_proficiency", "formation_proficiency", "alchemy_proficiency"];
    internal static string AttributeNumber(float value) => value.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);
    private float CalculationValue(string key) => entity_data!.Get(key).AsSingle();
    public AttributeCalculation? GetAttributeCalculation(string key)
        => _attributeCalculations.TryGetValue(key, out var entry) && Mathf.IsEqualApprox(entry.Result, CalculationValue(key)) ? entry : null;

    private void ResetAttributeCalculations()
    {
        _attributeCalculations.Clear();
        foreach (var key in CalculationKeys)
        {
            var value = CalculationValue(key);
            _attributeCalculations[key] = new(AttributeNumber(value), value, []);
        }
        foreach (var key in new[] { "physique", "intelligence", "soul", "fortune", "comprehension" })
        {
            var basis = entity_data!.Get("base_" + key).AsSingle();
            var (growth, flat) = get_realm_attribute_adjustment(key);
            var condensation = get_condensation_flat_bonus(key);
            var expression = $"基础值[{AttributeNumber(basis)}]×成长倍率[{AttributeNumber(growth)}]＋定额加成[{AttributeNumber(flat)}]";
            var sources = new List<string>();
            if (growth != 1) sources.Add($"境界累计成长倍率：{AttributeNumber(growth)}");
            if (flat != 0) sources.Add($"境界累计定额加成：{AttributeNumber(flat)}");
            if (condensation != 0) { expression += $"＋凝核加值[{AttributeNumber(condensation)}]"; sources.Add($"凝核：＋{AttributeNumber(condensation)}"); }
            _attributeCalculations[key] = new(expression, CalculationValue(key), sources);
        }
        var p = $"体魄[{AttributeNumber(physique)}]"; var i = $"灵慧[{AttributeNumber(intelligence)}]"; var s = $"神魂[{AttributeNumber(soul)}]";
        var c = $"身心协调[{AttributeNumber(coordination)}]";
        void Baseline(string key, string expression) => _attributeCalculations[key] = new(expression, CalculationValue(key), []);
        Baseline("max_health", $"60＋9×{p}＋2×基础体魄[{AttributeNumber(base_physique)}]＋3×{s}＋1.5×{i}");
        Baseline("initiative_speed", $"80＋0.5×{p}＋0.75×{i}＋1.5×{c}＋0.75×{s}");
        Baseline("precision", $"7×{i}＋{s}＋0.5×{p}");
        Baseline("dodge", $"4×{c}＋2×{s}＋{i}＋0.5×{p}");
        Baseline("critical", $"0.5×{p}＋2×{s}＋0.3×{i}");
        Baseline("crit_resist", $"3×{p}＋2×{i}＋2×{c}＋1.5×{s}");
        foreach (var key in CalculationKeys.Where(key => key.EndsWith("_proficiency")))
            Baseline(key, $"基础造诣[{AttributeNumber(CalculationValue(key))}]");
    }

    private IDisposable TraceAttributeSource(string source) => new AttributeSourceScope(this, source);
    private sealed class AttributeSourceScope : IDisposable
    {
        private readonly Character _owner;
        private readonly string _source;
        private readonly Dictionary<string, float> _before;
        public AttributeSourceScope(Character owner, string source)
        { _owner = owner; _source = source; _before = CalculationKeys.ToDictionary(key => key, owner.CalculationValue); }
        public void Dispose()
        {
            foreach (var key in CalculationKeys)
            {
                var after = _owner.CalculationValue(key); var before = _before[key];
                if (Mathf.IsEqualApprox(before, after) || !_owner._attributeCalculations.TryGetValue(key, out var entry)) continue;
                var delta = after - before;
                // This is the observed change from this named operation, not an
                // unexplained final-value residual attributed to an invented source.
                var sign = delta < 0 ? "－" : "＋";
                _owner._attributeCalculations[key] = new(entry.Expression + $"{sign}{_source}[{AttributeNumber(Mathf.Abs(delta))}]", after,
                    entry.Sources.Append($"{_source}：{sign}{AttributeNumber(Mathf.Abs(delta))}").ToArray());
            }
        }
    }
    private void TraceArtifactBondDefense(Action apply)
    {
        var factor = 1 + get_artifact_bonds().DefenseBonus;
        apply();
        if (factor == 1 || !_attributeCalculations.TryGetValue("physical_defense", out var entry)) return;
        _attributeCalculations["physical_defense"] = new($"({entry.Expression})×器蕴倍率[{AttributeNumber((float)factor)}]", physical_defense,
            entry.Sources.Append($"器蕴物防倍率：{AttributeNumber((float)factor)}").ToArray());
    }
}
