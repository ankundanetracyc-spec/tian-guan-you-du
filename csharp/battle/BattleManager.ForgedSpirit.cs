using Godot;

public partial class BattleManager
{
    private readonly Dictionary<GodotObject, int> _forgedTemporarySpirit = [];
    private bool CanReserveForgedSpirit(GodotObject owner, ActiveSkillData skill) => skill.forged_spirit_cost <= 0 ||
        (long)owner.Get("used_spirit_perception").AsInt32() + skill.forged_spirit_cost <= owner.Get("max_spirit_perception").AsInt32();
    private void ReserveForgedSpirit(GodotObject owner, ActiveSkillData skill)
    {
        if (skill.forged_spirit_cost <= 0) return;
        if (!CanReserveForgedSpirit(owner, skill)) throw new InvalidOperationException("器物施法灵识不足");
        _forgedTemporarySpirit[owner] = _forgedTemporarySpirit.GetValueOrDefault(owner) + skill.forged_spirit_cost;
        owner.Set("used_spirit_perception", owner.Get("used_spirit_perception").AsInt32() + skill.forged_spirit_cost);
    }
    private void ReleaseForgedSpirit(GodotObject owner)
    {
        if (!_forgedTemporarySpirit.Remove(owner, out var count) || !GodotObject.IsInstanceValid(owner)) return;
        owner.Set("used_spirit_perception", Math.Max(0, owner.Get("used_spirit_perception").AsInt32() - count));
    }
}
