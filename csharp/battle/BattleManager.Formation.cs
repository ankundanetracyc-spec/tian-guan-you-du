using Godot;
using Godot.Collections;

public partial class BattleManager : Node2D
{
    public void emit_formation_event(GodotObject owner, string eventId, string sourceTag, int amplitude = 1)
    {
        foreach (var circuit in _formationCircuits.GetValueOrDefault(owner, [])) circuit.emit(eventId, sourceTag, amplitude);
    }
    /// <summary>阵器最终只调用已有的战斗效果入口；不伪装成一张手牌，也不支付体力/法力。</summary>
    public void resolve_formation_actuator(GodotObject owner, FormationNodeData actuator)
    {
        var value = Mathf.Max(0, actuator.effect_value);
        switch (actuator.actuator_effect)
        {
            case FormationNodeData.ActuatorEffect.SHIELD:
                owner.Set("shield", owner.Get("shield").AsInt32() + value);
                break;
            case FormationNodeData.ActuatorEffect.RESTORE_MANA:
                owner.Set("current_mana", Mathf.Min(owner.Get("max_mana").AsInt32(), owner.Get("current_mana").AsInt32() + value));
                break;
            case FormationNodeData.ActuatorEffect.MAGIC_DAMAGE:
                var target = FindTarget(owner);
                if (!GodotObject.IsInstanceValid(target)) return;
                var damage = Mathf.Max(1, BattleActionResolver.apply_defense(value, "magic", target!));
                target!.Set("current_health", Mathf.Max(0, target.Get("current_health").AsInt32() - damage));
                if (target.HasMethod("update_ui")) target.Call("update_ui");
                show_damage_number(target, damage, "magic");
                break;
        }
    }
    public Array<Dictionary> get_formation_snapshot(GodotObject owner)
    {
        var result = new Array<Dictionary>();
        foreach (var circuit in _formationCircuits.GetValueOrDefault(owner, [])) result.Add(circuit.snapshot());
        return result;
    }
    private void initialize_formation_circuits(GodotObject owner, BattleEntityData snapshot)
    {
        var circuits = new List<FormationCircuitRuntime>();
        foreach (var data in snapshot.formation_circuits)
        {
            if (data is null || data.required_formation_proficiency > CardRules.Number(owner, "formation_proficiency")) continue;
            if (data.required_manual_ids.Any(id => !snapshot.formation_manual_ids.Contains(id))) { GD.PushWarning($"阵图 {data.circuit_name} 未载入：缺少所需阵法录端口"); continue; }
            var errors = data.validate_circuit();
            if (errors.Count > 0) { GD.PushWarning($"阵图 {data.circuit_name} 未载入：{string.Join("；", errors)}"); continue; }
            circuits.Add(new FormationCircuitRuntime(data, owner, this));
        }
        if (circuits.Count > 0) _formationCircuits[owner] = circuits;
    }
}
