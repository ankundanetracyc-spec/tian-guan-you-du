using Godot;
using Godot.Collections;

/// <summary>集中处理流转效果的触发前置条件。</summary>
public partial class FlowConditionChecker : RefCounted
{
    public static bool check(string condition_type, Dictionary ctx)
    {
        return condition_type switch
        {
            "" => true,
            "on_kill" => get_bool(ctx, "target_killed"),
            "on_damage" => get_bool(ctx, "caster_damaged_this_turn"),
            "one_per_turn" => !get_bool(ctx, "already_triggered_this_turn"),
            "buff_stacks" => check_buff_stacks(ctx),
            _ => false,
        };
    }

    private static bool get_bool(Dictionary ctx, string key) =>
        ctx.TryGetValue(key, out var value) && value.AsBool();

    private static bool check_buff_stacks(Dictionary ctx)
    {
        if (!ctx.TryGetValue("params", out var paramsValue) || paramsValue.VariantType != Variant.Type.Array)
            return false;
        var parameters = paramsValue.AsGodotArray();
        if (parameters.Count < 2 || !ctx.TryGetValue("caster", out var casterValue)) return false;
        var caster = casterValue.AsGodotObject();
        if (!GodotObject.IsInstanceValid(caster)) return false;
        var buffName = parameters[0].AsString();
        var minStacks = parameters[1].AsDouble();
        return caster.Call("get_buff_stack", buffName).AsDouble() >= minStacks;
    }
}
