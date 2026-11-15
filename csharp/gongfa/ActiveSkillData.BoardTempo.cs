using Godot;
using System.Text.RegularExpressions;

/// <summary>灵台招式出手节奏与定力冲击参数；默认值从招式占格、费用及伤害属性推导，可由单招覆盖。</summary>
public partial class ActiveSkillData
{
    [ExportGroup("灵台节奏")]
    /// <summary>单招出手节奏倍率；0表示按占格与体力费用推导。</summary>
    [Export(PropertyHint.Range, "0,2,0.01")] public float board_tempo_multiplier;
    /// <summary>单招定力冲击系数；0表示按占格与体力费用推导。</summary>
    [Export(PropertyHint.Range, "0,2,0.01")] public float composure_pressure_coefficient;

    [ExportGroup("灵台走位与射程")]
    /// <summary>AUTO按招式类型处理；其余值可令单招接近、保持理想距离或原地施展。</summary>
    [Export(PropertyHint.Enum, "AUTO,APPROACH,MAINTAIN_RANGE,HOLD")] public string battle_movement_mode = "AUTO";
    /// <summary>远程招式期望的六角格距离；-1时取合法射程中点。</summary>
    [Export(PropertyHint.Range, "-1,20,1")] public int preferred_range = -1;
    /// <summary>远程招式表现为飞行投射物或在目标处直接显现。</summary>
    [Export(PropertyHint.Enum, "projectile,direct")] public string ranged_delivery = "projectile";

    [ExportGroup("灵台击退")]
    /// <summary>体魄为0时的最大击退格数；0表示不击退。</summary>
    [Export(PropertyHint.Range, "0,8,1")] public int knockback_distance;
    /// <summary>冲击强度，与受击方当前体魄同量纲。</summary>
    [Export(PropertyHint.Range, "0,10000,1")] public float knockback_force;

    public float get_board_tempo_multiplier(int occupiedCellsInSelectedRow = -1, int effectiveEnergyCost = -1)
    {
        if (board_tempo_multiplier > 0) return Mathf.Clamp(board_tempo_multiplier, .25f, 2f);
        var rowCellCount = Mathf.Max(1, occupiedCellsInSelectedRow > 0 ? occupiedCellsInSelectedRow : board_size.X);
        var cost = Mathf.Max(0, effectiveEnergyCost >= 0 ? effectiveEnergyCost : energy_cost);
        return 1f / (1f + .08f * Mathf.Max(0, rowCellCount - 1) + .04f * cost);
    }

    public int get_board_preferred_range()
    {
        var minimum = Mathf.Max(0, range_min);
        var maximum = Mathf.Max(minimum, range_max);
        return Mathf.Clamp(preferred_range >= 0 ? preferred_range : (minimum + maximum) / 2, minimum, maximum);
    }

    public float get_composure_pressure_coefficient(int footprintArea = -1, int effectiveEnergyCost = -1)
    {
        if (composure_pressure_coefficient > 0) return Mathf.Clamp(composure_pressure_coefficient, .05f, 2f);
        var area = Mathf.Max(1, footprintArea > 0 ? footprintArea : board_size.X * board_size.Y);
        var cost = Mathf.Max(0, effectiveEnergyCost >= 0 ? effectiveEnergyCost : energy_cost);
        return Mathf.Clamp(.20f + .12f * Mathf.Max(0, area - 1) + .04f * cost, .05f, 2f);
    }

    /// <summary>读取本招伤害公式中贡献最大的基础属性项，作为对应属性带来的出手修正。</summary>
    public float get_board_speed_affinity_bonus(GodotObject owner)
    {
        var contributions = new List<(string Attribute, float Coefficient)>();
        if (!string.IsNullOrWhiteSpace(damage_formula))
        {
            const string attributePattern = "体魄|灵慧|神魂|physique|intelligence|soul";
            const string coefficientPattern = "(?:\\d+(?:\\.\\d*)?|\\.\\d+)";
            var explicitAttributes = new HashSet<(int Index, int Length)>();
            var formulaPatterns = new[]
            {
                $"(?<attribute>{attributePattern})\\s*(?:×|\\*)\\s*(?<coefficient>{coefficientPattern})",
                $"(?<coefficient>{coefficientPattern})\\s*(?:×|\\*)\\s*(?<attribute>{attributePattern})"
            };
            foreach (var pattern in formulaPatterns)
            {
                foreach (Match match in Regex.Matches(damage_formula, pattern, RegexOptions.IgnoreCase))
                {
                    var attribute = NormalizeCoreAttribute(match.Groups["attribute"].Value);
                    if (attribute.Length == 0 || !float.TryParse(match.Groups["coefficient"].Value,
                            System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var coefficient)) continue;
                    contributions.Add((attribute, coefficient));
                    var attributeMatch = match.Groups["attribute"];
                    explicitAttributes.Add((attributeMatch.Index, attributeMatch.Length));
                }
            }
            foreach (Match match in Regex.Matches(damage_formula, attributePattern, RegexOptions.IgnoreCase))
            {
                if (explicitAttributes.Contains((match.Index, match.Length))) continue;
                var attribute = NormalizeCoreAttribute(match.Value);
                if (attribute.Length > 0) contributions.Add((attribute, 1f));
            }
        }
        else
        {
            foreach (var bonus in damage_bonuses)
            {
                if (bonus is null || string.IsNullOrWhiteSpace(bonus.attribute)) continue;
                var attribute = NormalizeCoreAttribute(bonus.attribute);
                if (attribute.Length == 0) continue;
                contributions.Add((attribute, bonus.multiplier));
            }
        }

        return contributions.Select(entry => Mathf.Max(0, CardRules.Number(owner, entry.Attribute) * entry.Coefficient))
            .DefaultIfEmpty(0).Max();
    }

    private static string NormalizeCoreAttribute(string attribute) => attribute.Trim().ToLowerInvariant() switch
    {
        "体魄" or "physique" => "physique",
        "灵慧" or "intelligence" => "intelligence",
        "神魂" or "soul" => "soul",
        _ => ""
    };
}
