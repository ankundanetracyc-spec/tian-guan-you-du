using System;
using Calc = CombatLingjiCalculator;

internal static class CombatLingjiCalculatorSmoke
{
    private static int _checks;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        _checks++;
    }
    private static Calc.Result Calculate(double attribute, double proficiency, int roll)
    {
        Check(Calc.TryCalculate(attribute, proficiency, roll, out var result), "valid inputs");
        return result;
    }
    private static void Main()
    {
        var low = Calculate(20, 5, 1);
        var high = Calculate(20, 5, 6);
        Check(low == new Calc.Result(5, 6, 11, 1, 6), "20 attribute and 5 proficiency minimum is 6");
        Check(high == new Calc.Result(5, 6, 11, 6, 11), "20 attribute and 5 proficiency maximum is 11");
        for (var roll = 1; roll <= 6; roll++)
        {
            var current = Calculate(20, 5, roll);
            Check(current.Value == 5 + roll && current.Minimum == 6 && current.Maximum == 11, "inclusive random range");
        }
        Check(Calculate(0, 0, 1).Value == 4, "zero attribute minimum");
        Check(Calculate(0, 0, 6).Value == 9, "zero attribute maximum");
        Check(Calculate(5, 10, 1).Value == 5, "fractional contributions add before floor");
        Check(Calculate(9.999, 0, 1).Value == 4 && Calculate(10, 0, 1).Value == 5, "attribute threshold");
        Check(Calculate(0, 19.999, 1).Value == 4 && Calculate(0, 20, 1).Value == 5, "proficiency threshold");
        Check(Calculate(4.999, 10, 1).Value == 4 && Calculate(5.001, 10, 1).Value == 5, "combined fractional boundary");
        Check(Calculate(Math.BitDecrement(10d), 0, 1).Value == 4 && Calculate(10d, 0, 1).Value == 5,
            "adjacent representable values around threshold");

        for (var attribute = 0; attribute <= 100; attribute++)
        for (var proficiency = 0; proficiency <= 100; proficiency++)
        {
            var current = Calculate(attribute, proficiency, 3);
            Check(Calculate(attribute + 1, proficiency, 3).Value >= current.Value, "attribute monotonic");
            Check(Calculate(attribute, proficiency + 1, 3).Value >= current.Value, "proficiency monotonic");
        }

        var calls = 0;
        Check(Calc.TryRoll(20, 5, () => { calls++; return 4; }, out var fixedRoll)
            && calls == 1 && fixedRoll.Value == 9 && fixedRoll.Roll == 4, "deterministic injected roll called once");
        Check(!Calc.TryRoll(20, 5, () => 0, out _) && !Calc.TryRoll(20, 5, () => 7, out _), "injected roll range rejected");
        Check(!Calc.TryRoll(20, 5, null, out _), "missing generator rejected");
        Check(!Calc.TryRoll(-1, 5, () => { calls++; return 1; }, out _) && calls == 1,
            "invalid numeric input does not call generator");
        Check(!Calc.TryCalculate(-1, 0, 1, out _) && !Calc.TryCalculate(0, -1, 1, out _), "negative inputs rejected");
        Check(!Calc.TryCalculate(double.NaN, 0, 1, out _) && !Calc.TryCalculate(0, double.NaN, 1, out _), "NaN rejected");
        Check(!Calc.TryCalculate(double.PositiveInfinity, 0, 1, out _)
            && !Calc.TryCalculate(0, double.NegativeInfinity, 1, out _), "infinite input rejected");
        Check(!Calc.TryCalculate(20, 5, 0, out _) && !Calc.TryCalculate(20, 5, 7, out _), "random value outside 1 to 6 rejected");
        var maxAttribute = ((double)int.MaxValue - 9d) * 10d;
        var max = Calculate(maxAttribute, 0, 6);
        Check(max.Maximum == int.MaxValue && max.Value == int.MaxValue, "largest complete range fits");
        Check(!Calc.TryCalculate(maxAttribute + 10d, 0, 1, out _) && !Calc.TryCalculate(double.MaxValue, double.MaxValue, 1, out _),
            "reject range overflow even when chosen roll is low");
        Console.WriteLine($"COMBAT_LINGJI_CALCULATOR_SMOKE_OK checks={_checks}");
    }
}
