using Godot;
using System;
using System.Linq;

/// <summary>Frozen, optional sword-control display coefficients; never an instruction to execute a skill.</summary>
[GlobalClass]
public partial class SwordControlPreviewData : Resource
{
    [Export] public int definition_version = 1;
    [Export] public string rules_version = "";
    [Export] public string input_sha256 = "";
    [Export] public bool has_coefficients;
    [Export] public int spirit_required = -1;
    [Export] public string skill_name = "";
    [Export] public int energy_cost = -1;
    [Export] public int mana_cost = -1;
    [Export] public string target_text = "";
    [Export] public string timing_text = "";

    // Negative defaults distinguish a missing member from an explicitly authored zero.
    [Export] public double stable_base = -1;
    [Export] public double stable_soul = -1;
    [Export] public double stable_sword = -1;
    [Export] public double stable_occupation = -1;
    [Export] public double attack_base = -1;
    [Export] public double attack_soul = -1;
    [Export] public double attack_sword = -1;

    public bool IsValid => definition_version == 1 && has_coefficients
        && !string.IsNullOrWhiteSpace(rules_version)
        && IsSha256(input_sha256) && spirit_required >= 0
        && Coefficients().All(entry => IsCoefficient(entry.Value));

    public Godot.Collections.Array<string> validate_definition()
    {
        Godot.Collections.Array<string> errors = [];
        if (definition_version != 1) errors.Add("御使展示定义版本不支持");
        if (!has_coefficients) errors.Add("御使系数未记录");
        if (string.IsNullOrWhiteSpace(rules_version)) errors.Add("御使规则版本未记录");
        if (!IsSha256(input_sha256)) errors.Add("御使输入来源校验值未记录或无效");
        if (spirit_required < 0) errors.Add("灵识占用未记录或无效");
        foreach (var coefficient in Coefficients())
            if (!IsCoefficient(coefficient.Value)) errors.Add(coefficient.Key + "未记录或无效");
        return errors;
    }

    private (string Key, double Value)[] Coefficients() =>
    [
        (nameof(stable_base), stable_base), (nameof(stable_soul), stable_soul),
        (nameof(stable_sword), stable_sword), (nameof(stable_occupation), stable_occupation),
        (nameof(attack_base), attack_base), (nameof(attack_soul), attack_soul),
        (nameof(attack_sword), attack_sword),
    ];

    private static bool IsCoefficient(double value) =>
        double.IsFinite(value) && value >= 0 && value < (double)decimal.MaxValue;

    private static bool IsSha256(string value) =>
        value is { Length: 64 } && value.All(Uri.IsHexDigit);
}
