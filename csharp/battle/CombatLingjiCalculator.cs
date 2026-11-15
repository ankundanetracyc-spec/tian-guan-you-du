using System;

/// <summary>
/// Standalone trial formula. The caller chooses the matching attribute and
/// proficiency and supplies the random roll; no skills or live combat are read.
/// </summary>
public static class CombatLingjiCalculator
{
    public readonly record struct Result(int Base, int Minimum, int Maximum, int Roll, int Value);

    /// <summary>
    /// The complete possible range must fit in Int32. Inputs must be finite,
    /// nonnegative and the supplied random value must be an integer in [1, 6].
    /// Attribute and proficiency are added before flooring.
    /// </summary>
    public static bool TryCalculate(double attribute, double proficiency, int roll, out Result result)
    {
        result = default;
        if (roll is < 1 or > 6 || !TryBase(attribute, proficiency, out var baseValue)) return false;
        result = new(baseValue, baseValue + 1, baseValue + 6, roll, baseValue + roll);
        return true;
    }

    /// <summary>
    /// The supplied function is called exactly once after numeric inputs have
    /// been checked. It is responsible for choosing a value; this class does not
    /// claim a random distribution. Exceptions from the function propagate.
    /// </summary>
    public static bool TryRoll(double attribute, double proficiency, Func<int>? nextRoll, out Result result)
    {
        result = default;
        if (nextRoll is null || !TryBase(attribute, proficiency, out var baseValue)) return false;
        var roll = nextRoll();
        if (roll is < 1 or > 6) return false;
        result = new(baseValue, baseValue + 1, baseValue + 6, roll, baseValue + roll);
        return true;
    }

    private static bool TryBase(double attribute, double proficiency, out int value)
    {
        value = default;
        if (!double.IsFinite(attribute) || !double.IsFinite(proficiency) || attribute < 0 || proficiency < 0)
            return false;
        var step = Math.Floor(attribute / 10d + proficiency / 20d);
        // Reserve both the constant 3 and the largest random value 6 before casting.
        if (!double.IsFinite(step) || step > int.MaxValue - 9d) return false;
        value = 3 + (int)step;
        return true;
    }
}
